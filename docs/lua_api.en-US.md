# EffectViewer Lua API

This document describes the current MoonSharp ShowCase Lua API. The API is intentionally exposed through explicit Lua wrapper types. Legacy `effect.*` globals and global `update` / `draw` callbacks are not available.

Conventions:

- Coordinates, colors, time values, and runtime IDs are Lua numbers. Color channels use `0-255`.
- `nil` can clear Image overrides or mean “leave this field unchanged” where noted.
- Runtime IDs are valid only in the current script-created scene.
- Functions returning multiple values are used as normal Lua multi-return functions, for example `r, g, b, a = g:get_color()`.

## Lifecycle

```lua
local context = {}

function context:update(dt, elapsed, frame)
end

function context:draw(g, elapsed, frame)
end

scene.regist(context)
```

`scene.regist(context)` registers a Lua table as the script context. `context:update(dt, elapsed, frame)` runs on each logic step. `dt` is the step duration in seconds, `elapsed` is accumulated seconds, and `frame` is the accumulated frame number. `context:draw(g, elapsed, frame)` runs on each render pass with a Graphics object.

If no context is registered, EffectViewer automatically updates and draws all non-attachment objects.

## scene

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `scene.global_attachment` | GlobalAttachment | Attachment helper API. Same object as the global `global_attachment`. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `scene.regist(context)` | Lua table | none | Registers the script context table. |
| `scene.clear()` | none | none | Clears current scene objects. |
| `scene.reanim(id, x, y)` | resource ID, coordinates | Reanimation | Creates a reanimation instance. |
| `scene.particle_system(id, x, y)` | resource ID, coordinates | TodParticleSystem | Creates a particle system. |
| `scene.trail(id, x, y)` | resource ID, coordinates | Trail | Creates a trail instance. |
| `scene.log(message)` | string | none | Adds a normal log entry. |
| `scene.warn(message)` | string | none | Adds a warning log entry. |
| `scene.vector2(x, y)` | number, number | Vector2 | Creates a 2D vector. |
| `scene.vector3(x, y, z)` | number, number, number | Vector3 | Creates a 3D vector. |
| `scene.matrix3x3()` | none | Matrix3x3 | Creates an identity matrix. |
| `scene.matrix3x3(m11, m12, m13, m21, m22, m23, m31, m32, m33)` | 9 numbers | Matrix3x3 | Creates a 3x3 matrix. |
| `scene.get_image(id)` | image resource ID | Image or nil | Gets and caches an image wrapper. |
| `scene.resource_exist(id, type)` | ID, `"reanim"` / `"particle"` / `"trail"` / `"image"` | bool | Checks whether a project resource exists. |
| `scene.tri_vertex(pos_x, pos_y, pos_z, r, g, b, a, coordinate_x, coordinate_y)` | position, color, UV | TriVertex | Creates a textured triangle vertex. |
| `scene.reanim_get_id(reanim)` | Reanimation | number | Gets a reanimation runtime ID. |
| `scene.particle_system_get_id(particle)` | TodParticleSystem | number | Gets a particle system runtime ID. |
| `scene.emitter_get_id(emitter)` | TodParticleEmitter | number | Gets an emitter runtime ID. |
| `scene.particle_get_id(particle_instance)` | TodParticle | number | Gets a particle instance runtime ID. |
| `scene.attachment_get_id(attachment)` | Attachment | number | Gets an attachment runtime ID. |
| `scene.trail_get_id(trail)` | Trail | number | Gets a trail runtime ID. |
| `scene.reanim_get(id)` / `scene.reanim_try_to_get(id)` | number | Reanimation or nil | Gets a reanimation by runtime ID. |
| `scene.particle_system_get(id)` / `scene.particle_system_try_to_get(id)` | number | TodParticleSystem or nil | Gets a particle system by runtime ID. |
| `scene.emitter_get(id)` / `scene.emitter_try_to_get(id)` | number | TodParticleEmitter or nil | Gets an emitter by runtime ID. |
| `scene.particle_get(id)` / `scene.particle_try_to_get(id)` | number | TodParticle or nil | Gets a particle instance by runtime ID. |
| `scene.attachment_get(id)` / `scene.attachment_try_to_get(id)` | number | Attachment or nil | Gets an attachment by runtime ID. |
| `scene.trail_get(id)` / `scene.trail_try_to_get(id)` | number | Trail or nil | Gets a trail by runtime ID. |

