using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        private TypingUndoGroupKind? _typingUndoGroupKind;
        private bool _typingUndoGroupHasChanges;

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
            ScriptEditor.TextArea.AddHandler(InputElement.KeyDownEvent, ScriptEditor_KeyDown, RoutingStrategies.Tunnel);
            ScriptEditor.TextArea.AddHandler(InputElement.PointerPressedEvent, ScriptEditor_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            ScriptEditor.KeyDown += ScriptEditor_KeyDown;
            ScriptEditor.AddHandler(InputElement.PointerPressedEvent, ScriptEditor_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            ScriptEditor.LostFocus += ScriptEditor_LostFocus;
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
            _completionWindow?.Close();
            EndTypingUndoGroup();
            if (ScriptEditor.CanUndo)
            {
                ScriptEditor.Undo();
            }

            UpdateScriptUndoRedoState();
        }

        private void OnScriptRedoRequested(object sender, EventArgs e)
        {
            _completionWindow?.Close();
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
            FocusScriptEditor();
        }

        private void FocusScriptEditor()
        {
            ScriptEditor.Focus();
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
                return;
            }

            _completionWindow?.Close();
            _completionWindow = new CompletionWindow(ScriptEditor.TextArea)
            {
                StartOffset = request.StartOffset,
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
            (ShowcaseCompletionScope.Image, @"scene\s*[:\.]\s*(?:get_image|image)\s*\("),
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
