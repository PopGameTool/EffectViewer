# Font Editor

The font editor inspects and edits Font resources. EffectViewer supports image font descriptors and TrueType fonts.

## Open The Editor

1. Open a project that contains font resources.
2. Expand `Fonts` in the project explorer.
3. Click a font resource.

The editor header shows:

- The current font resource ID.
- The font path inside the project.

## Supported Font Sources

| Type | Common Path/Extension | Description |
| --- | --- | --- |
| Image font | `.txt` font descriptor | Usually imported from `resources.xml` or a resource folder. Describes glyph layers and texture maps. |
| TrueType font | `.ttf` | Can be imported as a single file. Preview texture is generated dynamically. |

## Preview Area

Image fonts:

- Display the resolved font texture map.
- If multiple texture maps are available, use the top-right dropdown to switch between them.
- If no texture map can be resolved, the editor shows a no-texture message.

TrueType fonts:

- Display automatically generated preview text.
- The preview includes letters and numbers; Chinese sample text is included when supported.

Viewport controls:

- Mouse wheel to zoom.
- Middle-drag to pan.
- Double-click to reset zoom and pan.

## Properties Panel

Menu: editor `Layout -> Properties`.

Common fields:

| Field | Description |
| --- | --- |
| `ID` | Font resource ID. Lua scripts or reanim text tracks use this ID. |
| `Default Point Size` | The default point size declared by the font file. |
| `Point Size` | Current font point size or descriptor point size. |
| `Ascent` | Font ascender metric. |
| `Ascent Padding` | Ascent padding. |
| `Height` | Font height metric. |
| `Line Spacing Offset` | Line spacing offset. |
| `Layers` | Number of image font layers. |
| `Glyphs` | Number of parsed glyphs. |
| `Textures` | Number of previewable font textures. |

Editable TrueType fields:

| Field | Range | Description |
| --- | --- | --- |
| `Font Size` | 1 to 256 | Font size used to generate preview textures. |
| `Border Size` | 0 to 64 | Border width used to generate preview textures. |

Extra image-font information:

- `Texture Map`: shows the selected texture's layer, image ID, texture size, and image name.
- `Layers`: lists each layer's image name, resolved image, point size, ascent, height, spacing, and glyph count.

## Change The Font ID

Steps:

1. Enter the new ID in the properties panel `ID` field.
2. The change is applied when the text box loses focus.
3. Save the current file or project.

Rules:

- Leading and trailing whitespace is trimmed.
- The ID cannot be empty.
- If it conflicts with another font in the same project, the editor tries to append `_2`, `_3`, and so on.

Impact:

- The project asset index is updated.
- Existing script references or animation text-track references are not automatically renamed.

## Adjust TrueType Preview

TrueType fonts can adjust:

- `Font Size`
- `Border Size`

The preview refreshes after changes, and the values are written to the project manifest when saved.

Notes:

- These settings affect EffectViewer preview and runtime texture generation.
- They do not modify the `.ttf` file itself.

## Undo And Redo

Undo/redo supports:

- Font ID changes.
- TrueType font size changes.
- TrueType border size changes.

Shortcuts:

- Undo: Windows/Linux `Ctrl+Z`, macOS/iOS `Command+Z`
- Redo: Windows/Linux `Ctrl+Y` or `Ctrl+Shift+Z`, macOS/iOS `Command+Y` or `Command+Shift+Z`

## Save And Export

Save entries:

- `File -> Save Current File`
- `Project -> Save Project`
- `File -> Save All Files`

Editable font settings are saved in the project manifest.

Export entry: `File -> Export Current File...`

Export copies the current font file to the selected location.

## FAQ

### Why does the editor say it cannot read the font descriptor?

Possible causes:

- The font file path does not exist.
- The image font descriptor format is not supported by the parser.
- Textures referenced by the image font are missing.

Check the load error in the properties panel and verify that the `path` in the project manifest is correct.

### Why does an image font have no preview texture?

Image fonts need a descriptor with layers, and those layers must reference image resources that exist in the project. Check whether the `Layers` and `Texture Map` sections are empty.

### Does changing a TrueType font make the file larger?

No. Font size and border size are stored in the project manifest and do not modify the `.ttf` file.

