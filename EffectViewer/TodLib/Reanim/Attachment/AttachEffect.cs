namespace EffectViewer.TodLib.Reanim.Attachment
{
    public struct AttachEffect
    {
        public uint mEffectID;
        public EffectType mEffectType;
        public Matrix4x4 mOffset;
        public bool mDontDrawIfParentHidden;
        public bool mDontPropogateColor;

        public AttachEffect()
        {
            Reset();
        }

        public void Reset()
        {
            mEffectID = 0;
            mEffectType = EffectType.Particle;
            mOffset = default;
            mDontDrawIfParentHidden = false;
            mDontPropogateColor = false;
        }
    }
}