using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using EffectViewer.Localization;
using EffectViewer.ViewModels;

namespace EffectViewer.Views
{
    public partial class ShowcaseEditorView : UserControl
    {
        private ShowcaseEditorViewModel _viewModel;
        private bool _isSyncingEditorText;
        private bool _isAcceptingCompletion;
        private bool _isPointerInteractingWithInlineCompletion;
        private bool _isApplyingScriptUndoRedo;
        private readonly Stack<ScriptEditSnapshot> _scriptUndoStack = new();
        private readonly Stack<ScriptEditSnapshot> _scriptRedoStack = new();
        private ScriptEditSnapshot _currentScriptEditSnapshot;
        private ScriptEditSnapshot _pendingScriptEditBeforeChange;
        private TypingUndoGroupKind? _typingUndoGroupKind;
        private int _inlineCompletionStartOffset;
        private int _inlineCompletionEndOffset;

        private const double InlineCompletionMargin = 8;
        private const double InlineCompletionVerticalGap = 2;
        private const double InlineCompletionMaxHeight = 220;
        private const double InlineCompletionMinimumHeight = 72;

        public ShowcaseEditorView()
        {
            InitializeComponent();
            ConfigureScriptEditor();
            DataContextChanged += OnDataContextChanged;
            AttachViewModel(DataContext as ShowcaseEditorViewModel);
        }

        private void ConfigureScriptEditor()
        {
            ScriptEditor.IsUndoEnabled = false;
            ScriptEditor.TextChanging += ScriptEditor_TextChanging;
            ScriptEditor.TextChanged += ScriptEditor_TextChanged;
            ScriptEditor.PropertyChanged += ScriptEditor_PropertyChanged;
            ScriptEditor.SizeChanged += ScriptEditor_SizeChanged;
            ScriptEditor.LostFocus += ScriptEditor_LostFocus;
            ScriptEditor.AddHandler(InputElement.KeyDownEvent, ScriptEditor_KeyDown, RoutingStrategies.Tunnel);
            ScriptEditor.AddHandler(InputElement.PointerPressedEvent, ScriptEditor_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            InlineCompletionList.AddHandler(InputElement.PointerPressedEvent, InlineCompletionList_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            InlineCompletionList.AddHandler(InputElement.PointerReleasedEvent, InlineCompletionList_PointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            InlineCompletionList.AddHandler(InputElement.TappedEvent, InlineCompletionList_Tapped, RoutingStrategies.Bubble, handledEventsToo: true);
            ScriptEditor.PlaceholderText = LocalizationManager.Instance.Text("Placeholder.ShowcaseScript");
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            ScriptEditor.PlaceholderText = LocalizationManager.Instance.Text("Placeholder.ShowcaseScript");
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
                _viewModel.ScriptUndoRequested -= OnScriptUndoRequested;
                _viewModel.ScriptRedoRequested -= OnScriptRedoRequested;
            }

            _viewModel = viewModel;
            if (_viewModel is not null)
            {
                _viewModel.ScriptEditRequested += OnScriptEditRequested;
                _viewModel.ScriptNavigationRequested += OnScriptNavigationRequested;
                _viewModel.ScriptUndoRequested += OnScriptUndoRequested;
                _viewModel.ScriptRedoRequested += OnScriptRedoRequested;
                SetEditorText(_viewModel.ScriptText);
            }
            else
            {
                SetEditorText(string.Empty);
            }
        }

        private void ScriptEditor_TextChanging(object sender, TextChangingEventArgs e)
        {
            if (_isSyncingEditorText || _isApplyingScriptUndoRedo)
            {
                return;
            }

            _pendingScriptEditBeforeChange = CreateScriptEditSnapshot();
        }

        private void ScriptEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecordScriptEditForUndo();
            SyncScriptTextFromEditor(ScriptEditor.Text);
            UpdateScriptUndoRedoState();
            if (_isSyncingEditorText || _isAcceptingCompletion || _isApplyingScriptUndoRedo)
            {
                return;
            }

            if (HasJustTypedMemberSeparator())
            {
                ShowCompletion(membersOnly: true);
            }
            else if (InlineCompletionHost.IsVisible)
            {
                RefreshInlineCompletion();
            }
        }

        private void ScriptEditor_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_typingUndoGroupKind.HasValue ||
                _isSyncingEditorText ||
                _isApplyingScriptUndoRedo ||
                e.Property != TextBox.CaretIndexProperty &&
                e.Property != TextBox.SelectionStartProperty &&
                e.Property != TextBox.SelectionEndProperty)
            {
                return;
            }

            if (_currentScriptEditSnapshot is null ||
                string.Equals(_currentScriptEditSnapshot.Text, GetScriptEditorText(), StringComparison.Ordinal))
            {
                _currentScriptEditSnapshot = CreateScriptEditSnapshot();
            }
        }

