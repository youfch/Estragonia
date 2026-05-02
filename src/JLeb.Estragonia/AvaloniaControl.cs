using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Threading;
using Godot;
using Godot.NativeInterop;
using JLeb.Estragonia.Input;
using AvControl = Avalonia.Controls.Control;
using AvDispatcher = Avalonia.Threading.Dispatcher;
using GdControl = Godot.Control;
using GdDispatcher = Godot.Dispatcher;
using GdInput = Godot.Input;
using GdKey = Godot.Key;

namespace JLeb.Estragonia;

/// <summary>Renders an Avalonia control and forwards input to it.</summary>
public class AvaloniaControl : GdControl {

	private AvControl? _control;
	private double _renderScaling = 1.0;
	private GodotTopLevel? _topLevel;
	private Godot.Window? _connectedWindow;

	/// <summary>
	/// Whether an OS file drag session is currently in progress over this control.
	/// Set to true when mouse enters from outside the window while no buttons are pressed
	/// (heuristic for OS file drag-and-drop).
	/// </summary>
	private bool _osdDragHovering;

	/// <summary>
	/// Whether the mouse recently entered the window (not yet determined if it's
	/// a normal move or an OS file drag). Reset after the first mouse event.
	/// </summary>
	private bool _mouseJustEnteredWindow;

	/// <summary>Gets or sets the underlying Avalonia control that will be rendered.</summary>
	public AvControl? Control {
		get => _control;
		set {
			if (ReferenceEquals(_control, value))
				return;

			_control = value;

			if (_topLevel is not null)
				_topLevel.Content = _control;
		}
	}

	/// <summary>Gets or sets the render scaling for the Avalonia control. Defaults to 1.0.</summary>
	[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Doesn't affect correctness")]
	public double RenderScaling {
		get => _renderScaling;
		set {
			if (_renderScaling == value)
				return;

			_renderScaling = value;
			OnResized();
			QueueRedraw();
		}
	}

	/// <summary>
	/// Gets or sets whether some Godot UI actions will be automatically mapped to an <see cref="InputElement.KeyDownEvent"/> event.
	/// The mapped actions are ui_left, ui_right, ui_up, ui_down, ui_accept and ui_cancel.
	/// Defaults to true.
	/// </summary>
	public bool AutoConvertUIActionToKeyDown { get; set; } = true;

	/// <summary>Gets the underlying Avalonia top-level element.</summary>
	/// <returns>The Avalonia top-level element.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the control isn't ready or has been disposed.</exception>
	public GodotTopLevel GetTopLevel()
		=> _topLevel ?? throw new InvalidOperationException($"The {nameof(AvaloniaControl)} isn't initialized");

	/// <summary>Gets the underlying Godot texture where <see cref="Control"/> is rendered.</summary>
	/// <returns>A texture.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the control isn't ready or has been disposed.</exception>
	public Texture2D GetTexture()
		=> GetTopLevel().Impl.GetOrCreateSurface().GdTexture;

	protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret) {
		if (method == Node.MethodName._Ready && args.Count == 0) {
			_Ready();
			ret = default;
			return true;
		}

		if (method == Node.MethodName._Process && args.Count == 1) {
			_Process(VariantUtils.ConvertTo<double>(args[0]));
			ret = default;
			return true;
		}

		if (method == CanvasItem.MethodName._Draw && args.Count == 0) {
			_Draw();
			ret = default;
			return true;
		}

		if (method == MethodName._GuiInput && args.Count == 1) {
			_GuiInput(VariantUtils.ConvertTo<InputEvent>(args[0]));
			ret = default;
			return true;
		}

		if (method == MethodName._HasPoint && args.Count == 1) {
			ret = VariantUtils.CreateFrom(_HasPoint(VariantUtils.ConvertTo<Vector2>(args[0])));
			return true;
		}

		return base.InvokeGodotClassMethod(method, args, out ret);
	}

	protected override bool HasGodotClassMethod(in godot_string_name method)
		=> method == Node.MethodName._Ready
			|| method == Node.MethodName._Process
			|| method == CanvasItem.MethodName._Draw
			|| method == MethodName._GuiInput
			|| method == MethodName._HasPoint
			|| base.HasGodotClassMethod(method);

