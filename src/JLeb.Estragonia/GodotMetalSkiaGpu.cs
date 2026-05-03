using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>Bridges the Godot Metal renderer with a Skia context used by Avalonia.</summary>
internal sealed class GodotMetalSkiaGpu : IGpuBackend {

	private readonly RenderingDevice _renderingDevice;
	private readonly GRContext _grContext;

	public bool IsLost
		=> _grContext.IsAbandoned;

	public GodotMetalSkiaGpu() {
		_renderingDevice = RenderingServer.GetRenderingDevice();

		if (_renderingDevice is null)
			throw new NotSupportedException("Estragonia is only supported on Metal renderers (macOS/iOS)");

		// On Metal, Godot returns MTLDevice via LogicalDevice and MTLCommandQueue via CommandQueue.
		// TopmostObject, PhysicalDevice, and QueueFamily all return 0 on Metal — not needed.
		var deviceHandle = (IntPtr) _renderingDevice.GetDriverResource(
			RenderingDevice.DriverResource.LogicalDevice, default, 0UL);

		var queueHandle = (IntPtr) _renderingDevice.GetDriverResource(
			RenderingDevice.DriverResource.CommandQueue, default, 0UL);

		if (deviceHandle == IntPtr.Zero)
			throw new InvalidOperationException("Godot returned null for Metal device handle");

		if (queueHandle == IntPtr.Zero)
			throw new InvalidOperationException("Godot returned null for Metal command queue handle");

		var mtlContext = new GRMtlBackendContext {
			DeviceHandle = deviceHandle,
			QueueHandle = queueHandle
		};

		if (GRContext.CreateMetal(mtlContext, new GRContextOptions { AvoidStencilBuffers = true }) is not { } grContext)
			throw new InvalidOperationException("Couldn't create Metal context");

		_grContext = grContext;
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
		=> null;

	IDisposable IPlatformGraphicsContext.EnsureCurrent()
		=> EmptyDisposable.Instance;

	public IPlatformGraphicsContext? PlatformGraphicsContext
		=> this;

	public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
		=> true;

	public ISkiaGpuRenderTarget? TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
		=> surfaces.OfType<GodotSkiaSurface>().FirstOrDefault() is { } surface
			? new GodotSkiaRenderTarget(surface, _grContext)
			: null;

	public IScopedResource<GRContext>? TryGetGrContext()
		=> ScopedResource<GRContext>.Create(_grContext, static () => { });

	public GodotSkiaSurface CreateSurface(PixelSize size, double renderScaling) {
		size = new PixelSize(Math.Max(size.Width, 1), Math.Max(size.Height, 1));

		var gdRdTextureFormat = new RDTextureFormat {
			Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
			TextureType = RenderingDevice.TextureType.Type2D,
			Width = (uint)size.Width,
			Height = (uint)size.Height,
			Depth = 1,
			ArrayLayers = 1,
			Mipmaps = 1,
			Samples = RenderingDevice.TextureSamples.Samples1,
			UsageBits = RenderingDevice.TextureUsageBits.SamplingBit
				| RenderingDevice.TextureUsageBits.CanCopyFromBit
				| RenderingDevice.TextureUsageBits.CanCopyToBit
				| RenderingDevice.TextureUsageBits.ColorAttachmentBit
		};

		var gdRdTexture = _renderingDevice.TextureCreate(gdRdTextureFormat, new RDTextureView());

		// On Metal, GetDriverResource(Texture, ...) returns a pointer to id<MTLTexture>.
		var mtlTextureHandle = (IntPtr) _renderingDevice.GetDriverResource(
			RenderingDevice.DriverResource.Texture, gdRdTexture, 0UL);

		if (mtlTextureHandle == IntPtr.Zero)
			throw new InvalidOperationException("Couldn't get Metal texture from Godot texture");

		var grMtlTextureInfo = new GRMtlTextureInfo(mtlTextureHandle);

		var skSurface = SKSurface.Create(
			_grContext,
			new GRBackendRenderTarget(size.Width, size.Height, grMtlTextureInfo),
			GRSurfaceOrigin.TopLeft,
			SKColorType.Bgra8888,
			new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal)
		);

		if (skSurface is null)
			throw new InvalidOperationException("Couldn't create Skia surface from Metal texture");

		var gdTexture = new Texture2Drd {
			TextureRdRid = gdRdTexture
		};

		// Metal doesn't need explicit layout transitions — use the no-op implementation.
		var surfaceState = new MetalSurfaceState();

		var surface = new GodotSkiaSurface(
			skSurface,
			gdTexture,
			_renderingDevice,
			renderScaling,
			surfaceState
		);

		return surface;
	}

	public ISkiaSurface? TryCreateSurface(PixelSize size, ISkiaGpuRenderSession? session)
		=> session is GodotSkiaGpuRenderSession godotSession
			? CreateSurface(size, godotSession.Surface.RenderScaling)
			: null;

	public void Dispose() {
		_grContext.Dispose();
	}

}
