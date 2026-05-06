# Release Checklist

[Simplified Chinese](release_checklist.zh-CN.md) | English

Use this checklist when cutting an EffectViewer release candidate or publishing a tagged release.

## Version Metadata

Before tagging a release, keep these values in sync:

- `Directory.Build.props`: `VersionPrefix`
- `EffectViewer.Android/EffectViewer.Android.csproj`: `ApplicationDisplayVersion`
- `EffectViewer.macOS/EffectViewer.macOS.csproj`: `ApplicationDisplayVersion`
- `CHANGELOG.md`: a dated `## x.y.z - YYYY-MM-DD` section

The release workflow validates this metadata before it builds artifacts.

## Local Release Candidate Checks

Run these checks from the repository root:

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj --configuration Release
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj --configuration Release
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj --configuration Release
```

Then run the QuickStart smoke checks:

```bash
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj --filter QuickStartShowcaseSmokeTests
```

Complete the manual Desktop and Browser checks in [QuickStart Smoke Test](quickstart_smoke_test.en-US.md) before publishing a public release.

## GitHub Release Workflow

The release workflow is defined in `.github/workflows/release.yml`.

It runs automatically when a tag matching `v*.*.*` is pushed:

```bash
git tag v0.1.0
git push origin v0.1.0
```

It can also be run manually from GitHub Actions. Manual runs can build artifacts without creating a GitHub release, or create a draft release when `create_release` is enabled.

## Release Artifacts

The workflow publishes:

- Windows `win-x64` and `win-arm64` zip packages from `EffectViewer.Desktop`.
- Linux `linux-x64` and `linux-arm64` tarballs from `EffectViewer.Desktop`.
- Browser static WebAssembly zip package from `EffectViewer.Browser`.
- macOS `osx-arm64` DMG from `EffectViewer.macOS`.
- iOS `ios-arm64` IPA from `EffectViewer.iOS` on macOS.
- Android `android-arm64` packages on Windows, macOS, and Linux.

The scripts used by the workflow live in `scripts/release`. iOS signing and provisioning must already be available on the macOS runner, or provided through the `IOS_CODESIGN_KEY`, `IOS_CODESIGN_PROVISION`, and `IOS_CODESIGN_KEYCHAIN` environment variables.

## Final Review

Before marking the draft GitHub release as ready:

- Confirm all workflow jobs passed.
- Download and open each release artifact on its target platform when possible.
- Verify the QuickStart sample imports, runs, and exports a preview.
- Confirm release notes match the shipped changes.
- Confirm the release tag, artifact names, and app display versions use the same version.
