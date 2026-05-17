# Release Scripts

Run these commands from the repository root. `VERSION` defaults to `Directory.Build.props` when it is not set.

```bash
# macOS app bundle packaged as a DMG. Must run on macOS.
VERSION=0.1.0 scripts/release/publish-macos.sh

# iOS device IPA. Must run on macOS with Xcode and signing assets available.
VERSION=0.1.0 IOS_CODESIGN_KEY="Apple Distribution: ..." IOS_CODESIGN_PROVISION="..." scripts/release/publish-ios.sh

# Android arm64 package. Works on Windows/macOS/Linux when Android SDK and NDK are installed.
VERSION=0.1.0 ANDROID_NDK_VERSION=27.2.12479018 scripts/release/publish-android.sh

# Android package signed with a JKS keystore.
VERSION=0.1.0 \
ANDROID_SIGNING_KEYSTORE=/path/to/release.jks \
ANDROID_SIGNING_KEY_ALIAS=effectviewer \
ANDROID_SIGNING_STORE_PASS=store-password \
ANDROID_SIGNING_KEY_PASS=key-password \
scripts/release/publish-android.sh

# Browser static WebAssembly package.
VERSION=0.1.0 scripts/release/publish-browser.sh

# Linux native package for the current host architecture.
VERSION=0.1.0 scripts/release/publish-linux.sh

# Linux native package for an explicit architecture.
VERSION=0.1.0 scripts/release/publish-linux.sh linux-x64

# Linux x64 and arm64 packages via Docker. Useful from macOS.
VERSION=0.1.0 scripts/release/publish-linux-docker.sh
```

```powershell
# Windows x64 and arm64 zip packages. Must run on Windows.
# Uses EffectViewer.Windows.
./scripts/release/publish-windows.ps1 -Version 0.1.0

# Android arm64 package on Windows. Add -RestoreWorkload on first setup if needed.
./scripts/release/publish-android.ps1 -Version 0.1.0

# Android package signed with a JKS keystore on Windows.
./scripts/release/publish-android.ps1 -Version 0.1.0 `
  -AndroidSigningKeyStore C:\keys\release.jks `
  -AndroidSigningKeyAlias effectviewer `
  -AndroidSigningStorePass store-password `
  -AndroidSigningKeyPass key-password

# Browser static WebAssembly package. Add -RestoreWorkload on first setup if needed.
./scripts/release/publish-browser.ps1 -Version 0.1.0

# Linux x64 and arm64 packages via Docker. Useful from Windows PowerShell.
./scripts/release/publish-linux-docker.ps1 -Version 0.1.0
```

Outputs are written to `artifacts/dist`.
