using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Godot;
using JLeb.Estragonia.Input;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;
using AvKey = Avalonia.Input.Key;
using AvWindow = Avalonia.Controls.Window;
using GdCursorShape = Godot.Control.CursorShape;
using GdWindow = Godot.Window;

namespace JLeb.Estragonia;

internal sealed class GodotWindowImpl : IWindowImpl {

	private readonly GodotVkPlatformGraphics _platformGraphics;
	private readonly IClipboard _clipboard;
	private readonly TouchDevice _touchDevice = new();

	private GodotSkiaSurface? _surface;
	private WindowTransparencyLevel _transparencyLevel = WindowTransparencyLevel.None;
	private PixelSize _renderSize;
	private IInputRoot? _inputRoot;
	private GdCursorShape _cursorShape;
	private bool _isDisposed;
	private int _lastMouseDeviceId = GodotDevices.EmulatedDeviceId;
	private WindowState _windowState = WindowState.Normal;
	private bool _isVisible;
	private Thickness _extendedMargins = default;
	private Thickness _offScreenMargin = default;

	private GdWindow? _gdWindow;
	private int _windowId = -1;
	private GodotPlatformHandle? _platformHandle;

	public double RenderScaling { get; private set; } = 1.0;

	double ITopLevelImpl.DesktopScaling
		=> 1.0;

	public Size ClientSize { get; private set; }

	public Size MaxAutoSizeHint { get; set; } = Size.Infinity;

	public Action<Rect>? Paint { get; set; }

	public Action<Size, WindowResizeReason>? Resized { get; set; }

	public Action? Closed { get; set; }

	public Action<RawInputEventArgs>? Input { get; set; }

	public Action? LostFocus { get; set; }

	public Action<double>? ScalingChanged { get; set; }

	public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

	public Action<WindowState>? WindowStateChanged { get; set; }

