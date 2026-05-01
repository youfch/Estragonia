using ControlsDemo.AvaloniaUI.ViewModels;
using ControlsDemo.AvaloniaUI.Views;
using Godot;
using JLeb.Estragonia;

namespace ControlsDemo.Godot;

public partial class UserInterface : AvaloniaControl {

	public override void _Ready() {
		GetWindow().SetImeActive(true);

		var viewModel = new MainViewModel();

		Control = new MainWindow {
			DataContext = viewModel
		};

		base._Ready();
	}

}
