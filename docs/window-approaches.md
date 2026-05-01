# Avalonia Window Implementation Approaches for Godot

## Overview

Three approaches to implement `Window.Show()` in Estragonia (Avalonia-on-Godot), each with distinct trade-offs.

| | A: Overlay Texture | B: Godot Embedded Window | C: EmbeddableControlRoot |
|---|---|---|---|
| **Status** | ✅ Current implementation | 🔶 Recommended | ❌ Not viable |
| **Core idea** | Render Avalonia window to texture, composite in `AvaloniaControl._Draw()` | Use Godot `Window` node with `gui_embed_subwindows=true`, Avalonia renders texture content | Use Avalonia's `EmbeddableControlRoot` (Android/iOS/Browser pattern) |
| **Window chrome** | Self-implemented (managed decorations) | Godot native (drag, resize, title bar) | N/A — doesn't support `Window.Show()` |
| **Drag/Resize** | Must implement manually | Godot handles natively | N/A |
| **Z-order** | Must manage manually | Godot handles natively | N/A |
| **Focus** | Must implement manually | Godot handles natively | N/A |
| **Avalonia fidelity** | High (Avalonia renders everything) | High (Avalonia renders content, Godot provides chrome) | N/A |
| **Implementation effort** | Very high (all window behavior from scratch) | Medium (bridge Avalonia rendering to Godot Window) | N/A |

---

## Approach A: Overlay Texture (Current)

### How it works

```
┌──────────────────────────────────────┐
│  Godot Viewport                      │
│  ┌────────────────────────────────┐  │
│  │  AvaloniaControl               │  │
│  │                                │  │
│  │  _Draw():                      │  │
│  │  1. Draw main TopLevel texture │  │
│  │  2. For each overlay window:   │  │
│  │     - Draw shadow              │  │
│  │     - Draw window texture      │  │
│  │                                │  │
│  │  _GuiInput():                  │  │
│  │  1. Hit test overlay windows   │  │
│  │  2. Forward input to hit       │  │
│  │  3. Handle drag/resize manually│  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
```

`GodotOverlayWindowImpl` implements `IWindowImpl` without any Godot `Window` node. The Avalonia window is rendered to a texture by `GodotTopLevelImpl`, then composited into the parent `AvaloniaControl`'s `_Draw()` method.

### Pros
- ✅ Pure Avalonia rendering — window content looks exactly like Avalonia
- ✅ No Godot Window node dependency
- ✅ Works with any Avalonia theme (Fluent, Simple, Semi, etc.)
- ✅ Full control over rendering pipeline
- ✅ Already partially implemented (Phase 1 complete)

### Cons
- ❌ **Must implement all window behavior from scratch**: drag, resize, focus, Z-order, minimize/maximize, modal dialogs
- ❌ **Behavior diverges from Avalonia**: "对话框和avalonia的行为不一致" — overlay windows aren't independent, can't be dragged outside the parent area
- ❌ **High ongoing maintenance**: every Avalonia window feature needs manual reimplementation
- ❌ **Input handling complexity**: hit testing, drag forwarding, focus management all custom
- ❌ **No OS-level integration**: no taskbar entry, no Alt+Tab, no native window snapping
- ❌ **Modal dialog support difficult**: `ShowDialog()` requires owner window support

### Current status
- Phase 1 (core overlay): ✅ Complete
- Phase 2 (window interaction): 🔶 Drag partially working, resize not implemented
- Phase 3 (advanced features): ❌ Not started
- User feedback: "当前无法拖拽，也不独立，行为和avalonia差异较大"

---

## Approach B: Godot Embedded Window (Recommended)

### How it works

```
┌──────────────────────────────────────┐
│  Godot Viewport                      │
│  (gui_embed_subwindows = true)       │
│  ┌────────────────────────────────┐  │
│  │  AvaloniaControl               │  │
│  │                                │  │
│  │  ┌──────────────────────────┐  │  │
│  │  │  Godot Window node       │  │  │  ← Godot handles:
│  │  │  ┌────────────────────┐  │  │  │    - Drag/resize
│  │  │  │  SubViewport       │  │  │  │    - Z-order/focus
│  │  │  │  ┌──────────────┐  │  │  │  │    - Title bar
│  │  │  │  │ Avalonia     │  │  │  │  │    - Minimize/maximize
│  │  │  │  │ Texture      │  │  │  │  │
│  │  │  │  └──────────────┘  │  │  │  │
│  │  │  └────────────────────┘  │  │  │
│  │  └──────────────────────────┘  │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
```

