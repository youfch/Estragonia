using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Dialogs;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace JLeb.Estragonia;

/// <summary>Contains extensions methods for <see cref="AppBuilder"/> related to Godot.</summary>
public static class AppBuilderExtensions {

	/// <summary>
	/// Configures Avalonia to use the Godot platform backend.
	/// Call <see cref="SetupWithGodot"/> instead of <see cref="AppBuilder.SetupWithoutStarting"/>
	/// to enable <see cref="IClassicDesktopStyleApplicationLifetime"/> support.
	/// </summary>
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

	/// <summary>
	/// Sets up the Godot platform with the appropriate application lifetime for the current platform.
	/// On desktop, uses <see cref="IClassicDesktopStyleApplicationLifetime" />; on mobile, uses
	/// <see cref="ISingleViewApplicationLifetime" />.
	/// </summary>
	public static AppBuilder SetupWithGodot(this AppBuilder builder)
		=> GodotPlatform.IsMobile
			? builder.SetupWithLifetime(GodotPlatform.CreateMobileApplicationLifetime())
			: builder.SetupWithLifetime(GodotPlatform.CreateApplicationLifetime());

}
