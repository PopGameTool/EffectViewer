# EffectViewer Lua API

本文档描述当前 Lua ShowCase 脚本 API。脚本运行于 MoonSharp 软沙盒内，所有场景对象都通过显式注册过的 Lua 包装类型访问。

约定：

- 坐标、颜色、时间和 ID 在 Lua 中都是 number。颜色范围为 0-255。
- `nil` 可用于清除 Image 覆写，或在标注为“可为 nil”的参数中表示不修改该字段。
- `*_id` 为运行时对象 ID，只在当前脚本运行创建的场景内有效。
- 返回多个值的函数按 Lua 多返回值使用，例如 `r, g, b, a = g:get_color()`。

## 生命周期

```lua
local context = {}

function context:update(dt, elapsed, frame)
end

function context:draw(g, elapsed, frame)
end

scene.regist(context)
```

`scene.regist(context)` 注册脚本上下文。`context:update(dt, elapsed, frame)` 每个逻辑帧调用，`dt` 为本次步进秒数，`elapsed` 为累计秒数，`frame` 为累计帧号。`context:draw(g, elapsed, frame)` 每次渲染调用，`g` 为 Graphics API。

未注册 context 时，运行时自动更新并绘制所有非 attachment 对象。脚本入口必须通过 `scene.regist(context)` 注册；不再提供 `effect` 全局对象，也不再读取全局 `update` / `draw` 函数。

## scene

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `scene.global_attachment` | GlobalAttachment | 全局 attachment API，同全局变量 `global_attachment`。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `scene.regist(context)` | Lua table | 无 | 注册包含 `update`/`draw` 的上下文。 |
| `scene.clear()` | 无 | 无 | 清空当前场景对象。 |
| `scene.reanim(id, x, y)` | 资源 ID，坐标 | Reanimation | 创建 reanim 实例。 |
| `scene.particle_system(id, x, y)` | 资源 ID，坐标 | TodParticleSystem | 创建粒子系统。 |
| `scene.trail(id, x, y)` | 资源 ID，坐标 | Trail | 创建 trail 实例。 |
| `scene.log(message)` | string | 无 | 写入普通日志。 |
| `scene.warn(message)` | string | 无 | 写入 warning 日志。 |
| `scene.vector2(x, y)` | number, number | Vector2 | 创建二维向量。 |
| `scene.vector3(x, y, z)` | number, number, number | Vector3 | 创建三维向量。 |
| `scene.matrix3x3()` | 无 | Matrix3x3 | 创建单位矩阵。 |
| `scene.matrix3x3(m11, m12, m13, m21, m22, m23, m31, m32, m33)` | 9 个 number | Matrix3x3 | 创建 3x3 矩阵。 |
| `scene.get_image(id)` | 图片资源 ID | Image 或 nil | 获取并缓存图片包装对象。 |
| `scene.get_font(id)` | 字体资源 ID | Font 或 nil | 获取并缓存字体包装对象。 |
| `scene.resource_exist(id, type)` | 资源 ID, `"reanim"`/`"particle"`/`"trail"`/`"image"`/`"font"` | bool | 判断项目资源是否存在。 |
| `scene.tri_vertex(pos_x, pos_y, pos_z, r, g, b, a, coordinate_x, coordinate_y)` | 顶点坐标、颜色、UV | TriVertex | 创建纹理三角形顶点。 |
| `scene.reanim_get_id(reanim)` | Reanimation | number | 获取 reanim 运行时 ID。 |
| `scene.particle_system_get_id(particle)` | TodParticleSystem | number | 获取粒子系统运行时 ID。 |
| `scene.emitter_get_id(emitter)` | TodParticleEmitter | number | 获取发射器运行时 ID。 |
| `scene.particle_get_id(particle_instance)` | TodParticle | number | 获取粒子实例运行时 ID。 |
| `scene.attachment_get_id(attachment)` | Attachment | number | 获取 attachment 运行时 ID。 |
| `scene.trail_get_id(trail)` | Trail | number | 获取 trail 运行时 ID。 |
| `scene.reanim_get(id)` / `scene.reanim_try_to_get(id)` | number | Reanimation 或 nil | 按运行时 ID 获取 reanim。 |
| `scene.particle_system_get(id)` / `scene.particle_system_try_to_get(id)` | number | TodParticleSystem 或 nil | 按运行时 ID 获取粒子系统。 |
| `scene.emitter_get(id)` / `scene.emitter_try_to_get(id)` | number | TodParticleEmitter 或 nil | 按运行时 ID 获取发射器。 |
| `scene.particle_get(id)` / `scene.particle_try_to_get(id)` | number | TodParticle 或 nil | 按运行时 ID 获取粒子实例。 |
| `scene.attachment_get(id)` / `scene.attachment_try_to_get(id)` | number | Attachment 或 nil | 按运行时 ID 获取 attachment。 |
| `scene.trail_get(id)` / `scene.trail_try_to_get(id)` | number | Trail 或 nil | 按运行时 ID 获取 trail。 |

