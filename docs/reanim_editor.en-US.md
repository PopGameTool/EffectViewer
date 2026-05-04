# Reanim Editor

The Reanim editor inspects, previews, and edits `.reanim` / `.reanim.compiled` animation definitions. It supports tracks, frames, transforms, timeline selection, track visibility, tween metadata, and preview export.

## Open The Editor

1. Open a project that contains animation resources.
2. Expand `Reanim` in the project explorer.
3. Click the target reanim resource.

The editor header shows:

- The resource ID.
- The file path inside the project.

## Main Areas

- Central preview viewport: displays the current animation preview.
- Properties panel: shows structure information, selected-frame transform, tweens, image references, and errors.
- Bottom timeline: shows tracks and frames, and lets you select frames, toggle track visibility, and play the animation.

## Preview Viewport

Common controls:

- Mouse wheel: zoom.
- Middle-drag: pan.
- Double-click: reset view.
- Enable `Free Transform` to drag the selected frame object in the viewport.

Viewport background:

- `View -> Light Viewport Background`
- `View -> Dark Viewport Background`

## Timeline

The timeline has track headers on the left and frame cells on the right.

Click actions:

- Click a frame header: select that frame.
- Click a track name: select that track at the current frame.
- Click a cell: select a specific track and frame.
- Click the visibility box before a track name: toggle whether that track participates in preview.

Cell markers:

| Marker | Meaning |
| --- | --- |
| `I` | The frame has an image reference. |
| `*` | The frame has content, but not necessarily an image. |
| `T` | The frame belongs to a tween range. |

Timeline controls in the top-right:

| Control | Description |
| --- | --- |
| Layer dropdown | Switch between the full timeline and detected track/animation layers. |
| `FPS` | Set animation preview and export frame rate, from 1 to 240. |
| `Play` | Start or pause preview playback. |
| `Free Transform` | Allow viewport dragging for the selected frame transform. |

## Add And Remove Tracks

Menu: editor `Animation -> Add Track` / `Animation -> Remove Track`

Add track:

- Appends a track at the end of the animation.
- The new track uses the current animation frame count.
- The first frame uses a default visible transform; other frames use default empty transforms.

Remove track:

- At least 1 track must remain.
- Removes the currently selected track.
- Related tween metadata is adjusted.

## Add And Remove Frames

Menu: editor `Animation -> Add Frame` / `Animation -> Remove Frame`

Add frame:

- Inserts a new frame after the currently selected frame.
- Every track receives a new frame.
- Tween metadata is adjusted with the inserted frame.

Remove frame:

- At least 1 frame must remain.
- Removes the selected frame from every track.
- Related tween metadata is adjusted.

## Edit The Selected Frame Transform

The `Transform` section in the properties panel edits the currently selected track/frame.

Resource fields:

| Field | Description |
| --- | --- |
| `Track Name` | Current track name. |
| `Image ID` | Image ID bound to the selected frame. |
| `Font` | Font ID used by the selected frame. |
| `Text` | Text displayed by the selected frame. |

Numeric fields:

| Field | Description |
| --- | --- |
| `X` / `Y` | Translation position. |
| `Scale X` / `Scale Y` | Scale. |
| `Skew X` / `Skew Y` | Rotation/skew-related parameters. |
| `Frame` | Atlas frame index. |
| `Alpha` | Opacity. |
| `Visible` | Whether the selected frame is visible. |

Notes:

- If the selected frame belongs to a tween range, direct transform editing is disabled.
- Numeric text boxes apply their values when focus leaves the field.
- Track-name changes update the layer list and tween display names.

## Free Transform

Requirements:

- The current editor is a reanim editor.
- A track and frame are selected.
- The selected frame is not tweened.
- The selected frame is visible.

Controls:

- Drag inside the object: move.
- Drag square handles: scale.
- Drag diamond handles: skew.

Free transform directly edits the selected frame transform and marks the document as unsaved.

## Tween Metadata

The tween section provides:

- `Detect`: infer tween metadata from current frame data.
- `Add`: create a tween near the current track and frame.
- `Bake All`: write all tween results into frame data and clear tween metadata.

When a tween is selected, editable fields include:

| Field | Description |
| --- | --- |
| `Track` | Track number for the tween. |
| `Track Name` | Read-only track name. |
| `Start Frame` | Tween start frame. |
| `End Frame` | Tween end frame. |
| `Anchor X` / `Anchor Y` | Tween anchor point. |

Tween buttons:

| Button | Description |
| --- | --- |
| `Keyframe` | Split the current in-between tween frame into a keyframe. |
| `Bake` | Write the selected tween into frame data and remove that tween. |
| `Remove` | Delete the selected tween metadata. |

Notes:

- Tween metadata is stored on the reanim entry in the project manifest.
- When saving or exporting `.reanim`, current frame data is encoded from the edited state.

## Image Reference Check

The `Image References` section in the properties panel shows:

- `References`: image IDs requested by the animation file.
- `Resolved`: image resources found in the current project.
- `Missing`: image IDs referenced by the animation but missing from the project.

If a preview is missing textures, check the missing list first, then import or fix the matching image ID.

## Error Information

The `Errors` section shows file analysis errors, such as file not found or unsupported format.

## Undo And Redo

Undo/redo supports:

- Resource ID changes.
- Track/frame add and remove.
- Transform edits.
- Tween detection, add, remove, and bake operations.
- Timeline structure changes.

Shortcuts:

- Undo: Windows/Linux `Ctrl+Z`, macOS/iOS `Command+Z`
- Redo: Windows/Linux `Ctrl+Y` or `Ctrl+Shift+Z`, macOS/iOS `Command+Y` or `Command+Shift+Z`

## Save And Export

Save entries:

- `File -> Save Current File`
- `File -> Save All Files`

Export entry: `File -> Export Current File...`

Export formats:

- Source: `.reanim`
- Compiled: `.reanim.compiled`

Preview export:

- `File -> Export Preview...`
- Choose the full timeline or a detected layer range.
- Supports PNG, PNG sequence Zip, GIF, and WebP.

## FAQ

### Why is the selected frame not editable?

The frame may be inside a tween range. Make the frame a keyframe first, or bake/delete the corresponding tween.

### Why is the preview missing textures?

Check `Image References -> Missing`. If an image ID is missing, import the image or edit the frame's `Image ID`.

### Why does changing FPS alter animation speed?

`FPS` affects preview playback speed and the default reanim export frame rate. It represents how many frames are played per second.

