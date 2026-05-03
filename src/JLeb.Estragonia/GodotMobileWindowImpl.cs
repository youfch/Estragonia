using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Godot;
using JLeb.Estragonia.Input;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;
using AvDispatcher = Avalonia.Threading.Dispatcher;

namespace JLeb.Estragonia;

/// <summary>
/// IWindowImpl for mobile platforms where only fullscreen rendering is available.
/// No sub-windows are created — content renders within the main Godot viewport.
/// </summary>
internal sealed class GodotMobileWindowImpl : IWindowImpl {

	private readonly GodotTopLevelImpl _topLevelImpl;
	private readonly GodotScreenImpl _screenImpl;
	private bool _isDisposed;

	// IWindowImpl events
	public Action<Rect>? Paint { get; set; }
	public Action<Size, WindowResizeReason>? Resized { get; set; }
	public Action? Closed { get; set; }
	public Action<RawInputEventArgs>? Input { get; set; }
	public Action? LostFocus { get; set; }
	public Action<double>? ScalingChanged { get; set; }
	public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }
	public Action<PixelPoint>? PositionChanged { get; set; }
	public Action? Activated { get; set; }
	public Action? Deactivated { get; set; }
	public Action<WindowState>? WindowStateChanged { get; set; }
	public Action? GotInputWhenDisabled { get; set; }
	public Func<WindowCloseReason, bool>? Closing { get; set; }
	public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }
	public Action<PlatformAllowedWindowActions>? AllowedWindowActionsChanged { get; set; }

	public Size ClientSize => _topLevelImpl.ClientSize;
	public double RenderScaling => _topLevelImpl.RenderScaling;
	public WindowTransparencyLevel TransparencyLevel => _topLevelImpl.TransparencyLevel;
	public AvCompositor Compositor => _topLevelImpl.Compositor;
	double ITopLevelImpl.DesktopScaling => 1.0;
	IPlatformHandle? ITopLevelImpl.Handle => null;
	AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels => new(1.0, 1.0, 1.0);
	IPlatformRenderSurface[] ITopLevelImpl.Surfaces => ((ITopLevelImpl)_topLevelImpl).Surfaces;
	public Size? FrameSize => null;
	public PixelPoint Position { get; private set; }
	public Size MaxAutoSizeHint => Size.Infinity;

	public WindowState WindowState {
		get => WindowState.FullScreen;
		set { } // Always fullscreen on mobile
	}

	// No managed decorations on mobile
	public bool WindowStateGetterIsUsable => false;
	public bool IsClientAreaExtendedToDecorations => false;
	public bool NeedsManagedDecorations => false;
	public PlatformRequestedDrawnDecoration RequestedDrawnDecorations => PlatformRequestedDrawnDecoration.None;
	public Thickness ExtendedMargins => default;
	public Thickness OffScreenMargin => default;
	public PlatformAllowedWindowActions AllowedWindowActions => PlatformAllowedWindowActions.None;

	public GodotMobileWindowImpl(GodotPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_topLevelImpl = new GodotTopLevelImpl(platformGraphics, clipboard, compositor);
		_screenImpl = new GodotScreenImpl();
		_topLevelImpl.Paint = rect => Paint?.Invoke(rect);
		_topLevelImpl.Resized = (size, reason) => Resized?.Invoke(size, reason);
		_topLevelImpl.Input = args => Input?.Invoke(args);
		_topLevelImpl.LostFocus = () => LostFocus?.Invoke();
		_topLevelImpl.ScalingChanged = scaling => ScalingChanged?.Invoke(scaling);
		_topLevelImpl.TransparencyLevelChanged = level => TransparencyLevelChanged?.Invoke(level);

		// Initialize to fullscreen size
		var screenSize = DisplayServer.ScreenGetSize();
		var pixelSize = new PixelSize(Math.Max(screenSize.X, 1), Math.Max(screenSize.Y, 1));
		_topLevelImpl.SetRenderSize(pixelSize, 1.0);
	}

	public void Show(bool activate, bool isDialog) {
		// On mobile, the window is always visible (fullscreen)
		// No Godot.Window sub-node creation needed
	}

	public void Hide() { } // No-op on mobile
	public void Activate() { } // No-op on mobile
	public void SetTopmost(bool value) { }
	public void SetTitle(string? title) { }
	public void SetParent(IWindowImpl? parent) { }
	public void SetEnabled(bool enable) { }
	public void SetWindowDecorations(WindowDecorations enabled) { }
	public void SetIcon(IWindowIconImpl? icon) { }
	public void ShowTaskbarIcon(bool value) { }
	public void CanResize(bool value) { }
	public void SetCanMinimize(bool value) { }
	public void SetCanMaximize(bool value) { }
	public void BeginMoveDrag(PointerPressedEventArgs e) { }
	public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e) { }

	public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application) {
		var pixelSize = new PixelSize(Math.Max((int)clientSize.Width, 1), Math.Max((int)clientSize.Height, 1));
		_topLevelImpl.SetRenderSize(pixelSize, 1.0);
	}

	public void Move(PixelPoint point) { } // No-op on mobile

	public void SetMinMaxSize(Size minSize, Size maxSize) { } // No-op on mobile

	public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint) { }
	public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight) { }
	void ITopLevelImpl.SetInputRoot(IInputRoot inputRoot) => ((ITopLevelImpl)_topLevelImpl).SetInputRoot(inputRoot);
	Point ITopLevelImpl.PointToClient(PixelPoint point) => ((ITopLevelImpl)_topLevelImpl).PointToClient(point);
	PixelPoint ITopLevelImpl.PointToScreen(Point point) => ((ITopLevelImpl)_topLevelImpl).PointToScreen(point);
	void ITopLevelImpl.SetCursor(ICursorImpl? cursor) => ((ITopLevelImpl)_topLevelImpl).SetCursor(cursor);
	IPopupImpl? ITopLevelImpl.CreatePopup() => null;
	void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels) => ((ITopLevelImpl)_topLevelImpl).SetTransparencyLevelHint(transparencyLevels);
	void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant themeVariant) { }

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType) {
		if (featureType == typeof(IScreenImpl)) return _screenImpl;
		return ((ITopLevelImpl)_topLevelImpl).TryGetFeature(featureType);
	}

	public void Dispose() {
		if (_isDisposed) return;
		_isDisposed = true;
		_topLevelImpl.Dispose();
		Closed?.Invoke();
	}

}
