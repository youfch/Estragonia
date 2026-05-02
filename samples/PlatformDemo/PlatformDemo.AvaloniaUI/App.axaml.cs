using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PlatformDemo.AvaloniaUI.ViewModels;
using PlatformDemo.AvaloniaUI.Views;

namespace PlatformDemo.AvaloniaUI;

public partial class App : Application {
	public override void Initialize() {
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted() {
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
			desktop.MainWindow = new Window {
				Title = "PlatformDemo",
				Width = 600,
				Height = 500,
				Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E")),
				Content = new MainView {
					DataContext = new MainViewModel()
				}
			};
		}

		base.OnFrameworkInitializationCompleted();
	}
}
