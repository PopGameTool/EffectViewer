using System;
using System.IO;
using System.Text;

namespace EffectViewer.TodLib.Common
{
    internal sealed class SexyXmlWriter
    {
        private const string IndentText = "    ";

        private readonly TextWriter _writer;
        private int _depth;

        public SexyXmlWriter(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void WriteStartElement(string name)
        {
            WriteIndent();
            _writer.Write('<');
            _writer.Write(name);
            _writer.WriteLine('>');
            _depth++;
        }

        public void WriteEndElement(string name)
        {
            _depth--;
            WriteIndent();
            _writer.Write("</");
            _writer.Write(name);
            _writer.WriteLine('>');
        }

        public void WriteElement(string name, string value)
        {
            WriteIndent();
            _writer.Write('<');
            _writer.Write(name);
            _writer.Write('>');
            _writer.Write(EncodeString(value));
            _writer.Write("</");
            _writer.Write(name);
            _writer.WriteLine('>');
        }

        public void Flush()
        {
            _writer.Flush();
        }

        private void WriteIndent()
        {
            for (int i = 0; i < _depth; i++)
            {
                _writer.Write(IndentText);
            }
        }

        private static string EncodeString(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder encoded = null;
            bool hasSpace = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    if (hasSpace)
                    {
                        encoded ??= new StringBuilder(text, 0, i, text.Length + 6);
                        encoded.Append("&nbsp;");
                        continue;
                    }

                    hasSpace = true;
                }
                else
                {
                    hasSpace = false;
                }

                string replacement = c switch
                {
                    '<' => "&lt;",
                    '&' => "&amp;",
                    '>' => "&gt;",
                    '"' => "&quot;",
                    '\'' => "&apos;",
                    '\n' => "&cr;",
                    _ => null
                };

                if (replacement is null)
                {
                    encoded?.Append(c);
                    continue;
                }

                encoded ??= new StringBuilder(text, 0, i, text.Length + replacement.Length);
                encoded.Append(replacement);
            }

            return encoded?.ToString() ?? text;
        }
    }
}
