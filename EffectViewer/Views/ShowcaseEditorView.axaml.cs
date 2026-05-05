using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using EffectViewer.Localization;
using EffectViewer.ViewModels;

namespace EffectViewer.Views
{
    public partial class ShowcaseEditorView : UserControl
    {
        private ShowcaseEditorViewModel _viewModel;
        private bool _isSyncingEditorText;
        private TypingUndoGroupKind? _typingUndoGroupKind;
        private bool _typingUndoGroupHasChanges;
        private bool _isAcceptingCompletion;
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
            ScriptEditor.TextArea.TextEntering += ScriptEditor_TextEntering;
            ScriptEditor.TextArea.TextEntered += ScriptEditor_TextEntered;
            ScriptEditor.TextArea.TextView.ScrollOffsetChanged += ScriptEditor_TextViewChanged;
            ScriptEditor.TextArea.TextView.VisualLinesChanged += ScriptEditor_TextViewChanged;
            ScriptEditor.TextArea.AddHandler(InputElement.KeyDownEvent, ScriptEditor_KeyDown, RoutingStrategies.Tunnel);
            ScriptEditor.TextArea.AddHandler(InputElement.PointerPressedEvent, ScriptEditor_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            ScriptEditor.KeyDown += ScriptEditor_KeyDown;
            ScriptEditor.AddHandler(InputElement.PointerPressedEvent, ScriptEditor_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            ScriptEditor.SizeChanged += ScriptEditor_SizeChanged;
            ScriptEditor.LostFocus += ScriptEditor_LostFocus;
            InlineCompletionList.AddHandler(InputElement.TappedEvent, InlineCompletionList_Tapped, RoutingStrategies.Bubble, handledEventsToo: true);
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

        private void ScriptEditor_TextChanged(object sender, EventArgs e)
        {
            if (_typingUndoGroupKind.HasValue)
            {
                _typingUndoGroupHasChanges = true;
            }

            UpdateScriptUndoRedoState();

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
            EndTypingUndoGroup();

            if (string.Equals(ScriptEditor.Text, text ?? string.Empty, StringComparison.Ordinal))
            {
                UpdateScriptUndoRedoState();
                return;
            }

            _isSyncingEditorText = true;
            ScriptEditor.Text = text ?? string.Empty;
            ScriptEditor.Document?.UndoStack.ClearAll();
            _isSyncingEditorText = false;
            UpdateScriptUndoRedoState();
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
            EndTypingUndoGroup();
            if (ScriptEditor.CanUndo)
            {
                ScriptEditor.Undo();
            }

            UpdateScriptUndoRedoState();
        }

        private void OnScriptRedoRequested(object sender, EventArgs e)
        {
            HideInlineCompletion();
            EndTypingUndoGroup();
            if (ScriptEditor.CanRedo)
            {
                ScriptEditor.Redo();
            }

            UpdateScriptUndoRedoState();
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

            EndTypingUndoGroup();
            ShowCompletion(membersOnly: false);
            e.Handled = true;
        }

        private void ScriptEditor_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            HideInlineCompletion();
            FocusScriptEditor();
        }

        private void FocusScriptEditor()
        {
            ScriptEditor.TextArea.Focus();
        }

        private void UpdateScriptUndoRedoState()
        {
            _viewModel?.SetScriptUndoRedoState(
                ScriptEditor.CanUndo || _typingUndoGroupHasChanges,
                ScriptEditor.CanRedo);
        }

        private void ScriptEditor_TextEntering(object sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text) || ScriptEditor.IsReadOnly)
            {
                EndTypingUndoGroup();
                return;
            }

            StartOrContinueTypingUndoGroup(GetTypingUndoGroupKind(e.Text));
        }

        private void ScriptEditor_TextEntered(object sender, TextInputEventArgs e)
        {
            if (e.Text is "." or ":")
            {
                EndTypingUndoGroup();
                ShowCompletion(membersOnly: true);
            }
        }

