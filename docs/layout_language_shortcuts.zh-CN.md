# 布局、标签页、语言和快捷键

本文说明 EffectViewer 的工作区操作，包括项目资源管理器、编辑器侧栏、文档标签页、视口操作、语言切换和快捷键。

## 主窗口布局

默认布局：

- 顶部：主菜单和当前项目名。
- 左侧：项目资源管理器。
- 中央：文档标签页和当前编辑器。
- 底部：状态栏。

## 项目资源管理器布局

入口：`视图`

可操作：

- `项目资源管理器`：显示或隐藏。
- `资源管理器停靠左侧`：移动到左侧。
- `资源管理器停靠右侧`：移动到右侧。
- `重置布局`：恢复默认布局。

项目资源管理器宽度可通过分隔条拖拽调整。

快捷键：

- 显示/隐藏资源管理器：Windows/Linux `Ctrl+B`，macOS/iOS `Command+B`

## 编辑器侧栏布局

Image、Font、Reanim、Particle、Trail 编辑器提供 `属性` 侧栏。

ShowCase 编辑器提供 `脚本面板` 侧栏。

入口：各编辑器右上角 `布局`

可操作：

- 显示或隐藏侧栏。
- 停靠左侧。
- 停靠右侧。
- 重置编辑器布局。

## 文档标签页

每个打开的资源都会显示为一个文档标签。

标签内容：

- 资源类型短码，例如 `IMG`、`FNT`、`REA`、`PAR`、`TRL`、`LUA`。
- 标题。
- 未保存修改会在标题后显示 `*`。
- 固定标签显示 `PIN`。

鼠标操作：

- 点击标签：切换文档。
- 点击 `x`：关闭标签。
- 拖动标签：改变标签顺序。
- 右键标签：打开标签操作菜单。

菜单入口：`标签页`

可操作：

- 上一个/下一个标签页。
- 向左/向右移动标签页。
- 固定/取消固定标签页。
- 关闭当前标签页。
- 关闭其他标签页。
- 关闭左侧/右侧标签页。
- 关闭已保存标签页。
- 关闭全部标签页。

快捷键：

| 操作 | Windows/Linux | macOS/iOS |
| --- | --- | --- |
| 关闭当前标签页 | `Ctrl+W` | `Command+W` |
| 关闭全部标签页 | `Ctrl+Shift+W` | `Command+Shift+W` |
| 下一个标签页 | `Ctrl+Tab` | `Command+Tab` |
| 上一个标签页 | `Ctrl+Shift+Tab` | `Command+Shift+Tab` |
| 标签向左移动 | `Ctrl+Alt+Left` | `Command+Alt+Left` |
| 标签向右移动 | `Ctrl+Alt+Right` | `Command+Alt+Right` |

## 未保存更改提示

当你关闭文档、切换项目、导入项目、删除资源等操作会影响未保存内容时，系统会提示：

- `保存`：保存当前修改后继续。
- `放弃`：丢弃修改并继续。
- `取消`：取消本次操作。

固定标签页不会阻止保存和关闭全部操作，但标签菜单会根据文档是否允许关闭来启用或禁用命令。

## 预览视口操作

所有预览视口支持：

| 操作 | 效果 |
| --- | --- |
| 鼠标滚轮 | 缩放。 |
| 中键拖动 | 平移。 |
| 左键拖动 | 普通编辑器中平移；Reanim 自由变换模式中拖拽对象或控制点。 |
| 双击 | 重置缩放和平移。 |

背景模式：

- `视图 -> 浅色背景`
- `视图 -> 深色背景`

如果两个选项都不勾选，视口使用主题默认背景。

## 语言切换

入口：`语言`

可选：

- `English`
- `简体中文`
- `加载语言文件...`

加载自定义语言文件：

1. 点击 `语言 -> 加载语言文件...`。
2. 选择 `.json` 语言文件。
3. 加载成功后界面文本会更新。

自定义语言文件应使用与内置语言相同的结构，例如：

```json
{
  "languageCode": "custom",
  "languageName": "Custom",
  "strings": {
    "Common": {
      "Save": "Save"
    }
  }
}
```

内置语言文件可参考：

- `EffectViewer/Localization/en-US.json`
- `EffectViewer/Localization/zh-CN.json`

## 常用快捷键总表

macOS/iOS 上，文档中的 `Ctrl` 会自动映射为 `Command`。

| 操作 | Windows/Linux | macOS/iOS |
| --- | --- | --- |
| 新建项目 | `Ctrl+Shift+N` | `Command+Shift+N` |
| 打开项目 | `Ctrl+O` | `Command+O` |
| 保存当前文件 | `Ctrl+S` | `Command+S` |
| 保存项目 | `Ctrl+Alt+S` | `Command+Alt+S` |
| 保存全部文件 | `Ctrl+Shift+S` | `Command+Shift+S` |
| 新建资源 | `Ctrl+N` | `Command+N` |
| 导入资源文件 | `Ctrl+I` | `Command+I` |
| 导入资源文件夹 | `Ctrl+Shift+I` | `Command+Shift+I` |
| 删除资源 | `Ctrl+Delete` | `Command+Delete` |
| 导出预览 | `Ctrl+E` | `Command+E` |
| 导出当前文件 | `Ctrl+Shift+E` | `Command+Shift+E` |
| 显示/隐藏资源管理器 | `Ctrl+B` | `Command+B` |
| 关闭当前标签页 | `Ctrl+W` | `Command+W` |
| 关闭全部标签页 | `Ctrl+Shift+W` | `Command+Shift+W` |
| 下一个标签页 | `Ctrl+Tab` | `Command+Tab` |
| 上一个标签页 | `Ctrl+Shift+Tab` | `Command+Shift+Tab` |
| 标签向左移动 | `Ctrl+Alt+Left` | `Command+Alt+Left` |
| 标签向右移动 | `Ctrl+Alt+Right` | `Command+Alt+Right` |
| 撤销 | `Ctrl+Z` | `Command+Z` |
| 重做 | `Ctrl+Y` 或 `Ctrl+Shift+Z` | `Command+Y` 或 `Command+Shift+Z` |
| ShowCase 补全 | `Ctrl+Space` | `Command+Space` |

