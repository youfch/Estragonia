using System;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>
/// Metal surface synchronizer. Uses GPU-to-GPU blitting when possible.
/// </summary>
internal sealed class MtlSynchronizer : ISurfaceSynchronizer {

	private bool _gpuBlitFailed;

	/// <summary>Prepares the surface for Skia rendering.</summary>
	public void PrepareForRendering(IGodotSkiaSurface surface) {
		// Clear the texture on first draw to avoid corruption on some GPUs
		if (surface.DrawCount == 0)
			surface.RenderingDevice.TextureClear(surface.GdTexture.TextureRdRid, new Color(0u), 0, 1, 0, 1);
	}

	/// <summary>Finalizes rendering by blitting from Skia surface to Godot texture.</summary>
	public void FinishRendering(IGodotSkiaSurface surface) {
		var skSurface = surface.SkSurface;

		// Flush Skia GPU commands
		skSurface.Flush();

		// Check if this is a zero-copy surface (renders directly to Godot's texture)
		if (surface is GodotSkiaSurfaceMetal { IsZeroCopy: true }) {
			// No copy needed - Skia rendered directly to Godot's texture
			surface.DrawCount++;
			return;
		}

		// Try GPU-to-GPU blit if we have a Metal surface
		if (!_gpuBlitFailed && surface is GodotSkiaSurfaceMetal mtlSurface) {
			if (TryGpuBlit(mtlSurface)) {
				surface.DrawCount++;
				return;
			}
			// Fall back to CPU copy if GPU blit fails
			_gpuBlitFailed = true;
			GD.Print("[Estragonia Metal] GPU blit failed, falling back to CPU copy");
		}

		// CPU fallback: read pixels and upload
		CpuCopy(surface);
		surface.DrawCount++;
	}

	private static bool TryGpuBlit(GodotSkiaSurfaceMetal surface) {
		// Get Skia's Metal texture from its surface
		var skiaTexture = MtlInterop.GetSurfaceMetalTexture(surface.SkSurface);
		if (skiaTexture == IntPtr.Zero) {
			GD.PrintErr("[Estragonia Metal] Could not get Skia Metal texture");
			return false;
		}

		// Perform GPU blit from Skia texture to Godot texture
		return MtlInterop.BlitTexture(
			surface.CommandQueue,
			skiaTexture,
			surface.GdMetalTexture,
			surface.Width,
			surface.Height
		);
	}

	private static void CpuCopy(IGodotSkiaSurface surface) {
		var skSurface = surface.SkSurface;
		var canvas = skSurface.Canvas;
		var bounds = canvas.DeviceClipBounds;
		var width = bounds.Width;
		var height = bounds.Height;

		if (width <= 0 || height <= 0)
			return;

		// Read pixels using a bitmap (works for both GPU and raster surfaces)
		var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
		using var bitmap = new SKBitmap(imageInfo);

		if (skSurface.ReadPixels(imageInfo, bitmap.GetPixels(), imageInfo.RowBytes, 0, 0)) {
			// Get pixel data and upload to Godot texture
			var pixelData = bitmap.GetPixelSpan().ToArray();
			surface.RenderingDevice.TextureUpdate(
				surface.GdTexture.TextureRdRid,
				0, // layer
				pixelData
			);
		}
	}

	public void Dispose() {
		// No resources to dispose for Metal synchronizer
	}

}
