using Godot;
using JLeb.Estragonia;
using PlatformDemo.AvaloniaUI.ViewModels;
using PlatformDemo.AvaloniaUI.Views;

namespace PlatformDemo.Godot;

public partial class UserInterface : AvaloniaControl {

	public override void _Ready() {
		GetWindow().SetImeActive(true);

		Control = new MainView {
			DataContext = new MainViewModel()
		};

		base._Ready();
	}

}
