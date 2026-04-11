using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Dialogs;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
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
		var clipboard = new GodotClipboard(clipboardImpl);

		s_renderTimer = renderTimer;

		AvaloniaLocator.CurrentMutable
			.Bind<IClipboard>().ToConstant(clipboard)
			.Bind<ICursorFactory>().ToConstant(new GodotCursorFactory())
			.Bind<IDispatcherImpl>().ToConstant(new GodotDispatcherImpl(Thread.CurrentThread))
			.Bind<IKeyboardDevice>().ToConstant(GodotDevices.Keyboard)
			.Bind<IPlatformGraphics>().ToConstant(platformGraphics)
			.Bind<IPlatformIconLoader>().ToConstant(new StubPlatformIconLoader())
			.Bind<IPlatformSettings>().ToConstant(new GodotPlatformSettings())
			.Bind<IRenderTimer>().ToConstant(renderTimer);

		var renderLoop = RenderLoop.FromTimer(renderTimer);
		AvaloniaLocator.CurrentMutable.Bind<IRenderLoop>().ToConstant(renderLoop);

		s_compositor = new AvCompositor(platformGraphics);

		var windowingPlatform = new GodotWindowingPlatform(platformGraphics, clipboard, s_compositor);

		AvaloniaLocator.CurrentMutable
			.Bind<IWindowingPlatform>().ToConstant(windowingPlatform)
			.Bind<IStorageProviderFactory>().ToConstant(new GodotStorageProviderFactory())
			.Bind<PlatformHotkeyConfiguration>().ToConstant(CreatePlatformHotKeyConfiguration())
			.Bind<ManagedFileDialogOptions>().ToConstant(new ManagedFileDialogOptions { AllowDirectorySelection = true });
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
		try {
			Assembly headlessAssembly = Assembly.Load("Avalonia.Headless");

			// Try the Avalonia 12 class name first, then fallback to old name
			Type? stubType = headlessAssembly.GetType("Avalonia.Headless.HeadlessClipboardImplStub", false, false)
				?? headlessAssembly.GetType("Avalonia.Headless.ClipboardImplStub", false, false);

			if (stubType is null) {
				// Search for any class implementing IOwnedClipboardImpl in the assembly
				var iface = typeof(IOwnedClipboardImpl);
				stubType = headlessAssembly.GetTypes().FirstOrDefault(t => iface.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
			}

			if (stubType is not null) {
				var ctor = stubType.GetConstructor(
					bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					binder: null,
					types: Type.EmptyTypes,
					modifiers: null
				);

				if (ctor is not null) {
					var instance = ctor.Invoke(null);
					if (instance is IOwnedClipboardImpl owned)
						return owned;
				}
			}
		}
		catch {
			// Reflection failed, fall through to stub
		}

		return new GodotClipboardImplStub();
	}

	private sealed class GodotClipboardImplStub : IOwnedClipboardImpl {
		public Task ClearAsync() => Task.CompletedTask;
		public Task<IAsyncDataTransfer?> TryGetDataAsync() => Task.FromResult<IAsyncDataTransfer?>(null);
		public Task SetDataAsync(IAsyncDataTransfer dataTransfer) => Task.CompletedTask;
		public Task<bool> IsCurrentOwnerAsync() => Task.FromResult(false);
		public void Dispose() { }
	}
}
