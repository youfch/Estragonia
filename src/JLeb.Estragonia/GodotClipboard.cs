using System;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>An implementation of <see cref="IClipboard"/> that uses Godot clipboard methods.</summary>
internal sealed class GodotClipboard : IClipboard {

	public Task ClearAsync() {
		DisplayServer.ClipboardSet(String.Empty);
		return Task.CompletedTask;
	}

	public Task SetDataAsync(IAsyncDataTransfer? dataTransfer) {
		if (dataTransfer is null) {
			DisplayServer.ClipboardSet(String.Empty);
			return Task.CompletedTask;
		}

		// Try to extract text from the data transfer.
		string? text = null;
		foreach (var item in dataTransfer.Items) {
			foreach (var format in item.Formats) {
				if (format.Equals(DataFormat.Text)) {
					text = (item as IDataTransferItem)?.TryGetRaw(DataFormat.Text) as string;
					break;
				}
			}

			if (text is not null)
				break;
		}

		DisplayServer.ClipboardSet(text ?? String.Empty);
		return Task.CompletedTask;
	}

	public Task FlushAsync()
		=> Task.CompletedTask;

	public Task<IAsyncDataTransfer?> TryGetDataAsync() {
		var text = DisplayServer.ClipboardGet();

		if (String.IsNullOrEmpty(text))
			return Task.FromResult<IAsyncDataTransfer?>(null);

		var dataTransfer = new DataTransfer();
		dataTransfer.Add(DataTransferItem.CreateText(text));

		return Task.FromResult<IAsyncDataTransfer?>(dataTransfer);
	}

	public Task<IAsyncDataTransfer?> TryGetInProcessDataAsync()
		=> Task.FromResult<IAsyncDataTransfer?>(null);

}
