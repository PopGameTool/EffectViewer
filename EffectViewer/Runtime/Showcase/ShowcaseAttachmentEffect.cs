using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Reanim.Attachment;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseAttachmentEffect
    {
        private readonly Attachment _attachment;
        private readonly int _index;

        internal ShowcaseAttachmentEffect(Attachment attachment, int index)
        {
            _attachment = attachment;
            _index = index;
        }

        public int index => _index;
        public string type => IsValid ? Effect.mEffectType.ToString() : string.Empty;
        public double effect_id
        {
            get => IsValid ? Effect.mEffectID : 0d;
            set
            {
                if (IsValid)
                {
                    Effect.mEffectID = value > 0 ? unchecked((uint)System.Math.Round(value)) : 0U;
                }
            }
        }

        public string effect_type
        {
            get => type;
            set
            {
                if (IsValid && System.Enum.TryParse(value, ignoreCase: true, out EffectType parsed))
                {
                    Effect.mEffectType = parsed;
                }
            }
        }
        public bool dont_draw_if_parent_hidden
        {
            get => IsValid && Effect.mDontDrawIfParentHidden;
            set
            {
                if (IsValid)
                {
                    Effect.mDontDrawIfParentHidden = value;
                }
            }
        }

        public bool dont_propagate_color
        {
            get => IsValid && Effect.mDontPropogateColor;
            set
            {
                if (IsValid)
                {
                    Effect.mDontPropogateColor = value;
                }
            }
        }

        public bool dont_propogate_color
        {
            get => dont_propagate_color;
            set => dont_propagate_color = value;
        }

        public ShowcaseMatrix offset_matrix()
        {
            return IsValid ? new ShowcaseMatrix(Effect.mOffset) : null;
        }

        public ShowcaseMatrix get_offset()
        {
            return offset_matrix();
        }

        public ShowcaseMatrix get_offset(ShowcaseMatrix matrix)
        {
            ShowcaseMatrix offset = offset_matrix();
            if (matrix is null || offset is null)
            {
                return offset;
            }

            return matrix.copy_from(offset);
        }

        public ShowcaseAttachmentEffect set_offset(double x, double y)
        {
            if (IsValid)
            {
                Effect.mOffset.M41 = (float)x;
                Effect.mOffset.M42 = (float)y;
            }

            return this;
        }

        public ShowcaseAttachmentEffect set_offset_matrix(
            double m11,
            double m12,
            double m21,
            double m22,
            double x,
            double y)
        {
            if (IsValid)
            {
                Effect.mOffset = Matrix4x4.Identity;
                Effect.mOffset.M11 = (float)m11;
                Effect.mOffset.M12 = (float)m12;
                Effect.mOffset.M21 = (float)m21;
                Effect.mOffset.M22 = (float)m22;
                Effect.mOffset.M41 = (float)x;
                Effect.mOffset.M42 = (float)y;
            }

            return this;
        }

        public ShowcaseAttachmentEffect set_offset(ShowcaseMatrix matrix)
        {
            if (IsValid && matrix is not null)
            {
                Effect.mOffset = matrix.ToMatrix4x4();
            }

            return this;
        }

        public bool is_valid()
        {
            return IsValid && Effect.mEffectType != EffectType.Other;
        }

        private ref AttachEffect Effect => ref _attachment.mEffectArray[_index];
        private bool IsValid => _attachment is not null && _index >= 0 && _index < _attachment.mNumEffects;

        internal AttachEffect Snapshot()
        {
            return IsValid ? Effect : default;
        }
    }
}
