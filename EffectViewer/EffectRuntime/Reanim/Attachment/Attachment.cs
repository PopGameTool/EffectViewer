using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.EffectRuntime.Reanim.Attachment
{
    public class Attachment : IDataArrayItem
    {
        public InlineArray16<AttachEffect> mEffectArray; // EffectConstants.MAX_EFFECTS_PER_ATTACHMENT
        public int mNumEffects;
        public bool mDead;
        public AttachmentHolder mAttachmentHolder;

        uint IDataArrayItem.Id { get; set; }
        int IDataArrayItem.Index { get; init; }

        private EffectSystem mEffectSystem => mAttachmentHolder?.mEffectSystem;

        public Attachment()
        {
            Reset();
        }

        public void Reset()
        {
            mNumEffects = 0;
            mDead = false;
            mAttachmentHolder = null;
            foreach (ref var item in mEffectArray)
            {
                item = new();
            }
        }

        public void Dispose()
        {
            AttachmentDie();
        }

        public void Update()
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                bool isNotEmpty = false;
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null && !aParticleSystem.mDead)
                    {
                        aParticleSystem.Update();
                        isNotEmpty = true;
                    }
                    break;
                }
                case EffectType.Trail:
                {
                    EffectViewer.EffectRuntime.Trail.Trail aTrail = mEffectSystem.mTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null && !aTrail.mDead)
                    {
                        aTrail.Update();
                        isNotEmpty = true;
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = mEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null && !aReanimation.mDead)
                    {
                        aReanimation.Update();
                        isNotEmpty = true;
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = mEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.Update();
                        isNotEmpty = true;
                    }
                    break;
                }
                default:
                    Debug.Assert(false);
                    break;
                }

                if (!isNotEmpty)
                {
                    int aNumEffectsRemaining = mNumEffects - i - 1;
                    if (aNumEffectsRemaining > 0)
                    {
                        Span<AttachEffect> aSrcFrames = mEffectArray[(i + 1)..mNumEffects];
                        Span<AttachEffect> aDestFrames = mEffectArray[i..(mNumEffects - 1)];
                        aSrcFrames.CopyTo(aDestFrames);
                        i--;
                    }

                    mNumEffects--;
                }
            }

            if (mNumEffects == 0)
            {
                mDead = true;
            }
        }

        public void SetMatrix(in Matrix4x4 theMatrix)
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                Matrix4x4 aPosition = aAttachEffect.mOffset * theMatrix; // 琛屽悜閲忕煩闃?
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        aParticleSystem.SystemMove(aPosition.M41, aPosition.M42);
                    }
                    break;
                }
                case EffectType.Trail:
                {
                    EffectViewer.EffectRuntime.Trail.Trail aTrail = mEffectSystem.mTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null)
                    {
                        aTrail.mTrailCenter = new Vector2(aPosition.M41, aPosition.M42);
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = mEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        aReanimation.mOverlayMatrix = aPosition;
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = mEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.SetMatrix(aPosition);
                    }
                    break;
                }
                }
            }
        }

        public void OverrideColor(in EffectColor theColor)
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        aParticleSystem.OverrideColor("", theColor);
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = mEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        aReanimation.mColorOverride = theColor;
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = mEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.OverrideColor(theColor);
                    }
                    break;
                }
                }
            }
        }

        public void OverrideScale(float theScale)
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        aParticleSystem.OverrideScale(null, theScale);
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = mEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        aReanimation.OverrideScale(theScale, theScale);
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = mEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.OverrideScale(theScale);
                    }
                    break;
                }
                }
            }
        }

        public void Draw(EffectViewer.EffectRuntime.Graphics.Graphics g, bool theParentHidden)
        {
            Debug.Assert(!mDead);
            Debug.Assert(mEffectSystem != null);

            ParticleHolder aParticleHolder = mEffectSystem.mParticleHolder;
            TrailHolder aTrailHolder = mEffectSystem.mTrailHolder;
            ReanimationHolder aReanimationHolder = mEffectSystem.mReanimationHolder;
            AttachmentHolder aAttachmentHolder = mEffectSystem.mAttachmentHolder;
            
            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                if (!theParentHidden || !aAttachEffect.mDontDrawIfParentHidden)
                {
                    switch (aAttachEffect.mEffectType)
                    {
                    case EffectType.Particle:
                    {
                        ParticleSystem aParticleSystem = aParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                        if (aParticleSystem != null)
                        {
                            aParticleSystem.Draw(g);
                        }
                        break;
                    }
                    case EffectType.Trail:
                    {
                        EffectViewer.EffectRuntime.Trail.Trail aTrail = aTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                        if (aTrail != null)
                        {
                            aTrail.Draw(g);
                        }
                        break;
                    }
                    case EffectType.Reanim:
                    {
                        Reanimation aReanimation = aReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                        if (aReanimation != null)
                        {
                            aReanimation.Draw(g);
                        }
                        break;
                    }
                    case EffectType.Attachment:
                    {
                        Attachment aAttachment = aAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                        if (aAttachment != null)
                        {
                            aAttachment.Draw(g, theParentHidden);
                        }
                        break;
                    }
                    }
                }
            }
        }

        public void AttachmentDie()
        {
            Debug.Assert(mEffectSystem != null);

            ParticleHolder aParticleHolder = mEffectSystem.mParticleHolder;
            TrailHolder aTrailHolder = mEffectSystem.mTrailHolder;
            ReanimationHolder aReanimationHolder = mEffectSystem.mReanimationHolder;
            AttachmentHolder aAttachmentHolder = mEffectSystem.mAttachmentHolder;

            for (int i = 0; i < mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref mEffectArray[i];
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = aParticleHolder?.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        aParticleSystem.ParticleSystemDie();
                    }
                    break;
                }
                case EffectType.Trail:
                {
                    EffectViewer.EffectRuntime.Trail.Trail aTrail = aTrailHolder?.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null)
                    {
                        aTrail.mDead = true;
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = aReanimationHolder?.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        aReanimation.ReanimationDie();
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = aAttachmentHolder?.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.AttachmentDie();
                    }
                    break;
                }
                }

                aAttachEffect.mEffectID = 0U;
            }

            mNumEffects = 0;
            mDead = true;
        }

        public void Detach()
        {
            Debug.Assert(mEffectSystem != null);
            ParticleHolder aParticleHolder = mEffectSystem.mParticleHolder;
            TrailHolder aTrailHolder = mEffectSystem.mTrailHolder;
            ReanimationHolder aReanimationHolder = mEffectSystem.mReanimationHolder;
            AttachmentHolder aAttachmentHolder = mEffectSystem.mAttachmentHolder;

            for (int i = 0; i < mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref mEffectArray[i];
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = aParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        Debug.Assert(aParticleSystem.mIsAttachment);
                        aParticleSystem.mIsAttachment = false;
                    }
                    break;
                }
                case EffectType.Trail:
                {
                    EffectViewer.EffectRuntime.Trail.Trail aTrail = aTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null)
                    {
                        Debug.Assert(aTrail.mIsAttachment);
                        aTrail.mIsAttachment = false;
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = aReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        Debug.Assert(aReanimation.mIsAttachment);
                        aReanimation.mIsAttachment = false;
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = aAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.Detach();
                    }
                    break;
                }
                }

                aAttachEffect.mEffectID = 0U;
            }

            mNumEffects = 0;
            mDead = true;
        }

        public void CrossFade(string theCrossFadeName)
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                EffectType attachEffectType = aAttachEffect.mEffectType;
                if (attachEffectType == EffectType.Particle)
                {
                    ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        aParticleSystem.CrossFade(theCrossFadeName);
                    }
                }
            }
        }

        public void PropogateColor(in EffectColor theColor, bool theEnableAdditiveColor, in EffectColor theAdditiveColor, bool theEnableOverlayColor, in EffectColor theOverlayColor)
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                if (!aAttachEffect.mDontPropogateColor)
                {
                    switch (aAttachEffect.mEffectType)
                    {
                    case EffectType.Particle:
                    {
                        ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                        if (aParticleSystem != null)
                        {
                            aParticleSystem.OverrideColor(null, theColor);
                            aParticleSystem.OverrideExtraAdditiveDraw(null, theEnableAdditiveColor);
                        }
                        break;
                    }
                    case EffectType.Reanim:
                    {
                        Reanimation aReanimation = mEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                        if (aReanimation != null)
                        {
                            aReanimation.mColorOverride = theColor;
                            aReanimation.mExtraAdditiveColor = theAdditiveColor;
                            aReanimation.mEnableExtraAdditiveDraw = theEnableAdditiveColor;
                            aReanimation.mExtraOverlayColor = theOverlayColor;
                            aReanimation.mEnableExtraOverlayDraw = theEnableOverlayColor;
                            aReanimation.PropogateColorToAttachments();
                        }
                        break;
                    }
                    case EffectType.Attachment:
                    {
                        Attachment aAttachment = mEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                        if (aAttachment != null)
                        {
                            aAttachment.PropogateColor(theColor, theEnableAdditiveColor, theAdditiveColor, theEnableOverlayColor, theOverlayColor);
                        }
                        break;
                    }
                    }
                }
            }
        }

        public void SetPosition(in Vector2 thePosition)
        {
            Debug.Assert(mEffectSystem != null);

            for (int i = 0; i < mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref mEffectArray[i];
                Vector2 aNewPos = Vector2.Transform(thePosition, aAttachEffect.mOffset);
                
                switch (aAttachEffect.mEffectType)
                {
                case EffectType.Particle:
                {
                    ParticleSystem aParticleSystem = mEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null)
                    {
                        aParticleSystem.SystemMove(aNewPos.X, aNewPos.Y);
                    }
                    break;
                }
                case EffectType.Trail:
                {
                    EffectViewer.EffectRuntime.Trail.Trail aTrail = mEffectSystem.mTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null)
                    {
                        aTrail.AddPoint(aNewPos.X, aNewPos.Y);
                    }
                    break;
                }
                case EffectType.Reanim:
                {
                    Reanimation aReanimation = mEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        aReanimation.SetPosition(aNewPos.X, aNewPos.Y);
                    }
                    break;
                }
                case EffectType.Attachment:
                {
                    Attachment aAttachment = mEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)aAttachEffect.mEffectID);
                    if (aAttachment != null)
                    {
                        aAttachment.SetPosition(aNewPos);
                    }
                    break;
                }
                }
            }
        }
    }
}