## Graphics

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `g.trans_x` / `g.trans_y` | number | 绘制平移。 |
| `g.draw_mode` | string | `"normal"` 或 `"additive"`。 |
| `g.colorize_images` | bool | 是否用当前颜色着色图片。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `g:get_color()` | 无 | `r, g, b, a` | 获取当前颜色。 |
| `g:set_color(r, g, b, a)` | number 或 nil | Graphics | 设置颜色，nil 表示保留原通道。 |
| `g:set_clip_rect(x, y, width, height)` | number | Graphics | 设置裁剪矩形。 |
| `g:get_clip_rect()` | 无 | `x, y, width, height` | 获取裁剪矩形。 |
| `g:clear_clip_rect()` | 无 | Graphics | 清除裁剪限制。 |
| `g:translate(x, y)` | number | Graphics | 累加平移。 |
| `g:fill_rect(x, y, width, height)` | number | Graphics | 填充矩形。 |
| `g:draw_image(img, x, y)` | Image, 坐标 | Graphics | 以左上角坐标绘制图片当前 cel。 |
| `g:draw_image_matrix(img, matrix, src_x, src_y, src_width, src_height)` | Image, Matrix3x3, 源矩形 | Graphics | 使用矩阵绘制图片源矩形。 |
| `g:draw_string(font, msg, x, y)` | Font, string, 坐标 | Graphics | 使用当前 Graphics 颜色在指定位置绘制文字。 |
| `g:draw_string_matrix(font, msg, matrix3x3)` | Font, string, Matrix3x3 | Graphics | 使用当前 Graphics 颜色通过矩阵绘制文字。 |
| `g:draw_triangles_tex(img, v0, v1, v2, ...)` | Image, TriVertex 三个一组 | Graphics | 绘制纹理三角形。 |
| `g:reset()` | 无 | Graphics | 重置平移、颜色、模式和裁剪。 |

## ReanimatorTrackInstance

由 `reanim:get_track_instance(index)`、`reanim:get_track_instance_by_name(track_name)` 返回。

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `blend_counter` | int | 剩余混合帧数。 |
| `blend_time` | int | 混合总帧数。 |
| `shake_override` | number | 震动幅度。 |
| `shake_x` / `shake_y` | number | 当前震动偏移。 |
| `attachment_id` | number | 当前轨道 attachment ID。 |
| `image_override` | Image 或 nil | 轨道图片覆写。 |
| `render_group` | int | 绘制分组，`-1` 通常表示隐藏。 |
| `ignore_clip_rect` | bool | 绘制时忽略裁剪。 |
| `truncate_disappearing_frames` | bool | 截断消失帧补间。 |
| `ignore_color_override` | bool | 忽略 reanim 颜色覆写。 |
| `ignore_extra_additive_color` | bool | 忽略额外叠加色。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `instance:get_track_color()` | 无 | `r, g, b, a` | 读取轨道颜色。 |
| `instance:set_track_color(r, g, b, a)` | number 或 nil | instance | 设置轨道颜色，nil 表示保留原通道。 |
| `instance:get_blend_transform()` | 无 | `tx, ty, kx, ky, sx, sy, f, a, img, font, text` | 读取混合源变换。 |
| `instance:set_blend_transform(tx, ty, kx, ky, sx, sy, f, a, img, font, text)` | number/string 或 nil | instance | 设置混合源变换，nil 表示保留原字段。 |

