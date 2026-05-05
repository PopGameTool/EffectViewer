using Avalonia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EffectViewer.Localization
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        public const string ResourcePrefix = "Loc.";
        public const string EnglishLanguageCode = "en-US";
        public const string ChineseLanguageCode = "zh-CN";

        private readonly Dictionary<string, string> _fallbackStrings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _currentStrings = new(StringComparer.Ordinal);
        private string _currentLanguageCode = EnglishLanguageCode;

        private LocalizationManager()
        {
        }

        public static LocalizationManager Instance { get; } = new();

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler LanguageChanged;

        public IReadOnlyList<LocalizationLanguage> BuiltInLanguages { get; } =
        [
            new LocalizationLanguage(EnglishLanguageCode, "Localization/en-US.json"),
            new LocalizationLanguage(ChineseLanguageCode, "Localization/zh-CN.json")
        ];

        public string CurrentLanguageCode => _currentLanguageCode;
        public bool IsEnglish => string.Equals(CurrentLanguageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase);
        public bool IsChinese => string.Equals(CurrentLanguageCode, ChineseLanguageCode, StringComparison.OrdinalIgnoreCase);

        public string this[string key] => Text(key);

        public void Initialize(string languageCode = null, string customLanguageJson = null)
        {
            EnsureFallbackLoaded();
            try
            {
                if (!string.IsNullOrWhiteSpace(customLanguageJson))
                {
                    UseLanguageJson(customLanguageJson, languageCode);
                    return;
                }

                UseBuiltInLanguage(IsBuiltInLanguageCode(languageCode)
                    ? languageCode
                    : SelectInitialLanguageCode());
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                UseBuiltInLanguage(SelectInitialLanguageCode());
            }
        }

        public void UseBuiltInLanguage(string languageCode)
        {
            EnsureFallbackLoaded();
            string selectedLanguageCode = string.IsNullOrWhiteSpace(languageCode)
                ? EnglishLanguageCode
                : languageCode;
            Dictionary<string, string> loaded = string.Equals(selectedLanguageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase)
                ? new Dictionary<string, string>(_fallbackStrings, StringComparer.Ordinal)
                : LoadEmbeddedLanguage(selectedLanguageCode);

            ApplyLanguage(selectedLanguageCode, loaded);
        }

        public async Task LoadLanguageFileAsync(Stream stream, string languageCode)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            EnsureFallbackLoaded();
            Dictionary<string, string> loaded = await ReadLanguageAsync(stream);
            string selectedLanguageCode = string.IsNullOrWhiteSpace(languageCode)
                ? Text("Language.Custom")
                : languageCode;
            ApplyLanguage(selectedLanguageCode, loaded);
        }

        public void UseLanguageJson(string json, string languageCode)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                UseBuiltInLanguage(IsBuiltInLanguageCode(languageCode)
                    ? languageCode
                    : SelectInitialLanguageCode());
                return;
            }

            EnsureFallbackLoaded();
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
            Dictionary<string, string> loaded = ReadLanguage(stream);
            string selectedLanguageCode = string.IsNullOrWhiteSpace(languageCode)
                ? Text("Language.Custom")
                : languageCode;
            ApplyLanguage(selectedLanguageCode, loaded);
        }

        public string Text(string key)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return string.Empty;
            }

            if (_currentStrings.TryGetValue(normalizedKey, out string value))
            {
                return value;
            }

            if (_fallbackStrings.TryGetValue(normalizedKey, out string fallback))
            {
                return fallback;
            }

            return normalizedKey;
        }

        public string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentUICulture, Text(key), args);
        }

        private void ApplyLanguage(string languageCode, Dictionary<string, string> loaded)
        {
            _currentStrings.Clear();
            foreach ((string key, string value) in loaded)
            {
                _currentStrings[key] = value;
            }

            _currentLanguageCode = languageCode;
            ApplyAvaloniaResources();
            OnPropertyChanged(nameof(CurrentLanguageCode));
            OnPropertyChanged(nameof(IsEnglish));
            OnPropertyChanged(nameof(IsChinese));
            OnPropertyChanged("Item[]");
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyAvaloniaResources()
        {
            if (Application.Current is null)
            {
                return;
            }

            foreach (string key in _fallbackStrings.Keys.Concat(_currentStrings.Keys).Distinct(StringComparer.Ordinal))
            {
                Application.Current.Resources[ResourcePrefix + key] = Text(key);
            }
        }

        private void EnsureFallbackLoaded()
        {
            if (_fallbackStrings.Count > 0)
            {
                return;
            }

            Dictionary<string, string> english = LoadEmbeddedLanguage(EnglishLanguageCode);
            foreach ((string key, string value) in english)
            {
                _fallbackStrings[key] = value;
            }
        }

        private static Dictionary<string, string> LoadEmbeddedLanguage(string languageCode)
        {
            Assembly assembly = typeof(LocalizationManager).Assembly;
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith($".Localization.{languageCode}.json", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new FileNotFoundException($"Embedded language file '{languageCode}' was not found.");
            }

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            return ReadLanguage(stream);
        }

        private static async Task<Dictionary<string, string>> ReadLanguageAsync(Stream stream)
        {
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            return ReadLanguage(buffer);
        }

        private static Dictionary<string, string> ReadLanguage(Stream stream)
        {
            JsonDocumentOptions options = new()
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            using JsonDocument document = JsonDocument.Parse(stream, options);
            JsonElement stringsElement = document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("strings", out JsonElement strings)
                    ? strings
                    : document.RootElement;

            Dictionary<string, string> values = new(StringComparer.Ordinal);
            Flatten(stringsElement, string.Empty, values);
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
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        values[prefix] = element.GetString() ?? string.Empty;
                    }
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        values[prefix] = element.ToString();
                    }
                    break;
            }
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return key.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                ? key[ResourcePrefix.Length..]
                : key;
        }

        private static string SelectInitialLanguageCode()
        {
            return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
                ? ChineseLanguageCode
                : EnglishLanguageCode;
        }

        private static bool IsBuiltInLanguageCode(string languageCode)
        {
            return string.Equals(languageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(languageCode, ChineseLanguageCode, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
