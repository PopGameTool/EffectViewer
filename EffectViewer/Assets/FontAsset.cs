using EffectViewer.Projects;
using System.Text.Json.Serialization;

namespace EffectViewer.Assets
{
    public sealed class FontAsset : EffectAsset
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool TrueType { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FontSize { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int BorderSize { get; set; }
    }
}