	public override void _Ready() {
		if (Engine.IsEditorHint())
			return;

		// Skia outputs a premultiplied alpha image, ensure we got the correct blend mode if the user didn't specify any
		Material ??= new CanvasItemMaterial {
			BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha,
			LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
		};

		var locator = AvaloniaLocator.Current;

		if (locator.GetService<IPlatformGraphics>() is not GodotVkPlatformGraphics graphics) {
			GD.PrintErr("No Godot platform graphics found, did you forget to register your Avalonia app with UseGodot()?");
			return;
		}

		var topLevelImpl = new GodotTopLevelImpl(graphics, locator.GetRequiredService<IClipboard>(), GodotPlatform.Compositor) {
			CursorChanged = OnAvaloniaCursorChanged
		};

		topLevelImpl.SetRenderSize(GetFrameSize(), RenderScaling);

		_topLevel = new GodotTopLevel(topLevelImpl) {
			Background = null,
			Content = Control,
			TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None }
		};

		_topLevel.Prepare();
		_topLevel.StartRendering();

		Resized += OnResized;
		FocusEntered += OnFocusEntered;
		FocusExited += OnFocusExited;
		MouseExited += OnMouseExited;

		// Connect to the root window's FilesDropped signal for OS file drag-and-drop
		var rootWindow = GetTree().Root;
		rootWindow.FilesDropped += OnFilesDropped;
		rootWindow.MouseEntered += OnRootMouseEntered;
		rootWindow.MouseExited += OnRootMouseExited;
		_connectedWindow = rootWindow;

