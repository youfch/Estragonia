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
using GdCursorShape = Godot.Control.CursorShape;

namespace JLeb.Estragonia;

/// <summary>
/// Implementation of Avalonia <see cref="IWindowImpl"/> that creates a Godot sub-window.
/// Each instance wraps a Godot <see cref="Godot.Window"/> node with an embedded Avalonia rendering surface.
/// </summary>
internal sealed class GodotWindowImpl : IWindowImpl {

	private readonly GodotTopLevelImpl _topLevelImpl;
	private readonly Godot.Window _gdWindow;
	private readonly WindowHostControl _hostControl;

	private bool _isDisposed;
	private WindowState _windowState = WindowState.Normal;
	private bool _isVisible;

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

	public PixelPoint Position { get; private set; }

	public Size MaxAutoSizeHint => Size.Infinity;

	// IWindowImpl properties
	public WindowState WindowState {
		get => _windowState;
		set {
			_windowState = value;
			_gdWindow.Mode = value switch {
				WindowState.Normal => Godot.Window.ModeEnum.Windowed,
				WindowState.Maximized => Godot.Window.ModeEnum.Maximized,
				WindowState.Minimized => Godot.Window.ModeEnum.Minimized,
				WindowState.FullScreen => Godot.Window.ModeEnum.Fullscreen,
				_ => Godot.Window.ModeEnum.Windowed
			};
		}
	}

	public bool WindowStateGetterIsUsable => false;

	public bool IsClientAreaExtendedToDecorations => false;

	public bool NeedsManagedDecorations => true;

	public PlatformRequestedDrawnDecoration RequestedDrawnDecorations => PlatformRequestedDrawnDecoration.None;

	public Thickness ExtendedMargins => default;

	public Thickness OffScreenMargin => default;

	public PlatformAllowedWindowActions AllowedWindowActions => PlatformAllowedWindowActions.All;

	public GodotWindowImpl(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_topLevelImpl = new GodotTopLevelImpl(platformGraphics, clipboard, compositor);

		// Forward _topLevelImpl events to our own events
		_topLevelImpl.Paint = rect => Paint?.Invoke(rect);
		_topLevelImpl.Resized = (size, reason) => Resized?.Invoke(size, reason);
		_topLevelImpl.Input = args => Input?.Invoke(args);
		_topLevelImpl.LostFocus = () => LostFocus?.Invoke();
		_topLevelImpl.ScalingChanged = scaling => ScalingChanged?.Invoke(scaling);
		_topLevelImpl.TransparencyLevelChanged = level => TransparencyLevelChanged?.Invoke(level);
		_topLevelImpl.CursorChanged = cursorShape => _hostControl?.SetCursor(cursorShape);

		// Create the Godot Window (hidden initially)
		_gdWindow = new Godot.Window {
			Title = string.Empty,
			Visible = false,
			WrapControls = true,
			MinSize = new Vector2I(100, 50),
		};

		// Create the host control inside the window
		_hostControl = new WindowHostControl(this);
		_gdWindow.AddChild(_hostControl);

		// Connect Godot Window signals
		_gdWindow.CloseRequested += OnCloseRequested;
		_gdWindow.SizeChanged += OnSizeChanged;
		_gdWindow.WindowInput += OnWindowInput;
	}

	// --- Show / Hide / Activate ---

	public void Show(bool activate, bool isDialog) {
		if (_isDisposed)
			return;

		var sceneTree = (SceneTree)Engine.GetMainLoop();
		sceneTree.Root.AddChild(_gdWindow);

		// Set initial render size
		var size = _gdWindow.Size;
		_topLevelImpl.SetRenderSize(
			new PixelSize(Math.Max((int)size.X, 1), Math.Max((int)size.Y, 1)),
			1.0
		);

		_gdWindow.Visible = true;
		_isVisible = true;

		if (activate)
			_gdWindow.GrabFocus();
	}

	public void Hide() {
		_gdWindow.Visible = false;
		_isVisible = false;
	}

	public void Activate()
		=> _gdWindow.GrabFocus();

	public void SetTopmost(bool value) {
		// Godot doesn't have a direct always-on-top API for sub-windows
	}

	// --- IWindowImpl methods ---

	public void SetTitle(string? title)
		=> _gdWindow.Title = title ?? string.Empty;

	public void SetParent(IWindowImpl? parent) {
		// Godot sub-windows don't have a direct parent relationship
		// TODO: implement modal behavior via transient window
	}

	public void SetEnabled(bool enable) {
		// When disabled (modal dialog is open), the host control stops processing input
		_hostControl.SetEnabled(enable);
	}

	public void SetWindowDecorations(WindowDecorations enabled)
		=> _gdWindow.Borderless = enabled == WindowDecorations.None;

	public void SetIcon(IWindowIconImpl? icon) { }

	public void ShowTaskbarIcon(bool value) { }

	public void CanResize(bool value)
		=> _gdWindow.Unresizable = !value;

	public void SetCanMinimize(bool value) { }

	public void SetCanMaximize(bool value) { }

	public void BeginMoveDrag(PointerPressedEventArgs e) { }

