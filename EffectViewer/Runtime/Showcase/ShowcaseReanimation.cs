using System;
using System.Globalization;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Filter;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Reanim.Attachment;
using EffectViewer.Runtime.Lua;

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

        public bool extra_overlay_draw
        {
            get => Reanimation.mEnableExtraOverlayDraw;
            set => Reanimation.mEnableExtraOverlayDraw = value;
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
        public double fps => Reanimation.mDefinition?.mFPS ?? 0;
        public int current_frame => track_count == 0 || Reanimation.mFrameCount == 0 ? 0 : Reanimation.GetCurrentFrame();
        public double get_x() => Reanimation.mOverlayMatrix.M41;
        public double get_y() => Reanimation.mOverlayMatrix.M42;
        public double get_rate() => Reanimation.mAnimRate;
        public double get_time() => Reanimation.mAnimTime;
        public bool is_dead() => Reanimation.mDead;
        public ShowcaseMatrix matrix() => new(Reanimation.mOverlayMatrix);

        public ShowcaseReanimation set_position(double x, double y)
        {
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

        public ShowcaseReanimation set_image_override(string trackName, string imageId)
        {
            EnsureTrack(trackName);
            Reanimation.SetImageOverride(trackName, RequireImage(imageId));
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

        public ShowcaseReanimation set_render_group(string trackName, double renderGroup)
        {
            EnsureTrack(trackName);
            Reanimation.AssignRenderGroupToTrack(trackName, (int)Math.Round(renderGroup));
            return this;
        }

        public ShowcaseReanimation set_render_group_prefix(string trackNamePrefix, double renderGroup)
        {
            Reanimation.AssignRenderGroupToPrefix(trackNamePrefix ?? string.Empty, (int)Math.Round(renderGroup));
            return this;
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

        public ShowcaseFrameTime frame_time()
        {
            if (Reanimation.mFrameCount == 0)
            {
                return null;
            }

            Reanimation.GetFrameTime(out ReanimatorFrameTime frameTime);
            return new ShowcaseFrameTime(frameTime);
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

        public ShowcaseReanimation draw_group(LuaGraphicsApi graphics, double renderGroup)
        {
            if (graphics is not null && !Reanimation.mDead)
            {
                Reanimation.DrawRenderGroup(graphics.Graphics, (int)Math.Round(renderGroup));
            }

            return this;
        }

        public ShowcaseReanimation die()
        {
            Reanimation.ReanimationDie();
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

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static Image RequireImage(string imageId)
        {
            Image image = ResourceHandler.GetImage(imageId);
            return image ?? throw new InvalidOperationException($"Image '{imageId}' was not found in the current project.");
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