        private void ScriptEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            EndTypingUndoGroup();
        }

        private void ScriptEditor_TextViewChanged(object sender, EventArgs e)
        {
            UpdateInlineCompletionPlacement();
        }

        private void ScriptEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateInlineCompletionPlacement();
        }

        private void StartOrContinueTypingUndoGroup(TypingUndoGroupKind kind)
        {
            if (_typingUndoGroupKind == kind)
            {
                return;
            }

            EndTypingUndoGroup();
            ScriptEditor.Document?.UndoStack.StartUndoGroup();
            _typingUndoGroupKind = kind;
            _typingUndoGroupHasChanges = false;
        }

        private void EndTypingUndoGroup()
        {
            if (!_typingUndoGroupKind.HasValue)
            {
                return;
            }

            ScriptEditor.Document?.UndoStack.EndUndoGroup();
            _typingUndoGroupKind = null;
            _typingUndoGroupHasChanges = false;
            UpdateScriptUndoRedoState();
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

        private static TypingUndoGroupKind GetTypingUndoGroupKind(string text)
        {
            if (text.All(IsIdentifierPart))
            {
                return TypingUndoGroupKind.Word;
            }

            if (text.All(char.IsWhiteSpace))
            {
                return TypingUndoGroupKind.Whitespace;
            }

            return TypingUndoGroupKind.Symbol;
        }

        private void ShowCompletion(bool membersOnly)
        {
            if (_viewModel?.CompletionItems is null || ScriptEditor.Document is null)
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
                .OrderBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count == 0)
            {
                HideInlineCompletion();
                return;
            }

            ShowInlineCompletion(items, request.StartOffset, ScriptEditor.CaretOffset);
        }

        private void ShowInlineCompletion(IEnumerable<ShowcaseCompletionItem> items, int startOffset, int endOffset)
        {
            List<ShowcaseCompletionData> completionData = items
                .Select(item => new ShowcaseCompletionData(item))
                .ToList();
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
        }

        private void InlineCompletionList_Tapped(object sender, TappedEventArgs e)
        {
            if (TryGetInlineCompletionData(e, out ICompletionData completionData))
            {
                AcceptInlineCompletion(completionData, e);
                e.Handled = true;
            }
        }

        private bool TryGetInlineCompletionData(TappedEventArgs e, out ICompletionData completionData)
        {
            Point position = e.GetPosition(InlineCompletionList);
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

        private static bool TryGetCompletionDataFromVisual(Visual visual, out ICompletionData completionData)
        {
            for (; visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is Control { DataContext: ICompletionData item })
                {
                    completionData = item;
                    return true;
                }
            }

            completionData = null;
            return false;
        }

        private void AcceptInlineCompletion(ICompletionData completionData, EventArgs e)
        {
            if (_isAcceptingCompletion || completionData is null || ScriptEditor.Document is null)
            {
                return;
            }

            _isAcceptingCompletion = true;
            try
            {
                int endOffset = Math.Clamp(ScriptEditor.CaretOffset, _inlineCompletionStartOffset, ScriptEditor.Document.TextLength);
                _inlineCompletionEndOffset = Math.Max(_inlineCompletionEndOffset, endOffset);
                ISegment segment = new AnchorSegment(
                    ScriptEditor.Document,
                    _inlineCompletionStartOffset,
                    Math.Max(0, _inlineCompletionEndOffset - _inlineCompletionStartOffset));
                HideInlineCompletion();
                completionData.Complete(ScriptEditor.TextArea, segment, e);
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
                    if (InlineCompletionList.SelectedItem is ICompletionData completionData)
                    {
                        AcceptInlineCompletion(completionData, e);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private void MoveInlineCompletionSelection(int direction)
        {
            if (InlineCompletionList.ItemsSource is not ICollection<ShowcaseCompletionData> items || items.Count == 0)
            {
                return;
            }

            int selectedIndex = InlineCompletionList.SelectedIndex >= 0 ? InlineCompletionList.SelectedIndex : 0;
            InlineCompletionList.SelectedIndex = Math.Clamp(selectedIndex + direction, 0, items.Count - 1);
        }

        private void UpdateInlineCompletionPlacement()
        {
            if (!InlineCompletionHost.IsVisible ||
                ScriptEditor.Document is null ||
                InlineCompletionHost.GetVisualParent() is not Control parent)
            {
                return;
            }

            TextView textView = ScriptEditor.TextArea.TextView;
            textView.EnsureVisualLines();

            int caretOffset = Math.Clamp(ScriptEditor.CaretOffset, 0, ScriptEditor.Document.TextLength);
            TextLocation location = ScriptEditor.Document.GetLocation(caretOffset);
            if (TryGetInlineCompletionTop(textView, location, parent, out double top, out double parentHeight))
            {
                double availableHeight = parentHeight - top - InlineCompletionMargin;
                if (availableHeight < InlineCompletionMinimumHeight && TryScrollInlineCompletionLineIntoRoom(location))
                {
                    textView.EnsureVisualLines();
                    TryGetInlineCompletionTop(textView, location, parent, out top, out parentHeight);
                    availableHeight = parentHeight - top - InlineCompletionMargin;
                }

                InlineCompletionHost.Margin = new Thickness(
                    InlineCompletionMargin,
                    Math.Max(InlineCompletionMargin, top),
                    InlineCompletionMargin,
                    InlineCompletionMargin);
                InlineCompletionHost.MaxHeight = Math.Min(
                    InlineCompletionMaxHeight,
                    Math.Max(InlineCompletionMinimumHeight, availableHeight));
            }
        }

        private bool TryGetInlineCompletionTop(
            TextView textView,
            TextLocation location,
            Control parent,
            out double top,
            out double parentHeight)
        {
            TextViewPosition position = new(location);
            Point lineBottom = textView.GetVisualPosition(position, VisualYPosition.LineBottom);
            Point? parentPoint = textView.TranslatePoint(lineBottom, parent);

            parentHeight = parent.Bounds.Height;
            if (parentPoint is null || double.IsNaN(parentHeight) || parentHeight <= 0)
            {
                top = InlineCompletionMargin;
                return false;
            }

            top = parentPoint.Value.Y + InlineCompletionVerticalGap;
            return true;
        }

        private bool TryScrollInlineCompletionLineIntoRoom(TextLocation location)
        {
            if (ScriptEditor.ViewportHeight <= InlineCompletionMinimumHeight + InlineCompletionMargin * 2)
            {
                return false;
            }

            double lineBottomOffset = ScriptEditor.ViewportHeight - InlineCompletionMinimumHeight - InlineCompletionMargin;
            ScriptEditor.ScrollTo(
                location.Line,
                location.Column,
                VisualYPosition.LineBottom,
                lineBottomOffset,
                minimumScrollFraction: 0);
            return true;
        }

        private CompletionRequest CreateCompletionRequest(bool membersOnly)
        {
            TextDocument document = ScriptEditor.Document;
            int caretOffset = Math.Clamp(ScriptEditor.CaretOffset, 0, document.TextLength);
            int wordStart = GetIdentifierStartOffset(document, caretOffset);
            int separatorOffset = -1;

            if (membersOnly && caretOffset > 0 && IsMemberSeparator(document.GetCharAt(caretOffset - 1)))
            {
                separatorOffset = caretOffset - 1;
                wordStart = caretOffset;
            }
            else if (wordStart > 0 && IsMemberSeparator(document.GetCharAt(wordStart - 1)))
            {
                separatorOffset = wordStart - 1;
            }

            if (separatorOffset >= 0)
            {
                string receiver = GetReceiverBeforeSeparator(document, separatorOffset);
                if (string.IsNullOrWhiteSpace(receiver) || !IsIdentifierStart(receiver[0]))
                {
                    return CompletionRequest.Suppressed(caretOffset);
                }

                ShowcaseCompletionScope? scope = ResolveCompletionScope(receiver, document, separatorOffset);
                return new CompletionRequest(wordStart, scope ?? ShowcaseCompletionScope.Reanimation, !scope.HasValue);
            }

            return new CompletionRequest(wordStart, ShowcaseCompletionScope.Global, allMembers: false);
        }

        private static int GetIdentifierStartOffset(TextDocument document, int caretOffset)
        {
            int offset = Math.Clamp(caretOffset, 0, document.TextLength);
            while (offset > 0)
            {
                char value = document.GetCharAt(offset - 1);
                if (!IsIdentifierPart(value))
                {
                    break;
                }

                offset--;
            }

            return offset;
        }

        private static string GetReceiverBeforeSeparator(TextDocument document, int separatorOffset)
        {
            int endOffset = Math.Clamp(separatorOffset, 0, document.TextLength);
            int startOffset = endOffset;
            while (startOffset > 0 && IsIdentifierPart(document.GetCharAt(startOffset - 1)))
            {
                startOffset--;
            }

            return startOffset == endOffset ? string.Empty : document.GetText(startOffset, endOffset - startOffset);
        }

        private static ShowcaseCompletionScope? ResolveCompletionScope(string receiver, TextDocument document, int separatorOffset)
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
                _ => InferReceiverScope(receiver, document.GetText(0, Math.Clamp(separatorOffset, 0, document.TextLength)))
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

        private void InsertScriptText(string rawText, bool replaceDocument)
        {
            if (ScriptEditor.Document is null)
            {
                return;
            }

            EndTypingUndoGroup();
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
            FocusScriptEditor();
            UpdateScriptUndoRedoState();
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

        private enum TypingUndoGroupKind
        {
            Word,
            Whitespace,
            Symbol
        }
    }
}
