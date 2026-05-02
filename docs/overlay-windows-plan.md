# Overlay Avalonia Windows Implementation Plan

## Goal
Render Avalonia `Window.Show()` as native Avalonia overlays within the existing `AvaloniaControl` — NO Godot `Window` nodes. The result looks and feels 100% like Avalonia because it IS rendered by Avalonia's own pipeline.

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Godot Viewport (main game window)              │
│  ┌───────────────────────────────────────────┐  │
│  │  AvaloniaControl                          │  │
│  │                                           │  │
│  │  _Draw():                                 │  │
│  │  1. Draw main TopLevel texture            │  │
│  │  2. For each overlay window (Z-order):    │  │
│  │     Draw shadow (optional)                │  │
│  │     Draw window texture at (x, y)         │  │
│  │                                           │  │
│  │  _GuiInput():                             │  │
│  │  1. Hit test overlay windows (top Z first)│  │
│  │  2. If hit → forward to that window       │  │
│  │  3. If miss → forward to main TopLevel    │  │
│  │                                           │  │
│  │  ┌─────────────────┐                      │  │
│  │  │ Overlay Window  │ ← Avalonia rendered  │  │
│  │  │ (title bar +    │   with managed        │  │
│  │  │  content)       │   decorations         │  │
│  │  └─────────────────┘                      │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

## Key Components

### 1. `GodotOverlayWindowImpl` (new, replaces `GodotWindowImpl`)
- Implements `IWindowImpl` — no Godot `Window` node
- Creates `GodotTopLevelImpl` for rendering (same as `AvaloniaControl`)
- Texture is composited by the parent `AvaloniaControl`
- Properties:
  - `NeedsManagedDecorations = true` (always)
  - `RequestedDrawnDecorations = TitleBar | Border | ResizeGrips`
  - `Borderless = true` conceptually (no OS chrome)
- Tracks: position, size, Z-order, visibility

### 2. `OverlayWindowManager` (new static registry)
- Maps `GodotOverlayWindowImpl` → `AvaloniaControl` host
- `AvaloniaControl` registers as host on `_Ready`
- Overlay windows query host for compositing
- Manages Z-order (list of active windows)
- Manages focus (which window receives input)

### 3. `AvaloniaControl` modifications
- `_Process()`: also triggers `OnDraw()` for all overlay windows
- `_Draw()`: after main texture, draws overlay window textures at their positions
- `_GuiInput()`: hit tests against overlay windows first, forwards input
- Registers itself as the overlay host

### 4. Input Routing
- Mouse position is translated to overlay window coordinates
- Z-order: topmost window gets first chance at input
- Click on non-focused window → bring to front + focus
- Click outside all windows → forward to main TopLevel

### 5. Window Management (via Avalonia managed decorations)
- Title bar drag → `BeginMoveDrag()` → manual position tracking
- Edge resize → `BeginResizeDrag()` → manual size + position tracking
- Close/Minimize/Maximize buttons → handled by Avalonia template → Window.Close() etc.

## Implementation Phases

### Phase 1: Core Overlay (Week 1-2) ✦ Current Focus
- [x] Create `GodotOverlayWindowImpl` implementing `IWindowImpl`
- [x] Create `OverlayWindowManager` registry
- [x] Modify `AvaloniaControl` to composite overlay textures
- [x] Route mouse/keyboard input to overlay windows
- [x] Window positioning (centered by default)
- [x] Window close/dispose lifecycle

### Phase 2: Window Interaction (Week 3-4)
- [ ] Title bar dragging (BeginMoveDrag → position tracking)
- [ ] Edge/corner resizing (BeginResizeDrag → size tracking)
- [ ] Z-order management (click to bring forward)
- [ ] Window activation/deactivation events
- [ ] Cursor shape changes (resize cursors at edges)

### Phase 3: Advanced Features (Week 5-8)
- [ ] Modal dialog support (ShowDialog)
- [ ] Owner window disabling during modal
- [ ] Window minimize/maximize/restore
- [ ] Window startup location (CenterOwner, CenterScreen)
- [ ] Multi-monitor support (via GodotScreenImpl)
- [ ] Popup overlay layer (tooltips, dropdowns within window)

### Phase 4: Polish & Edge Cases (Week 9-12)
- [ ] Performance optimization (dirty rect tracking)
- [ ] Window animation (open/close transitions)
- [ ] DPI scaling per-window
- [ ] Keyboard focus management across windows
- [ ] Accessibility (screen reader support)
- [ ] Stress testing (many windows, rapid open/close)
- [ ] Documentation and API cleanup

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `GodotOverlayWindowImpl.cs` | **Create** | New IWindowImpl without Godot Window node |
| `OverlayWindowManager.cs` | **Create** | Static registry for overlay windows |
| `AvaloniaControl.cs` | **Modify** | Add overlay compositing in _Draw/_Process/_GuiInput |
| `GodotWindowingPlatform.cs` | **Modify** | Return GodotOverlayWindowImpl instead of GodotWindowImpl |
| `GodotWindowImpl.cs` | **Keep** (backup) | Old implementation, can be removed later |
