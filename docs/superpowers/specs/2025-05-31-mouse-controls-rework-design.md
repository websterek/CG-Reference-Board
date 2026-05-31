# Mouse Controls Rework for CG Artists

**Date:** 2025-05-31
**Status:** Approved
**Architecture:** Approach 1 — Extend the interaction state machine

## Context

The current mouse/pointer input handling has a dual-path architecture: an interaction state machine (13 states) handles canvas-level gestures, while legacy code-behind in `MainWindow.*.cs` partial classes handles item-level drags, resize, and Alt-duplication. Several gestures are awkward or missing for CG artist workflows.

## Goals

- Left click selects, transforms, and draws selection rectangles in Grid Mode
- Middle click pans the canvas (no modifier needed)
- Alt+LMB drag pans the canvas (overrides item interaction)
- Alt+Middle drag controls zoom (vertical drag: up = zoom in, down = zoom out)
- Left+Middle simultaneous drag controls zoom (same behavior as Alt+Middle)
- `+`/`-` keys control zoom level in steps
- Remove Shift+LMB pan (redundant with Middle-drag and Alt+LMB)
- Remove Alt+LMB duplicate (replaced by other duplication methods)
- Clean, convenient, and testable implementation

## Final Gesture Mapping

| Input | Action | Implementation |
|---|---|---|
| LMB click on empty canvas | Clear selection | `MarqueePendingState` (existing) |
| LMB drag on empty canvas | Draw selection marquee | `MarqueePendingState` → `MarqueeSelectState` |
| LMB click/drag on cell | Select & drag item | Legacy code-behind (unchanged) |
| LMB on transform handle | Resize | Legacy code-behind (unchanged) |
| Ctrl+LMB drag | Additive marquee selection | `MarqueeSelectState(additive:true)` (existing) |
| Middle-drag (no modifiers) | Canvas pan | New `MiddleDragState` pan sub-mode |
| Alt+LMB drag | Canvas pan (always, overrides items) | New `AltPanState` |
| Alt+Middle vertical drag | Zoom (up=in, down=out) | `MiddleDragState` zoom sub-mode |
| Left+Middle vertical drag | Zoom (same as Alt+Middle) | `MiddleDragState` zoom sub-mode |
| Scroll wheel | Zoom at cursor | `Canvas_PointerWheelChanged` (existing) |
| `+` / `=` key | Zoom in (step) | `Window_KeyDown` handler → `ViewportService.ZoomAt()` |
| `-` key | Zoom out (step) | `Window_KeyDown` handler → `ViewportService.ZoomAt()` |
| Shift+LMB drag | — (removed) | — |
| Alt+LMB on item | — (removed, was duplicate) | — |

## State Machine Changes

### New States

**`AltPanState`**
- Entry condition: Alt+LMB pressed (routed from `IdleState`)
- On move: pan canvas via `ViewportService.PanBy()`
- Ignores hit tests — always pans, even over cells/annotations
- Pop to Idle on: release, Escape, capture lost

**`MiddleDragState`** (replaces `MiddleZoomState`)
- Entry condition: Middle button pressed
- **Pan sub-mode** (default): neither Alt nor Left held → Middle-drag pans canvas
- **Zoom sub-mode**: Alt held OR Left button also pressed → vertical drag controls zoom at anchor point
- Dynamic mode switching: adding/removing a modifier button during drag switches sub-mode in real-time
- Zoom math: `factor = exp(clamp(ΔY * sensitivity, ±0.08))` anchored at initial pointer position
- Pop to Idle on: release, Escape, capture lost

### Modified States

**`IdleState`** (`OnPointerPressed` routing priority)
1. Alt+LMB → `AltPanState`
2. Middle button → `MiddleDragState`
3. Ctrl+LMB → `MarqueePendingState` (additive marquee)
4. LMB on empty canvas → `MarqueePendingState` (normal marquee)
5. LMB on transform body → `TransformBodyMoveState`
6. LMB on cell/annotation → (no-op in state machine, legacy code-behind handles)

**`MarqueePendingState`** — Remove Shift+LMB pan dispatch branch

