using System;
using System.Runtime.CompilerServices;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Reanim.Attachment;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseReanimationTrack
    {
        private readonly Reanimation _reanimation;
        private readonly int _trackIndex;

        internal ShowcaseReanimationTrack(Reanimation reanimation, int trackIndex)
        {
            _reanimation = reanimation;
            _trackIndex = trackIndex;
        }

        public string name => _reanimation.mDefinition?.mTracks?[_trackIndex].mName ?? string.Empty;
        public int index => _trackIndex;
        public int transform_count => _reanimation.mDefinition?.mTracks?[_trackIndex].mTransformCount ?? 0;
        public string image_override_id => Track.mImageOverride?.mId;
        public ShowcaseImage image_override
        {
            get => Track.mImageOverride is null ? null : new ShowcaseImage(Track.mImageOverride);
            set => Track.mImageOverride = LuaApiUtility.ImageFrom(value);
        }

        public int blend_counter
        {
            get => Track.mBlendCounter;
            set => Track.mBlendCounter = Math.Max(0, value);
        }

        public int blend_time
        {
            get => Track.mBlendTime;
            set => Track.mBlendTime = Math.Max(0, value);
        }

        public double shake_override
        {
            get => Track.mShakeOverride;
            set => Track.mShakeOverride = (float)value;
        }

        public double shake_x
        {
            get => Track.mShakeX;
            set => Track.mShakeX = (float)value;
        }

        public double shake_y
        {
            get => Track.mShakeY;
            set => Track.mShakeY = (float)value;
        }

        public double attachment_id
        {
            get => IdToNumber(Track.mAttachmentID);
            set => Track.mAttachmentID = IdFromNumber<AttachmentID>(value);
        }

        public int render_group
        {
            get => Track.mRenderGroup;
            set => Track.mRenderGroup = value;
        }

        public bool ignore_clip_rect
        {
            get => Track.mIgnoreClipRect;
            set => Track.mIgnoreClipRect = value;
        }

        public bool truncate_disappearing_frames
        {
            get => Track.mTruncateDisappearingFrames;
            set => Track.mTruncateDisappearingFrames = value;
        }

        public bool ignore_color_override
        {
            get => Track.mIgnoreColorOverride;
            set => Track.mIgnoreColorOverride = value;
        }

        public bool ignore_extra_additive_color
        {
            get => Track.mIgnoreExtraAdditiveColor;
            set => Track.mIgnoreExtraAdditiveColor = value;
        }

        public bool is_attacher
        {
            get => Track.mIsAttacher;
            set => Track.mIsAttacher = value;
        }

        public double color_red
        {
            get => Track.mTrackColor.mRed;
            set => Track.mTrackColor.mRed = ClampColor(value);
        }

        public double color_green
        {
            get => Track.mTrackColor.mGreen;
            set => Track.mTrackColor.mGreen = ClampColor(value);
        }

        public double color_blue
        {
            get => Track.mTrackColor.mBlue;
            set => Track.mTrackColor.mBlue = ClampColor(value);
        }

        public double color_alpha
        {
            get => Track.mTrackColor.mAlpha;
            set => Track.mTrackColor.mAlpha = ClampColor(value);
        }

        public bool has_attachment() => Track.mAttachmentID != AttachmentID.Null;

        internal ReanimatorTrackInstance Snapshot()
        {
            return Track;
        }

        public ShowcaseAttachment attachment()
        {
            if (Track.mAttachmentID == AttachmentID.Null)
            {
                return null;
            }

            Attachment attachment = _reanimation.mReanimationHolder?.mEffectSystem?.mAttachmentHolder?.mAttachments.DataArrayTryToGet(Track.mAttachmentID);
            return attachment is null ? null : new ShowcaseAttachment(attachment);
        }

        public ShowcaseReanimationTrack set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseReanimationTrack set_color(double red, double green, double blue, double alpha)
        {
            Track.mTrackColor = new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha));
            return this;
        }

        public DynValue get_track_color()
        {
            return LuaApiUtility.ColorTuple(Track.mTrackColor);
        }

        public ShowcaseReanimationTrack set_track_color(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            Track.mTrackColor = LuaApiUtility.MergeColor(Track.mTrackColor, red, green, blue, alpha);
            return this;
        }

        public DynValue get_blend_transform()
        {
            ReanimatorTransform transform = Track.mBlendTransform;
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

        public ShowcaseReanimationTrack set_blend_transform(params DynValue[] values)
        {
            ReanimatorTransform transform = Track.mBlendTransform;
            if (values is not null)
            {
                if (values.Length > 0 && !LuaApiUtility.IsNil(values[0])) transform.mTransX = (float)values[0].CastToNumber();
                if (values.Length > 1 && !LuaApiUtility.IsNil(values[1])) transform.mTransY = (float)values[1].CastToNumber();
                if (values.Length > 2 && !LuaApiUtility.IsNil(values[2])) transform.mSkewX = (float)values[2].CastToNumber();
                if (values.Length > 3 && !LuaApiUtility.IsNil(values[3])) transform.mSkewY = (float)values[3].CastToNumber();
                if (values.Length > 4 && !LuaApiUtility.IsNil(values[4])) transform.mScaleX = (float)values[4].CastToNumber();
                if (values.Length > 5 && !LuaApiUtility.IsNil(values[5])) transform.mScaleY = (float)values[5].CastToNumber();
                if (values.Length > 6 && !LuaApiUtility.IsNil(values[6])) transform.mFrame = (float)values[6].CastToNumber();
                if (values.Length > 7 && !LuaApiUtility.IsNil(values[7])) transform.mAlpha = (float)values[7].CastToNumber();
                if (values.Length > 8 && !LuaApiUtility.IsNil(values[8])) transform.mImage = values[8].CastToString();
                if (values.Length > 9 && !LuaApiUtility.IsNil(values[9])) transform.mFont = values[9].CastToString();
                if (values.Length > 10 && !LuaApiUtility.IsNil(values[10])) transform.mText = values[10].CastToString();
            }

            Track.mBlendTransform = transform;
            return this;
        }

        public ShowcaseReanimationTrack set_shake(double amount)
        {
            Track.mShakeOverride = (float)amount;
            return this;
        }

        public ShowcaseReanimationTrack set_render_group(double renderGroup)
        {
            Track.mRenderGroup = (int)Math.Round(renderGroup);
            return this;
        }

        public ShowcaseReanimationTrack set_truncate(bool truncate)
        {
            Track.mTruncateDisappearingFrames = truncate;
            return this;
        }

        public ShowcaseReanimationTrack show()
        {
            Track.mRenderGroup = ReanimatorXnaHelpers.RENDER_GROUP_NORMAL;
            return this;
        }

        public ShowcaseReanimationTrack hide()
        {
            Track.mRenderGroup = ReanimatorXnaHelpers.RENDER_GROUP_HIDDEN;
            return this;
        }

        public ShowcaseReanimationTrack detach()
        {
            AttachmentID attachmentId = Track.mAttachmentID;
            GlobalMembersAttachment.AttachmentDetach(_reanimation.mReanimationHolder?.mEffectSystem, ref attachmentId);
            Track.mAttachmentID = attachmentId;
            return this;
        }

        public string current_image()
        {
            return _reanimation.GetCurrentTrackImage(name);
        }

        public ShowcaseReanimationTrack set_image_override(string imageId)
        {
            Track.mImageOverride = RequireImage(imageId);
            return this;
        }

        public ShowcaseReanimationTrack clear_image_override()
        {
            Track.mImageOverride = null;
            return this;
        }

        public ShowcaseReanimationTransform transform_at(int frameIndex)
        {
            ReanimatorTrack track = _reanimation.mDefinition?.mTracks?[_trackIndex];
            if (track?.mTransforms is null || frameIndex < 0 || frameIndex >= track.mTransformCount)
            {
                return null;
            }

            return new ShowcaseReanimationTransform(track.mTransforms[frameIndex]);
        }

        public string image_at(int frameIndex)
        {
            return transform_at(frameIndex)?.image;
        }

        public string text_at(int frameIndex)
        {
            return transform_at(frameIndex)?.text;
        }

        public bool is_showing()
        {
            return _reanimation.IsTrackShowing(name);
        }

        public double velocity()
        {
            return _reanimation.GetTrackVelocity(name);
        }

        public ShowcaseMatrix matrix()
        {
            if (_reanimation.mFrameCount == 0)
            {
                return null;
            }

            _reanimation.GetTrackMatrix(_trackIndex, out Matrix4x4 matrix);
            return new ShowcaseMatrix(matrix);
        }

        public ShowcaseMatrix attachment_overlay_matrix()
        {
            if (_reanimation.mFrameCount == 0)
            {
                return null;
            }

            _reanimation.GetAttachmentOverlayMatrix(_trackIndex, out Matrix4x4 matrix);
            return new ShowcaseMatrix(matrix);
        }

        public ShowcaseMatrix base_pose_matrix()
        {
            if (_reanimation.mFrameCount == 0)
            {
                return null;
            }

            _reanimation.GetTrackBasePoseMatrix(_trackIndex, out Matrix4x4 matrix);
            return new ShowcaseMatrix(matrix);
        }

        public ShowcaseReanimationTransform current_transform()
        {
            if (_reanimation.mFrameCount == 0)
            {
                return null;
            }

            _reanimation.GetCurrentTransform(_trackIndex, out ReanimatorTransform transform);
            return new ShowcaseReanimationTransform(transform);
        }

        public bool draw(LuaGraphicsApi graphics)
        {
            if (graphics is null || _reanimation.mDead || _reanimation.mFrameCount == 0)
            {
                return false;
            }

            bool trackDrawn = _reanimation.DrawTrack(graphics.Graphics, _trackIndex, Track.mRenderGroup);
            EffectSystem effectSystem = _reanimation.mReanimationHolder?.mEffectSystem;
            if (Track.mAttachmentID != AttachmentID.Null && effectSystem is not null)
            {
                GlobalMembersAttachment.AttachmentDraw(effectSystem, Track.mAttachmentID, graphics.Graphics, !trackDrawn);
            }

            return trackDrawn;
        }

        private ref ReanimatorTrackInstance Track => ref _reanimation.mTrackInstances[_trackIndex];

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static Image RequireImage(string imageId)
        {
            Image image = ResourceHandler.GetImage(imageId);
            return image ?? throw new InvalidOperationException($"Image '{imageId}' was not found in the current project.");
        }

        private static double IdToNumber<TId>(TId id)
            where TId : unmanaged
        {
            uint raw = Unsafe.As<TId, uint>(ref id);
            return raw;
        }

        private static TId IdFromNumber<TId>(double id)
            where TId : unmanaged
        {
            uint raw = double.IsFinite(id) && id > 0 ? unchecked((uint)Math.Round(id)) : 0U;
            return Unsafe.As<uint, TId>(ref raw);
        }

        private static DynValue StringOrNil(string value)
        {
            return value is null ? DynValue.Nil : DynValue.NewString(value);
        }
    }
}
