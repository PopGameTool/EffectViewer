using System;
using System.Globalization;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Reanim;

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
        public double get_x() => Reanimation.mOverlayMatrix.M41;
        public double get_y() => Reanimation.mOverlayMatrix.M42;
        public double get_rate() => Reanimation.mAnimRate;
        public double get_time() => Reanimation.mAnimTime;
        public bool is_dead() => Reanimation.mDead;

        public ShowcaseReanimation set_position(double x, double y)
        {
            Reanimation.SetPosition((float)x, (float)y);
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
            Reanimation.PlayReanim(
                trackName,
                ParseLoopType(loopType),
                (byte)Math.Clamp((int)Math.Round(blendTicks), 0, byte.MaxValue),
                (float)animRate);
            return this;
        }

        public bool track_exists(string trackName)
        {
            return Reanimation.TrackExists(trackName);
        }

        public string current_image(string trackName)
        {
            return Reanimation.GetCurrentTrackImage(trackName);
        }

        public ShowcaseReanimation show_only_track(string trackName)
        {
            Reanimation.ShowOnlyTrack(trackName);
            return this;
        }

        public ShowcaseReanimation hide_track(string trackName)
        {
            Reanimation.AssignRenderGroupToTrack(trackName, ReanimatorXnaHelpers.RENDER_GROUP_HIDDEN);
            return this;
        }

        public ShowcaseReanimation show_track(string trackName)
        {
            Reanimation.AssignRenderGroupToTrack(trackName, ReanimatorXnaHelpers.RENDER_GROUP_NORMAL);
            return this;
        }

        public ShowcaseReanimation set_shake(string trackName, double amount)
        {
            Reanimation.SetShakeOverride(trackName, (float)amount);
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

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
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
