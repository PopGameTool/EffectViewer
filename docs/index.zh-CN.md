# EffectViewer 功能使用文档

[English](index.en-US.md) | 简体中文

本文档目录面向使用 EffectViewer 管理、查看、编辑和演示特效资源的用户。更偏向脚本 API 的内容请阅读已有的 [Lua API 文档](lua_api.zh-CN.md)。

## 文档目录

- [项目管理](project_management.zh-CN.md)：新建、打开、重命名、删除、导入和导出 EffectViewer 项目。
- [资源管理与导入导出](resource_management.zh-CN.md)：项目资源管理器、搜索、最近打开、单文件导入、资源文件夹导入、Pak 导入、拖放导入、删除资源和导出当前文件。
- [图像编辑器](image_editor.zh-CN.md)：查看图像图集，编辑图像 ID、行列数和当前帧。
- [字体编辑器](font_editor.zh-CN.md)：查看图片字体和 TrueType 字体，编辑字体 ID、字号和描边。
- [Reanim 动画编辑器](reanim_editor.zh-CN.md)：轨道、帧、时间线、变换、可视化拖拽和补间元数据。
- [Particle 粒子编辑器](particle_editor.zh-CN.md)：粒子发射器、参数轨道、粒子场、系统场和实时预览。
- [Trail 轨迹编辑器](trail_editor.zh-CN.md)：轨迹图片、点设置、循环、宽度/透明度曲线和时长。
- [ShowCase 脚本编辑器](showcase_editor.zh-CN.md)：Lua 展示脚本、运行预览、补全和日志定位。
- [预览与导出](preview_export.zh-CN.md)：导出当前预览为 PNG、PNG 序列 Zip、GIF 或 WebP。
- [布局、标签页、语言和快捷键](layout_language_shortcuts.zh-CN.md)：工作区布局、标签页管理、视口操作、界面语言和常用快捷键。
- [运行目标与开发启动](runtime_targets.zh-CN.md)：Desktop、Browser、Android 和 iOS 目标的启动与构建。

## 支持的资源类型

EffectViewer 项目使用 `project.effectproj.json` 作为项目清单。清单可引用以下资源：

| 类型 | 用途 | 常见扩展名 |
| --- | --- | --- |
| Image | 图像、图集、粒子贴图、动画帧贴图 | `.png`、`.jpg`、`.jpeg`、`.bmp`、`.gif`、`.webp`、`.tga` |
| Font | 图片字体描述或 TrueType 字体 | `.txt`、`.ttf` |
| Reanim | PvZ/Tod 风格 reanimation 动画定义 | `.reanim`、`.reanim.compiled` |
| Particle | 粒子系统定义 | `.xml`、`.xml.compiled` |
| Trail | 拖尾/轨迹定义 | `.trail`、`.trail.compiled` |
| ShowCase | Lua 展示脚本 | `.lua` |

## 基本工作流

1. 启动桌面应用。
2. 通过 `项目 -> 新建项目...` 创建空项目，或通过 `项目 -> 导入项目 Zip...` / `资源 -> 导入资源文件夹...` 导入现有资源。
3. 在项目资源管理器中搜索或展开资源分组。
4. 打开图像、字体、动画、粒子、轨迹或 ShowCase 资源进行编辑。
5. 用 `文件 -> 保存当前文件`、`文件 -> 保存全部文件` 或 `项目 -> 保存项目` 保存修改。
6. 用 `文件 -> 导出当前文件...` 导出资源源文件/编译文件，或用 `文件 -> 导出预览...` 导出预览图片/动画。
7. 用 `项目 -> 导出项目 Zip...` 打包整个项目。

## 重要概念

- **内部项目**：通过新建、打开、导入项目 Zip、导入资源文件夹或导入 Pak 创建/加载到应用私有项目目录的项目。重命名、删除、保存和导出项目 Zip 都以内部项目为操作对象。
- **资源 ID**：脚本和资源引用时使用的名称。图像导入时通常会生成 `IMAGE_...` 格式的 ID；其他资源使用安全化后的文件名或创建时输入的 ID。
- **项目路径**：清单中记录的相对路径，例如 `assets/images/IMAGE_FIRE.png`、`assets/reanims/zombie.reanim` 或 `scripts/demo.lua`。
- **未保存标记**：文档标签标题后出现 `*` 表示当前编辑器有未保存修改。
- **预览视口**：所有资源编辑器共享的渲染预览区域，可滚轮缩放、中键拖动画布，双击重置缩放和平移。