## Reanimation

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `reanimation_type` | string | reanim 类型/资源 ID。 |
| `anim_time` | number | 当前循环时间，通常 0-1。 |
| `anim_rate` | number | 播放速度，单位为原始帧率倍率。 |
| `loop_type` | string | `Loop`、`PlayOnce`、`PlayOnceAndHold` 等。 |
| `dead` | bool | 是否死亡。 |
| `frame_start` / `frame_count` | int | 当前播放帧段。 |
| `frame_base_pose` | int | attachment 基准帧。 |
| `loop_count` | int | 已循环次数。 |
| `is_attachment` | bool | 是否作为 attachment 绘制。 |
| `enable_extra_additive_draw` | bool | 是否启用额外叠加色。 |
| `enable_extra_overlay_draw` | bool | 是否启用额外覆盖色。 |
| `last_frame_time` | number | 上一帧循环时间。 |
| `filter_effect` | string | 滤镜类型。 |
| `track_instance_count` | int，只读 | 轨道实例数量。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_track_instance(index)` | int | ReanimatorTrackInstance 或 nil | 按索引取轨道实例。 |
| `set_track_instance(index, instance)` | int, instance | Reanimation | 用另一个实例快照覆盖轨道实例。 |
| `get_overlay_matrix([matrix])` | Matrix3x3 可选 | Matrix3x3 | 获取或写入 overlay matrix。 |
| `set_overlay_matrix(matrix)` | Matrix3x3 | Reanimation | 设置 overlay matrix。 |
| `get_color_override()` / `set_color_override(r, g, b, a)` | number 或 nil | 多返回值 / Reanimation | 读取/设置主颜色。 |
| `get_extra_additive_color()` / `set_extra_additive_color(r, g, b, a)` | number 或 nil | 多返回值 / Reanimation | 读取/设置额外叠加色。 |
| `get_extra_overlay_color()` / `set_extra_overlay_color(r, g, b, a)` | number 或 nil | 多返回值 / Reanimation | 读取/设置额外覆盖色。 |
| `reanimation_initialize(x, y, type)` | 坐标, string | Reanimation | 重新设置位置和类型。 |
| `reanimation_die()` / `reanimation_delete()` | 无 | Reanimation | 标记死亡 / 删除内部轨道数组。 |
| `update()` / `draw(g)` | Graphics 可选 | Reanimation | 更新或绘制。 |
| `draw_render_group(g, group)` | Graphics, int | Reanimation | 绘制指定 render group。 |
| `draw_track(g, index, group)` | Graphics, int, int | bool | 绘制指定轨道。 |
| `get_current_transform(index)` | int | `tx, ty, kx, ky, sx, sy, f, a, img, font, text` | 读取当前轨道变换。 |
| `get_transform_at_time(index, fraction, before, after)` | int, number, int, int | 同上 | 按指定帧时间计算变换。 |
| `get_frame_time()` | 无 | `fraction, before, after` | 获取当前帧插值信息。 |
| `find_track_index(track_name)` | string | int | 查找轨道索引，失败返回 -1。 |
| `attach_to_another_reanimation(parent, track_name)` | Reanimation, string | Reanimation | 把当前 reanim 挂到另一个 reanim 轨道。 |
| `get_attachment_overlay_matrix(index, [matrix])` | int, Matrix3x3 可选 | Matrix3x3 | 获取 attachment overlay matrix。 |
| `set_frames_for_layer(track_name)` | string | Reanimation | 按轨道非空白帧设置播放帧段。 |
| `matrix_from_transform(...)` | 6 或 11 个 transform 字段，最后可传 matrix | Matrix3x3 | 根据 transform 生成矩阵。 |
| `track_exists(track_name)` | string | bool | 判断轨道是否存在。 |
| `start_blend(blend_time)` | int | Reanimation | 以当前姿态开始混合。 |
| `set_shake_override(track_name, amount)` | string, number | Reanimation | 设置轨道震动。 |
| `set_position(x, y)` | number | Reanimation | 设置位置。 |
| `override_scale(sx, sy)` | number | Reanimation | 覆写缩放。 |
| `get_track_velocity(track_name)` | string | number | 获取轨道瞬时横向速度。 |
| `set_image_override(track_name, img)` | string, Image 或 nil | Reanimation | 设置轨道图片覆写。 |
| `get_image_override(track_name)` | string | Image 或 nil | 获取轨道图片覆写。 |
| `show_only_track(track_name)` | string | Reanimation | 只显示指定轨道。 |
| `get_track_matrix(index, [matrix])` | int, Matrix3x3 可选 | Matrix3x3 | 获取轨道绘制矩阵。 |
| `assign_render_group_to_track(track_name, group)` | string, int | Reanimation | 设置单轨道 render group。 |
| `assign_render_group_to_prefix(prefix, group)` | string, int | Reanimation | 设置前缀匹配轨道 render group。 |
| `get_current_frame()` | 无 | int | 获取当前整数帧。 |
| `get_render_group_by_prefix(prefix)` | string | int | 获取首个前缀匹配轨道的 render group。 |
| `propogate_color_to_attachments()` | 无 | Reanimation | 把颜色传播给 attachment。 |
| `should_trigger_timed_event(event_time)` | number | bool | 判断事件时间是否在本帧跨过。 |
| `get_current_track_image(track_name)` | string | string 或 nil | 获取轨道当前图片名。 |
| `attach_particle_to_track(track_name, particle, x, y)` | string, TodParticleSystem, 坐标 | `attachment, attach_effect_index` | 挂接粒子，失败返回 nil。 |
| `get_track_base_pos_matrix(index, [matrix])` | int, Matrix3x3 可选 | Matrix3x3 | 获取轨道基准姿态矩阵。 |
| `is_track_showing(track_name)` | string | bool | 当前帧是否显示该轨道。 |
| `set_truncate_disappearing_frames(track_name, enabled)` | string 或 nil, bool | Reanimation | 设置消失帧截断，可传 nil 作用于全部轨道。 |
| `play_reanim(track_name, loop_type, blend_time, anim_rate)` | string, string, int, number | Reanimation | 播放指定层动作。 |
| `get_track_instance_by_name(track_name)` | string | ReanimatorTrackInstance 或 nil | 按名称获取轨道实例。 |
| `set_track_instance_by_name(track_name, instance)` | string, instance | Reanimation | 按名称覆盖轨道实例。 |
| `get_frames_for_layer(track_name)` | string | `frame_start, frame_count` | 读取指定层有效帧段。 |
| `update_attacher_track(track_index)` | int | Reanimation | 更新 attacher 轨道。 |
| `parse_attacher_track(f, text)` 或 `parse_attacher_track(tx, ty, kx, ky, sx, sy, f, a, img, font, text)` | transform 字段 | `reanim_name, track_name, anim_rate, loop_type, reanim_type` | 解析 attacher 文本。 |
| `attacher_synch_walk_speed(track_index, attach_reanim)` | int, Reanimation | Reanimation | 同步 walk 速度。 |
| `is_anim_playing(track_name)` | string | bool | 判断动作是否正在播放。 |
| `set_base_pos_from_anim(track_name)` | string | Reanimation | 从动画设置基准姿态。 |
| `reanim_blt_matrix(g, img, matrix, clip_x, clip_y, clip_w, clip_h, r, g, b, a, draw_mode, src_x, src_y, src_w, src_h)` | 绘图参数 | Reanimation | 低层矩阵绘图。 |
| `find_sub_reanim(reanim_type)` | string | Reanimation 或 nil | 在 attachment 树中查找子 reanim。 |

## TodParticleSystem

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `effect_type` | string | 粒子特效类型/资源 ID。 |
| `dead` | bool | 是否死亡。 |
| `is_attachment` | bool | 是否作为 attachment。 |
| `dont_update` | bool | 是否暂停更新。 |
| `emitter_count` | int，只读 | 当前发射器数量。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_emitter_id(index)` | int | number | 按发射器列表顺序获取 ID。 |
| `tod_particle_initialize(x, y, effect_type)` | 坐标, string | TodParticleSystem | 设置位置和类型。 |
| `particle_system_die()` | 无 | TodParticleSystem | 杀死粒子系统。 |
| `update()` / `draw(g)` | Graphics 可选 | TodParticleSystem | 更新或绘制。 |
| `system_move(x, y)` | number | TodParticleSystem | 移动系统中心。 |
| `override_color(emitter_name, r, g, b, a)` | string 或 nil, number | TodParticleSystem | 覆写发射器颜色，emitter_name 为 nil 时作用于全部。 |
| `override_extra_additive_draw(emitter_name, enabled)` | string 或 nil, bool | TodParticleSystem | 覆写额外叠加绘制开关。 |
| `override_image(emitter_name, img)` | string 或 nil, Image 或 nil | TodParticleSystem | 覆写或清除图片。 |
| `override_frame(emitter_name, frame)` | string 或 nil, int | TodParticleSystem | 覆写图片帧。 |
| `override_scale(emitter_name, scale)` | string 或 nil, number | TodParticleSystem | 覆写缩放。 |
| `cross_fade(emitter_name)` | string | TodParticleSystem | 交叉淡化到指定发射器定义。 |
| `find_emitter_by_name(emitter_name)` | string | TodParticleEmitter 或 nil | 查找当前发射器。 |

