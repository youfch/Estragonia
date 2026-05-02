using System;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace JLeb.Estragonia;

/// <summary>Contains extensions methods for <see cref="AppBuilder"/> related to Godot.</summary>
public static class AppBuilderExtensions {

	public static AppBuilder UseGodot(this AppBuilder builder) {
		// Register PlatformHotkeyConfiguration early so it's available
		// when UrsaSemiTheme XAML is loaded during App.Initialize().
		AvaloniaLocator.CurrentMutable
			.Bind<PlatformHotkeyConfiguration>()
			.ToConstant(OperatingSystem.IsMacOS()
				? new PlatformHotkeyConfiguration(commandModifiers: KeyModifiers.Meta, wholeWordTextActionModifiers: KeyModifiers.Alt)
				: new PlatformHotkeyConfiguration(commandModifiers: KeyModifiers.Control));

		return builder
			.UseManagedSystemDialogs()
			.UseStandardRuntimePlatformSubsystem()
			.UseSkia()
			.UseHarfBuzz()
			.UseWindowingSubsystem(GodotPlatform.Initialize);
	}

}
