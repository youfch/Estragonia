using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace JLeb.Estragonia;

/// <summary>
/// Minimal <see cref="IClassicDesktopStyleApplicationLifetime" /> for Godot mode.
/// Tracks open windows but doesn't manage application shutdown (Godot handles that).
/// </summary>
internal sealed class GodotApplicationLifetime : IClassicDesktopStyleApplicationLifetime, IDisposable {

	private readonly List<Window> _windows = new();
	private IDisposable? _eventSubscription;

	public string[]? Args { get; set; }

	public ShutdownMode ShutdownMode { get; set; }

	public Window? MainWindow { get; set; }

	public IReadOnlyList<Window> Windows => _windows;

	public event EventHandler<ControlledApplicationLifetimeStartupEventArgs>? Startup;
	public event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested;
	public event EventHandler<ControlledApplicationLifetimeExitEventArgs>? Exit;

	/// <summary>
	/// Subscribes to global window open/close events to track the <see cref="Windows" /> list.
	/// Must be called before any windows are created.
	/// </summary>
	public void Initialize() {
		var openedSubscription = Window.WindowOpenedEvent.AddClassHandler(
			typeof(Window),
			(sender, _) => {
				if (sender is Window window && !_windows.Contains(window))
					_windows.Add(window);
			});

		var closedSubscription = Window.WindowClosedEvent.AddClassHandler(
			typeof(Window),
			(sender, _) => {
				if (sender is Window window)
					_windows.Remove(window);
			});

		_eventSubscription = new CombinedDisposable(openedSubscription, closedSubscription);
	}

	public bool TryShutdown(int exitCode = 0) => false; // Godot manages app lifetime

	public void Shutdown(int exitCode = 0) { } // No-op

	public void Dispose() {
		_eventSubscription?.Dispose();
		_eventSubscription = null;
	}

	private sealed class CombinedDisposable : IDisposable {
		private readonly IDisposable _first;
		private readonly IDisposable _second;

		public CombinedDisposable(IDisposable first, IDisposable second) {
			_first = first;
			_second = second;
		}

		public void Dispose() {
			_first.Dispose();
			_second.Dispose();
		}
	}

}