        private bool HasJustTypedMemberSeparator()
        {
            string text = GetScriptEditorText();
            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, 0, text.Length);
            return caretOffset > 0 && IsMemberSeparator(text[caretOffset - 1]);
        }

        private void SyncScriptTextFromEditor(string text)
        {
            if (_isSyncingEditorText || _viewModel is null)
            {
                return;
            }

            text ??= string.Empty;
            if (!string.Equals(_viewModel.ScriptText, text, StringComparison.Ordinal))
            {
                _viewModel.ScriptText = text;
            }
        }

        private void SetEditorText(string text)
        {
            string normalizedText = text ?? string.Empty;
            if (string.Equals(GetScriptEditorText(), normalizedText, StringComparison.Ordinal))
            {
                ClearScriptUndoHistory();
                UpdateScriptUndoRedoState();
                return;
            }

            _isSyncingEditorText = true;
            try
            {
                SetScriptEditorTextWithoutUndo(normalizedText);
            }
            finally
            {
                _isSyncingEditorText = false;
            }

            ClearScriptUndoHistory();
            UpdateScriptUndoRedoState();
        }

        private string GetScriptEditorText()
        {
            return ScriptEditor.Text ?? string.Empty;
        }

        private void SetScriptEditorTextWithoutUndo(string text)
        {
            bool wasUndoEnabled = ScriptEditor.IsUndoEnabled;
            ScriptEditor.IsUndoEnabled = false;
            ScriptEditor.Text = text;
            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, 0, text.Length);
            ScriptEditor.CaretIndex = caretOffset;
            ScriptEditor.SelectionStart = caretOffset;
            ScriptEditor.SelectionEnd = caretOffset;
            ScriptEditor.IsUndoEnabled = wasUndoEnabled;
        }

        private void OnScriptEditRequested(object sender, ShowcaseScriptEditRequest request)
        {
            if (request is null)
            {
                return;
            }

            InsertScriptText(request.Text, request.ReplaceDocument);
        }

        private void OnScriptUndoRequested(object sender, EventArgs e)
        {
            HideInlineCompletion();
            UndoScriptEdit();
        }

        private void OnScriptRedoRequested(object sender, EventArgs e)
        {
            HideInlineCompletion();
            RedoScriptEdit();
        }

        private void OnScriptNavigationRequested(object sender, ShowcaseScriptNavigationRequest request)
        {
            if (request is null)
            {
                return;
            }

            string text = GetScriptEditorText();
            (int lineStart, int lineEnd) = GetLineSpan(text, request.LineNumber);
            int caretOffset = Math.Clamp(lineStart + Math.Max(0, request.ColumnNumber - 1), lineStart, lineEnd);

            ScriptEditor.CaretIndex = caretOffset;
            ScriptEditor.SelectionStart = lineStart;
            ScriptEditor.SelectionEnd = lineEnd;
            int lineIndex = Math.Max(0, request.LineNumber - 1);
            int lineCount = ScriptEditor.GetLineCount();
            if (lineCount > lineIndex)
            {
                ScriptEditor.ScrollToLine(lineIndex);
            }

            FocusScriptEditor();
        }

        private void ScriptEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (ShouldEndTypingUndoGroup(e))
            {
                EndTypingUndoGroup();
            }

            if (HandleScriptUndoRedoKey(e))
            {
                e.Handled = true;
                return;
            }

            if (InlineCompletionHost.IsVisible && HandleInlineCompletionKey(e))
            {
                e.Handled = true;
                return;
            }

            bool wantsCompletion = e.Key == Key.Space &&
                (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta));
            if (!wantsCompletion)
            {
                return;
            }

            ShowCompletion(membersOnly: false);
            e.Handled = true;
        }

        private void CompletionButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCompletion(membersOnly: false);
            FocusScriptEditor();
        }

        private void ScriptEditor_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            EndTypingUndoGroup();
            HideInlineCompletion();
            FocusScriptEditor();
        }

        private void FocusScriptEditor()
        {
            ScriptEditor.Focus();
        }

        private void UpdateScriptUndoRedoState()
        {
            _viewModel?.SetScriptUndoRedoState(_scriptUndoStack.Count > 0, _scriptRedoStack.Count > 0);
        }

        private void RecordScriptEditForUndo()
        {
            ScriptEditSnapshot after = CreateScriptEditSnapshot();
            if (_isSyncingEditorText || _isApplyingScriptUndoRedo)
            {
                _pendingScriptEditBeforeChange = null;
                _currentScriptEditSnapshot = after;
                return;
            }

            ScriptEditSnapshot before = _pendingScriptEditBeforeChange ?? _currentScriptEditSnapshot ?? after;
            _pendingScriptEditBeforeChange = null;
            if (string.Equals(before.Text, after.Text, StringComparison.Ordinal))
            {
                _currentScriptEditSnapshot = after;
                return;
            }

            TypingUndoGroupKind? editKind = GetTypingUndoGroupKind(before, after);
            bool continuesTypingGroup =
                editKind.HasValue &&
                _typingUndoGroupKind == editKind &&
                before.HasSameState(_currentScriptEditSnapshot);

            if (!continuesTypingGroup)
            {
                PushUndoSnapshot(before);
                _typingUndoGroupKind = editKind;
            }

            if (!editKind.HasValue)
            {
                EndTypingUndoGroup();
            }

            _scriptRedoStack.Clear();
            _currentScriptEditSnapshot = after;
        }

        private void PushUndoSnapshot(ScriptEditSnapshot snapshot)
        {
            if (snapshot is null ||
                _scriptUndoStack.TryPeek(out ScriptEditSnapshot previous) &&
                previous.HasSameState(snapshot))
            {
                return;
            }

            _scriptUndoStack.Push(snapshot);
        }

        private void UndoScriptEdit()
        {
            EndTypingUndoGroup();
            if (_scriptUndoStack.Count == 0)
            {
                UpdateScriptUndoRedoState();
                return;
            }

            ScriptEditSnapshot current = CreateScriptEditSnapshot();
            ScriptEditSnapshot previous = _scriptUndoStack.Pop();
            _scriptRedoStack.Push(current);
            ApplyScriptEditSnapshot(previous);
            UpdateScriptUndoRedoState();
        }

        private void RedoScriptEdit()
        {
            EndTypingUndoGroup();
            if (_scriptRedoStack.Count == 0)
            {
                UpdateScriptUndoRedoState();
                return;
            }

            ScriptEditSnapshot current = CreateScriptEditSnapshot();
            ScriptEditSnapshot next = _scriptRedoStack.Pop();
            _scriptUndoStack.Push(current);
            ApplyScriptEditSnapshot(next);
            UpdateScriptUndoRedoState();
        }

        private void ApplyScriptEditSnapshot(ScriptEditSnapshot snapshot)
        {
            if (snapshot is null)
            {
                return;
            }

            _isApplyingScriptUndoRedo = true;
            try
            {
                SetScriptEditorTextWithoutUndo(snapshot.Text);
                int textLength = GetScriptEditorText().Length;
                int selectionStart = Math.Clamp(snapshot.SelectionStart, 0, textLength);
                int selectionEnd = Math.Clamp(snapshot.SelectionEnd, 0, textLength);
                int caretIndex = Math.Clamp(snapshot.CaretIndex, 0, textLength);
                ScriptEditor.SelectionStart = selectionStart;
                ScriptEditor.SelectionEnd = selectionEnd;
                ScriptEditor.CaretIndex = caretIndex;
            }
            finally
            {
                _isApplyingScriptUndoRedo = false;
                _pendingScriptEditBeforeChange = null;
                _currentScriptEditSnapshot = CreateScriptEditSnapshot();
            }

            FocusScriptEditor();
        }

        private void ClearScriptUndoHistory()
        {
            EndTypingUndoGroup();
            _scriptUndoStack.Clear();
            _scriptRedoStack.Clear();
            _pendingScriptEditBeforeChange = null;
            _currentScriptEditSnapshot = CreateScriptEditSnapshot();
        }

        private void EndTypingUndoGroup()
        {
            _typingUndoGroupKind = null;
        }

        private ScriptEditSnapshot CreateScriptEditSnapshot()
        {
            string text = GetScriptEditorText();
            int textLength = text.Length;
            return new ScriptEditSnapshot(
                text,
                Math.Clamp(ScriptEditor.CaretIndex, 0, textLength),
                Math.Clamp(ScriptEditor.SelectionStart, 0, textLength),
                Math.Clamp(ScriptEditor.SelectionEnd, 0, textLength));
        }

        private bool HandleScriptUndoRedoKey(KeyEventArgs e)
        {
            bool hasCommandModifier =
                e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            if (!hasCommandModifier)
            {
                return false;
            }

            if (e.Key == Key.Z)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    RedoScriptEdit();
                }
                else
                {
                    UndoScriptEdit();
                }

                return true;
            }

            if (e.Key == Key.Y)
            {
                RedoScriptEdit();
                return true;
            }

            return false;
        }

        private static bool ShouldEndTypingUndoGroup(KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                return true;
            }

            return e.Key is Key.Back
                or Key.Delete
                or Key.Enter
                or Key.Tab
                or Key.Escape
                or Key.Left
                or Key.Right
                or Key.Up
                or Key.Down
                or Key.Home
                or Key.End
                or Key.PageUp
                or Key.PageDown;
        }

        private static TypingUndoGroupKind? GetTypingUndoGroupKind(
            ScriptEditSnapshot before,
            ScriptEditSnapshot after)
        {
            if (!TryGetPureInsertedText(before.Text, after.Text, out string insertedText) ||
                string.IsNullOrEmpty(insertedText))
            {
                return null;
            }

            if (insertedText.All(IsIdentifierPart))
            {
                return TypingUndoGroupKind.Word;
            }

            if (insertedText.All(char.IsWhiteSpace))
            {
                return TypingUndoGroupKind.Whitespace;
            }

            return insertedText.Length == 1 ? TypingUndoGroupKind.Symbol : null;
        }

        private static bool TryGetPureInsertedText(string before, string after, out string insertedText)
        {
            before ??= string.Empty;
            after ??= string.Empty;
            insertedText = string.Empty;
            if (after.Length <= before.Length)
            {
                return false;
            }

            int prefixLength = 0;
            while (prefixLength < before.Length &&
                prefixLength < after.Length &&
                before[prefixLength] == after[prefixLength])
            {
                prefixLength++;
            }

            int suffixLength = 0;
            while (suffixLength < before.Length - prefixLength &&
                suffixLength < after.Length - prefixLength &&
                before[before.Length - suffixLength - 1] == after[after.Length - suffixLength - 1])
            {
                suffixLength++;
            }

            if (before.Length - prefixLength - suffixLength != 0)
            {
                return false;
            }

            insertedText = after.Substring(prefixLength, after.Length - before.Length);
            return true;
        }

        private void ScriptEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            EndTypingUndoGroup();
            if (_isPointerInteractingWithInlineCompletion)
            {
                return;
            }

            HideInlineCompletion();
        }

        private void ScriptEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateInlineCompletionPlacement();
        }

        private void ShowCompletion(bool membersOnly)
        {
            if (_viewModel?.CompletionItems is null)
            {
                return;
            }

            CompletionRequest request = CreateCompletionRequest(membersOnly);
            if (request.Suppress)
            {
                return;
            }

            List<ShowcaseCompletionItem> items = _viewModel.CompletionItems
                .Where(item => request.AllMembers ? item.IsMember : item.Matches(request.Scope))
                .DistinctBy(item => $"{item.DisplayText}\n{item.InsertText}")
                .Where(item => MatchesCompletionPrefix(item, request.StartOffset))
                .OrderBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count == 0)
            {
                HideInlineCompletion();
                return;
            }

            ShowInlineCompletion(items, request.StartOffset, ScriptEditor.CaretIndex);
        }

        private void RefreshInlineCompletion()
        {
            if (_viewModel?.CompletionItems is null)
            {
                return;
            }

            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, 0, GetScriptEditorText().Length);
            if (caretOffset < _inlineCompletionStartOffset)
            {
                HideInlineCompletion();
                return;
            }

            CompletionRequest request = CreateCompletionRequest(membersOnly: false);
            if (request.Suppress || request.StartOffset != _inlineCompletionStartOffset)
            {
                HideInlineCompletion();
                return;
            }

            List<ShowcaseCompletionItem> items = _viewModel.CompletionItems
                .Where(item => request.AllMembers ? item.IsMember : item.Matches(request.Scope))
                .DistinctBy(item => $"{item.DisplayText}\n{item.InsertText}")
                .Where(item => MatchesCompletionPrefix(item, request.StartOffset))
                .OrderBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count == 0)
            {
                HideInlineCompletion();
                return;
            }

            _inlineCompletionEndOffset = caretOffset;
            InlineCompletionList.ItemsSource = items;
            InlineCompletionList.SelectedIndex = 0;
            UpdateInlineCompletionPlacement();
        }

        private bool MatchesCompletionPrefix(ShowcaseCompletionItem item, int startOffset)
        {
            string prefix = GetCompletionPrefix(startOffset);
            return string.IsNullOrEmpty(prefix) ||
                item.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                (item.InsertText?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private string GetCompletionPrefix(int startOffset)
        {
            string text = GetScriptEditorText();
            int start = Math.Clamp(startOffset, 0, text.Length);
            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, start, text.Length);
            return text[start..caretOffset];
        }

        private void ShowInlineCompletion(IEnumerable<ShowcaseCompletionItem> items, int startOffset, int endOffset)
        {
            List<ShowcaseCompletionItem> completionData = items.ToList();
            InlineCompletionList.ItemsSource = completionData;
            InlineCompletionList.SelectedIndex = completionData.Count > 0 ? 0 : -1;
            _inlineCompletionStartOffset = startOffset;
            _inlineCompletionEndOffset = endOffset;
            InlineCompletionHost.IsVisible = completionData.Count > 0;
            UpdateInlineCompletionPlacement();
        }

        private void HideInlineCompletion()
        {
            InlineCompletionHost.IsVisible = false;
            InlineCompletionList.ItemsSource = null;
            InlineCompletionList.SelectedIndex = -1;
            _isPointerInteractingWithInlineCompletion = false;
        }

        private void InlineCompletionList_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _isPointerInteractingWithInlineCompletion = true;
        }

        private void InlineCompletionList_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isPointerInteractingWithInlineCompletion = false;
        }

        private void InlineCompletionList_Tapped(object sender, TappedEventArgs e)
        {
            if (TryGetInlineCompletionData(e.GetPosition(InlineCompletionList), out ShowcaseCompletionItem completionData))
            {
                AcceptInlineCompletion(completionData);
                e.Handled = true;
            }
        }

        private bool TryGetInlineCompletionData(Point position, out ShowcaseCompletionItem completionData)
        {
            foreach (Visual visual in InlineCompletionList.GetVisualsAt(position))
            {
                if (TryGetCompletionDataFromVisual(visual, out completionData))
                {
                    return true;
                }
            }

            completionData = null;
            return false;
        }

        private static bool TryGetCompletionDataFromVisual(Visual visual, out ShowcaseCompletionItem completionData)
        {
            for (; visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is Control { DataContext: ShowcaseCompletionItem item })
                {
                    completionData = item;
                    return true;
                }
            }

            completionData = null;
            return false;
        }

        private void AcceptInlineCompletion(ShowcaseCompletionItem completionData)
        {
            if (_isAcceptingCompletion || completionData is null)
            {
                return;
            }

            _isAcceptingCompletion = true;
            try
            {
                int textLength = GetScriptEditorText().Length;
                int endOffset = Math.Clamp(ScriptEditor.CaretIndex, _inlineCompletionStartOffset, textLength);
                _inlineCompletionEndOffset = Math.Max(_inlineCompletionEndOffset, endOffset);
                HideInlineCompletion();
                ReplaceScriptText(
                    _inlineCompletionStartOffset,
                    Math.Max(0, _inlineCompletionEndOffset - _inlineCompletionStartOffset),
                    completionData.InsertText);
                FocusScriptEditor();
                UpdateScriptUndoRedoState();
            }
            finally
            {
                _isAcceptingCompletion = false;
            }
        }

        private bool HandleInlineCompletionKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    HideInlineCompletion();
                    return true;
                case Key.Up:
                    MoveInlineCompletionSelection(-1);
                    return true;
                case Key.Down:
                    MoveInlineCompletionSelection(1);
                    return true;
                case Key.Enter:
                case Key.Tab:
                    if (InlineCompletionList.SelectedItem is ShowcaseCompletionItem completionData)
                    {
                        AcceptInlineCompletion(completionData);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private void MoveInlineCompletionSelection(int direction)
        {
            if (InlineCompletionList.ItemsSource is not ICollection<ShowcaseCompletionItem> items || items.Count == 0)
            {
                return;
            }

            int selectedIndex = InlineCompletionList.SelectedIndex >= 0 ? InlineCompletionList.SelectedIndex : 0;
            InlineCompletionList.SelectedIndex = Math.Clamp(selectedIndex + direction, 0, items.Count - 1);
        }

        private void UpdateInlineCompletionPlacement()
        {
            if (!InlineCompletionHost.IsVisible ||
                InlineCompletionHost.GetVisualParent() is not Control parent)
            {
                return;
            }

            double parentHeight = parent.Bounds.Height;
            InlineCompletionCaretLineBounds caretLineBounds = TryGetInlineCompletionCaretLineBounds(parent, out InlineCompletionCaretLineBounds bounds)
                ? bounds
                : new InlineCompletionCaretLineBounds(InlineCompletionMargin, InlineCompletionMargin);

            double belowTop = Math.Max(InlineCompletionMargin, caretLineBounds.Bottom + InlineCompletionVerticalGap);
            bool hasParentHeight = !double.IsNaN(parentHeight) && parentHeight > 0;
            if (!hasParentHeight)
            {
                InlineCompletionHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                InlineCompletionHost.Margin = new Thickness(
                    InlineCompletionMargin,
                    belowTop,
                    InlineCompletionMargin,
                    InlineCompletionMargin);
                SetInlineCompletionMaxHeight(InlineCompletionMaxHeight);
                return;
            }

            double availableBelow = Math.Max(0, parentHeight - belowTop - InlineCompletionMargin);
            double aboveBottom = Math.Clamp(
                caretLineBounds.Top - InlineCompletionVerticalGap,
                InlineCompletionMargin,
                Math.Max(InlineCompletionMargin, parentHeight - InlineCompletionMargin));
            double availableAbove = Math.Max(0, aboveBottom - InlineCompletionMargin);
            bool placeAbove = availableBelow < InlineCompletionMinimumHeight && availableAbove > availableBelow;

            if (placeAbove)
            {
                InlineCompletionHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
                InlineCompletionHost.Margin = new Thickness(
                    InlineCompletionMargin,
                    InlineCompletionMargin,
                    InlineCompletionMargin,
                    Math.Max(InlineCompletionMargin, parentHeight - aboveBottom));
                SetInlineCompletionMaxHeight(Math.Min(InlineCompletionMaxHeight, availableAbove));
                return;
            }

            InlineCompletionHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            InlineCompletionHost.Margin = new Thickness(
                InlineCompletionMargin,
                belowTop,
                InlineCompletionMargin,
                InlineCompletionMargin);
            SetInlineCompletionMaxHeight(Math.Min(InlineCompletionMaxHeight, availableBelow));
        }

        private void SetInlineCompletionMaxHeight(double maxHeight)
        {
            double normalizedMaxHeight = Math.Max(0, maxHeight);
            InlineCompletionHost.MaxHeight = normalizedMaxHeight;
            InlineCompletionList.MaxHeight = normalizedMaxHeight;
        }

        private bool TryGetInlineCompletionCaretLineBounds(Control parent, out InlineCompletionCaretLineBounds bounds)
        {
            if (TryGetTextPresenterCaretLineBounds(parent, out bounds))
            {
                return true;
            }

            return TryGetEstimatedCaretLineBounds(out bounds);
        }

        private bool TryGetTextPresenterCaretLineBounds(Control parent, out InlineCompletionCaretLineBounds bounds)
        {
            TextPresenter presenter = ScriptEditor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
            TextLayout layout = presenter?.TextLayout;
            if (presenter is null || layout?.TextLines is not { Count: > 0 } lines)
            {
                bounds = default;
                return false;
            }

            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, 0, GetScriptEditorText().Length);
            double lineTop = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                TextLine line = lines[i];
                double lineHeight = Math.Max(1, line.Height);
                int lineEnd = line.FirstTextSourceIndex + line.Length;
                if (i < lines.Count - 1 && caretOffset >= lines[i + 1].FirstTextSourceIndex)
                {
                    lineTop += lineHeight;
                    continue;
                }

                if (caretOffset <= lineEnd)
                {
                    Point? parentLineTop = presenter.TranslatePoint(new Point(0, lineTop), parent);
                    Point? parentLineBottom = presenter.TranslatePoint(new Point(0, lineTop + lineHeight), parent);
                    if (parentLineTop is not null && parentLineBottom is not null)
                    {
                        bounds = new InlineCompletionCaretLineBounds(
                            parentLineTop.Value.Y,
                            parentLineBottom.Value.Y);
                        return true;
                    }

                    break;
                }

                lineTop += lineHeight;
            }

            bounds = default;
            return false;
        }

        private bool TryGetEstimatedCaretLineBounds(out InlineCompletionCaretLineBounds bounds)
        {
            string text = GetScriptEditorText();
            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, 0, text.Length);
            int lineIndex = 0;
            for (int i = 0; i < caretOffset; i++)
            {
                if (text[i] == '\n')
                {
                    lineIndex++;
                }
            }

            double lineHeight = double.IsNaN(ScriptEditor.LineHeight) || ScriptEditor.LineHeight <= 0
                ? ScriptEditor.FontSize * 1.4
                : ScriptEditor.LineHeight;
            double top = ScriptEditor.Padding.Top + lineIndex * lineHeight;
            bounds = new InlineCompletionCaretLineBounds(top, top + lineHeight);
            return lineHeight > 0;
        }

        private CompletionRequest CreateCompletionRequest(bool membersOnly)
        {
            string text = GetScriptEditorText();
            int caretOffset = Math.Clamp(ScriptEditor.CaretIndex, 0, text.Length);
            int wordStart = GetIdentifierStartOffset(text, caretOffset);
            int separatorOffset = -1;

            if (membersOnly && caretOffset > 0 && IsMemberSeparator(text[caretOffset - 1]))
            {
                separatorOffset = caretOffset - 1;
                wordStart = caretOffset;
            }
            else if (wordStart > 0 && IsMemberSeparator(text[wordStart - 1]))
            {
                separatorOffset = wordStart - 1;
            }

            if (separatorOffset >= 0)
            {
                string receiver = GetReceiverBeforeSeparator(text, separatorOffset);
                if (string.IsNullOrWhiteSpace(receiver) || !IsIdentifierStart(receiver[0]))
                {
                    return CompletionRequest.Suppressed(caretOffset);
                }

                string precedingText = text[..Math.Clamp(separatorOffset, 0, text.Length)];
                ShowcaseCompletionScope? scope = ResolveCompletionScope(receiver, precedingText);
                return new CompletionRequest(wordStart, scope ?? ShowcaseCompletionScope.Reanimation, !scope.HasValue);
            }

            return new CompletionRequest(wordStart, ShowcaseCompletionScope.Global, allMembers: false);
        }

        private static int GetIdentifierStartOffset(string text, int caretOffset)
        {
            int offset = Math.Clamp(caretOffset, 0, text.Length);
            while (offset > 0)
            {
                char value = text[offset - 1];
                if (!IsIdentifierPart(value))
                {
                    break;
                }

                offset--;
            }

            return offset;
        }

        private static string GetReceiverBeforeSeparator(string text, int separatorOffset)
        {
            int endOffset = Math.Clamp(separatorOffset, 0, text.Length);
            int startOffset = endOffset;
            while (startOffset > 0 && IsIdentifierPart(text[startOffset - 1]))
            {
                startOffset--;
            }

            return startOffset == endOffset ? string.Empty : text[startOffset..endOffset];
        }

        private static ShowcaseCompletionScope? ResolveCompletionScope(string receiver, string text)
        {
            if (string.IsNullOrWhiteSpace(receiver))
            {
                return null;
            }

            return receiver switch
            {
                "scene" => ShowcaseCompletionScope.Scene,
                "global_attachment" => ShowcaseCompletionScope.AttachmentApi,
                "g" or "graphics" => ShowcaseCompletionScope.Graphics,
                _ => InferReceiverScope(receiver, text)
            };
        }

        private static ShowcaseCompletionScope? InferReceiverScope(string receiver, string text)
        {
            string escapedReceiver = Regex.Escape(receiver);
            if (Regex.IsMatch(
                text,
                $@"function\s+[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*draw\s*\(\s*{escapedReceiver}\s*\)",
                RegexOptions.CultureInvariant))
            {
                return ShowcaseCompletionScope.Graphics;
            }

            int bestIndex = -1;
            ShowcaseCompletionScope? bestScope = null;
            foreach ((ShowcaseCompletionScope scope, string expressionPattern) in TypeInferenceRules)
            {
                string pattern = $@"(?<![A-Za-z0-9_])(?:local\s+)?{escapedReceiver}\s*=\s*{expressionPattern}";
                foreach (Match match in Regex.Matches(text, pattern, RegexOptions.CultureInvariant | RegexOptions.Multiline))
                {
                    if (match.Index >= bestIndex)
                    {
                        bestIndex = match.Index;
                        bestScope = scope;
                    }
                }
            }

            return bestScope;
        }

        private static bool IsMemberSeparator(char value)
        {
            return value is '.' or ':';
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsIdentifierStart(char value)
        {
            return char.IsLetter(value) || value == '_';
        }

        private static (int Start, int End) GetLineSpan(string text, int lineNumber)
        {
            text ??= string.Empty;
            int targetLine = Math.Max(1, lineNumber);
            int currentLine = 1;
            int lineStart = 0;

            for (int i = 0; i < text.Length && currentLine < targetLine; i++)
            {
                if (text[i] == '\n')
                {
                    currentLine++;
                    lineStart = i + 1;
                }
            }

            int lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
            {
                lineEnd--;
            }

            return (lineStart, lineEnd);
        }

        private void InsertScriptText(string rawText, bool replaceDocument)
        {
            string text = NormalizeSnippetText(rawText, out int markerIndex);
            int caretOffset;
            if (replaceDocument)
            {
                ScriptEditor.Text = text;
                caretOffset = markerIndex >= 0 ? markerIndex : text.Length;
            }
            else
            {
                int start = GetSelectionStart();
                ScriptEditor.SelectedText = text;
                caretOffset = start + (markerIndex >= 0 ? markerIndex : text.Length);
            }

            MoveCaret(caretOffset);
            FocusScriptEditor();
            UpdateScriptUndoRedoState();
        }

        private void ReplaceScriptText(int start, int length, string rawText)
        {
            string text = NormalizeSnippetText(rawText, out int markerIndex);
            string currentText = GetScriptEditorText();
            int normalizedStart = Math.Clamp(start, 0, currentText.Length);
            int normalizedLength = Math.Clamp(length, 0, currentText.Length - normalizedStart);

            ScriptEditor.SelectionStart = normalizedStart;
            ScriptEditor.SelectionEnd = normalizedStart + normalizedLength;
            ScriptEditor.SelectedText = text;
            MoveCaret(normalizedStart + (markerIndex >= 0 ? markerIndex : text.Length));
        }

        private static string NormalizeSnippetText(string rawText, out int markerIndex)
        {
            string text = rawText ?? string.Empty;
            markerIndex = text.IndexOf("$0", StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                text = text.Replace("$0", string.Empty, StringComparison.Ordinal);
            }

            return text;
        }

        private int GetSelectionStart()
        {
            return Math.Clamp(Math.Min(ScriptEditor.SelectionStart, ScriptEditor.SelectionEnd), 0, GetScriptEditorText().Length);
        }

        private void MoveCaret(int offset)
        {
            int caretOffset = Math.Clamp(offset, 0, GetScriptEditorText().Length);
            ScriptEditor.CaretIndex = caretOffset;
            ScriptEditor.SelectionStart = caretOffset;
            ScriptEditor.SelectionEnd = caretOffset;
        }

        private static readonly IReadOnlyList<(ShowcaseCompletionScope Scope, string ExpressionPattern)> TypeInferenceRules =
        [
            (ShowcaseCompletionScope.Reanimation, @"scene\s*[:\.]\s*(?:reanim|find_reanim|reanim_at|reanim_get|reanim_try_to_get)\s*\("),
            (ShowcaseCompletionScope.Particle, @"scene\s*[:\.]\s*(?:particle_system|find_particle|particle_at|particle_system_get|particle_system_try_to_get)\s*\("),
            (ShowcaseCompletionScope.ParticleEmitter, @"scene\s*[:\.]\s*(?:emitter_get|emitter_try_to_get)\s*\("),
            (ShowcaseCompletionScope.ParticleInstance, @"scene\s*[:\.]\s*(?:particle_get|particle_try_to_get|particle_instance_get|particle_instance_try_to_get)\s*\("),
            (ShowcaseCompletionScope.Trail, @"scene\s*[:\.]\s*(?:trail|find_trail|trail_at|trail_get|trail_try_to_get)\s*\("),
            (ShowcaseCompletionScope.Attachment, @"scene\s*[:\.]\s*(?:attachment_get|attachment_try_to_get)\s*\("),
            (ShowcaseCompletionScope.SceneObject, @"scene\s*[:\.]\s*(?:object_at|find_object)\s*\("),
            (ShowcaseCompletionScope.Image, @"scene\s*[:\.]\s*get_image\s*\("),
            (ShowcaseCompletionScope.Matrix, @"scene\s*[:\.]\s*matrix3x3\s*\("),
            (ShowcaseCompletionScope.Vector, @"scene\s*[:\.]\s*vector2\s*\("),
            (ShowcaseCompletionScope.Vector3, @"scene\s*[:\.]\s*vector3\s*\("),
            (ShowcaseCompletionScope.TriVertex, @"scene\s*[:\.]\s*tri_vertex\s*\("),
            (ShowcaseCompletionScope.ReanimationTrack, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:track|track_at|get_track_instance|get_track_instance_by_name)\s*\("),
            (ShowcaseCompletionScope.ReanimationTransform, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:transform|transform_at|current_transform|current_transform_at)\s*\("),
            (ShowcaseCompletionScope.ReanimationFrameRange, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*frame_range\s*\("),
            (ShowcaseCompletionScope.FrameTime, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*frame_time\s*\("),
            (ShowcaseCompletionScope.ParticleEmitter, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:emitter|find_emitter_by_name|emitter_at)\s*\("),
            (ShowcaseCompletionScope.ParticleInstance, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:particle|spawn_particle)\s*\("),
            (ShowcaseCompletionScope.ParticleRenderParams, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*get_render_params\s*\("),
            (ShowcaseCompletionScope.TrailPoint, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:point|get_trail_point)\s*\("),
            (ShowcaseCompletionScope.Attachment, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:attachment|find_first_attachment)\s*\("),
            (ShowcaseCompletionScope.AttachmentEffect, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:effect|get_effect)\s*\("),
            (ShowcaseCompletionScope.Font, @"scene\s*[:\.]\s*get_font\s*\("),
            (ShowcaseCompletionScope.Matrix, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:matrix|get_overlay_matrix|track_matrix|track_matrix_at|get_track_matrix|attachment_overlay_matrix|attachment_overlay_matrix_at|get_attachment_overlay_matrix|track_base_pose_matrix|track_base_pose_matrix_at|get_track_base_pos_matrix|offset_matrix|get_offset|base_pose_matrix)\s*\("),
            (ShowcaseCompletionScope.Vector, @"[A-Za-z_][A-Za-z0-9_]*\s*[:\.]\s*(?:position|normal|normal_at|get_perp)\s*\(")
        ];

        private sealed class CompletionRequest
        {
            private CompletionRequest(int startOffset, ShowcaseCompletionScope scope, bool allMembers, bool suppress)
            {
                StartOffset = startOffset;
                Scope = scope;
                AllMembers = allMembers;
                Suppress = suppress;
            }

            public CompletionRequest(int startOffset, ShowcaseCompletionScope scope, bool allMembers)
                : this(startOffset, scope, allMembers, suppress: false)
            {
            }

            public static CompletionRequest Suppressed(int startOffset)
            {
                return new CompletionRequest(startOffset, ShowcaseCompletionScope.Global, allMembers: false, suppress: true);
            }

            public int StartOffset { get; }
            public ShowcaseCompletionScope Scope { get; }
            public bool AllMembers { get; }
            public bool Suppress { get; }
        }

        private readonly record struct InlineCompletionCaretLineBounds(double Top, double Bottom);

        private sealed class ScriptEditSnapshot
        {
            public ScriptEditSnapshot(string text, int caretIndex, int selectionStart, int selectionEnd)
            {
                Text = text ?? string.Empty;
                CaretIndex = caretIndex;
                SelectionStart = selectionStart;
                SelectionEnd = selectionEnd;
            }

            public string Text { get; }
            public int CaretIndex { get; }
            public int SelectionStart { get; }
            public int SelectionEnd { get; }

            public bool HasSameState(ScriptEditSnapshot other)
            {
                return other is not null &&
                    CaretIndex == other.CaretIndex &&
                    SelectionStart == other.SelectionStart &&
                    SelectionEnd == other.SelectionEnd &&
                    string.Equals(Text, other.Text, StringComparison.Ordinal);
            }
        }

        private enum TypingUndoGroupKind
        {
            Word,
            Whitespace,
            Symbol
        }
    }
}
