using Avalonia;
using Godot;
using JLeb.Estragonia;

namespace PlatformDemo.Godot;

public partial class AvaloniaLoader : Node {

	public override void _Ready()
		=> AppBuilder
			.Configure<AvaloniaUI.App>()
			.UseGodot()
			.SetupWithoutStarting();

}