## TodParticleEmitter

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `particle_system` | TodParticleSystem，只读 | 所属粒子系统。 |
| `spawn_accum` | number | 发射累计量。 |
| `system_center_x` / `system_center_y` | number | 系统中心。 |
| `particles_spawned` | int | 已发射粒子数。 |
| `system_age` / `system_duration` | int | 发射器年龄和持续帧数。 |
| `system_time_value` / `system_last_time_value` | number | 当前和上一系统时间值。 |
| `dead` | bool | 是否死亡。 |
| `extra_additive_draw_override` | bool | 额外叠加绘制覆写。 |
| `scale_override` | number | 缩放覆写。 |
| `image_override` | Image 或 nil | 图片覆写。 |
| `cross_fade_emitter_id` | number | 交叉淡化目标发射器 ID。 |
| `emitter_cross_fade_count_down` | int | 发射器交叉淡化剩余帧。 |
| `frame_override` | int | 帧覆写。 |
| `particle_count` | int，只读 | 当前粒子数量。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_particle_id(index)` | int | number | 按粒子列表顺序获取粒子 ID。 |
| `get_track_interp(index)` / `set_track_interp(index, value)` | int, number | number / emitter | 读写系统轨道随机插值。 |
| `get_system_field_interp(index)` / `set_system_field_interp(index, v1, v2)` | int, number, number | `v1, v2` / emitter | 读写系统场插值。 |
| `update()` / `draw(g)` | Graphics 可选 | emitter | 更新或绘制发射器。 |
| `system_move(x, y)` | number | emitter | 移动系统中心。 |
| `get_color_override()` / `set_color_override(r, g, b, a)` | number 或 nil | 多返回值 / emitter | 读写颜色覆写。 |
| `get_render_params(particle_instance)` | TodParticle | ParticleRenderParams 或 nil | 计算粒子渲染参数。 |
| `draw_particle(g, particle_instance)` | Graphics, TodParticle | emitter | 绘制单个粒子。 |
| `update_spawning()` | 无 | emitter | 执行发射逻辑。 |
| `update_particle(particle_instance)` | TodParticle | bool | 更新单个粒子，返回是否仍存活。 |
| `spawn_particle(index, spawn_count)` | int, int | TodParticle 或 nil | 生成一个粒子。 |
| `cross_fade_particle(particle_instance, to_emitter)` | TodParticle, emitter | bool | 让单个粒子交叉淡化。 |
| `cross_fade_emitter(to_emitter)` | emitter | emitter | 整个发射器交叉淡化。 |
| `cross_fade_particle_to_name(particle_instance, emitter_name)` | TodParticle, string | bool | 交叉淡化到指定名称。 |
| `delete_all()` | 无 | emitter | 删除全部粒子。 |
| `delete_particle(particle_instance)` | TodParticle | emitter | 删除指定粒子。 |
| `delete_non_cross_fading()` | 无 | emitter | 删除非交叉淡化粒子。 |

## TodParticle

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `particle_emitter` | TodParticleEmitter，只读 | 所属发射器。 |
| `particle_duration` / `particle_age` | int | 粒子持续帧数和年龄。 |
| `particle_time_value` / `particle_last_time_value` | number | 当前和上一粒子时间值。 |
| `animation_time_value` | number | 动画循环时间值。 |
| `velocity_x` / `velocity_y` | number | 速度。 |
| `position_x` / `position_y` | number | 位置。 |
| `image_frame` | int | 当前图片帧。 |
| `spin_position` / `spin_velocity` | number | 旋转角和旋转速度。 |
| `cross_fade_particle_id` | number | 交叉淡化来源粒子 ID。 |
| `cross_fade_duration` | int | 交叉淡化持续帧数。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_particle_interp(index)` / `set_particle_interp(index, value)` | int, number | number / particle | 读写粒子轨道随机插值。 |
| `get_particle_field_interp(index)` / `set_particle_field_interp(index, v1, v2)` | int, number, number | `v1, v2` / particle | 读写粒子场插值。 |

