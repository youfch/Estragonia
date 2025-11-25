using System;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>An implementation of <see cref="IClipboard"/> that uses Godot clipboard methods.</summary>
internal sealed class GodotClipboard(IClipboardImpl clipboardImpl) : IClipboard {
	private readonly IClipboardImpl _clipboardImpl = clipboardImpl;
	private IAsyncDataTransfer? _lastDataTransfer;

	public Task<string?> GetTextAsync()
		=> Task.FromResult<string?>(DisplayServer.ClipboardGet());

	public Task SetTextAsync(string? text) {
		DisplayServer.ClipboardSet(text);
		return Task.CompletedTask;
	}

	public Task ClearAsync() {
		_lastDataTransfer?.Dispose();
		_lastDataTransfer = null;
		SetTextAsync(String.Empty);
		return _clipboardImpl.ClearAsync();
	}

	public Task SetDataObjectAsync(IDataObject data)
		=> Task.CompletedTask;

	public Task<object?> GetDataAsync(string format)
		=> Task.FromResult<object?>(null);

	public Task<IDataObject?> TryGetInProcessDataObjectAsync()
		=> Task.FromResult<IDataObject?>(null);

	public Task SetDataAsync(IAsyncDataTransfer? dataTransfer) {
		if (dataTransfer is null)
			return ClearAsync();
		if (_clipboardImpl is IOwnedClipboardImpl)
			_lastDataTransfer = dataTransfer;
		return _clipboardImpl.SetDataAsync(dataTransfer);
	}

	public Task FlushAsync()
	=> Task.CompletedTask;

	public Task<string[]> GetFormatsAsync()
		=> Task.FromResult(Array.Empty<string>());

	public Task<IAsyncDataTransfer?> TryGetDataAsync() {
		this.SetValueAsync(DataFormat.Text, DisplayServer.ClipboardGet());
		return _clipboardImpl.TryGetDataAsync();
	}

	public Task<IAsyncDataTransfer?> TryGetInProcessDataAsync() {
		return Task.FromResult<IAsyncDataTransfer?>(null);
	}
}
