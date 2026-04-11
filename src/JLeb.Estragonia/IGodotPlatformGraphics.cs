using System;
using Avalonia.Platform;

namespace JLeb.Estragonia;

/// <summary>Interface for Godot platform graphics implementations.</summary>
internal interface IGodotPlatformGraphics : IPlatformGraphics, IDisposable {

	/// <summary>Gets the shared GPU context.</summary>
	/// <returns>The shared GPU context.</returns>
	new IGodotSkiaGpu GetSharedContext();

	/// <summary>Adds a reference to this platform graphics instance.</summary>
	void AddRef();

	/// <summary>Releases a reference to this platform graphics instance.</summary>
	void Release();

}
