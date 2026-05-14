# Runtime Targets And Development Startup

EffectViewer is a cross-platform application built with Avalonia. The repository contains shared UI/runtime code plus Desktop, Windows, Browser, Android, and iOS host projects.

## Project Layout

```text
EffectViewer/           Shared Avalonia UI, view models, rendering and runtime code
EffectViewer.Desktop/   Desktop app host
EffectViewer.Windows/   Windows publishing host
EffectViewer.Browser/   Browser/WebAssembly app host
EffectViewer.Android/   Android app host
EffectViewer.iOS/       iOS app host
EffectViewer.Tests/     Unit tests
Samples/                Importable sample projects
docs/                   User documentation and Lua API references
```

## Requirements

Base requirements:

- .NET SDK with `net10.0` support.
- Browser builds require `net10.0-browser` support.
- Windows publishing builds require `net10.0-windows` support.

Desktop requirements:

- System OpenGL support.

Mobile requirements:

- Android: Android SDK.
- iOS: Xcode, iOS simulator, or related signing/deployment tools.

## Run Desktop

From the repository root:

```bash
dotnet run --project EffectViewer.Desktop/EffectViewer.Desktop.csproj
```

The desktop target is the best choice for day-to-day editing and debugging, and provides the most complete experience.

## Build Desktop

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj
```

If you only changed shared UI, view models, project management, rendering, or EffectRuntime code, a Desktop build is usually enough for a basic smoke test.

## Build Windows Publishing Host

```bash
dotnet build EffectViewer.Windows/EffectViewer.Windows.csproj
```

The Windows publishing host targets `net10.0-windows` and references `YY-Thunks`; the Windows release script packages this project into zip artifacts.

## Build Browser

```bash
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
```

The Browser target uses a WebAssembly/WebGL host and is useful for validating browser rendering and file-picker behavior.

## Build The Full Solution

```bash
dotnet build EffectViewer.slnx
```

Notes:

- The full solution includes Android and iOS projects.
- If Android SDK or iOS tooling is not configured, the full build may fail on mobile projects.
- If you only need the main editor, build Desktop, Windows, or Browser directly.

## Android And iOS Projects

The repository includes mobile host projects:

- `EffectViewer.Android/EffectViewer.Android.csproj`
- `EffectViewer.iOS/EffectViewer.iOS.csproj`

These projects reuse the shared UI and runtime code, while build, signing, simulator, and deployment behavior are handled by the corresponding platform toolchains.

## Sample Project

The repository includes a sample project:

```text
Samples/QuickStartShowcase.zip
```

Try it:

1. Run the desktop app.
2. Choose `Project -> Import Project Zip...`.
3. Select `Samples/QuickStartShowcase.zip`.
4. Open `quickstart_showcase` from the project explorer.
5. Click `Run` in the ShowCase editor.

## Recommended Verification

For documentation-only changes:

```bash
git diff -- docs
```

For code changes:

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj
dotnet build EffectViewer.Windows/EffectViewer.Windows.csproj
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj
```

For Browser host changes:

```bash
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
```

For mobile changes, build the corresponding mobile project after the required platform SDK is configured.
