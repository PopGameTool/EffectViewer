using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EffectViewer.TodLib.Graphics
{
    public static class ImageFontDescriptorReader
    {
        public static Font Load(string id, string descriptorPath, Func<string, Image> imageResolver)
        {
            if (string.IsNullOrWhiteSpace(descriptorPath) || !File.Exists(descriptorPath))
            {
                return null;
            }

            string text = File.ReadAllText(descriptorPath, Encoding.UTF8);
            FontDescriptor descriptor = Parse(text, imageResolver);
            if (descriptor.Layers.Count == 0)
            {
                return null;
            }

            Font font = new()
            {
                mId = id ?? string.Empty,
                mDefaultPointSize = descriptor.DefaultPointSize,
                mPointSize = descriptor.DefaultPointSize
            };
            font.SetCharMap(descriptor.CharMap);
            font.Layers.AddRange(descriptor.Layers);
            font.RecalculateMetrics();
            return font;
        }

        private static FontDescriptor Parse(string text, Func<string, Image> imageResolver)
        {
            FontDescriptor descriptor = new();
            Dictionary<string, List<string>> stringDefines = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<int>> intDefines = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<Rectangle>> rectDefines = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<Point>> pointDefines = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FontLayer> layers = new(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in Regex.Matches(text, @"Define\s+([A-Za-z0-9_]+)\s*\((.*?)\)\s*;", RegexOptions.Singleline))
            {
                string name = match.Groups[1].Value;
                string body = match.Groups[2].Value;
                if (name.StartsWith("CharList", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("KerningPairs", StringComparison.OrdinalIgnoreCase))
                {
                    stringDefines[name] = ParseStringList(body);
                }
                else if (name.StartsWith("WidthList", StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith("KerningValues", StringComparison.OrdinalIgnoreCase))
                {
                    intDefines[name] = ParseIntList(body);
                }
                else if (name.StartsWith("RectList", StringComparison.OrdinalIgnoreCase))
                {
                    rectDefines[name] = ParseRectList(body);
                }
                else if (name.StartsWith("OffsetList", StringComparison.OrdinalIgnoreCase))
                {
                    pointDefines[name] = ParsePointList(body);
                }
            }

            foreach (Match match in Regex.Matches(text, @"^\s*([A-Za-z][A-Za-z0-9_]*)\s+([^;\r\n]+)\s*;", RegexOptions.Multiline))
            {
                string command = match.Groups[1].Value;
                List<string> args = TokenizeArguments(match.Groups[2].Value);
                if (args.Count == 0)
                {
                    continue;
                }

                if (command.Equals("CreateLayer", StringComparison.OrdinalIgnoreCase))
                {
                    string layerName = args[0];
                    if (!layers.ContainsKey(layerName))
                    {
                        FontLayer layer = new() { Name = layerName };
                        layers[layerName] = layer;
                        descriptor.Layers.Add(layer);
                    }

                    continue;
                }

                if (command.Equals("SetDefaultPointSize", StringComparison.OrdinalIgnoreCase))
                {
                    descriptor.DefaultPointSize = ParseInt(args[0]);
                    continue;
                }

                if (command.Equals("SetCharMap", StringComparison.OrdinalIgnoreCase) &&
                    args.Count >= 2 &&
                    stringDefines.TryGetValue(args[0], out List<string> fromChars) &&
                    stringDefines.TryGetValue(args[1], out List<string> toChars))
                {
                    int count = Math.Min(fromChars.Count, toChars.Count);
                    for (int i = 0; i < count; i++)
                    {
                        if (TryGetSingleChar(fromChars[i], out char fromChar) &&
                            TryGetSingleChar(toChars[i], out char toChar))
                        {
                            descriptor.CharMap[fromChar] = toChar;
                        }
                    }

                    continue;
                }

                if (!command.StartsWith("Layer", StringComparison.OrdinalIgnoreCase) || args.Count < 2)
                {
                    continue;
                }

                if (!layers.TryGetValue(args[0], out FontLayer targetLayer))
                {
                    continue;
                }

                ApplyLayerCommand(command, args, targetLayer, stringDefines, intDefines, rectDefines, pointDefines, imageResolver);
            }

            return descriptor;
        }

        private static void ApplyLayerCommand(
            string command,
            List<string> args,
            FontLayer layer,
            IReadOnlyDictionary<string, List<string>> stringDefines,
            IReadOnlyDictionary<string, List<int>> intDefines,
            IReadOnlyDictionary<string, List<Rectangle>> rectDefines,
            IReadOnlyDictionary<string, List<Point>> pointDefines,
            Func<string, Image> imageResolver)
        {
            string value = args.Count > 1 ? args[1] : string.Empty;
            if (command.Equals("LayerSetImage", StringComparison.OrdinalIgnoreCase))
            {
                layer.ImageName = value;
                layer.Image = imageResolver?.Invoke(value);
            }
            else if (command.Equals("LayerSetBaseOrder", StringComparison.OrdinalIgnoreCase))
            {
                layer.BaseOrder = ParseInt(value);
            }
            else if (command.Equals("LayerSetAscent", StringComparison.OrdinalIgnoreCase))
            {
                layer.Ascent = ParseInt(value);
            }
            else if (command.Equals("LayerSetAscentPadding", StringComparison.OrdinalIgnoreCase))
            {
                layer.AscentPadding = ParseInt(value);
            }
            else if (command.Equals("LayerSetHeight", StringComparison.OrdinalIgnoreCase))
            {
                layer.Height = ParseInt(value);
            }
            else if (command.Equals("LayerSetLineSpacingOffset", StringComparison.OrdinalIgnoreCase))
            {
                layer.LineSpacingOffset = ParseInt(value);
            }
            else if (command.Equals("LayerSetPointSize", StringComparison.OrdinalIgnoreCase))
            {
                layer.PointSize = ParseInt(value);
            }
            else if (command.Equals("LayerSetDrawMode", StringComparison.OrdinalIgnoreCase))
            {
                layer.DrawMode = ParseInt(value);
            }
            else if (command.Equals("LayerSetSpacing", StringComparison.OrdinalIgnoreCase))
            {
                layer.Spacing = ParseInt(value);
            }
            else if (command.Equals("LayerSetColorMult", StringComparison.OrdinalIgnoreCase))
            {
                layer.ColorMult = ParseColor(value);
            }
            else if (command.Equals("LayerSetColorAdd", StringComparison.OrdinalIgnoreCase))
            {
                layer.ColorAdd = ParseColor(value);
            }
            else if (command.Equals("LayerSetOffset", StringComparison.OrdinalIgnoreCase))
            {
                layer.Offset = ParsePoint(value);
            }
            else if (command.Equals("LayerSetCharWidths", StringComparison.OrdinalIgnoreCase) &&
                     args.Count >= 3 &&
                     stringDefines.TryGetValue(args[1], out List<string> chars) &&
                     intDefines.TryGetValue(args[2], out List<int> widths))
            {
                int count = Math.Min(chars.Count, widths.Count);
                for (int i = 0; i < count; i++)
                {
                    if (TryGetSingleChar(chars[i], out char c))
                    {
                        layer.GetCharData(c).Width = widths[i];
                    }
                }
            }
            else if (command.Equals("LayerSetImageMap", StringComparison.OrdinalIgnoreCase) &&
                     args.Count >= 3 &&
                     stringDefines.TryGetValue(args[1], out List<string> imageMapChars) &&
                     rectDefines.TryGetValue(args[2], out List<Rectangle> rects))
            {
                int count = Math.Min(imageMapChars.Count, rects.Count);
                for (int i = 0; i < count; i++)
                {
                    if (TryGetSingleChar(imageMapChars[i], out char c))
                    {
                        layer.GetCharData(c).ImageRect = rects[i];
                    }
                }

                foreach (KeyValuePair<char, FontCharData> item in layer.CharData)
                {
                    layer.DefaultHeight = Math.Max(layer.DefaultHeight, item.Value.ImageRect.Height + item.Value.Offset.Y);
                }
            }
            else if (command.Equals("LayerSetCharOffsets", StringComparison.OrdinalIgnoreCase) &&
                     args.Count >= 3 &&
                     stringDefines.TryGetValue(args[1], out List<string> offsetChars) &&
                     pointDefines.TryGetValue(args[2], out List<Point> offsets))
            {
                int count = Math.Min(offsetChars.Count, offsets.Count);
                for (int i = 0; i < count; i++)
                {
                    if (TryGetSingleChar(offsetChars[i], out char c))
                    {
                        layer.GetCharData(c).Offset = offsets[i];
                    }
                }
            }
            else if (command.Equals("LayerSetKerningPairs", StringComparison.OrdinalIgnoreCase) &&
                     args.Count >= 3 &&
                     stringDefines.TryGetValue(args[1], out List<string> pairs) &&
                     intDefines.TryGetValue(args[2], out List<int> values))
            {
                int count = Math.Min(pairs.Count, values.Count);
                for (int i = 0; i < count; i++)
                {
                    string pair = pairs[i];
                    if (pair.Length >= 2)
                    {
                        layer.GetCharData(pair[0]).SetKerning(pair[1], values[i]);
                    }
                }
            }
        }

        private static List<string> ParseStringList(string body)
        {
            List<string> values = [];
            for (int i = 0; i < body.Length; i++)
            {
                char quote = body[i];
                if (quote is not ('\'' or '"'))
                {
                    continue;
                }

                i++;
                StringBuilder builder = new();
                while (i < body.Length)
                {
                    char c = body[i++];
                    if (c == '\\' && i < body.Length)
                    {
                        builder.Append(Unescape(body[i++]));
                        continue;
                    }

                    if (c == quote)
                    {
                        break;
                    }

                    builder.Append(c);
                }

                values.Add(builder.ToString());
            }

            return values;
        }

        private static List<int> ParseIntList(string body)
        {
            List<int> values = [];
            foreach (Match match in Regex.Matches(body, @"-?\d+"))
            {
                values.Add(ParseInt(match.Value));
            }

            return values;
        }

        private static List<Rectangle> ParseRectList(string body)
        {
            List<Rectangle> values = [];
            foreach (Match match in Regex.Matches(body, @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)"))
            {
                values.Add(new Rectangle(
                    ParseInt(match.Groups[1].Value),
                    ParseInt(match.Groups[2].Value),
                    ParseInt(match.Groups[3].Value),
                    ParseInt(match.Groups[4].Value)));
            }

            return values;
        }

        private static List<Point> ParsePointList(string body)
        {
            List<Point> values = [];
            foreach (Match match in Regex.Matches(body, @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)"))
            {
                values.Add(new Point(ParseInt(match.Groups[1].Value), ParseInt(match.Groups[2].Value)));
            }

            return values;
        }

        private static List<string> TokenizeArguments(string text)
        {
            List<string> args = [];
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ',')
                {
                    continue;
                }

                if (text[i] is '\'' or '"')
                {
                    char quote = text[i++];
                    StringBuilder builder = new();
                    while (i < text.Length)
                    {
                        char c = text[i++];
                        if (c == '\\' && i < text.Length)
                        {
                            builder.Append(Unescape(text[i++]));
                            continue;
                        }

                        if (c == quote)
                        {
                            break;
                        }

                        builder.Append(c);
                    }

                    args.Add(builder.ToString());
                    continue;
                }

                if (text[i] == '(')
                {
                    int start = i;
                    while (i < text.Length && text[i] != ')')
                    {
                        i++;
                    }

                    if (i < text.Length)
                    {
                        i++;
                    }

                    args.Add(text[start..i]);
                    continue;
                }

                int tokenStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != ',')
                {
                    i++;
                }

                args.Add(text[tokenStart..i]);
            }

            return args;
        }

        private static bool TryGetSingleChar(string text, out char c)
        {
            if (!string.IsNullOrEmpty(text))
            {
                c = text[0];
                return true;
            }

            c = '\0';
            return false;
        }

        private static Point ParsePoint(string value)
        {
            Match match = Regex.Match(value ?? string.Empty, @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)");
            return match.Success
                ? new Point(ParseInt(match.Groups[1].Value), ParseInt(match.Groups[2].Value))
                : Point.Empty;
        }

        private static SexyColor ParseColor(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.StartsWith("(", StringComparison.Ordinal))
            {
                List<int> values = ParseIntList(trimmed);
                if (values.Count >= 4)
                {
                    return new SexyColor(values[0], values[1], values[2], values[3]);
                }
            }

            int color = ParseInt(trimmed);
            int alpha = (color >> 24) & 0xFF;
            if (alpha == 0)
            {
                alpha = 0xFF;
            }

            return new SexyColor((color >> 16) & 0xFF, (color >> 8) & 0xFF, color & 0xFF, alpha);
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, out int result) ? result : 0;
        }

        private static char Unescape(char c)
        {
            return c switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => c
            };
        }

        private sealed class FontDescriptor
        {
            public int DefaultPointSize { get; set; }
            public List<FontLayer> Layers { get; } = [];
            public Dictionary<char, char> CharMap { get; } = [];
        }
    }
}