## Graphics

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `g.trans_x` / `g.trans_y` | number | Draw translation. |
| `g.draw_mode` | string | `"normal"` or `"additive"`. |
| `g.colorize_images` | bool | Whether current color tints images. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `g:get_color()` | none | `r, g, b, a` | Gets current color. |
| `g:set_color(r, g, b, a)` | number or nil | Graphics | Sets color; nil leaves a channel unchanged. |
| `g:set_clip_rect(x, y, width, height)` | numbers | Graphics | Sets clipping rectangle. |
| `g:get_clip_rect()` | none | `x, y, width, height` | Gets clipping rectangle. |
| `g:clear_clip_rect()` | none | Graphics | Clears clipping limits. |
| `g:translate(x, y)` | numbers | Graphics | Adds draw translation. |
| `g:fill_rect(x, y, width, height)` | numbers | Graphics | Fills a rectangle. |
| `g:draw_image(img, x, y)` | Image, coordinates | Graphics | Draws the current image cel at top-left coordinates. |
| `g:draw_image_matrix(img, matrix, src_x, src_y, src_width, src_height)` | Image, Matrix3x3, source rect | Graphics | Draws an image source rectangle through a matrix. |
| `g:draw_triangles_tex(img, v0, v1, v2, ...)` | Image, TriVertex groups of three | Graphics | Draws textured triangles. |
| `g:reset()` | none | Graphics | Resets translation, color, draw mode, and clip rect. |

## ReanimatorTrackInstance

Returned by `reanim:get_track_instance(index)` and `reanim:get_track_instance_by_name(track_name)`.

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `blend_counter` | int | Remaining blend frames. |
| `blend_time` | int | Total blend frames. |
| `shake_override` | number | Shake amount. |
| `shake_x` / `shake_y` | number | Current shake offset. |
| `attachment_id` | number | Current track attachment ID. |
| `image_override` | Image or nil | Track image override. |
| `render_group` | int | Render group; `-1` normally means hidden. |
| `ignore_clip_rect` | bool | Ignore clipping while drawing. |
| `truncate_disappearing_frames` | bool | Cut disappearing-frame interpolation. |
| `ignore_color_override` | bool | Ignore the parent reanimation color override. |
| `ignore_extra_additive_color` | bool | Ignore extra additive color. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `instance:get_track_color()` | none | `r, g, b, a` | Reads track color. |
| `instance:set_track_color(r, g, b, a)` | number or nil | instance | Sets track color; nil leaves a channel unchanged. |
| `instance:get_blend_transform()` | none | `tx, ty, kx, ky, sx, sy, f, a, img, font, text` | Reads blend source transform. |
| `instance:set_blend_transform(tx, ty, kx, ky, sx, sy, f, a, img, font, text)` | number/string or nil | instance | Sets blend source transform; nil leaves a field unchanged. |

