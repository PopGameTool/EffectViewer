namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleField
    {
        public ParticleFieldType mFieldType;
        public readonly FloatParameterTrack mX = new();
        public readonly FloatParameterTrack mY = new();

        public ParticleField()
        {
            mFieldType = ParticleFieldType.Invalid;
        }
    }
}