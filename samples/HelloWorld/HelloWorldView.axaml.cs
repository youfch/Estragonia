using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HelloWorld;

public partial class HelloWorldView : UserControl {

	private int _clickCount;
	private DispatcherTimer? _progressTimer;
	private double _progressValue;

	public HelloWorldView() {
		InitializeComponent();
		InitializeInteractions();
		InitializePlayerStats();
		StartProgressAnimation();
	}

	private void InitializeInteractions() {
		// Button click handlers
		NormalButton.Click += (_, _) => {
			_clickCount++;
			StatusText.Text = $"Normal button clicked! Total clicks: {_clickCount}";
		};

		AccentButton.Click += (_, _) => {
			StatusText.Text = "Accent button clicked! This is a primary action button.";
		};

		CounterButton.Click += (_, _) => {
			var currentCount = int.Parse(CounterButton.Content?.ToString()?.Split(' ')[2] ?? "0");
			currentCount++;
			CounterButton.Content = $"Click Count: {currentCount}";
			StatusText.Text = $"Counter button: {currentCount} clicks";
		};

		// Slider value handlers
		VolumeSlider.ValueChanged += (_, e) => {
			VolumeValue.Text = $"{e.NewValue:F0}%";
			StatusText.Text = $"Master Volume set to {e.NewValue:F0}%";
		};

		SfxSlider.ValueChanged += (_, e) => {
			SfxValue.Text = $"{e.NewValue:F0}%";
		};

		MusicSlider.ValueChanged += (_, e) => {
			MusicValue.Text = $"{e.NewValue:F0}%";
		};

		// CheckBox handlers
		VSyncCheck.IsCheckedChanged += (_, _) => {
			UpdateStatusFromToggles();
		};

		FullscreenCheck.IsCheckedChanged += (_, _) => {
			UpdateStatusFromToggles();
		};

		ShadowsCheck.IsCheckedChanged += (_, _) => {
			UpdateStatusFromToggles();
		};

		// ToggleSwitch handler
		MasterToggle.IsCheckedChanged += (_, _) => {
			var isEnabled = MasterToggle.IsChecked ?? false;
			VSyncCheck.IsEnabled = isEnabled;
			FullscreenCheck.IsEnabled = isEnabled;
			ShadowsCheck.IsEnabled = isEnabled;
			VolumeSlider.IsEnabled = isEnabled;
			SfxSlider.IsEnabled = isEnabled;
			MusicSlider.IsEnabled = isEnabled;
			StatusText.Text = isEnabled ? "Master switch ON - All controls enabled" : "Master switch OFF - Controls disabled";
		};

		// RadioButton handlers
		QualityLow.IsCheckedChanged += (_, _) => { if (QualityLow.IsChecked == true) UpdateQualityStatus("Low"); };
		QualityMedium.IsCheckedChanged += (_, _) => { if (QualityMedium.IsChecked == true) UpdateQualityStatus("Medium"); };
		QualityHigh.IsCheckedChanged += (_, _) => { if (QualityHigh.IsChecked == true) UpdateQualityStatus("High"); };
		QualityUltra.IsCheckedChanged += (_, _) => { if (QualityUltra.IsChecked == true) UpdateQualityStatus("Ultra"); };

		// ComboBox handler
		ResolutionCombo.SelectionChanged += (_, _) => {
			var selected = ResolutionCombo.SelectedItem as ComboBoxItem;
			if (selected != null) {
				ResolutionStatus.Text = $"Selected: {selected.Content}";
				StatusText.Text = $"Resolution changed to {selected.Content}";
			}
		};

		// TextBox handlers
		PlayerNameInput.TextChanged += (_, _) => {
			if (!string.IsNullOrEmpty(PlayerNameInput.Text) && PlayerNameInput.Text.Length > 3) {
				StatusText.Text = $"Player name updated: {PlayerNameInput.Text}";
			}
		};

		// Set initial selection
		ResolutionCombo.SelectedIndex = 0;
	}

	private void UpdateStatusFromToggles() {
		var vsync = VSyncCheck.IsChecked ?? false ? "ON" : "OFF";
		var fullscreen = FullscreenCheck.IsChecked ?? false ? "ON" : "OFF";
		var shadows = ShadowsCheck.IsChecked ?? false ? "ON" : "OFF";
		StatusText.Text = $"Settings: VSync={vsync}, Fullscreen={fullscreen}, Shadows={shadows}";
	}

	private void UpdateQualityStatus(string quality) {
		StatusText.Text = $"Graphics Quality set to: {quality}";
	}

	private void InitializePlayerStats() {
		// Create sample player stats with colored indicators
		var stats = new[] {
			("Health", "100/100", "#4CAF50"),
			("Mana", "85/100", "#2196F3"),
			("Stamina", "60/100", "#FF9800"),
			("Experience", "2,450 / 5,000", "#9C27B0"),
			("Strength", "25", "#F44336"),
			("Dexterity", "18", "#00BCD4"),
			("Intelligence", "32", "#3F51B5"),
			("Defense", "15", "#795548"),
			("Speed", "12", "#607D8B"),
			("Luck", "7", "#FFC107"),
			("Gold", "1,250", "#FFD700"),
			("Items", "42/99", "#4CAF50"),
			("Quests Active", "3", "#FF5722"),
			("Quests Completed", "47", "#8BC34A"),
			("Play Time", "12h 34m", "#E91E63"),
		};

		foreach (var (name, value, color) in stats) {
			var itemBorder = new Border {
				Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D")),
				CornerRadius = new Avalonia.CornerRadius(4),
				Padding = new Avalonia.Thickness(12, 8),
				Margin = new Avalonia.Thickness(0, 2)
			};

			var grid = new Grid {
				ColumnDefinitions = new Avalonia.Controls.ColumnDefinitions("*,Auto,Auto")
			};

			var nameText = new TextBlock {
				Text = name,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
				Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0E0E0"))
			};

			var indicator = new Border {
				Width = 12,
				Height = 12,
				CornerRadius = new Avalonia.CornerRadius(6),
				Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color)),
				Margin = new Avalonia.Thickness(8, 0),
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			};

			var valueText = new TextBlock {
				Text = value,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
				FontWeight = Avalonia.Media.FontWeight.SemiBold,
				Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color))
			};

			grid.Children.Add(nameText);
			grid.Children.Add(indicator);
			grid.Children.Add(valueText);

			Grid.SetColumn(nameText, 0);
			Grid.SetColumn(indicator, 1);
			Grid.SetColumn(valueText, 2);

			itemBorder.Child = grid;
			StatsList.Children.Add(itemBorder);
		}
	}

	private void StartProgressAnimation() {
		_progressValue = 0;
		_progressTimer = new DispatcherTimer {
			Interval = TimeSpan.FromMilliseconds(50)
		};

		_progressTimer.Tick += (_, _) => {
			_progressValue += 2;
			if (_progressValue > 100) {
				_progressValue = 0;
				ProgressStatus.Text = "Loading complete! Restarting...";
			}
			else {
				LoadingProgress.Value = _progressValue;
				if (_progressValue < 30) {
					ProgressStatus.Text = "Initializing...";
				}
				else if (_progressValue < 60) {
					ProgressStatus.Text = "Loading assets...";
				}
				else if (_progressValue < 90) {
					ProgressStatus.Text = "Finalizing...";
				}
				else {
					ProgressStatus.Text = "Almost done...";
				}
			}
		};

		_progressTimer.Start();
	}

}