## Reanimation

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `reanimation_type` | string | Reanimation type / resource ID. |
| `anim_time` | number | Current loop time, usually `0-1`. |
| `anim_rate` | number | Playback speed. |
| `loop_type` | string | `Loop`, `PlayOnce`, `PlayOnceAndHold`, etc. |
| `dead` | bool | Whether this instance is dead. |
| `frame_start` / `frame_count` | int | Active frame range. |
| `frame_base_pose` | int | Attachment base-pose frame. |
| `loop_count` | int | Completed loop count. |
| `is_attachment` | bool | Whether this instance is drawn as an attachment. |
| `enable_extra_additive_draw` | bool | Enables extra additive color. |
| `enable_extra_overlay_draw` | bool | Enables extra overlay color. |
| `last_frame_time` | number | Previous loop time. |
| `filter_effect` | string | Filter effect type. |
| `track_instance_count` | int, read-only | Track instance count. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_track_instance(index)` / `get_track_instance_by_name(track_name)` | int / string | ReanimatorTrackInstance or nil | Gets a track instance. |
| `set_track_instance(index, instance)` / `set_track_instance_by_name(track_name, instance)` | index/name, instance | Reanimation | Copies another track instance snapshot into this track. |
| `get_overlay_matrix([matrix])` | optional Matrix3x3 | Matrix3x3 | Gets or writes the overlay matrix. |
| `set_overlay_matrix(matrix)` | Matrix3x3 | Reanimation | Sets the overlay matrix. |
| `get_color_override()` / `set_color_override(r, g, b, a)` | number or nil | multi-return / Reanimation | Reads or writes main color. |
| `get_extra_additive_color()` / `set_extra_additive_color(r, g, b, a)` | number or nil | multi-return / Reanimation | Reads or writes extra additive color. |
| `get_extra_overlay_color()` / `set_extra_overlay_color(r, g, b, a)` | number or nil | multi-return / Reanimation | Reads or writes extra overlay color. |
| `reanimation_initialize(x, y, type)` | coordinates, string | Reanimation | Resets position and type. |
| `reanimation_die()` / `reanimation_delete()` | none | Reanimation | Marks dead / deletes internal track array. |
| `update()` / `draw(g)` | optional Graphics | Reanimation | Updates or draws. |
| `draw_render_group(g, group)` | Graphics, int | Reanimation | Draws one render group. |
| `draw_track(g, index, group)` | Graphics, int, int | bool | Draws one track. |
| `get_current_transform(index)` | int | `tx, ty, kx, ky, sx, sy, f, a, img, font, text` | Gets current track transform. |
| `get_transform_at_time(index, fraction, before, after)` | int, number, int, int | same as above | Computes a transform for a frame time. |
| `get_frame_time()` | none | `fraction, before, after` | Gets current frame interpolation. |
| `find_track_index(track_name)` | string | int | Finds a track index; returns `-1` on failure. |
| `attach_to_another_reanimation(parent, track_name)` | Reanimation, string | Reanimation | Attaches this reanimation to another reanimation track. |
| `get_attachment_overlay_matrix(index, [matrix])` | int, optional Matrix3x3 | Matrix3x3 | Gets attachment overlay matrix. |
| `set_frames_for_layer(track_name)` | string | Reanimation | Sets active frames from non-blank frames on a track. |
| `matrix_from_transform(...)` | 6 or 11 transform fields, optional matrix as last arg | Matrix3x3 | Builds a matrix from transform fields. |
| `track_exists(track_name)` | string | bool | Checks whether a track exists. |
| `start_blend(blend_time)` | int | Reanimation | Starts blending from current pose. |
| `set_shake_override(track_name, amount)` | string, number | Reanimation | Sets track shake. |
| `set_position(x, y)` | numbers | Reanimation | Sets position. |
| `override_scale(sx, sy)` | numbers | Reanimation | Overrides scale. |
| `get_track_velocity(track_name)` | string | number | Gets current horizontal track velocity. |
| `set_image_override(track_name, img)` | string, Image or nil | Reanimation | Sets or clears track image override. |
| `get_image_override(track_name)` | string | Image or nil | Gets track image override. |
| `show_only_track(track_name)` | string | Reanimation | Shows only one track. |
| `get_track_matrix(index, [matrix])` | int, optional Matrix3x3 | Matrix3x3 | Gets track draw matrix. |
| `assign_render_group_to_track(track_name, group)` | string, int | Reanimation | Sets one track render group. |
| `assign_render_group_to_prefix(prefix, group)` | string, int | Reanimation | Sets render group for matching track prefixes. |
| `get_current_frame()` | none | int | Gets current integer frame. |
| `get_render_group_by_prefix(prefix)` | string | int | Gets first matching prefix render group. |
| `propogate_color_to_attachments()` | none | Reanimation | Propagates color to attachments. |
| `should_trigger_timed_event(event_time)` | number | bool | Checks whether an event time was crossed this frame. |
| `get_current_track_image(track_name)` | string | string or nil | Gets current track image name. |
| `attach_particle_to_track(track_name, particle, x, y)` | string, TodParticleSystem, coordinates | `attachment, attach_effect_index` | Attaches a particle system to a track. |
| `get_track_base_pos_matrix(index, [matrix])` | int, optional Matrix3x3 | Matrix3x3 | Gets track base-pose matrix. |
| `is_track_showing(track_name)` | string | bool | Checks whether this track is showing on the current frame. |
| `set_truncate_disappearing_frames(track_name, enabled)` | string or nil, bool | Reanimation | Sets disappearing-frame truncation; nil track name applies to all tracks. |
| `play_reanim(track_name, loop_type, blend_time, anim_rate)` | string, string, int, number | Reanimation | Plays an action layer. |
| `get_frames_for_layer(track_name)` | string | `frame_start, frame_count` | Gets valid frame range for a layer. |
| `update_attacher_track(track_index)` | int | Reanimation | Updates an attacher track. |
| `parse_attacher_track(f, text)` or `parse_attacher_track(tx, ty, kx, ky, sx, sy, f, a, img, font, text)` | transform fields | `reanim_name, track_name, anim_rate, loop_type, reanim_type` | Parses attacher text. |
| `attacher_synch_walk_speed(track_index, attach_reanim)` | int, Reanimation | Reanimation | Synchronizes walk speed. |
| `is_anim_playing(track_name)` | string | bool | Checks whether an action is playing. |
| `set_base_pos_from_anim(track_name)` | string | Reanimation | Sets base pose from animation. |
| `reanim_blt_matrix(g, img, matrix, clip_x, clip_y, clip_w, clip_h, r, g, b, a, draw_mode, src_x, src_y, src_w, src_h)` | draw args | Reanimation | Low-level matrix image draw. |
| `find_sub_reanim(reanim_type)` | string | Reanimation or nil | Finds a child reanimation in the attachment tree. |

## TodParticleSystem

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `effect_type` | string | Particle effect type / resource ID. |
| `dead` | bool | Whether the system is dead. |
| `is_attachment` | bool | Whether it is drawn as an attachment. |
| `dont_update` | bool | Pauses updates. |
| `emitter_count` | int, read-only | Current emitter count. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_emitter_id(index)` | int | number | Gets an emitter ID by emitter list order. |
| `tod_particle_initialize(x, y, effect_type)` | coordinates, string | TodParticleSystem | Sets position and type. |
| `particle_system_die()` | none | TodParticleSystem | Kills the particle system. |
| `update()` / `draw(g)` | optional Graphics | TodParticleSystem | Updates or draws. |
| `system_move(x, y)` | numbers | TodParticleSystem | Moves system center. |
| `override_color(emitter_name, r, g, b, a)` | string or nil, color | TodParticleSystem | Overrides emitter color; nil emitter name applies to all. |
| `override_extra_additive_draw(emitter_name, enabled)` | string or nil, bool | TodParticleSystem | Overrides additive drawing. |
| `override_image(emitter_name, img)` | string or nil, Image or nil | TodParticleSystem | Sets or clears image override. |
| `override_frame(emitter_name, frame)` | string or nil, int | TodParticleSystem | Overrides image frame. |
| `override_scale(emitter_name, scale)` | string or nil, number | TodParticleSystem | Overrides scale. |
| `cross_fade(emitter_name)` | string | TodParticleSystem | Cross-fades to an emitter definition. |
| `find_emitter_by_name(emitter_name)` | string | TodParticleEmitter or nil | Finds a current emitter. |

