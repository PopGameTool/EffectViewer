namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseCompletionItem
    {
        public ShowcaseCompletionItem(string displayText, string insertText, string description, bool isMember)
        {
            DisplayText = displayText ?? string.Empty;
            InsertText = insertText ?? string.Empty;
            Description = description ?? string.Empty;
            IsMember = isMember;
        }

        public string DisplayText { get; }
        public string InsertText { get; }
        public string Description { get; }
        public bool IsMember { get; }
    }
}
