using System;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Platform;

namespace JLeb.Estragonia {

internal sealed class GodotWindowingPlatform : IWindowingPlatform {

	public IWindowImpl CreateWindow() {
		var platformGraphics = AvaloniaLocator.Current.GetService<IPlatformGraphics>() as GodotVkPlatformGraphics;
		var clipboard = AvaloniaLocator.Current.GetService<IClipboard>();

		if (platformGraphics is null || clipboard is null)
			throw new InvalidOperationException("GodotPlatform not initialized — call UseGodot().SetupWithoutStarting() first.");

		return new GodotOverlayWindowImpl(platformGraphics, clipboard, GodotPlatform.Compositor);
	}

	public IWindowImpl CreateEmbeddableWindow()
		=> throw new NotImplementedException("Embeddable windows aren't implemented yet");

	public ITopLevelImpl CreateEmbeddableTopLevel()
		=> throw new NotImplementedException("Embeddable top levels aren't implemented yet");

	public ITrayIconImpl? CreateTrayIcon()
		=> null;

	public void GetWindowsZOrder(ReadOnlySpan<IWindowImpl> windows, Span<long> zOrder) {
		zOrder.Clear();
	}

}

}
