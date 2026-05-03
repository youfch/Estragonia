using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Platform.Surfaces;
using Godot;
using JLeb.Estragonia.Input;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;
using AvKey = Avalonia.Input.Key;
using GdCursorShape = Godot.Control.CursorShape;
using GdMouseButton = Godot.MouseButton;

namespace JLeb.Estragonia;

/// <summary>Implementation of Avalonia <see cref="ITopLevelImpl"/> that renders to a Godot texture.</summary>
internal sealed class GodotTopLevelImpl : ITopLevelImpl {

	private readonly GodotVkPlatformGraphics _platformGraphics;
	private readonly IClipboard _clipboard;
	private readonly TouchDevice _touchDevice = new();

	private GodotSkiaSurface? _surface;
	private WindowTransparencyLevel _transparencyLevel = WindowTransparencyLevel.Transparent;
	private PixelSize _renderSize;
	private IInputRoot? _inputRoot;
	private GdCursorShape _cursorShape;
	private bool _isDisposed;
	private int _lastMouseDeviceId = GodotDevices.EmulatedDeviceId;

	public double RenderScaling { get; private set; } = 1.0;

	double ITopLevelImpl.DesktopScaling
		=> 1.0;

	IPlatformHandle? ITopLevelImpl.Handle
		=> null;

	public AvCompositor Compositor { get; }

	public Size ClientSize { get; private set; }

	public WindowTransparencyLevel TransparencyLevel {
		get => _transparencyLevel;
		private set {
			if (_transparencyLevel.Equals(value))
				return;

			_transparencyLevel = value;
			TransparencyLevelChanged?.Invoke(value);
		}
	}

	public Action<Rect>? Paint { get; set; }

	public Action<Size, WindowResizeReason>? Resized { get; set; }

	public Action? Closed { get; set; }

	public Action<RawInputEventArgs>? Input { get; set; }

	public Action? LostFocus { get;set; }

	public Action<GdCursorShape>? CursorChanged { get; set; }

	public Action<double>? ScalingChanged { get; set; }

	public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

	IPlatformRenderSurface[] ITopLevelImpl.Surfaces
		=> GetOrCreateSurfaces();

	AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels
		=> new(1.0, 1.0, 1.0);

	public GodotTopLevelImpl(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_platformGraphics = platformGraphics;
		_clipboard = clipboard;
		Compositor = compositor;

		platformGraphics.AddRef();
	}

	private GodotSkiaSurface CreateSurface() {
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(GodotTopLevelImpl));

