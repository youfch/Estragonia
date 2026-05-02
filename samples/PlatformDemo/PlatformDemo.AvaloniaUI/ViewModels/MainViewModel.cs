using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PlatformDemo.AvaloniaUI.ViewModels;

public partial class MainViewModel : ViewModelBase {

	[ObservableProperty]
	private string _dialogResultText = string.Empty;

	[ObservableProperty]
	private string _realWindowResultText = string.Empty;

	/// <summary>
	/// Overlay panel shown during a dialog. When null, no dialog is visible.
	/// </summary>
	[ObservableProperty]
	private Border? _dialogOverlay;

	[ObservableProperty]
	private string _fileDialogResultText = string.Empty;

	[ObservableProperty]
	private string? _openFilePath;

	[ObservableProperty]
	private string? _saveFilePath;

	[ObservableProperty]
	private string? _folderPath;

	[RelayCommand]
	private void FileSelected(IReadOnlyList<IStorageItem> items) {
		FileDialogResultText = items.Count > 0
			? $"已选择: {items[0].TryGetLocalPath() ?? items[0].Name}"
			: "未选择。";
	}

	[RelayCommand]
	private void ShowConfirmDialog() {
		ShowDialog(
			"Avalonia",
			"Do you like Avalonia? Yes or No?",
			("Yes", () => DialogResultText = "You chose: Yes!"),
			("No", () => DialogResultText = "You chose: No!")
		);
	}

	[RelayCommand]
	private void ShowInfoDialog() {
		ShowDialog(
			"Info",
			"This is an Avalonia dialog running inside Godot via Estragonia.\n\n" +
			"It renders as an overlay within the same visual tree — no Window or IWindowImpl needed.",
			("OK", () => DialogResultText = "Info dialog closed.")
		);
	}

	[RelayCommand]
	private void ShowWarningDialog() {
		ShowDialog(
			"Warning",
			"Something requires your attention!",
			("Dismiss", () => DialogResultText = "Warning dialog closed.")
		);
	}

	/// <summary>
	/// Shows a real Avalonia Window using IWindowImpl (Godot sub-window).
	/// </summary>
	[RelayCommand]
	private void ShowRealWindow() {
		try {
			Window? window = null;
			window = new Window {
				Title = "Real Avalonia Window",
				Width = 420,
				Height = 280,
				Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
				Content = BuildRealWindowContent(
					"Real Avalonia Window",
					"This is a real Avalonia Window created via IWindowImpl.\n" +
					"It uses a Godot sub-window with its own rendering surface.",
					closeCallback: () => {
						window!.Close();
						RealWindowResultText = "Window closed.";
					}
				)
			};

			window.Show();
			RealWindowResultText = "Window shown (non-modal).";
		}
		catch (Exception ex) {
			RealWindowResultText = $"Error: {ex.Message}";
		}
	}

	/// <summary>
	/// Shows a real Avalonia dialog using IWindowImpl.
	/// Uses ShowDialog (modal) when an owner window is available,
	/// otherwise falls back to Show (non-modal).
	/// </summary>
	[RelayCommand]
	private async Task ShowRealDialogAsync() {
		try {
			Window? dialog = null;
			dialog = new Window {
				Title = "Avalonia Dialog",
				Width = 420,
				Height = 280,
				Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
				Content = BuildRealWindowContent(
					"Avalonia Dialog",
					"This is an Avalonia dialog created via IWindowImpl.\n" +
					"It renders as a Godot sub-window with OS decorations.",
					closeCallback: () => dialog!.Close()
				)
			};

			var owner = GetOwnerWindow();
			if (owner is not null) {
				dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
				dialog.CanResize = false;
				await dialog.ShowDialog(owner);
				RealWindowResultText = "Dialog closed (was modal).";
			} else {
				// No owner window available — show as non-modal
				dialog.Show();
				RealWindowResultText = "Dialog shown (non-modal, no owner window).";
			}
		}
		catch (Exception ex) {
			RealWindowResultText = $"Error: {ex.Message}";
		}
	}

	private static Window? GetOwnerWindow() {
		var lifetime = Application.Current?.ApplicationLifetime;
		if (lifetime is IClassicDesktopStyleApplicationLifetime desktop) {
			System.Diagnostics.Debug.WriteLine($"[PlatformDemo] GetOwnerWindow: Windows.Count={desktop.Windows.Count}, MainWindow={desktop.MainWindow?.Title ?? "null"}");
			// First try MainWindow (traditional desktop)
			if (desktop.MainWindow is { IsVisible: true } mainWindow)
				return mainWindow;
			// In Godot mode, MainWindow may be null. Find any visible window.
			foreach (var w in desktop.Windows) {
				System.Diagnostics.Debug.WriteLine($"[PlatformDemo]   checking window: {w.Title}, IsVisible={w.IsVisible}");
				if (w.IsVisible)
					return w;
			}
		} else {
			System.Diagnostics.Debug.WriteLine($"[PlatformDemo] GetOwnerWindow: lifetime is {lifetime?.GetType().Name ?? "null"}");
		}
		return null;
	}

	private static StackPanel BuildRealWindowContent(string title, string message, Action closeCallback) {
		var btn = new Button {
			Content = "Close",
			MinWidth = 80,
			Height = 32,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		btn.Click += (_, _) => closeCallback();

		return new StackPanel {
			Margin = new Thickness(24),
			Spacing = 16,
			Children = {
				new TextBlock {
					Text = title,
					FontSize = 18,
					FontWeight = FontWeight.SemiBold,
					Foreground = Brushes.White
				},
				new TextBlock {
					Text = message,
					FontSize = 14,
					Foreground = Brushes.White,
					TextWrapping = TextWrapping.Wrap
				},
				btn
			}
		};
	}

	private void ShowDialog(string title, string message, params (string Label, Action OnClick)[] buttons) {
		var buttonPanel = new StackPanel {
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Spacing = 10
		};

		foreach (var (label, onClick) in buttons) {
			var btn = new Button {
				Content = label,
				MinWidth = 80,
				Height = 32
			};
			btn.Click += (_, _) => {
				onClick();
				DialogOverlay = null;
			};
			buttonPanel.Children.Add(btn);
		}

		var card = new Border {
			Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(24),
			Width = 360,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Child = new StackPanel {
				Spacing = 16,
				Children = {
					new TextBlock {
						Text = title,
						FontSize = 18,
						FontWeight = FontWeight.SemiBold,
						Foreground = Brushes.White
					},
					new TextBlock {
						Text = message,
						FontSize = 14,
						Foreground = Brushes.White,
						TextWrapping = TextWrapping.Wrap
					},
					buttonPanel
				}
			}
		};

		DialogOverlay = new Border {
			Background = new SolidColorBrush(Color.Parse("#80000000")),
			Child = card
		};
	}
}
