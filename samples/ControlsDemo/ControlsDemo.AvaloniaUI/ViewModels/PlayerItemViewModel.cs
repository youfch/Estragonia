using ControlsDemo.AvaloniaUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlsDemo.AvaloniaUI.ViewModels;

public partial class PlayerItemViewModel : ViewModelBase {

	[ObservableProperty]
	private string _name;

	[ObservableProperty]
	private PlayerStatus _status;

	public string StatusColor => Status switch {
		PlayerStatus.Online => "#4CAF50",
		PlayerStatus.Away => "#FF9800",
		_ => "#F44336"
	};

	public string DisplayText => $"{Status} — {Name}";

	public PlayerItemViewModel(string name, PlayerStatus status) {
		_name = name;
		_status = status;
	}

}
