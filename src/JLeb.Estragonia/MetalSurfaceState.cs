namespace JLeb.Estragonia;

/// <summary>
/// Metal-specific <see cref="ISurfaceState"/> implementation.
/// Metal doesn't require explicit image layout transitions like Vulkan,
/// so all methods are no-ops.
/// </summary>
internal sealed class MetalSurfaceState : ISurfaceState {

	public void TransitionToRender() {
		// Metal manages resource synchronization implicitly — no layout transitions needed.
	}

	public void TransitionToRead() {
		// Metal manages resource synchronization implicitly — no layout transitions needed.
	}

}
