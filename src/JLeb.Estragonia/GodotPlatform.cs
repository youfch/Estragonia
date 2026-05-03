using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Dialogs;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

	/// <summary>Detects whether the current platform is mobile (Android/iOS).</summary>
	public static bool IsMobile { get; } = OS.HasFeature("mobile");

	/// <summary>
	/// Set to <c>true</c> during <c>ManagedFileDialogOptions.ContentRootFactory</c> invocation
	/// so that <see cref="GodotWindowImpl" /> can detect the resulting window is a managed
	/// file dialog and configure modal behavior accordingly.
	/// </summary>
	[ThreadStatic]
	internal static bool IsManagedDialogWindow;

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

	/// <summary>
	/// Creates and initializes the <see cref="GodotMobileApplicationLifetime" /> for mobile platforms.
	/// </summary>
	public static GodotMobileApplicationLifetime CreateMobileApplicationLifetime()
		=> new();

	public static void Initialize() {
		AvaloniaSynchronizationContext.AutoInstall = false; // Godot has its own sync context, don't replace it

		var platformGraphics = new GodotPlatformGraphics();
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
			.Bind<IWindowingPlatform>().ToConstant(IsMobile
			? (IWindowingPlatform)new GodotMobileWindowingPlatform()
			: new GodotWindowingPlatform())
			.Bind<ManagedFileDialogOptions>().ToConstant(new ManagedFileDialogOptions {
				AllowDirectorySelection = true,
				// Force managed file dialogs to use the Window path instead of the Popup path.
				// In Godot mode, the parent TopLevel is GodotTopLevel (not Window), so
				// ManagedStorageProvider.ShowAsPopup() would fail because it can't find a Panel
				// in the visual tree. By providing a Window as ContentRootFactory, PrepareRoot()
				// returns a Window and Show() takes the ShowAsWindow() path, which creates
				// an overlay window via GodotWindowingPlatform.CreateWindow().
				// IsManagedDialogWindow flag is set during factory invocation so GodotWindowImpl
				// can configure Godot Transient + Exclusive for modal blocking.
				// Fixed Width/Height: SizeToContent.WidthAndHeight causes Y-axis growth on
				// repeated dialog opens due to Avalonia's ManagedFileChooser measuring larger
				// Y values each time (internal QuickLinks/volumes state accumulation).
				ContentRootFactory = () => {
					IsManagedDialogWindow = true;
					try {
						return new Avalonia.Controls.Window {
							Width = 900,
							Height = 563,
							//SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight
						};
					} finally {
						IsManagedDialogWindow = false;
					}
				}
			});

		s_renderTimer = renderTimer;
		s_compositor = new AvCompositor(platformGraphics);

		// Subscribe to TemplateAppliedEvent on TopLevel to fix PopupOverlayLayer support.
		// Some theme templates (e.g. Ursa's StandardDialogWindow) include a VisualLayerManager
		// without Name="PART_VisualLayerManager", which prevents TopLevel.OnApplyTemplate from
		// discovering it. This means EnableVisualLayerManagerLayers() is a no-op and
		// PopupOverlayLayer is never enabled, causing "Unable to create IPopupImpl and no
		// overlay layer is found" when ComboBox or other popup controls try to open.
		// We work around this by manually enabling PopupOverlayLayer on every VisualLayerManager
		// in the TopLevel's visual tree right after its template is applied.
		// IMPORTANT: We use TemplateAppliedEvent (not WindowOpenedEvent) because
		// WindowOpenedEvent fires BEFORE ApplyTemplate(), so the visual tree isn't ready yet.
		// Note: VisualLayerManager.EnablePopupOverlayLayer is internal in Avalonia.
		// We use reflection to access it, with DynamicDependency to ensure AOT safety.
		TemplatedControl.TemplateAppliedEvent.AddClassHandler(
			typeof(TopLevel),
			(sender, _) => EnablePopupLayerOnTopLevel((TopLevel)sender),
			RoutingStrategies.Direct);
	}

	/// <summary>
	/// Enables <c>PopupOverlayLayer</c> on all <see cref="VisualLayerManager"/> instances
	/// in the given TopLevel's visual tree. Uses reflection because the property is internal.
	/// <see cref="DynamicDependencyAttribute"/> ensures the AOT trimmer preserves it.
	/// </summary>
	[DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(VisualLayerManager))]
	private static void EnablePopupLayerOnTopLevel(TopLevel topLevel) {
		var prop = typeof(VisualLayerManager).GetProperty("EnablePopupOverlayLayer",
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		if (prop == null) return;

		foreach (var v in topLevel.GetSelfAndVisualDescendants()) {
			if (v is VisualLayerManager vlm && !(bool)prop.GetValue(vlm)!)
				prop.SetValue(vlm, true);
		}
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
