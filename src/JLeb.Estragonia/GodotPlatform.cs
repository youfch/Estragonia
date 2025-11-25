using System;
using System.IO;
using System.Reflection;
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

	public static AvCompositor Compositor
		=> s_compositor ?? throw new InvalidOperationException($"{nameof(GodotPlatform)} hasn't been initialized");

	public static void Initialize() {
		AvaloniaSynchronizationContext.AutoInstall = false; // Godot has its own sync context, don't replace it

		var platformGraphics = new GodotVkPlatformGraphics();
		var renderTimer = new ManualRenderTimer();

		var clipboardImpl = CreateHeadlessClipboardStub();

		AvaloniaLocator.CurrentMutable
			.Bind<IClipboard>().ToConstant(new GodotClipboard(clipboardImpl))
			.Bind<ICursorFactory>().ToConstant(new GodotCursorFactory())
			.Bind<IDispatcherImpl>().ToConstant(new GodotDispatcherImpl(Thread.CurrentThread))
			.Bind<IKeyboardDevice>().ToConstant(GodotDevices.Keyboard)
			.Bind<IPlatformGraphics>().ToConstant(platformGraphics)
			.Bind<IPlatformIconLoader>().ToConstant(new StubPlatformIconLoader())
			.Bind<IPlatformSettings>().ToConstant(new GodotPlatformSettings())
			.Bind<IRenderTimer>().ToConstant(renderTimer)
			.Bind<IWindowingPlatform>().ToConstant(new GodotWindowingPlatform())
			.Bind<IStorageProviderFactory>().ToConstant(new GodotStorageProviderFactory())
			.Bind<PlatformHotkeyConfiguration>().ToConstant(CreatePlatformHotKeyConfiguration())
			.Bind<ManagedFileDialogOptions>().ToConstant(new ManagedFileDialogOptions { AllowDirectorySelection = true });

		s_renderTimer = renderTimer;
		s_compositor = new AvCompositor(platformGraphics);
	}

	private static PlatformHotkeyConfiguration CreatePlatformHotKeyConfiguration()
		=> OperatingSystem.IsMacOS()
			? new PlatformHotkeyConfiguration(commandModifiers: KeyModifiers.Meta, wholeWordTextActionModifiers: KeyModifiers.Alt)
			: new PlatformHotkeyConfiguration(commandModifiers: KeyModifiers.Control);

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
	
	public static IOwnedClipboardImpl CreateHeadlessClipboardStub() {
		// Create an instance of HeadlessClipboardImplStub via reflection, or implement your own GodotHeadlessClipboardImplStub that inherits from IOwnedClipboardImpl.
		try {
			// Load target assembly (Avalonia.Headless) via framework core interface for reliability
			Assembly headlessAssembly = Assembly.Load("Avalonia.Headless");

			// Get Type of internal sealed class using full qualified name
			Type stubType = headlessAssembly.GetType(
				"Avalonia.Headless.HeadlessClipboardImplStub",
				throwOnError: true,
				ignoreCase: false
			);

			// Retrieve parameterless constructor (common for Avalonia Stub classes)
			ConstructorInfo ctor = stubType.GetConstructor(
				bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				binder: null,
				types: Type.EmptyTypes,
				modifiers: null
			);

			if (ctor == null) {
				throw new InvalidOperationException("Parameterless constructor not found for Avalonia.Headless.HeadlessClipboardImplStub");
			}

			// Create instance via reflection and cast to public interface
			object instance = ctor.Invoke(null);
			return instance as IOwnedClipboardImpl ??
				throw new InvalidCastException("Failed to cast instance to IOwnedClipboardImpl");
		}
		catch (FileNotFoundException ex) {
			throw new InvalidOperationException("Avalonia.Headless assembly not found - ensure corresponding NuGet package is installed", ex);
		}
		catch (Exception ex) {
			throw new InvalidOperationException("Failed to create HeadlessClipboardImplStub via reflection", ex);
		}
	}
}
