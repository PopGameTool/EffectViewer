# Preview Export

EffectViewer supports two export flows:

- `Export Current File`: export the current resource file outside the project.
- `Export Preview`: export the current editor's rendered preview as an image or animation.

This document focuses on `File -> Export Preview...`.

## Open The Preview Export Dialog

Menu: `File -> Export Preview...`

Shortcuts:

- Windows/Linux: `Ctrl+E`
- macOS/iOS: `Command+E`

Editors that can export preview:

- Image
- Font
- Reanim
- Particle
- Trail
- ShowCase

The welcome page and project page cannot export previews.

## Export Formats

| Format | Output | Best For |
| --- | --- | --- |
| `Png` | Single `.png` | Static preview or current-frame screenshot. |
| `PngSequenceZip` | `.zip` containing PNG frames | Post-processing or frame-by-frame inspection. |
| `Gif` | Animated `.gif` | Quick sharing and broad compatibility. |
| `Webp` | Animated `.webp` | Better compression or Web usage. |

## Common Options

| Option | Description |
| --- | --- |
| `Format` | Select the export format. |
| `Canvas Scale` | Output resolution multiplier, from 0.1 to 16. |
| `FPS` | Animation frame rate, from 1 to 240; static PNG does not use it. |
| `Duration` | Total animation duration when the editor has no seekable timeline. |

Canvas scale examples:

- `1`: export at the base preview canvas size.
- `2`: export at double width and double height.
- `0.5`: export at half width and half height.

## Reanim Frame Range Export

The Reanim editor provides timeline options:

- `Full Timeline`
- Detected track/animation layer ranges

Options:

| Option | Description |
| --- | --- |
| `Timeline` | Select the full timeline or a layer range. |
| `Start Frame` | First frame to export, displayed as 1-based. |
| `End Frame` | Last frame for animated formats, displayed as 1-based. |

Notes:

- Frame ranges in the UI are displayed starting from 1.
- Reanim editor internals use 0-based frame indices.
- Selecting a timeline fills start/end frame fields automatically.
- Static PNG export uses only the start frame.

## Particle And Trail Time Range Export

Particle and Trail editors use time ranges in seconds.

Options:

| Option | Description |
| --- | --- |
| `Start Second` | Capture start time in seconds. |
| `End Second` | Capture end time in seconds. |
| `Export To End` | For animated export, capture until simulation completion or the maximum time limit. |

Limits:

- Seconds are clamped to the allowed time range.
- If end second is smaller than start second, it is corrected automatically.
- Animated export computes frame count from FPS.

## Image And Font Export

Image and Font are usually static previews:

- PNG: exports the current preview frame.
- Animated formats: if no timeline exists, `Duration` and `FPS` capture repeated or static frames.

The `Frame` selected in the image editor affects static preview export.

## ShowCase Export

ShowCase export:

1. Creates an isolated runtime world.
2. Runs the current script again.
3. Captures script scene frames.

Recommendations:

- Click `Run` before exporting and confirm there are no log errors.
- If the script depends on randomness, export may differ from the current playback state.
- If the script does not create a continuously updating frame provider, animated export may look static.

## File Name Rules

Preview export suggests a file name based on the editor title and selected range.

Examples:

- `fire-f0001.png`
- `fire-f0001-f0024.gif`
- `trail-t0s-2s.webp`
- `particle-t1.5s-end-frames.zip`

The extension is determined by the format:

- PNG: `.png`
- PNG sequence: `.zip`
- GIF: `.gif`
- WebP: `.webp`

## Export Progress

During export, a progress dialog can show:

- Capturing frames.
- Rendering frames.
- Rasterizing frames.
- Encoding image.
- Writing frames.

When export finishes, the status bar shows success or the failure reason.

## FAQ

### Why does animation export create many frames?

Frame count is roughly `Duration * FPS` or the selected frame range length. Reduce FPS, shorten duration, or narrow the range to reduce frame count.

### Why does GIF look worse than PNG?

GIF has limited color capabilities. Use PNG sequence or WebP for higher quality.

### Why did export fail?

Common causes:

- The current editor has no exportable preview.
- The script failed to run.
- The output path is not writable.
- Canvas scale is too high and causes memory pressure.
- The current resource definition cannot be encoded or rendered.