	public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e) { }

	public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application) {
		_gdWindow.Size = new Vector2I(
			Math.Max((int)clientSize.Width, 1),
			Math.Max((int)clientSize.Height, 1)
		);
	}

	public void Move(PixelPoint point) {
		_gdWindow.Position = new Vector2I(point.X, point.Y);
		Position = point;
	}

	public void SetMinMaxSize(Size minSize, Size maxSize) {
		_gdWindow.MinSize = new Vector2I(
			Math.Max((int)minSize.Width, 0),
			Math.Max((int)minSize.Height, 0)
		);

		if (maxSize.Width > 0 && maxSize.Height > 0)
			_gdWindow.MaxSize = new Vector2I((int)maxSize.Width, (int)maxSize.Height);
	}

	public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint) { }

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

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
		=> ((ITopLevelImpl)_topLevelImpl).TryGetFeature(featureType);

	// --- Godot signal handlers ---

	private void OnCloseRequested() {
		var closing = Closing;
		if (closing is not null) {
			var shouldCancel = closing(WindowCloseReason.WindowClosing);
			if (shouldCancel)
				return;
		}

		Dispose();
	}

	private void OnSizeChanged() {
		var size = _gdWindow.Size;
		var pixelSize = new PixelSize(Math.Max((int)size.X, 1), Math.Max((int)size.Y, 1));
		_topLevelImpl.SetRenderSize(pixelSize, 1.0);
	}

	private void OnWindowInput(InputEvent @event) {
		// Window-level input — forward to host control's input handling
		if (_hostControl != null && !_isDisposed) {
			var handled = @event switch {
				InputEventMouseMotion mm => _topLevelImpl.OnMouseMotion(mm, Time.GetTicksMsec()),
				InputEventMouseButton mb => _topLevelImpl.OnMouseButton(mb, Time.GetTicksMsec()),
				InputEventKey k => _topLevelImpl.OnKey(k, Time.GetTicksMsec()),
				_ => false
			};
		}
	}

	// --- Dispose ---

	public void Dispose() {
		if (_isDisposed)
			return;

		_isDisposed = true;

		// Remove from scene tree
		if (GodotObject.IsInstanceValid(_gdWindow) && _gdWindow.IsInsideTree()) {
			_gdWindow.GetParent()?.RemoveChild(_gdWindow);
			_gdWindow.QueueFree();
		}

		_topLevelImpl.Dispose();

		Closed?.Invoke();
	}

	// --- Inner class: host control inside the Godot Window ---

	private sealed class WindowHostControl : Godot.Control {

		private readonly GodotWindowImpl _owner;
		private bool _isEnabled = true;

		public WindowHostControl(GodotWindowImpl owner) {
			_owner = owner;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}

		public void SetEnabled(bool enabled)
			=> _isEnabled = enabled;

		public override void _Ready() {
			if (Engine.IsEditorHint())
				return;

			Material = new CanvasItemMaterial {
				BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha,
				LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
			};

			// Set initial render size based on window
			var window = GetWindow();
			if (window != null) {
				var size = window.Size;
				_owner._topLevelImpl.SetRenderSize(
					new PixelSize(Math.Max((int)size.X, 1), Math.Max((int)size.Y, 1)),
					1.0
				);
			}
		}

		public override void _Process(double delta) {
			if (_owner._isDisposed)
				return;

			GodotPlatform.TriggerRenderTick();
			AvDispatcher.UIThread.RunJobs();

			var size = Size;
			_owner._topLevelImpl.OnDraw(new Rect(new Size(size.X, size.Y)));
		}

		public override void _Draw() {
			if (_owner._isDisposed)
				return;

			var surface = _owner._topLevelImpl.TryGetSurface();
			if (surface != null)
				DrawTexture(surface.GdTexture, Vector2.Zero);
		}

		public override void _GuiInput(InputEvent @event) {
			if (_owner._isDisposed || !_isEnabled) {
				if (!_isEnabled)
					_owner.GotInputWhenDisabled?.Invoke();
				return;
			}

			var handled = @event switch {
				InputEventMouseMotion mm => _owner._topLevelImpl.OnMouseMotion(mm, Time.GetTicksMsec()),
				InputEventMouseButton mb => _owner._topLevelImpl.OnMouseButton(mb, Time.GetTicksMsec()),
				InputEventScreenTouch st => _owner._topLevelImpl.OnScreenTouch(st, Time.GetTicksMsec()),
				InputEventScreenDrag sd => _owner._topLevelImpl.OnScreenDrag(sd, Time.GetTicksMsec()),
				InputEventKey k => _owner._topLevelImpl.OnKey(k, Time.GetTicksMsec()),
				InputEventJoypadButton jb => _owner._topLevelImpl.OnJoypadButton(jb, Time.GetTicksMsec()),
				InputEventJoypadMotion jm => _owner._topLevelImpl.OnJoypadMotion(jm, Time.GetTicksMsec()),
				_ => false
			};

			if (handled)
				AcceptEvent();
		}

		public void SetCursor(GdCursorShape cursorShape)
			=> MouseDefaultCursorShape = cursorShape;
	}
}
