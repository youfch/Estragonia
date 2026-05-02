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
			// Don't create a MainWindow in Godot mode — the main UI is hosted
			// in an AvaloniaControl node. The IClassicDesktopStyleApplicationLifetime
			// is registered solely to enable ShowDialog() for sub-windows.
			// We detect Godot mode by checking if the Args are null (Godot doesn't pass args).
			// On desktop, ClassicDesktopStyleApplicationLifetime.Args is set from StartWithClassicDesktopLifetime.
			if (desktop.Args is not null) {
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
		}

		base.OnFrameworkInitializationCompleted();
	}
}
