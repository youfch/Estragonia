using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Godot;
using Godot.NativeInterop;
using JLeb.Estragonia.Input;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;
using AvDispatcher = Avalonia.Threading.Dispatcher;
using GdCursorShape = Godot.Control.CursorShape;

namespace JLeb.Estragonia;

/// <summary>
/// IWindowImpl using a Godot Window node with native OS decorations.
/// Drag, resize, maximize, minimize are all handled by the OS/Godot.
/// Avalonia content is rendered inside the client area.
/// </summary>
	internal sealed class GodotWindowImpl : IWindowImpl {

		private readonly GodotTopLevelImpl _topLevelImpl;
		private readonly Godot.Window _gdWindow;
		private readonly WindowHostControl _hostControl;
		private readonly GodotScreenImpl _screenImpl;

		private bool _isDisposed;
		private WindowState _windowState = WindowState.Normal;
		private bool _isVisible;
		private bool _unresizable;
		private Vector2I _pendingSize = new(400, 300);
		private GodotWindowImpl? _parentImpl;
		private readonly bool _isManagedDialog;

		// Tracks the last PixelSize pushed from _Process to detect external size changes
		// (user resize, maximize) vs Avalonia-driven changes (SizeToContent layout).
		// This prevents feedback loops where SetRenderSize → Resized → layout → Resize
		// causes the window to grow each frame.
		// Initialized to (-1,-1) so that _Process always pushes the first real size
		// (even if the window starts at 0x0 or matches the default PixelSize).
		private PixelSize _lastProcessRenderSize = new(-1, -1);

		// For SizeToContent windows, we need to re-center after the first layout pass
		// determines the actual content size (which differs from the initial 400×300).
		private bool _needsRecenter;

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

	// OS handles decorations — no managed decorations needed
	public bool WindowStateGetterIsUsable => false;
	public bool IsClientAreaExtendedToDecorations => false;
	public bool NeedsManagedDecorations => false;
	public PlatformRequestedDrawnDecoration RequestedDrawnDecorations => PlatformRequestedDrawnDecoration.None;
	public Thickness ExtendedMargins => default;
	public Thickness OffScreenMargin => default;
	public PlatformAllowedWindowActions AllowedWindowActions => PlatformAllowedWindowActions.All;

		private static int _dialogSeq; // per-instance sequence for log correlation
		// Tracks the maximum Y seen from Avalonia layout for managed dialogs.
		// Avalonia's ManagedFileChooser accumulates internal state (QuickLinks/volumes)
		// across opens, causing layout to measure progressively larger Y values.
		// We clamp to the first measured Y to prevent this growth.
		private static int s_clampedDialogHeight;

	public GodotWindowImpl(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_isManagedDialog = GodotPlatform.IsManagedDialogWindow;
		if (_isManagedDialog)
			GD.Print($"[Dialog#{Interlocked.Increment(ref _dialogSeq)}] new GodotWindowImpl");
		_topLevelImpl = new GodotTopLevelImpl(platformGraphics, clipboard, compositor);
		_screenImpl = new GodotScreenImpl();
		_topLevelImpl.Paint = rect => Paint?.Invoke(rect);
		_topLevelImpl.Resized = (size, reason) => Resized?.Invoke(size, reason);
		_topLevelImpl.Input = args => Input?.Invoke(args);
		_topLevelImpl.LostFocus = () => LostFocus?.Invoke();
		_topLevelImpl.ScalingChanged = scaling => ScalingChanged?.Invoke(scaling);
		_topLevelImpl.TransparencyLevelChanged = level => TransparencyLevelChanged?.Invoke(level);

		_topLevelImpl.SetRenderSize(new PixelSize(400, 300), 1.0);

		_gdWindow = new Godot.Window {
			Title = string.Empty,
			Visible = false,
			// Keep native OS decorations — Godot/OS handles drag, resize, maximize, minimize
			Borderless = false,
			Transparent = false,
			InitialPosition = Godot.Window.WindowInitialPosition.Absolute,
			WrapControls = false,
			MinSize = new Vector2I(100, 50),
			Size = new Vector2I(400, 300)
		};
		_hostControl = new WindowHostControl(this);
		_gdWindow.AddChild(_hostControl);
		_topLevelImpl.CursorChanged = cursorShape => _hostControl.SetCursor(cursorShape);
		_gdWindow.CloseRequested += OnCloseRequested;
		_gdWindow.SizeChanged += OnSizeChanged;
		_gdWindow.WindowInput += OnWindowInput;
		_gdWindow.FilesDropped += OnFilesDropped;
	}

	public void Show(bool activate, bool isDialog) {
		if (_isDisposed || _isVisible) return;
		var sceneTree = (SceneTree)Engine.GetMainLoop();

		if (_isManagedDialog)
			GD.Print($"[Dialog] Show: pendingSize={_pendingSize}, lastRender={_lastProcessRenderSize}");

		sceneTree.Root.GuiEmbedSubwindows = false;

		// Determine if this window should be modal (block input to parent).
		// isDialog: set by Avalonia's ShowDialog().
		// _isManagedDialog: set when created via ManagedFileDialogOptions.ContentRootFactory
		//   (managed file dialogs that use Show() instead of ShowDialog() because the
		//   parent TopLevel is GodotTopLevel, not an Avalonia Window).
		// _unresizable && _parentImpl is null: fallback heuristic for other dialog-like windows.
		var modal = isDialog || (_isManagedDialog && _parentImpl is null) || (_unresizable && _parentImpl is null);

		// Always add sub-windows as siblings under the root viewport.
		// Godot's Transient + Exclusive provides modal semantics via window IDs,
		// not node hierarchy — nesting creates incorrect scene tree structure.
		sceneTree.Root.AddChild(_gdWindow);

		// Transient: stays on top of parent, focus returns on close
		// Exclusive: blocks ALL input to parent (Godot modal mechanism)
		if (modal) {
			_gdWindow.Transient = true;
			_gdWindow.Exclusive = true;
		}

		// Apply pending size AFTER AddChild so the window is registered
		// in DisplayServer before OnSizeChanged fires.
		_gdWindow.Size = _pendingSize;

		// Defer initial positioning (Godot bug #89372)
		// Center relative to the main window's actual screen position,
		// not the screen origin (0,0).
		var mainWinId = sceneTree.Root.GetWindowId();
		var mainWinPos = DisplayServer.WindowGetPosition(mainWinId);
		var mainWinSize = sceneTree.Root.Size;
		var subWinSize = _gdWindow.Size;
		var centerPos = new Vector2I(
			mainWinPos.X + Math.Max((mainWinSize.X - subWinSize.X) / 2, 0),
			mainWinPos.Y + Math.Max((mainWinSize.Y - subWinSize.Y) / 2, 0)
		);
		_gdWindow.CallDeferred(Godot.Window.MethodName.SetPosition, centerPos);

		var size = _gdWindow.Size;
		_lastProcessRenderSize = new PixelSize(Math.Max((int)size.X, 1), Math.Max((int)size.Y, 1));
		// Use UpdateClientSize (no Resized event) to sync the render surface.
		// SetRenderSize would fire Resized → layout → Resize, which can cause
		// SizeToContent windows to accumulate Y-axis growth across dialog opens.
		_topLevelImpl.UpdateClientSize(_lastProcessRenderSize, 1.0);
		// For SizeToContent windows (e.g. managed file dialogs), the initial 400×300
		// will be replaced by Avalonia's layout-determined size on the first _Process tick.
		// Flag that we need to re-center after that happens.
		if (_isManagedDialog)
			_needsRecenter = true;
		_gdWindow.Visible = true;
		_isVisible = true;
		if (activate) _gdWindow.GrabFocus();
	}

	public void Hide() { _gdWindow.Visible = false; _isVisible = false; }
	public void Activate() => _gdWindow.GrabFocus();
	public void SetTopmost(bool value) { }
	public void SetTitle(string? title) => _gdWindow.Title = title ?? string.Empty;
	public void SetParent(IWindowImpl? parent) => _parentImpl = parent as GodotWindowImpl;
	public void SetEnabled(bool enable) => _hostControl.SetEnabled(enable);
	public void SetWindowDecorations(WindowDecorations enabled) => _gdWindow.Borderless = enabled == WindowDecorations.None;
	public void SetIcon(IWindowIconImpl? icon) { }
	public void ShowTaskbarIcon(bool value) { }
	public void CanResize(bool value) {
		_unresizable = !value;
		_gdWindow.Unresizable = !value;
	}
	public void SetCanMinimize(bool value) { }
	public void SetCanMaximize(bool value) { }

	// OS handles drag/resize natively — these are no-ops
	public void BeginMoveDrag(PointerPressedEventArgs e) { }
	public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e) { }

	public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application) {
		// Avalonia's ManagedFileChooser accumulates internal state across dialog opens,
		// causing layout to measure progressively larger Y values. Clamp to the first
		// measured height to prevent this growth while still allowing Avalonia to
		// determine the initial size via SizeToContent.WidthAndHeight.
		if (_isManagedDialog && clientSize.Height > 0) {
			var clampedH = (int)clientSize.Height;
			if (s_clampedDialogHeight == 0) {
				// First open — record the height as the canonical maximum.
				s_clampedDialogHeight = clampedH;
			} else if (clampedH > s_clampedDialogHeight) {
				// Subsequent open grew — clamp to the first measured height.
				clientSize = clientSize.WithHeight(s_clampedDialogHeight);
			}
		}
		var pixelSize = new Vector2I(Math.Max((int)clientSize.Width, 1), Math.Max((int)clientSize.Height, 1));
		var pxSize = new PixelSize(pixelSize.X, pixelSize.Y);
		if (_isManagedDialog)
			GD.Print($"[Dialog] Resize: clientSize={clientSize}, reason={reason}, lastRender={_lastProcessRenderSize}, visible={_isVisible}, clamp={s_clampedDialogHeight}");
		_pendingSize = pixelSize;
		if (_isVisible && _gdWindow.IsInsideTree())
			_gdWindow.Size = pixelSize;
		// Record the size so _Process doesn't re-push it back to Avalonia,
		// which would cause a feedback loop with SizeToContent windows.
		_lastProcessRenderSize = pxSize;
		// Update the render surface to match the new size.
		// Do NOT use _topLevelImpl.SetRenderSize here — that fires Resized which
		// triggers Avalonia layout which calls Resize() again, causing Y-axis growth
		// with SizeToContent windows on repeated dialog opens.
		_topLevelImpl.UpdateClientSize(pxSize, 1.0);
	}

	public void Move(PixelPoint point) {
		if (_isVisible && _gdWindow.IsInsideTree())
			DisplayServer.WindowSetPosition(new Vector2I(point.X, point.Y), _gdWindow.GetWindowId());
		Position = point;
	}

	public void SetMinMaxSize(Size minSize, Size maxSize) {
		_gdWindow.MinSize = new Vector2I(Math.Max((int)minSize.Width, 0), Math.Max((int)minSize.Height, 0));
		if (maxSize.Width > 0 && maxSize.Height > 0) _gdWindow.MaxSize = new Vector2I((int)maxSize.Width, (int)maxSize.Height);
	}

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

	private void OnCloseRequested() {
		var closing = Closing;
		if (closing is not null) { if (closing(WindowCloseReason.WindowClosing)) return; }
		Dispose();
	}

	private void OnSizeChanged() {
		if (_isDisposed) return;

		var size = _gdWindow.Size;
		var pixelSize = new PixelSize(Math.Max((int)size.X, 1), Math.Max((int)size.Y, 1));

		// Only push the size to Avalonia if it changed externally (user resize,
		// maximize, etc.). Skip when Resize() already updated _lastProcessRenderSize
		// to match — this prevents the feedback loop:
		//   Resize() → _gdWindow.Size = X → OnSizeChanged → SetRenderSize →
		//   Resized → layout → Resize() → _gdWindow.Size = X → OnSizeChanged → ...
		if (pixelSize != _lastProcessRenderSize) {
			if (_isManagedDialog)
				GD.Print($"[Dialog] OnSizeChanged EXTERNAL: gdSize={pixelSize}, lastRender={_lastProcessRenderSize} → SetRenderSize");
			_lastProcessRenderSize = pixelSize;
			_topLevelImpl.SetRenderSize(pixelSize, 1.0);
		} else if (_isManagedDialog) {
			GD.Print($"[Dialog] OnSizeChanged SKIP (matches lastRender): gdSize={pixelSize}");
		}

		// DisplayServer.WindowGetPosition fails if window isn't registered yet
		// (e.g., Size set before AddChild) or already removed (during Dispose).
		if (_isVisible && _gdWindow.IsInsideTree()) {
			var windowId = _gdWindow.GetWindowId();
			var actualPos = DisplayServer.WindowGetPosition(windowId);
			Position = new PixelPoint(actualPos.X, actualPos.Y);
			PositionChanged?.Invoke(Position);
		}

		var newAvState = _gdWindow.Mode switch {
			Godot.Window.ModeEnum.Maximized => WindowState.Maximized,
			Godot.Window.ModeEnum.Minimized => WindowState.Minimized,
			Godot.Window.ModeEnum.Fullscreen => WindowState.FullScreen,
			_ => WindowState.Normal
		};
		if (newAvState != _windowState) {
			_windowState = newAvState;
			WindowStateChanged?.Invoke(newAvState);
		}
	}

	private void OnWindowInput(InputEvent @event) {
		if (_isDisposed) return;

		_ = @event switch {
			InputEventMouseMotion motion => _topLevelImpl.OnMouseMotion(motion, Time.GetTicksMsec()),
			InputEventMouseButton button => _topLevelImpl.OnMouseButton(button, Time.GetTicksMsec()),
			InputEventScreenTouch st => _topLevelImpl.OnScreenTouch(st, Time.GetTicksMsec()),
			InputEventScreenDrag sd => _topLevelImpl.OnScreenDrag(sd, Time.GetTicksMsec()),
			InputEventKey k => _topLevelImpl.OnKey(k, Time.GetTicksMsec()),
			InputEventJoypadButton jb => _topLevelImpl.OnJoypadButton(jb, Time.GetTicksMsec()),
			InputEventJoypadMotion jm => _topLevelImpl.OnJoypadMotion(jm, Time.GetTicksMsec()),
			_ => false
		};
	}

	private void OnFilesDropped(string[] files) {
		if (_isDisposed || files.Length == 0)
			return;

		// Get mouse position relative to the window content area
		var mousePos = _gdWindow.GetMousePosition();
		_topLevelImpl.OnFilesDropped(files, mousePos, Time.GetTicksMsec());
	}

	public void Dispose() {
		if (_isDisposed) return;
		_isDisposed = true; _isVisible = false;

		// Unsubscribe events BEFORE removing from tree to prevent
		// OnSizeChanged from firing on an invalid window.
		if (GodotObject.IsInstanceValid(_gdWindow)) {
			_gdWindow.CloseRequested -= OnCloseRequested;
			_gdWindow.SizeChanged -= OnSizeChanged;
			_gdWindow.WindowInput -= OnWindowInput;
			_gdWindow.FilesDropped -= OnFilesDropped;
			if (_gdWindow.IsInsideTree()) {
				_gdWindow.GetParent()?.RemoveChild(_gdWindow);
				_gdWindow.QueueFree();
			}
		}
		_topLevelImpl.Dispose();
		Closed?.Invoke();
	}

	private sealed class WindowHostControl : Godot.Control {
		private readonly GodotWindowImpl _owner;
		private bool _isEnabled = true;
		public WindowHostControl(GodotWindowImpl owner) { _owner = owner; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); }
		public void SetEnabled(bool enabled) => _isEnabled = enabled;

		protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret) {
			if (method == Node.MethodName._Ready && args.Count == 0) { _Ready(); ret = default; return true; }
			if (method == Node.MethodName._Process && args.Count == 1) { _Process(VariantUtils.ConvertTo<double>(args[0])); ret = default; return true; }
			if (method == CanvasItem.MethodName._Draw && args.Count == 0) { _Draw(); ret = default; return true; }
			if (method == MethodName._GuiInput && args.Count == 1) { _GuiInput(VariantUtils.ConvertTo<InputEvent>(args[0])); ret = default; return true; }
			return base.InvokeGodotClassMethod(method, args, out ret);
		}

		protected override bool HasGodotClassMethod(in godot_string_name method)
			=> method == Node.MethodName._Ready || method == Node.MethodName._Process || method == CanvasItem.MethodName._Draw || method == MethodName._GuiInput || base.HasGodotClassMethod(method);

		public override void _Ready() {
			if (Engine.IsEditorHint()) return;
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha, LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
		}

		public override void _Process(double delta) {
			if (_owner._isDisposed) return;

			GodotPlatform.TriggerRenderTick();
			AvDispatcher.UIThread.RunJobs();

			var window = _owner._gdWindow;
			var winSize = window.Size;
			var pixelSize = new PixelSize(Math.Max((int)winSize.X, 1), Math.Max((int)winSize.Y, 1));

			// Only push Godot's window size to Avalonia when it changed externally
			// (user resize, maximize, fullscreen). Skip when the size matches what
			// Avalonia already set via Resize() — this prevents feedback loops with
			// SizeToContent where SetRenderSize → Resized → layout → Resize grows
			// the window each frame.
			if (pixelSize != _owner._lastProcessRenderSize) {
				if (_owner._isManagedDialog)
					GD.Print($"[Dialog] _Process SIZE DIFF: gdSize={pixelSize}, lastRender={_owner._lastProcessRenderSize} → SetRenderSize + RunJobs");
				_owner._lastProcessRenderSize = pixelSize;
				_owner._topLevelImpl.SetRenderSize(pixelSize, 1.0);
				// Run queued layout jobs triggered by SetRenderSize before drawing.
				// Without this, maximize/fullscreen shows a blurry texture because
				// OnDraw renders with the old layout onto the new surface.
				AvDispatcher.UIThread.RunJobs();
			}

			// For SizeToContent windows, re-center after Avalonia layout determines
			// the actual content size (which differs from the initial 400×300 default).
			if (_owner._needsRecenter) {
				_owner._needsRecenter = false;
				var sceneTree = (SceneTree)Engine.GetMainLoop();
				var mainWinId = sceneTree.Root.GetWindowId();
				var mainWinPos = DisplayServer.WindowGetPosition(mainWinId);
				var mainWinSize = sceneTree.Root.Size;
				var subWinSize = window.Size;
				var centerPos = new Vector2I(
					mainWinPos.X + Math.Max((mainWinSize.X - subWinSize.X) / 2, 0),
					mainWinPos.Y + Math.Max((mainWinSize.Y - subWinSize.Y) / 2, 0)
				);
				window.CallDeferred(Godot.Window.MethodName.SetPosition, centerPos);
			}

			_owner._topLevelImpl.OnDraw(new Rect(pixelSize.ToSize(1.0)));
			QueueRedraw();
		}

		public override void _Draw() {
			if (_owner._isDisposed) return;
			var surface = _owner._topLevelImpl.GetOrCreateSurface();
			DrawTexture(surface.GdTexture, Vector2.Zero);
		}

		public override void _GuiInput(InputEvent @event) {
			if (_owner._isDisposed || !_isEnabled) { if (!_isEnabled) _owner.GotInputWhenDisabled?.Invoke(); return; }
			var handled = @event switch {
				InputEventMouseMotion motion => _owner._topLevelImpl.OnMouseMotion(motion, Time.GetTicksMsec()),
				InputEventMouseButton button => _owner._topLevelImpl.OnMouseButton(button, Time.GetTicksMsec()),
				InputEventScreenTouch touch => _owner._topLevelImpl.OnScreenTouch(touch, Time.GetTicksMsec()),
				InputEventScreenDrag drag => _owner._topLevelImpl.OnScreenDrag(drag, Time.GetTicksMsec()),
				InputEventKey key => _owner._topLevelImpl.OnKey(key, Time.GetTicksMsec()),
				InputEventJoypadButton jb => _owner._topLevelImpl.OnJoypadButton(jb, Time.GetTicksMsec()),
				InputEventJoypadMotion jm => _owner._topLevelImpl.OnJoypadMotion(jm, Time.GetTicksMsec()),
				_ => false
			};
			if (handled) AcceptEvent();
		}

		public void SetCursor(GdCursorShape cursorShape) => MouseDefaultCursorShape = cursorShape;
	}
}
