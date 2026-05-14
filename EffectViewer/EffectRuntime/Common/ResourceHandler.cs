namespace EffectViewer.EffectRuntime.Common
{
    public interface IResourceProvider
    {
        Image GetImage(string id);
        Font GetFont(string id);
    }

    public static class ResourceHandler
    {
        private static IResourceProvider sProvider;

        public static void SetProvider(IResourceProvider provider)
        {
            sProvider = provider;
        }

        public static Image GetImage(string id)
        {
            return sProvider?.GetImage(id);
        }

        public static Font GetFont(string id)
        {
            return sProvider?.GetFont(id);
        }
    }
}
