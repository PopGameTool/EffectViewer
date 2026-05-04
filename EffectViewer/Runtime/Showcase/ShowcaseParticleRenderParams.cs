using EffectViewer.TodLib.Particle;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticleRenderParams
    {
        internal ShowcaseParticleRenderParams(ParticleRenderParams value)
        {
            red_is_set = value.mRedIsSet;
            green_is_set = value.mGreenIsSet;
            blue_is_set = value.mBlueIsSet;
            alpha_is_set = value.mAlphaIsSet;
            particle_scale_is_set = value.mParticleScaleIsSet;
            particle_stretch_is_set = value.mParticleStretchIsSet;
            spin_position_is_set = value.mSpinPositionIsSet;
            position_is_set = value.mPositionIsSet;
            red = value.mRed;
            green = value.mGreen;
            blue = value.mBlue;
            alpha = value.mAlpha;
            particle_scale = value.mParticleScale;
            particle_stretch = value.mParticleStretch;
            spin_position = value.mSpinPosition;
            pos_x = value.mPosX;
            pos_y = value.mPosY;
        }

        public bool red_is_set { get; }
        public bool green_is_set { get; }
        public bool blue_is_set { get; }
        public bool alpha_is_set { get; }
        public bool particle_scale_is_set { get; }
        public bool particle_stretch_is_set { get; }
        public bool spin_position_is_set { get; }
        public bool position_is_set { get; }
        public double red { get; }
        public double green { get; }
        public double blue { get; }
        public double alpha { get; }
        public double particle_scale { get; }
        public double particle_stretch { get; }
        public double spin_position { get; }
        public double pos_x { get; }
        public double pos_y { get; }
    }
}
