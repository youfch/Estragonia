using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PlatformDemo.AvaloniaUI.ViewModels;

namespace PlatformDemo.AvaloniaUI.Views;

public partial class MainView : UserControl {
	public MainView() {
		InitializeComponent();
	}

	protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e) {
		base.OnLoaded(e);

		var dropZone = this.FindControl<Border>("DropZone");
		if (dropZone is not null) {
			DragDrop.SetAllowDrop(dropZone, true);
			dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
			dropZone.AddHandler(DragDrop.DropEvent, OnDrop);
		}
	}

	private void OnDragOver(object? sender, DragEventArgs e) {
		if (e.DataTransfer.Contains(DataFormat.File)) {
			e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);
		} else {
			e.DragEffects = DragDropEffects.None;
		}

		if (DataContext is MainViewModel vm)
			vm.IsDragOver = true;

		if (sender is Border border)
			border.Background = new SolidColorBrush(Color.Parse("#1B5E20"));
	}

	private void OnDrop(object? sender, DragEventArgs e) {
		if (DataContext is MainViewModel vm) {
			vm.IsDragOver = false;

			if (sender is Border border)
				border.Background = new SolidColorBrush(Color.Parse("#00251A"));

			if (e.DataTransfer.Contains(DataFormat.File)) {
				var files = e.DataTransfer.TryGetFiles();
				if (files is not null)
					vm.HandleDrop(files);
			} else {
				vm.DropResultText = "不支持的数据格式。";
			}
		}
	}
}