When `gui_embed_subwindows = true`, Godot renders `Window` nodes within the parent viewport instead of creating separate OS windows. The `GodotEmbeddedWindowImpl` creates a Godot `Window` node, adds a `SubViewport` inside it, and renders Avalonia content as a texture in that viewport. Godot natively handles all window management.

### Pros
- ✅ **Godot handles window behavior natively**: drag, resize, minimize/maximize, focus, Z-order
- ✅ **Behavior matches user expectations**: independent, draggable windows
- ✅ **Much less code to maintain**: no custom hit testing, drag, resize, or focus logic
- ✅ **Modal dialog support**: Godot's window system supports parent/owner relationships
- ✅ **Avalonia renders content**: window interior still looks like Avalonia
- ✅ **Godot title bar can be hidden**: use `borderless = true` + Avalonia managed decorations for full Avalonia look

### Cons
- ❌ **Two rendering systems**: Godot renders window chrome, Avalonia renders content — potential visual inconsistency
- ❌ **Godot Window styling**: default Godot window chrome doesn't match Avalonia themes (but can use `borderless = true` + Avalonia managed decorations)
- ❌ **SubViewport overhead**: each window needs its own Godot SubViewport + texture
- ❌ **Coordinate translation**: input events need translation between Godot Window coordinates and Avalonia coordinates
- ❌ **Godot version dependency**: `gui_embed_subwindows` behavior may vary across Godot versions
- ❌ **Window cannot extend beyond parent**: embedded windows are clipped to the parent viewport (same limitation as Approach A)

### Implementation sketch

```csharp
// GodotEmbeddedWindowImpl : IWindowImpl
// - Creates Godot Window node as child of AvaloniaControl's parent
// - Adds SubViewport inside Window for Avalonia rendering
// - Uses GodotTopLevelImpl to render Avalonia content into SubViewport
// - Translates input events from Godot Window → Avalonia coordinates
// - For full Avalonia look: Window.Borderless = true + managed decorations
```

---

## Approach C: EmbeddableControlRoot (Not Viable)

### How it works

Avalonia's `EmbeddableControlRoot` is used on Android, iOS, and Browser platforms where there's no OS window system. It creates a `TopLevel` without `IWindowImpl` support.

### Why it doesn't work

- ❌ **`Window.Show()` throws `NotSupportedException`**: `EmbeddableControlRoot` doesn't implement `IWindowImpl.CreateWindow()`
- ❌ **No window lifecycle**: can't show/hide/close windows independently
- ❌ **Only supports single TopLevel**: designed for single-view embedding, not multi-window

This approach is fundamentally incompatible with the goal of supporting `Window.Show()`.

---

## Recommendation

**Approach B (Godot Embedded Window)** is recommended for the following reasons:

1. **Dramatically reduces implementation effort**: Godot handles drag, resize, focus, Z-order, and window lifecycle — features that took weeks to partially implement in Approach A
2. **Matches user expectations**: "avalonia是一个独立可拖拽的窗口" — embedded windows are independent and draggable
3. **Sustainable maintenance**: no need to reimplement every Avalonia window feature
4. **Path to full Avalonia look**: use `borderless = true` + Avalonia managed decorations to get Avalonia-styled title bars while keeping Godot's window behavior

### Migration path from A → B

1. Create `GodotEmbeddedWindowImpl` implementing `IWindowImpl`
2. In `GodotWindowingPlatform`, return `GodotEmbeddedWindowImpl` instead of `GodotOverlayWindowImpl`
3. Remove `OverlayWindowManager` (Godot handles Z-order/focus)
4. Remove overlay compositing code from `AvaloniaControl._Draw()` / `_GuiInput()`
5. Keep `GodotOverlayWindowImpl` as fallback option

---

## File Dialogs (Cross-Approach)

File dialogs work regardless of which window approach is used:

- **`IStorageProvider`** (Avalonia's standard API): Already implemented in `GodotStorageProvider`, uses Godot's native `FileDialog`
- **Ursa `PathPicker`**: Calls `TopLevel.GetTopLevel(this)?.StorageProvider` → `GodotStorageProvider` → Godot native dialog ✅
- **Ursa `OverlayDialog` / `MessageBox`**: Pure Avalonia visual tree, works with any approach ✅

No additional work needed for file dialog support.