## TodParticleEmitter

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `particle_system` | TodParticleSystem, read-only | Owning particle system. |
| `spawn_accum` | number | Spawn accumulator. |
| `system_center_x` / `system_center_y` | number | System center. |
| `particles_spawned` | int | Total spawned particle count. |
| `system_age` / `system_duration` | int | Emitter age and duration. |
| `system_time_value` / `system_last_time_value` | number | Current and previous system time. |
| `dead` | bool | Whether the emitter is dead. |
| `extra_additive_draw_override` | bool | Additive draw override. |
| `scale_override` | number | Scale override. |
| `image_override` | Image or nil | Image override. |
| `cross_fade_emitter_id` | number | Cross-fade target emitter ID. |
| `emitter_cross_fade_count_down` | int | Remaining emitter cross-fade frames. |
| `frame_override` | int | Frame override. |
| `particle_count` | int, read-only | Current particle count. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_particle_id(index)` | int | number | Gets a particle ID by list order. |
| `get_track_interp(index)` / `set_track_interp(index, value)` | int, number | number / emitter | Reads or writes system-track random interpolation. |
| `get_system_field_interp(index)` / `set_system_field_interp(index, v1, v2)` | int, numbers | `v1, v2` / emitter | Reads or writes system-field interpolation. |
| `update()` / `draw(g)` | optional Graphics | emitter | Updates or draws. |
| `system_move(x, y)` | numbers | emitter | Moves system center. |
| `get_color_override()` / `set_color_override(r, g, b, a)` | number or nil | multi-return / emitter | Reads or writes color override. |
| `get_render_params(particle_instance)` | TodParticle | ParticleRenderParams or nil | Computes particle render parameters. |
| `draw_particle(g, particle_instance)` | Graphics, TodParticle | emitter | Draws one particle. |
| `update_spawning()` | none | emitter | Runs spawning logic. |
| `update_particle(particle_instance)` | TodParticle | bool | Updates one particle and returns whether it remains alive. |
| `spawn_particle(index, spawn_count)` | int, int | TodParticle or nil | Spawns one particle. |
| `cross_fade_particle(particle_instance, to_emitter)` | TodParticle, emitter | bool | Cross-fades one particle. |
| `cross_fade_emitter(to_emitter)` | emitter | emitter | Cross-fades the whole emitter. |
| `cross_fade_particle_to_name(particle_instance, emitter_name)` | TodParticle, string | bool | Cross-fades to a named emitter. |
| `delete_all()` | none | emitter | Deletes all particles. |
| `delete_particle(particle_instance)` | TodParticle | emitter | Deletes one particle. |
| `delete_non_cross_fading()` | none | emitter | Deletes non-cross-fading particles. |

## TodParticle

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `particle_emitter` | TodParticleEmitter, read-only | Owning emitter. |
| `particle_duration` / `particle_age` | int | Particle duration and age. |
| `particle_time_value` / `particle_last_time_value` | number | Current and previous particle time. |
| `animation_time_value` | number | Animation loop time. |
| `velocity_x` / `velocity_y` | number | Velocity. |
| `position_x` / `position_y` | number | Position. |
| `image_frame` | int | Current image frame. |
| `spin_position` / `spin_velocity` | number | Spin angle and spin velocity. |
| `cross_fade_particle_id` | number | Cross-fade source particle ID. |
| `cross_fade_duration` | int | Cross-fade duration in frames. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_particle_interp(index)` / `set_particle_interp(index, value)` | int, number | number / particle | Reads or writes particle-track random interpolation. |
| `get_particle_field_interp(index)` / `set_particle_field_interp(index, v1, v2)` | int, numbers | `v1, v2` / particle | Reads or writes particle-field interpolation. |

