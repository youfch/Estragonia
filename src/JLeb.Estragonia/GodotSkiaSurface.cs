using System;
using Avalonia.Platform.Surfaces;
using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>Encapsulates a Skia surface along with the Godot texture it comes from.</summary>
internal sealed class GodotSkiaSurface : ISkiaSurface, IPlatformRenderSurface {

	public SKSurface SkSurface { get; }

	public Texture2Drd GdTexture { get; }

	public ISurfaceState SurfaceState { get; }

	public RenderingDevice RenderingDevice { get; }

	public double RenderScaling { get; set; }

	public ulong DrawCount { get; set; }

	public bool IsDisposed { get; private set; }

	SKSurface ISkiaSurface.Surface
		=> SkSurface;

	bool ISkiaSurface.CanBlit
		=> false;

	public GodotSkiaSurface(
		SKSurface skSurface,
		Texture2Drd gdTexture,
		RenderingDevice renderingDevice,
		double renderScaling,
		ISurfaceState surfaceState
	) {
		SkSurface = skSurface;
		GdTexture = gdTexture;
		RenderingDevice = renderingDevice;
		RenderScaling = renderScaling;
		SurfaceState = surfaceState;
		IsDisposed = false;
	}

	void ISkiaSurface.Blit(SKCanvas canvas)
		=> throw new NotSupportedException();

	public void Dispose() {
		if (IsDisposed)
			return;

		IsDisposed = true;
		SkSurface.Dispose();
		RenderingDevice.FreeRid(GdTexture.TextureRdRid);
		GdTexture.Dispose();
	}

}
