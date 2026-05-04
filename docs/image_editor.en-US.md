# Image Editor

The image editor inspects and edits Image resources in a project. It mainly handles the image ID, atlas row/column counts, and selected preview frame.

## Open The Editor

1. Open or import a project that contains image resources.
2. Expand `Images` in the project explorer.
3. Click an image resource.

The editor header shows:

- The current image resource ID.
- The image path inside the project.

## Preview Area

The main area displays the current frame of the image or atlas.

Viewport controls:

- Mouse wheel: zoom.
- Middle-drag: pan.
- Left-drag: pan in editors without draggable content.
- Double-click: reset zoom and pan.

Use `View -> Light Viewport Background` or `View -> Dark Viewport Background` to change the preview background.

## Properties Panel

Menu: editor `Layout -> Properties`.

Editable fields:

| Field | Description |
| --- | --- |
| `ID` | Image resource ID. Scripts, particles, and animations use this ID to reference the image. |
| `Rows` | Vertical atlas split count, minimum 1, maximum 128. |
| `Cols` | Horizontal atlas split count, minimum 1, maximum 128. |
| `Frame` | Current preview frame index, starting at 0. |

Read-only information:

| Field | Description |
| --- | --- |
| `Cells` | `Rows * Cols`. |
| `Row` | Current frame row, starting at 0. |
| `Col` | Current frame column, starting at 0. |

## Edit Atlas Rows And Columns

If the image is a sprite atlas:

1. Set `Rows` to the vertical frame count.
2. Set `Cols` to the horizontal frame count.
3. Use `Frame` to inspect each atlas cell.
4. Save the current file or project.

Example:

- A 4-row, 5-column atlas should use `Rows = 4` and `Cols = 5`.
- The total frame count is 20, and valid frame indices are `0` through `19`.

## Change The Image ID

Steps:

1. Enter the new ID in the properties panel `ID` field.
2. The change is applied when the text box loses focus.
3. Save the project or current file.

Rules:

- Leading and trailing whitespace is trimmed.
- The ID cannot be empty.
- If the entered ID conflicts with another image in the same project, the editor tries to append `_2`, `_3`, and so on.

Impact:

- The project asset index is updated.
- Existing references in reanim, particle, trail, or Lua scripts are not automatically renamed; update them manually.

## Undo And Redo

Undo/redo supports:

- Image ID changes.
- Row count changes.
- Column count changes.

Shortcuts:

- Undo: Windows/Linux `Ctrl+Z`, macOS/iOS `Command+Z`
- Redo: Windows/Linux `Ctrl+Y` or `Ctrl+Shift+Z`, macOS/iOS `Command+Y` or `Command+Shift+Z`

## Save And Export

Save entries:

- `File -> Save Current File`
- `Project -> Save Project`
- `File -> Save All Files`

Image editor changes are saved in the project manifest. The original image pixels are not modified.

Export entry: `File -> Export Current File...`

Export copies the current image file to the selected location.

## FAQ

### Why did the preview frame change after editing rows or columns?

The editor clamps the current frame to the valid range. If the new `Rows * Cols` is smaller than the old frame index, the current frame moves to the last valid frame.

### Why does saving say the image ID cannot be empty?

The `ID` field in the properties panel cannot be empty. Enter a non-empty ID and save again.

### What frame number does the atlas start from?

The image editor uses frame indices starting at 0, and row/column display also starts at 0.

