using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>A render session that uses an underlying Skia surface.</summary>
internal sealed class GodotSkiaGpuRenderSession : ISkiaGpuRenderSession {

	public GodotSkiaSurface Surface { get; }

	public GRContext GrContext { get; }

	SKSurface ISkiaGpuRenderSession.SkSurface
		=> Surface.SkSurface;

	double ISkiaGpuRenderSession.ScaleFactor
		=> Surface.RenderScaling;

	GRSurfaceOrigin ISkiaGpuRenderSession.SurfaceOrigin
		=> GRSurfaceOrigin.TopLeft;

	public GodotSkiaGpuRenderSession(GodotSkiaSurface surface, GRContext grContext) {
		Surface = surface;
		GrContext = grContext;

		// Clear the texture on first draw. This is already done by Avalonia, but Godot doesn't know that.
		// We need it to avoid texture corruption on first draw on AMD GPUs. It will result in a few transparent frames after resizing.
		// TODO: find a better solution.
		if (Surface.DrawCount == 0)
			Surface.RenderingDevice.TextureClear(Surface.GdTexture.TextureRdRid, new Color(0u), 0, 1, 0, 1);

		// Transition the surface to a renderable layout (e.g. Vulkan COLOR_ATTACHMENT_OPTIMAL)
		Surface.SurfaceState.TransitionToRender();
	}

	public void Dispose() {
		Surface.SkSurface.Flush(true);

		// Transition back to a shader-readable layout (e.g. Vulkan SHADER_READ_ONLY_OPTIMAL)
		Surface.SurfaceState.TransitionToRead();

		Surface.DrawCount++;
	}

}
