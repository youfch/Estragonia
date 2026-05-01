using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>
/// Minimal <see cref="IScreenImpl"/> backed by Godot's <see cref="DisplayServer"/>.
/// Reports the primary screen based on Godot's current window/screen info.
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

	private readonly GodotScreen _primaryScreen;
	private readonly GodotScreen[] _allScreens;

	public GodotScreenImpl() {
		var screenSize = DisplayServer.ScreenGetSize();
		var screenPosition = DisplayServer.ScreenGetPosition();
		var scaling = DisplayServer.ScreenGetScale();

		var bounds = new PixelRect(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y);

		_primaryScreen = new GodotScreen();
		_primaryScreen.Initialize("Godot Primary", scaling, bounds, bounds, isPrimary: true);

		_allScreens = [_primaryScreen];
	}

	public int ScreenCount => 1;

	public IReadOnlyList<Screen> AllScreens => _allScreens;

	public Action? Changed { get; set; }

	public Screen? ScreenFromWindow(IWindowBaseImpl window)
		=> _primaryScreen;

	public Screen? ScreenFromTopLevel(ITopLevelImpl topLevel)
		=> _primaryScreen;

	public Screen? ScreenFromPoint(PixelPoint point)
		=> _primaryScreen;

	public Screen? ScreenFromRect(PixelRect rect)
		=> _primaryScreen;

	public Task<bool> RequestScreenDetails()
		=> Task.FromResult(true);

}
