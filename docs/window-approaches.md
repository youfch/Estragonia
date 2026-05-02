# Avalonia Window Implementation in Estragonia

## Current Architecture

Estragonia uses **Godot native OS windows** to implement Avalonia `Window.Show()`. Each Avalonia `Window` creates a separate Godot `Window` node with native OS decorations (title bar, drag, resize, minimize/maximize).

### How it works

```
┌──────────────────────────────────────┐
│  Godot Main Viewport                 │
│  ┌────────────────────────────────┐  │
│  │  AvaloniaControl               │  │
│  │  (renders main TopLevel)       │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘

┌──────────────────────────────────────┐
│  Godot Window (separate OS window)   │
│  ┌────────────────────────────────┐  │
│  │  WindowHostControl             │  │
│  │  ┌────────────────────────┐    │  │
│  │  │  Avalonia Texture      │    │  │
│  │  │  (rendered via Skia)   │    │  │
│  │  └────────────────────────┘    │  │
│  └────────────────────────────────┘  │
│  (OS handles: drag, resize, title)   │
└──────────────────────────────────────┘
```

`GodotWindowImpl` implements `IWindowImpl` using a Godot `Window` node with `Borderless=false` and `GuiEmbedSubwindows=false`. The OS/Godot handles all window management (drag, resize, maximize, minimize, focus). Avalonia content is rendered inside the client area via `GodotTopLevelImpl` + `WindowHostControl`.

### Key properties

- `NeedsManagedDecorations = false` — OS provides window chrome
- `RequestedDrawnDecorations = None` — no Avalonia-managed decorations needed
- `Borderless = false` — native OS decorations enabled
- `GuiEmbedSubwindows = false` — sub-windows appear as separate OS windows

### Pros
- ✅ Godot/OS handles all window behavior natively: drag, resize, minimize/maximize, focus, Z-order
- ✅ Behavior matches user expectations: independent, draggable windows with taskbar entry
- ✅ Minimal code to maintain: no custom hit testing, drag, resize, or focus logic
- ✅ Native Alt+Tab, window snapping, and OS-level integration

### Cons
- ❌ Window chrome is OS-native, not Avalonia-themed (can switch to `Borderless=true` + managed decorations for Avalonia look)
- ❌ Sub-windows are separate OS windows, not embedded in the game viewport
- ❌ Godot version dependency: window behavior may vary across Godot versions

---

## Alternative Approaches (not currently used)

### EmbeddableControlRoot — Not viable

Avalonia's `EmbeddableControlRoot` is used on Android/iOS/Browser platforms. It doesn't implement `IWindowImpl`, so `Window.Show()` throws `NotSupportedException`. Fundamentally incompatible with multi-window support.

---

## File Dialogs

File dialogs work regardless of window implementation:

- **`IStorageProvider`** (Avalonia's standard API): Implemented in `GodotStorageProvider`, uses Godot's native `FileDialog`
- **Managed file dialogs**: Configured via `ManagedFileDialogOptions` in `GodotPlatform.Initialize()`, creates overlay windows via `GodotWindowingPlatform.CreateWindow()`