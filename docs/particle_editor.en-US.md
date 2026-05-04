# Particle Editor

The Particle editor inspects, previews, and edits particle system definitions. It works with `.xml` / `.xml.compiled` particle resources and supports emitters, parameter tracks, flags, particle fields, and system fields.

## Open The Editor

1. Open a project that contains particle resources.
2. Expand `Particles` in the project explorer.
3. Click the target particle resource.

The editor header shows:

- The resource ID.
- The file path inside the project.

## Main Areas

- Central preview viewport: plays the particle system in real time.
- Properties panel: edits the resource ID, emitters, tracks, fields, and image references.

## Emitter List

The top of the `Definition` section contains the emitter dropdown.

Actions:

- Select emitter: switch the emitter being edited.
- `Add`: append a new emitter.
- `Remove`: delete the current emitter.

Emitter display:

- With name: `number. name`
- Without name: `Emitter number`

## General Settings

The emitter `General` section contains:

| Field | Description |
| --- | --- |
| `Name` | Emitter name. |
| `Image ID` | Image resource ID used by the particles. |
| `Image Row` | Atlas row index, starting at 0. |
| `Image Col` | Atlas column index, starting at 0. |
| `Image Frames` | Number of image frames used by particle animation, minimum 1. |
| `Emitter Type` | Emitter shape/behavior type. |
| `Animated` | Whether to play image frames as an animation. |

## Flags

The emitter `Flags` section provides boolean switches:

- `Random Launch Spin`
- `Align Launch Spin`
- `Align To Pixels`
- `System Loops`
- `Particle Loops`
- `Particles Dont Follow`
- `Die If Overloaded`
- `Additive`
- `Fullscreen`
- `Software Only`
- `Hardware Only`

These switches are written back to the particle definition flags.

## Parameter Tracks

The Particle editor uses many float parameter tracks. Each track contains one or more nodes.

Each node contains:

| Field | Description |
| --- | --- |
| `Time` | Node time percentage, from 0 to 100. |
| `Low` | Low value. |
| `High` | High value. |
| `Curve` | Curve type over time. |
| `Distribution` | Distribution type between low and high values. |

Track actions:

- `Reset`: restore the default single-node track.
- `Add`: add a node after the last node.
- `Copy`: duplicate the current node and offset its time by 1.
- `Remove`: delete the current node; each track keeps at least 1 node.

Nodes are automatically sorted by `Time`, and values are rounded to three decimals.

## Track Groups

Emitter parameters are grouped by section:

| Section | Main Tracks |
| --- | --- |
| `Lifetime` | `SystemDuration`, `CrossFadeDuration`, `ParticleDuration` |
| `Spawn` | `SpawnRate`, `SpawnMinActive`, `SpawnMaxActive`, `SpawnMaxLaunched` |
| `Emitter Shape` | `EmitterRadius`, `EmitterOffsetX/Y`, `EmitterBoxX/Y`, `EmitterSkewX/Y`, `EmitterPath` |
| `Launch` | `LaunchSpeed`, `LaunchAngle` |
| `System Color` | `SystemRed/Green/Blue/Alpha/Brightness` |
| `Particle Color` | `ParticleRed/Green/Blue/Alpha/Brightness` |
| `Particle Transform` | `ParticleScale`, `ParticleStretch`, `ParticleSpinAngle`, `ParticleSpinSpeed` |
| `Rendering` | `AnimationRate`, `ClipTop/Bottom/Left/Right` |
| `Collision` | `CollisionReflect`, `CollisionSpin` |

## Particle Fields And System Fields

`Particle Fields` and `System Fields` are both lists of field definitions.

Field contents:

| Field | Description |
| --- | --- |
| `Field Type` | Field type. |
| `x` | X-direction parameter track. |
| `y` | Y-direction parameter track. |

Actions:

- `Add`: add a field.
- `Remove`: delete the current field.

Particle fields usually affect individual particles, while system fields usually affect the whole system or emitter context.

## Image Reference Check

The `Image References` section in the properties panel shows:

- Image IDs referenced by the particle definition.
- Image IDs resolved in the project.
- Missing image IDs.

If the preview has no texture or the particles are invisible, first check:

1. Whether the emitter `Image ID` is empty.
2. Whether the image ID exists in project image resources.
3. Whether image row, column, and frame count match the atlas settings.

## Preview

The particle preview continuously runs the current definition.

Viewport controls:

- Mouse wheel to zoom.
- Middle-drag to pan.
- Double-click to reset the view.

Changing emitters, tracks, or fields refreshes the preview.

## Undo And Redo

Undo/redo supports:

- Resource ID changes.
- Emitter add/remove.
- Emitter general fields and flags.
- Parameter track and node edits.
- Particle field / system field edits.

Shortcuts:

- Undo: Windows/Linux `Ctrl+Z`, macOS/iOS `Command+Z`
- Redo: Windows/Linux `Ctrl+Y` or `Ctrl+Shift+Z`, macOS/iOS `Command+Y` or `Command+Shift+Z`

## Save And Export

Save entries:

- `File -> Save Current File`
- `File -> Save All Files`

Export entry: `File -> Export Current File...`

Export formats:

- Source: `.xml`
- Compiled: `.xml.compiled`

Preview export:

- `File -> Export Preview...`
- Particle editor uses time-range export.
- You can set start second, end second, export until end, FPS, and canvas scale.

## FAQ

### Why did editing a track not visibly change the file?

If a track has exactly one default node, it is normalized to an empty track during save to keep the resource file compact. Non-default values are written normally.

### Why are particles completely invisible?

Common causes:

- The emitter has no image ID.
- The image ID is missing.
- `SpawnRate`, lifetime, or alpha tracks make particles invisible.
- The selected image row, column, or frame count is wrong.

