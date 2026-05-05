using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EffectViewer.Settings
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(UserSettings))]
    [JsonSerializable(typeof(Dictionary<string, EditorLayoutSettings>))]
    internal sealed partial class UserSettingsJsonSerializerContext : JsonSerializerContext
    {
    }
}
