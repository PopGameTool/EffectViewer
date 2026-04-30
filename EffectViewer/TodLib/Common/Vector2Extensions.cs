namespace EffectViewer.TodLib.Common
{
    public static class Vector2Extensions
    {
        public static Vector2 GetPerp(this Vector2 vector)
        {
            return new Vector2(vector.Y, -vector.X);
        }
    }
}
