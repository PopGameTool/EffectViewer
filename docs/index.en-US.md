# EffectViewer Feature Guide

English | [Simplified Chinese](index.zh-CN.md)

This documentation is for users who manage, inspect, edit, preview, and stage effect resources with EffectViewer. For Lua scripting APIs, see the existing [Lua API reference](lua_api.en-US.md).

## Documentation Index

- [Project Management](project_management.en-US.md): create, open, rename, delete, import, and export EffectViewer projects.
- [Resource Management And Import/Export](resource_management.en-US.md): project explorer, search, recently opened resources, single-file import, resource-folder import, Pak import, drag-and-drop import, resource deletion, and current-file export.
- [Image Editor](image_editor.en-US.md): inspect image atlases and edit image IDs, rows, columns, and selected frame.
- [Font Editor](font_editor.en-US.md): inspect image fonts and TrueType fonts, edit font IDs, font size, and border size.
- [Reanim Editor](reanim_editor.en-US.md): tracks, frames, timeline, transforms, visual dragging, and tween metadata.
- [Particle Editor](particle_editor.en-US.md): particle emitters, float parameter tracks, particle fields, system fields, and live preview.
- [Trail Editor](trail_editor.en-US.md): trail image, point settings, looping, width/alpha curves, and duration.
- [ShowCase Script Editor](showcase_editor.en-US.md): Lua ShowCase scripts, live preview, completion, and log navigation.
- [Preview Export](preview_export.en-US.md): export the current preview as PNG, PNG sequence Zip, GIF, or WebP.
- [Layout, Language, Tabs, And Shortcuts](layout_language_shortcuts.en-US.md): workspace layout, tab management, viewport controls, UI language, and common shortcuts.
- [Runtime Targets And Development Startup](runtime_targets.en-US.md): Desktop, Browser, Android, and iOS startup and build targets.

## Supported Resource Types

EffectViewer projects use `project.effectproj.json` as the project manifest. A manifest can reference these resources:

| Type | Purpose | Common Extensions |
| --- | --- | --- |
| Image | Images, atlases, particle textures, animation frame textures | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp`, `.tga` |
| Font | Image font descriptors or TrueType fonts | `.txt`, `.ttf` |
| Reanim | PvZ/Tod-style reanimation definitions | `.reanim`, `.reanim.compiled` |
| Particle | Particle system definitions | `.xml`, `.xml.compiled` |
| Trail | Trail definitions | `.trail`, `.trail.compiled` |
| ShowCase | Lua presentation scripts | `.lua` |

## Basic Workflow

1. Start the desktop app.
2. Create an empty project with `Project -> New Project...`, or import existing resources with `Project -> Import Project Zip...` / `Resource -> Import Resource Folder...`.
3. Search or expand resource groups in the project explorer.
4. Open image, font, reanim, particle, trail, or ShowCase resources for editing.
5. Save changes with `File -> Save Current File`, `File -> Save All Files`, or `Project -> Save Project`.
6. Export resource files with `File -> Export Current File...`, or export rendered previews with `File -> Export Preview...`.
7. Package the whole project with `Project -> Export Project Zip...`.

## Key Concepts

- **Internal project**: a project loaded into the app-private project directory through new project, open project, project-Zip import, resource-folder import, or Pak import. Rename, delete, save, and project-Zip export operate on internal projects.
- **Resource ID**: the name used by scripts and resource references. Imported images usually receive an `IMAGE_...` ID; other resources use a safe version of the file name or the ID entered when creating the resource.
- **Project path**: a path stored in the manifest relative to the project root, such as `assets/images/IMAGE_FIRE.png`, `assets/reanims/zombie.reanim`, or `scripts/demo.lua`.
- **Unsaved marker**: a `*` after a document tab title means that editor has unsaved changes.
- **Preview viewport**: the shared rendering surface used by resource editors. Use the mouse wheel to zoom, middle-drag to pan, and double-click to reset zoom and pan.