## ParticleRenderParams

Read-only fields:

| Field | Type | Description |
| --- | --- | --- |
| `red_is_set` / `green_is_set` / `blue_is_set` / `alpha_is_set` | bool | Whether each color channel is set by definition or override. |
| `particle_scale_is_set` / `particle_stretch_is_set` | bool | Whether scale/stretch is set. |
| `spin_position_is_set` | bool | Whether spin angle is set. |
| `position_is_set` | bool | Whether position is affected by definition. |
| `red` / `green` / `blue` / `alpha` | number | Computed color channels. |
| `particle_scale` / `particle_stretch` | number | Computed scale and stretch. |
| `spin_position` | number | Computed spin angle. |
| `pos_x` / `pos_y` | number | Computed draw position. |

## Trail

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `num_trail_points` | int, read-only | Current trail point count. |
| `dead` | bool | Whether the trail is dead. |
| `trail_age` / `trail_duration` | int | Trail age and duration. |
| `trail_center_x` / `trail_center_y` | number | Trail center. |
| `is_attachment` | bool | Whether it is drawn as an attachment. |
| `image_override` | Image or nil | Image override. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_trail_point(index)` / `set_trail_point(index, x, y)` | int, coordinates | `x, y` / Trail | Reads or writes one trail point. |
| `get_trail_interp(index)` / `set_trail_interp(index, value)` | int, number | number / Trail | Reads or writes trail random interpolation. |
| `get_color_override()` / `set_color_override(r, g, b, a)` | number or nil | multi-return / Trail | Reads or writes color override. |
| `update()` / `draw(g)` | optional Graphics | Trail | Updates or draws. |
| `add_point(x, y)` | numbers | Trail | Adds a trail point. |
| `get_normal_at_point(index)` | int | `ok, x, y` | Gets a point normal. |

## Attachment

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `num_effects` | int, read-only | Attached effect count. |
| `dead` | bool | Whether the attachment is dead. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_effect(index)` / `set_effect(index, attach_effect)` | int, AttachEffect | AttachEffect / Attachment | Reads or writes an attached effect. |
| `update()` | none | Attachment | Updates attached effects. |
| `set_matrix(matrix)` | Matrix3x3 | Attachment | Updates attached effect positions with a matrix. |
| `override_color(r, g, b, a)` | color, alpha may be nil | Attachment | Overrides color. |
| `override_scale(scale)` | number | Attachment | Overrides scale. |
| `draw(g, parent_hidden)` | Graphics, bool | Attachment | Draws attachment. |
| `attachment_die()` | none | Attachment | Kills inner effects. |
| `detach()` | none | Attachment | Detaches inner effects. |
| `cross_fade(cross_fade_name)` | string | Attachment | Cross-fades particle effects. |
| `propogate_color(r, g, b, a, enable_additive, ar, ag, ab, aa, enable_overlay, or, og, ob, oa)` | colors and flags | Attachment | Propagates main, additive, and overlay color. |
| `set_position(x, y)` | numbers | Attachment | Sets attachment position. |

