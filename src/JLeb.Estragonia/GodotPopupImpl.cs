using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
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
/// <see cref="IPopupImpl"/> using a borderless Godot sub-window.
/// The Godot window is created lazily on <see cref="Show"/> and destroyed on <see cref="Dispose"/>,
/// matching Avalonia's PopupRoot lifecycle (one IPopupImpl per open/close cycle).
/// </summary>
internal sealed class GodotPopupImpl : IPopupImpl {

	private readonly ITopLevelImpl _parent;
	private readonly GodotTopLevelImpl _topLevelImpl;
	private readonly ManagedPopupPositioner _popupPositioner;

	private Godot.Window? _gdWindow;
	private PopupHostControl? _hostControl;
	private bool _isDisposed;
	private bool _isVisible;

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

	public Size ClientSize => _topLevelImpl.ClientSize;
	public double RenderScaling => _topLevelImpl.RenderScaling;
	public WindowTransparencyLevel TransparencyLevel => _topLevelImpl.TransparencyLevel;
	public AvCompositor Compositor => _topLevelImpl.Compositor;

	double ITopLevelImpl.DesktopScaling => 1.0;
	IPlatformHandle? ITopLevelImpl.Handle => null;

	Size? IWindowBaseImpl.FrameSize => null;
	public PixelPoint Position { get; private set; }
	Size IWindowBaseImpl.MaxAutoSizeHint => Size.Infinity;

	public IPopupPositioner PopupPositioner => _popupPositioner;

	IPlatformRenderSurface[] ITopLevelImpl.Surfaces => ((ITopLevelImpl)_topLevelImpl).Surfaces;

	AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels => new(1.0, 1.0, 1.0);

	public GodotPopupImpl(ITopLevelImpl parent) {
		_parent = parent;

		var platformGraphics = AvaloniaLocator.Current.GetService<IPlatformGraphics>() as GodotVkPlatformGraphics
			?? throw new InvalidOperationException("GodotPlatform not initialized");
		var clipboard = AvaloniaLocator.Current.GetService<IClipboard>()!;

		_topLevelImpl = new GodotTopLevelImpl(platformGraphics, clipboard, GodotPlatform.Compositor);

		_topLevelImpl.Paint = rect => Paint?.Invoke(rect);
		_topLevelImpl.Resized = (size, reason) => Resized?.Invoke(size, reason);
		_topLevelImpl.Input = args => Input?.Invoke(args);
		_topLevelImpl.LostFocus = () => LostFocus?.Invoke();
		_topLevelImpl.ScalingChanged = scaling => ScalingChanged?.Invoke(scaling);
		_topLevelImpl.TransparencyLevelChanged = level => TransparencyLevelChanged?.Invoke(level);

		_topLevelImpl.SetRenderSize(new PixelSize(1, 1), 1.0);

		_popupPositioner = new ManagedPopupPositioner(
			new ManagedPopupPositionerPopupImplHelper(parent, MoveResize)
		);
	}

	private void MoveResize(PixelPoint position, Size size, double scaling) {
		if (_isDisposed) return;

		Position = position;
		if (_isVisible && _gdWindow != null && _gdWindow.IsInsideTree())
			_gdWindow.Position = new Vector2I(position.X, position.Y);

		var pixelSize = new PixelSize(Math.Max((int)size.Width, 1), Math.Max((int)size.Height, 1));
		_topLevelImpl.SetRenderSize(pixelSize, 1.0);
		if (_isVisible && _gdWindow != null && _gdWindow.IsInsideTree())
			_gdWindow.Size = new Vector2I(pixelSize.Width, pixelSize.Height);
	}

	/// <summary>
	/// Creates the Godot Window node lazily on first Show().
	/// This aligns with Avalonia's lifecycle: IPopupImpl is created → Show() is called once → Dispose().
	/// </summary>
	private void EnsureWindowCreated() {
		if (_gdWindow != null) return;

		_gdWindow = new Godot.Window {
			Title = string.Empty,
			Visible = false,
			Borderless = true,
			Transparent = true,
			TransparentBg = true,
			InitialPosition = Godot.Window.WindowInitialPosition.Absolute,
			WrapControls = false,
			Size = new Vector2I(1, 1),
			AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
		};
		_gdWindow.AddToGroup("avalonia_windows");

		_hostControl = new PopupHostControl(this);
		_gdWindow.AddChild(_hostControl);
		_topLevelImpl.CursorChanged = cursorShape => _hostControl.SetCursor(cursorShape);

		_gdWindow.WindowInput += OnWindowInput;
		_gdWindow.SizeChanged += OnSizeChanged;
	}

	// --- IPopupImpl ---

	public void SetWindowManagerAddShadowHint(bool enabled) {
		// No-op — shadows are managed by Avalonia themes
	}

	public void TakeFocus() {
		// Popups should not steal focus from the parent (matching Win32 MA_NOACTIVATE)
	}

	// --- IWindowBaseImpl ---

	public void Show(bool activate, bool isDialog) {
		if (_isDisposed || _isVisible) return;

		EnsureWindowCreated();

		var sceneTree = (SceneTree)Engine.GetMainLoop();
		sceneTree.Root.GuiEmbedSubwindows = false;

		_gdWindow!.Transient = false;
		_gdWindow.AlwaysOnTop = true;

		sceneTree.Root.AddChild(_gdWindow);

		_gdWindow.Visible = true;
		_isVisible = true;
	}

	public void Hide() {
		if (!_isVisible) return;
		_gdWindow!.Visible = false;
		_isVisible = false;
	}

