using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using EffectViewer.Localization;
using EffectViewer.ViewModels;

namespace EffectViewer.Views
{
    public partial class ShowcaseEditorView : UserControl
    {
        private ShowcaseEditorViewModel _viewModel;
        private CompletionWindow _completionWindow;
        private bool _isSyncingEditorText;

        public ShowcaseEditorView()
        {
            InitializeComponent();
            ConfigureScriptEditor();
            DataContextChanged += OnDataContextChanged;
            AttachViewModel(DataContext as ShowcaseEditorViewModel);
        }

        private void ConfigureScriptEditor()
        {
            ScriptEditor.Options = new()
            {
                AllowScrollBelowDocument = true,
                ConvertTabsToSpaces = true,
                HighlightCurrentLine = true,
                IndentationSize = 4,
                EnableHyperlinks = false
            };
            ScriptEditor.LineNumbersForeground = Brush.Parse("#7A7F87");
            ScriptEditor.TextArea.TextView.LineTransformers.Add(new LuaSyntaxColorizer());
            ScriptEditor.TextChanged += ScriptEditor_TextChanged;
            ScriptEditor.TextArea.TextEntered += ScriptEditor_TextEntered;
            ScriptEditor.KeyDown += ScriptEditor_KeyDown;
            SearchPanel.Install(ScriptEditor);
            ScriptEditor.Watermark = LocalizationManager.Instance.Text("Placeholder.ShowcaseScript");
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            ScriptEditor.Watermark = LocalizationManager.Instance.Text("Placeholder.ShowcaseScript");
        }

        private void OnDataContextChanged(object sender, EventArgs e)
        {
            AttachViewModel(DataContext as ShowcaseEditorViewModel);
        }

        private void AttachViewModel(ShowcaseEditorViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            if (_viewModel is not null)
            {
                _viewModel.ScriptEditRequested -= OnScriptEditRequested;
                _viewModel.ScriptNavigationRequested -= OnScriptNavigationRequested;
            }

            _viewModel = viewModel;
            if (_viewModel is not null)
            {
                _viewModel.ScriptEditRequested += OnScriptEditRequested;
                _viewModel.ScriptNavigationRequested += OnScriptNavigationRequested;
                SetEditorText(_viewModel.ScriptText);
            }
            else
            {
                SetEditorText(string.Empty);
            }
        }

        private void ScriptEditor_TextChanged(object sender, EventArgs e)
        {
            if (_isSyncingEditorText || _viewModel is null)
            {
                return;
            }

            if (!string.Equals(_viewModel.ScriptText, ScriptEditor.Text, StringComparison.Ordinal))
            {
                _viewModel.ScriptText = ScriptEditor.Text;
            }
        }

