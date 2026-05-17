# 资源管理与导入导出

资源管理功能围绕项目资源管理器展开。你可以创建部分资源、导入外部资源、搜索资源、打开最近使用资源、删除资源，并把当前资源导出为项目外文件。

## 项目资源管理器

项目资源管理器默认停靠在主窗口左侧，按类型分组显示资源：

- `图像`
- `字体`
- `动画`
- `粒子`
- `轨迹`
- `展示`

常用操作：

- 点击分组前的 `>` / `v` 展开或收起。
- 点击资源打开对应编辑器。
- 在搜索框输入文本过滤资源。
- 点击搜索框右侧的 `x` 清空搜索。
- 右键资源，选择 `删除资源...` 删除。

入口：

- 显示/隐藏：`视图 -> 项目资源管理器`
- 停靠左侧：`视图 -> 资源管理器停靠左侧`
- 停靠右侧：`视图 -> 资源管理器停靠右侧`

快捷键：

- Windows/Linux：`Ctrl+B`
- macOS/iOS：`Command+B`

## 最近打开

入口：`资源 -> 最近打开`

最近打开列表记录当前项目中最近打开过的资源，最多显示 8 项。点击列表项可直接重新打开资源。

## 新建资源

入口：`资源 -> 新建资源...`

快捷键：

- Windows/Linux：`Ctrl+N`
- macOS/iOS：`Command+N`

可新建类型：

- Reanim
- Particle
- Trail
- ShowCase

不可通过新建对话框创建：

- Image：请通过导入图像文件添加。
- Font：请通过导入 `.ttf` 或资源文件夹添加。

操作步骤：

1. 确保当前已加载可写项目。
2. 点击 `资源 -> 新建资源...`。
3. 选择类型。
4. 输入资源 ID。
5. 点击 `创建`。

默认文件位置：

| 类型 | 默认目录 | 默认扩展名 |
| --- | --- | --- |
| Reanim | `assets/reanims` | `.reanim` |
| Particle | `assets/particles` | `.xml` |
| Trail | `assets/trails` | `.trail` |
| ShowCase | `scripts` | `.lua` |

如果 ID 或文件名冲突，应用会自动追加序号。

## 导入单个资源文件

入口：`资源 -> 导入资源文件...`

快捷键：

- Windows/Linux：`Ctrl+I`
- macOS/iOS：`Command+I`

导入单个文件会把资源复制到当前项目，并加入项目清单。

支持的单文件类型：

| 类型 | 扩展名 |
| --- | --- |
| Image | `.png`、`.jpg`、`.jpeg`、`.bmp`、`.gif`、`.webp`、`.tga` |
| Font | `.ttf` |
| Reanim | `.reanim`、`.reanim.compiled` |
| Particle | `.xml`、`.xml.compiled` |
| Trail | `.trail`、`.trail.compiled` |
| ShowCase | `.lua` |

导入规则：

- 图像 ID 会以 `IMAGE_` 开头，并使用大写安全名称。
- 其他资源 ID 默认来自文件名的安全化结果。
- 文件会复制到对应的项目资源目录。
- 如果资源 ID 或路径冲突，应用会自动追加序号。
- 导入后会打开对应编辑器。

## 导入资源文件夹

入口：`资源 -> 导入资源文件夹...`

快捷键：

- Windows/Linux：`Ctrl+Shift+I`
- macOS/iOS：`Command+Shift+I`

导入资源文件夹用于把外部资源目录追加到当前项目，并把识别出的资源加入项目清单。

导入前行为：

- 必须先创建或打开一个可写项目。
- 如果导入资源的 ID 已存在，会弹窗选择 `跳过`、`覆盖` 或 `同时保留`。
- 可勾选“后续冲突执行相同操作”，把本次选择应用到剩余重复 ID。

推荐的源目录结构：

```text
properties/resources.xml
reanim/*.reanim
particles/*.xml
particles/*.trail
```

或编译资源结构：

```text
properties/resources.xml
compiled/reanim/*.reanim.compiled
compiled/particles/*.xml.compiled
compiled/particles/*.trail.compiled
compiled/trails/*.trail.compiled
```

导入后的项目目录：

```text
assets/images
assets/fonts
assets/reanims
assets/particles
assets/trails
```

资源文件夹导入规则：

