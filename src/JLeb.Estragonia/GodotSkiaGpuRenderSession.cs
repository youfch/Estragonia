using Avalonia.Skia;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>A render session that uses an underlying Skia surface.</summary>
internal sealed class GodotSkiaGpuRenderSession : ISkiaGpuRenderSession {

	public IGodotSkiaSurface Surface { get; }

	public GRContext GrContext { get; }

	public ISurfaceSynchronizer Synchronizer { get; }

	SKSurface ISkiaGpuRenderSession.SkSurface
		=> Surface.SkSurface;

	double ISkiaGpuRenderSession.ScaleFactor
		=> Surface.RenderScaling;

	GRSurfaceOrigin ISkiaGpuRenderSession.SurfaceOrigin
		=> GRSurfaceOrigin.TopLeft;

	public GodotSkiaGpuRenderSession(IGodotSkiaSurface surface, GRContext grContext, ISurfaceSynchronizer synchronizer) {
		Surface = surface;
		GrContext = grContext;
		Synchronizer = synchronizer;

		// Prepare surface for rendering (handles texture clear and layout transitions)
		Synchronizer.PrepareForRendering(Surface);
	}

	public void Dispose() {
		// Finalize rendering (handles flush and layout transitions)
		Synchronizer.FinishRendering(Surface);
	}

}
