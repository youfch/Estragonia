using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ControlsDemo.AvaloniaUI.ViewModels;
using ControlsDemo.AvaloniaUI.Views;

namespace ControlsDemo.AvaloniaUI;

public partial class App : Application {
	public override void Initialize() {
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted() {
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
			desktop.MainWindow = new Window {
				Title = "ControlsDemo",
				Width = 900,
				Height = 700,
				Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#121212")),
				Content = new MainWindow {
					DataContext = new MainWindowViewModel()
				}
			};
		}

		base.OnFrameworkInitializationCompleted();
	}
}