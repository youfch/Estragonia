using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>Bridges the Godot Metal renderer with a Skia context used by Avalonia.</summary>
internal sealed class GodotMtlSkiaGpu : IGodotSkiaGpu {

	private readonly RenderingDevice _renderingDevice;
	private readonly GRContext _grContext;
	private readonly MtlSynchronizer _synchronizer;
	private readonly IntPtr _mtlQueue;

	public bool IsLost
		=> _grContext.IsAbandoned;

	public GodotMtlSkiaGpu() {
		_renderingDevice = RenderingServer.GetRenderingDevice();

		if (_renderingDevice is null)
			throw new NotSupportedException("Estragonia is only supported on Forward+ or Mobile renderers");

		// Get Metal device and command queue from Godot
		var mtlDevice = (IntPtr)_renderingDevice.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice, default, 0UL);
		if (mtlDevice == IntPtr.Zero)
			throw new InvalidOperationException("Godot returned null for Metal device");

		_mtlQueue = (IntPtr)_renderingDevice.GetDriverResource(RenderingDevice.DriverResource.CommandQueue, default, 0UL);
		if (_mtlQueue == IntPtr.Zero)
			throw new InvalidOperationException("Godot returned null for Metal command queue");

		// Create Metal GRContext using native interop
		var grContext = MtlInterop.CreateMetalContext(mtlDevice, _mtlQueue);
		if (grContext is null)
			throw new InvalidOperationException("Couldn't create Metal context");

		_grContext = grContext;
		_synchronizer = new MtlSynchronizer();
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
		=> null;

	IDisposable IPlatformGraphicsContext.EnsureCurrent()
		=> EmptyDisposable.Instance;

	ISkiaGpuRenderTarget? ISkiaGpu.TryCreateRenderTarget(IEnumerable<object> surfaces)
		=> surfaces.OfType<GodotSkiaSurfaceMetal>().FirstOrDefault() is { } surface
			? new GodotSkiaRenderTarget(surface, _grContext, _synchronizer)
			: null;

	public IGodotSkiaSurface CreateSurface(PixelSize size, double renderScaling) {
		size = new PixelSize(Math.Max(size.Width, 1), Math.Max(size.Height, 1));

		// Create Godot texture for display - needs ColorAttachment for rendering
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
				| RenderingDevice.TextureUsageBits.ColorAttachmentBit
				| RenderingDevice.TextureUsageBits.CanCopyFromBit
				| RenderingDevice.TextureUsageBits.CanCopyToBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit
		};

		var gdRdTexture = _renderingDevice.TextureCreate(gdRdTextureFormat, new RDTextureView());

		// Get the native Metal texture handle from Godot
		var gdMetalTexture = (IntPtr)_renderingDevice.GetDriverResource(
			RenderingDevice.DriverResource.Texture,
			gdRdTexture,
			0UL
		);

		var gdTexture = new Texture2Drd {
			TextureRdRid = gdRdTexture
		};

		// Try zero-copy: create Skia surface wrapping Godot's Metal texture directly
		if (gdMetalTexture != IntPtr.Zero) {
			var surface = TryCreateZeroCopySurface(gdMetalTexture, size, gdTexture, renderScaling);
			if (surface is not null)
				return surface;
		}

		// Fallback: Create a Skia-owned GPU surface (requires copy to Godot texture)
		var imageInfo = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
		var skSurface = SKSurface.Create(_grContext, true, imageInfo, 1, GRSurfaceOrigin.TopLeft,
			new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal), false);

		if (skSurface is null) {
			GD.PrintErr("[Estragonia Metal] Failed to create Skia GPU surface, falling back to raster");
			skSurface = SKSurface.Create(imageInfo);
		}

		if (skSurface is null)
			throw new InvalidOperationException("Couldn't create Skia surface");

		return new GodotSkiaSurfaceMetal(
			skSurface,
			gdTexture,
			_renderingDevice,
			renderScaling,
			_mtlQueue,
			gdMetalTexture,
			size.Width,
			size.Height,
			isZeroCopy: false
		);
	}

	private GodotSkiaSurfaceMetal? TryCreateZeroCopySurface(
		IntPtr gdMetalTexture,
		PixelSize size,
		Texture2Drd gdTexture,
		double renderScaling
	) {
		try {
			// Create a GRBackendTexture wrapping Godot's Metal texture
			var backendTexture = MtlInterop.CreateMetalBackendTexture(
				size.Width, size.Height, false, gdMetalTexture);

			if (backendTexture is null)
				return null;

			// Create Skia surface that renders directly to Godot's texture
			var skSurface = SKSurface.Create(
				_grContext,
				backendTexture,
				GRSurfaceOrigin.TopLeft,
				SKColorType.Rgba8888);

			if (skSurface is null) {
				backendTexture.Dispose();
				return null;
			}

			return new GodotSkiaSurfaceMetal(
				skSurface,
				gdTexture,
				_renderingDevice,
				renderScaling,
				_mtlQueue,
				gdMetalTexture,
				size.Width,
				size.Height,
				isZeroCopy: true,
				backendTexture: backendTexture
			);
		}
		catch {
			return null;
		}
	}

	ISkiaSurface? ISkiaGpu.TryCreateSurface(PixelSize size, ISkiaGpuRenderSession? session)
		=> session is GodotSkiaGpuRenderSession godotSession
			? CreateSurface(size, godotSession.Surface.RenderScaling)
			: null;

	public void Dispose() {
		_grContext.Dispose();
		_synchronizer.Dispose();
	}

}
