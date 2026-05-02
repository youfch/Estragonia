using System.Collections.Generic;
using Godot;

namespace JLeb.Estragonia;

/// <summary>
/// Static registry for overlay Avalonia windows rendered within <see cref="AvaloniaControl"/>.
/// Provides compositing so <see cref="GodotOverlayWindowImpl"/> instances are drawn
/// into the Godot scene without using Godot <see cref="Window"/> nodes.
/// </summary>
internal static class OverlayWindowManager {

	private static readonly List<GodotOverlayWindowImpl> _windows = new();
	private static AvaloniaControl? _host;

	/// <summary>Registers an <see cref="AvaloniaControl"/> as the overlay host.</summary>
	public static void RegisterHost(AvaloniaControl host)
		=> _host = host;

	/// <summary>Unregisters the overlay host when it's disposed.</summary>
	public static void UnregisterHost(AvaloniaControl host) {
		if (_host == host)
			_host = null;
	}

	/// <summary>Gets the current overlay host, or null if none registered.</summary>
	public static AvaloniaControl? Host => _host;

	/// <summary>Registers an overlay window.</summary>
	public static void RegisterWindow(GodotOverlayWindowImpl window)
		=> _windows.Add(window);

	/// <summary>Unregisters an overlay window.</summary>
	public static void UnregisterWindow(GodotOverlayWindowImpl window)
		=> _windows.Remove(window);

	/// <summary>Brings a window to the top of the Z-order.</summary>
	public static void BringToFront(GodotOverlayWindowImpl window) {
		if (_windows.Remove(window))
			_windows.Add(window);
	}

	/// <summary>Gets all registered overlay windows in Z-order (bottom to top).</summary>
	public static IReadOnlyList<GodotOverlayWindowImpl> Windows => _windows;

	/// <summary>
	/// Performs hit testing against all overlay windows (top Z-order first).
	/// Returns the window at the given point, or null if no window contains the point.
	/// The point is in Godot control coordinates relative to the host AvaloniaControl.
	/// </summary>
	public static GodotOverlayWindowImpl? HitTest(Vector2 point) {
		for (var i = _windows.Count - 1; i >= 0; i--) {
			var w = _windows[i];
			if (!w.IsVisible)
				continue;

			var pos = w.OverlayPosition;
			var size = w.OverlaySize;
			if (point.X >= pos.X && point.X < pos.X + size.X
				&& point.Y >= pos.Y && point.Y < pos.Y + size.Y) {
				return w;
			}
		}

		return null;
	}

}
