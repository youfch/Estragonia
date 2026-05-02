using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Dialogs;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using Godot;
using JLeb.Estragonia.Input;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;

namespace JLeb.Estragonia;

/// <summary>Contains Godot to Avalonia platform initialization.</summary>
internal static class GodotPlatform {

	private static AvCompositor? s_compositor;
	private static ManualRenderTimer? s_renderTimer;
	private static ulong s_lastProcessFrame = UInt64.MaxValue;
	private static GodotApplicationLifetime? s_lifetime;

	public static AvCompositor Compositor
		=> s_compositor ?? throw new InvalidOperationException($"{nameof(GodotPlatform)} hasn't been initialized");

	/// <summary>
	/// Creates and initializes the <see cref="GodotApplicationLifetime" /> for use with
	/// <see cref="AppBuilder.SetupWithLifetime" />.
	/// </summary>
	public static GodotApplicationLifetime CreateApplicationLifetime() {
		s_lifetime = new GodotApplicationLifetime();
		s_lifetime.Initialize();
		return s_lifetime;
	}

	public static void Initialize() {
		AvaloniaSynchronizationContext.AutoInstall = false; // Godot has its own sync context, don't replace it

		var platformGraphics = new GodotVkPlatformGraphics();
		var renderTimer = new ManualRenderTimer();

		AvaloniaLocator.CurrentMutable
			.Bind<IClipboard>().ToConstant(new GodotClipboard())
			.Bind<ICursorFactory>().ToConstant(new GodotCursorFactory())
			.Bind<IDispatcherImpl>().ToConstant(new GodotDispatcherImpl(Thread.CurrentThread))
			.Bind<IKeyboardDevice>().ToConstant(GodotDevices.Keyboard)
			.Bind<IPlatformGraphics>().ToConstant(platformGraphics)
			.Bind<IPlatformIconLoader>().ToConstant(new StubPlatformIconLoader())
			.Bind<IPlatformSettings>().ToConstant(new GodotPlatformSettings())
			.Bind<IRenderTimer>().ToConstant(renderTimer)
			.Bind<IRenderLoop>().ToConstant(RenderLoop.FromTimer(renderTimer))
			.Bind<IWindowingPlatform>().ToConstant(new GodotWindowingPlatform())
			.Bind<ManagedFileDialogOptions>().ToConstant(new ManagedFileDialogOptions {
				AllowDirectorySelection = true,
				// Force managed file dialogs to use the Window path instead of the Popup path.
				// In Godot mode, the parent TopLevel is GodotTopLevel (not Window), so
				// ManagedStorageProvider.ShowAsPopup() would fail because it can't find a Panel
				// in the visual tree. By providing a Window as ContentRootFactory, PrepareRoot()
				// returns a Window and Show() takes the ShowAsWindow() path, which creates
				// an overlay window via GodotWindowingPlatform.CreateWindow().
				// SizeToContent.WidthAndHeight makes the dialog auto-size to its content
				// (ManagedFileChooser needs ~900x500+ to display properly).
				ContentRootFactory = () => new Avalonia.Controls.Window {
					SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight
				}
			});

		s_renderTimer = renderTimer;
		s_compositor = new AvCompositor(platformGraphics);
	}

	public static void TriggerRenderTick() {
		if (s_renderTimer is null)
			return;

		// if we have several AvaloniaControls, ensure we tick the timer only once each frame
		var processFrame = Engine.GetProcessFrames();
		if (processFrame == s_lastProcessFrame)
			return;

		s_lastProcessFrame = processFrame;
		s_renderTimer.TriggerTick(new TimeSpan((long) (Time.GetTicksUsec() * 10UL)));
	}

}
