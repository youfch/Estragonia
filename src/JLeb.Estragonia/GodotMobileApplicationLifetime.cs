using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace JLeb.Estragonia;

/// <summary>
/// Minimal <see cref="ISingleViewApplicationLifetime" /> for mobile Godot mode.
/// Mobile platforms use a single main view — no multiple windows.
/// </summary>
internal sealed class GodotMobileApplicationLifetime : ISingleViewApplicationLifetime, IDisposable {

	public string[]? Args { get; set; }

	public Avalonia.Controls.Control? MainView { get; set; }

	public event EventHandler<ControlledApplicationLifetimeStartupEventArgs>? Startup;
	public event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested;
	public event EventHandler<ControlledApplicationLifetimeExitEventArgs>? Exit;

	public bool TryShutdown(int exitCode = 0) => false; // Godot manages app lifetime

	public void Shutdown(int exitCode = 0) { } // No-op

	public void Dispose() {
		// No subscriptions to clean up
	}

}