        private void SetEditorText(string text)
        {
            if (string.Equals(ScriptEditor.Text, text ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            _isSyncingEditorText = true;
            ScriptEditor.Text = text ?? string.Empty;
            _isSyncingEditorText = false;
        }

        private void OnScriptEditRequested(object sender, ShowcaseScriptEditRequest request)
        {
            if (request is null)
            {
                return;
            }

            InsertScriptText(request.Text, request.ReplaceDocument);
        }

        private void OnScriptNavigationRequested(object sender, ShowcaseScriptNavigationRequest request)
        {
            if (request is null || ScriptEditor.Document is null || ScriptEditor.Document.LineCount == 0)
            {
                return;
            }

            int lineNumber = Math.Clamp(request.LineNumber, 1, ScriptEditor.Document.LineCount);
            DocumentLine line = ScriptEditor.Document.GetLineByNumber(lineNumber);
            int column = Math.Clamp(request.ColumnNumber, 1, Math.Max(1, line.Length + 1));
            int offset = Math.Clamp(line.Offset + column - 1, line.Offset, line.EndOffset);
            ScriptEditor.CaretOffset = offset;
            ScriptEditor.Select(line.Offset, line.Length);
            ScriptEditor.ScrollToLine(lineNumber);
            ScriptEditor.Focus();
        }

        private void ScriptEditor_KeyDown(object sender, KeyEventArgs e)
        {
            bool wantsCompletion = e.Key == Key.Space &&
                (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta));
            if (!wantsCompletion)
            {
                return;
            }

            ShowCompletion(membersOnly: false);
            e.Handled = true;
        }

        private void ScriptEditor_TextEntered(object sender, TextInputEventArgs e)
        {
            if (e.Text is "." or ":")
            {
                ShowCompletion(membersOnly: true);
            }
        }

        private void ShowCompletion(bool membersOnly)
        {
            if (_viewModel?.CompletionItems is null || ScriptEditor.Document is null)
            {
                return;
            }

            List<ShowcaseCompletionItem> items = _viewModel.CompletionItems
                .Where(item => !membersOnly || item.IsMember)
                .ToList();
            if (items.Count == 0)
            {
                return;
            }

            _completionWindow?.Close();
            int startOffset = membersOnly ? ScriptEditor.CaretOffset : GetWordStartOffset(ScriptEditor.Document, ScriptEditor.CaretOffset);
            _completionWindow = new CompletionWindow(ScriptEditor.TextArea)
            {
                StartOffset = startOffset,
                EndOffset = ScriptEditor.CaretOffset,
                CloseWhenCaretAtBeginning = false
            };

            foreach (ShowcaseCompletionItem item in items)
            {
                _completionWindow.CompletionList.CompletionData.Add(new ShowcaseCompletionData(item));
            }

            _completionWindow.Closed += (_, _) => _completionWindow = null;
            _completionWindow.Show();
        }

        private static int GetWordStartOffset(TextDocument document, int caretOffset)
        {
            int offset = Math.Clamp(caretOffset, 0, document.TextLength);
            while (offset > 0)
            {
                char value = document.GetCharAt(offset - 1);
                if (!char.IsLetterOrDigit(value) && value != '_' && value != '.')
                {
                    break;
                }

                offset--;
            }

            return offset;
        }

        private void InsertScriptText(string rawText, bool replaceDocument)
        {
            if (ScriptEditor.Document is null)
            {
                return;
            }

            string text = rawText ?? string.Empty;
            int markerIndex = text.IndexOf("$0", StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                text = text.Replace("$0", string.Empty, StringComparison.Ordinal);
            }

            int caretOffset;
            if (replaceDocument)
            {
                ScriptEditor.Text = text;
                caretOffset = markerIndex >= 0 ? markerIndex : ScriptEditor.Document.TextLength;
            }
            else
            {
                int start = ScriptEditor.SelectionLength > 0 ? ScriptEditor.SelectionStart : ScriptEditor.CaretOffset;
                int length = ScriptEditor.SelectionLength > 0 ? ScriptEditor.SelectionLength : 0;
                ScriptEditor.Document.Replace(start, length, text);
                caretOffset = start + (markerIndex >= 0 ? markerIndex : text.Length);
            }

            ScriptEditor.CaretOffset = Math.Clamp(caretOffset, 0, ScriptEditor.Document.TextLength);
            ScriptEditor.Focus();
        }

        private sealed class ShowcaseCompletionData : ICompletionData
        {
            private readonly ShowcaseCompletionItem _item;

            public ShowcaseCompletionData(ShowcaseCompletionItem item)
            {
                _item = item;
            }

            public IImage Image => null;
            public string Text => _item.DisplayText;
            public object Content => _item.DisplayText;
            public object Description => _item.Description;
            public double Priority => _item.IsMember ? 0 : 1;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                string text = _item.InsertText ?? string.Empty;
                int markerIndex = text.IndexOf("$0", StringComparison.Ordinal);
                if (markerIndex >= 0)
                {
                    text = text.Replace("$0", string.Empty, StringComparison.Ordinal);
                }

                int start = completionSegment.Offset;
                textArea.Document.Replace(completionSegment, text);
                textArea.Caret.Offset = start + (markerIndex >= 0 ? markerIndex : text.Length);
            }
        }
    }
}
