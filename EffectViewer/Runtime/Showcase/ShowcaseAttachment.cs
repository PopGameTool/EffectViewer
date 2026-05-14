using System;
using System.Numerics;
using EffectViewer.Runtime.Lua;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.EffectRuntime.Graphics;
using EffectViewer.EffectRuntime.Reanim.Attachment;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseAttachment
    {
        internal ShowcaseAttachment(Attachment attachment)
        {
            Attachment = attachment;
        }

        internal Attachment Attachment { get; }

        public int effect_count => Attachment?.mNumEffects ?? 0;
        public int num_effects => effect_count;
        public bool dead
        {
            get => Attachment?.mDead ?? true;
            set
            {
                if (Attachment is not null)
                {
                    Attachment.mDead = value;
                }
            }
        }

        public bool is_dead() => Attachment is null || Attachment.mDead;
        public bool is_empty() => Attachment is null || Attachment.mNumEffects == 0;
        public bool is_full() => Attachment is not null && Attachment.mNumEffects >= EffectConstants.MAX_EFFECTS_PER_ATTACHMENT;

        public ShowcaseAttachment update()
        {
            Attachment?.Update();
            return this;
        }

        public ShowcaseAttachment draw(LuaGraphicsApi graphics)
        {
            if (graphics is not null && Attachment is { mDead: false })
            {
                Attachment.Draw(graphics.Graphics, theParentHidden: false);
            }

            return this;
        }

        public ShowcaseAttachment draw(LuaGraphicsApi graphics, bool parentHidden)
        {
            if (graphics is not null && Attachment is { mDead: false })
            {
                Attachment.Draw(graphics.Graphics, parentHidden);
            }

            return this;
        }

        public ShowcaseAttachment die()
        {
            Attachment?.AttachmentDie();
            return this;
        }

        public ShowcaseAttachment attachment_die()
        {
            return die();
        }

        public ShowcaseAttachment detach()
        {
            Attachment?.Detach();
            return this;
        }

        public ShowcaseAttachmentEffect effect(int index)
        {
            return Attachment is null || index < 0 || index >= Attachment.mNumEffects
                ? null
                : new ShowcaseAttachmentEffect(Attachment, index);
        }

        public ShowcaseAttachmentEffect get_effect(int index)
        {
            return effect(index);
        }

        public ShowcaseAttachment set_effect(int index, ShowcaseAttachmentEffect effect)
        {
            if (Attachment is not null && effect is not null && index >= 0 && index < Attachment.mNumEffects)
            {
                Attachment.mEffectArray[index] = effect.Snapshot();
            }

            return this;
        }

        public ShowcaseAttachment cross_fade(string emitterName)
        {
            Attachment?.CrossFade(emitterName);
            return this;
        }

        public ShowcaseAttachment set_position(double x, double y)
        {
            Attachment?.SetPosition(new Vector2((float)x, (float)y));
            return this;
        }

        public ShowcaseAttachment set_matrix(
            double m11,
            double m12,
            double m21,
            double m22,
            double x,
            double y)
        {
            if (Attachment is null)
            {
                return this;
            }

            Matrix4x4 matrix = Matrix4x4.Identity;
            matrix.M11 = (float)m11;
            matrix.M12 = (float)m12;
            matrix.M21 = (float)m21;
            matrix.M22 = (float)m22;
            matrix.M41 = (float)x;
            matrix.M42 = (float)y;
            Attachment.SetMatrix(matrix);
            return this;
        }

        public ShowcaseAttachment set_matrix(ShowcaseMatrix matrix)
        {
            if (Attachment is not null && matrix is not null)
            {
                Attachment.SetMatrix(matrix.ToMatrix4x4());
            }

            return this;
        }

        public ShowcaseAttachment set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseAttachment set_color(double red, double green, double blue, double alpha)
        {
            Attachment?.OverrideColor(new EffectColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha)));
            return this;
        }

        public ShowcaseAttachment override_color(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            Attachment?.OverrideColor(new EffectColor(
                LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(red, 255)),
                LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(green, 255)),
                LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(blue, 255)),
                LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(alpha, 255))));
            return this;
        }

        public ShowcaseAttachment set_scale(double scale)
        {
            Attachment?.OverrideScale((float)scale);
            return this;
        }

        public ShowcaseAttachment override_scale(double scale)
        {
            return set_scale(scale);
        }

        public ShowcaseAttachment propogate_color(
            double red,
            double green,
            double blue,
            double alpha,
            bool enableAdditiveColor,
            double additiveRed,
            double additiveGreen,
            double additiveBlue,
            double additiveAlpha,
            bool enableOverlayColor,
            double overlayRed,
            double overlayGreen,
            double overlayBlue,
            double overlayAlpha)
        {
            Attachment?.PropogateColor(
                new EffectColor(ClampColor(red), ClampColor(green), ClampColor(blue), ClampColor(alpha)),
                enableAdditiveColor,
                new EffectColor(ClampColor(additiveRed), ClampColor(additiveGreen), ClampColor(additiveBlue), ClampColor(additiveAlpha)),
                enableOverlayColor,
                new EffectColor(ClampColor(overlayRed), ClampColor(overlayGreen), ClampColor(overlayBlue), ClampColor(overlayAlpha)));
            return this;
        }

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }
    }
}
