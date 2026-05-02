# ShowCases 文档

ShowCase 是 EffectViewer 中用于组合、预览和演示特效资源的 Lua 脚本。一个 ShowCase 可以在同一场景里创建 reanim、particle 和 trail，并通过脚本控制它们的位置、颜色、缩放、挂点、绘制顺序和逐帧行为。

## 项目配置

ShowCase 资源记录在项目清单 `project.effectproj.json` 的 `Showcases` 数组中：

```json
{
  "Showcases": [
    {
      "Id": "demo_scene",
      "Path": "scripts/demo.lua"
    }
  ]
}
```

字段说明：

- `Id`：ShowCase 在项目树中显示的资源 ID。
- `Path`：Lua 脚本路径。相对路径会从项目根目录解析，通常放在 `scripts/` 下。

在应用内新建 ShowCase 资源时，会生成一个 `.lua` 文件，并写入默认脚本：

```lua
scene.clear()
effect.log("new_showcase initialized")
```

## 脚本生命周期

点击 ShowCase 编辑器中的 `Run` 后，EffectViewer 会执行当前 Lua 脚本：

1. 创建一个新的 ShowCase 场景。
2. 注册全局对象 `scene` 和 `effect`。
3. 执行脚本正文。
4. 如果定义了 `update` 或 `draw` 函数，会在预览播放期间持续调用。

可选回调：

```lua
function update(dt, elapsed, frame)
    -- dt: 当前固定步长秒数
    -- elapsed: 脚本累计运行秒数
    -- frame: 脚本更新帧序号
    -- 如果定义了 update，需要手动更新希望推进的对象
end

function draw(g, elapsed, frame)
    -- g: 绘图 API
    -- 如果定义了 draw，需要手动绘制希望显示的对象
end
```

如果没有定义 `update`，场景会自动更新所有动画、粒子、拖尾和挂接关系。  
如果定义了 `update`，脚本会接管更新流程，需要显式调用对象的 `update()`，以及必要时调用 trail 的路径更新方法。

如果没有定义 `draw`，场景会自动绘制所有非 attachment 的 reanim、particle 和 trail。  
如果定义了 `draw`，脚本需要显式调用对象的 `draw(g)` 或 `draw_group(g, group)`。

## 全局 API

### scene

```lua
scene.clear()
local count = scene.count()
local obj0 = scene.object_at(0)
local reanim0 = scene.reanim_at(0)
local particle0 = scene.particle_at(0)
local trail0 = scene.trail_at(0)
local body = scene.find_reanim("sample_reanim")
local fire = scene.find_particle("fire_burst")
local slash = scene.find_trail("sword_slash")
local any_object = scene.find_object("sample_reanim")
```

- `scene.clear()`：清空当前场景和对象列表。
- `scene.count()`：返回当前场景对象数量。
- `scene.object_at(index)`：返回 Objects 面板中的场景对象记录，含 `Kind`、`Id`、`X`、`Y`。
- `scene.reanim_count()` / `scene.particle_count()` / `scene.trail_count()`：返回对应 ShowCase 对象数量。
- `scene.reanim_at(index)` / `scene.particle_at(index)` / `scene.trail_at(index)`：按创建顺序返回对应对象，越界返回 `nil`。
- `scene.find_reanim(id)` / `scene.find_particle(id)` / `scene.find_trail(id)`：按资源 ID 返回第一个已创建的对应对象，找不到返回 `nil`。
- `scene.find_object(id)`：按资源 ID 返回 Objects 面板中的第一个场景对象记录，找不到返回 `nil`。

### effect

```lua
local body = effect.reanim("sample_reanim", 400, 300)
local fire = effect.particle("fire_burst", 420, 280)
local slash = effect.trail("sword_slash", 0, 0)

effect.log("message")
effect.warn("message")

local v = effect.vector(10, 20)
local m = effect.matrix(1, 0, 0, 1, 400, 300)
local img = effect.image("IMAGE_SPARK")

local has_reanim = effect.reanim_exists("sample_reanim")
local has_particle = effect.particle_exists("fire_burst")
local has_trail = effect.trail_exists("sword_slash")
local has_image = effect.image_exists("IMAGE_SPARK")

local reanim_id = effect.reanim_id(0)
local particle_id = effect.particle_id(0)
local trail_id = effect.trail_id(0)
local image_id = effect.image_id(0)
```

