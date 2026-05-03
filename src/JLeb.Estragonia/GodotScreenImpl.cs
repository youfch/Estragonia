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
	private int _lastScreenWidth;
	private int _lastScreenHeight;

	public GodotScreenImpl() {
		var screenSize = DisplayServer.ScreenGetSize();
		var screenPosition = DisplayServer.ScreenGetPosition();
		var scaling = DisplayServer.ScreenGetScale();

		var bounds = new PixelRect(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y);
		var workingArea = GodotPlatform.IsMobile
			? GetMobileWorkingArea(screenPosition, screenSize)
			: bounds;

		_primaryScreen = new GodotScreen();
		_primaryScreen.Initialize("Godot Primary", scaling, bounds, workingArea, isPrimary: true);

		_allScreens = [_primaryScreen];
		_lastScreenWidth = screenSize.X;
		_lastScreenHeight = screenSize.Y;
	}

	/// <summary>
	/// Checks for screen dimension changes (e.g., orientation change on mobile)
	/// and fires the <see cref="Changed"/> event if detected.
	/// Should be called periodically (e.g., from _Process).
	/// </summary>
	public void CheckForChanges() {
		var screenSize = DisplayServer.ScreenGetSize();
		if (screenSize.X != _lastScreenWidth || screenSize.Y != _lastScreenHeight) {
			_lastScreenWidth = screenSize.X;
			_lastScreenHeight = screenSize.Y;

			var screenPosition = DisplayServer.ScreenGetPosition();
			var scaling = DisplayServer.ScreenGetScale();
			var bounds = new PixelRect(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y);
			var workingArea = GodotPlatform.IsMobile
				? GetMobileWorkingArea(screenPosition, screenSize)
				: bounds;

			_primaryScreen.Initialize("Godot Primary", scaling, bounds, workingArea, isPrimary: true);
			Changed?.Invoke();
		}
	}

	private static PixelRect GetMobileWorkingArea(Vector2I screenPosition, Vector2I screenSize) {
		// On mobile, WorkingArea should exclude system bars (status bar, navigation bar).
		// Godot doesn't expose safe area insets directly, but DisplayServer.ScreenGetUsableRect()
		// may provide this information on some platforms. Fall back to full screen bounds.
		// TODO: Use DisplayServer.ScreenGetUsableRect() when available in GodotSharp.
		return new PixelRect(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y);
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
