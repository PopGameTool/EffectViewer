# QuickStart 冒烟测试

简体中文 | [English](quickstart_smoke_test.en-US.md)

发布候选版本前，或改动项目导入导出、Lua ShowCase 播放、预览渲染、打包流程后，运行这份冒烟测试。

## 自动化检查

```bash
dotnet test EffectViewer.Tests/EffectViewer.Tests.csproj --filter QuickStartShowcaseSmokeTests
```

这个测试会导入 `Samples/QuickStartShowcase.zip`，运行内置 Lua ShowCase，捕获一帧预览，并验证预览可以导出为 PNG。

## Desktop 手动检查

1. 启动桌面应用：

   ```bash
   dotnet run --project EffectViewer.Desktop/EffectViewer.Desktop.csproj
   ```

2. 选择 `项目 -> 导入项目 Zip...`。
3. 选择 `Samples/QuickStartShowcase.zip`。
4. 确认导入后的项目名称是 `Quick Start Showcase`。
5. 从项目资源管理器打开 `quickstart_showcase` 资源。
6. 在 ShowCase 编辑器中点击 `运行`。
7. 确认预览区出现深色面板，以及带动画的彩色条块。
8. 确认日志包含 `Quick Start Showcase loaded`。
9. 分别导出一次 PNG，以及一次 GIF 或 WebP。
10. 保存项目，关闭应用，重新启动，并从项目列表重新打开导入后的项目。

## Browser 手动检查

1. 构建或启动浏览器目标：

   ```bash
   dotnet build EffectViewer.Browser/EffectViewer.Browser.csproj
   ```

2. 通过当前平台使用的本地开发流程打开 Browser 目标。
3. 导入 `Samples/QuickStartShowcase.zip`。
4. 打开并运行 `quickstart_showcase`。
5. 确认预览可以渲染，日志正常出现，文件选择和导出操作没有失败。

## 通过标准

- 示例项目导入时不出现错误弹窗。
- 项目资源管理器中存在且只存在一个名为 `quickstart_showcase` 的 ShowCase 资源。
- 脚本运行成功，并且编辑器保持响应。
- 预览导出能为 PNG 和至少一种动画格式写出非空结果。
- 保存并重新打开导入项目后，manifest 和脚本仍然保留。