### Removed States

| State | Reason |
|---|---|
| `ShiftPanPendingState` | Shift+LMB pan removed |
| `AltDuplicateState` | Alt+LMB duplicate removed |
| `MiddleZoomState` | Replaced by `MiddleDragState` |

## Code Cleanup

### Files to Delete
- `Interaction/States/ShiftPanPendingState.cs`
- `Interaction/States/AltDuplicateState.cs`
- `Interaction/States/MiddleZoomState.cs`

### Legacy Code to Remove
- `Views/MainWindow.Cells.cs`: Alt+LMB cell duplicate logic (keymod check in `Cell_PointerPressed`)
- `Views/MainWindow.Annotations.cs`: Alt+LMB annotation duplicate logic (keymod check)
- `Views/MainWindow.axaml.cs`: `_isPanning` field and related checks in `CanvasBorder_Tunneled_PointerPressed` and `Canvas_PointerMoved`
- `IInteractionContext` / `MainWindowInteractionContext`: `CancelAltDuplicate()` and related plumbing

### New Code
- `Interaction/States/AltPanState.cs`
- `Interaction/States/MiddleDragState.cs`
- `Window_KeyDown` in `MainWindow.axaml.cs`: `+`/`=`/`-` key handlers calling `ViewportService.ZoomAt()`

## Keyboard Zoom Detail

Handled in `MainWindow.axaml.cs` `Window_KeyDown` (not in state machine — stateless operation):

- `Key.OemPlus` or `Key.D8` (with Shift for `+`) → `ZoomAt(viewportCenter, 1.1)`
- `Key.OemMinus` → `ZoomAt(viewportCenter, 1.0 / 1.1)`
- Viewport center = `(viewportWidth / 2, viewportHeight / 2)`

## Edge Cases

- **Alt released mid-pan:** `AltPanState` pops to Idle (pan cancelled)
- **Alt pressed mid-Middle-drag:** `MiddleDragState` switches from pan to zoom sub-mode
- **Left pressed mid-Middle-drag:** `MiddleDragState` switches from pan to zoom sub-mode
- **One button released during dual-hold:** Stay in whichever sub-mode matches remaining buttons
- **Alt+LMB over a cell:** Always pans (Alt overrides item hit-testing)

## Testing

### New Unit Tests
- `AltPanStateTests`: enter/exit, pan on move, pop on release/Escape/capture-lost, no-op on keydown
- `MiddleDragStateTests`: enter in pan mode, enter in zoom mode with Alt, enter in zoom mode with Left+Middle, pan tracking, zoom tracking (up=in, down=out), clamp at ±0.08, mode switch when modifier added mid-drag, mode switch when modifier released

### Modified Unit Tests
- `IdleStateTests`: verify Alt+LMB → AltPanState, Middle → MiddleDragState; remove Shift+LMB and AltDuplicate tests
- `MarqueePendingStateTests`: remove Shift+LMB pan branch tests

### Removed Tests
- `ShiftPanPendingStateTests.cs`
- `AltDuplicateStateTests.cs`
- `MiddleZoomStateTests.cs` (replaced by MiddleDragStateTests)

### Integration Tests
- `GestureIntegrationTests.cs`: update stubs to reflect new mapping (keep `MiddleAndLeftButton_ZomsViewport`, remove `ShiftLeftDrag_PansViewport`)

### Manual Validation Checklist
- Middle-drag pans in all directions
- Alt+LMB pans (over cells, over empty canvas)
- Left+Middle zooms (vertical up = zoom in, down = zoom out)
- Alt+Middle zooms
- `+`/`-` keys zoom in/out by step
- LMB still selects/drags cells
- Ctrl+LMB still does additive marquee
- Scroll wheel zoom still works
- Shift+LMB no longer pans
- Alt+LMB no longer duplicates

## Non-Goals

- Migrating item-level drags/resize into the state machine (future work)
- Modifying annotation mode pointer handling
- Changing selection behavior beyond removing Shift+LMB pan and Alt+LMB duplicate
- Tablet/stylus pressure sensitivity (already not present)