- `effect.reanim(id, x, y)`：创建 reanim 实例。
- `effect.particle(id, x, y)`：创建 particle 实例。
- `effect.trail(id)` / `effect.trail(id, x, y)`：创建 trail 实例。
- `effect.log(message)`：写入日志。
- `effect.warn(message)`：写入 warning 日志。
- `effect.vector(x, y)`：创建向量对象。
- `effect.matrix(m11, m12, m21, m22, x, y)`：创建 2D 矩阵对象。
- `effect.image(id)`：返回图片资源信息对象，找不到返回 `nil`。
- `effect.reanim_exists(id)` / `effect.particle_exists(id)` / `effect.trail_exists(id)`：检查当前项目中是否存在对应资源。
- `effect.image_exists(id)`：检查当前项目中是否存在对应图片资源。
- `effect.reanim_count()` / `effect.particle_count()` / `effect.trail_count()` / `effect.image_count()`：返回当前项目对应资源数量。
- `effect.reanim_id(index)` / `effect.particle_id(index)` / `effect.trail_id(index)` / `effect.image_id(index)`：按资源 ID 字母顺序返回对应资源 ID，越界返回 `nil`。

`id` 必须存在于当前项目对应资源列表中，否则脚本会失败并在日志面板显示错误。

## 绘图 API

`draw(g)` 中的 `g` 是当前帧绘图上下文。

```lua
g:reset()
g:set_color(255, 255, 255)
g:set_color(255, 128, 64, 180)
g:set_draw_mode("normal")
g:set_draw_mode("additive")
g:set_translation(20, 10)
g:translate(5, 0)
g:fill_rect(0, 0, 160, 32)
g:set_clip_rect(0, 0, 800, 600)
g:clear_clip_rect()
```

属性：

- `g.trans_x`
- `g.trans_y`
- `g.mode`：`"normal"` 或 `"additive"`。

方法：

- `set_color(r, g, b)` / `set_color(r, g, b, a)`
- `set_draw_mode(mode)`
- `set_translation(x, y)`
- `translate(x, y)`
- `fill_rect(x, y, width, height)`
- `set_clip_rect(x, y, width, height)`
- `clear_clip_rect()`
- `reset()`

颜色通道范围为 `0` 到 `255`。

## Reanimation API

创建：

```lua
local body = effect.reanim("sample_reanim", 400, 300)
```

常用属性：

- `id_`
- `type`
- `pos_x`, `pos_y`
- `anim_time`, `anim_rate`
- `loop_type`
- `dead`
- `frame_start`, `frame_count`, `frame_base_pose`
- `loop_count`
- `is_attachment`
- `render_order`
- `extra_additive_draw`, `extra_overlay_draw`
- `last_frame_time`
- `filter_effect`
- `track_count`
- `fps`
- `current_frame`

常用方法：

```lua
body:set_position(400, 300)
body:move(400, 300)
body:offset(10, -5)
body:set_matrix(1, 0, 0, 1, 400, 300)
body:set_time(0)
body:set_rate(12)
body:set_scale(1.2)
body:set_scale(1.2, 0.9)
body:set_color(255, 255, 255)
body:set_color(255, 255, 255, 180)
body:set_extra_additive_color(255, 80, 20, 255)
body:set_extra_overlay_color(80, 160, 255, 128)
body:clear_extra_colors()

local x = body:get_x()
local y = body:get_y()
local rate = body:get_rate()
local time = body:get_time()
local dead = body:is_dead()
local matrix = body:matrix()

body:play("anim_idle")
body:play("anim_idle", "loop")
body:play("anim_attack", "once", 18, 4)
body:set_frames_for_layer("anim_idle")
body:start_blend(4)

body:show_only_track("track_name")
body:hide_track("track_name")
body:show_track("track_name")
body:show_prefix("arm")
body:hide_prefix("shadow")
body:set_shake("track_name", 2.0)
body:set_render_group("track_name", 1)
body:set_render_group_prefix("arm", 2)
body:set_render_group_by_prefix("arm", 2)
body:set_base_pose_from_anim("anim_idle")
body:set_truncate_disappearing_frames("track_name", true)

local exists = body:track_exists("track_name")
local image = body:current_image("track_name")
local track = body:track("track_name")
local track0 = body:track_at(0)
local name0 = body:track_name(0)
local index = body:track_index("track_name")
local group = body:get_render_group_by_prefix("arm")
local velocity = body:track_velocity("track_name")
local showing = body:is_track_showing("track_name")
local playing = body:is_anim_playing("anim_idle")
local hit = body:should_trigger_timed_event(0.5)
local range = body:frame_range("anim_idle")
local frame_time = body:frame_time()
local transform = body:current_transform("track_name")
local raw_transform = body:transform("track_name", 0)
local raw_transform_by_index = body:transform_at(0, 0)
local raw_image = body:image_at("track_name", 0)
local raw_text = body:text_at("track_name", 0)
local track_matrix = body:track_matrix("track_name")
local attach_matrix = body:attachment_overlay_matrix("track_name")
local base_matrix = body:track_base_pose_matrix("track_name")

body:set_frame_range(range.start_frame, range.frame_count)
body:set_image_override("track_name", "IMAGE_SPARK")
local override_image = body:image_override_id("track_name")
body:clear_image_override("track_name")

body:update()
body:draw(g)
body:draw_track(g, "track_name")
body:draw_track_at(g, 0)
body:draw_group(g, 1)
body:die()
```

