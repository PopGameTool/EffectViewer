namespace EffectViewer.Projects
{
    public sealed class ImageReferenceResolution
    {
        public string RequestedId { get; }
        public string ResolvedId { get; }
        public bool IsResolved => !string.IsNullOrWhiteSpace(ResolvedId);

        public ImageReferenceResolution(string requestedId, string resolvedId)
        {
            RequestedId = requestedId;
            ResolvedId = resolvedId;
        }

        public override string ToString()
        {
            return RequestedId == ResolvedId || string.IsNullOrWhiteSpace(ResolvedId)
                ? RequestedId
                : $"{RequestedId} -> {ResolvedId}";
        }
    }
}
