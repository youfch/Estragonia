using Avalonia;
using Avalonia.Skia;

namespace JLeb.Estragonia;

/// <summary>Interface for GPU backends that bridge Godot and SkiaSharp.</summary>
internal interface IGodotSkiaGpu : ISkiaGpu {

	/// <summary>Creates a new surface for rendering.</summary>
	/// <param name="size">The size of the surface in pixels.</param>
	/// <param name="renderScaling">The render scaling factor.</param>
	/// <returns>A new Godot Skia surface.</returns>
	IGodotSkiaSurface CreateSurface(PixelSize size, double renderScaling);

}
