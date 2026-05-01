using System.Collections.Generic;
using System.Text.Json.Serialization;
using EffectViewer.Assets;

namespace EffectViewer.Projects
{
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = true)]
    [JsonSerializable(typeof(ProjectManifest))]
    [JsonSerializable(typeof(List<ImageAsset>))]
    [JsonSerializable(typeof(List<ReanimAsset>))]
    [JsonSerializable(typeof(List<ReanimTween>))]
    [JsonSerializable(typeof(List<EffectAsset>))]
    [JsonSerializable(typeof(List<ShowcaseAsset>))]
    internal sealed partial class ProjectJsonSerializerContext : JsonSerializerContext
    {
    }
}
