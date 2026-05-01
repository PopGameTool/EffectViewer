using System;
using System.Linq;

namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseLogEntry
    {
        private const int SummaryMaxLength = 140;

        public string Summary { get; }
        public string Detail { get; }

        private ShowcaseLogEntry(string summary, string detail)
        {
            Summary = summary;
            Detail = detail;
        }

        public static ShowcaseLogEntry FromMessage(string message)
        {
            string detail = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            string summary = detail
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?? string.Empty;

            if (summary.Length > SummaryMaxLength)
            {
                summary = summary[..SummaryMaxLength] + "...";
            }

            return new ShowcaseLogEntry(summary, detail);
        }

        public override string ToString()
        {
            return Summary;
        }
    }
}
