# Resource Management And Import/Export

Resource management is centered around the project explorer. You can create some resource types, import external files, search resources, reopen recently used resources, delete resources, and export the current resource to an external file.

## Project Explorer

The project explorer is docked on the left by default and groups resources by type:

- `Images`
- `Fonts`
- `Reanim`
- `Particles`
- `Trails`
- `Showcases`

Common actions:

- Click `>` / `v` next to a group to expand or collapse it.
- Click a resource to open its editor.
- Type in the search box to filter resources.
- Click `x` next to the search box to clear the search.
- Right-click a resource and choose `Delete Resource...` to delete it.

Menu entries:

- Show/hide: `View -> Project Explorer`
- Dock left: `View -> Dock Explorer Left`
- Dock right: `View -> Dock Explorer Right`

Shortcut:

- Windows/Linux: `Ctrl+B`
- macOS/iOS: `Command+B`

## Recently Opened

Menu: `Resource -> Recently Opened`

The recently opened list records resources recently opened in the current project. It keeps up to 8 entries. Click an item to reopen that resource.

## Create A New Resource

Menu: `Resource -> New Resource...`

Shortcuts:

- Windows/Linux: `Ctrl+N`
- macOS/iOS: `Command+N`

Creatable types:

- Reanim
- Particle
- Trail
- ShowCase

Not creatable through this dialog:

- Image: import an image file instead.
- Font: import a `.ttf` file or import a resource folder instead.

Steps:

1. Make sure a writable project is loaded.
2. Choose `Resource -> New Resource...`.
3. Select the resource type.
4. Enter the resource ID.
5. Click `Create`.

Default file locations:

| Type | Default Directory | Default Extension |
| --- | --- | --- |
| Reanim | `assets/reanims` | `.reanim` |
| Particle | `assets/particles` | `.xml` |
| Trail | `assets/trails` | `.trail` |
| ShowCase | `scripts` | `.lua` |

If the ID or file name conflicts, the app appends a number automatically.

## Import A Single Resource File

Menu: `Resource -> Import Resource File...`

Shortcuts:

- Windows/Linux: `Ctrl+I`
- macOS/iOS: `Command+I`

Single-file import copies a resource into the current project and adds it to the project manifest.

Supported single-file types:

| Type | Extensions |
| --- | --- |
| Image | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp`, `.tga` |
| Font | `.ttf` |
| Reanim | `.reanim`, `.reanim.compiled` |
| Particle | `.xml`, `.xml.compiled` |
| Trail | `.trail`, `.trail.compiled` |
| ShowCase | `.lua` |

Import rules:

- Image IDs start with `IMAGE_` and use an uppercase safe name.
- Other resource IDs default to a safe version of the file name.
- Files are copied into the matching project resource directory.
- If a resource ID or path conflicts, the app appends a number automatically.
- The imported resource opens in its editor after import.

## Import Resource Folder

Menu: `Resource -> Import Resource Folder...`

Shortcuts:

- Windows/Linux: `Ctrl+Shift+I`
- macOS/iOS: `Command+Shift+I`

Resource-folder import appends an external resource directory to the current project and adds recognized resources to the project manifest.

Before import:

- A writable current project must already be open.
- If an imported resource ID already exists, choose `Skip`, `Overwrite`, or `Keep Both`.
- Enable "Apply this choice to remaining conflicts" to reuse the selected action for later duplicate IDs in the same import.

Recommended source structure:

```text
properties/resources.xml
reanim/*.reanim
particles/*.xml
particles/*.trail
```

Or compiled resource structure:

```text
properties/resources.xml
compiled/reanim/*.reanim.compiled
compiled/particles/*.xml.compiled
compiled/particles/*.trail.compiled
compiled/trails/*.trail.compiled
```

Target project directories:

```text
assets/images
assets/fonts
assets/reanims
assets/particles
assets/trails
```

Resource-folder import rules:

- If `properties/resources.xml` exists, `Image` and `Font` resources are read from it.
- `SetDefaults` attributes `path` and `idprefix` affect following `Image` / `Font` entries.
- Folder import recognizes image extensions `.png`, `.jpg`, `.jpeg`, and `.gif` by convention.
- Image `rows` and `cols` are read from `resources.xml`.
- Font resources resolve to `.txt` image font descriptors or `.ttf` TrueType fonts.
- Images not listed in `resources.xml` are also imported by file-name convention.
- Compiled reanim, particle, and trail files are converted to source format in the current project.
- When `Keep Both` is selected, duplicate IDs get a numeric suffix; imported reanim, particle, and trail files try to remap references to imported images.

Alpha companion image rules:

- The companion alpha image for `name.png` can be named `_name.png` or `name_.png`.
- If a companion image is found, it is stored as the image asset's `alphaPath`.
- If only `_name.png` or `name_.png` exists, it is treated as an `alphaOnly` image.

## Import Resource Pak

Menu: `Resource -> Import Resource Pak...`

Use this to read a `.pak` resource archive as a resource folder and append it to the current project.

Pak import behaves like resource-folder import:

- It imports images, fonts, reanims, particles, and trails according to the archive contents and resource manifest.
- Duplicate IDs use the same Skip, Overwrite, or Keep Both choices.

## Drag-And-Drop Import

You can drag supported resource files onto the project explorer.

Supported:

- Single resource files.
- `.pak` files.

Not supported:

- Resource folders.
- Project Zip files.
- Unsupported file types.

Dropping a single resource file or `.pak` file requires a writable current project. Dropping a `.pak` file runs the Pak import flow and appends to the current project.

## Delete Resource

Entry points:

- Select a resource in the project explorer and press `Delete`
- `Resource -> Delete Resource...`
- Right-click a resource and choose `Delete Resource...`
- Windows/Linux: `Ctrl+Delete`
- macOS/iOS: `Command+Delete`

Delete behavior:

- Removes the resource from the project manifest.
- Deletes the matching file from the project directory.
- Deleting an image also deletes the companion file referenced by `alphaPath`.
- Closes matching open editors.

This operation cannot be undone. If the resource editor has unsaved changes, the app asks for confirmation first.

## Save Current File And Save All Files

Menu entries:

- `File -> Save Current File`
- `File -> Save All Files`

Shortcuts:

- Save current file: Windows/Linux `Ctrl+S`, macOS/iOS `Command+S`
- Save all files: Windows/Linux `Ctrl+Shift+S`, macOS/iOS `Command+Shift+S`

Save behavior:

- Image / Font: saved to the project manifest.
- Reanim: encoded and written back to the `.reanim` or matching animation file, and the project manifest is saved.
- Particle: encoded and written back to the `.xml` or matching particle file.
- Trail: encoded and written back to the `.trail` or matching trail file.
- ShowCase: written back to the `.lua` script file.

## Export Current File

Menu: `File -> Export Current File...`

Shortcuts:

- Windows/Linux: `Ctrl+Shift+E`
- macOS/iOS: `Command+Shift+E`

Use this to export the project file behind the current editor to a location outside the project.

Export rules:

- Image / Font / ShowCase export the original project file by default.
- Reanim can export source `.reanim` or compiled `.reanim.compiled`.
- Particle can export source `.xml` or compiled `.xml.compiled`.
- Trail can export source `.trail` or compiled `.trail.compiled`.
- If the current editor has unsaved changes, it is saved before export.

## FAQ

### What should I do when resource-folder import finds duplicate IDs?

Choose `Skip` to keep the current project resource, `Overwrite` to replace it with the imported content, or `Keep Both` to generate a new ID. Enable "Apply this choice to remaining conflicts" to reuse the same action for later conflicts in this import.

### Why are image and font missing from the New Resource dialog?

Images require actual image files, and fonts require font files or font descriptors. They are currently added through import.

### Why did folder import not pick up `.webp` or `.tga` images?

Single-file import supports `.webp` and `.tga`. Folder import currently scans `.png`, `.jpg`, `.jpeg`, and `.gif` images by convention.