`frame_range(trackName)` 返回对象属性：

- `start_frame`
- `frame_count`
- `end_frame`

`frame_time()` 返回对象属性：

- `fraction`
- `frame_before`
- `frame_after`

`current_transform(trackName)` / `current_transform_at(index)` 返回当前轨道变换快照：

- `x`, `y`
- `skew_x`, `skew_y`
- `scale_x`, `scale_y`
- `frame`
- `alpha`
- `image`
- `font`
- `text`
- `has_image`
- `has_font`
- `has_text`
- `is_blank`

可用方法：

- `position()`

`transform(trackName, frameIndex)` / `transform_at(trackIndex, frameIndex)` 返回指定轨道、指定帧的原始变换快照，属性同上。`image_at(trackName, frameIndex)` 和 `text_at(trackName, frameIndex)` 是读取原始帧图片和文本的简写。

`matrix()`、`track_matrix(trackName)`、`attachment_overlay_matrix(trackName)`、`track_base_pose_matrix(trackName)` 返回矩阵对象：

- `m11`, `m12`
- `m21`, `m22`
- `x`, `y`

矩阵对象还提供 `transform_x(pointX, pointY)`、`transform_y(pointX, pointY)`、`transform(pointX, pointY)`、`translate(offsetX, offsetY)`、`scale(scale)` 和 `scale(scaleX, scaleY)`。

循环类型字符串：

- `loop`
- `once`, `play_once`, `playonce`
- `once_hold`, `hold`, `play_once_and_hold`, `playonceandhold`
- `loop_full`, `loop_full_last_frame`
- `once_full`, `play_once_full_last_frame`
- `hold_full`, `play_once_full_last_frame_and_hold`

挂接资源：

```lua
local body = effect.reanim("sample_reanim", 400, 300)
local fire = effect.particle("fire_burst", 0, 0)
local slash = effect.trail("sword_slash", 0, 0)

body:attach_particle("attacher_fire", fire, 0, 0)
body:attach_trail("attacher_slash", slash, 0, 0)
body:detach("attacher_fire")
```

可用方法：

- `attach_reanim(trackName, child[, offsetX, offsetY])`
- `attach_to_another_reanimation(parent, trackName)`
- `attach_particle(trackName, child[, offsetX, offsetY])`
- `attach_trail(trackName, child[, offsetX, offsetY])`
- `detach(trackName)`
- `attachment(trackName)`
- `find_sub_reanim(reanimationType)`

## Reanimation Track API

```lua
local track = body:track("track_name")
if track ~= nil then
    track:set_color(255, 180, 80)
    track:set_shake(1.5)
    track:hide()
    track:show()
end
```

属性：

- `name`
- `index`
- `transform_count`
- `blend_counter`, `blend_time`
- `shake_override`, `shake_x`, `shake_y`
- `render_group`
- `ignore_clip_rect`
- `truncate_disappearing_frames`
- `ignore_color_override`
- `ignore_extra_additive_color`
- `is_attacher`
- `color_red`, `color_green`, `color_blue`, `color_alpha`
- `image_override_id`

方法：

- `has_attachment()`
- `attachment()`
- `set_color(r, g, b[, a])`
- `set_shake(amount)`
- `set_render_group(renderGroup)`
- `set_truncate(truncate)`
- `show()`
- `hide()`
- `detach()`
- `current_image()`
- `set_image_override(imageId)`
- `clear_image_override()`
- `transform_at(frameIndex)`
- `image_at(frameIndex)`
- `text_at(frameIndex)`
- `is_showing()`
- `velocity()`
- `matrix()`
- `attachment_overlay_matrix()`
- `base_pose_matrix()`
- `current_transform()`
- `draw(g)`

## 图片、向量和矩阵对象

`effect.image(id)` 返回图片信息：

- `id`
- `width`, `height`
- `cols`, `rows`
- `cel_width`, `cel_height`
- `has_platform_image`

向量对象可通过 `effect.vector(x, y)`、trail normal 或 transform position 获取：

