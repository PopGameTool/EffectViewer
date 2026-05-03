using EffectViewer.Projects;

namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseResourceReference
    {
        public ShowcaseResourceReference(EffectAssetKind kind, string id)
        {
            Kind = kind;
            Id = id ?? string.Empty;
            DisplayName = $"{KindLabel(kind)}: {Id}";
            InsertText = $"\"{EscapeLuaString(Id)}\"";
        }

        public EffectAssetKind Kind { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string InsertText { get; }

        public override string ToString()
        {
            return DisplayName;
        }

        private static string KindLabel(EffectAssetKind kind)
        {
            return kind switch
            {
                EffectAssetKind.Image => "image",
                EffectAssetKind.Reanim => "reanim",
                EffectAssetKind.Particle => "particle",
                EffectAssetKind.Trail => "trail",
                EffectAssetKind.Showcase => "showcase",
                _ => kind.ToString().ToLowerInvariant()
            };
        }

        private static string EscapeLuaString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
