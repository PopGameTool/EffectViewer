using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EffectViewer.TodLib.Common
{
    internal enum SexyXmlElementType
    {
        None,
        Start,
        End,
        Element,
        Instruction,
        Comment
    }

    internal sealed class SexyXmlElement
    {
        public SexyXmlElementType Type { get; init; }
        public string Section { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Instruction { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, string> Attributes { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class SexyXmlParser
    {
        private readonly string _text;
        private readonly Queue<SexyXmlElement> _pending = new();
        private readonly List<string> _sections = [];
        private int _position;

        private SexyXmlParser(string text, string fileName)
        {
            _text = text ?? string.Empty;
            FileName = fileName ?? string.Empty;
            CurrentLineNumber = 1;
        }

        public string FileName { get; }
        public int CurrentLineNumber { get; private set; }

        public static SexyXmlParser FromStream(Stream stream, string fileName = "")
        {
            using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return new SexyXmlParser(reader.ReadToEnd(), fileName);
        }

        public static SexyXmlParser FromFile(string fileName)
        {
            using FileStream stream = File.OpenRead(fileName);
            return FromStream(stream, fileName);
        }

        public bool TryNextElement(out SexyXmlElement element)
        {
            if (_pending.Count > 0)
            {
                element = _pending.Dequeue();
                if (element.Type == SexyXmlElementType.End)
                {
                    PopSection(element.Value);
                }

                return true;
            }

            while (_position < _text.Length)
            {
                if (Peek() == '<')
                {
                    if (TryReadTag(out element))
                    {
                        return true;
                    }

                    continue;
                }

                if (TryReadText(out element))
                {
                    return true;
                }
            }

            element = null;
            return false;
        }

        public InvalidDataException CreateError(string message)
        {
            string location = string.IsNullOrEmpty(FileName)
                ? $"line {CurrentLineNumber}"
                : $"{FileName}({CurrentLineNumber})";
            return new InvalidDataException($"{location}: XML Definition Error: {message}");
        }

        private bool TryReadText(out SexyXmlElement element)
        {
            StringBuilder value = new();
            bool hasSpace = false;

            while (_position < _text.Length)
            {
                char c = Peek();
                if (c == '<')
                {
                    break;
                }

                Read();
                if (char.IsWhiteSpace(c))
                {
                    if (value.Length > 0)
                    {
                        hasSpace = true;
                    }
                }
                else if (c > 32)
                {
                    if (hasSpace)
                    {
                        value.Append(' ');
                        hasSpace = false;
                    }

                    value.Append(c);
                }
                else
                {
                    throw CreateError("Illegal Character");
                }
            }

            if (value.Length == 0)
            {
                element = null;
                return false;
            }

            element = new SexyXmlElement
            {
                Type = SexyXmlElementType.Element,
                Section = CurrentSection(),
                Value = DecodeString(value.ToString())
            };
            return true;
        }

        private bool TryReadTag(out SexyXmlElement element)
        {
            string section = CurrentSection();
            ReadExpected('<');

            if (_position >= _text.Length)
            {
                throw CreateError("Unexpected End of File");
            }

            if (Peek() == '/')
            {
                Read();
                string endName = DecodeString(ReadUntilTagEnd().Trim());
                PopSection(endName);
                element = new SexyXmlElement
                {
                    Type = SexyXmlElementType.End,
                    Section = section,
                    Value = endName
                };
                return true;
            }

            if (Peek() == '?')
            {
                Read();
                string instruction = ReadUntilInstructionEnd();
                string value = ReadLeadingToken(instruction, out string rest);
                element = new SexyXmlElement
                {
                    Type = SexyXmlElementType.Instruction,
                    Section = section,
                    Value = DecodeString(value),
                    Instruction = DecodeString(rest)
                };
                return true;
            }

            if (StartsWith("!--"))
            {
                _position += 3;
                string comment = ReadUntilCommentEnd();
                element = new SexyXmlElement
                {
                    Type = SexyXmlElementType.Comment,
                    Section = section,
                    Instruction = DecodeString(comment)
                };
                return false;
            }

            string content = ReadUntilStartTagEnd();
            bool insertEnd = false;
            content = content.Trim();
            if (content.EndsWith("/", StringComparison.Ordinal))
            {
                insertEnd = true;
                content = content[..^1].TrimEnd();
            }

            string name = ReadLeadingToken(content, out string attributesText);
            name = DecodeString(name);
            Dictionary<string, string> attributes = ParseAttributes(attributesText);
            PushSection(name);

            if (insertEnd)
            {
                _pending.Enqueue(new SexyXmlElement
                {
                    Type = SexyXmlElementType.End,
                    Section = CurrentSection(),
                    Value = name
                });
            }

            element = new SexyXmlElement
            {
                Type = SexyXmlElementType.Start,
                Section = section,
                Value = name,
                Attributes = attributes
            };
            return true;
        }

        private string ReadUntilTagEnd()
        {
            StringBuilder value = new();
            while (_position < _text.Length)
            {
                char c = Read();
                if (c == '>')
                {
                    return value.ToString();
                }

                value.Append(c);
            }

            throw CreateError("Unexpected End of File");
        }

        private string ReadUntilStartTagEnd()
        {
            StringBuilder value = new();
            bool inQuote = false;
            while (_position < _text.Length)
            {
                char c = Read();
                if (c == '"')
                {
                    inQuote = !inQuote;
                }

                if (c == '>' && !inQuote)
                {
                    return value.ToString();
                }

                value.Append(c);
            }

            throw CreateError("Unexpected End of File");
        }

        private string ReadUntilInstructionEnd()
        {
            StringBuilder value = new();
            while (_position < _text.Length)
            {
                char c = Read();
                value.Append(c);
                int length = value.Length;
                if (c == '>' && length >= 2 && value[length - 2] == '?')
                {
                    return value.ToString(0, length - 2);
                }
            }

            throw CreateError("Unexpected End of File");
        }

        private string ReadUntilCommentEnd()
        {
            StringBuilder value = new();
            while (_position < _text.Length)
            {
                char c = Read();
                value.Append(c);
                int length = value.Length;
                if (c == '>' && length >= 3 && value[length - 2] == '-' && value[length - 3] == '-')
                {
                    return value.ToString(0, length - 3);
                }
            }

            throw CreateError("Unexpected End of File");
        }

        private static string ReadLeadingToken(string text, out string rest)
        {
            int i = 0;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            rest = i < text.Length ? text[i..] : string.Empty;
            return text[start..i];
        }

        private static Dictionary<string, string> ParseAttributes(string text)
        {
            Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i >= text.Length)
                {
                    break;
                }

                int keyStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '=')
                {
                    i++;
                }

                string key = text[keyStart..i];
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                string value = string.Empty;
                if (i < text.Length && text[i] == '=')
                {
                    i++;
                    while (i < text.Length && char.IsWhiteSpace(text[i]))
                    {
                        i++;
                    }

                    if (i < text.Length && (text[i] == '"' || text[i] == '\''))
                    {
                        char quote = text[i++];
                        int valueStart = i;
                        while (i < text.Length && text[i] != quote)
                        {
                            i++;
                        }

                        value = text[valueStart..Math.Min(i, text.Length)];
                        if (i < text.Length)
                        {
                            i++;
                        }
                    }
                    else
                    {
                        int valueStart = i;
                        while (i < text.Length && !char.IsWhiteSpace(text[i]))
                        {
                            i++;
                        }

                        value = text[valueStart..i];
                    }
                }

                if (!string.IsNullOrEmpty(key))
                {
                    attributes[DecodeString(key)] = DecodeString(value);
                }
            }

            return attributes;
        }

        private static string DecodeString(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains('&', StringComparison.Ordinal))
            {
                return text;
            }

            StringBuilder value = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '&')
                {
                    int semi = text.IndexOf(';', i + 1);
                    if (semi != -1)
                    {
                        string entity = text[(i + 1)..semi];
                        i = semi;
                        c = entity switch
                        {
                            "lt" => '<',
                            "amp" => '&',
                            "gt" => '>',
                            "quot" => '"',
                            "apos" => '\'',
                            "nbsp" => ' ',
                            "cr" => '\n',
                            _ => '&'
                        };
                    }
                }

                value.Append(c);
            }

            return value.ToString();
        }

        private char Peek()
        {
            return _text[_position];
        }

        private char Read()
        {
            char c = _text[_position++];
            if (c == '\n')
            {
                CurrentLineNumber++;
            }

            return c;
        }

        private void ReadExpected(char expected)
        {
            char c = Read();
            if (c != expected)
            {
                throw CreateError($"Expected '{expected}'");
            }
        }

        private bool StartsWith(string value)
        {
            return _position + value.Length <= _text.Length &&
                string.CompareOrdinal(_text, _position, value, 0, value.Length) == 0;
        }

        private string CurrentSection()
        {
            return _sections.Count == 0 ? string.Empty : string.Join("/", _sections);
        }

        private void PushSection(string name)
        {
            _sections.Add(name);
        }

        private void PopSection(string name)
        {
            if (_sections.Count == 0)
            {
                throw CreateError("Unexpected End");
            }

            string last = _sections[^1];
            if (!string.Equals(last, name, StringComparison.Ordinal))
            {
                throw CreateError($"End '{name}' Doesn't Match Start '{last}'");
            }

            _sections.RemoveAt(_sections.Count - 1);
        }
    }
}
