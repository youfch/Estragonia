using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace HelloWorld;

public partial class HelloWorldView : UserControl {

	private int _windowCount;

	public HelloWorldView() {
		InitializeComponent();
		OpenWindowButton.Click += OnOpenWindowClick;
		OpenFileDialogButton.Click += OnOpenFileDialogClick;
	}

	private void OnOpenWindowClick(object? sender, RoutedEventArgs e) {
		try {
			_windowCount++;
			var newWindow = new Window {
				Title = $"New Window #{_windowCount}",
				Width = 400,
				Height = 300,
				Content = new TextBlock {
					Text = $"This is window #{_windowCount}\nCreated from Avalonia in Godot!",
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
					VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
					FontSize = 18
				}
			};

			newWindow.Show();
			StatusText.Text = $"Opened window #{_windowCount}";
		}
		catch (Exception ex) {
			StatusText.Text = $"Error opening window: {ex.Message}";
		}
	}

	private async void OnOpenFileDialogClick(object? sender, RoutedEventArgs e) {
		try {
			var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
			if (storageProvider is null) {
				StatusText.Text = "Storage provider not available";
				return;
			}

			var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
				Title = "Open File",
				AllowMultiple = false,
				FileTypeFilter = new[] {
					new FilePickerFileType("All Files") { Patterns = new[] { "*" } },
					new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
					new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
				}
			});

			if (files.Count > 0) {
				var file = files[0];
				StatusText.Text = $"Selected: {file.Name}";
			}
			else {
				StatusText.Text = "No file selected";
			}
		}
		catch (Exception ex) {
			StatusText.Text = $"Error opening file dialog: {ex.Message}";
		}
	}

}
