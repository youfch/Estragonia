using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>Godot <see cref="IPlatformGraphics"/> implementation with backend selection.</summary>
internal sealed class GodotPlatformGraphics : IPlatformGraphics, IDisposable {

	private IGpuBackend? _context;
	private int _refCount;

	bool IPlatformGraphics.UsesSharedContext
		=> true;

	public IGpuBackend GetSharedContext() {
		if (Volatile.Read(ref _refCount) == 0)
			ThrowDisposed();

		if (_context is null || _context.IsLost) {
			_context?.Dispose();
			_context = null;
			_context = CreateGpuBackend();
		}

		return _context;
	}

	/// <summary>
	/// Detects the active rendering backend and creates the appropriate <see cref="IGpuBackend"/>.
	/// On macOS/iOS, Godot uses Metal — detected by checking if the LogicalDevice driver resource
	/// is non-zero while TopmostObject (VkInstance) is zero. On other platforms, Vulkan is used.
	/// </summary>
	private static IGpuBackend CreateGpuBackend() {
		var renderingDevice = RenderingServer.GetRenderingDevice();

		if (renderingDevice is null)
			throw new NotSupportedException(
				"Estragonia requires a GPU renderer (Forward+, Mobile, or Metal). " +
				"The Compatibility renderer (OpenGL) is not supported.");

		// On Vulkan, TopmostObject returns a non-zero VkInstance handle.
		// On Metal, TopmostObject returns 0 (Metal has no instance concept),
		// but LogicalDevice returns a non-zero MTLDevice handle.
		var topmostHandle = (IntPtr) renderingDevice.GetDriverResource(
			RenderingDevice.DriverResource.TopmostObject, default, 0UL);

		if (topmostHandle != IntPtr.Zero) {
			// Vulkan backend: TopmostObject = VkInstance
			return new GodotVkSkiaGpu();
		}

		// Metal backend: TopmostObject = 0, LogicalDevice = MTLDevice
		return new GodotMetalSkiaGpu();
	}

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowDisposed()
		=> throw new ObjectDisposedException(nameof(GodotPlatformGraphics));

	IPlatformGraphicsContext IPlatformGraphics.CreateContext()
		=> throw new NotSupportedException();

	IPlatformGraphicsContext IPlatformGraphics.GetSharedContext()
		=> GetSharedContext();

	public void AddRef()
		=> Interlocked.Increment(ref _refCount);

	public void Release() {
		if (Interlocked.Decrement(ref _refCount) == 0)
			Dispose();
	}


	public void Dispose() {
		if (_context is not null) {
			_context.Dispose();
			_context = null;
		}
	}
}
