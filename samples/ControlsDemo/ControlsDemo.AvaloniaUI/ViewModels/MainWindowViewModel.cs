using System.Collections.ObjectModel;
using ControlsDemo.AvaloniaUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ControlsDemo.AvaloniaUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase {

	public MainWindowViewModel() {
		Engines = ["Avalonia UI", "Godot Engine", "Unity", "Unreal Engine"];
		Players =
		[
			new PlayerItemViewModel("Player 1", PlayerStatus.Online),
			new PlayerItemViewModel("Player 2", PlayerStatus.Online),
			new PlayerItemViewModel("Player 3", PlayerStatus.Away),
			new PlayerItemViewModel("Player 4", PlayerStatus.Offline),
		];
	}

	// ─── Input Controls ───

	[ObservableProperty]
	private string _userName = "";

	[ObservableProperty]
	private string _password = "";

	[ObservableProperty]
	private string _notes = "Avalonia supports multi-line text editing.\nThis TextBox allows line breaks and word wrapping.";

	[ObservableProperty]
	private int _selectedEngineIndex;

	[ObservableProperty]
	private double _numericValue = 42;

	public ObservableCollection<string> Engines { get; }

	// ─── Selection Controls ───

	[ObservableProperty]
	private bool _isDarkMode = true;

	[ObservableProperty]
	private bool _showNotifications;

	[ObservableProperty]
	private bool _autoSave = true;

	// ─── Range Controls ───

	[ObservableProperty]
	private double _volume = 75;

	[ObservableProperty]
	private double _brightness = 50;

	[ObservableProperty]
	private double _downloadProgress = 65;

	// ─── Button Demo ───

	[ObservableProperty]
	private string _greetingText = "Click the button to see a greeting";

	[ObservableProperty]
	private int _counter;

	[RelayCommand]
	private void Greet()
		=> GreetingText = string.IsNullOrWhiteSpace(UserName)
			? "Please enter your name first!"
			: $"Hello, {UserName}!";

	[RelayCommand]
	private void Increment()
		=> Counter++;

	// ─── List ───

	public ObservableCollection<PlayerItemViewModel> Players { get; }

}
