using System;
using static JLeb.Estragonia.VkInterop;

namespace JLeb.Estragonia;

/// <summary>
/// Vulkan-specific <see cref="ISurfaceState"/> implementation.
/// Encapsulates a <c>VkImage</c>, its current layout, and the barrier helper
/// needed to transition between layouts.
/// </summary>
internal sealed class VkSurfaceState : ISurfaceState {

	private readonly VkImage _vkImage;
	private readonly VkBarrierHelper _barrierHelper;

	public VkImageLayout LastLayout { get; private set; }

	public VkSurfaceState(VkImage vkImage, VkImageLayout initialLayout, VkBarrierHelper barrierHelper) {
		_vkImage = vkImage;
		LastLayout = initialLayout;
		_barrierHelper = barrierHelper;
	}

	public void TransitionToRender()
		=> TransitionLayoutTo(VkImageLayout.COLOR_ATTACHMENT_OPTIMAL);

	public void TransitionToRead()
		=> TransitionLayoutTo(VkImageLayout.SHADER_READ_ONLY_OPTIMAL);

	private void TransitionLayoutTo(VkImageLayout newLayout) {
		if (LastLayout == newLayout)
			return;

		var sourceAccessMask = LastLayout switch {
			VkImageLayout.COLOR_ATTACHMENT_OPTIMAL => VkAccessFlags.COLOR_ATTACHMENT_READ_BIT,
			VkImageLayout.SHADER_READ_ONLY_OPTIMAL => VkAccessFlags.SHADER_READ_BIT,
			_ => VkAccessFlags.MEMORY_READ_BIT | VkAccessFlags.MEMORY_WRITE_BIT
		};

		var destinationAccessMask = newLayout switch {
			VkImageLayout.COLOR_ATTACHMENT_OPTIMAL => VkAccessFlags.COLOR_ATTACHMENT_WRITE_BIT,
			VkImageLayout.SHADER_READ_ONLY_OPTIMAL => VkAccessFlags.SHADER_WRITE_BIT,
			_ => VkAccessFlags.MEMORY_READ_BIT | VkAccessFlags.MEMORY_WRITE_BIT
		};

		_barrierHelper.TransitionImageLayout(_vkImage, LastLayout, sourceAccessMask, newLayout, destinationAccessMask);
		LastLayout = newLayout;
	}

}
