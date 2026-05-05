# Layout, Language, Tabs, And Shortcuts

This document covers EffectViewer workspace operations, including the project explorer, editor side panels, document tabs, viewport controls, language switching, and shortcuts.

## Main Window Layout

Default layout:

- Top: main menu and current project name.
- Left: project explorer.
- Center: document tabs and current editor.
- Bottom: status bar.

## Project Explorer Layout

Menu: `View`

Available actions:

- `Project Explorer`: show or hide the explorer.
- `Dock Explorer Left`: move it to the left.
- `Dock Explorer Right`: move it to the right.
- `Reset Layout`: restore the default layout.

The project explorer width can be adjusted by dragging the splitter.

Shortcut:

- Show/hide project explorer: Windows/Linux `Ctrl+B`, macOS/iOS `Command+B`

## Editor Side Panel Layout

Image, Font, Reanim, Particle, and Trail editors provide a `Properties` side panel.

The ShowCase editor provides a `Script Panel` side panel.

Menu: each editor's top-right `Layout`.

Available actions:

- Show or hide the side panel.
- Dock left.
- Dock right.
- Reset editor layout.

## Document Tabs

Each opened resource appears as a document tab.

Tab contents:

- Resource type code, such as `IMG`, `FNT`, `REA`, `PAR`, `TRL`, or `LUA`.
- Title.
- Unsaved changes show a `*` after the title.
- Pinned tabs show `PIN`.

Mouse actions:

- Click a tab: switch document.
- Click `x`: close the tab.
- Drag a tab: reorder tabs.
- Right-click a tab: open the tab action menu.

Menu: `Tabs`

Available actions:

- Previous/next tab.
- Move tab left/right.
- Pin/unpin tab.
- Close current tab.
- Close other tabs.
- Close tabs to the left/right.
- Close saved tabs.
- Close all tabs.

Shortcuts:

| Action | Windows/Linux | macOS/iOS |
| --- | --- | --- |
| Close current tab | `Ctrl+W` | `Command+W` |
| Close all tabs | `Ctrl+Shift+W` | `Command+Shift+W` |
| Next tab | `Ctrl+Tab` | `Command+Tab` |
| Previous tab | `Ctrl+Shift+Tab` | `Command+Shift+Tab` |
| Move tab left | `Ctrl+Alt+Left` | `Command+Alt+Left` |
| Move tab right | `Ctrl+Alt+Right` | `Command+Alt+Right` |

## Unsaved Changes Prompt

When closing documents, switching projects, importing projects, deleting resources, or doing other operations that affect unsaved content, the app asks:

- `Save`: save the current changes and continue.
- `Discard`: discard changes and continue.
- `Cancel`: cancel the operation.

Pinned tabs do not prevent save or close-all operations, but tab menu commands are enabled or disabled depending on whether a document can be closed.

## Preview Viewport Controls

All preview viewports support:

| Action | Effect |
| --- | --- |
| Mouse wheel | Zoom. |
| Middle-drag | Pan. |
| Left-drag | Pan in normal editors; drag object or handles in Reanim free-transform mode. |
| Double-click | Reset zoom and pan. |

Theme modes:

- `View -> Light theme`
- `View -> Dark theme`

If neither option is checked, the app follows the system theme.

Background modes:

- `View -> Light Viewport Background`
- `View -> Dark Viewport Background`

If neither option is checked, the viewport uses the theme default background.

## Language Switching

Menu: `Language`

Options:

- `English`
- `Simplified Chinese`
- `Load Language File...`

Load a custom language file:

1. Choose `Language -> Load Language File...`.
2. Select a `.json` language file.
3. After loading succeeds, UI text is updated.

A custom language file should use the same structure as built-in language files, for example:

```json
{
  "languageCode": "custom",
  "languageName": "Custom",
  "strings": {
    "Common": {
      "Save": "Save"
    }
  }
}
```

Built-in language files can be used as references:

- `EffectViewer/Localization/en-US.json`
- `EffectViewer/Localization/zh-CN.json`

## Common Shortcuts

On macOS/iOS, shortcuts that use `Ctrl` are automatically mapped to `Command`.

| Action | Windows/Linux | macOS/iOS |
| --- | --- | --- |
| New project | `Ctrl+Shift+N` | `Command+Shift+N` |
| Open project | `Ctrl+O` | `Command+O` |
| Save current file | `Ctrl+S` | `Command+S` |
| Save project | `Ctrl+Alt+S` | `Command+Alt+S` |
| Save all files | `Ctrl+Shift+S` | `Command+Shift+S` |
| New resource | `Ctrl+N` | `Command+N` |
| Import resource file | `Ctrl+I` | `Command+I` |
| Import resource folder | `Ctrl+Shift+I` | `Command+Shift+I` |
| Delete resource | `Ctrl+Delete` | `Command+Delete` |
| Export preview | `Ctrl+E` | `Command+E` |
| Export current file | `Ctrl+Shift+E` | `Command+Shift+E` |
| Show/hide project explorer | `Ctrl+B` | `Command+B` |
| Close current tab | `Ctrl+W` | `Command+W` |
| Close all tabs | `Ctrl+Shift+W` | `Command+Shift+W` |
| Next tab | `Ctrl+Tab` | `Command+Tab` |
| Previous tab | `Ctrl+Shift+Tab` | `Command+Shift+Tab` |
| Move tab left | `Ctrl+Alt+Left` | `Command+Alt+Left` |
| Move tab right | `Ctrl+Alt+Right` | `Command+Alt+Right` |
| Undo | `Ctrl+Z` | `Command+Z` |
| Redo | `Ctrl+Y` or `Ctrl+Shift+Z` | `Command+Y` or `Command+Shift+Z` |
| ShowCase completion | `Ctrl+Space` | `Command+Space` |
