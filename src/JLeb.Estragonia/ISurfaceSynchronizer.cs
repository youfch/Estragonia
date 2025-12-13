using System;

namespace JLeb.Estragonia;

/// <summary>Interface for synchronizing GPU operations between Godot and Skia.</summary>
internal interface ISurfaceSynchronizer : IDisposable {

	/// <summary>Prepares the surface for Skia rendering.</summary>
	/// <param name="surface">The surface to prepare.</param>
	void PrepareForRendering(IGodotSkiaSurface surface);

	/// <summary>Finalizes rendering and prepares the surface for Godot consumption.</summary>
	/// <param name="surface">The surface to finalize.</param>
	void FinishRendering(IGodotSkiaSurface surface);

}