## ParticleRenderParams

字段均只读：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `red_is_set` / `green_is_set` / `blue_is_set` / `alpha_is_set` | bool | 对应颜色通道是否由定义或覆写设置。 |
| `particle_scale_is_set` / `particle_stretch_is_set` | bool | 缩放/拉伸是否设置。 |
| `spin_position_is_set` | bool | 旋转角是否设置。 |
| `position_is_set` | bool | 位置是否由定义影响。 |
| `red` / `green` / `blue` / `alpha` | number | 计算后的颜色通道。 |
| `particle_scale` / `particle_stretch` | number | 计算后的缩放和拉伸。 |
| `spin_position` | number | 计算后的旋转角。 |
| `pos_x` / `pos_y` | number | 计算后的绘制位置。 |

## Trail

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `num_trail_points` | int，只读 | 当前轨迹点数量。 |
| `dead` | bool | 是否死亡。 |
| `trail_age` / `trail_duration` | int | trail 年龄和持续帧数。 |
| `trail_center_x` / `trail_center_y` | number | trail 中心。 |
| `is_attachment` | bool | 是否作为 attachment。 |
| `image_override` | Image 或 nil | 图片覆写。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_trail_point(index)` / `set_trail_point(index, x, y)` | int, 坐标 | `x, y` / Trail | 读写轨迹点。 |
| `get_trail_interp(index)` / `set_trail_interp(index, value)` | int, number | number / Trail | 读写 trail 随机插值。 |
| `get_color_override()` / `set_color_override(r, g, b, a)` | number 或 nil | 多返回值 / Trail | 读写颜色覆写。 |
| `update()` / `draw(g)` | Graphics 可选 | Trail | 更新或绘制。 |
| `add_point(x, y)` | number | Trail | 添加轨迹点。 |
| `get_normal_at_point(index)` | int | `ok, x, y` | 获取轨迹点法线。 |

## Attachment

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `num_effects` | int，只读 | 附着的效果数量。 |
| `dead` | bool | 是否死亡。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_effect(index)` / `set_effect(index, attach_effect)` | int, AttachEffect | AttachEffect / Attachment | 读写指定附着效果。 |
| `update()` | 无 | Attachment | 更新附着效果。 |
| `set_matrix(matrix)` | Matrix3x3 | Attachment | 用矩阵更新附着效果位置。 |
| `override_color(r, g, b, a)` | number，a 可 nil | Attachment | 覆写颜色。 |
| `override_scale(scale)` | number | Attachment | 覆写缩放。 |
| `draw(g, parent_hidden)` | Graphics, bool | Attachment | 绘制 attachment。 |
| `attachment_die()` | 无 | Attachment | 杀死内部效果。 |
| `detach()` | 无 | Attachment | 分离内部效果。 |
| `cross_fade(cross_fade_name)` | string | Attachment | 对粒子效果执行交叉淡化。 |
| `propogate_color(r, g, b, a, enable_additive, ar, ag, ab, aa, enable_overlay, or, og, ob, oa)` | 颜色和开关 | Attachment | 传播主色、叠加色和覆盖色。 |
| `set_position(x, y)` | number | Attachment | 设置 attachment 位置。 |

