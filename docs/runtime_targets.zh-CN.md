# 运行目标与开发启动

EffectViewer 是基于 Avalonia 的跨平台应用。仓库包含共享 UI/运行时代码，以及 Desktop、Windows、Browser、Android、iOS 宿主项目。

## 项目结构

```text
EffectViewer/           共享 Avalonia UI、ViewModel、渲染、运行时和 TodLib 代码
EffectViewer.Desktop/   桌面应用宿主
EffectViewer.Windows/   Windows 发布宿主
EffectViewer.Browser/   浏览器/WebAssembly 应用宿主
EffectViewer.Android/   Android 应用宿主
EffectViewer.iOS/       iOS 应用宿主
EffectViewer.Tests/     单元测试
Samples/                可导入的示例项目
docs/                   使用文档和 Lua API 文档
```

## 环境要求

基础要求：

- 支持 `net10.0` 的 .NET SDK。
- 构建浏览器目标时需要支持 `net10.0-browser`。
- 构建 Windows 发布目标时需要支持 `net10.0-windows`。

桌面版要求：

- 系统支持 OpenGL。

移动端要求：

- Android：需要 Android SDK。
- iOS：需要 Xcode、iOS 模拟器或相关签名/部署工具。

## 运行桌面版

在仓库根目录执行：

```bash
dotnet run --project EffectViewer.Desktop/EffectViewer.Desktop.csproj
```

桌面版适合日常编辑和调试，功能最完整。

## 构建 Desktop

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj
```

如果只修改共享 UI、ViewModel、项目管理、渲染或 TodLib，大多数情况下构建 Desktop 就能完成基础冒烟验证。

## 构建 Windows 发布宿主

```bash
dotnet build EffectViewer.Windows/EffectViewer.Windows.csproj
```

Windows 发布宿主使用 `net10.0-windows` 并引用 `YY-Thunks`，发布脚本会从这个项目生成 Windows zip 包。

## 构建 Browser

```bash
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
```

Browser 目标使用 WebAssembly/WebGL 宿主，适合验证浏览器环境下的渲染和文件选择行为。

## 构建完整解决方案

```bash
dotnet build EffectViewer.slnx
```

注意：

- 完整解决方案包含 Android 和 iOS 项目。
- 如果本机没有 Android SDK 或 iOS 工具链，完整构建可能在移动端项目失败。
- 只需要主编辑器时，优先单独构建 Desktop、Windows 或 Browser。

## Android 和 iOS 项目

仓库包含移动端壳项目：

- `EffectViewer.Android/EffectViewer.Android.csproj`
- `EffectViewer.iOS/EffectViewer.iOS.csproj`

这些项目复用共享 UI 和运行时代码，但构建、签名、模拟器和部署由对应平台工具链负责。

## 示例项目

仓库内置示例：

```text
Samples/QuickStartShowcase.zip
```

试用步骤：

1. 运行桌面版。
2. 点击 `项目 -> 导入项目 Zip...`。
3. 选择 `Samples/QuickStartShowcase.zip`。
4. 在项目资源管理器中打开 `quickstart_showcase`。
5. 在 ShowCase 编辑器中点击 `运行`。

## 推荐验证流程

修改文档时：

```bash
git diff -- docs
```

修改代码时：

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj
dotnet build EffectViewer.Windows/EffectViewer.Windows.csproj
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj
```

涉及 Browser 宿主时：

```bash
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
```

涉及移动端时，在对应平台 SDK 配置完成后再构建对应项目。
