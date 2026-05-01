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
using AvCompositor = Avalonia.Rendering.Composition.Compositor;
using GdCursorShape = Godot.Control.CursorShape;

namespace JLeb.Estragonia;

/// <summary>
/// Implementation of Avalonia <see cref="IWindowImpl"/> that renders as an overlay
/// within an existing <see cref="AvaloniaControl"/> — no Godot <see cref="Godot.Window"/> node needed.
/// The Avalonia Window (with managed decorations) is composited into the host control's texture.
/// </summary>
internal sealed class GodotOverlayWindowImpl : IWindowImpl {

	private readonly GodotTopLevelImpl _topLevelImpl;
	private readonly GodotScreenImpl _screenImpl;

	private bool _isDisposed;
	private bool _isVisible;
	private bool _isDragging;
	private WindowState _windowState = WindowState.Normal;
	private Vector2I _position;
	private Vector2I _size = new(400, 300);
	private Vector2 _dragStartMousePos;
	private Vector2I _dragStartWindowPos;
	private Size _minSize;
	private Size _maxSize;

	// ITopLevelImpl events — forwarded from _topLevelImpl
	public Action<Rect>? Paint { get; set; }
	public Action<Size, WindowResizeReason>? Resized { get; set; }
	public Action? Closed { get; set; }
	public Action<RawInputEventArgs>? Input { get; set; }
	public Action? LostFocus { get; set; }
	public Action<double>? ScalingChanged { get; set; }
	public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

	// IWindowBaseImpl events
	public Action<PixelPoint>? PositionChanged { get; set; }
	public Action? Activated { get; set; }
	public Action? Deactivated { get; set; }

