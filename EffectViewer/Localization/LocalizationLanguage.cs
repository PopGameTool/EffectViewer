namespace EffectViewer.Localization
{
    public sealed class LocalizationLanguage
    {
        public LocalizationLanguage(string code, string resourceName)
        {
            Code = code;
            ResourceName = resourceName;
        }

        public string Code { get; }
        public string ResourceName { get; }
    }
}
