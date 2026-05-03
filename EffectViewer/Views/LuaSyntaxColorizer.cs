using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace EffectViewer.Views
{
    internal sealed class LuaSyntaxColorizer : DocumentColorizingTransformer
    {
        private static readonly IBrush KeywordBrush = Brush.Parse("#7C5CFF");
        private static readonly IBrush ApiBrush = Brush.Parse("#0E8A87");
        private static readonly IBrush StringBrush = Brush.Parse("#A15C00");
        private static readonly IBrush NumberBrush = Brush.Parse("#006DCC");
        private static readonly IBrush CommentBrush = Brush.Parse("#6A737D");

        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function",
            "if", "in", "local", "nil", "not", "or", "repeat", "return", "then",
            "true", "until", "while"
        };

        private static readonly HashSet<string> ApiNames = new(StringComparer.Ordinal)
        {
            "effect", "scene", "update", "draw", "dt", "g", "math", "string", "table",
            "reanim", "particle", "trail", "image", "vector", "matrix", "log", "warn",
            "clear", "set_position", "move", "offset", "set_scale", "set_color", "draw",
            "update", "die", "set_image_override", "clear_image_override", "add_point",
            "clear_points", "attach_reanim", "attach_particle", "attach_trail"
        };

        protected override void ColorizeLine(DocumentLine line)
        {
            string text = CurrentContext.Document.GetText(line);
            int lineStart = line.Offset;
            int commentIndex = FindCommentStart(text);
            int codeLength = commentIndex >= 0 ? commentIndex : text.Length;

            ColorizeStrings(text, lineStart, codeLength);
            ColorizeWords(text, lineStart, codeLength);

            if (commentIndex >= 0)
            {
                SetForeground(lineStart + commentIndex, lineStart + text.Length, CommentBrush);
            }
        }

        private static int FindCommentStart(string text)
        {
            bool inString = false;
            char quote = '\0';
            for (int i = 0; i < text.Length - 1; i++)
            {
                char current = text[i];
                if (inString)
                {
                    if (current == '\\')
                    {
                        i++;
                    }
                    else if (current == quote)
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current is '"' or '\'')
                {
                    inString = true;
                    quote = current;
                    continue;
                }

                if (current == '-' && text[i + 1] == '-')
                {
                    return i;
                }
            }

            return -1;
        }

        private void ColorizeStrings(string text, int lineStart, int codeLength)
        {
            for (int i = 0; i < codeLength; i++)
            {
                char current = text[i];
                if (current is not ('"' or '\''))
                {
                    continue;
                }

                char quote = current;
                int start = i++;
                while (i < codeLength)
                {
                    if (text[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (text[i++] == quote)
                    {
                        break;
                    }
                }

                SetForeground(lineStart + start, lineStart + Math.Min(i, codeLength), StringBrush);
            }
        }

        private void ColorizeWords(string text, int lineStart, int codeLength)
        {
            int i = 0;
            while (i < codeLength)
            {
                char current = text[i];
                if (char.IsDigit(current))
                {
                    int start = i++;
                    while (i < codeLength && (char.IsDigit(text[i]) || text[i] == '.'))
                    {
                        i++;
                    }

                    SetForeground(lineStart + start, lineStart + i, NumberBrush);
                    continue;
                }

                if (!IsIdentifierStart(current))
                {
                    i++;
                    continue;
                }

                int wordStart = i++;
                while (i < codeLength && IsIdentifierPart(text[i]))
                {
                    i++;
                }

                string word = text[wordStart..i];
                if (Keywords.Contains(word))
                {
                    SetForeground(lineStart + wordStart, lineStart + i, KeywordBrush);
                }
                else if (ApiNames.Contains(word))
                {
                    SetForeground(lineStart + wordStart, lineStart + i, ApiBrush);
                }
            }
        }

        private void SetForeground(int startOffset, int endOffset, IBrush brush)
        {
            if (startOffset >= endOffset)
            {
                return;
            }

            ChangeLinePart(startOffset, endOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
            });
        }

        private static bool IsIdentifierStart(char value)
        {
            return char.IsLetter(value) || value == '_';
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }
    }
}
