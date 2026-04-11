﻿using System;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;

namespace JLeb.Estragonia;

internal sealed class GodotWindowingPlatform : IWindowingPlatform {

	private readonly GodotVkPlatformGraphics _platformGraphics;
	private readonly IClipboard _clipboard;
	private readonly AvCompositor _compositor;

	public GodotWindowingPlatform(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_platformGraphics = platformGraphics;
		_clipboard = clipboard;
		_compositor = compositor;
	}

	public IWindowImpl CreateWindow()
		=> new GodotWindowImpl(_platformGraphics, _clipboard, _compositor);

	public IWindowImpl CreateEmbeddableWindow()
		=> throw CreateNotImplementedException();

	public ITopLevelImpl CreateEmbeddableTopLevel()
		=> throw CreateNotImplementedException();

	private static NotImplementedException CreateNotImplementedException()
		=> new("Sub windows aren't implemented yet");

	public ITrayIconImpl? CreateTrayIcon()
		=> null;

}
