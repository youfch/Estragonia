using System;
using Avalonia;
using Avalonia.Dialogs;

namespace PlatformDemo.AvaloniaUI;

internal sealed class Program {
	[STAThread]
	public static void Main(string[] args) => BuildAvaloniaApp()
		.StartWithClassicDesktopLifetime(args);

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UseManagedSystemDialogs()
			.UsePlatformDetect()
			.LogToTrace();
}
