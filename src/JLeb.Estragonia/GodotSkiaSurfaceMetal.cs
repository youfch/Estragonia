using System;
using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>Encapsulates a Skia surface along with the Godot texture it comes from (Metal backend).</summary>
internal sealed class GodotSkiaSurfaceMetal : IGodotSkiaSurface {

	public SKSurface SkSurface { get; }

	public Texture2Drd GdTexture { get; }

	public RenderingDevice RenderingDevice { get; }

	public double RenderScaling { get; set; }

	public ulong DrawCount { get; set; }

	public bool IsDisposed { get; private set; }

	/// <summary>The Metal command queue handle for GPU blitting.</summary>
	public IntPtr CommandQueue { get; }

	/// <summary>The Godot texture's native Metal handle.</summary>
	public IntPtr GdMetalTexture { get; }

	/// <summary>Width of the surface in pixels.</summary>
	public int Width { get; }

	/// <summary>Height of the surface in pixels.</summary>
	public int Height { get; }

	/// <summary>True if this surface renders directly to Godot's texture (no copy needed).</summary>
	public bool IsZeroCopy { get; }

	/// <summary>The backend texture wrapping Godot's Metal texture (only for zero-copy mode).</summary>
	private GRBackendTexture? BackendTexture { get; }

	SKSurface ISkiaSurface.Surface
		=> SkSurface;

	bool ISkiaSurface.CanBlit
		=> false;

	public GodotSkiaSurfaceMetal(
		SKSurface skSurface,
		Texture2Drd gdTexture,
		RenderingDevice renderingDevice,
		double renderScaling,
		IntPtr commandQueue,
		IntPtr gdMetalTexture,
		int width,
		int height,
		bool isZeroCopy = false,
		GRBackendTexture? backendTexture = null
	) {
		SkSurface = skSurface;
		GdTexture = gdTexture;
		RenderingDevice = renderingDevice;
		RenderScaling = renderScaling;
		CommandQueue = commandQueue;
		GdMetalTexture = gdMetalTexture;
		Width = width;
		Height = height;
		IsZeroCopy = isZeroCopy;
		BackendTexture = backendTexture;
		IsDisposed = false;
	}

	void ISkiaSurface.Blit(SKCanvas canvas)
		=> throw new NotSupportedException();

	public void Dispose() {
		if (IsDisposed)
			return;

		IsDisposed = true;
		SkSurface.Dispose();
		BackendTexture?.Dispose();
		RenderingDevice.FreeRid(GdTexture.TextureRdRid);
		GdTexture.Dispose();
	}

}
