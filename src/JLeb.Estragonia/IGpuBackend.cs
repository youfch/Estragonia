using Avalonia;
using Avalonia.Platform;
using Avalonia.Skia;

namespace JLeb.Estragonia;

/// <summary>
/// Represents a GPU backend (Vulkan, Metal, etc.) that can create Skia surfaces
/// for rendering Avalonia content inside Godot.
/// </summary>
internal interface IGpuBackend : ISkiaGpu, IOptionalFeatureProvider {

	/// <summary>Gets whether the GPU context has been lost and must be recreated.</summary>
	new bool IsLost { get; }

	/// <summary>Creates a new render surface of the specified size.</summary>
	/// <param name="size">The pixel dimensions of the surface.</param>
	/// <param name="renderScaling">The render scaling factor.</param>
	/// <returns>A new <see cref="GodotSkiaSurface"/>.</returns>
	GodotSkiaSurface CreateSurface(PixelSize size, double renderScaling);

}