	public void Activate() {
		// Popups should not activate
	}

	public void SetTopmost(bool value) {
		if (GodotObject.IsInstanceValid(_gdWindow))
			_gdWindow!.AlwaysOnTop = value;
	}

	// --- ITopLevelImpl delegation ---

	void ITopLevelImpl.SetInputRoot(IInputRoot inputRoot)
		=> ((ITopLevelImpl)_topLevelImpl).SetInputRoot(inputRoot);

	Point ITopLevelImpl.PointToClient(PixelPoint point)
		=> ((ITopLevelImpl)_topLevelImpl).PointToClient(point);

	PixelPoint ITopLevelImpl.PointToScreen(Point point)
		=> ((ITopLevelImpl)_topLevelImpl).PointToScreen(point);

	void ITopLevelImpl.SetCursor(ICursorImpl? cursor)
		=> ((ITopLevelImpl)_topLevelImpl).SetCursor(cursor);

	IPopupImpl? ITopLevelImpl.CreatePopup()
		=> new GodotPopupImpl(this);

	void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
		=> ((ITopLevelImpl)_topLevelImpl).SetTransparencyLevelHint(transparencyLevels);

	void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant themeVariant) { }

	public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application) {
		var pixelSize = new PixelSize(Math.Max((int)clientSize.Width, 1), Math.Max((int)clientSize.Height, 1));
		_topLevelImpl.SetRenderSize(pixelSize, 1.0);
		if (_isVisible && _gdWindow != null && _gdWindow.IsInsideTree())
			_gdWindow.Size = new Vector2I(pixelSize.Width, pixelSize.Height);
	}

	public void Move(PixelPoint point) {
		Position = point;
		if (_isVisible && _gdWindow != null && _gdWindow.IsInsideTree())
			_gdWindow.Position = new Vector2I(point.X, point.Y);
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
		=> ((ITopLevelImpl)_topLevelImpl).TryGetFeature(featureType);

	// --- Internal handlers ---

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

	private void OnSizeChanged() {
		if (_isDisposed) return;

		var size = _gdWindow!.Size;
		var pixelSize = new PixelSize(Math.Max((int)size.X, 1), Math.Max((int)size.Y, 1));
		_topLevelImpl.SetRenderSize(pixelSize, 1.0);
	}

	public void Dispose() {
		if (_isDisposed) return;
		_isDisposed = true;
		_isVisible = false;

		if (GodotObject.IsInstanceValid(_gdWindow)) {
			_gdWindow!.WindowInput -= OnWindowInput;
			_gdWindow.SizeChanged -= OnSizeChanged;

			if (_gdWindow.IsInsideTree()) {
				_gdWindow.Visible = false;
				var parent = _gdWindow.GetParent();
				if (parent != null)
					parent.CallDeferred(Godot.Node.MethodName.RemoveChild, _gdWindow);
				_gdWindow.CallDeferred(Godot.Node.MethodName.QueueFree);
			}
		}

		_gdWindow = null;
		_hostControl = null;

		_topLevelImpl.Dispose();
		Closed?.Invoke();
	}

	// --- Nested PopupHostControl ---

	private sealed class PopupHostControl : Godot.Control {

		private readonly GodotPopupImpl _owner;

		public PopupHostControl(GodotPopupImpl owner) {
			_owner = owner;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}

		public void SetCursor(GdCursorShape cursorShape)
			=> MouseDefaultCursorShape = cursorShape;

		protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret) {
			if (method == Node.MethodName._Ready && args.Count == 0) {
				_Ready(); ret = default; return true;
			}
			if (method == Node.MethodName._Process && args.Count == 1) {
				_Process(VariantUtils.ConvertTo<double>(args[0])); ret = default; return true;
			}
			if (method == CanvasItem.MethodName._Draw && args.Count == 0) {
				_Draw(); ret = default; return true;
			}
			if (method == MethodName._GuiInput && args.Count == 1) {
				_GuiInput(VariantUtils.ConvertTo<InputEvent>(args[0])); ret = default; return true;
			}
			return base.InvokeGodotClassMethod(method, args, out ret);
		}

		protected override bool HasGodotClassMethod(in godot_string_name method)
			=> method == Node.MethodName._Ready
				|| method == Node.MethodName._Process
				|| method == CanvasItem.MethodName._Draw
				|| method == MethodName._GuiInput
				|| base.HasGodotClassMethod(method);

		public override void _Ready() {
			if (Engine.IsEditorHint()) return;
			Material = new CanvasItemMaterial {
				BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha,
				LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
			};
		}

		public override void _Process(double delta) {
			if (_owner._isDisposed || _owner._gdWindow is not { } window) return;

			GodotPlatform.TriggerRenderTick();
			AvDispatcher.UIThread.RunJobs();

			var winSize = window.Size;
			var pixelSize = new PixelSize(Math.Max((int)winSize.X, 1), Math.Max((int)winSize.Y, 1));
			_owner._topLevelImpl.SetRenderSize(pixelSize, 1.0);

			AvDispatcher.UIThread.RunJobs();

			_owner._topLevelImpl.OnDraw(new Rect(pixelSize.ToSize(1.0)));
			QueueRedraw();
		}

		public override void _Draw() {
			if (_owner._isDisposed) return;
			var surface = _owner._topLevelImpl.GetOrCreateSurface();
			DrawTexture(surface.GdTexture, Vector2.Zero);
		}

		public override void _GuiInput(InputEvent @event) {
			if (_owner._isDisposed) return;
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

	}

}
