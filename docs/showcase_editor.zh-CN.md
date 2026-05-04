# ShowCase 脚本编辑器

ShowCase 编辑器用于编写和运行 Lua 展示脚本，把图像、reanim、particle、trail 和绘图命令组合到同一个预览场景中。

完整脚本 API 请阅读 [Lua API 文档](lua_api.zh-CN.md)。

## 打开方式

有两种 ShowCase：

- 项目中的 `.lua` 资源：在项目资源管理器中展开 `展示` 并点击脚本。
- 内置用户脚本页：应用没有打开具体 `.lua` 时也可进入临时 ShowCase 编辑体验。

项目脚本支持保存和导出；内置用户脚本页不对应项目文件，通常用于临时试验。

## 主界面组成

- 中央预览视口：显示脚本运行后的场景。
- 脚本面板：编辑 Lua 代码。
- `运行` 按钮：执行当前脚本。
- 日志列表：显示 `scene.log` 输出、运行状态和错误。

## 编写脚本

脚本编辑器特性：

- 显示行号。
- Lua 语法着色。
- 支持搜索面板。
- Tab 会转换为空格。
- 当前行高亮。
- 支持撤销/重做。

补全：

- Windows/Linux：`Ctrl+Space`
- macOS/iOS：`Command+Space`
- 输入 `.` 或 `:` 后会尝试显示成员补全。

补全会根据常见变量来源推断类型，例如：

- `scene`
- `global_attachment`
- `g` 或 `graphics`
- `scene.reanim(...)` 返回的 reanim 对象
- `scene.particle_system(...)` 返回的粒子对象
- `scene.trail(...)` 返回的轨迹对象

## 运行脚本

操作步骤：

1. 在脚本面板中输入 Lua 代码。
2. 点击右上角 `运行`。
3. 查看预览视口和日志列表。

运行时行为：

- 会清空上一次日志和场景对象列表。
- 成功时，预览视口会绑定脚本创建的帧提供器。
- 失败时，预览会停止，并选中第一条带位置的错误日志。

如果日志带行列号，点击日志会：

- 跳转到对应行列。
- 选中错误所在行。
- 聚焦脚本编辑器。

## 最小脚本示例

```lua
scene.clear()
scene.log("showcase initialized")
```

## 创建并绘制资源

```lua
scene.clear()

local body = scene.reanim("sample_reanim", 400, 300)
local fire = scene.particle_system("fire_burst", 420, 280)
local slash = scene.trail("sword_slash", 0, 0)

local context = {}
local t = 0

function context:update(dt)
    t = t + dt
    body:set_position(400 + math.sin(t * 2) * 40, 300)
    fire:set_scale(0.8 + math.sin(t * 3) * 0.2)
    slash:clear_points()
    slash:add_point(330, 320)
    slash:add_point(470, 280 + math.sin(t * 4) * 40)
end

function context:draw(g)
    body:draw(g)
    fire:draw(g)
    slash:draw(g)
end

scene.regist(context)
scene.log("showcase initialized")
```

注意：示例中的资源 ID 必须存在于当前项目中。

## 直接绘图

```lua
scene.clear()

local context = {}

function context:draw(g)
    g:reset()
    g:set_color(255, 128, 64, 220)
    g:fill_rect(120, 120, 180, 64)
end

scene.regist(context)
```

## 保存与导出

保存入口：

- `文件 -> 保存当前文件`
- `文件 -> 保存全部文件`

保存会把脚本文本写回项目中的 `.lua` 文件。

导出入口：`文件 -> 导出当前文件...`

导出会复制当前 `.lua` 文件到你选择的位置。

## 预览导出

入口：`文件 -> 导出预览...`

ShowCase 预览导出会在独立的运行世界中重新运行脚本，再捕获帧。支持：

- PNG
- PNG 序列 Zip
- GIF
- WebP

如果脚本运行失败，导出无法生成有效动画；请先在编辑器中点击 `运行` 并修复日志错误。

## 撤销与重做

脚本编辑器使用文本编辑器自己的撤销栈。

快捷键：

- 撤销：Windows/Linux `Ctrl+Z`，macOS/iOS `Command+Z`
- 重做：Windows/Linux `Ctrl+Y` 或 `Ctrl+Shift+Z`，macOS/iOS `Command+Y` 或 `Command+Shift+Z`

## 布局

入口：编辑器右上角 `布局`。

可操作：

- 显示/隐藏脚本面板。
- 将脚本面板停靠左侧或右侧。
- 重置编辑器布局。

## 常见问题

### 为什么运行后没有画面？

常见原因：

- 脚本没有注册 `context`。
- `draw(g)` 中没有绘制对象。
- 资源 ID 不存在，导致创建对象失败。
- 对象位置在视口外。

先查看日志列表，再确认资源 ID 和绘制逻辑。

### 为什么导出预览和当前画面不一致？

导出会重新运行脚本，并从初始状态开始捕获帧。如果脚本依赖外部随机数或运行时状态，导出结果可能与当前已经播放过的画面不同。

