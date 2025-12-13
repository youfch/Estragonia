using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>Interface for Godot Skia surfaces that can be used for rendering.</summary>
internal interface IGodotSkiaSurface : ISkiaSurface {

	/// <summary>Gets the underlying Skia surface.</summary>
	SKSurface SkSurface { get; }

	/// <summary>Gets the Godot texture.</summary>
	Texture2Drd GdTexture { get; }

	/// <summary>Gets the Godot rendering device.</summary>
	RenderingDevice RenderingDevice { get; }

	/// <summary>Gets or sets the render scaling factor.</summary>
	double RenderScaling { get; set; }

	/// <summary>Gets or sets the number of times this surface has been drawn to.</summary>
	ulong DrawCount { get; set; }

	/// <summary>Gets or sets whether this surface has been disposed.</summary>
	bool IsDisposed { get; }

}
