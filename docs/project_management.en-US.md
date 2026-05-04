# Project Management

A project is the resource container used by EffectViewer. A project directory contains at least `project.effectproj.json`, and usually contains resource folders such as `assets/` and `scripts/`.

## Create A Project

Menu: `Project -> New Project...`

Shortcuts:

- Windows/Linux: `Ctrl+Shift+N`
- macOS/iOS: `Command+Shift+N`

Steps:

1. Choose `Project -> New Project...`.
2. Enter the project name in the `Name` field.
3. Click `Create`.

Result:

- The app creates a unique project folder in its private project directory.
- The project manifest is written to `project.effectproj.json`.
- The workspace switches to the new project.

Notes:

- If the project name is empty, the default untitled project name is used.
- If there are unsaved documents, the app asks whether to save, discard, or cancel before continuing.

## Open A Project

Menu: `Project -> Open Project...`

Shortcuts:

- Windows/Linux: `Ctrl+O`
- macOS/iOS: `Command+O`

The open-project dialog lists EffectViewer projects stored in the app-private project directory. Projects are sorted by recent modification time.

Steps:

1. Choose `Project -> Open Project...`.
2. Select a project from the list.
3. Click `Open`.

Each project row also provides:

- `Rename`: change the project display name and synchronize the safe project folder name.
- `Delete`: delete the internal project folder.

## Rename A Project

Entry point: the `Rename` button in `Project -> Open Project...`.

Steps:

1. Open the project list.
2. Click `Rename` next to the target project.
3. Enter the new name.
4. Click `Rename`.

Result:

- The `name` field in `project.effectproj.json` is updated.
- The project folder is renamed using a safe version of the new name; a number is appended automatically if needed.

Restrictions:

- Only projects inside the app-private project directory can be managed.
- Projects outside that directory cannot be renamed by this dialog.

## Delete A Project

Entry point: the `Delete` button in `Project -> Open Project...`.

Deleting a project removes the whole project folder and cannot be undone. Export a Zip or make a backup before confirming.

Steps:

1. Open the project list.
2. Click `Delete` next to the target project.
3. Read the confirmation dialog.
4. Click `Delete`.

Restrictions:

- Only projects inside the app-private project directory can be deleted.
- Deleting removes the manifest and all resource files in the project directory.

## Save A Project

Menu: `Project -> Save Project`

Shortcuts:

- Windows/Linux: `Ctrl+Alt+S`
- macOS/iOS: `Command+Alt+S`

Saving the project writes the project manifest and accepts changes that are stored in the manifest, such as image/font IDs and resource IDs.

Before saving, the app validates:

- Image IDs cannot be empty.
- Font IDs cannot be empty.
- Reanim, particle, and trail resource IDs cannot be empty.

## Import Project Zip

Menu: `Project -> Import Project Zip...`

Use this to import a complete project exported by EffectViewer, or any project Zip that contains `project.effectproj.json`.

Steps:

1. Choose `Project -> Import Project Zip...`.
2. Select a `.zip` file.
3. Wait for the import progress to complete.

Import rules:

- The Zip must contain `project.effectproj.json`.
- The manifest can be at the Zip root or inside a subdirectory.
- If the manifest is inside a subdirectory, only that project subtree is extracted.
- The project is copied into the app-private project directory with a non-conflicting folder name.

Safety restrictions:

- Zip entries cannot use absolute paths.
- Zip entries cannot contain `..` path traversal.
- If import fails, the incomplete project directory is cleaned up.

## Export Project Zip

Menu: `Project -> Export Project Zip...`

Use this to package the current internal project for backup, migration, or sharing.

Steps:

1. Open the project to export.
2. Choose `Project -> Export Project Zip...`.
3. Choose the output location and file name.

Before export:

- The project manifest is saved.
- All files under the project directory are compressed.
- If open editors have unsaved changes, the app tries to save those documents first.

Restrictions:

- Only projects stored in the app-private project directory can be exported.
- The current project must have a valid project root path.

## Project Manifest Structure

`project.effectproj.json` mainly contains:

```json
{
  "version": 2,
  "name": "My Effect Project",
  "images": [],
  "fonts": [],
  "reanims": [],
  "particles": [],
  "trails": [],
  "showcases": []
}
```

Most resource entries contain:

- `id`: the resource ID.
- `path`: the project-relative file path.

Images also contain:

- `alphaPath`: optional companion alpha image.
- `alphaOnly`: whether the image is alpha-only.
- `rows`, `cols`: atlas row and column counts.

TrueType fonts also contain:

- `trueType`: whether the asset is handled as a TTF font.
- `fontSize`: preview font size.
- `borderSize`: preview border size.