		if (HasFocus())
			OnFocusEntered();
	}

	public override void _Process(double delta) {
		GodotPlatform.TriggerRenderTick();

		// Process all queued Avalonia dispatcher work items (layout passes, animations, etc.)
		// This ensures layout is up-to-date before we force a synchronous render.
		AvDispatcher.UIThread.RunJobs();

		RenderAvalonia();
	}

	private PixelSize GetFrameSize()
		=> PixelSize.FromSize(Size.ToAvaloniaSize(), 1.0);

	private void RenderAvalonia()
		=> _topLevel!.Impl.OnDraw(new Rect(Size.ToAvaloniaSize()));

	private void OnAvaloniaCursorChanged(CursorShape cursor)
		=> MouseDefaultCursorShape = cursor;

	private void OnResized() {
		if (_topLevel is null)
			return;

		_topLevel.Impl.SetRenderSize(GetFrameSize(), RenderScaling);
		RenderAvalonia();
	}

	private void OnFocusEntered() {
		if (_topLevel is null)
			return;

		_topLevel.Focus();

		if (_topLevel.FocusManager?.FindFirstFocusableElement() is not { } inputElement)
			return;

		NavigationMethod navigationMethod;

		if (GdInput.IsActionPressed(GodotBuiltInActions.UIFocusNext) || GdInput.IsActionPressed(GodotBuiltInActions.UIFocusPrev))
			navigationMethod = NavigationMethod.Tab;
		else if (GdInput.GetMouseButtonMask() != 0)
			navigationMethod = NavigationMethod.Pointer;
		else
			navigationMethod = NavigationMethod.Unspecified;

		inputElement.Focus(navigationMethod);
	}

	private void OnFocusExited()
		=> _topLevel?.Impl.OnLostFocus();

	public override void _Draw() {
		if (_topLevel is null)
			return;

		var surface = _topLevel.Impl.GetOrCreateSurface();
		DrawTexture(surface.GdTexture, Vector2.Zero);
	}

	public override void _GuiInput(InputEvent @event) {
		if (_topLevel is null)
			return;

		// Debug: log all events during potential OS drag state
		if (@event is InputEventMouseMotion dbgMotion) {
			GD.Print($"[OSD] _GuiInput mouse motion: justEntered={_mouseJustEnteredWindow}, hovering={_osdDragHovering}, buttons={dbgMotion.ButtonMask}, pos={dbgMotion.Position}");
		} else if (@event is InputEventMouseButton) {
			GD.Print($"[OSD] _GuiInput mouse button");
		}

		// Detect OS file drag-and-drop hover:
		// When files are dragged from the OS file manager into the window,
		// Godot sends InputEventMouseMotion with no button mask.
		// We detect this only right after MouseEntered fires (mouse came from
		// outside the window), which distinguishes OS drag from normal mouse movement.
		if (_mouseJustEnteredWindow && @event is InputEventMouseMotion motion && motion.ButtonMask == 0) {
			// Mouse entered from outside with no buttons — likely an OS file drag
			_mouseJustEnteredWindow = false;
			var localPos = motion.Position;
			GD.Print($"[OSD] Attempting DragEnter at {localPos}");
			if (_topLevel.Impl.OnOsdDragEnter(localPos, Time.GetTicksMsec())) {
				_osdDragHovering = true;
				GD.Print("[OSD] DragEnter succeeded, hovering=true");
				AcceptEvent();
				return;
			}
			GD.Print("[OSD] DragEnter failed (OnOsdDragEnter returned false)");
		} else if (_mouseJustEnteredWindow) {
			// First event after entering was a button press or something else — not an OS drag
			_mouseJustEnteredWindow = false;
			GD.Print($"[OSD] First event after enter was {@event.GetType().Name} — not OS drag");
		}

		// Continue OS drag hover — synthesize DragOver on each mouse motion
		if (_osdDragHovering && @event is InputEventMouseMotion hoverMotion) {
			var localPos = hoverMotion.Position;
			if (_topLevel.Impl.OnOsdDragOver(localPos, Time.GetTicksMsec()))
				AcceptEvent();
			return;
		}

		if (TryHandleInput(_topLevel.Impl, @event) || TryHandleAction(@event))
			AcceptEvent();
	}

	private bool TryHandleAction(InputEvent inputEvent) {
		if (!inputEvent.IsActionType())
			return false;

		if (inputEvent.IsActionPressed(GodotBuiltInActions.UIFocusNext, true, true))
			return TryMoveFocus(NavigationDirection.Next, inputEvent);

		if (inputEvent.IsActionPressed(GodotBuiltInActions.UIFocusPrev, true, true))
			return TryMoveFocus(NavigationDirection.Previous, inputEvent);

		if (AutoConvertUIActionToKeyDown) {

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UILeft, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Left);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIRight, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Right);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIUp, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Up);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIDown, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Down);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIAccept, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Enter);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UICancel, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Escape);

		}

		return false;
	}

	private bool SimulateKeyDownFromAction(InputEvent inputEvent, GdKey key) {
		// if the action already matches the key we're going to simulate, abort: it already got through TryHandleInput and wasn't handled
		if (inputEvent is InputEventKey inputEventKey && inputEventKey.Keycode == key)
			return false;

		if (_topLevel?.FocusManager?.GetFocusedElement() is not { } currentElement)
			return false;

		var args = new KeyEventArgs {
			RoutedEvent = InputElement.KeyDownEvent,
			Key = key.ToAvaloniaKey(),
			KeyModifiers = inputEvent.GetKeyModifiers()
		};
		currentElement.RaiseEvent(args);
		return args.Handled;
	}

	private static bool TryHandleInput(GodotTopLevelImpl impl, InputEvent inputEvent)
		=> inputEvent switch {
			InputEventMouseMotion mouseMotion => impl.OnMouseMotion(mouseMotion, Time.GetTicksMsec()),
			InputEventMouseButton mouseButton => impl.OnMouseButton(mouseButton, Time.GetTicksMsec()),
			InputEventScreenTouch screenTouch => impl.OnScreenTouch(screenTouch, Time.GetTicksMsec()),
			InputEventScreenDrag screenDrag => impl.OnScreenDrag(screenDrag, Time.GetTicksMsec()),
			InputEventKey key => impl.OnKey(key, Time.GetTicksMsec()),
			InputEventJoypadButton joypadButton => impl.OnJoypadButton(joypadButton, Time.GetTicksMsec()),
			InputEventJoypadMotion joypadMotion => impl.OnJoypadMotion(joypadMotion, Time.GetTicksMsec()),
			_ => false
		};

	private bool TryMoveFocus(NavigationDirection direction, InputEvent inputEvent) {
		if (_topLevel?.FocusManager is not { } focusManager)
			return false;

		var currentElement = focusManager.GetFocusedElement() ?? _topLevel;

		// GodotTopLevel has a Continue tab navigation since we want to be able to focus the Godot controls
		// once we're done with the Avalonia ones. However, if there's no Godot control, we want to act as Cycle.
		var nextElement = GetNextTabElement(focusManager, currentElement, direction);
		if (nextElement is null) {
			var nextGdControl = direction switch {
				NavigationDirection.Next => FindNextValidFocus(),
				NavigationDirection.Previous => FindPrevValidFocus(),
				_ => null
			};

			if ((nextGdControl is null || nextGdControl == this) && (object) currentElement != _topLevel)
				nextElement = GetNextTabElement(focusManager, _topLevel, direction);
		}


		if (nextElement is null)
			return false;

		nextElement.Focus(NavigationMethod.Tab, inputEvent.GetKeyModifiers());
		return true;
	}

	private static IInputElement? GetNextTabElement(IFocusManager focusManager, IInputElement element, NavigationDirection direction) {
		var previous = element;

		while (true) {
			// FindNextElement doesn't take IsEffectivelyEnabled into account, check it manually
			var next = focusManager.FindNextElement(direction, new FindNextElementOptions { FocusedElement = previous });
			if (next is null || next.IsEffectivelyEnabled)
				return next;

			// handle potential all-disabled cycle
			if (next == element)
				return null;

			previous = next;
		}
	}

	private void OnMouseExited() {
		// End OS drag hover session when mouse leaves the control
		if (_osdDragHovering) {
			_osdDragHovering = false;
			_topLevel?.Impl.OnOsdDragLeave();
		}

		_topLevel?.Impl.OnMouseExited(Time.GetTicksMsec());
	}

	private void OnRootMouseEntered() {
		// Flag that mouse just entered from outside — used to detect OS file drag
		GD.Print("[OSD] Root MouseEntered");
		_mouseJustEnteredWindow = true;
	}

	private void OnRootMouseExited() {
		// End OS drag hover session when mouse leaves the window
		GD.Print($"[OSD] Root MouseExited, hovering={_osdDragHovering}");
		if (_osdDragHovering) {
			_osdDragHovering = false;
			_topLevel?.Impl.OnOsdDragLeave();
		}
		_mouseJustEnteredWindow = false;
	}

	private void OnFilesDropped(string[] files) {
		GD.Print($"[OSD] OnFilesDropped: {files.Length} files, hovering={_osdDragHovering}");
		if (_topLevel is null || files.Length == 0)
			return;

		_osdDragHovering = false;

		// Get the mouse position relative to this control
		var mousePos = GetGlobalMousePosition();
		var localPos = mousePos - GlobalPosition;
		GD.Print($"[OSD] OnFilesDropped: mousePos={mousePos}, localPos={localPos}");

		if (_topLevel.Impl.OnFilesDropped(files, localPos, Time.GetTicksMsec()))
			AcceptEvent();
	}

	public override bool _HasPoint(Vector2 point)
		=> _topLevel?.InputHitTest(point.ToAvaloniaPoint() / _topLevel.RenderScaling, false) is not null;

	protected override void Dispose(bool disposing) {
		if (disposing && _topLevel is not null) {

			Resized -= OnResized;
			FocusEntered -= OnFocusEntered;
			FocusExited -= OnFocusExited;
			MouseExited -= OnMouseExited;

			if (_connectedWindow is not null) {
				_connectedWindow.FilesDropped -= OnFilesDropped;
				_connectedWindow.MouseEntered -= OnRootMouseEntered;
				_connectedWindow.MouseExited -= OnRootMouseExited;
				_connectedWindow = null;
			}

			_topLevel.Dispose();
			_topLevel = null;
		}

		base.Dispose(disposing);
	}

}
