# ShowCase Script Editor

The ShowCase editor is used to write and run Lua presentation scripts. A script can combine images, reanimations, particles, trails, and drawing commands into one preview scene.

For the full scripting API, see the [Lua API reference](lua_api.en-US.md).

## Open The Editor

There are two kinds of ShowCase editor:

- Project `.lua` resources: expand `Showcases` in the project explorer and click a script.
- Built-in user script page: a temporary ShowCase editing experience when no concrete `.lua` resource is opened.

Project scripts can be saved and exported. The built-in user script page does not map to a project file and is mainly useful for temporary experiments.

## Main Areas

- Central preview viewport: displays the scene after the script runs.
- Script panel: edits Lua code.
- `Run` button: executes the current script.
- Log list: shows `scene.log` output, run status, and errors.

## Write Scripts

Script editor features:

- Line numbers.
- Lua syntax coloring.
- Search panel.
- Tabs converted to spaces.
- Current-line highlighting.
- Undo and redo.

Completion:

- Windows/Linux: `Ctrl+Space`
- macOS/iOS: `Command+Space`
- Typing `.` or `:` attempts to show member completion.

Completion can infer common receiver types, such as:

- `scene`
- `global_attachment`
- `g` or `graphics`
- Objects returned by `scene.reanim(...)`
- Objects returned by `scene.particle_system(...)`
- Objects returned by `scene.trail(...)`

## Run A Script

Steps:

1. Enter Lua code in the script panel.
2. Click `Run`.
3. Check the preview viewport and log list.

Runtime behavior:

- Clears previous logs and scene objects.
- On success, binds the preview viewport to the frame provider created by the script.
- On failure, stops the preview and selects the first error log with a source location when available.

If a log entry has line/column information, selecting it will:

- Jump to the corresponding line and column.
- Select the error line.
- Focus the script editor.

## Minimal Script

```lua
scene.clear()
scene.log("showcase initialized")
```

## Create And Draw Resources

```lua
scene.clear()

local body = scene.reanim("sample_reanim", 400, 300)
local fire = scene.particle_system("fire_burst", 420, 280)
local slash = scene.trail("sword_slash", 0, 0)

local context = {}
local t = 0

function context:update(dt)
    t = t + dt
    body:set_position(400 + math.sin(t * 2) * 40, 300)
    fire:set_scale(0.8 + math.sin(t * 3) * 0.2)
    slash:clear_points()
    slash:add_point(330, 320)
    slash:add_point(470, 280 + math.sin(t * 4) * 40)
end

function context:draw(g)
    body:draw(g)
    fire:draw(g)
    slash:draw(g)
end

scene.regist(context)
scene.log("showcase initialized")
```

The resource IDs in this example must exist in the current project.

## Draw Directly

```lua
scene.clear()

local context = {}

function context:draw(g)
    g:reset()
    g:set_color(255, 128, 64, 220)
    g:fill_rect(120, 120, 180, 64)
end

scene.regist(context)
```

## Save And Export

Save entries:

- `File -> Save Current File`
- `File -> Save All Files`

Saving writes the script text back to the project `.lua` file.

Export entry: `File -> Export Current File...`

Export copies the current `.lua` file to the selected location.

## Preview Export

Menu: `File -> Export Preview...`

ShowCase preview export:

1. Creates an isolated runtime world.
2. Runs the current script again.
3. Captures scene frames.

Supported formats:

- PNG
- PNG sequence Zip
- GIF
- WebP

If the script fails to run, export cannot produce a valid animation. Click `Run` in the editor first and fix log errors before exporting.

## Undo And Redo

The script editor uses its own text-editor undo stack.

Shortcuts:

- Undo: Windows/Linux `Ctrl+Z`, macOS/iOS `Command+Z`
- Redo: Windows/Linux `Ctrl+Y` or `Ctrl+Shift+Z`, macOS/iOS `Command+Y` or `Command+Shift+Z`

## Layout

Menu: editor `Layout`.

Available actions:

- Show or hide the script panel.
- Dock the script panel on the left or right.
- Reset editor layout.

## FAQ

### Why is there no image after running the script?

Common causes:

- The script did not register a `context`.
- `draw(g)` does not draw anything.
- A resource ID does not exist, causing object creation to fail.
- Objects are positioned outside the viewport.

Check the log list first, then verify resource IDs and draw logic.

### Why does preview export differ from the current view?

Export runs the script again and captures frames from the initial state. If the script depends on randomness or runtime state, exported output may differ from the already-playing editor preview.

