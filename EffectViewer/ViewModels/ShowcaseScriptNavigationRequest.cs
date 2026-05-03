namespace EffectViewer.ViewModels
{
    public sealed class ShowcaseScriptNavigationRequest
    {
        public ShowcaseScriptNavigationRequest(int lineNumber, int columnNumber)
        {
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
        }

        public int LineNumber { get; }
        public int ColumnNumber { get; }
    }
}
