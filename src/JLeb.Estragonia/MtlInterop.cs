using System;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>
/// Native interop for SkiaSharp Metal functions and GPU texture blitting.
/// </summary>
internal static class MtlInterop {

	private const string SKIA_LIBRARY = "libSkiaSharp";
	private const string OBJC_LIBRARY = "/usr/lib/libobjc.A.dylib";

	#region Objective-C Runtime

	[DllImport(OBJC_LIBRARY, EntryPoint = "sel_registerName")]
	private static extern IntPtr sel_registerName(string name);

	[DllImport(OBJC_LIBRARY, EntryPoint = "objc_msgSend")]
	private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

	[DllImport(OBJC_LIBRARY, EntryPoint = "objc_msgSend")]
	private static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector);

	[DllImport(OBJC_LIBRARY, EntryPoint = "objc_msgSend")]
	private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

	[DllImport(OBJC_LIBRARY, EntryPoint = "objc_msgSend")]
	private static extern void objc_msgSend_blit(
		IntPtr receiver, IntPtr selector,
		IntPtr sourceTexture, ulong sourceSlice, ulong sourceLevel,
		MTLOrigin sourceOrigin, MTLSize sourceSize,
		IntPtr destTexture, ulong destSlice, ulong destLevel,
		MTLOrigin destOrigin);

	[StructLayout(LayoutKind.Sequential)]
	public struct MTLOrigin {
		public ulong x, y, z;
		public static MTLOrigin Zero => new MTLOrigin { x = 0, y = 0, z = 0 };
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MTLSize {
		public ulong width, height, depth;
		public static MTLSize Create(int w, int h) => new MTLSize { width = (ulong)w, height = (ulong)h, depth = 1 };
	}

	// Cached selectors for Metal operations
	private static IntPtr _selCommandBuffer;
	private static IntPtr _selBlitCommandEncoder;
	private static IntPtr _selCopyFromTexture;
	private static IntPtr _selEndEncoding;
	private static IntPtr _selCommit;
	private static IntPtr _selWaitUntilCompleted;

	private static void EnsureSelectorsInitialized() {
		if (_selCommandBuffer != IntPtr.Zero) return;

		_selCommandBuffer = sel_registerName("commandBuffer");
		_selBlitCommandEncoder = sel_registerName("blitCommandEncoder");
		_selCopyFromTexture = sel_registerName("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:");
		_selEndEncoding = sel_registerName("endEncoding");
		_selCommit = sel_registerName("commit");
		_selWaitUntilCompleted = sel_registerName("waitUntilCompleted");
	}

	/// <summary>
	/// Performs GPU-to-GPU texture blit using Metal command buffer.
	/// </summary>
	public static bool BlitTexture(IntPtr commandQueue, IntPtr sourceTexture, IntPtr destTexture, int width, int height) {
		if (commandQueue == IntPtr.Zero || sourceTexture == IntPtr.Zero || destTexture == IntPtr.Zero)
			return false;

		try {
			EnsureSelectorsInitialized();

			// Get command buffer from queue
			var commandBuffer = objc_msgSend(commandQueue, _selCommandBuffer);
			if (commandBuffer == IntPtr.Zero) {
				Godot.GD.PrintErr("[Estragonia Metal] Failed to create command buffer");
				return false;
			}

			// Get blit command encoder
			var blitEncoder = objc_msgSend(commandBuffer, _selBlitCommandEncoder);
			if (blitEncoder == IntPtr.Zero) {
				Godot.GD.PrintErr("[Estragonia Metal] Failed to create blit encoder");
				return false;
			}

			// Copy texture
			var origin = MTLOrigin.Zero;
			var size = MTLSize.Create(width, height);

			objc_msgSend_blit(
				blitEncoder, _selCopyFromTexture,
				sourceTexture, 0, 0, origin, size,
				destTexture, 0, 0, origin
			);

			// End encoding
			objc_msgSend_void(blitEncoder, _selEndEncoding);

			// Commit and wait
			objc_msgSend_void(commandBuffer, _selCommit);
			objc_msgSend_void(commandBuffer, _selWaitUntilCompleted);

			return true;
		}
		catch (Exception ex) {
			Godot.GD.PrintErr($"[Estragonia Metal] BlitTexture failed: {ex.Message}");
			return false;
		}
	}

	#endregion

	#region SkiaSharp Backend Texture

	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr sk_surface_get_backend_texture(IntPtr surface, int mode);

	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool gr_backendtexture_get_gl_textureinfo(IntPtr texture, out IntPtr info);

	/// <summary>
	/// Gets the Metal texture handle from a Skia surface's backend texture.
	/// </summary>
	public static IntPtr GetSurfaceMetalTexture(SKSurface surface) {
		try {
			// Get the native handle of the surface
			var surfaceHandle = GetSKObjectHandle(surface);
			if (surfaceHandle == IntPtr.Zero)
				return IntPtr.Zero;

			// FlushAndSubmit mode = 1 (kFlushRead_
			var backendTexture = sk_surface_get_backend_texture(surfaceHandle, 1);
			if (backendTexture == IntPtr.Zero)
				return IntPtr.Zero;

			// The backend texture contains the Metal texture info
			// For Metal, we need to extract the texture handle
			// This is stored at a specific offset in the GrBackendTexture structure
			return GetMetalTextureFromBackend(backendTexture);
		}
		catch (Exception ex) {
			Godot.GD.PrintErr($"[Estragonia Metal] GetSurfaceMetalTexture failed: {ex.Message}");
			return IntPtr.Zero;
		}
	}

	private static IntPtr GetSKObjectHandle(SKObject obj) {
		// SKObject.Handle property
		var handleProp = typeof(SKObject).GetProperty("Handle",
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
		return handleProp?.GetValue(obj) as IntPtr? ?? IntPtr.Zero;
	}

	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool gr_backendtexture_get_mtl_textureinfo(IntPtr texture, out GRMtlTextureInfoNative info);

	private static IntPtr GetMetalTextureFromBackend(IntPtr backendTexture) {
		if (gr_backendtexture_get_mtl_textureinfo(backendTexture, out var info)) {
			return info.Texture;
		}
		return IntPtr.Zero;
	}

	#endregion

	/// <summary>
	/// Creates a GRContext for Metal.
	/// </summary>
	/// <param name="device">The MTLDevice handle.</param>
	/// <param name="queue">The MTLCommandQueue handle.</param>
	/// <returns>A handle to the GRContext, or IntPtr.Zero on failure.</returns>
	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr gr_direct_context_make_metal(IntPtr device, IntPtr queue);

	/// <summary>
	/// Creates a GRBackendRenderTarget for Metal.
	/// </summary>
	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr gr_backendrendertarget_new_metal(int width, int height, ref GRMtlTextureInfoNative mtlInfo);

	/// <summary>
	/// Deletes a GRBackendRenderTarget.
	/// </summary>
	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	public static extern void gr_backendrendertarget_delete(IntPtr handle);

	/// <summary>
	/// Creates a GRBackendTexture for Metal.
	/// </summary>
	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr gr_backendtexture_new_metal(int width, int height, bool mipmapped, ref GRMtlTextureInfoNative mtlInfo);

	/// <summary>
	/// Deletes a GRBackendTexture.
	/// </summary>
	[DllImport(SKIA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
	public static extern void gr_backendtexture_delete(IntPtr handle);

	/// <summary>
	/// Creates a GRBackendTexture for a Metal texture.
	/// </summary>
	public static GRBackendTexture? CreateMetalBackendTexture(int width, int height, bool mipmapped, IntPtr mtlTexture) {
		var textureInfo = new GRMtlTextureInfoNative { Texture = mtlTexture };
		var handle = gr_backendtexture_new_metal(width, height, mipmapped, ref textureInfo);
		if (handle == IntPtr.Zero)
			return null;

		return CreateGRBackendTextureFromHandle(handle);
	}

	/// <summary>
	/// Creates a GRBackendTexture from a native handle using reflection.
	/// </summary>
	private static GRBackendTexture? CreateGRBackendTextureFromHandle(IntPtr handle) {
		try {
			var ctors = typeof(GRBackendTexture).GetConstructors(
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
			);

			// Try (IntPtr, bool) constructor
			var ctor = ctors.FirstOrDefault(c => {
				var ps = c.GetParameters();
				return ps.Length == 2 &&
					   ps[0].ParameterType == typeof(IntPtr) &&
					   ps[1].ParameterType == typeof(bool);
			});

			if (ctor is not null)
				return ctor.Invoke(new object[] { handle, true }) as GRBackendTexture;

			// Try single IntPtr constructor
			ctor = ctors.FirstOrDefault(c => {
				var ps = c.GetParameters();
				return ps.Length == 1 && ps[0].ParameterType == typeof(IntPtr);
			});

			if (ctor is not null)
				return ctor.Invoke(new object[] { handle }) as GRBackendTexture;

			gr_backendtexture_delete(handle);
			return null;
		}
		catch {
			gr_backendtexture_delete(handle);
			return null;
		}
	}

	/// <summary>
	/// Native structure for Metal texture info.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct GRMtlTextureInfoNative {
		public IntPtr Texture;
	}

	/// <summary>
	/// Creates a GRContext from a native Metal context handle.
	/// </summary>
	public static GRContext? CreateMetalContext(IntPtr device, IntPtr queue) {
		IntPtr handle;
		try {
			handle = gr_direct_context_make_metal(device, queue);
		}
		catch {
			return null;
		}

		if (handle == IntPtr.Zero)
			return null;

		// Use reflection to create GRContext from handle
		// GRContext has an internal constructor that takes IntPtr
		return CreateGRContextFromHandle(handle);
	}

	/// <summary>
	/// Creates a GRBackendRenderTarget for a Metal texture.
	/// </summary>
	public static GRBackendRenderTarget? CreateMetalRenderTarget(int width, int height, IntPtr mtlTexture) {
		var textureInfo = new GRMtlTextureInfoNative { Texture = mtlTexture };
		var handle = gr_backendrendertarget_new_metal(width, height, ref textureInfo);
		if (handle == IntPtr.Zero)
			return null;

		return CreateGRBackendRenderTargetFromHandle(handle);
	}

	/// <summary>
	/// Creates a GRContext from a native handle using reflection.
	/// </summary>
	private static GRContext? CreateGRContextFromHandle(IntPtr handle) {
		try {
			// Try to find a constructor on GRContext that takes (IntPtr, bool)
			var ctors = typeof(GRContext).GetConstructors(
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
			);

			// Try (IntPtr, bool) constructor
			var ctor = ctors.FirstOrDefault(c => {
				var ps = c.GetParameters();
				return ps.Length == 2 &&
					   ps[0].ParameterType == typeof(IntPtr) &&
					   ps[1].ParameterType == typeof(bool);
			});

			if (ctor is not null)
				return ctor.Invoke(new object[] { handle, true }) as GRContext;

			// Try (IntPtr) constructor
			ctor = ctors.FirstOrDefault(c => {
				var ps = c.GetParameters();
				return ps.Length == 1 && ps[0].ParameterType == typeof(IntPtr);
			});

			if (ctor is not null)
				return ctor.Invoke(new object[] { handle }) as GRContext;

			// Last resort: check base class SKObject for useful patterns
			var owned = typeof(SKObject).GetMethod("Owned",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			if (owned is not null && owned.IsGenericMethod) {
				var typedOwned = owned.MakeGenericMethod(typeof(GRContext));
				var result = typedOwned.Invoke(null, new object[] { handle }) as GRContext;
				if (result is not null)
					return result;
			}

			return null;
		}
		catch {
			return null;
		}
	}

	/// <summary>
	/// Creates a GRBackendRenderTarget from a native handle using reflection.
	/// </summary>
	private static GRBackendRenderTarget? CreateGRBackendRenderTargetFromHandle(IntPtr handle) {
		// Try to find a constructor or factory method
		var ctor = typeof(GRBackendRenderTarget).GetConstructor(
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			null,
			new[] { typeof(IntPtr), typeof(bool) },
			null
		);

		if (ctor is not null) {
			return ctor.Invoke(new object[] { handle, true }) as GRBackendRenderTarget;
		}

		// Fallback: try single IntPtr constructor
		ctor = typeof(GRBackendRenderTarget).GetConstructor(
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			null,
			new[] { typeof(IntPtr) },
			null
		);

		if (ctor is not null) {
			return ctor.Invoke(new object[] { handle }) as GRBackendRenderTarget;
		}

		// Last resort: delete handle and return null
		gr_backendrendertarget_delete(handle);
		return null;
	}

}