	// IWindowImpl events
	public Action<WindowState>? WindowStateChanged { get; set; }
	public Action? GotInputWhenDisabled { get; set; }
	public Func<WindowCloseReason, bool>? Closing { get; set; }
	public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }
	public Action<PlatformAllowedWindowActions>? AllowedWindowActionsChanged { get; set; }

	// ITopLevelImpl properties — delegated
	public Size ClientSize => _topLevelImpl.ClientSize;
	public double RenderScaling => _topLevelImpl.RenderScaling;
	public WindowTransparencyLevel TransparencyLevel => _topLevelImpl.TransparencyLevel;
	public AvCompositor Compositor => _topLevelImpl.Compositor;

	double ITopLevelImpl.DesktopScaling => 1.0;

	IPlatformHandle? ITopLevelImpl.Handle => null;

	AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels
		=> new(1.0, 1.0, 1.0);

	IPlatformRenderSurface[] ITopLevelImpl.Surfaces
		=> ((ITopLevelImpl)_topLevelImpl).Surfaces;

	// IWindowBaseImpl properties
	public Size? FrameSize => null;

	public Size MaxAutoSizeHint => Size.Infinity;

	// Explicit interface implementation for IWindowBaseImpl.Position (PixelPoint)
	// Public Vector2I Position below is used by OverlayWindowManager
	PixelPoint IWindowBaseImpl.Position => new PixelPoint(_position.X, _position.Y);

	// Overlay window state (used by OverlayWindowManager for compositing)
	public Vector2I OverlayPosition => _position;
	public Vector2I OverlaySize => _size;
	public bool IsVisible => _isVisible;
	public bool IsDisposed => _isDisposed;
	public bool IsDragging => _isDragging;
	internal GodotTopLevelImpl TopLevelImpl => _topLevelImpl;

	// IWindowImpl properties
	public WindowState WindowState {
		get => _windowState;
		set => _windowState = value;
	}

	public bool WindowStateGetterIsUsable => false;

	public bool IsClientAreaExtendedToDecorations => false;

	public bool NeedsManagedDecorations => true;

	public PlatformRequestedDrawnDecoration RequestedDrawnDecorations
		=> PlatformRequestedDrawnDecoration.TitleBar
			| PlatformRequestedDrawnDecoration.Border
			| PlatformRequestedDrawnDecoration.Shadow
			| PlatformRequestedDrawnDecoration.ResizeGrips;

	public Thickness ExtendedMargins => default;

	public Thickness OffScreenMargin => default;

	public PlatformAllowedWindowActions AllowedWindowActions => PlatformAllowedWindowActions.All;

	public GodotOverlayWindowImpl(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_topLevelImpl = new GodotTopLevelImpl(platformGraphics, clipboard, compositor);
		_screenImpl = new GodotScreenImpl();

		// CRITICAL: Create an initial surface BEFORE the TopLevel constructor creates
		// the CompositionTarget. The CompositionTarget captures Surfaces at creation time;
		// if no surface exists yet, it gets an empty/null surface and never renders.
		_topLevelImpl.SetRenderSize(new PixelSize(Math.Max(_size.X, 1), Math.Max(_size.Y, 1)), 1.0);

		// Forward _topLevelImpl events to our own events
		_topLevelImpl.Paint = rect => Paint?.Invoke(rect);
		_topLevelImpl.Resized = (size, reason) => Resized?.Invoke(size, reason);
		_topLevelImpl.Input = args => Input?.Invoke(args);
		_topLevelImpl.LostFocus = () => LostFocus?.Invoke();
		_topLevelImpl.ScalingChanged = scaling => ScalingChanged?.Invoke(scaling);
		_topLevelImpl.TransparencyLevelChanged = level => TransparencyLevelChanged?.Invoke(level);
		_topLevelImpl.CursorChanged = _ => { }; // Overlay cursor handled by host AvaloniaControl
	}

	// --- Show / Hide / Activate ---

	public void Show(bool activate, bool isDialog) {
		if (_isDisposed || _isVisible)
			return;

		// Register with overlay manager
		OverlayWindowManager.RegisterWindow(this);

		// Center the overlay within the host AvaloniaControl viewport
		var host = OverlayWindowManager.Host;
		if (host is not null && GodotObject.IsInstanceValid(host)) {
			var hostSize = host.Size;
			_position = new Vector2I(
				Math.Max((int)((hostSize.X - _size.X) / 2), 0),
				Math.Max((int)((hostSize.Y - _size.Y) / 2), 0)
			);
		}

		// Set initial render size
		_topLevelImpl.SetRenderSize(
			new PixelSize(Math.Max(_size.X, 1), Math.Max(_size.Y, 1)),
			1.0
		);

		_isVisible = true;

		if (activate)
			Activated?.Invoke();
	}

	public void Hide() {
		_isVisible = false;
	}

	public void Activate()
		=> OverlayWindowManager.BringToFront(this);

	public void SetTopmost(bool value) {
		// Z-order management — Phase 2
	}

	// --- IWindowImpl methods ---

	public void SetTitle(string? title) {
		// Title is rendered by Avalonia's managed decorations
	}

	public void SetParent(IWindowImpl? parent) {
		// Overlay parent — Phase 3
	}

	public void SetEnabled(bool enable) {
		// When disabled (modal dialog is open) — stub
	}

	public void SetWindowDecorations(WindowDecorations enabled) {
		// Managed decorations — Avalonia handles this
	}

	public void SetIcon(IWindowIconImpl? icon) { }

	public void ShowTaskbarIcon(bool value) { }

	public void CanResize(bool value) { }

	public void SetCanMinimize(bool value) { }

	public void SetCanMaximize(bool value) { }

	public void BeginMoveDrag(PointerPressedEventArgs e) {
		if (_isDisposed || !_isVisible)
			return;

		// e.GetPosition(null) gives the position relative to the Window root.
		// We need to convert to AvaloniaControl-local coordinates by adding the
		// window's overlay position within the host.
		var pos = e.GetPosition(null); // position relative to Window root
		_dragStartMousePos = new Vector2(
			_position.X + (float)pos.X,
			_position.Y + (float)pos.Y
		);
		_dragStartWindowPos = _position;
		_isDragging = true;
	}

	public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e) {
		// Phase 2 — resize drag not yet implemented
	}

	/// <summary>
	/// Processes a mouse motion event during an active drag.
	/// Moves the overlay window by the delta from the drag start position.
	/// </summary>
	internal void ProcessDragMotion(Vector2 currentMousePos) {
		if (!_isDragging)
			return;

		var delta = currentMousePos - _dragStartMousePos;
		_position = new Vector2I(
			_dragStartWindowPos.X + (int)delta.X,
			_dragStartWindowPos.Y + (int)delta.Y
		);
		PositionChanged?.Invoke(new PixelPoint(_position.X, _position.Y));
	}

	/// <summary>Ends an active move drag.</summary>
	internal void EndDrag() {
		_isDragging = false;
	}

	public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application) {
		_size = new Vector2I(
			Math.Max((int)clientSize.Width, 1),
			Math.Max((int)clientSize.Height, 1)
		);
		_topLevelImpl.SetRenderSize(
			new PixelSize(_size.X, _size.Y),
			1.0
		);
	}

	public void Move(PixelPoint point) {
		_position = new Vector2I(point.X, point.Y);
		PositionChanged?.Invoke(point);
	}

	public void SetMinMaxSize(Size minSize, Size maxSize) {
		_minSize = minSize;
		_maxSize = maxSize;
	}

	public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint) {
		ExtendClientAreaToDecorationsChanged?.Invoke(false);
	}

	public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight) { }

	// --- ITopLevelImpl methods (delegated to _topLevelImpl) ---

	void ITopLevelImpl.SetInputRoot(IInputRoot inputRoot)
		=> ((ITopLevelImpl)_topLevelImpl).SetInputRoot(inputRoot);

	Point ITopLevelImpl.PointToClient(PixelPoint point)
		=> ((ITopLevelImpl)_topLevelImpl).PointToClient(point);

	PixelPoint ITopLevelImpl.PointToScreen(Point point)
		=> ((ITopLevelImpl)_topLevelImpl).PointToScreen(point);

	void ITopLevelImpl.SetCursor(ICursorImpl? cursor)
		=> ((ITopLevelImpl)_topLevelImpl).SetCursor(cursor);

	IPopupImpl? ITopLevelImpl.CreatePopup()
		=> null;

	void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
		=> ((ITopLevelImpl)_topLevelImpl).SetTransparencyLevelHint(transparencyLevels);

	void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant themeVariant) { }

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType) {
		if (featureType == typeof(IScreenImpl))
			return _screenImpl;

		return ((ITopLevelImpl)_topLevelImpl).TryGetFeature(featureType);
	}

	// --- Input handling (called from AvaloniaControl overlay compositing) ---

	/// <summary>
	/// Processes a mouse motion event for this overlay window.
	/// Translates the position to window-local coordinates before forwarding.
	/// Returns true if the event was handled.
	/// </summary>
	internal bool ProcessMouseMotion(InputEventMouseMotion inputEvent, ulong timestamp) {
		var localEvent = new InputEventMouseMotion {
			Position = inputEvent.Position - new Vector2(_position.X, _position.Y),
			Relative = inputEvent.Relative,
			Pressure = inputEvent.Pressure,
			Tilt = inputEvent.Tilt,
		};
		return _topLevelImpl.OnMouseMotion(localEvent, timestamp);
	}

	/// <summary>
	/// Processes a mouse button event for this overlay window.
	/// Translates the position to window-local coordinates before forwarding.
	/// Returns true if the event was handled.
	/// </summary>
	internal bool ProcessMouseButton(InputEventMouseButton inputEvent, ulong timestamp) {
		var localEvent = new InputEventMouseButton {
			Position = inputEvent.Position - new Vector2(_position.X, _position.Y),
			ButtonIndex = inputEvent.ButtonIndex,
			Pressed = inputEvent.Pressed,
			Factor = inputEvent.Factor,
		};
		return _topLevelImpl.OnMouseButton(localEvent, timestamp);
	}

	/// <summary>
	/// Processes a key event for this overlay window.
	/// Returns true if the event was handled.
	/// </summary>
	internal bool ProcessKey(InputEventKey inputEvent, ulong timestamp)
		=> _topLevelImpl.OnKey(inputEvent, timestamp);

	/// <summary>
	/// Processes a generic input event (touch, joypad, etc).
	/// Returns true if the event was handled.
	/// </summary>
	internal bool ProcessGenericInput(InputEvent inputEvent, ulong timestamp)
		=> inputEvent switch {
			InputEventScreenTouch st => _topLevelImpl.OnScreenTouch(st, timestamp),
			InputEventScreenDrag sd => _topLevelImpl.OnScreenDrag(sd, timestamp),
			InputEventJoypadButton jb => _topLevelImpl.OnJoypadButton(jb, timestamp),
			InputEventJoypadMotion jm => _topLevelImpl.OnJoypadMotion(jm, timestamp),
			_ => false
		};

	/// <summary>
	/// Gets the rendered surface for compositing into the host.
	/// </summary>
	internal GodotSkiaSurface? TryGetSurface()
		=> _topLevelImpl.TryGetSurface();

	// --- Dispose ---

	public void Dispose() {
		if (_isDisposed)
			return;

		_isDisposed = true;
		_isVisible = false;
		_isDragging = false;

		OverlayWindowManager.UnregisterWindow(this);
		_topLevelImpl.Dispose();

		Closed?.Invoke();
	}

}
