# EffectViewer language files

EffectViewer ships with `en-US.json` and `zh-CN.json`. Users can create another JSON file with the same shape and load it from **Language > Load Language File...**.

The app reads values from the top-level `strings` object. Nested objects are flattened with dots, so this:

```json
{
  "languageCode": "ja-JP",
  "languageName": "Japanese",
  "strings": {
    "Common": {
      "Save": "Save"
    }
  }
}
```

defines the key `Common.Save`. Missing keys fall back to English.