## AttachEffect

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `effect_id` | number | Attached object ID. |
| `effect_type` | string | `Particle`, `Trail`, `Reanim`, `Attachment`, or `Other`. |
| `dont_draw_if_parent_hidden` | bool | Do not draw if parent track is hidden. |
| `dont_propogate_color` | bool | Prevent color propagation. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_offset([matrix])` | optional Matrix3x3 | Matrix3x3 | Gets or writes the offset matrix. |
| `set_offset(matrix)` | Matrix3x3 | AttachEffect | Sets offset matrix. |

## GlobalAttachment

The global `global_attachment` and `scene.global_attachment` reference the same object. All functions implicitly use the current scene EffectSystem.

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `attachment_update_and_move(attachment_id, x, y)` | ID, coordinates | attachment_id | Updates and moves an attachment; returns 0 if invalidated. |
| `attachment_update_and_set_matrix(attachment_id, matrix)` | ID, Matrix3x3 | attachment_id | Updates and sets matrix. |
| `attachment_override_color(attachment_id, r, g, b, a)` | ID, color | none | Overrides color; alpha may be nil and defaults to 255. |
| `attachment_override_scale(attachment_id, scale)` | ID, number | none | Overrides scale. |
| `attachment_cross_fade(attachment_id, cross_fade_name)` | ID, string | none | Cross-fades particle attachments. |
| `attachment_draw(attachment_id, g, parent_hidden)` | ID, Graphics, bool | none | Draws attachment. |
| `attachment_die(attachment_id)` | ID | attachment_id | Kills attachment and returns the new ID, usually 0. |
| `attachment_detach(attachment_id)` | ID | attachment_id | Detaches attachment and returns the new ID, usually 0. |
| `attach_reanim(attachment_id, reanim, offset_x, offset_y)` | ID, Reanimation, coordinates | `attachment_id, attachment, attach_effect_index` | Adds a reanimation attachment. |
| `attach_particle(attachment_id, particle, offset_x, offset_y)` | ID, TodParticleSystem, coordinates | same | Adds a particle attachment. |
| `attach_trail(attachment_id, trail, offset_x, offset_y)` | ID, Trail, coordinates | same | Adds a trail attachment. |
| `attachment_detach_cross_fade_particle_type(attachment_id, particle_effect, cross_fade_name)` | ID, string, string or nil | none | Detaches a particle type and cross-fades it; nil cross-fade name kills directly. |
| `attachment_propogate_color(attachment_id, r, g, b, a, enable_additive, ar, ag, ab, aa, enable_overlay, or, og, ob, oa)` | ID, colors and flags | none | Propagates colors. |
| `find_reanim_attachment(attachment_id)` | ID | Reanimation or nil | Finds the first reanimation attachment. |
| `find_trail_attachment(attachment_id)` | ID | Trail or nil | Finds the first trail attachment. |
| `find_first_attachment(attachment_id)` | ID | `attachment, attach_effect_index` | Returns the attachment and index for the first AttachEffect. |
| `attachment_reanim_type_die(attachment_id, reanim_type)` | ID, string | none | Kills reanimation attachments of a type. |
| `is_full_of_attachments(attachment_id)` | ID | bool | Checks whether attachment is full. |
| `create_effect_attachment(attachment_id, effect_type, data_id, offset_x, offset_y)` | ID, string, ID, coordinates | `attachment_id, attachment, attach_effect_index` | Low-level AttachEffect creation. |

If the underlying operation returns a NullRef, `attachment` and `attach_effect_index` are nil.

## Image

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | Image resource ID. |
| `width` / `height` | int | Image pixel size. |
| `num_cols` / `num_rows` | int | Cel columns and rows. |

Functions:

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `get_cel_width()` | none | int | Gets cel width. |
| `get_cel_height()` | none | int | Gets cel height. |

## Matrix3x3

Uses row-vector convention; translation is stored in `m31` and `m32`.

Fields: `m11`, `m12`, `m13`, `m21`, `m22`, `m23`, `m31`, `m32`, `m33`; all are numbers.

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `set(m11, m12, m13, m21, m22, m23, m31, m32, m33)` | 9 numbers | Matrix3x3 | Overwrites matrix fields. |
| `set_identity()` | none | Matrix3x3 | Sets identity matrix. |
| `copy_from(other)` | Matrix3x3 | Matrix3x3 | Copies from another matrix. |
| `clone()` | none | Matrix3x3 | Clones this matrix. |
| `translation(x, y)` | numbers | Matrix3x3 | Adds translation. |
| `multiply(left, right)` | Matrix3x3, Matrix3x3 | Matrix3x3 | Sets this matrix to `right * left`. |
| `transpose(source)` | Matrix3x3 | Matrix3x3 | Sets this matrix to source transpose. |
| `inverse(source)` | Matrix3x3 | Matrix3x3 | Sets this matrix to inverse; identity on failure. |
| `extract_scale()` | none | `sx, sy` | Extracts scale. |
| `transform_point(x, y)` | numbers | `tx, ty` | Transforms a point. |
| `transform_vector(vec2)` | Vector2 | Vector2 | Transforms a 2D vector and returns a new object. |

## Vector2

Fields: `x`, `y`, `length` read-only, `length_squared` read-only.

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `set(x, y)` | numbers | Vector2 | Sets coordinates. |
| `copy_from(other)` | Vector2 | Vector2 | Copies from another vector. |
| `clone()` | none | Vector2 | Clones this vector. |
| `add(x, y)` / `subtract(x, y)` | numbers | Vector2 | Adds or subtracts. |
| `scale(value)` | number | Vector2 | Scales vector. |
| `dot(x, y)` | numbers | number | Dot product. |
| `normalize_safe()` | none | Vector2 | Normalizes safely; zero vector stays zero. |
| `get_perp()` | none | Vector2 | Returns a perpendicular vector. |

## Vector3

Fields: `x`, `y`, `z`, `length` read-only, `length_squared` read-only.

| Function | Parameters | Returns | Description |
| --- | --- | --- | --- |
| `set(x, y, z)` | numbers | Vector3 | Sets coordinates. |
| `copy_from(other)` | Vector3 | Vector3 | Copies from another vector. |
| `clone()` | none | Vector3 | Clones this vector. |
| `add(x, y, z)` / `subtract(x, y, z)` | numbers | Vector3 | Adds or subtracts. |
| `scale(value)` | number | Vector3 | Scales vector. |
| `dot(x, y, z)` | numbers | number | Dot product. |
| `normalize_safe()` | none | Vector3 | Normalizes safely. |

## TriVertex

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `pos_x`, `pos_y`, `pos_z` | number | Vertex position. |
| `red`, `green`, `blue`, `alpha` | number | Vertex color. |
| `coordinate_x`, `coordinate_y` | number | Texture coordinates. |
