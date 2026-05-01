using Avalonia;

namespace JLeb.Estragonia;

/// <summary>Contains extensions methods for <see cref="AppBuilder"/> related to Godot.</summary>
public static class AppBuilderExtensions {

	public static AppBuilder UseGodot(this AppBuilder builder)
		=> builder
			.UseStandardRuntimePlatformSubsystem()
			.UseSkia()
			.UseHarfBuzz()
			.UseWindowingSubsystem(GodotPlatform.Initialize);

}