- 属性：`x`、`y`、`length`、`length_squared`
- 方法：`add(x, y)`、`subtract(x, y)`、`scale(value)`、`normalize()`、`dot(x, y)`

矩阵对象可通过 `effect.matrix(m11, m12, m21, m22, x, y)` 或 reanim/track 查询获取：

- 属性：`m11`、`m12`、`m21`、`m22`、`x`、`y`
- 方法：`transform_x(x, y)`、`transform_y(x, y)`、`transform(x, y)`、`translate(x, y)`、`scale(scale)`、`scale(scaleX, scaleY)`

## Particle API

创建：

```lua
local fire = effect.particle("fire_burst", 420, 280)
```

属性：

- `id_`
- `type`
- `dead`
- `is_attachment`
- `render_order`
- `dont_update`
- `emitter_count`
- `emitter_definition_count`

方法：

```lua
local x = fire:get_x()
local y = fire:get_y()
local dead = fire:is_dead()

fire:set_position(420, 280)
fire:move(420, 280)
fire:offset(4, -2)
fire:set_color(255, 160, 80)
fire:set_color(255, 160, 80, 180)
fire:set_scale(0.8)
fire:set_frame(2)
fire:set_extra_additive_draw(true)
fire:set_image_override("IMAGE_SPARK")
fire:clear_image_override()
fire:cross_fade("emitter_name")

fire:set_emitter_color("spark", 255, 255, 128, 255)
fire:set_emitter_scale("spark", 1.4)
fire:set_emitter_frame("spark", 3)
fire:set_emitter_extra_additive_draw("spark", true)
fire:set_emitter_image_override("spark", "IMAGE_SPARK")
fire:clear_emitter_image_override("spark")

local emitter = fire:emitter("spark")
local first = fire:emitter_at(0)
local name = fire:emitter_name(0)
local has_spark = fire:emitter_exists("spark")
local spark_index = fire:emitter_index("spark")
local def_name = fire:emitter_definition_name(0)
local def_image = fire:emitter_definition_image_id(0)

fire:delete_emitter_particles("spark")
fire:delete_all()

fire:update()
fire:draw(g)
fire:die()
```

额外方法：

- `set_image_override(imageId)`
- `clear_image_override()`
- `set_emitter_image_override(emitterName, imageId)`
- `clear_emitter_image_override(emitterName)`
- `emitter_definition_name(index)`
- `emitter_definition_image_id(index)`

## Particle Emitter API

```lua
local emitter = fire:emitter_at(0)
if emitter ~= nil then
    emitter:set_scale(1.2)
    emitter:set_color(255, 220, 120)
end
```

属性：

- `name`
- `spawn_accum`
- `x`, `y`
- `particles_spawned`
- `system_age`, `system_duration`
- `system_time`, `last_system_time`
- `dead`
- `extra_additive_draw`
- `scale_override`
- `frame`
- `particle_count`
- `cross_fade_countdown`
- `image_id`
- `image_override_id`
- `image_col`, `image_row`
- `image_frames`
- `animated`
- `emitter_type`
- `on_duration`
- `particle_flags`
- `particle_field_count`
- `system_field_count`

方法：

- `is_dead()`
- `center_x()`
- `center_y()`
- `update()`
- `draw(g)`
- `set_position(x, y)`
- `move(x, y)`
- `set_color(r, g, b[, a])`
- `set_scale(scale)`
- `set_frame(frame)`
- `set_image_override(imageId)`
- `clear_image_override()`
- `has_image_override()`
- `spawn([count])`
- `delete_all()`
- `delete_non_cross_fading()`
- `particle(index)`
- `particle_index(particle)`
- `delete_particle(particle)`
- `cross_fade_particle(particle, toEmitter)`
- `cross_fade_particle_to_name(particle, emitterName)`
- `cross_fade_to(toEmitter)`

## Particle Instance API

```lua
local p = emitter:particle(0)
if p ~= nil then
    p:set_position(400, 300)
    p:set_velocity(0, -2)
end
```

属性：

- `duration`
- `age`
- `time`, `last_time`
- `animation_time`
- `x`, `y`
- `velocity_x`, `velocity_y`
- `image_frame`
- `spin`, `spin_velocity`
- `cross_fade_duration`

方法：

- `pos_x()`
- `pos_y()`
- `velocity_x_value()`
- `velocity_y_value()`
- `is_cross_fading()`
- `emitter()`
- `set_position(x, y)`
- `set_velocity(x, y)`
- `offset_velocity(x, y)`
- `set_age(age)`
- `set_duration(duration)`
- `move(x, y)`
- `offset(x, y)`

## Trail API

创建：

