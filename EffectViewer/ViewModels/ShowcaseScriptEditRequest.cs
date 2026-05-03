namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseScriptEditRequest
    {
        public ShowcaseScriptEditRequest(string text, bool replaceDocument)
        {
            Text = text ?? string.Empty;
            ReplaceDocument = replaceDocument;
        }

        public string Text { get; }
        public bool ReplaceDocument { get; }
    }
}
