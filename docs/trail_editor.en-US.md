# Trail Editor

The Trail editor inspects, previews, and edits trail resources. It works with `.trail` / `.trail.compiled` files. Core parameters include image, maximum point count, minimum point distance, looping, width curves, alpha curves, and duration.

## Open The Editor

1. Open a project that contains trail resources.
2. Expand `Trails` in the project explorer.
3. Click the target trail resource.

The editor header shows:

- The resource ID.
- The file path inside the project.

## Main Areas

- Central preview viewport: displays the trail path and trail effect.
- Properties panel: edits the trail definition, image references, and errors.

## Basic Settings

The `Definition` section in the properties panel contains:

| Field | Description |
| --- | --- |
| `Image ID` | Image resource ID used by the trail. |
| `Max Points` | Maximum number of trail points, from 2 to the system maximum. |
| `Min Dist` | Minimum distance between new points, minimum 0. |
| `Loops` | Whether the trail loops. |

Suggestions:

- If the trail breaks or is too short, increase `Max Points`.
- If points are too dense and the shape jitters, increase `Min Dist`.
- If the preview has no texture, check whether `Image ID` exists.

## Parameter Tracks

Trail uses float parameter tracks to control width, alpha, and duration.

Track list:

| Track | Description |
| --- | --- |
| `WidthOverLength` | Width change along trail length. |
| `WidthOverTime` | Width change over time. |
| `AlphaOverLength` | Alpha change along trail length. |
| `AlphaOverTime` | Alpha change over time. |
| `TrailDuration` | Trail duration. |

Each track contains nodes:

| Field | Description |
| --- | --- |
| `Time` | Node time percentage, from 0 to 100. |
| `Low` | Low value. |
| `High` | High value. |
| `Curve` | Curve type over time. |
| `Distribution` | Distribution type between low and high values. |

Track actions:

- `Reset`: restore the default node.
- `Add`: add a node.
- `Copy`: duplicate the current node.
- `Remove`: delete the current node; each track keeps at least 1 node.

## Image Reference Check

The `Image References` section in the properties panel shows:

- The image ID referenced by the trail definition.
- Resolved image.
- Missing image.

If the preview trail has no texture or looks wrong, check the missing list first.

## Preview

Trail preview generates a sample path using the current definition.

Viewport controls:

- Mouse wheel to zoom.
- Middle-drag to pan.
- Double-click to reset the view.

Changing basic settings or tracks refreshes the preview.

## Undo And Redo

Undo/redo supports:

- Resource ID changes.
- Image ID changes.
- Max points, minimum distance, and loop changes.
- Width, alpha, and duration track edits.

Shortcuts:

- Undo: Windows/Linux `Ctrl+Z`, macOS/iOS `Command+Z`
- Redo: Windows/Linux `Ctrl+Y` or `Ctrl+Shift+Z`, macOS/iOS `Command+Y` or `Command+Shift+Z`

## Save And Export

Save entries:

- `File -> Save Current File`
- `File -> Save All Files`

Export entry: `File -> Export Current File...`

Export formats:

- Source: `.trail`
- Compiled: `.trail.compiled`

Preview export:

- `File -> Export Preview...`
- Trail editor uses time-range export.
- You can set start second, end second, export until end, FPS, and canvas scale.

## FAQ

### The trail is too thick or too thin. What should I change?

Adjust `WidthOverLength` and `WidthOverTime`. If you need a constant width, keep one node and set identical low/high values.

### The trail disappears too quickly.

Check the `TrailDuration` track and the `Loops` option. A short duration can make the trail fade out or finish quickly.

### Saving reports a track error.

Track nodes are encoded into the trail file. Check that node times are between 0 and 100 and that low/high values are reasonable numbers.

