# Selection And Transform Tools Design

## Goal

Improve selection and transform interactions for both grid and annotation modes while preserving the app's current behavior and Avalonia MVVM structure.

The result should feel familiar to PureRef or Photoshop: drag-select with a marquee rectangle, then use one visible transform box with move and resize handles. Grid mode keeps grid snapping and collision rules. Annotation mode allows smooth freeform movement and resizing.

## Current Context

The app already has working but scattered interaction code:

- `SelectionService` tracks selected grid cells and annotations.
- `MainWindow.Cells.cs` handles grid cell click, drag, group drag, and the current bottom-right resize thumb.
- `MainWindow.Canvas.cs` handles canvas marquee selection, annotation marquee selection, annotation dragging, drawing, panning, and zooming.
- `MainWindow.Annotations.cs` handles annotation click selection, movement, duplication, deletion, and text editing.
- `GridLayoutService` owns grid collision checks.
- `CellViewModel` and `AnnotationViewModel` expose the geometry needed to compute selected bounds.

The implementation should refactor around these existing pieces rather than replacing unrelated canvas, layer, or rendering systems.

## Recommended Architecture

Use one shared transform overlay with mode-specific behavior.

Add a small `TransformService` owned by `MainWindowViewModel` alongside `SelectionService`. It should track:

- Current selected bounds in canvas coordinates.
- Whether the transform box is visible.
- Active transform operation: none, move, or resize.
- Active resize handle: top-left, top, top-right, right, bottom-right, bottom, bottom-left, or left.
- Capabilities for the active mode: can move, can resize, uses grid snapping, uses collision checks.

Keep `SelectionService` as the selection source of truth. The transform service derives bounds and capabilities from the selected cells, selected annotations, and current mode.

Use mode-specific transform behavior helpers:

- Grid transform behavior moves and resizes grid cells using `Constants.GridSize` snapping and `GridLayoutService` collision checks.
- Annotation transform behavior moves and resizes annotations smoothly without snapping.

This keeps the user-facing transform tool unified while isolating mode-specific rules for future extension.

## Selection Behavior

Selection should behave like standard graphics software:

- Drag empty canvas to draw a marquee rectangle.
- Regular marquee selection replaces the current selection.
- `Ctrl` click or `Ctrl` marquee modifies the current selection.
- Clicking a selected item or transform box body starts a move.
- Clicking outside the selection clears selection unless it starts a marquee.
- The transform box appears only when editable content is selected and the app is not in view mode.

Grid mode marquee selection should select intersecting grid cells and annotations, preserving the current mixed-selection behavior. Annotation mode marquee selection should operate on annotations only while the Move tool is active.

Selection bounds should be calculated from:

- Cells: `CanvasX`, `CanvasY`, `ColSpan`, and `RowSpan`.
- Annotations: cached absolute bounds from `AnnotationViewModel`. Text annotations should use the same rendered text measurement approach as `AnnotationShape` so the transform box matches visible content.

## Transform Overlay UI

Add one overlay layer in `MainWindow.axaml` above content layers and below transient UI such as drop previews. It should render:

- A thin accent bounding rectangle around the selected bounds.
- 8 resize handles at corners and edge midpoints.
- A move affordance over the selected body or bounding box interior.

The overlay should use zoom-independent sizing for outline thickness and handle size so it remains usable at all zoom levels. The visual style should be simple and familiar: Photoshop/PureRef-like, not decorative.

The current per-cell bottom-right resize thumb should be replaced by the transform overlay for editable selected cells. This avoids duplicate resize controls.

Individual selected item highlights can remain as secondary feedback, but the transform box is the primary manipulation affordance.

## Grid Mode Transform Rules

Grid mode keeps the app's existing board behavior:

- Moving selected grid items is snapped to `Constants.GridSize`.
- Moving selected annotations together with selected grid cells remains supported.
- Resizing a single grid cell updates `ColSpan` and `RowSpan` in grid units.
- Resizing multiple selected grid cells scales their rectangles relative to the group bounds, then snaps the result to grid units.
- Collision checks use `GridLayoutService`.
- Invalid grid transforms should show existing invalid feedback and revert on release.

Grid mode resizing applies to grid cells. Selected annotations in grid mode move with the group but do not expose independent annotation-specific transform options.

## Annotation Mode Transform Rules

Annotation mode uses smooth canvas-space transforms:

- Move selected annotations without grid snapping.
- Resize selected annotations by scaling geometry from the opposite anchor of the dragged handle.
- Brush, arrow, rectangle, ellipse, and text annotations all participate in smooth resizing.
- Cell transform handles are not available in annotation mode.

Annotation resizing should update annotation geometry directly. The implementation may normalize each annotation's `CanvasX` and `CanvasY` with local points after transform if that keeps rendering and hit testing consistent with existing `AnnotationShape` behavior.

## Pointer Flow

Pointer handling should delegate transform math to focused helpers instead of adding large new branches to existing event handlers.

Expected flow:

1. Pointer press checks transform handles first.
2. If a handle is hit, start resize and capture the pointer.
3. If the transform body or selected item is hit, start move.
4. If empty canvas is hit, start marquee selection.
5. Pointer move updates transform preview, selection marquee, drag, pan, or drawing based on current interaction state.
6. Pointer release commits valid transforms, reverts invalid grid transforms, saves board data when content changed, and clears transient state.

Existing panning, zooming, drawing, erasing, text editing, drag-to-duplicate, and view-mode restrictions must continue to work.

## Persistence And Undo

Transform commits should follow existing persistence behavior:

- Call `MarkUnsaved()` when content changes.
- Call `SaveBoardData()` after completed move or resize operations.
- Do not save during every pointer move.

Undo behavior should not be expanded in this change unless existing save/undo infrastructure already supports the transform operation naturally.

## Testing And Verification

Add unit tests where practical for pure transform logic:

- Selection bounds for cells, annotations, and mixed selections.
- Grid-snapped move deltas.
- Grid resize snapping.
- Annotation smooth scale calculations.

Manual verification should cover:

- Grid mode marquee selection.
- Annotation Move tool marquee selection.
- Grid-snapped move and resize.
- Collision feedback and revert in grid mode.
- Smooth annotation move and resize.
- Zoom-independent transform handle usability.
- Mixed cell and annotation movement in grid mode.
- View mode prevents transforms.
- Existing pan, zoom, draw, erase, text edit, and alt-drag duplicate behaviors still work.

## Out Of Scope

- Rotation handles.
- Perspective, skew, or free distort.
- New item-specific transform panels.
- Large rewrites of canvas rendering, layer management, or persistence format.
