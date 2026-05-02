using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>
/// <see cref="IScreenImpl"/> backed by Godot's <see cref="DisplayServer"/>.
/// Enumerates all connected screens using Godot's multi-display API.
/// </summary>
internal sealed class GodotScreenImpl : IScreenImpl {

	private sealed class GodotScreen : PlatformScreen {

		public GodotScreen()
			: base(new PlatformHandle(IntPtr.Zero, "GodotScreen")) {
		}

		public void Initialize(string displayName, double scaling, PixelRect bounds, PixelRect workingArea, bool isPrimary) {
			DisplayName = displayName;
			Scaling = scaling;
			Bounds = bounds;
			WorkingArea = workingArea;
			IsPrimary = isPrimary;
		}

	}

	private readonly GodotScreen[] _allScreens;
	private readonly GodotScreen _primaryScreen;

	public GodotScreenImpl() {
		var screenCount = DisplayServer.GetScreenCount();
		var primaryIndex = DisplayServer.GetPrimaryScreen();

		var screens = new GodotScreen[screenCount];
		GodotScreen? primary = null;

		for (int i = 0; i < screenCount; i++) {
			var size = DisplayServer.ScreenGetSize(i);
			var position = DisplayServer.ScreenGetPosition(i);
			var scaling = DisplayServer.ScreenGetScale(i);

			var bounds = new PixelRect(position.X, position.Y, size.X, size.Y);

			// Godot doesn't expose a separate working area (taskbar-free region),
			// so we use the full bounds as the working area.
			var screen = new GodotScreen();
			screen.Initialize(
				$"Screen {i}",
				scaling,
				bounds,
				bounds,
				isPrimary: i == primaryIndex
			);

			screens[i] = screen;
			if (i == primaryIndex)
				primary = screen;
		}

		_allScreens = screens;
		_primaryScreen = primary ?? screens[0];
	}

	public int ScreenCount => _allScreens.Length;

	public IReadOnlyList<Screen> AllScreens => _allScreens;

	public Action? Changed { get; set; }

	public Screen? ScreenFromWindow(IWindowBaseImpl window)
		=> _primaryScreen;

	public Screen? ScreenFromTopLevel(ITopLevelImpl topLevel)
		=> _primaryScreen;

	public Screen? ScreenFromPoint(PixelPoint point) {
		// Find the screen that contains the given point.
		foreach (var screen in _allScreens) {
			if (screen.Bounds.Contains(point))
				return screen;
		}

		// Fallback to the screen whose center is closest to the point.
		var bestScreen = _primaryScreen;
		var bestDist = long.MaxValue;

		foreach (var screen in _allScreens) {
			var center = screen.Bounds.Center;
			var dx = point.X - center.X;
			var dy = point.Y - center.Y;
			var dist = (long)dx * dx + (long)dy * dy;
			if (dist < bestDist) {
				bestDist = dist;
				bestScreen = screen;
			}
		}

		return bestScreen;
	}

	public Screen? ScreenFromRect(PixelRect rect) {
		// Find the screen that contains the largest portion of the rect.
		var bestScreen = _primaryScreen;
		var bestArea = 0;

		foreach (var screen in _allScreens) {
			var overlap = screen.Bounds.Intersect(rect);
			var area = overlap.Width * overlap.Height;
			if (area > bestArea) {
				bestArea = area;
				bestScreen = screen;
			}
		}

		return bestScreen;
	}

	public Task<bool> RequestScreenDetails()
		=> Task.FromResult(true);

}
