using System;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Platform;

namespace JLeb.Estragonia;

/// <summary>
/// Windowing platform for mobile Godot mode.
/// Creates fullscreen-only windows without OS sub-window support.
/// </summary>
internal sealed class GodotMobileWindowingPlatform : IWindowingPlatform {

	public IWindowImpl CreateWindow() {
		var platformGraphics = AvaloniaLocator.Current.GetService<IPlatformGraphics>() as GodotPlatformGraphics;
		var clipboard = AvaloniaLocator.Current.GetService<IClipboard>();

		if (platformGraphics is null || clipboard is null)
			throw new InvalidOperationException("GodotPlatform not initialized — call UseGodot().SetupWithoutStarting() first.");

		return new GodotMobileWindowImpl(platformGraphics, clipboard, GodotPlatform.Compositor);
	}

	public IWindowImpl CreateEmbeddableWindow()
		=> throw new NotImplementedException("Embeddable windows aren't supported on mobile");

	public ITopLevelImpl CreateEmbeddableTopLevel()
		=> throw new NotImplementedException("Embeddable top levels aren't supported on mobile");

	public ITrayIconImpl? CreateTrayIcon()
		=> null;

	public void GetWindowsZOrder(ReadOnlySpan<IWindowImpl> windows, Span<long> zOrder) {
		zOrder.Clear();
	}

}
