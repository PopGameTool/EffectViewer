namespace EffectViewer.EffectRuntime.Reanim.Attachment
{
    public class AttachmentHolder
    {
        public readonly EffectSystem mEffectSystem;
        public readonly DataArray<Attachment, AttachmentID> mAttachments = new();

        public AttachmentHolder(EffectSystem effectSystem = null)
        {
            mEffectSystem = effectSystem;
        }

        public void Dispose()
        {
            DisposeHolder();
        }

        public void InitializeHolder()
        {
            mAttachments.DataArrayInitialize(1024U, "attachments");
        }

        public void DisposeHolder()
        {
            mAttachments.DataArrayDispose();
        }

        public Attachment AllocAttachment()
        {
            Attachment attachment = mAttachments.DataArrayAlloc();
            attachment.mAttachmentHolder = this;
            return attachment;
        }
    }
}
