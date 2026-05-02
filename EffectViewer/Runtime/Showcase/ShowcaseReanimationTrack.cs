using System;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Reanim.Attachment;

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

        public ShowcaseReanimationTrack set_shake(double amount)
        {
            Track.mShakeOverride = (float)amount;
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

        public bool is_showing()
        {
            return _reanimation.IsTrackShowing(name);
        }

        public double velocity()
        {
            return _reanimation.GetTrackVelocity(name);
        }

        private ref ReanimatorTrackInstance Track => ref _reanimation.mTrackInstances[_trackIndex];

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }
    }
}
