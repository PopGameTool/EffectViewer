namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseScriptAction
    {
        public ShowcaseScriptAction(string title, string description, string text)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string Title { get; }
        public string Description { get; }
        public string Text { get; }

        public override string ToString()
        {
            return Title;
        }
    }
}
