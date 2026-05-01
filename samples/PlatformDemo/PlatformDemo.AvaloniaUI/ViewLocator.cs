using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PlatformDemo.AvaloniaUI.ViewModels;

namespace PlatformDemo.AvaloniaUI;

public class ViewLocator : IDataTemplate {
	public Control Build(object? data) {
		if (data is null)
			return new TextBlock { Text = "No data" };

		var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
		var type = Type.GetType(name);

		if (type is not null)
			return (Control) Activator.CreateInstance(type)!;

		return new TextBlock { Text = "Not Found: " + name };
	}

	public bool Match(object? data)
		=> data is ViewModelBase;
}
