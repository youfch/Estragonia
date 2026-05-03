namespace JLeb.Estragonia;

/// <summary>
/// Abstracts GPU surface layout transitions, decoupling from backend-specific
/// image layout concepts (e.g. Vulkan <c>VkImageLayout</c>).
/// </summary>
internal interface ISurfaceState {

	/// <summary>Transitions the surface to a render-target layout (e.g. Vulkan COLOR_ATTACHMENT_OPTIMAL).</summary>
	void TransitionToRender();

	/// <summary>Transitions the surface to a shader-readable layout (e.g. Vulkan SHADER_READ_ONLY_OPTIMAL).</summary>
	void TransitionToRead();

}
