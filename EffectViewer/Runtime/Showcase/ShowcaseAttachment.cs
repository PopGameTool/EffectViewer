using System;
using System.Numerics;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Reanim.Attachment;

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

        public ShowcaseAttachment die()
        {
            Attachment?.AttachmentDie();
            return this;
        }

        public ShowcaseAttachment detach()
        {
            Attachment?.Detach();
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

        public ShowcaseAttachment set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseAttachment set_color(double red, double green, double blue, double alpha)
        {
            Attachment?.OverrideColor(new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha)));
            return this;
        }

        public ShowcaseAttachment set_scale(double scale)
        {
            Attachment?.OverrideScale((float)scale);
            return this;
        }

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }
    }
}
