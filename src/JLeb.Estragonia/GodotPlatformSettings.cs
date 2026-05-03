using Avalonia.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>Implementation of <see cref="IPlatformSettings"/> for Godot.</summary>
internal sealed class GodotPlatformSettings : DefaultPlatformSettings {

	public override PlatformColorValues GetColorValues()
		=> new() {
			ThemeVariant = GodotPlatform.IsMobile
				? GetMobileThemeVariant()
				: PlatformThemeVariant.Dark,
			ContrastPreference = ColorContrastPreference.NoPreference,
			AccentColor1 = DisplayServer.GetAccentColor().ToAvaloniaColor()
		};

	private static PlatformThemeVariant GetMobileThemeVariant() {
		// On mobile, query the system theme preference.
		// DisplayServer doesn't expose a direct theme API, so we use
		// OS.HasFeature as a heuristic — iOS dark mode is detected via
		// UIScreen.MainScreen, Android via uiMode config, but Godot
		// doesn't expose these directly. Default to Dark for now,
		// which matches most game UI conventions.
		return PlatformThemeVariant.Dark;
	}

}
