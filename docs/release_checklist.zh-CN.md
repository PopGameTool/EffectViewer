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

- Desktop `linux-x64`
- Desktop `win-x64`
- Desktop `osx-arm64`
- Browser 静态 WebAssembly 构建

Android 和 iOS 打包暂时保留为手动流程，因为签名、provisioning 和 SDK 环境都依赖具体机器配置。

## 最终检查

把草稿 GitHub Release 标记为正式发布前，确认：

- 所有 workflow job 都已通过。
- 尽可能在目标平台下载并打开每个发布产物。
- QuickStart 示例可以导入、运行，并导出预览。
- Release notes 与实际发布内容一致。
- 发布标签、产物名称和应用显示版本使用同一个版本号。
