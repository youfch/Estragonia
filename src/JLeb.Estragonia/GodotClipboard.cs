using System;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>An implementation of <see cref="IClipboard"/> that uses Godot clipboard methods.</summary>
internal sealed class GodotClipboard : IClipboard {

	public Task<string?> GetTextAsync()
		=> Task.FromResult<string?>(DisplayServer.ClipboardGet());

	public Task SetTextAsync(string? text) {
		DisplayServer.ClipboardSet(text);
		return Task.CompletedTask;
	}

	public Task ClearAsync()
		=> SetTextAsync(String.Empty);

	public Task FlushAsync()
	=> Task.CompletedTask;

	public Task SetDataObjectAsync(IDataObject data)
		=> Task.CompletedTask;

	public Task<string[]> GetFormatsAsync()
		=> Task.FromResult(Array.Empty<string>());

	public Task<object?> GetDataAsync(string format)
		=> Task.FromResult<object?>(null);

	public Task<IDataObject?> TryGetInProcessDataObjectAsync()
		=> Task.FromResult<IDataObject?>(null);
}
