using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PlatformDemo.AvaloniaUI.ViewModels;

public partial class MainViewModel : ViewModelBase {

	[ObservableProperty]
	private string _dialogResultText = string.Empty;

	[RelayCommand]
	private async Task ShowConfirmDialogAsync() {
		var result = await ShowYesNoDialogAsync("Avalonia", "Do you like Avalonia? Yes or No?");
		DialogResultText = result ? "You chose: Yes!" : "You chose: No!";
	}

	[RelayCommand]
	private async Task ShowInfoDialogAsync() {
		await ShowOkDialogAsync("Info", "This is an Avalonia information dialog running inside Godot.");
		DialogResultText = "Info dialog closed.";
	}

	[RelayCommand]
	private async Task ShowWarningDialogAsync() {
		await ShowOkDialogAsync("Warning", "Something requires your attention!");
		DialogResultText = "Warning dialog closed.";
	}

	private static Window? GetOwnerWindow() {
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			return desktop.MainWindow;
		return null;
	}

	private static async Task<bool> ShowYesNoDialogAsync(string title, string message) {
		var owner = GetOwnerWindow();
		if (owner is null)
			return false;

		var dialog = new Window {
			Title = title,
			Width = 360,
			Height = 180,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			CanResize = false,
			Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
			Content = BuildDialogPanel(
				message,
				("Yes", true), ("No", false)
			)
		};

		return await dialog.ShowDialog<bool>(owner);
	}

	private static async Task ShowOkDialogAsync(string title, string message) {
		var owner = GetOwnerWindow();
		if (owner is null)
			return;

		var dialog = new Window {
			Title = title,
			Width = 360,
			Height = 180,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			CanResize = false,
			Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
			Content = BuildDialogPanel(
				message,
				("OK", null)
			)
		};

		await dialog.ShowDialog(owner);
	}

	private static StackPanel BuildDialogPanel(string message, params (string Label, object? Result)[] buttons) {
		var stack = new StackPanel {
			Margin = new Thickness(24),
			Spacing = 16,
			VerticalAlignment = VerticalAlignment.Center,
			Children = {
				new TextBlock {
					Text = message,
					FontSize = 15,
					Foreground = Brushes.White,
					TextWrapping = TextWrapping.Wrap
				}
			}
		};

		var buttonPanel = new StackPanel {
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Spacing = 10
		};

		foreach (var (label, result) in buttons) {
			var btn = new Button {
				Content = label,
				MinWidth = 80,
				Height = 32,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			var capturedResult = result;
			btn.Click += (_, _) => {
				if (TopLevel.GetTopLevel(btn) is Window parentWindow) {
					if (capturedResult is bool b)
						parentWindow.Close(b);
					else
						parentWindow.Close();
				}
			};

			buttonPanel.Children.Add(btn);
		}

		stack.Children.Add(buttonPanel);
		return stack;
	}
}
