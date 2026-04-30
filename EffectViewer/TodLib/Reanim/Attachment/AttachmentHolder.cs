namespace EffectViewer.TodLib.Reanim.Attachment
{
    public class AttachmentHolder
    {
        public readonly DataArray<Attachment, AttachmentID> mAttachments = new();

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
            return mAttachments.DataArrayAlloc();
        }
    }
}