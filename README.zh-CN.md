# EffectViewer

[English](README.md) | [简体中文](README.zh-CN.md)

EffectViewer 是一个基于 Avalonia 的跨平台特效资源查看和编辑工具。它可以把特效资源组织成项目，通过 OpenGL/WebGL 预览，并运行 Lua ShowCase 脚本，把 reanimation、particle、trail 和简单绘图命令组合到同一个场景里。

## 可以做什么

- 创建、重命名、删除、导入、导出和重新打开 EffectViewer 项目。
- 导入单个资源文件或整个资源文件夹。
- 预览图片图集，并编辑图片 ID、行数、列数和当前帧。
- 查看和编辑 reanimation、particle、trail 定义。
- 编辑 reanimation 轨道、帧、变换、补间元数据和时间线选择。
- 预览粒子发射器、浮点参数轨道、粒子场和拖尾曲线。
- 编写并运行 Lua ShowCase 脚本，制作可动画演示的特效场景。
- 使用 English 或简体中文界面，也可以加载自定义 JSON 语言文件。
- 运行 Desktop 和 Browser 目标，并包含 Android 与 iOS 项目壳。

## 快速开始

### 环境要求

- 支持 `net10.0` 和 `net10.0-browser` 的 .NET SDK。
- 桌面版需要系统支持 OpenGL。
- 构建移动端目标时需要额外平台 SDK：
  - `EffectViewer.Android` 需要 Android SDK。
  - `EffectViewer.iOS` 需要 Xcode/iOS 模拟器相关工具。

### 运行桌面版

```bash
dotnet run --project EffectViewer.Desktop/EffectViewer.Desktop.csproj
```

### 构建 Desktop 和 Browser

```bash
dotnet build EffectViewer.Desktop/EffectViewer.Desktop.csproj
dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
```

完整解决方案包含 Android 和 iOS 项目：

```bash
dotnet build EffectViewer.slnx
```

如果本机没有配置 Android SDK，完整解决方案可能会在 `EffectViewer.Android` 处构建失败。只需要主编辑器时，可以直接构建 Desktop 或 Browser 项目。

## 试用示例项目

仓库里包含一个可直接导入的示例：

```text
Samples/QuickStartShowcase.zip
```

在应用里试用：

1. 运行桌面版应用。
2. 选择 `项目` -> `导入项目 Zip...`。
3. 选择 `Samples/QuickStartShowcase.zip`。
4. 在项目资源管理器中打开 `quickstart_showcase`。
5. 在 ShowCase 编辑器里点击 `运行`。

如果想直接查看 manifest 和 Lua 脚本，可以先解压 `Samples/QuickStartShowcase.zip`。

## 项目结构

```text
EffectViewer/           共享 Avalonia UI、ViewModel、渲染、运行时和 TodLib 代码
EffectViewer.Desktop/   桌面应用宿主
EffectViewer.Browser/   浏览器/WebAssembly 应用宿主
EffectViewer.Android/   Android 应用宿主
EffectViewer.iOS/       iOS 应用宿主
Samples/                可导入的示例项目
docs/lua_api.zh-CN.md   Lua ShowCase 脚本文档
```

## 支持的资源类型

EffectViewer 项目使用 `project.effectproj.json` 作为清单文件，可以引用：

- 图片：`.png`、`.jpg`、`.jpeg`、`.bmp`、`.gif`、`.webp`、`.tga`
- Reanimation：`.reanim`、`.reanim.compiled`
- Particle：`.xml`、`.xml.compiled`
- Trail：`.trail`、`.trail.compiled`
- ShowCase：`.lua`

## ShowCase 脚本

ShowCase 是运行在当前项目里的 Lua 脚本。脚本可以创建项目中的资源：

```lua
local body = scene.reanim("sample_reanim", 400, 300)
local fire = scene.particle_system("fire_burst", 420, 280)
local slash = scene.trail("sword_slash", 0, 0)
```

也可以直接绘图：

```lua
local context = {}

function context:draw(g, elapsed, frame)
    g:reset()
    g:set_color(255, 128, 64, 220)
    g:fill_rect(120, 120, 180, 64)
end

scene.regist(context)
```

完整脚本 API 见 [docs/lua_api.zh-CN.md](docs/lua_api.zh-CN.md)。

## 开发说明

- NuGet 包版本集中维护在 [Directory.Packages.props](Directory.Packages.props)。
- 共享 UI 和运行时代码目标框架为 `net10.0`；浏览器宿主目标框架为 `net10.0-browser`。
- Desktop 和 Browser 构建可以作为大多数共享代码的基础冒烟测试。
- 移动端构建即使没有改动共享代码，也依然需要对应原生 SDK。