- 如果存在 `properties/resources.xml`，会读取其中的 `Image` 和 `Font` 资源。
- `SetDefaults` 的 `path` 和 `idprefix` 会影响后续 `Image` / `Font` 的解析。
- 文件夹导入按约定识别图片扩展名 `.png`、`.jpg`、`.jpeg`、`.gif`。
- `resources.xml` 中的图片会读取 `rows` 和 `cols`。
- 字体资源会查找 `.txt` 图片字体描述或 `.ttf` TrueType 字体。
- 没有在 `resources.xml` 中出现的图片也会按文件名导入。
- 编译版 reanim、particle 和 trail 会转换为源格式保存到当前项目中。
- 选择 `同时保留` 时，重复 ID 会自动追加序号；导入的 reanim、particle、trail 会尽量同步改写对导入图像的引用。

Alpha 伴随图规则：

- `name.png` 的伴随 alpha 图可以命名为 `_name.png` 或 `name_.png`。
- 如果找到伴随图，会写入图像资源的 `alphaPath`。
- 如果只有 `_name.png` 或 `name_.png`，会被识别为 `alphaOnly` 图像。

## 导入资源 Pak

入口：`资源 -> 导入资源 Pak...`

用途：把 `.pak` 资源包当作资源文件夹读取并追加到当前项目。

导入行为与“导入资源文件夹”相同：

- 会根据包内目录和资源清单导入图像、字体、reanim、particle 和 trail。
- 遇到重复 ID 时选择跳过、覆盖或同时保留。

## 拖放导入

可把支持的资源文件拖到项目资源管理器中。

支持拖放：

- 单个资源文件。
- `.pak` 文件。

不支持拖放：

- 资源文件夹。
- 项目 Zip。
- 不在支持列表中的文件。

拖放导入单个资源或 `.pak` 时都需要当前已有可写项目；拖放 `.pak` 会按 Pak 导入流程追加到当前项目。

## 删除资源

入口：

- 资源管理器中选中资源后按 `Delete`
- `资源 -> 删除资源...`
- 右键资源，选择 `删除资源...`
- Windows/Linux：`Ctrl+Delete`
- macOS/iOS：`Command+Delete`

删除行为：

- 从项目清单中移除资源。
- 删除项目目录中对应文件。
- 删除图像时会同时删除 `alphaPath` 指向的伴随图。
- 关闭对应已打开编辑器。

注意：删除不可撤销。删除前如资源编辑器有未保存更改，会先出现未保存更改确认。

## 保存当前文件和保存全部文件

入口：

- `文件 -> 保存当前文件`
- `文件 -> 保存全部文件`

快捷键：

- 保存当前文件：Windows/Linux `Ctrl+S`，macOS/iOS `Command+S`
- 保存全部文件：Windows/Linux `Ctrl+Shift+S`，macOS/iOS `Command+Shift+S`

保存行为：

- Image / Font：保存到项目清单。
- Reanim：编码并写回 `.reanim` 或对应动画文件，同时保存项目清单。
- Particle：编码并写回 `.xml` 或对应粒子文件。
- Trail：编码并写回 `.trail` 或对应轨迹文件。
- ShowCase：写回 `.lua` 脚本文件。

## 导出当前文件

入口：`文件 -> 导出当前文件...`

快捷键：

- Windows/Linux：`Ctrl+Shift+E`
- macOS/iOS：`Command+Shift+E`

用途：把当前编辑器对应的项目文件导出到项目外。

导出规则：

- Image / Font / ShowCase 默认导出原始项目文件。
- Reanim 可选择源格式 `.reanim` 或编译格式 `.reanim.compiled`。
- Particle 可选择源格式 `.xml` 或编译格式 `.xml.compiled`。
- Trail 可选择源格式 `.trail` 或编译格式 `.trail.compiled`。
- 如果当前编辑器有未保存修改，导出前会先保存。

## 常见问题

### 导入资源文件夹遇到重复 ID 怎么办？

导入时可以选择 `跳过` 保留当前项目已有资源，选择 `覆盖` 用导入内容替换已有资源，或选择 `同时保留` 自动生成新 ID。勾选“后续冲突执行相同操作”可把同一选择应用到本次导入剩余冲突。

### 为什么新建资源里没有图像和字体？

图像需要实际图片文件，字体需要字体文件或字体描述文件；它们目前通过导入添加。

### 为什么导入资源文件夹没有识别 `.webp` 或 `.tga`？

单文件导入支持 `.webp` 和 `.tga`。资源文件夹按约定导入目前只扫描 `.png`、`.jpg`、`.jpeg`、`.gif` 图片。