```lua
local slash = effect.trail("sword_slash", 0, 0)
```

属性：

- `id_`
- `pos_x`, `pos_y`
- `dead`
- `render_order`
- `age`
- `duration`
- `is_attachment`
- `point_count`
- `image_id`
- `image_override_id`
- `trail_flags`
- `max_points`
- `min_point_distance`

方法：

```lua
local x = slash:get_x()
local y = slash:get_y()
local dead = slash:is_dead()

slash:set_position(0, 0)
slash:move(0, 0)
slash:offset(5, -2)
slash:add_point(330, 320)
slash:add_point(470, 280)
slash:clear_points()
slash:set_manual_points(true)
slash:set_age(0)
slash:set_duration(90)
slash:set_color(255, 255, 255, 200)
slash:set_image_override("IMAGE_SLASH")
slash:clear_image_override()
local normal = slash:normal_at(0)

local p = slash:point(0)
if p ~= nil then
    p:set_position(330, 320)
end

slash:update()
slash:update_attached_path()
slash:update_standalone_path()
slash:draw(g)
slash:die()
```

额外方法：

- `set_image_override(imageId)`
- `clear_image_override()`
- `has_image_override()`

说明：

- 手动调用 `add_point` 或 `clear_points` 后，trail 会进入手动点模式。
- 没有定义 `update`，且 trail 没有进入手动点模式、也没有作为 attachment 时，场景会生成一条默认移动路径用于预览。
- 定义了 `update` 后，如需继续使用默认挂接路径或独立预览路径，需要手动调用 `update_attached_path()` 或 `update_standalone_path()`。

## Trail Point API

属性：

- `index`
- `x`, `y`

方法：

- `set_position(x, y)`
- `offset(x, y)`
- `normal()`

## Attachment API

Attachment 可通过 reanim track 或 reanim 的 `attachment(trackName)` 获取。

```lua
local att = body:attachment("attacher_fire")
if att ~= nil then
    att:set_color(255, 180, 120)
    att:set_scale(1.1)
end
```

属性：

- `effect_count`
- `dead`

方法：

- `is_dead()`
- `is_empty()`
- `is_full()`
- `update()`
- `draw(g)`
- `die()`
- `detach()`
- `effect(index)`
- `cross_fade(emitterName)`
- `set_position(x, y)`
- `set_matrix(m11, m12, m21, m22, x, y)`
- `set_color(r, g, b[, a])`
- `set_scale(scale)`

`effect(index)` 返回 Attachment 内部效果记录：

- 属性：`index`、`type`、`dont_draw_if_parent_hidden`、`dont_propagate_color`
- 方法：`is_valid()`、`offset_matrix()`、`set_offset(x, y)`、`set_offset_matrix(m11, m12, m21, m22, x, y)`

## 完整示例

下面的脚本创建一个 reanim、一个粒子和一条手动 trail，并在每帧更新位置和颜色。

```lua
scene.clear()

local body = effect.reanim("sample_reanim", 400, 300)
local fire = effect.particle("fire_burst", 420, 280)
local slash = effect.trail("sword_slash", 0, 0)

body.render_order = 0
fire.render_order = 1
slash.render_order = 2

local t = 0

function update(dt, elapsed, frame)
    t = elapsed

    local x = 400 + math.sin(t * 2.0) * 40
    local y = 300 + math.cos(t * 1.5) * 12

    body:set_position(x, y)
    body:set_scale(1.0 + math.sin(t * 3.0) * 0.08)
    body:update()

    fire:set_position(x + 20, y - 20)
    fire:set_scale(0.8 + math.sin(t * 4.0) * 0.2)
    fire:update()

    slash:clear_points()
    slash:add_point(x - 70, y + 30)
    slash:add_point(x + 70, y - 20 + math.sin(t * 5.0) * 35)
    slash:set_color(255, 255, 255, 200)
    slash:update()
end

function draw(g, elapsed, frame)
    g:reset()
    body:draw(g)
    fire:draw(g)
    slash:draw(g)
end

effect.log("showcase initialized")
```

## 调试建议

- 运行脚本后查看 `Logs` 面板。语法错误、运行时错误和 `effect.log` 输出都会出现在这里。
- 查看 `Objects` 面板确认脚本实际创建了哪些对象。
- 如果资源创建失败，优先检查 `Id` 是否和项目清单中的资源 ID 完全一致。
- 如果定义了 `draw` 但画面为空，确认是否调用了对象的 `draw(g)`。
- 如果定义了 `update` 但动画不动，确认是否调用了对象的 `update()`。
- 如果访问 track 或 emitter，先判断返回值是否为 `nil`。
