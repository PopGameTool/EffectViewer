using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EffectViewer.Localization;

namespace EffectViewer.Tests.Localization;

public sealed class LocalizationManagerTests
{
    [Fact]
    public void BuiltInLanguageResourcesExposeTheSameStringKeys()
    {
        Dictionary<string, string> english = LoadEmbeddedStrings(LocalizationManager.EnglishLanguageCode);
        Dictionary<string, string> chinese = LoadEmbeddedStrings(LocalizationManager.ChineseLanguageCode);

        Assert.NotEmpty(english);
        Assert.Equal(
            english.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            chinese.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void BuiltInLanguageSwitchUsesEmbeddedStringsAndKeyNormalization()
    {
        LocalizationManager manager = LocalizationManager.Instance;

        manager.UseBuiltInLanguage(LocalizationManager.ChineseLanguageCode);
        Assert.True(manager.IsChinese);
        Assert.Equal("保存", manager.Text("Common.Save"));

        manager.UseBuiltInLanguage(LocalizationManager.EnglishLanguageCode);
        Assert.True(manager.IsEnglish);
        Assert.Equal("Save", manager.Text("Loc.Common.Save"));
        Assert.Equal("Missing.Key", manager.Text("Missing.Key"));
    }

    [Fact]
    public async Task CustomLanguageFileFlattensStringsAndFallsBackToEnglish()
    {
        LocalizationManager manager = LocalizationManager.Instance;
        string json = """
            {
              "strings": {
                "Common": {
                  "Save": "Guardar",
                  "Enabled": true
                }
              }
            }
            """;

        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        await manager.LoadLanguageFileAsync(stream, "test");

        Assert.Equal("test", manager.CurrentLanguageCode);
        Assert.Equal("Guardar", manager.Text("Common.Save"));
        Assert.Equal("True", manager.Text("Common.Enabled"));
        Assert.Equal("Open", manager.Text("Common.Open"));

        manager.UseBuiltInLanguage(LocalizationManager.EnglishLanguageCode);
    }

    private static Dictionary<string, string> LoadEmbeddedStrings(string languageCode)
    {
        Assembly assembly = typeof(LocalizationManager).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith($".Localization.{languageCode}.json", StringComparison.OrdinalIgnoreCase));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(resourceName);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement.TryGetProperty("strings", out JsonElement strings)
            ? strings
            : document.RootElement;

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        Flatten(root, string.Empty, values);
        return values;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string key = string.IsNullOrWhiteSpace(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";
                    Flatten(property.Value, key, values);
                }
                break;

            case JsonValueKind.String:
                values[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                values[prefix] = element.ToString();
                break;
        }
    }
}
