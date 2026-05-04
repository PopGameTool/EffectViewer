using System.Collections.Generic;
using System.Linq;

namespace EffectViewer.ViewModels
{
    public enum ShowcaseCompletionScope
    {
        Global,
        Scene,
        Graphics,
        AttachmentApi,
        SceneObject,
        Reanimation,
        ReanimationTrack,
        ReanimationTransform,
        ReanimationFrameRange,
        FrameTime,
        Particle,
        ParticleEmitter,
        ParticleInstance,
        ParticleRenderParams,
        Trail,
        TrailPoint,
        Attachment,
        AttachmentEffect,
        Font,
        Image,
        Matrix,
        Vector,
        Vector3,
        TriVertex
    }

    public sealed class ShowcaseCompletionItem
    {
        public ShowcaseCompletionItem(string displayText, string insertText, string description, bool isMember)
            : this(
                displayText,
                insertText,
                description,
                isMember ? MemberScopes : [ShowcaseCompletionScope.Global])
        {
        }

        public ShowcaseCompletionItem(
            string displayText,
            string insertText,
            string description,
            params ShowcaseCompletionScope[] scopes)
        {
            DisplayText = displayText ?? string.Empty;
            InsertText = insertText ?? string.Empty;
            Description = description ?? string.Empty;
            Scopes = scopes is { Length: > 0 } ? scopes : [ShowcaseCompletionScope.Global];
        }

        public string DisplayText { get; }
        public string InsertText { get; }
        public string Description { get; }
        public IReadOnlyList<ShowcaseCompletionScope> Scopes { get; }
        public bool IsMember => !Scopes.Contains(ShowcaseCompletionScope.Global);

        public bool Matches(ShowcaseCompletionScope scope)
        {
            return Scopes.Contains(scope);
        }

        private static readonly ShowcaseCompletionScope[] MemberScopes =
        [
            ShowcaseCompletionScope.Scene,
            ShowcaseCompletionScope.Graphics,
            ShowcaseCompletionScope.AttachmentApi,
            ShowcaseCompletionScope.SceneObject,
            ShowcaseCompletionScope.Reanimation,
            ShowcaseCompletionScope.ReanimationTrack,
            ShowcaseCompletionScope.ReanimationTransform,
            ShowcaseCompletionScope.ReanimationFrameRange,
            ShowcaseCompletionScope.FrameTime,
            ShowcaseCompletionScope.Particle,
            ShowcaseCompletionScope.ParticleEmitter,
            ShowcaseCompletionScope.ParticleInstance,
            ShowcaseCompletionScope.ParticleRenderParams,
            ShowcaseCompletionScope.Trail,
            ShowcaseCompletionScope.TrailPoint,
            ShowcaseCompletionScope.Attachment,
            ShowcaseCompletionScope.AttachmentEffect,
            ShowcaseCompletionScope.Font,
            ShowcaseCompletionScope.Image,
            ShowcaseCompletionScope.Matrix,
            ShowcaseCompletionScope.Vector,
            ShowcaseCompletionScope.Vector3,
            ShowcaseCompletionScope.TriVertex
        ];
    }
}