## AttachEffect

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `effect_id` | number | 被附着对象 ID。 |
| `effect_type` | string | `Particle`、`Trail`、`Reanim`、`Attachment` 或 `Other`。 |
| `dont_draw_if_parent_hidden` | bool | 父轨道隐藏时是否不绘制。 |
| `dont_propogate_color` | bool | 是否阻止颜色传播。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_offset([matrix])` | Matrix3x3 可选 | Matrix3x3 | 获取或写入 offset matrix。 |
| `set_offset(matrix)` | Matrix3x3 | AttachEffect | 设置 offset matrix。 |

## GlobalAttachment

全局变量 `global_attachment` 和 `scene.global_attachment` 指向同一个对象。所有函数隐式使用当前场景的 EffectSystem。

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `attachment_update_and_move(attachment_id, x, y)` | ID, 坐标 | attachment_id | 更新并移动 attachment；失效时返回 0。 |
| `attachment_update_and_set_matrix(attachment_id, matrix)` | ID, Matrix3x3 | attachment_id | 更新并设置矩阵。 |
| `attachment_override_color(attachment_id, r, g, b, a)` | ID, 颜色 | 无 | 覆写颜色，a 可 nil，默认 255。 |
| `attachment_override_scale(attachment_id, scale)` | ID, number | 无 | 覆写缩放。 |
| `attachment_cross_fade(attachment_id, cross_fade_name)` | ID, string | 无 | 对粒子 attachment 交叉淡化。 |
| `attachment_draw(attachment_id, g, parent_hidden)` | ID, Graphics, bool | 无 | 绘制 attachment。 |
| `attachment_die(attachment_id)` | ID | attachment_id | 杀死 attachment，返回新 ID，通常为 0。 |
| `attachment_detach(attachment_id)` | ID | attachment_id | 分离 attachment，返回新 ID，通常为 0。 |
| `attach_reanim(attachment_id, reanim, offset_x, offset_y)` | ID, Reanimation, 坐标 | `attachment_id, attachment, attach_effect_index` | 添加 reanim attachment。 |
| `attach_particle(attachment_id, particle, offset_x, offset_y)` | ID, TodParticleSystem, 坐标 | 同上 | 添加 particle attachment。 |
| `attach_trail(attachment_id, trail, offset_x, offset_y)` | ID, Trail, 坐标 | 同上 | 添加 trail attachment。 |
| `attachment_detach_cross_fade_particle_type(attachment_id, particle_effect, cross_fade_name)` | ID, string, string 或 nil | 无 | 分离指定粒子类型并交叉淡化；cross_fade_name 为 nil 时直接 die。 |
| `attachment_propogate_color(attachment_id, r, g, b, a, enable_additive, ar, ag, ab, aa, enable_overlay, or, og, ob, oa)` | ID, 颜色和开关 | 无 | 传播颜色。 |
| `find_reanim_attachment(attachment_id)` | ID | Reanimation 或 nil | 查找首个 reanim attachment。 |
| `find_trail_attachment(attachment_id)` | ID | Trail 或 nil | 查找首个 trail attachment。 |
| `find_first_attachment(attachment_id)` | ID | `attachment, attach_effect_index` | 返回第一个 AttachEffect 所在 attachment 和索引。 |
| `attachment_reanim_type_die(attachment_id, reanim_type)` | ID, string | 无 | 杀死指定类型 reanim attachment。 |
| `is_full_of_attachments(attachment_id)` | ID | bool | 判断 attachment 是否已满。 |
| `create_effect_attachment(attachment_id, effect_type, data_id, offset_x, offset_y)` | ID, string, ID, 坐标 | `attachment_id, attachment, attach_effect_index` | 低层创建 AttachEffect。 |

如果底层返回 NullRef，`attachment` 和 `attach_effect_index` 返回 nil。

## Image

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `id` | string | 图片资源 ID。 |
| `width` / `height` | int | 图片像素尺寸。 |
| `num_cols` / `num_rows` | int | cel 列数和行数。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `get_cel_width()` | 无 | int | 返回单帧宽度。 |
| `get_cel_height()` | 无 | int | 返回单帧高度。 |

## Font

由 `scene.get_font(id)` 返回。绘制时传给 `g:draw_string` 或 `g:draw_string_matrix`。

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `id` | string | 字体资源 ID。 |
| `is_true_type` | bool | 是否为 TrueType 字体。 |
| `supports_chinese` | bool | TrueType 字体是否支持中文字符集。 |
| `is_initialized` | bool | 底层字体资源是否已初始化。 |
| `true_type_font_size` | int | TrueType 字体字号；位图字体为 0。 |
| `true_type_border_size` | int | TrueType 字体描边尺寸；位图字体为 0。 |
| `true_type_glyph_count` | int | 已生成的 TrueType 字形数量。 |
| `ascent` | number | 字体上升高度。 |
| `ascent_padding` | number | 上升高度补偿。 |
| `height` | number | 字体行高。 |
| `line_spacing_offset` | number | 行距偏移。 |
| `default_point_size` | int | 默认字号。 |
| `point_size` | int | 当前字号；可写，主要影响位图字体。 |
| `scale` | number | 当前缩放；可写，主要影响位图字体。 |

函数：

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `string_width(text)` | string | int | 计算文本宽度。 |
| `char_width(text)` | string | int | 计算首个字符宽度；空字符串返回 0。 |
| `char_width_kern(text, previous)` | string, string | int | 计算首个字符在前一字符后的字距宽度。 |

## Matrix3x3

行向量约定，平移位于 `m31`、`m32`。

字段：`m11`, `m12`, `m13`, `m21`, `m22`, `m23`, `m31`, `m32`, `m33`，均为 number。

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `set(m11, m12, m13, m21, m22, m23, m31, m32, m33)` | 9 个 number | Matrix3x3 | 覆盖矩阵。 |
| `set_identity()` | 无 | Matrix3x3 | 设为单位矩阵。 |
| `copy_from(other)` | Matrix3x3 | Matrix3x3 | 从另一个矩阵复制。 |
| `clone()` | 无 | Matrix3x3 | 克隆矩阵。 |
| `translation(x, y)` | number | Matrix3x3 | 累加平移量。 |
| `multiply(left, right)` | Matrix3x3, Matrix3x3 | Matrix3x3 | 对自身赋值为 `right * left`。 |
| `transpose(source)` | Matrix3x3 | Matrix3x3 | 对自身赋值为转置矩阵。 |
| `inverse(source)` | Matrix3x3 | Matrix3x3 | 对自身赋值为逆矩阵，失败时为单位矩阵。 |
| `extract_scale()` | 无 | `sx, sy` | 提取缩放。 |
| `transform_point(x, y)` | number | `tx, ty` | 变换点。 |
| `transform_vector(vec2)` | Vector2 | Vector2 | 变换二维向量并返回新对象。 |

## Vector2

字段：`x`, `y`, `length`（只读）, `length_squared`（只读）。

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `set(x, y)` | number | Vector2 | 设置坐标。 |
| `copy_from(other)` | Vector2 | Vector2 | 复制。 |
| `clone()` | 无 | Vector2 | 克隆。 |
| `add(x, y)` / `subtract(x, y)` | number | Vector2 | 加/减。 |
| `scale(value)` | number | Vector2 | 缩放。 |
| `dot(x, y)` | number | number | 点乘。 |
| `normalize_safe()` | 无 | Vector2 | 安全归一化，零向量保持为 0。 |
| `get_perp()` | 无 | Vector2 | 返回垂直向量。 |

## Vector3

字段：`x`, `y`, `z`, `length`（只读）, `length_squared`（只读）。

| 函数 | 参数 | 返回值 | 作用 |
| --- | --- | --- | --- |
| `set(x, y, z)` | number | Vector3 | 设置坐标。 |
| `copy_from(other)` | Vector3 | Vector3 | 复制。 |
| `clone()` | 无 | Vector3 | 克隆。 |
| `add(x, y, z)` / `subtract(x, y, z)` | number | Vector3 | 加/减。 |
| `scale(value)` | number | Vector3 | 缩放。 |
| `dot(x, y, z)` | number | number | 点乘。 |
| `normalize_safe()` | 无 | Vector3 | 安全归一化。 |

## TriVertex

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `pos_x`, `pos_y`, `pos_z` | number | 顶点位置。 |
| `red`, `green`, `blue`, `alpha` | number | 顶点颜色。 |
| `coordinate_x`, `coordinate_y` | number | 纹理坐标。 |
