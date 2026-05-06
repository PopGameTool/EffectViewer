# EffectViewer

[English](README.md) | [简体中文](README.zh-CN.md)

EffectViewer is a cross-platform Avalonia tool for inspecting, editing, and staging effect assets. It can organize effect resources into projects, preview them with OpenGL/WebGL, and run Lua-powered ShowCase scripts that combine reanimations, particles, trails, and simple drawing commands in one scene.

## What It Can Do

- Create, rename, delete, import, export, and reopen EffectViewer projects.
- Import individual resources or whole resource folders.
- Preview image atlases and edit image IDs, rows, columns, and frame selection.
- Inspect and edit reanimation, particle, and trail definitions.
- Edit reanimation tracks, frames, transforms, tween metadata, and timeline selection.
- Preview particle emitters, float parameter tracks, particle fields, and trail curves.
- Write and run Lua ShowCase scripts for animated effect scenes.
- Use English or Simplified Chinese UI text, plus custom JSON language files.
- Run on Desktop and Browser targets, with Android and iOS project shells included.

## Quick Start

### Requirements

- .NET SDK that supports `net10.0` and `net10.0-browser`.
- Desktop OpenGL support for the desktop app.
- Optional platform SDKs when building mobile targets:
  - Android SDK for `EffectViewer.Android`
  - Xcode/iOS simulator tooling for `EffectViewer.iOS`

### Run The Desktop App

```bash
dotnet run --project EffectViewer.Desktop/EffectViewer.Desktop.csproj
```

### Build Desktop And Browser

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
```

The full solution includes Android and iOS projects:

```bash
dotnet build EffectViewer.slnx
```

If Android SDK is not configured, the full solution build can fail on `EffectViewer.Android`. Build Desktop or Browser directly when you only need the main editor.

## Try The Sample Project

A ready-to-import sample is included at:

```text
Samples/QuickStartShowcase.zip
```

To try it in the app:

1. Run the desktop app.
2. Choose `Project` -> `Import Project Zip`.
3. Select `Samples/QuickStartShowcase.zip`.
4. Open `quickstart_showcase` from the project tree.
5. Click `Run` in the ShowCase editor.

To inspect the manifest and Lua script directly, unzip `Samples/QuickStartShowcase.zip` first.

## Project Layout

```text
EffectViewer/           Shared Avalonia UI, view models, rendering, runtime, and TodLib code
EffectViewer.Desktop/   Desktop app host
EffectViewer.Browser/   Browser/WebAssembly app host
EffectViewer.Android/   Android app host
EffectViewer.iOS/       iOS app host
Samples/                Importable sample projects
docs/lua_api.en-US.md   Lua ShowCase scripting reference
```

## Supported Resource Types

EffectViewer projects use a `project.effectproj.json` manifest and can reference:

- Images: `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp`, `.tga`
- Reanimations: `.reanim`, `.reanim.compiled`
- Particles: `.xml`, `.xml.compiled`
- Trails: `.trail`, `.trail.compiled`
- ShowCases: `.lua`

## ShowCase Scripts

ShowCases are Lua scripts that run inside the current project. A script can create project resources:

```lua
local body = scene.reanim("sample_reanim", 400, 300)
local fire = scene.particle_system("fire_burst", 420, 280)
local slash = scene.trail("sword_slash", 0, 0)
```

It can also draw directly:

```lua
local context = {}

function context:draw(g, elapsed, frame)
    g:reset()
    g:set_color(255, 128, 64, 220)
    g:fill_rect(120, 120, 180, 64)
end

scene.regist(context)
```

See [docs/lua_api.en-US.md](docs/lua_api.en-US.md) for the full scripting API.

## Development Notes

- Package versions are centralized in [Directory.Packages.props](Directory.Packages.props).
- The shared UI and runtime target `net10.0`; the browser host targets `net10.0-browser`.
- Desktop and Browser builds are good smoke tests for most shared code.
- Run the [QuickStart smoke test](docs/quickstart_smoke_test.en-US.md) before release candidates.
- Mobile builds require their native SDKs even when shared code is unchanged.