		return _platformGraphics.GetSharedContext().CreateSurface(_renderSize, RenderScaling);
	}

	public GodotSkiaSurface? TryGetSurface()
		=> _surface;

	public GodotSkiaSurface GetOrCreateSurface()
		=> _surface ??= CreateSurface();

	private IPlatformRenderSurface[] GetOrCreateSurfaces()
		=> [GetOrCreateSurface()];

	[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Doesn't affect correctness")]
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

	/// <summary>
	/// Updates the render surface and ClientSize without firing <see cref="Resized"/>.
	/// Used by <c>GodotWindowImpl.Resize()</c> to apply Avalonia-determined sizes
	/// without creating a feedback loop (Resize → SetRenderSize → Resized → layout → Resize).
	/// </summary>
	[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Doesn't affect correctness")]
	internal void UpdateClientSize(PixelSize renderSize, double renderScaling) {
		var hasScalingChanged = RenderScaling != renderScaling;
		if (_renderSize == renderSize && !hasScalingChanged)
			return;

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

		// Intentionally NOT firing Resized — the caller (Resize) is responding
		// to an Avalonia layout pass, so no re-layout should be triggered.
	}

	public void OnDraw(Rect rect) {
		if (_isDisposed)
			return;

		Paint?.Invoke(rect);
	}

	public bool OnMouseMotion(InputEventMouseMotion inputEvent, ulong timestamp) {
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

	public bool OnMouseButton(InputEventMouseButton inputEvent, ulong timestamp) {
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
			(GdMouseButton.Left, true) => CreateButtonArgs(RawPointerEventType.LeftButtonDown),
			(GdMouseButton.Left, false) => CreateButtonArgs(RawPointerEventType.LeftButtonUp),
			(GdMouseButton.Right, true) => CreateButtonArgs(RawPointerEventType.RightButtonDown),
			(GdMouseButton.Right, false) => CreateButtonArgs(RawPointerEventType.RightButtonUp),
			(GdMouseButton.Middle, true) => CreateButtonArgs(RawPointerEventType.MiddleButtonDown),
			(GdMouseButton.Middle, false) => CreateButtonArgs(RawPointerEventType.MiddleButtonUp),
			(GdMouseButton.Xbutton1, true) => CreateButtonArgs(RawPointerEventType.XButton1Down),
			(GdMouseButton.Xbutton1, false) => CreateButtonArgs(RawPointerEventType.XButton1Up),
			(GdMouseButton.Xbutton2, true) => CreateButtonArgs(RawPointerEventType.XButton2Down),
			(GdMouseButton.Xbutton2, false) => CreateButtonArgs(RawPointerEventType.XButton2Up),
			(GdMouseButton.WheelUp, _) => CreateWheelArgs(new Vector(0.0, inputEvent.Factor)),
			(GdMouseButton.WheelDown, _) => CreateWheelArgs(new Vector(0.0, -inputEvent.Factor)),
			(GdMouseButton.WheelLeft, _) => CreateWheelArgs(new Vector(inputEvent.Factor, 0.0)),
			(GdMouseButton.WheelRight, _) => CreateWheelArgs(new Vector(-inputEvent.Factor, 0.0)),
			_ => null
		};

		if (args is null)
			return false;

		input(args);

		return args.Handled;
	}

	public bool OnScreenTouch(InputEventScreenTouch inputEvent, ulong timestamp) {
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

	public bool OnScreenDrag(InputEventScreenDrag inputEvent, ulong timestamp) {
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

	public bool OnKey(InputEventKey inputEvent, ulong timestamp) {
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

		if (pressed && OS.IsKeycodeUnicode((long) keyCode)) {
			var text = Char.ConvertFromUtf32((int) inputEvent.Unicode);
			var args = new RawTextInputEventArgs(GodotDevices.Keyboard, timestamp, _inputRoot, text);

			input(args);

			if (args.Handled)
				return true;
		}

		return false;
	}

	public bool OnJoypadButton(InputEventJoypadButton inputEvent, ulong timestamp) {
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

	public bool OnJoypadMotion(InputEventJoypadMotion inputEvent, ulong timestamp) {
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

	public void OnLostFocus()
		=> LostFocus?.Invoke();

	public bool OnMouseExited(ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawPointerEventArgs(
			GodotDevices.GetMouse(_lastMouseDeviceId),
			timestamp,
			_inputRoot,
			RawPointerEventType.LeaveWindow,
			new Point(-1, -1),
			InputModifiersProvider.GetRawInputModifiers()
		);

		input(args);

		return args.Handled;
	}

	/// <summary>
	/// Handles files dropped from the OS onto the Godot window.
	/// First sends DragLeave to end any hover session, then
	/// synthesizes DragEnter → DragOver → Drop with real file data.
	/// </summary>
	public bool OnFilesDropped(string[] files, Vector2 position, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var point = position.ToAvaloniaPoint() / RenderScaling;
		var modifiers = InputModifiersProvider.GetRawInputModifiers();
		var device = AvaloniaLocator.Current.GetRequiredService<IDragDropDevice>();

		// Build IDataTransfer from the dropped file paths
		var dataTransfer = new DataTransfer();
		foreach (var filePath in files) {
			IStorageItem storageItem = Directory.Exists(filePath)
				? new BclStorageFolder(new DirectoryInfo(filePath))
				: new BclStorageFile(new FileInfo(filePath));
			dataTransfer.Add(DataTransferItem.CreateFile(storageItem));
		}

		// Synthesize DragEnter → DragOver → Drop sequence
		var enterArgs = new RawDragEvent(device, RawDragEventType.DragEnter, _inputRoot, point, dataTransfer, DragDropEffects.Copy | DragDropEffects.Link, modifiers);
		input(enterArgs);

		var overArgs = new RawDragEvent(device, RawDragEventType.DragOver, _inputRoot, point, dataTransfer, DragDropEffects.Copy | DragDropEffects.Link, modifiers);
		input(overArgs);

		var dropArgs = new RawDragEvent(device, RawDragEventType.Drop, _inputRoot, point, dataTransfer, DragDropEffects.Copy | DragDropEffects.Link, modifiers);
		input(dropArgs);

		return dropArgs.Handled;
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
		CursorChanged?.Invoke(cursorShape);
	}

	IPopupImpl? ITopLevelImpl.CreatePopup()
		=> null; // OverlayPopupHost: popups render within the same Avalonia context (no separate window)

	void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels) {
		// Overlay windows are always composited onto the host control's texture,
		// so we force Transparent level regardless of what Avalonia requests.
		// This prevents PART_TransparencyFallback from showing an opaque white background.
		TransparencyLevel = WindowTransparencyLevel.Transparent;
	}

	void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant themeVariant) {
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType) {
		if (featureType == typeof(IClipboard))
			return _clipboard;

		return null;
	}

	public void Dispose() {
		if (_isDisposed)
			return;

		_isDisposed = true;

		if (_surface is not null) {
			_surface.Dispose();
			_surface = null;
		}

		Closed?.Invoke();

		_platformGraphics.Release();
	}

}
