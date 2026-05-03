using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseLogEntry
    {
        private const int SummaryMaxLength = 140;
        private static readonly Regex ParenthesizedLocationRegex = new(@"\((?<line>\d+),(?<column>\d+)(?:-\d+)?\)", RegexOptions.Compiled);
        private static readonly Regex ColonLocationRegex = new(@":(?<line>\d+):", RegexOptions.Compiled);

        public string Summary { get; }
        public string Detail { get; }
        public int LineNumber { get; }
        public int ColumnNumber { get; }
        public bool HasLocation => LineNumber > 0;

        private ShowcaseLogEntry(string summary, string detail, int lineNumber, int columnNumber)
        {
            Summary = summary;
            Detail = detail;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
        }

        public static ShowcaseLogEntry FromMessage(string message)
        {
            string detail = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            TryParseLocation(detail, out int lineNumber, out int columnNumber);
            string summary = detail
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?? string.Empty;

            if (summary.Length > SummaryMaxLength)
            {
                summary = summary[..SummaryMaxLength] + "...";
            }

            return new ShowcaseLogEntry(summary, detail, lineNumber, columnNumber);
        }

        public override string ToString()
        {
            return Summary;
        }

        private static bool TryParseLocation(string message, out int lineNumber, out int columnNumber)
        {
            lineNumber = 0;
            columnNumber = 1;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            Match parenthesized = ParenthesizedLocationRegex.Match(message);
            if (parenthesized.Success &&
                int.TryParse(parenthesized.Groups["line"].Value, out lineNumber))
            {
                if (!int.TryParse(parenthesized.Groups["column"].Value, out columnNumber))
                {
                    columnNumber = 1;
                }

                return true;
            }

            Match colon = ColonLocationRegex.Match(message);
            return colon.Success && int.TryParse(colon.Groups["line"].Value, out lineNumber);
        }
    }
}
