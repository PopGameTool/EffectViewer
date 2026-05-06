using System;
using System.Globalization;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Filter;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Reanim.Attachment;
using EffectViewer.Runtime.Lua;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseReanimation
    {
        private readonly ShowcaseScene _scene;

        internal ShowcaseReanimation(ShowcaseScene scene, string id, Reanimation reanimation)
        {
            _scene = scene;
            id_ = id;
            Reanimation = reanimation;
        }

        internal Reanimation Reanimation { get; }

        public string id_ { get; }
        public string type
        {
            get => Reanimation.mReanimationType;
            set => Reanimation.mReanimationType = value;
        }

        public string reanimation_type
        {
            get => type;
            set => type = value;
        }

        public double pos_x
        {
            get => Reanimation.mOverlayMatrix.M41;
            set => Reanimation.SetPosition((float)value, Reanimation.mOverlayMatrix.M42);
        }

        public double pos_y
        {
            get => Reanimation.mOverlayMatrix.M42;
            set => Reanimation.SetPosition(Reanimation.mOverlayMatrix.M41, (float)value);
        }

        public double anim_time
        {
            get => Reanimation.mAnimTime;
            set => Reanimation.mAnimTime = (float)value;
        }

        public double anim_rate
        {
            get => Reanimation.mAnimRate;
            set => Reanimation.mAnimRate = (float)value;
        }

        public string loop_type
        {
            get => Reanimation.mLoopType.ToString();
            set => Reanimation.mLoopType = ParseLoopType(value);
        }

        public bool dead
        {
            get => Reanimation.mDead;
            set => Reanimation.mDead = value;
        }

        public int frame_start
        {
            get => Reanimation.mFrameStart;
            set => Reanimation.mFrameStart = Math.Max(0, value);
        }

        public int frame_count
        {
            get => Reanimation.mFrameCount;
            set => Reanimation.mFrameCount = Math.Max(0, value);
        }

        public int frame_base_pose
        {
            get => Reanimation.mFrameBasePose;
            set => Reanimation.mFrameBasePose = value;
        }

        public int loop_count
        {
            get => Reanimation.mLoopCount;
            set => Reanimation.mLoopCount = Math.Max(0, value);
        }

        public bool is_attachment
        {
            get => Reanimation.mIsAttachment;
            set => Reanimation.mIsAttachment = value;
        }

        public int render_order
        {
            get => Reanimation.mRenderOrder;
            set => Reanimation.mRenderOrder = value;
        }

        public bool extra_additive_draw
        {
            get => Reanimation.mEnableExtraAdditiveDraw;
            set => Reanimation.mEnableExtraAdditiveDraw = value;
        }

        public bool enable_extra_additive_draw
        {
            get => extra_additive_draw;
            set => extra_additive_draw = value;
        }

        public bool extra_overlay_draw
        {
            get => Reanimation.mEnableExtraOverlayDraw;
            set => Reanimation.mEnableExtraOverlayDraw = value;
        }

        public bool enable_extra_overlay_draw
        {
            get => extra_overlay_draw;
            set => extra_overlay_draw = value;
        }

        public double last_frame_time
        {
            get => Reanimation.mLastFrameTime;
            set => Reanimation.mLastFrameTime = (float)value;
        }

        public string filter_effect
        {
            get => Reanimation.mFilterEffect.ToString();
            set => Reanimation.mFilterEffect = ParseEnum(value, FilterEffectType.None);
        }

        public int track_count => Reanimation.mDefinition?.mTrackCount ?? 0;
        public int track_instance_count => Reanimation.mTrackInstances?.Length ?? 0;
        public double fps => Reanimation.mDefinition?.mFPS ?? 0;
        public int get_current_frame() => track_count == 0 || Reanimation.mFrameCount == 0 ? 0 : Reanimation.GetCurrentFrame();
        public double get_x() => Reanimation.mOverlayMatrix.M41;
        public double get_y() => Reanimation.mOverlayMatrix.M42;
        public double get_rate() => Reanimation.mAnimRate;
        public double get_time() => Reanimation.mAnimTime;
        public bool is_dead() => Reanimation.mDead;
        public ShowcaseMatrix matrix() => new(Reanimation.mOverlayMatrix);

        public ShowcaseReanimationTrack get_track_instance(int index)
        {
            return track_at(index);
        }

        public ShowcaseReanimation set_track_instance(int index, ShowcaseReanimationTrack instance)
        {
            if (index >= 0 && index < track_instance_count && instance is not null)
            {
                Reanimation.mTrackInstances[index] = instance.Snapshot();
            }

            return this;
        }

        public ShowcaseMatrix get_overlay_matrix()
        {
            return new ShowcaseMatrix(Reanimation.mOverlayMatrix);
        }

        public ShowcaseMatrix get_overlay_matrix(ShowcaseMatrix matrix)
        {
            if (matrix is null)
            {
                return get_overlay_matrix();
            }

            matrix.copy_from(new ShowcaseMatrix(Reanimation.mOverlayMatrix));
            return matrix;
        }

        public ShowcaseReanimation set_overlay_matrix(ShowcaseMatrix matrix)
        {
            if (matrix is not null)
            {
                Reanimation.mOverlayMatrix = matrix.ToMatrix4x4();
            }

            return this;
        }

        public DynValue get_color_override()
        {
            return LuaApiUtility.ColorTuple(Reanimation.mColorOverride);
        }

        public ShowcaseReanimation set_color_override(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            Reanimation.mColorOverride = LuaApiUtility.MergeColor(Reanimation.mColorOverride, red, green, blue, alpha);
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public DynValue get_extra_additive_color()
        {
            return LuaApiUtility.ColorTuple(Reanimation.mExtraAdditiveColor);
        }

        public ShowcaseReanimation set_extra_additive_color(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            Reanimation.mExtraAdditiveColor = LuaApiUtility.MergeColor(Reanimation.mExtraAdditiveColor, red, green, blue, alpha);
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public DynValue get_extra_overlay_color()
        {
            return LuaApiUtility.ColorTuple(Reanimation.mExtraOverlayColor);
        }

        public ShowcaseReanimation set_extra_overlay_color(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            Reanimation.mExtraOverlayColor = LuaApiUtility.MergeColor(Reanimation.mExtraOverlayColor, red, green, blue, alpha);
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public ShowcaseReanimation set_position(double x, double y)
        {
            Reanimation.SetPosition((float)x, (float)y);
            return this;
        }

        public ShowcaseReanimation reanimation_initialize(double x, double y, string type)
        {
            Reanimation.mReanimationType = type;
            Reanimation.SetPosition((float)x, (float)y);
            return this;
        }

        public ShowcaseReanimation move(double x, double y)
        {
            return set_position(x, y);
        }

        public ShowcaseReanimation offset(double x, double y)
        {
            Reanimation.SetPosition(Reanimation.mOverlayMatrix.M41 + (float)x, Reanimation.mOverlayMatrix.M42 + (float)y);
            return this;
        }

        public ShowcaseReanimation set_matrix(
            double m11,
            double m12,
            double m21,
            double m22,
            double x,
            double y)
        {
            Matrix4x4 matrix = Matrix4x4.Identity;
            matrix.M11 = (float)m11;
            matrix.M12 = (float)m12;
            matrix.M21 = (float)m21;
            matrix.M22 = (float)m22;
            matrix.M41 = (float)x;
            matrix.M42 = (float)y;
            Reanimation.mOverlayMatrix = matrix;
            return this;
        }

        public ShowcaseReanimation set_time(double value)
        {
            Reanimation.mAnimTime = (float)value;
            return this;
        }

        public ShowcaseReanimation set_rate(double rate)
        {
            Reanimation.mAnimRate = (float)rate;
            return this;
        }

        public ShowcaseReanimation set_scale(double scale)
        {
            return set_scale(scale, scale);
        }

        public ShowcaseReanimation set_scale(double scaleX, double scaleY)
        {
            Reanimation.OverrideScale((float)scaleX, (float)scaleY);
            return this;
        }

        public ShowcaseReanimation override_scale(double scaleX, double scaleY)
        {
            return set_scale(scaleX, scaleY);
        }

        public ShowcaseReanimation set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseReanimation set_color(double red, double green, double blue, double alpha)
        {
            Reanimation.mColorOverride = new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha));
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public ShowcaseReanimation set_extra_additive_color(double red, double green, double blue)
        {
            return set_extra_additive_color(red, green, blue, 255);
        }

        public ShowcaseReanimation set_extra_additive_color(double red, double green, double blue, double alpha)
        {
            Reanimation.mExtraAdditiveColor = new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha));
            Reanimation.mEnableExtraAdditiveDraw = true;
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public ShowcaseReanimation set_extra_overlay_color(double red, double green, double blue)
        {
            return set_extra_overlay_color(red, green, blue, 255);
        }

        public ShowcaseReanimation set_extra_overlay_color(double red, double green, double blue, double alpha)
        {
            Reanimation.mExtraOverlayColor = new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha));
            Reanimation.mEnableExtraOverlayDraw = true;
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public ShowcaseReanimation clear_extra_colors()
        {
            Reanimation.mEnableExtraAdditiveDraw = false;
            Reanimation.mEnableExtraOverlayDraw = false;
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public ShowcaseReanimation play(string trackName)
        {
            return play(trackName, "loop", 0, 0);
        }

        public ShowcaseReanimation play(string trackName, string loopType)
        {
            return play(trackName, loopType, 0, 0);
        }

        public ShowcaseReanimation play(string trackName, string loopType, double animRate)
        {
            return play(trackName, loopType, animRate, 0);
        }

        public ShowcaseReanimation play(string trackName, string loopType, double animRate, double blendTicks)
        {
            EnsureTrack(trackName);
            Reanimation.PlayReanim(
                trackName,
                ParseLoopType(loopType),
                (byte)Math.Clamp((int)Math.Round(blendTicks), 0, byte.MaxValue),
                (float)animRate);
            return this;
        }

        public ShowcaseReanimation play_reanim(string trackName, string loopType, double blendTime, double animRate)
        {
            return play(trackName, loopType, animRate, blendTime);
        }

        public ShowcaseReanimation set_frames_for_layer(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.SetFramesForLayer(trackName);
            return this;
        }

        public bool track_exists(string trackName)
        {
            return Reanimation.TrackExists(trackName);
        }

        public string current_image(string trackName)
        {
            EnsureTrack(trackName);
            return Reanimation.GetCurrentTrackImage(trackName);
        }

        public string get_current_track_image(string trackName)
        {
            return current_image(trackName);
        }

        public ShowcaseReanimationTrack track(string trackName)
        {
            if (!Reanimation.TrackExists(trackName))
            {
                return null;
            }

            int index = Reanimation.FindTrackIndex(trackName);
            return new ShowcaseReanimationTrack(Reanimation, index);
        }

        public ShowcaseReanimationTrack track_at(int index)
        {
            return index < 0 || index >= track_count
                ? null
                : new ShowcaseReanimationTrack(Reanimation, index);
        }

        public string track_name(int index)
        {
            return index < 0 || index >= track_count
                ? null
                : Reanimation.mDefinition.mTracks[index].mName;
        }

        public int track_index(string trackName)
        {
            return Reanimation.TrackExists(trackName) ? Reanimation.FindTrackIndex(trackName) : -1;
        }

        public int find_track_index(string trackName)
        {
            return track_index(trackName);
        }

        public ShowcaseReanimation set_image_override(string trackName, string imageId)
        {
            EnsureTrack(trackName);
            Reanimation.SetImageOverride(trackName, RequireImage(imageId));
            return this;
        }

        public ShowcaseReanimation set_image_override(string trackName, ShowcaseImage image)
        {
            EnsureTrack(trackName);
            Reanimation.SetImageOverride(trackName, LuaApiUtility.ImageFrom(image));
            return this;
        }

        public ShowcaseReanimation clear_image_override(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.SetImageOverride(trackName, null);
            return this;
        }

        public string image_override_id(string trackName)
        {
            EnsureTrack(trackName);
            return Reanimation.GetImageOverride(trackName)?.mId;
        }

        public ShowcaseImage get_image_override(string trackName)
        {
            EnsureTrack(trackName);
            Image image = Reanimation.GetImageOverride(trackName);
            return image is null ? null : new ShowcaseImage(image);
        }

        public ShowcaseReanimation set_font_override(string trackName, string fontId)
        {
            EnsureTrack(trackName);
            Reanimation.SetFontOverride(trackName, RequireFontId(fontId));
            return this;
        }

        public ShowcaseReanimation clear_font_override(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.SetFontOverride(trackName, null);
            return this;
        }

        public string font_override_id(string trackName)
        {
            EnsureTrack(trackName);
            return Reanimation.GetFontOverride(trackName);
        }

        public ShowcaseReanimationTransform transform(string trackName, double frameIndex)
        {
            EnsureTrack(trackName);
            return transform_at(Reanimation.FindTrackIndex(trackName), frameIndex);
        }

        public ShowcaseReanimationTransform transform_at(int trackIndex, double frameIndex)
        {
            ReanimatorTrack track = trackIndex < 0 || trackIndex >= track_count
                ? null
                : Reanimation.mDefinition?.mTracks?[trackIndex];
            int frame = (int)Math.Round(frameIndex);
            if (track?.mTransforms is null || frame < 0 || frame >= track.mTransformCount)
            {
                return null;
            }

            return new ShowcaseReanimationTransform(track.mTransforms[frame]);
        }

        public string image_at(string trackName, double frameIndex)
        {
            return transform(trackName, frameIndex)?.image;
        }

        public string text_at(string trackName, double frameIndex)
        {
            return transform(trackName, frameIndex)?.text;
        }

        public ShowcaseReanimation show_only_track(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.ShowOnlyTrack(trackName);
            return this;
        }

        public ShowcaseReanimation hide_track(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.AssignRenderGroupToTrack(trackName, ReanimatorXnaHelpers.RENDER_GROUP_HIDDEN);
            return this;
        }

        public ShowcaseReanimation show_track(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.AssignRenderGroupToTrack(trackName, ReanimatorXnaHelpers.RENDER_GROUP_NORMAL);
            return this;
        }

        public ShowcaseReanimation show_prefix(string trackNamePrefix)
        {
            Reanimation.AssignRenderGroupToPrefix(trackNamePrefix, ReanimatorXnaHelpers.RENDER_GROUP_NORMAL);
            return this;
        }

        public ShowcaseReanimation hide_prefix(string trackNamePrefix)
        {
            Reanimation.AssignRenderGroupToPrefix(trackNamePrefix, ReanimatorXnaHelpers.RENDER_GROUP_HIDDEN);
            return this;
        }

        public ShowcaseReanimation set_shake(string trackName, double amount)
        {
            EnsureTrack(trackName);
            Reanimation.SetShakeOverride(trackName, (float)amount);
            return this;
        }

        public ShowcaseReanimation set_shake_override(string trackName, double amount)
        {
            return set_shake(trackName, amount);
        }

        public ShowcaseReanimation set_render_group(string trackName, double renderGroup)
        {
            EnsureTrack(trackName);
            Reanimation.AssignRenderGroupToTrack(trackName, (int)Math.Round(renderGroup));
            return this;
        }

        public ShowcaseReanimation assign_render_group_to_track(string trackName, double renderGroup)
        {
            return set_render_group(trackName, renderGroup);
        }

        public ShowcaseReanimation set_render_group_prefix(string trackNamePrefix, double renderGroup)
        {
            Reanimation.AssignRenderGroupToPrefix(trackNamePrefix ?? string.Empty, (int)Math.Round(renderGroup));
            return this;
        }

        public ShowcaseReanimation assign_render_group_to_prefix(string trackNamePrefix, double renderGroup)
        {
            return set_render_group_prefix(trackNamePrefix, renderGroup);
        }

        public ShowcaseReanimation set_render_group_by_prefix(string trackNamePrefix, double renderGroup)
        {
            return set_render_group_prefix(trackNamePrefix, renderGroup);
        }

        public int get_render_group_by_prefix(string trackNamePrefix)
        {
            return Reanimation.GetRenderGroupByPrefix(trackNamePrefix ?? string.Empty);
        }

        public double track_velocity(string trackName)
        {
            EnsureTrack(trackName);
            return Reanimation.GetTrackVelocity(trackName);
        }

        public double get_track_velocity(string trackName)
        {
            return track_velocity(trackName);
        }

        public bool is_track_showing(string trackName)
        {
            EnsureTrack(trackName);
            return Reanimation.IsTrackShowing(trackName);
        }

        public bool is_anim_playing(string trackName)
        {
            EnsureTrack(trackName);
            return Reanimation.IsAnimPlaying(trackName);
        }

        public ShowcaseReanimation set_base_pose_from_anim(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.SetBasePoseFromAnim(trackName);
            return this;
        }

        public ShowcaseReanimation set_base_pos_from_anim(string trackName)
        {
            return set_base_pose_from_anim(trackName);
        }

        public ShowcaseReanimation propogate_color_to_attachments()
        {
            Reanimation.PropogateColorToAttachments();
            return this;
        }

        public ShowcaseReanimation set_truncate_disappearing_frames(string trackName, bool truncate)
        {
            if (!string.IsNullOrEmpty(trackName))
            {
                EnsureTrack(trackName);
            }

            Reanimation.SetTruncateDisappearingFrames(trackName, truncate);
            return this;
        }

        public ShowcaseReanimationFrameRange frame_range(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.GetFramesForLayer(trackName, out int frameStart, out int frameCount);
            return new ShowcaseReanimationFrameRange(frameStart, frameCount);
        }

        public DynValue get_frames_for_layer(string trackName)
        {
            EnsureTrack(trackName);
            Reanimation.GetFramesForLayer(trackName, out int frameStart, out int frameCount);
            return DynValue.NewTuple(DynValue.NewNumber(frameStart), DynValue.NewNumber(frameCount));
        }

        public ShowcaseReanimationTrack get_track_instance_by_name(string trackName)
        {
            return track(trackName);
        }

        public ShowcaseReanimation set_track_instance_by_name(string trackName, ShowcaseReanimationTrack instance)
        {
            int index = track_index(trackName);
            return set_track_instance(index, instance);
        }

        public ShowcaseReanimation set_frame_range(double startFrame, double frameCount)
        {
            int maxFrameCount = Reanimation.mDefinition?.mTracks is { Length: > 0 }
                ? Reanimation.mDefinition.mTracks[0].mTransformCount
                : 0;
            if (maxFrameCount <= 0)
            {
                Reanimation.mFrameStart = 0;
                Reanimation.mFrameCount = 0;
                Reanimation.mLastFrameTime = -1f;
                return this;
            }

            int start = Math.Clamp((int)Math.Round(startFrame), 0, maxFrameCount - 1);
            int count = Math.Clamp((int)Math.Round(frameCount), 1, maxFrameCount - start);
            Reanimation.mFrameStart = start;
            Reanimation.mFrameCount = count;
            Reanimation.mLastFrameTime = -1f;
            return this;
        }

        public ShowcaseReanimationTransform current_transform(string trackName)
        {
            EnsureTrack(trackName);
            int index = Reanimation.FindTrackIndex(trackName);
            return current_transform_at(index);
        }

        public ShowcaseReanimationTransform current_transform_at(int index)
        {
            if (index < 0 || index >= track_count || Reanimation.mFrameCount == 0)
            {
                return null;
            }

            Reanimation.GetCurrentTransform(index, out ReanimatorTransform transform);
            return new ShowcaseReanimationTransform(transform);
        }

        public DynValue get_current_transform(int index)
        {
            if (index < 0 || index >= track_count || Reanimation.mFrameCount == 0)
            {
                return DynValue.Nil;
            }

            Reanimation.GetCurrentTransform(index, out ReanimatorTransform transform);
            return TransformTuple(transform);
        }

        public DynValue get_transform_at_time(int index, double fraction, int animFrameBefore, int animFrameAfter)
        {
            if (index < 0 || index >= track_count)
            {
                return DynValue.Nil;
            }

            ReanimatorFrameTime frameTime = new()
            {
                mFraction = (float)fraction,
                mAnimFrameBeforeInt = animFrameBefore,
                mAnimFrameAfterInt = animFrameAfter
            };
            Reanimation.GetTransformAtTime(index, out ReanimatorTransform transform, frameTime);
            return TransformTuple(transform);
        }

        public ShowcaseFrameTime frame_time()
        {
            if (Reanimation.mFrameCount == 0)
            {
                return null;
            }

            Reanimation.GetFrameTime(out ReanimatorFrameTime frameTime);
            return new ShowcaseFrameTime(frameTime);
        }

        public DynValue get_frame_time()
        {
            if (Reanimation.mFrameCount == 0)
            {
                return DynValue.Nil;
            }

            Reanimation.GetFrameTime(out ReanimatorFrameTime frameTime);
            return DynValue.NewTuple(
                DynValue.NewNumber(frameTime.mFraction),
                DynValue.NewNumber(frameTime.mAnimFrameBeforeInt),
                DynValue.NewNumber(frameTime.mAnimFrameAfterInt));
        }

        public ShowcaseMatrix track_matrix(string trackName)
        {
            EnsureTrack(trackName);
            return track_matrix_at(Reanimation.FindTrackIndex(trackName));
        }

        public ShowcaseMatrix track_matrix_at(int index)
        {
            if (index < 0 || index >= track_count || Reanimation.mFrameCount == 0)
            {
                return null;
            }

            Reanimation.GetTrackMatrix(index, out Matrix4x4 matrix);
            return new ShowcaseMatrix(matrix);
        }

        public ShowcaseMatrix get_track_matrix(int index)
        {
            return track_matrix_at(index);
        }

        public ShowcaseMatrix get_track_matrix(int index, ShowcaseMatrix matrix)
        {
            ShowcaseMatrix result = track_matrix_at(index);
            if (matrix is null || result is null)
            {
                return result;
            }

            return matrix.copy_from(result);
        }

        public ShowcaseMatrix attachment_overlay_matrix(string trackName)
        {
            EnsureTrack(trackName);
            return attachment_overlay_matrix_at(Reanimation.FindTrackIndex(trackName));
        }

        public ShowcaseMatrix attachment_overlay_matrix_at(int index)
        {
            if (index < 0 || index >= track_count || Reanimation.mFrameCount == 0)
            {
                return null;
            }

            Reanimation.GetAttachmentOverlayMatrix(index, out Matrix4x4 matrix);
            return new ShowcaseMatrix(matrix);
        }

        public ShowcaseMatrix get_attachment_overlay_matrix(int index)
        {
            return attachment_overlay_matrix_at(index);
        }

        public ShowcaseMatrix get_attachment_overlay_matrix(int index, ShowcaseMatrix matrix)
        {
            ShowcaseMatrix result = attachment_overlay_matrix_at(index);
            if (matrix is null || result is null)
            {
                return result;
            }

            return matrix.copy_from(result);
        }

        public ShowcaseMatrix track_base_pose_matrix(string trackName)
        {
            EnsureTrack(trackName);
            return track_base_pose_matrix_at(Reanimation.FindTrackIndex(trackName));
        }

        public ShowcaseMatrix track_base_pose_matrix_at(int index)
        {
            if (index < 0 || index >= track_count || Reanimation.mFrameCount == 0)
            {
                return null;
            }

            Reanimation.GetTrackBasePoseMatrix(index, out Matrix4x4 matrix);
            return new ShowcaseMatrix(matrix);
        }

        public ShowcaseMatrix get_track_base_pos_matrix(int index)
        {
            return track_base_pose_matrix_at(index);
        }

        public ShowcaseMatrix get_track_base_pos_matrix(int index, ShowcaseMatrix matrix)
        {
            ShowcaseMatrix result = track_base_pose_matrix_at(index);
            if (matrix is null || result is null)
            {
                return result;
            }

            return matrix.copy_from(result);
        }

        public bool should_trigger_timed_event(double eventTime)
        {
            return Reanimation.ShouldTriggerTimedEvent((float)eventTime);
        }

        public ShowcaseReanimation start_blend(double blendTicks)
        {
            Reanimation.StartBlend((byte)Math.Clamp((int)Math.Round(blendTicks), 0, byte.MaxValue));
            return this;
        }

        public ShowcaseReanimation update()
        {
            Reanimation.Update();
            return this;
        }

        public ShowcaseReanimation update_attacher_track(int trackIndex)
        {
            if (trackIndex >= 0 && trackIndex < track_count)
            {
                Reanimation.UpdateAttacherTrack(trackIndex);
            }

            return this;
        }

        public ShowcaseReanimation draw(LuaGraphicsApi graphics)
        {
            if (graphics is not null && !Reanimation.mDead)
            {
                Reanimation.Draw(graphics.Graphics);
            }

            return this;
        }

        public bool draw_track(LuaGraphicsApi graphics, string trackName)
        {
            EnsureTrack(trackName);
            return draw_track_at(graphics, Reanimation.FindTrackIndex(trackName));
        }

        public bool draw_track_at(LuaGraphicsApi graphics, int index)
        {
            if (graphics is null || Reanimation.mDead || Reanimation.mFrameCount == 0 || index < 0 || index >= track_count)
            {
                return false;
            }

            ref ReanimatorTrackInstance track = ref Reanimation.mTrackInstances[index];
            bool trackDrawn = Reanimation.DrawTrack(graphics.Graphics, index, track.mRenderGroup);
            EffectSystem effectSystem = Reanimation.mReanimationHolder?.mEffectSystem;
            if (track.mAttachmentID != AttachmentID.Null && effectSystem is not null)
            {
                GlobalMembersAttachment.AttachmentDraw(effectSystem, track.mAttachmentID, graphics.Graphics, !trackDrawn);
            }

            return trackDrawn;
        }

        public bool draw_track(LuaGraphicsApi graphics, int index, double renderGroup)
        {
            if (graphics is null || Reanimation.mDead || Reanimation.mFrameCount == 0 || index < 0 || index >= track_count)
            {
                return false;
            }

            return Reanimation.DrawTrack(graphics.Graphics, index, (int)Math.Round(renderGroup));
        }

        public ShowcaseReanimation draw_group(LuaGraphicsApi graphics, double renderGroup)
        {
            if (graphics is not null && !Reanimation.mDead)
            {
                Reanimation.DrawRenderGroup(graphics.Graphics, (int)Math.Round(renderGroup));
            }

            return this;
        }

        public ShowcaseReanimation draw_render_group(LuaGraphicsApi graphics, double renderGroup)
        {
            return draw_group(graphics, renderGroup);
        }

        public ShowcaseReanimation die()
        {
            Reanimation.ReanimationDie();
            return this;
        }

        public ShowcaseReanimation reanimation_die()
        {
            return die();
        }

        public ShowcaseReanimation reanimation_delete()
        {
            Reanimation.ReanimationDelete();
            return this;
        }

        public ShowcaseReanimation attach_reanim(string trackName, ShowcaseReanimation child)
        {
            return attach_reanim(trackName, child, 0, 0);
        }

        public ShowcaseReanimation attach_reanim(string trackName, ShowcaseReanimation child, double offsetX, double offsetY)
        {
            _scene.AttachReanimation(Reanimation, trackName, child, (float)offsetX, (float)offsetY);
            return this;
        }

        public ShowcaseReanimation attach_to_another_reanimation(ShowcaseReanimation parent, string trackName)
        {
            if (parent?.Reanimation is null)
            {
                throw new InvalidOperationException("The parent reanimation is not valid.");
            }

            parent.EnsureTrack(trackName);
            Reanimation.AttachToAnotherReanimation(parent.Reanimation, trackName);
            return this;
        }

        public ShowcaseReanimation attach_particle(string trackName, ShowcaseParticle child)
        {
            return attach_particle(trackName, child, 0, 0);
        }

        public ShowcaseReanimation attach_particle(string trackName, ShowcaseParticle child, double offsetX, double offsetY)
        {
            _scene.AttachParticle(Reanimation, trackName, child, (float)offsetX, (float)offsetY);
            return this;
        }

        public DynValue attach_particle_to_track(string trackName, ShowcaseParticle child, double offsetX, double offsetY)
        {
            EnsureTrack(trackName);
            if (child?.ParticleSystem is null)
            {
                return DynValue.NewTuple(DynValue.Nil, DynValue.Nil);
            }

            ref AttachEffect effect = ref Reanimation.AttachParticleToTrack(trackName, child.ParticleSystem, (float)offsetX, (float)offsetY);
            if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref effect))
            {
                return DynValue.NewTuple(DynValue.Nil, DynValue.Nil);
            }

            AttachmentID attachmentId = Reanimation.GetTrackInstanceByName(trackName).mAttachmentID;
            Attachment attachment = Reanimation.mReanimationHolder?.mEffectSystem?.mAttachmentHolder?.mAttachments.DataArrayTryToGet(attachmentId);
            if (attachment is null)
            {
                return DynValue.NewTuple(DynValue.Nil, DynValue.Nil);
            }

            int index = Math.Max(0, attachment.mNumEffects - 1);
            return DynValue.NewTuple(
                UserData.Create(new ShowcaseAttachment(attachment)),
                DynValue.NewNumber(index));
        }

        public ShowcaseReanimation attach_trail(string trackName, ShowcaseTrail child)
        {
            return attach_trail(trackName, child, 0, 0);
        }

        public ShowcaseReanimation attach_trail(string trackName, ShowcaseTrail child, double offsetX, double offsetY)
        {
            _scene.AttachTrail(Reanimation, trackName, child, (float)offsetX, (float)offsetY);
            return this;
        }

        public ShowcaseReanimation detach(string trackName)
        {
            _scene.DetachTrack(Reanimation, trackName);
            return this;
        }

        public ShowcaseAttachment attachment(string trackName)
        {
            if (!Reanimation.TrackExists(trackName))
            {
                return null;
            }

            AttachmentID attachmentId = Reanimation.GetTrackInstanceByName(trackName).mAttachmentID;
            if (attachmentId == AttachmentID.Null)
            {
                return null;
            }

            Attachment attachment = Reanimation.mReanimationHolder?.mEffectSystem?.mAttachmentHolder?.mAttachments.DataArrayTryToGet(attachmentId);
            return attachment is null ? null : new ShowcaseAttachment(attachment);
        }

        public ShowcaseReanimation find_sub_reanim(string reanimationType)
        {
            Reanimation subReanimation = Reanimation.FindSubReanim(reanimationType);
            return subReanimation is null ? null : new ShowcaseReanimation(_scene, reanimationType, subReanimation);
        }

        public ShowcaseMatrix matrix_from_transform(params DynValue[] args)
        {
            if (args is null || args.Length < 6)
            {
                return ShowcaseMatrix.Identity();
            }

            ShowcaseMatrix target = TryGetMatrix(args[^1]);
            int count = target is null ? args.Length : args.Length - 1;
            ReanimatorTransform transform = TransformFromArgs(args, count);
            Reanimation.MatrixFromTransform(transform, out Matrix4x4 matrix);

            ShowcaseMatrix result = new(matrix);
            return target is null ? result : target.copy_from(result);
        }

        public DynValue parse_attacher_track(params DynValue[] args)
        {
            if (args is null || args.Length == 0)
            {
                return DynValue.Nil;
            }

            ReanimatorTransform transform;
            if (args.Length >= 2 && args.Length < 10)
            {
                transform = new ReanimatorTransform
                {
                    mFrame = (float)LuaApiUtility.NumberOr(args[0], -1),
                    mText = LuaApiUtility.StringOr(args[1], string.Empty)
                };
            }
            else
            {
                transform = TransformFromArgs(args, Math.Min(args.Length, 11));
            }

            Reanimation.ParseAttacherTrack(transform, out AttacherInfo info);
            return DynValue.NewTuple(
                DynValue.NewString(info.mReanimName ?? string.Empty),
                DynValue.NewString(info.mTrackName ?? string.Empty),
                DynValue.NewNumber(info.mAnimRate),
                DynValue.NewString(info.mLoopType.ToString()),
                DynValue.NewString(info.mReanimationType ?? string.Empty));
        }

        public ShowcaseReanimation attacher_synch_walk_speed(int trackIndex, ShowcaseReanimation attachReanim)
        {
            if (trackIndex < 0 || trackIndex >= track_count || attachReanim?.Reanimation is null)
            {
                return this;
            }

            Reanimation.GetCurrentTransform(trackIndex, out ReanimatorTransform transform);
            Reanimation.ParseAttacherTrack(transform, out AttacherInfo info);
            Reanimation attached = attachReanim.Reanimation;
            Reanimation.AttacherSynchWalkSpeed(trackIndex, ref attached, ref info);
            return this;
        }

        public ShowcaseReanimation reanim_blt_matrix(
            LuaGraphicsApi graphics,
            ShowcaseImage image,
            ShowcaseMatrix transform_matrix3x3,
            double clip_rect_x,
            double clip_rect_y,
            double clip_rect_width,
            double clip_rect_height,
            double red,
            double green,
            double blue,
            double alpha,
            string draw_mode,
            double src_rect_x,
            double src_rect_y,
            double src_rect_width,
            double src_rect_height)
        {
            if (graphics is null || image?.Image is null || transform_matrix3x3 is null)
            {
                return this;
            }

            DrawMode mode = string.Equals(draw_mode, "add", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(draw_mode, "additive", StringComparison.OrdinalIgnoreCase)
                ? DrawMode.Additive
                : DrawMode.Normal;
            Reanimation.ReanimBltMatrix(
                graphics.Graphics,
                image.Image,
                transform_matrix3x3.ToMatrix4x4(),
                new Rectangle(
                    (int)Math.Round(clip_rect_x),
                    (int)Math.Round(clip_rect_y),
                    (int)Math.Round(clip_rect_width),
                    (int)Math.Round(clip_rect_height)),
                new SexyColor(ClampColor(red), ClampColor(green), ClampColor(blue), ClampColor(alpha)),
                mode,
                new Rectangle(
                    (int)Math.Round(src_rect_x),
                    (int)Math.Round(src_rect_y),
                    (int)Math.Round(src_rect_width),
                    (int)Math.Round(src_rect_height)));
            return this;
        }

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static DynValue TransformTuple(ReanimatorTransform transform)
        {
            return DynValue.NewTuple(
                DynValue.NewNumber(transform.mTransX),
                DynValue.NewNumber(transform.mTransY),
                DynValue.NewNumber(transform.mSkewX),
                DynValue.NewNumber(transform.mSkewY),
                DynValue.NewNumber(transform.mScaleX),
                DynValue.NewNumber(transform.mScaleY),
                DynValue.NewNumber(transform.mFrame),
                DynValue.NewNumber(transform.mAlpha),
                StringOrNil(transform.mImage),
                StringOrNil(transform.mFont),
                StringOrNil(transform.mText));
        }

        private static ReanimatorTransform TransformFromArgs(DynValue[] args, int count)
        {
            ReanimatorTransform transform = new()
            {
                mTransX = (float)LuaApiUtility.NumberOr(args[0], 0),
                mTransY = (float)LuaApiUtility.NumberOr(args[1], 0),
                mSkewX = (float)LuaApiUtility.NumberOr(args[2], 0),
                mSkewY = (float)LuaApiUtility.NumberOr(args[3], 0),
                mScaleX = (float)LuaApiUtility.NumberOr(args[4], 1),
                mScaleY = (float)LuaApiUtility.NumberOr(args[5], 1),
                mFrame = count > 6 ? (float)LuaApiUtility.NumberOr(args[6], -1) : -1,
                mAlpha = count > 7 ? (float)LuaApiUtility.NumberOr(args[7], 1) : 1,
                mImage = count > 8 ? LuaApiUtility.StringOr(args[8]) : null,
                mFont = count > 9 ? LuaApiUtility.StringOr(args[9]) : null,
                mText = count > 10 ? LuaApiUtility.StringOr(args[10], string.Empty) : string.Empty
            };
            return transform;
        }

        private static ShowcaseMatrix TryGetMatrix(DynValue value)
        {
            return value.Type == DataType.UserData ? value.ToObject<ShowcaseMatrix>() : null;
        }

        private static DynValue StringOrNil(string value)
        {
            return value is null ? DynValue.Nil : DynValue.NewString(value);
        }

        private static Image RequireImage(string imageId)
        {
            Image image = ResourceHandler.GetImage(imageId);
            return image ?? throw new InvalidOperationException($"Image '{imageId}' was not found in the current project.");
        }

        private static string RequireFontId(string fontId)
        {
            Font font = ResourceHandler.GetFont(fontId);
            return font is null
                ? throw new InvalidOperationException($"Font '{fontId}' was not found in the current project.")
                : fontId;
        }

        private void EnsureTrack(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
            {
                throw new ArgumentException("A track name is required.", nameof(trackName));
            }

            if (!Reanimation.TrackExists(trackName))
            {
                throw new InvalidOperationException($"Track '{trackName}' was not found on reanim '{Reanimation.mReanimationType}'.");
            }
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, ignoreCase: true, out T parsed) ? parsed : fallback;
        }

        private static ReanimLoopType ParseLoopType(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLower(CultureInfo.InvariantCulture);
            return normalized switch
            {
                "once" or "play_once" or "playonce" => ReanimLoopType.PlayOnce,
                "once_hold" or "hold" or "play_once_and_hold" or "playonceandhold" => ReanimLoopType.PlayOnceAndHold,
                "loop_full" or "loop_full_last_frame" => ReanimLoopType.LoopFullLastFrame,
                "once_full" or "play_once_full_last_frame" => ReanimLoopType.PlayOnceFullLastFrame,
                "hold_full" or "play_once_full_last_frame_and_hold" => ReanimLoopType.PlayOnceFullLastFrameAndHold,
                _ => ReanimLoopType.Loop
            };
        }
    }
}