	public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }

	public Action? Activated { get; set; }

	public Action? Deactivated { get; set; }

	public Action<PixelPoint>? PositionChanged { get; set; }

	public Func<WindowCloseReason, bool>? Closing { get; set; }

	public Action? GotInputWhenDisabled { get; set; }

	IEnumerable<object> ITopLevelImpl.Surfaces
		=> GetOrCreateSurfaces();

	IPlatformHandle? ITopLevelImpl.Handle
		=> _platformHandle;

	public AvCompositor Compositor { get; }

	public WindowTransparencyLevel TransparencyLevel {
		get => _transparencyLevel;
		private set {
			if (_transparencyLevel.Equals(value))
				return;

			_transparencyLevel = value;
			TransparencyLevelChanged?.Invoke(value);
		}
	}

	public bool IsClientAreaExtendedToDecorations { get; private set; }

	public bool NeedsManagedDecorations { get; private set; }

	public Thickness ExtendedMargins {
		get => _extendedMargins;
		private set {
			_extendedMargins = value;
			ExtendClientAreaToDecorationsChanged?.Invoke(IsClientAreaExtendedToDecorations);
		}
	}

	public Thickness OffScreenMargin {
		get => _offScreenMargin;
		private set => _offScreenMargin = value;
	}

	public PixelPoint Position {
		get => GetPosition();
		set => SetPosition(value);
	}

	public Size? FrameSize => ClientSize;

	AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels
		=> new(1.0, 1.0, 1.0);

	public WindowState WindowState {
		get => GetWindowState();
		set => SetWindowState(value);
	}

	public GodotWindowImpl(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_platformGraphics = platformGraphics;
		_clipboard = clipboard;
		Compositor = compositor;

		platformGraphics.AddRef();
	}

	public void InitializeGodotWindow() {
		_gdWindow = new GdWindow();
		_gdWindow.WrapControls = false;
		_gdWindow.Unresizable = false;
		_gdWindow.Borderless = false;
		_gdWindow.Transparent = false;
		_gdWindow.TransparentBg = false;

		var sceneTree = Engine.GetMainLoop() as SceneTree;
		sceneTree?.Root.AddChild(_gdWindow);

		_windowId = _gdWindow.GetWindowId();

		var nativeHandle = DisplayServer.WindowGetNativeHandle(
			DisplayServer.HandleType.WindowHandle,
			_windowId
		);
		_platformHandle = new GodotPlatformHandle((IntPtr)nativeHandle);

		_gdWindow.CloseRequested += OnCloseRequested;
		_gdWindow.SizeChanged += OnSizeChanged;
		_gdWindow.WindowInput += OnWindowInput;
		_gdWindow.FocusEntered += OnFocusEntered;
		_gdWindow.FocusExited += OnFocusExited;
	}

	private void OnCloseRequested() {
		var canClose = Closing?.Invoke(WindowCloseReason.WindowClosing) ?? true;
		if (canClose)
			Closed?.Invoke();
	}

	private void OnSizeChanged() {
		if (_gdWindow is null)
			return;

		var size = _gdWindow.Size;
		var newSize = new PixelSize(size.X, size.Y);
		SetRenderSize(newSize, RenderScaling);
	}

	private void OnWindowInput(InputEvent inputEvent) {
		if (_inputRoot is null || Input is not { } input)
			return;

		var timestamp = Time.GetTicksMsec();
		var handled = inputEvent switch {
			InputEventMouseMotion mouseMotion => OnMouseMotion(mouseMotion, timestamp),
			InputEventMouseButton mouseButton => OnMouseButton(mouseButton, timestamp),
			InputEventScreenTouch screenTouch => OnScreenTouch(screenTouch, timestamp),
			InputEventScreenDrag screenDrag => OnScreenDrag(screenDrag, timestamp),
			InputEventKey key => OnKey(key, timestamp),
			InputEventJoypadButton joypadButton => OnJoypadButton(joypadButton, timestamp),
			InputEventJoypadMotion joypadMotion => OnJoypadMotion(joypadMotion, timestamp),
			_ => false
		};

		if (handled)
			_gdWindow?.GetViewport().SetInputAsHandled();
	}

	private void OnFocusEntered() {
		Activated?.Invoke();
	}

	private void OnFocusExited() {
		Deactivated?.Invoke();
		LostFocus?.Invoke();
	}

	private GodotSkiaSurface CreateSurface() {
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(GodotWindowImpl));

		return _platformGraphics.GetSharedContext().CreateSurface(_renderSize, RenderScaling);
	}

	public GodotSkiaSurface? TryGetSurface()
		=> _surface;

	public GodotSkiaSurface GetOrCreateSurface()
		=> _surface ??= CreateSurface();

	private IEnumerable<object> GetOrCreateSurfaces()
		=> new object[] { GetOrCreateSurface() };

	public void SetRenderSize(PixelSize renderSize, double renderScaling) {
		var hasScalingChanged = RenderScaling != renderScaling;
		if (_renderSize == renderSize && !hasScalingChanged)
			return;

		var oldClientSize = ClientSize;
		var unclampedClientSize = renderSize.ToSize(renderScaling);

		ClientSize = new Size(Math.Max(unclampedClientSize.Width, 0.0), Math.Max(unclampedClientSize.Height, 0.0));
		RenderScaling = renderScaling;

		if (_renderSize != renderSize) {
			_renderSize = renderSize;

			if (_surface is not null) {
				_surface.Dispose();
				_surface = null;
			}

			if (_isDisposed)
				return;

			_surface = CreateSurface();
		}

		if (hasScalingChanged) {
			if (_surface != null)
				_surface.RenderScaling = RenderScaling;
			ScalingChanged?.Invoke(RenderScaling);
		}

		if (oldClientSize != ClientSize)
			Resized?.Invoke(ClientSize, hasScalingChanged ? WindowResizeReason.DpiChange : WindowResizeReason.Unspecified);
	}

	public void OnDraw(Rect rect)
		=> Paint?.Invoke(rect);

	private bool OnMouseMotion(InputEventMouseMotion inputEvent, ulong timestamp) {
		_lastMouseDeviceId = inputEvent.Device;

		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawPointerEventArgs(
			GodotDevices.GetMouse(inputEvent.Device),
			timestamp,
			_inputRoot,
			RawPointerEventType.Move,
			CreateRawPointerPoint(inputEvent.Position, inputEvent.Pressure, inputEvent.Tilt),
			inputEvent.GetRawInputModifiers()
		);

		input(args);

		return args.Handled;
	}

	private bool OnMouseButton(InputEventMouseButton inputEvent, ulong timestamp) {
		_lastMouseDeviceId = inputEvent.Device;

		if (_inputRoot is null || Input is not { } input)
			return false;

		RawPointerEventArgs CreateButtonArgs(RawPointerEventType type)
			=> new(
				GodotDevices.GetMouse(inputEvent.Device),
				timestamp,
				_inputRoot,
				type,
				inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
				inputEvent.GetRawInputModifiers()
			);

		RawMouseWheelEventArgs CreateWheelArgs(Vector delta)
			=> new(
				GodotDevices.GetMouse(inputEvent.Device),
				timestamp,
				_inputRoot,
				inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
				delta,
				inputEvent.GetRawInputModifiers()
			);

		var args = (inputEvent.ButtonIndex, inputEvent.Pressed) switch {
			(Godot.MouseButton.Left, true) => CreateButtonArgs(RawPointerEventType.LeftButtonDown),
			(Godot.MouseButton.Left, false) => CreateButtonArgs(RawPointerEventType.LeftButtonUp),
			(Godot.MouseButton.Right, true) => CreateButtonArgs(RawPointerEventType.RightButtonDown),
			(Godot.MouseButton.Right, false) => CreateButtonArgs(RawPointerEventType.RightButtonUp),
			(Godot.MouseButton.Middle, true) => CreateButtonArgs(RawPointerEventType.MiddleButtonDown),
			(Godot.MouseButton.Middle, false) => CreateButtonArgs(RawPointerEventType.MiddleButtonUp),
			(Godot.MouseButton.Xbutton1, true) => CreateButtonArgs(RawPointerEventType.XButton1Down),
			(Godot.MouseButton.Xbutton1, false) => CreateButtonArgs(RawPointerEventType.XButton1Up),
			(Godot.MouseButton.Xbutton2, true) => CreateButtonArgs(RawPointerEventType.XButton2Down),
			(Godot.MouseButton.Xbutton2, false) => CreateButtonArgs(RawPointerEventType.XButton2Up),
			(Godot.MouseButton.WheelUp, _) => CreateWheelArgs(new Vector(0.0, inputEvent.Factor)),
			(Godot.MouseButton.WheelDown, _) => CreateWheelArgs(new Vector(0.0, -inputEvent.Factor)),
			(Godot.MouseButton.WheelLeft, _) => CreateWheelArgs(new Vector(inputEvent.Factor, 0.0)),
			(Godot.MouseButton.WheelRight, _) => CreateWheelArgs(new Vector(-inputEvent.Factor, 0.0)),
			_ => null
		};

		if (args is null)
			return false;

		input(args);

		return args.Handled;
	}

	private bool OnScreenTouch(InputEventScreenTouch inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawTouchEventArgs(
			_touchDevice,
			timestamp,
			_inputRoot,
			inputEvent.Pressed ? RawPointerEventType.TouchBegin : RawPointerEventType.TouchEnd,
			inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
			InputModifiersProvider.GetRawInputModifiers(),
			inputEvent.Index
		);

		input(args);

		return args.Handled;
	}

	private bool OnScreenDrag(InputEventScreenDrag inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawTouchEventArgs(
			_touchDevice,
			timestamp,
			_inputRoot,
			RawPointerEventType.TouchUpdate,
			CreateRawPointerPoint(inputEvent.Position, inputEvent.Pressure, inputEvent.Tilt),
			inputEvent.GetRawInputModifiers(),
			inputEvent.Index
		);

		input(args);

		return args.Handled;
	}

	private RawPointerPoint CreateRawPointerPoint(Vector2 position, float pressure, Vector2 tilt)
		=> new() {
			Position = position.ToAvaloniaPoint() / RenderScaling,
			Twist = 0.0f,
			Pressure = pressure,
			XTilt = tilt.X * 90.0f,
			YTilt = tilt.Y * 90.0f
		};

	private bool OnKey(InputEventKey inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var keyCode = inputEvent.Keycode;
		var pressed = inputEvent.Pressed;
		var key = keyCode.ToAvaloniaKey();

		if (key != AvKey.None) {
			var args = new RawKeyEventArgs(
				GodotDevices.Keyboard,
				timestamp,
				_inputRoot,
				pressed ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp,
				key,
				inputEvent.GetRawInputModifiers(),
				inputEvent.PhysicalKeycode.ToAvaloniaPhysicalKey(),
				OS.GetKeycodeString(inputEvent.KeyLabel)
			);

			input(args);

			if (args.Handled)
				return true;
		}

		if (pressed && OS.IsKeycodeUnicode((long)keyCode)) {
			var text = Char.ConvertFromUtf32((int)inputEvent.Unicode);
			var args = new RawTextInputEventArgs(GodotDevices.Keyboard, timestamp, _inputRoot, text);

			input(args);

			if (args.Handled)
				return true;
		}

		return false;
	}

	private bool OnJoypadButton(InputEventJoypadButton inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawJoypadButtonEventArgs(
			GodotDevices.GetJoypad(inputEvent.Device),
			timestamp,
			_inputRoot,
			inputEvent.IsPressed() ? RawJoypadButtonEventType.ButtonDown : RawJoypadButtonEventType.ButtonUp,
			inputEvent.ButtonIndex
		);

		input(args);

		return args.Handled;
	}

	private bool OnJoypadMotion(InputEventJoypadMotion inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawJoypadAxisEventArgs(
			GodotDevices.GetJoypad(inputEvent.Device),
			timestamp,
			_inputRoot,
			inputEvent.Axis,
			inputEvent.AxisValue
		);

		input(args);

		return args.Handled;
	}

	void ITopLevelImpl.SetInputRoot(IInputRoot inputRoot)
		=> _inputRoot = inputRoot;

	Point ITopLevelImpl.PointToClient(PixelPoint point)
		=> point.ToPoint(RenderScaling);

	PixelPoint ITopLevelImpl.PointToScreen(Point point)
		=> PixelPoint.FromPoint(point, RenderScaling);

	void ITopLevelImpl.SetCursor(ICursorImpl? cursor) {
		var cursorShape = (cursor as GodotStandardCursorImpl)?.CursorShape ?? GdCursorShape.Arrow;
		if (_cursorShape == cursorShape)
			return;

		_cursorShape = cursorShape;
	}

	IPopupImpl? ITopLevelImpl.CreatePopup()
		=> null;

	void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels) {
		foreach (var transparencyLevel in transparencyLevels) {
			if (transparencyLevel == WindowTransparencyLevel.Transparent || transparencyLevel == WindowTransparencyLevel.None) {
				TransparencyLevel = transparencyLevel;
				return;
			}
		}
	}

	void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant themeVariant) {
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType) {
		if (featureType == typeof(IClipboard))
			return _clipboard;

		return null;
	}

	public void Show(bool activate, bool isDialog) {
		if (_gdWindow is null)
			InitializeGodotWindow();

		if (_gdWindow is null)
			return;

		if (!_isVisible) {
			_isVisible = true;
			_gdWindow.Show();
		}

		if (activate)
			_gdWindow.GrabFocus();
	}

	public void Hide() {
		if (_gdWindow is null || !_isVisible)
			return;

		_isVisible = false;
		_gdWindow.Hide();
	}

	public void Activate() {
		_gdWindow?.GrabFocus();
		Activated?.Invoke();
	}

	public void Deactivate() {
		Deactivated?.Invoke();
	}

	public void SetTopmost(bool value) {
		if (_gdWindow is null)
			return;

		_gdWindow.AlwaysOnTop = value;
	}

	public void SetTitle(string? title) {
		if (_gdWindow is null)
			return;

		_gdWindow.Title = title ?? string.Empty;
	}

	public void SetParent(IWindowImpl? parent) {
	}

	public void SetEnabled(bool enable) {
	}

	public void SetSystemDecorations(SystemDecorations decorations) {
		if (_gdWindow is null)
			return;

		_gdWindow.Borderless = decorations == SystemDecorations.None;
	}

	public void SetCanResize(bool value) {
		if (_gdWindow is null)
			return;

		_gdWindow.Unresizable = !value;
	}

	public void CanResize(bool value)
		=> SetCanResize(value);

	public void SetCanMinimize(bool value) {
	}

	public void SetCanMaximize(bool value) {
	}

	public void SetIcon(IWindowIconImpl? icon) {
	}

	public void ShowTaskbarIcon(bool value) {
	}

	public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint) {
		IsClientAreaExtendedToDecorations = extendIntoClientAreaHint;
		ExtendClientAreaToDecorationsChanged?.Invoke(extendIntoClientAreaHint);
	}

	public void SetExtendClientAreaChromeHints(ExtendClientAreaChromeHints hints) {
		NeedsManagedDecorations = hints.HasFlag(ExtendClientAreaChromeHints.PreferSystemChrome);
	}

	public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight) {
		ExtendedMargins = new Thickness(0, titleBarHeight, 0, 0);
		OffScreenMargin = new Thickness(0, titleBarHeight, 0, 0);
	}

	public void SetMinMaxSize(Size minSize, Size maxSize) {
		if (_gdWindow is null)
			return;

		if (minSize != Size.Infinity && minSize.Width > 0 && minSize.Height > 0)
			_gdWindow.MinSize = new Vector2I((int)minSize.Width, (int)minSize.Height);

		if (maxSize != Size.Infinity && maxSize.Width > 0 && maxSize.Height > 0)
			_gdWindow.MaxSize = new Vector2I((int)maxSize.Width, (int)maxSize.Height);
	}

	public void Move(PixelPoint point) {
		if (_gdWindow is null)
			return;

		_gdWindow.Position = new Vector2I(point.X, point.Y);
		PositionChanged?.Invoke(point);
	}

	public void Resize(Size size, WindowResizeReason reason = WindowResizeReason.Unspecified) {
		if (_gdWindow is null)
			return;

		_gdWindow.Size = new Vector2I((int)size.Width, (int)size.Height);
		Resized?.Invoke(size, reason);
	}

	public void SetTransparencyLevel(WindowTransparencyLevel transparencyLevel) {
		TransparencyLevel = transparencyLevel;

		if (_gdWindow is null)
			return;

		_gdWindow.Transparent = transparencyLevel == WindowTransparencyLevel.Transparent;
		_gdWindow.TransparentBg = transparencyLevel == WindowTransparencyLevel.Transparent;
	}

	public void SetSize(Size size) {
		if (_gdWindow is null)
			return;

		_gdWindow.Size = new Vector2I((int)size.Width, (int)size.Height);
	}

	public void SetPosition(PixelPoint position) {
		if (_gdWindow is null)
			return;

		_gdWindow.Position = new Vector2I(position.X, position.Y);
		PositionChanged?.Invoke(position);
	}

	public PixelPoint GetPosition() {
		if (_gdWindow is null)
			return PixelPoint.Origin;

		var pos = _gdWindow.Position;
		return new PixelPoint(pos.X, pos.Y);
	}

	public Size GetClientSize() {
		if (_gdWindow is null)
			return new Size(0, 0);

		var size = _gdWindow.Size;
		return new Size(size.X, size.Y);
	}

	public PixelPoint PointToScreen(Point point) {
		if (_gdWindow is null)
			return PixelPoint.Origin;

		var screenPos = _gdWindow.Position;
		return new PixelPoint(
			(int)(screenPos.X + point.X),
			(int)(screenPos.Y + point.Y)
		);
	}

	public Point PointToClient(PixelPoint point) {
		if (_gdWindow is null)
			return new Point(0, 0);

		var windowPos = _gdWindow.Position;
		return new Point(point.X - windowPos.X, point.Y - windowPos.Y);
	}

	public void SetWindowState(WindowState state) {
		if (_windowState == state)
			return;

		_windowState = state;

		if (_gdWindow is null)
			return;

		switch (state) {
			case WindowState.Normal:
				_gdWindow.Mode = GdWindow.ModeEnum.Windowed;
				break;
			case WindowState.Minimized:
				_gdWindow.Mode = GdWindow.ModeEnum.Minimized;
				break;
			case WindowState.Maximized:
				_gdWindow.Mode = GdWindow.ModeEnum.Maximized;
				break;
			case WindowState.FullScreen:
				_gdWindow.Mode = GdWindow.ModeEnum.Fullscreen;
				break;
		}

		WindowStateChanged?.Invoke(state);
	}

	public WindowState GetWindowState() {
		if (_gdWindow is null)
			return WindowState.Normal;

		return _gdWindow.Mode switch {
			GdWindow.ModeEnum.Windowed => WindowState.Normal,
			GdWindow.ModeEnum.Minimized => WindowState.Minimized,
			GdWindow.ModeEnum.Maximized => WindowState.Maximized,
			GdWindow.ModeEnum.Fullscreen => WindowState.FullScreen,
			_ => WindowState.Normal
		};
	}

	public void BeginMoveDrag(PointerPressedEventArgs? e) {
		if (_gdWindow is null)
			return;

		DisplayServer.WindowMoveToForeground(_windowId);
	}

	public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs? e) {
	}

	public void GetWindowsZOrder(Span<AvWindow> windows, Span<long> zOrder) {
		for (var i = 0; i < windows.Length; i++)
			zOrder[i] = i;
	}

	public void Dispose() {
		if (_isDisposed)
			return;

		_isDisposed = true;

		if (_surface is not null) {
			_surface.Dispose();
			_surface = null;
		}

		if (_gdWindow is not null) {
			_gdWindow.CloseRequested -= OnCloseRequested;
			_gdWindow.SizeChanged -= OnSizeChanged;
			_gdWindow.WindowInput -= OnWindowInput;
			_gdWindow.FocusEntered -= OnFocusEntered;
			_gdWindow.FocusExited -= OnFocusExited;

			_gdWindow.QueueFree();
			_gdWindow = null;
		}

		Closed?.Invoke();

		_platformGraphics.Release();
	}

	private sealed class GodotPlatformHandle : IPlatformHandle {
		public IntPtr Handle { get; }

		public string HandleDescriptor { get; }

		public GodotPlatformHandle(IntPtr handle) {
			Handle = handle;
			HandleDescriptor = OperatingSystem.IsWindows() ? "HWND" :
				OperatingSystem.IsMacOS() ? "NSWindow" :
				"XID";
		}
	}

}
