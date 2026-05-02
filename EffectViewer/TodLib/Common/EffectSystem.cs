namespace EffectViewer.TodLib.Common
{
    public class EffectSystem
    {
        public TodParticleHolder mParticleHolder;
        public TrailHolder mTrailHolder;
        public ReanimationHolder mReanimationHolder;
        public AttachmentHolder mAttachmentHolder;

        public EffectSystem()
        {
            mParticleHolder = null;
            mTrailHolder = null;
            mReanimationHolder = null;
            mAttachmentHolder = null;
        }

        public void Dispose()
        {
        }

        public void EffectSystemInitialize()
        {
            Debug.ASSERT(mParticleHolder == null && mTrailHolder == null && mReanimationHolder == null && mAttachmentHolder == null);
            
            mParticleHolder = new TodParticleHolder(this);
            mTrailHolder = new TrailHolder(this);
            mReanimationHolder = new ReanimationHolder(this);
            mAttachmentHolder = new AttachmentHolder(this);

            mParticleHolder.InitializeHolder();
            mTrailHolder.InitializeHolder();
            mReanimationHolder.InitializeHolder();
            mAttachmentHolder.InitializeHolder();
        }

        public void EffectSystemDispose()
        {
            if (mParticleHolder != null)
            {
                mParticleHolder.DisposeHolder();
                mParticleHolder.Dispose();
                mParticleHolder = null;
            }

            if (mTrailHolder != null)
            {
                mTrailHolder.DisposeHolder();
                mTrailHolder.Dispose();
                mTrailHolder = null;
            }

            if (mReanimationHolder != null)
            {
                mReanimationHolder.DisposeHolder();
                mReanimationHolder.Dispose();
                mReanimationHolder = null;
            }

            if (mAttachmentHolder != null)
            {
                mAttachmentHolder.DisposeHolder();
                mAttachmentHolder.Dispose();
                mAttachmentHolder = null;
            }
        }

        public void EffectSystemFreeAll()
        {
            mParticleHolder.mParticleSystems.DataArrayFreeAll();
            mParticleHolder.mEmitters.DataArrayFreeAll();
            mParticleHolder.mParticles.DataArrayFreeAll();
            mTrailHolder.mTrails.DataArrayFreeAll();
            mReanimationHolder.mReanimations.DataArrayFreeAll();
            mAttachmentHolder.mAttachments.DataArrayFreeAll();
        }

        public void ProcessDeleteQueue()
        {
            TodParticleSystem aParticle = null;
            while (mParticleHolder.mParticleSystems.IterateNext(ref aParticle))
            {
                if (aParticle.mDead)
                {
                    mParticleHolder.mParticleSystems.DataArrayFree(aParticle);
                }
            }

            EffectViewer.TodLib.Trail.Trail aTrail = null;
            while (mTrailHolder.mTrails.IterateNext(ref aTrail))
            {
                if (aTrail.mDead)
                {
                    mTrailHolder.mTrails.DataArrayFree(aTrail);
                }
            }

            Reanimation aReanim = null;
            while (mReanimationHolder.mReanimations.IterateNext(ref aReanim))
            {
                if (aReanim.mDead)
                {
                    mReanimationHolder.mReanimations.DataArrayFree(aReanim);
                }
            }

            Attachment aAttachment = null;
            while (mAttachmentHolder.mAttachments.IterateNext(ref aAttachment))
            {
                if (aAttachment.mDead)
                {
                    mAttachmentHolder.mAttachments.DataArrayFree(aAttachment);
                }
            }
        }

        public void Update()
        {
            TodParticleSystem aParticle = null;
            while (mParticleHolder.mParticleSystems.IterateNext(ref aParticle))
            {
                if (!aParticle.mIsAttachment)
                {
                    aParticle.Update();
                }
            }

            EffectViewer.TodLib.Trail.Trail aTrail = null;
            while (mTrailHolder.mTrails.IterateNext(ref aTrail))
            {
                if (!aTrail.mIsAttachment)
                {
                    aTrail.Update();
                }
            }

            Reanimation aReanim = null;
            while (mReanimationHolder.mReanimations.IterateNext(ref aReanim))
            {
                if (!aReanim.mIsAttachment)
                {
                    aReanim.Update();
                }
            }
        }
    }
}
