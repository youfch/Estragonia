using System;
using Avalonia.Rendering;

namespace JLeb.Estragonia;

/// <summary>A <see cref="IRenderTimer"/> implementation that is only triggered manually.</summary>
internal sealed class ManualRenderTimer : IRenderTimer {

	private Action<TimeSpan>? _tick;

	public Action<TimeSpan>? Tick {
		get => _tick;
		set => _tick = value;
	}

	bool IRenderTimer.RunsInBackground
		=> false;

	public void TriggerTick(TimeSpan elapsed)
		=> _tick?.Invoke(elapsed);

}
