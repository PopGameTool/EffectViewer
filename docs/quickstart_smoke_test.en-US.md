# QuickStart Smoke Test

[简体中文](quickstart_smoke_test.zh-CN.md) | English

Run this smoke test before cutting a release candidate and after changes that touch project import/export, Lua ShowCase playback, preview rendering, or packaging.

## Automated Check

```bash
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj --filter QuickStartShowcaseSmokeTests
```

This imports `Samples/QuickStartShowcase.zip`, runs the bundled Lua ShowCase, captures a preview frame, and verifies that the preview can be exported as PNG.

## Desktop Manual Check

1. Start the desktop app:

   ```bash
   dotnet run --project EffectViewer.Desktop/EffectViewer.Desktop.csproj
   ```

2. Choose `Project -> Import Project Zip...`.
3. Select `Samples/QuickStartShowcase.zip`.
4. Confirm that the imported project is named `Quick Start Showcase`.
5. Open the `quickstart_showcase` resource from the project explorer.
6. Click `Run` in the ShowCase editor.
7. Confirm that the preview shows a dark panel with animated colored bars and blocks.
8. Confirm that the log contains `Quick Start Showcase loaded`.
9. Export the preview once as PNG and once as GIF or WebP.
10. Save the project, close the app, restart it, and reopen the imported project from the project list.

## Browser Manual Check

1. Build or run the browser target:

   ```bash
   dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
   ```

2. Open the browser target through the local development flow used for the current platform.
3. Import `Samples/QuickStartShowcase.zip`.
4. Open and run `quickstart_showcase`.
5. Confirm that the preview renders, the log line appears, and file picker/export actions do not fail.
6. Zoom and pan the preview, resize the window, and switch light/dark backgrounds. Check orientation, clipping, and alpha blending.
7. Close and reopen resource tabs, then import or switch projects. Check that previews return and other controls and dialogs still render correctly.
8. Check the browser console for WebGL errors. Preview rendering should use Avalonia's canvas without creating a separate preview canvas.

## Pass Criteria

- The sample project imports without an error dialog.
- The project explorer contains exactly one ShowCase resource named `quickstart_showcase`.
- Running the script succeeds and keeps the editor responsive.
- Preview export writes non-empty output for PNG and at least one animated format.
- Saving and reopening the imported project preserves the manifest and script.
