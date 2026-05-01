using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EffectViewer.TodLib.Common
{
    internal enum DefFieldType
    {
        Invalid = 0,
        Int = 1,
        Float = 2,
        String = 3,
        Enum = 4,
        Array = 6,
        TrackFloat = 7,
        Flags = 8,
        Image = 9,
        Font = 10
    }

    internal readonly record struct DefSymbol(int Value, string Name);

    internal sealed class DefMap<T>
    {
        public DefMap(Func<T> constructor, params IDefField<T>[] fields)
            : this(0, constructor, fields)
        {
        }

        public DefMap(int compiledSize, Func<T> constructor, params IDefField<T>[] fields)
        {
            CompiledSize = compiledSize;
            Constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public int CompiledSize { get; }
        public Func<T> Constructor { get; }
        public IReadOnlyList<IDefField<T>> Fields { get; }
    }

    internal interface IDefField<T>
    {
        string Name { get; }
        DefFieldType FieldType { get; }
        int CompiledOffset { get; }
        bool TryRead(SexyXmlParser parser, string elementName, ref T definition);
        void Write(SexyXmlWriter writer, ref T definition);
        void ReadCompiled(CompiledDefinitionReader reader, ReadOnlySpan<byte> rawDefinition, ref T definition);
        void WriteCompiledRaw(Span<byte> rawDefinition, ref T definition);
        void WriteCompiledExtra(CompiledDefinitionWriter writer, ref T definition);
        void AppendCompiledSchema(ref uint schemaHash, HashSet<object> progressMaps);
    }

    internal delegate void DefFieldSetter<T, in TValue>(ref T definition, TValue value);
    internal delegate TValue DefFieldGetter<T, out TValue>(ref T definition);
    internal delegate bool DefValueShouldWrite<in TValue>(TValue value);
    internal delegate FloatParameterTrack DefTrackGetter<T>(ref T definition);
    internal delegate void DefItemAppender<T, in TItem>(ref T definition, TItem item);
    internal delegate int DefArrayCountGetter<T>(ref T definition);
    internal delegate TItem DefArrayItemGetter<T, out TItem>(ref T definition, int index);
    internal delegate void DefFlagSetter<T>(ref T definition, int bitIndex, bool value);
    internal delegate int DefFlagGetter<T>(ref T definition);

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
            if (CompiledDefinitionFormat.IsCompiled(stream))
            {
                return CompiledDefinitionFormat.Load(stream, defMap, fileName);
            }

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

        public static void Save<T>(Stream stream, DefMap<T> defMap, T definition)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using StreamWriter streamWriter = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
            Save(streamWriter, defMap, definition);
        }

        public static void Save<T>(Stream stream, DefMap<T> defMap, T definition, string fileName)
        {
            if (IsCompiledFileName(fileName))
            {
                SaveCompiled(stream, defMap, definition);
                return;
            }

            Save(stream, defMap, definition);
        }

        public static void SaveCompiled<T>(Stream stream, DefMap<T> defMap, T definition)
        {
            CompiledDefinitionFormat.Save(stream, defMap, definition);
        }

        public static void Save<T>(TextWriter textWriter, DefMap<T> defMap, T definition)
        {
            SexyXmlWriter writer = new(textWriter);
            SaveMap(writer, defMap, ref definition);
            writer.Flush();
        }

        public static void SaveFile<T>(string fileName, DefMap<T> defMap, T definition)
        {
            using FileStream stream = File.Create(fileName);
            Save(stream, defMap, definition, fileName);
        }

        public static bool IsCompiledFileName(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName), ".compiled", StringComparison.OrdinalIgnoreCase);
        }

        public static void SaveMap<T>(SexyXmlWriter writer, DefMap<T> defMap, ref T definition)
        {
            foreach (IDefField<T> field in defMap.Fields)
            {
                field.Write(writer, ref definition);
            }
        }

        public static IDefField<T> Int<T>(string name, DefFieldSetter<T, int> setter)
        {
            return Int(name, setter, null);
        }

        public static IDefField<T> Int<T>(string name, DefFieldSetter<T, int> setter, DefFieldGetter<T, int> getter, DefValueShouldWrite<int> shouldWrite = null, int compiledOffset = -1)
        {
            return new DefField<T, int>(
                name,
                DefFieldType.Int,
                compiledOffset,
                null,
                (_, parser) => ReadIntField(parser),
                setter,
                getter,
                value => value.ToString(CultureInfo.InvariantCulture),
                shouldWrite ?? (static value => value != 0));
        }

        public static IDefField<T> Float<T>(string name, DefFieldSetter<T, float> setter)
        {
            return Float(name, setter, null);
        }

        public static IDefField<T> Float<T>(string name, DefFieldSetter<T, float> setter, DefFieldGetter<T, float> getter, DefValueShouldWrite<float> shouldWrite = null, int compiledOffset = -1)
        {
            return new DefField<T, float>(
                name,
                DefFieldType.Float,
                compiledOffset,
                null,
                (_, parser) => ReadFloatField(parser),
                setter,
                getter,
                FormatFloat,
                shouldWrite ?? (static value => value != 0f));
        }

        public static IDefField<T> String<T>(string name, DefFieldSetter<T, string> setter)
        {
            return String(name, setter, null);
        }

        public static IDefField<T> String<T>(string name, DefFieldSetter<T, string> setter, DefFieldGetter<T, string> getter, DefValueShouldWrite<string> shouldWrite = null, int compiledOffset = -1)
        {
            return new DefField<T, string>(
                name,
                DefFieldType.String,
                compiledOffset,
                null,
                (_, parser) => ReadStringField(parser),
                setter,
                getter,
                static value => value ?? string.Empty,
                shouldWrite ?? (static value => !string.IsNullOrEmpty(value)));
        }

        public static IDefField<T> Image<T>(string name, DefFieldSetter<T, string> setter)
        {
            return Image(name, setter, null);
        }

        public static IDefField<T> Image<T>(string name, DefFieldSetter<T, string> setter, DefFieldGetter<T, string> getter, DefValueShouldWrite<string> shouldWrite = null, int compiledOffset = -1)
        {
            return new DefField<T, string>(
                name,
                DefFieldType.Image,
                compiledOffset,
                null,
                (_, parser) => ReadImageField(parser),
                setter,
                getter,
                static value => value ?? string.Empty,
                shouldWrite ?? (static value => !string.IsNullOrEmpty(value)));
        }

        public static IDefField<T> Font<T>(string name, DefFieldSetter<T, string> setter)
        {
            return Font(name, setter, null);
        }

        public static IDefField<T> Font<T>(string name, DefFieldSetter<T, string> setter, DefFieldGetter<T, string> getter, DefValueShouldWrite<string> shouldWrite = null, int compiledOffset = -1)
        {
            return new DefField<T, string>(
                name,
                DefFieldType.Font,
                compiledOffset,
                null,
                (_, parser) => ReadImageField(parser),
                setter,
                getter,
                static value => value ?? string.Empty,
                shouldWrite ?? (static value => !string.IsNullOrEmpty(value)));
        }

        public static IDefField<T> Enum<T, TEnum>(string name, IReadOnlyDictionary<string, TEnum> symbols, DefFieldSetter<T, TEnum> setter)
            where TEnum : struct
        {
            return Enum(name, symbols, setter, null);
        }

        public static IDefField<T> Enum<T, TEnum>(
            string name,
            IReadOnlyDictionary<string, TEnum> symbols,
            DefFieldSetter<T, TEnum> setter,
            DefFieldGetter<T, TEnum> getter,
            DefValueShouldWrite<TEnum> shouldWrite = null,
            int compiledOffset = -1,
            IReadOnlyList<DefSymbol> compiledSymbols = null)
            where TEnum : struct
        {
            return new DefField<T, TEnum>(
                name,
                DefFieldType.Enum,
                compiledOffset,
                compiledSymbols ?? CreateCompiledSymbols(symbols),
                (_, parser) =>
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
                },
                setter,
                getter,
                value => FormatEnum(value, symbols),
                shouldWrite ?? (static value => !EqualityComparer<TEnum>.Default.Equals(value, default)));
        }

        public static TrackFloatDefField<T> TrackFloat<T>(string name, DefTrackGetter<T> getter, int compiledOffset = -1)
        {
            return new TrackFloatDefField<T>(name, getter, null, compiledOffset);
        }

        public static TrackFloatDefField<T> TrackFloat<T>(string name, DefTrackGetter<T> getter, float defaultValue, int compiledOffset = -1)
        {
            return new TrackFloatDefField<T>(name, getter, defaultValue, compiledOffset);
        }

        public static ArrayDefField<T, TItem> Array<T, TItem>(string name, DefMap<TItem> itemMap, DefItemAppender<T, TItem> append)
        {
            return Array(name, itemMap, append, null, null);
        }

        public static ArrayDefField<T, TItem> Array<T, TItem>(
            string name,
            DefMap<TItem> itemMap,
            DefItemAppender<T, TItem> append,
            DefArrayCountGetter<T> countGetter,
            DefArrayItemGetter<T, TItem> itemGetter,
            int compiledOffset = -1)
        {
            return new ArrayDefField<T, TItem>(name, itemMap, append, countGetter, itemGetter, compiledOffset);
        }

        public static FlagsDefField<T> Flags<T>(string name, IReadOnlyDictionary<string, int> symbols, DefFlagSetter<T> setter)
        {
            return Flags(name, symbols, setter, null);
        }

        public static FlagsDefField<T> Flags<T>(
            string name,
            IReadOnlyDictionary<string, int> symbols,
            DefFlagSetter<T> setter,
            DefFlagGetter<T> getter,
            int compiledOffset = -1,
            IReadOnlyList<DefSymbol> compiledSymbols = null)
        {
            return new FlagsDefField<T>(name, symbols, setter, getter, compiledOffset, compiledSymbols ?? CreateCompiledSymbols(symbols));
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

        public static string WriteFloatTrack(FloatParameterTrack track)
        {
            if (track?.mNodes is null || track.mCountNodes <= 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            int count = Math.Min(track.mCountNodes, track.mNodes.Length);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                FloatParameterTrackNode node = track.mNodes[i];
                AppendTrackValue(builder, node);
                builder.Append(',');
                builder.Append(FormatFloat(node.mTime * 100f));
                if (node.mCurveType != TodCurves.Linear)
                {
                    builder.Append(' ');
                    builder.Append(node.mCurveType);
                }
            }

            return builder.ToString();
        }

        private static void AppendTrackValue(StringBuilder builder, FloatParameterTrackNode node)
        {
            bool isSingleValue = node.mLowValue == node.mHighValue;
            if (isSingleValue && node.mDistribution == TodCurves.Linear)
            {
                builder.Append(FormatFloat(node.mLowValue));
                return;
            }

            builder.Append('[');
            builder.Append(FormatFloat(node.mLowValue));
            if (!isSingleValue || node.mDistribution != TodCurves.Constant)
            {
                builder.Append(' ');
                if (node.mDistribution != TodCurves.Linear)
                {
                    builder.Append(node.mDistribution);
                    builder.Append(' ');
                }

                builder.Append(FormatFloat(node.mHighValue));
            }

            builder.Append(']');
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

        private static string FormatFloat(float value)
        {
            string text = value.ToString("0.###", CultureInfo.InvariantCulture);
            if (text == "-0")
            {
                return "0";
            }

            if (text.StartsWith("0.", StringComparison.Ordinal))
            {
                return text[1..];
            }

            if (text.StartsWith("-0.", StringComparison.Ordinal))
            {
                return "-" + text[2..];
            }

            return text;
        }

        private static string FormatEnum<TEnum>(TEnum value, IReadOnlyDictionary<string, TEnum> symbols)
            where TEnum : struct
        {
            if (symbols != null)
            {
                foreach (KeyValuePair<string, TEnum> symbol in symbols)
                {
                    if (EqualityComparer<TEnum>.Default.Equals(symbol.Value, value))
                    {
                        return symbol.Key;
                    }
                }
            }

            return value.ToString();
        }

        private static void ReadValueRange(string text, FloatParameterTrackNode node)
        {
            int index = 0;
            node.mLowValue = ReadFloatValueToken(text, ref index);
            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                node.mHighValue = node.mLowValue;
                node.mDistribution = TodCurves.Constant;
                return;
            }

            if (IsIdentifierStart(text[index]))
            {
                node.mDistribution = ParseCurve(ReadIdentifier(text, ref index));
                node.mHighValue = ReadFloatValueToken(text, ref index);
            }
            else
            {
                node.mDistribution = TodCurves.Linear;
                node.mHighValue = ReadFloatValueToken(text, ref index);
            }

            SkipWhitespace(text, ref index);
            if (index < text.Length)
            {
                throw new FormatException($"Unexpected float track range text '{text[index..]}'.");
            }
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

        private static float ReadFloatValueToken(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            int start = index;
            if (index < text.Length && (text[index] == '+' || text[index] == '-'))
            {
                index++;
            }

            bool hasDigits = false;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
                hasDigits = true;
            }

            if (index < text.Length && text[index] == '.')
            {
                index++;
                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                    hasDigits = true;
                }
            }

            if (!hasDigits)
            {
                throw new FormatException("Expected float token.");
            }

            if (HasExponent(text, index))
            {
                index++;
                if (text[index] == '+' || text[index] == '-')
                {
                    index++;
                }

                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            return float.Parse(text[start..index], CultureInfo.InvariantCulture);
        }

        private static bool HasExponent(string text, int index)
        {
            if (index >= text.Length || (text[index] != 'e' && text[index] != 'E'))
            {
                return false;
            }

            int digitIndex = index + 1;
            if (digitIndex < text.Length && (text[digitIndex] == '+' || text[digitIndex] == '-'))
            {
                digitIndex++;
            }

            return digitIndex < text.Length && char.IsDigit(text[digitIndex]);
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

        private static IReadOnlyList<DefSymbol> CreateCompiledSymbols<TValue>(IReadOnlyDictionary<string, TValue> symbols)
        {
            return symbols is null
                ? []
                : symbols.Select(static symbol => new DefSymbol(Convert.ToInt32(symbol.Value, CultureInfo.InvariantCulture), symbol.Key)).ToArray();
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
            private readonly DefFieldGetter<T, TValue> _getter;
            private readonly Func<TValue, string> _writer;
            private readonly DefValueShouldWrite<TValue> _shouldWrite;
            private readonly IReadOnlyList<DefSymbol> _compiledSymbols;

            public DefField(
                string name,
                DefFieldType fieldType,
                int compiledOffset,
                IReadOnlyList<DefSymbol> compiledSymbols,
                Func<string, SexyXmlParser, TValue> reader,
                DefFieldSetter<T, TValue> setter,
                DefFieldGetter<T, TValue> getter,
                Func<TValue, string> writer,
                DefValueShouldWrite<TValue> shouldWrite)
            {
                Name = name;
                FieldType = fieldType;
                CompiledOffset = compiledOffset;
                _compiledSymbols = compiledSymbols;
                _reader = reader;
                _setter = setter;
                _getter = getter;
                _writer = writer;
                _shouldWrite = shouldWrite;
            }

            public string Name { get; }
            public DefFieldType FieldType { get; }
            public int CompiledOffset { get; }

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!string.Equals(elementName, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                _setter(ref definition, _reader(elementName, parser));
                return true;
            }

            public void Write(SexyXmlWriter writer, ref T definition)
            {
                if (_getter is null)
                {
                    return;
                }

                TValue value = _getter(ref definition);
                if (_shouldWrite != null && !_shouldWrite(value))
                {
                    return;
                }

                writer.WriteElement(Name, _writer(value));
            }

            public void ReadCompiled(CompiledDefinitionReader reader, ReadOnlySpan<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                object value = FieldType switch
                {
                    DefFieldType.Int => CompiledDefinitionFormat.ReadInt32(rawDefinition, CompiledOffset),
                    DefFieldType.Float => CompiledDefinitionFormat.ReadSingle(rawDefinition, CompiledOffset),
                    DefFieldType.Enum => ReadCompiledEnum(rawDefinition),
                    DefFieldType.String => reader.ReadString(),
                    DefFieldType.Image or DefFieldType.Font => ReadCompiledResourceName(reader),
                    _ => throw new InvalidDataException($"Compiled field '{Name}' has unsupported type '{FieldType}'.")
                };

                _setter(ref definition, (TValue)value);
            }

            public void WriteCompiledRaw(Span<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                if (_getter is null)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing a value getter.");
                }

                TValue value = _getter(ref definition);
                switch (FieldType)
                {
                    case DefFieldType.Int:
                        CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                        break;
                    case DefFieldType.Float:
                        CompiledDefinitionFormat.WriteSingle(rawDefinition, CompiledOffset, Convert.ToSingle(value, CultureInfo.InvariantCulture));
                        break;
                    case DefFieldType.Enum:
                        CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                        break;
                    case DefFieldType.String:
                    case DefFieldType.Image:
                    case DefFieldType.Font:
                        CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset, 0);
                        break;
                    default:
                        throw new InvalidDataException($"Compiled field '{Name}' has unsupported type '{FieldType}'.");
                }
            }

            public void WriteCompiledExtra(CompiledDefinitionWriter writer, ref T definition)
            {
                if (FieldType is not (DefFieldType.String or DefFieldType.Image or DefFieldType.Font))
                {
                    return;
                }

                if (_getter is null)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing a value getter.");
                }

                writer.WriteString(Convert.ToString(_getter(ref definition), CultureInfo.InvariantCulture) ?? string.Empty);
            }

            public void AppendCompiledSchema(ref uint schemaHash, HashSet<object> progressMaps)
            {
                CompiledDefinitionFormat.AppendFieldSchema(ref schemaHash, FieldType, CompiledOffset);
                if (FieldType == DefFieldType.Enum && _compiledSymbols is not null)
                {
                    CompiledDefinitionFormat.AppendSymbolSchema(ref schemaHash, _compiledSymbols);
                }
            }

            private object ReadCompiledEnum(ReadOnlySpan<byte> rawDefinition)
            {
                int value = CompiledDefinitionFormat.ReadInt32(rawDefinition, CompiledOffset);
                Type type = typeof(TValue);
                return type.IsEnum ? System.Enum.ToObject(type, value) : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }

            private static string ReadCompiledResourceName(CompiledDefinitionReader reader)
            {
                string value = reader.ReadString();
                return value.Length == 0 ? null : value;
            }

            private void EnsureCompiledOffset()
            {
                if (CompiledOffset < 0)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing an original structure offset.");
                }
            }
        }

        internal sealed class TrackFloatDefField<T> : IDefField<T>
        {
            private readonly DefTrackGetter<T> _getter;
            private readonly float? _defaultValue;

            public TrackFloatDefField(string name, DefTrackGetter<T> getter, float? defaultValue, int compiledOffset)
            {
                Name = name;
                _getter = getter;
                _defaultValue = defaultValue;
                CompiledOffset = compiledOffset;
            }

            public string Name { get; }
            public DefFieldType FieldType => DefFieldType.TrackFloat;
            public int CompiledOffset { get; }

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!string.Equals(elementName, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                ReadFloatTrackField(parser, _getter(ref definition));
                return true;
            }

            public void Write(SexyXmlWriter writer, ref T definition)
            {
                FloatParameterTrack track = _getter(ref definition);
                if (track?.mNodes is null || track.mCountNodes <= 0 || IsDefaultFloatTrack(track, _defaultValue))
                {
                    return;
                }

                writer.WriteElement(Name, WriteFloatTrack(track));
            }

            public void ReadCompiled(CompiledDefinitionReader reader, ReadOnlySpan<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                FloatParameterTrack track = _getter(ref definition);
                int count = reader.ReadInt32();
                if (count < 0 || count > 100000)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' has invalid float track node count {count}.");
                }

                track.mCountNodes = count;
                if (count == 0)
                {
                    track.mNodes = null;
                    return;
                }

                track.mNodes = new FloatParameterTrackNode[count];
                for (int i = 0; i < count; i++)
                {
                    track.mNodes[i] = new FloatParameterTrackNode
                    {
                        mTime = reader.ReadSingle(),
                        mLowValue = reader.ReadSingle(),
                        mHighValue = reader.ReadSingle(),
                        mCurveType = (TodCurves)reader.ReadInt32(),
                        mDistribution = (TodCurves)reader.ReadInt32()
                    };
                }
            }

            public void WriteCompiledRaw(Span<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                FloatParameterTrack track = _getter(ref definition);
                CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset, 0);
                CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset + sizeof(int), SafeTrackCount(track));
            }

            public void WriteCompiledExtra(CompiledDefinitionWriter writer, ref T definition)
            {
                FloatParameterTrack track = _getter(ref definition);
                int count = SafeTrackCount(track);
                writer.WriteInt32(count);
                for (int i = 0; i < count; i++)
                {
                    FloatParameterTrackNode node = track.mNodes[i];
                    writer.WriteSingle(node.mTime);
                    writer.WriteSingle(node.mLowValue);
                    writer.WriteSingle(node.mHighValue);
                    writer.WriteInt32((int)node.mCurveType);
                    writer.WriteInt32((int)node.mDistribution);
                }
            }

            public void AppendCompiledSchema(ref uint schemaHash, HashSet<object> progressMaps)
            {
                CompiledDefinitionFormat.AppendFieldSchema(ref schemaHash, FieldType, CompiledOffset);
            }

            private static bool IsDefaultFloatTrack(FloatParameterTrack track, float? defaultValue)
            {
                if (track.mCountNodes != 1 || track.mNodes.Length == 0)
                {
                    return false;
                }

                FloatParameterTrackNode node = track.mNodes[0];
                bool isSingleConstant = node.mTime == 0f &&
                    node.mLowValue == node.mHighValue &&
                    node.mCurveType == TodCurves.Constant &&
                    node.mDistribution == TodCurves.Linear;
                if (!isSingleConstant)
                {
                    return false;
                }

                return !defaultValue.HasValue || Math.Abs(node.mLowValue - defaultValue.Value) < 0.0005f;
            }

            private static int SafeTrackCount(FloatParameterTrack track)
            {
                return track?.mNodes is null ? 0 : Math.Min(track.mCountNodes, track.mNodes.Length);
            }

            private void EnsureCompiledOffset()
            {
                if (CompiledOffset < 0)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing an original structure offset.");
                }
            }
        }

        internal sealed class ArrayDefField<T, TItem> : IDefField<T>
        {
            private readonly DefMap<TItem> _itemMap;
            private readonly DefItemAppender<T, TItem> _append;
            private readonly DefArrayCountGetter<T> _countGetter;
            private readonly DefArrayItemGetter<T, TItem> _itemGetter;

            public ArrayDefField(
                string name,
                DefMap<TItem> itemMap,
                DefItemAppender<T, TItem> append,
                DefArrayCountGetter<T> countGetter,
                DefArrayItemGetter<T, TItem> itemGetter,
                int compiledOffset)
            {
                Name = name;
                _itemMap = itemMap;
                _append = append;
                _countGetter = countGetter;
                _itemGetter = itemGetter;
                CompiledOffset = compiledOffset;
            }

            public string Name { get; }
            public DefFieldType FieldType => DefFieldType.Array;
            public int CompiledOffset { get; }

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

            public void Write(SexyXmlWriter writer, ref T definition)
            {
                if (_countGetter is null || _itemGetter is null)
                {
                    return;
                }

                int count = _countGetter(ref definition);
                for (int i = 0; i < count; i++)
                {
                    TItem item = _itemGetter(ref definition, i);
                    writer.WriteStartElement(Name);
                    SaveMap(writer, _itemMap, ref item);
                    writer.WriteEndElement(Name);
                }
            }

            public void ReadCompiled(CompiledDefinitionReader reader, ReadOnlySpan<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                int count = CompiledDefinitionFormat.ReadInt32(rawDefinition, CompiledOffset + sizeof(int));
                if (count < 0 || count > 100000)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' has invalid array count {count}.");
                }

                int defSize = reader.ReadInt32();
                if (defSize != _itemMap.CompiledSize)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' has item size {defSize}, expected {_itemMap.CompiledSize}.");
                }

                if (count == 0)
                {
                    return;
                }

                byte[] rawItems = reader.ReadBytes(checked(count * defSize));
                for (int i = 0; i < count; i++)
                {
                    TItem item = _itemMap.Constructor();
                    ReadOnlySpan<byte> rawItem = rawItems.AsSpan(i * defSize, defSize);
                    CompiledDefinitionFormat.ReadMapFromRaw(reader, _itemMap, rawItem, ref item);
                    _append(ref definition, item);
                }
            }

            public void WriteCompiledRaw(Span<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset, 0);
                CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset + sizeof(int), SafeArrayCount(ref definition));
            }

            public void WriteCompiledExtra(CompiledDefinitionWriter writer, ref T definition)
            {
                int count = SafeArrayCount(ref definition);
                writer.WriteInt32(_itemMap.CompiledSize);

                for (int i = 0; i < count; i++)
                {
                    TItem item = _itemGetter(ref definition, i);
                    writer.Write(CompiledDefinitionFormat.CreateRawStruct(_itemMap, ref item));
                }

                for (int i = 0; i < count; i++)
                {
                    TItem item = _itemGetter(ref definition, i);
                    CompiledDefinitionFormat.WriteMapExtra(writer, _itemMap, ref item);
                }
            }

            public void AppendCompiledSchema(ref uint schemaHash, HashSet<object> progressMaps)
            {
                CompiledDefinitionFormat.AppendFieldSchema(ref schemaHash, FieldType, CompiledOffset);
                CompiledDefinitionFormat.AppendMapSchema(ref schemaHash, _itemMap, progressMaps);
            }

            private int SafeArrayCount(ref T definition)
            {
                if (_countGetter is null || _itemGetter is null)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing array accessors.");
                }

                return _countGetter(ref definition);
            }

            private void EnsureCompiledOffset()
            {
                if (CompiledOffset < 0)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing an original structure offset.");
                }
            }
        }

        internal sealed class FlagsDefField<T> : IDefField<T>
        {
            private readonly IReadOnlyDictionary<string, int> _symbols;
            private readonly DefFlagSetter<T> _setter;
            private readonly DefFlagGetter<T> _getter;
            private readonly IReadOnlyList<DefSymbol> _compiledSymbols;

            public FlagsDefField(
                string name,
                IReadOnlyDictionary<string, int> symbols,
                DefFlagSetter<T> setter,
                DefFlagGetter<T> getter,
                int compiledOffset,
                IReadOnlyList<DefSymbol> compiledSymbols)
            {
                Name = name;
                _symbols = symbols;
                _setter = setter;
                _getter = getter;
                CompiledOffset = compiledOffset;
                _compiledSymbols = compiledSymbols;
            }

            public string Name { get; }
            public DefFieldType FieldType => DefFieldType.Flags;
            public int CompiledOffset { get; }

            public bool TryRead(SexyXmlParser parser, string elementName, ref T definition)
            {
                if (!_symbols.TryGetValue(elementName, out int bitIndex))
                {
                    return false;
                }

                _setter(ref definition, bitIndex, ReadFlagValue(parser));
                return true;
            }

            public void Write(SexyXmlWriter writer, ref T definition)
            {
                if (_getter is null)
                {
                    return;
                }

                int flags = _getter(ref definition);
                HashSet<int> writtenBits = [];
                foreach (KeyValuePair<string, int> symbol in _symbols)
                {
                    if ((flags & (1 << symbol.Value)) != 0 && writtenBits.Add(symbol.Value))
                    {
                        writer.WriteElement(symbol.Key, "1");
                    }
                }
            }

            public void ReadCompiled(CompiledDefinitionReader reader, ReadOnlySpan<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                int flags = CompiledDefinitionFormat.ReadInt32(rawDefinition, CompiledOffset);
                for (int i = 0; i < sizeof(int) * 8; i++)
                {
                    _setter(ref definition, i, (flags & (1 << i)) != 0);
                }
            }

            public void WriteCompiledRaw(Span<byte> rawDefinition, ref T definition)
            {
                EnsureCompiledOffset();
                if (_getter is null)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing a value getter.");
                }

                CompiledDefinitionFormat.WriteInt32(rawDefinition, CompiledOffset, _getter(ref definition));
            }

            public void WriteCompiledExtra(CompiledDefinitionWriter writer, ref T definition)
            {
            }

            public void AppendCompiledSchema(ref uint schemaHash, HashSet<object> progressMaps)
            {
                CompiledDefinitionFormat.AppendFieldSchema(ref schemaHash, FieldType, CompiledOffset);
                CompiledDefinitionFormat.AppendSymbolSchema(ref schemaHash, _compiledSymbols);
            }

            private void EnsureCompiledOffset()
            {
                if (CompiledOffset < 0)
                {
                    throw new InvalidDataException($"Compiled field '{Name}' is missing an original structure offset.");
                }
            }
        }
    }
}
