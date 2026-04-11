using System;
using Godot;

namespace JLeb.Estragonia;

/// <summary>Factory for creating the appropriate platform graphics implementation.</summary>
internal static class GodotPlatformGraphicsFactory {

	/// <summary>Creates the appropriate platform graphics implementation based on the current renderer.</summary>
	/// <returns>A Vulkan or Metal platform graphics implementation.</returns>
	public static IGodotPlatformGraphics Create() {
		var renderingDevice = RenderingServer.GetRenderingDevice();

		if (renderingDevice is null)
			throw new NotSupportedException("Estragonia requires Forward+ or Mobile renderer");

		if (ShouldUseMetal())
			return new GodotMtlPlatformGraphics();

		return new GodotVkPlatformGraphics();
	}

	/// <summary>Determines whether to use the Metal backend.</summary>
	private static bool ShouldUseMetal() {
		// Only use Metal on macOS/iOS
		if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsIOS())
			return false;

		// Check if user explicitly requested Vulkan via project settings
		var settings = ProjectSettings.Singleton;
		if (settings.HasSetting("rendering/rendering_device/driver.macos")) {
			var macosDriver = settings.GetSetting("rendering/rendering_device/driver.macos").AsString();
			if (macosDriver == "vulkan")
				return false; // User explicitly wants Vulkan (via MoltenVK)
		}

		// Default to Metal on Apple platforms
		return true;
	}

}
