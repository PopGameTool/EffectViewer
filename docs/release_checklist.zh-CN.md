# 发布检查清单

[English](release_checklist.en-US.md) | 简体中文

准备 EffectViewer 发布候选版本或正式打标签发布时，使用这份清单。

## 版本元数据

打发布标签前，保持这些值一致：

- `Directory.Build.props`：`VersionPrefix`
- `EffectViewer.Android/EffectViewer.Android.csproj`：`ApplicationDisplayVersion`
- `EffectViewer.macOS/EffectViewer.macOS.csproj`：`ApplicationDisplayVersion`
- `CHANGELOG.md`：带日期的 `## x.y.z - YYYY-MM-DD` 小节

发布工作流会在构建产物前自动校验这些元数据。

## 本地发布候选检查

在仓库根目录运行：

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj --configuration Release
dotnet build EffectViewer.Windows/EffectViewer.Windows.csproj --configuration Release
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj --configuration Release
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj --configuration Release
```

然后运行 QuickStart 冒烟测试：

```bash
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj --filter QuickStartShowcaseSmokeTests
```

公开发布前，继续完成 [QuickStart 冒烟测试](quickstart_smoke_test.zh-CN.md) 中的 Desktop 和 Browser 手动检查。

## GitHub 发布工作流

发布工作流位于 `.github/workflows/release.yml`。

推送匹配 `v*.*.*` 的标签时会自动运行：

```bash
git tag v0.1.0
git push origin v0.1.0
```

也可以在 GitHub Actions 页面手动运行。手动运行可以只构建产物；开启 `create_release` 时，会同时创建草稿 GitHub Release。

## 发布产物

工作流会发布：

- 从 `EffectViewer.Windows` 发布 Windows `win-x64` 和 `win-arm64` zip 包。
- 从 `EffectViewer.Desktop` 发布 Linux `linux-x64` 和 `linux-arm64` tar 包。
- 从 `EffectViewer.Browser` 发布 Browser 静态 WebAssembly zip 包。
- 从 `EffectViewer.macOS` 发布 macOS `osx` DMG。
- 在 macOS 上从 `EffectViewer.iOS` 发布 iOS `ios-arm64` IPA。
- 在 Windows、macOS 和 Linux 上发布 Android `android-arm64` 包，并可使用 JKS keystore 签名。

工作流使用的脚本位于 `scripts/release`。iOS 签名与 provisioning 需要提前在 macOS runner 上配置好，或通过 `IOS_CODESIGN_KEY`、`IOS_CODESIGN_PROVISION`、`IOS_CODESIGN_KEYCHAIN` 环境变量传入。Android JKS 签名可用 `ANDROID_SIGNING_KEYSTORE` 指向本地 keystore，或在 GitHub Actions 中使用 `ANDROID_SIGNING_KEYSTORE_BASE64`，并配合 `ANDROID_SIGNING_KEY_ALIAS`、`ANDROID_SIGNING_STORE_PASS` 和可选的 `ANDROID_SIGNING_KEY_PASS`。

## 最终检查

把草稿 GitHub Release 标记为正式发布前，确认：

- 所有 workflow job 都已通过。
- 尽可能在目标平台下载并打开每个发布产物。
- QuickStart 示例可以导入、运行，并导出预览。
- Release notes 与实际发布内容一致。
- 发布标签、产物名称和应用显示版本使用同一个版本号。
