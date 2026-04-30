using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace EffectViewer.TodLib.Common
{
    internal enum DefFieldType
    {
        Invalid,
        Int,
        Float,
        String,
        Enum,
        Array,
        TrackFloat,
        Flags,
        Image,
        Font
    }

    internal readonly record struct DefSymbol<T>(int Value, string Name);

    internal sealed class DefMap<T>
    {
        public DefMap(Func<T> constructor, params IDefField<T>[] fields)
        {
            Constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public Func<T> Constructor { get; }
        public IReadOnlyList<IDefField<T>> Fields { get; }
    }

    internal interface IDefField<T>
    {
        string Name { get; }
        DefFieldType FieldType { get; }
        bool TryRead(SexyXmlParser parser, string elementName, ref T definition);
    }

    internal delegate void DefFieldSetter<T, in TValue>(ref T definition, TValue value);
    internal delegate FloatParameterTrack DefTrackGetter<T>(ref T definition);
    internal delegate void DefItemAppender<T, in TItem>(ref T definition, TItem item);
    internal delegate void DefFlagSetter<T>(ref T definition, int bitIndex, bool value);

    internal static class DefinitionMapLoader
    {
        public static T Load<T>(SexyXmlParser parser, DefMap<T> defMap)
        {
            T definition = defMap.Constructor();
            LoadMap(parser, defMap, ref definition);
            return definition;
        }

        public static T Load<T>(Stream stream, DefMap<T> defMap, string fileName = "")
        {
            return Load(SexyXmlParser.FromStream(stream, fileName), defMap);
        }

        public static T LoadFile<T>(string fileName, DefMap<T> defMap)
        {
            return Load(SexyXmlParser.FromFile(fileName), defMap);
        }

        public static void LoadMap<T>(SexyXmlParser parser, DefMap<T> defMap, ref T definition)
        {
            bool done = false;
            while (!done)
            {
                ReadField(parser, defMap, ref definition, ref done);
            }
        }

        public static IDefField<T> Int<T>(string name, DefFieldSetter<T, int> setter)
        {
            return new DefField<T, int>(name, DefFieldType.Int, (_, parser) => ReadIntField(parser), setter);
        }

        public static IDefField<T> Float<T>(string name, DefFieldSetter<T, float> setter)
        {
            return new DefField<T, float>(name, DefFieldType.Float, (_, parser) => ReadFloatField(parser), setter);
        }

        public static IDefField<T> String<T>(string name, DefFieldSetter<T, string> setter)
        {
            return new DefField<T, string>(name, DefFieldType.String, (_, parser) => ReadStringField(parser), setter);
        }

        public static IDefField<T> Image<T>(string name, DefFieldSetter<T, string> setter)
        {
            return new DefField<T, string>(name, DefFieldType.Image, (_, parser) => ReadImageField(parser), setter);
        }

        public static IDefField<T> Font<T>(string name, DefFieldSetter<T, string> setter)
        {
            return new DefField<T, string>(name, DefFieldType.Font, (_, parser) => ReadImageField(parser), setter);
        }

        public static IDefField<T> Enum<T, TEnum>(string name, IReadOnlyDictionary<string, TEnum> symbols, DefFieldSetter<T, TEnum> setter)
            where TEnum : struct
        {
            return new DefField<T, TEnum>(name, DefFieldType.Enum, (_, parser) =>
            {
                string value = ReadXmlString(parser);
                if (symbols != null && symbols.TryGetValue(value, out TEnum symbolValue))
                {
                    return symbolValue;
                }

                if (System.Enum.TryParse(value, ignoreCase: true, out TEnum enumValue))
                {
                    return enumValue;
                }

                throw parser.CreateError($"Can't parse enum value '{value}'");
            }, setter);
        }

        public static TrackFloatDefField<T> TrackFloat<T>(string name, DefTrackGetter<T> getter)
        {
            return new TrackFloatDefField<T>(name, getter);
        }

        public static ArrayDefField<T, TItem> Array<T, TItem>(string name, DefMap<TItem> itemMap, DefItemAppender<T, TItem> append)
        {
            return new ArrayDefField<T, TItem>(name, itemMap, append);
        }

        public static FlagsDefField<T> Flags<T>(string name, IReadOnlyDictionary<string, int> symbols, DefFlagSetter<T> setter)
        {
            return new FlagsDefField<T>(name, symbols, setter);
        }

        public static void ReadFloatTrackField(SexyXmlParser parser, FloatParameterTrack track)
        {
            ReadFloatTrack(ReadXmlString(parser), track);
        }

        public static void ReadFloatTrack(string text, FloatParameterTrack track)
        {
            if (track is null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            text ??= string.Empty;
            List<FloatParameterTrackNode> nodes = [];
            int i = 0;
            while (true)
            {
                SkipWhitespace(text, ref i);
                if (i >= text.Length)
                {
                    break;
                }

                FloatParameterTrackNode node = new()
                {
                    mTime = -10000f,
                    mCurveType = TodCurves.Linear,
                    mDistribution = TodCurves.Linear
                };

                if (text[i] == '[')
                {
                    i++;
                    int start = i;
                    while (i < text.Length && text[i] != ']')
                    {
                        i++;
                    }

                    if (i >= text.Length)
                    {
                        throw new FormatException("Unterminated float track value range.");
                    }

                    ReadValueRange(text[start..i], node);
                    i++;
                }
                else
                {
                    node.mLowValue = ReadFloatToken(text, ref i);
                    node.mHighValue = node.mLowValue;
                    node.mDistribution = TodCurves.Linear;
                }

                SkipWhitespace(text, ref i);
                if (i < text.Length && text[i] == ',')
                {
                    i++;
                    SkipWhitespace(text, ref i);
                    node.mTime = ReadFloatToken(text, ref i);
                }

                SkipWhitespace(text, ref i);
                if (i < text.Length && IsIdentifierStart(text[i]))
                {
                    string curveName = ReadIdentifier(text, ref i);
                    node.mCurveType = ParseCurve(curveName);
                }

                nodes.Add(node);
            }

            track.mNodes = [.. nodes];
            track.mCountNodes = nodes.Count;
            FillDefaultTrackTimes(track);
        }

        private static void ReadField<T>(SexyXmlParser parser, DefMap<T> defMap, ref T definition, ref bool done)
        {
            if (!parser.TryNextElement(out SexyXmlElement element) || element.Type == SexyXmlElementType.End)
            {
                done = true;
                return;
            }

            if (element.Type != SexyXmlElementType.Start)
            {
                throw parser.CreateError("Missing element start");
            }

            foreach (IDefField<T> field in defMap.Fields)
            {
                if (field.TryRead(parser, element.Value, ref definition))
                {
                    return;
                }
            }

            throw parser.CreateError($"Ignoring unknown element '{element.Value}'");
        }

        private static string ReadXmlString(SexyXmlParser parser)
        {
            if (!parser.TryNextElement(out SexyXmlElement element))
            {
                throw parser.CreateError("Missing element value");
            }

            if (element.Type == SexyXmlElementType.End)
            {
                return string.Empty;
            }

            if (element.Type != SexyXmlElementType.Element)
            {
                throw parser.CreateError("unknown element type");
            }

            string value = element.Value;
            if (!parser.TryNextElement(out element))
            {
                throw parser.CreateError("Can't read element end");
            }

            if (element.Type != SexyXmlElementType.End)
            {
                throw parser.CreateError("Missing element end");
            }

            return value;
        }

        private static int ReadIntField(SexyXmlParser parser)
        {
            string value = ReadXmlString(parser);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }

            throw parser.CreateError($"Can't parse int value '{value}'");
        }

        private static float ReadFloatField(SexyXmlParser parser)
        {
            string value = ReadXmlString(parser);
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            throw parser.CreateError($"Can't parse float value '{value}'");
        }

        private static string ReadStringField(SexyXmlParser parser)
        {
            return ReadXmlString(parser);
        }

        private static string ReadImageField(SexyXmlParser parser)
        {
            string value = ReadXmlString(parser);
            return value.Length == 0 ? null : value;
        }

        private static bool ReadFlagValue(SexyXmlParser parser)
        {
            string value = ReadXmlString(parser);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                return intValue != 0;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
            {
                return floatValue != 0f;
            }

            throw parser.CreateError($"Can't parse int value '{value}'");
        }

        private static void ReadValueRange(string text, FloatParameterTrackNode node)
        {
            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                throw new FormatException("Empty float track value range.");
            }

            node.mLowValue = float.Parse(parts[0], CultureInfo.InvariantCulture);
            if (parts.Length == 1)
            {
                node.mHighValue = node.mLowValue;
                node.mDistribution = TodCurves.Constant;
                return;
            }

            if (parts.Length == 2)
            {
                node.mHighValue = float.Parse(parts[1], CultureInfo.InvariantCulture);
                node.mDistribution = TodCurves.Linear;
                return;
            }

            node.mDistribution = ParseCurve(parts[1]);
            node.mHighValue = float.Parse(parts[2], CultureInfo.InvariantCulture);
        }

        private static float ReadFloatToken(string text, ref int index)
        {
            int start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != ',')
            {
                index++;
            }

            if (start == index)
            {
                throw new FormatException("Expected float token.");
            }

            return float.Parse(text[start..index], CultureInfo.InvariantCulture);
        }

        private static string ReadIdentifier(string text, ref int index)
        {
            int start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != ',')
            {
                index++;
            }

            return text[start..index];
        }

        private static bool IsIdentifierStart(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        private static TodCurves ParseCurve(string value)
        {
            if (System.Enum.TryParse(value, ignoreCase: true, out TodCurves curve))
            {
                return curve;
            }

            throw new FormatException($"Unknown curve '{value}'.");
        }

        private static void FillDefaultTrackTimes(FloatParameterTrack track)
        {
            if (track.mCountNodes == 0)
            {
                return;
            }

            if (track.mNodes[0].mTime < -1000f)
            {
                track.mNodes[0].mTime = 0f;
            }

            if (track.mCountNodes != 1 && track.mNodes[^1].mTime < -1000f)
            {
                track.mNodes[^1].mTime = 100f;
            }

            int lastKnown = 0;
            for (int i = 1; i < track.mCountNodes; i++)
            {
                if (track.mNodes[i].mTime >= -1000f)
                {
                    float start = track.mNodes[lastKnown].mTime;
                    float step = (track.mNodes[i].mTime - start) / (i - lastKnown);
                    for (int j = lastKnown + 1; j < i; j++)
                    {
                        track.mNodes[j].mTime = start + step * (j - lastKnown);
                    }

                    lastKnown = i;
                }
            }

            for (int nodeIndex = 0; nodeIndex < track.mCountNodes; nodeIndex++)
            {
                track.mNodes[nodeIndex].mTime /= 100f;
            }
        }

        internal sealed class DefField<T, TValue> : IDefField<T>
        {
            private readonly Func<string, SexyXmlParser, TValue> _reader;
            private readonly DefFieldSetter<T, TValue> _setter;

            public DefField(string name, DefFieldType fieldType, Func<string, SexyXmlParser, TValue> reader, DefFieldSetter<T, TValue> setter)
            {
                Name = name;
                FieldType = fieldType;
                _reader = reader;
                _setter = setter;
            }

            public string Name { get; }
            public DefFieldType FieldType { get; }

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!string.Equals(elementName, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                _setter(ref definition, _reader(elementName, parser));
                return true;
            }
        }

        internal sealed class TrackFloatDefField<T> : IDefField<T>
        {
            private readonly DefTrackGetter<T> _getter;

            public TrackFloatDefField(string name, DefTrackGetter<T> getter)
            {
                Name = name;
                _getter = getter;
            }

            public string Name { get; }
            public DefFieldType FieldType => DefFieldType.TrackFloat;

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!string.Equals(elementName, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                ReadFloatTrackField(parser, _getter(ref definition));
                return true;
            }
        }

        internal sealed class ArrayDefField<T, TItem> : IDefField<T>
        {
            private readonly DefMap<TItem> _itemMap;
            private readonly DefItemAppender<T, TItem> _append;

            public ArrayDefField(string name, DefMap<TItem> itemMap, DefItemAppender<T, TItem> append)
            {
                Name = name;
                _itemMap = itemMap;
                _append = append;
            }

            public string Name { get; }
            public DefFieldType FieldType => DefFieldType.Array;

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!string.Equals(elementName, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                TItem item = _itemMap.Constructor();
                LoadMap(parser, _itemMap, ref item);
                _append(ref definition, item);
                return true;
            }
        }

        internal sealed class FlagsDefField<T> : IDefField<T>
        {
            private readonly IReadOnlyDictionary<string, int> _symbols;
            private readonly DefFlagSetter<T> _setter;

            public FlagsDefField(string name, IReadOnlyDictionary<string, int> symbols, DefFlagSetter<T> setter)
            {
                Name = name;
                _symbols = symbols;
                _setter = setter;
            }

            public string Name { get; }
            public DefFieldType FieldType => DefFieldType.Flags;

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!_symbols.TryGetValue(elementName, out int bitIndex))
                {
                    return false;
                }

                _setter(ref definition, bitIndex, ReadFlagValue(parser));
                return true;
            }
        }
    }
}
