using System;
using Avalonia.Platform;

namespace JLeb.Estragonia;

internal sealed class GodotWindowingPlatform : IWindowingPlatform {

	public IWindowImpl CreateWindow()
		=> throw CreateNotImplementedException();

	public IWindowImpl CreateEmbeddableWindow()
		=> throw CreateNotImplementedException();

	public ITopLevelImpl CreateEmbeddableTopLevel()
		=> throw CreateNotImplementedException();

	public ITrayIconImpl? CreateTrayIcon()
		=> null;

	public void GetWindowsZOrder(ReadOnlySpan<IWindowImpl> windows, Span<long> zOrder) {
		// No multi-window support in Godot; fill with default ordering.
		zOrder.Clear();
	}

	private static NotImplementedException CreateNotImplementedException()
		=> new("Sub windows aren't implemented yet");

}
