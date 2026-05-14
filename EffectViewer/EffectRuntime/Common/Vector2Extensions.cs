namespace EffectViewer.EffectRuntime.Common
{
    public static class Vector2Extensions
    {
        public static Vector2 GetPerp(this Vector2 vector)
        {
            return new Vector2(-vector.Y, vector.X);
        }

        public static Vector2 NormalizeSafe(this Vector2 vector)
        {
            float length = vector.Length();
            return length != 0f ? vector / length : vector;
        }
    }
}
