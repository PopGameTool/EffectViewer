using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Reanim.Attachment
{
    public static class GlobalMembersAttachment
    {
        public static void AttachmentUpdateAndMove(EffectSystem effectSystem, ref AttachmentID theAttachmentID, float theX, float theY)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.Update();
                aAttachment.SetPosition(new Vector2(theX, theY));
            }
            else
            {
                theAttachmentID = AttachmentID.Null;
            }
        }

        public static void AttachmentUpdateAndSetMatrix(EffectSystem effectSystem, ref AttachmentID theAttachmentID, in Matrix4x4 theMatrix)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.Update();
                aAttachment.SetMatrix(theMatrix);
            }
            else
            {
                theAttachmentID = AttachmentID.Null;
            }
        }

        public static void AttachmentOverrideColor(EffectSystem effectSystem, AttachmentID theAttachmentID, in SexyColor theColor)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.OverrideColor(theColor);
            }
        }

        public static void AttachmentOverrideScale(EffectSystem effectSystem, AttachmentID theAttachmentID, float theScale)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.OverrideScale(theScale);
            }
        }

        public static void AttachmentCrossFade(EffectSystem effectSystem, AttachmentID theAttachmentID, string theCrossFadeName)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.CrossFade(theCrossFadeName);
            }
        }

        public static void AttachmentDraw(EffectSystem effectSystem, AttachmentID theAttachmentID, EffectViewer.TodLib.Graphics.Graphics g, bool theParentHidden)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.Draw(g, theParentHidden);
            }
        }

        public static void AttachmentDie(EffectSystem effectSystem, ref AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            theAttachmentID = AttachmentID.Null;
            if (aAttachment != null)
            {
                aAttachment.AttachmentDie();
            }
        }

        public static void AttachmentDetach(EffectSystem effectSystem, ref AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            theAttachmentID = AttachmentID.Null;
            if (aAttachment != null)
            {
                aAttachment.Detach();
            }
        }

        public static ref AttachEffect AttachReanim(EffectSystem effectSystem, ref AttachmentID theAttachmentID, Reanimation theReanimation, float theOffsetX, float theOffsetY)
        {
            effectSystem = RequireEffectSystem(effectSystem);
            ValidateReanimationSystem(effectSystem, theReanimation);
            uint aReanimId = (uint)effectSystem.mReanimationHolder.mReanimations.DataArrayGetID(theReanimation);
            ref AttachEffect aAttachEffect = ref CreateEffectAttachment(effectSystem, ref theAttachmentID, EffectType.Reanim, aReanimId, theOffsetX, theOffsetY);
            
            Debug.ASSERT(!theReanimation.mIsAttachment);
            theReanimation.mIsAttachment = true;

            return ref aAttachEffect;
        }

        public static ref AttachEffect AttachParticle(EffectSystem effectSystem, ref AttachmentID theAttachmentID, TodParticleSystem theParticleSystem, float theOffsetX, float theOffsetY)
        {
            if (theParticleSystem == null)
            {
                return ref Unsafe.NullRef<AttachEffect>();
            }

            effectSystem = RequireEffectSystem(effectSystem);
            ValidateParticleSystem(effectSystem, theParticleSystem);
            uint aParticleId = (uint)effectSystem.mParticleHolder.mParticleSystems.DataArrayGetID(theParticleSystem);
            ref AttachEffect aAttachEffect = ref CreateEffectAttachment(effectSystem, ref theAttachmentID, EffectType.Particle, aParticleId, theOffsetX, theOffsetY);
            
            Debug.ASSERT(!theParticleSystem.mIsAttachment);
            theParticleSystem.mIsAttachment = true;

            return ref aAttachEffect;
        }

        public static ref AttachEffect AttachTrail(EffectSystem effectSystem, ref AttachmentID theAttachmentID, EffectViewer.TodLib.Trail.Trail theTrail, float theOffsetX, float theOffsetY)
        {
            effectSystem = RequireEffectSystem(effectSystem);
            ValidateTrailSystem(effectSystem, theTrail);
            uint aTrailId = (uint)effectSystem.mTrailHolder.mTrails.DataArrayGetID(theTrail);
            ref AttachEffect aAttachEffect = ref CreateEffectAttachment(effectSystem, ref theAttachmentID, EffectType.Trail, aTrailId, theOffsetX, theOffsetY);
            
            Debug.ASSERT(!theTrail.mIsAttachment);
            theTrail.mIsAttachment = true;

            return ref aAttachEffect;
        }

        public static void AttachmentDetachCrossFadeParticleType(EffectSystem effectSystem, AttachmentID theAttachmentID, string theParticleEffect, string theCrossFadeName)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);
            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return;
            }

            Debug.ASSERT(TodParticleGlobal.gParticleDefArray.ContainsKey(theParticleEffect));
            TodParticleDefinition aDefinition = TodParticleGlobal.gParticleDefArray[theParticleEffect];
            
            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Particle)
                {
                    TodParticleSystem aParticleSystem = effectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
                    if (aParticleSystem != null && aParticleSystem.mParticleDef == aDefinition)
                    {
                        if (theCrossFadeName != null)
                        {
                            aParticleSystem.mIsAttachment = false;
                            aParticleSystem.CrossFade(theCrossFadeName);
                        }
                        else
                        {
                            aParticleSystem.ParticleSystemDie();
                        }
                        aAttachEffect.mEffectID = (uint)AttachmentID.Null;
                    }
                }
            }
        }

        public static void AttachmentPropogateColor(EffectSystem effectSystem, AttachmentID theAttachmentID, in SexyColor theColor, bool theEnableAdditiveColor, in SexyColor theAdditiveColor, bool theEnableOverlayColor, in SexyColor theOverlayColor)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.PropogateColor(theColor, theEnableAdditiveColor, theAdditiveColor, theEnableOverlayColor, theOverlayColor);
            }
        }

        public static Reanimation FindReanimAttachment(EffectSystem effectSystem, AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return null;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return null;
            }

            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Reanim)
                {
                    Reanimation aReanimation = effectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        return aReanimation;
                    }
                }
            }

            return null;
        }

        public static EffectViewer.TodLib.Trail.Trail FindTrailAttachment(EffectSystem effectSystem, AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return null;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return null;
            }

            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Trail)
                {
                    EffectViewer.TodLib.Trail.Trail aTrail = effectSystem.mTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null)
                    {
                        return aTrail;
                    }
                }
            }

            return null;
        }

        public static ref AttachEffect FindFirstAttachment(EffectSystem effectSystem, AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return ref Unsafe.NullRef<AttachEffect>();
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null || aAttachment.mNumEffects == 0)
            {
                return ref Unsafe.NullRef<AttachEffect>();
            }

            return ref aAttachment.mEffectArray[0];
        }

        public static void AttachmentReanimTypeDie(EffectSystem effectSystem, AttachmentID theAttachmentID, string theReanimType)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            effectSystem = RequireEffectSystem(effectSystem);
            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return;
            }

            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Reanim)
                {
                    Reanimation aReanimation = effectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null && aReanimation.mReanimationType == theReanimType)
                    {
                        aReanimation.ReanimationDie();
                    }
                }
            }
        }

        public static bool IsFullOfAttachments(EffectSystem effectSystem, AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return false;
            }

            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            return aAttachment != null && aAttachment.mNumEffects >= TodLibConstants.MAX_EFFECTS_PER_ATTACHMENT;
        }

        public static ref AttachEffect CreateEffectAttachment(EffectSystem effectSystem, ref AttachmentID theAttachmentID, EffectType theEffectType, uint theDataID, float theOffsetX, float theOffsetY)
        {
            effectSystem = RequireEffectSystem(effectSystem);

            Attachment aAttachment = effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null || aAttachment.mDead)
            {
                aAttachment = effectSystem.mAttachmentHolder.AllocAttachment();
                theAttachmentID = effectSystem.mAttachmentHolder.mAttachments.DataArrayGetID(aAttachment);
            }

            Debug.ASSERT(aAttachment.mNumEffects < TodLibConstants.MAX_EFFECTS_PER_ATTACHMENT);
            Debug.ASSERT(!aAttachment.mDead);

            ref AttachEffect aAttachEffect = ref aAttachment.mEffectArray[aAttachment.mNumEffects];
            aAttachEffect.mEffectType = theEffectType;
            aAttachEffect.mEffectID = theDataID;
            aAttachEffect.mDontDrawIfParentHidden = false;
            aAttachEffect.mOffset = Matrix4x4.Identity;
            aAttachEffect.mOffset.M41 = theOffsetX;
            aAttachEffect.mOffset.M42 = theOffsetY;
            aAttachment.mNumEffects++;
            return ref aAttachEffect;
        }

        private static EffectSystem RequireEffectSystem(EffectSystem effectSystem)
        {
            if (effectSystem == null)
            {
                throw new InvalidOperationException("Attachment operations require an EffectSystem instance.");
            }

            if (effectSystem.mAttachmentHolder == null)
            {
                throw new ObjectDisposedException(nameof(EffectSystem));
            }

            return effectSystem;
        }

        private static void ValidateReanimationSystem(EffectSystem effectSystem, Reanimation reanimation)
        {
            if (reanimation == null)
            {
                throw new ArgumentNullException(nameof(reanimation));
            }

            if (!ReferenceEquals(reanimation.mReanimationHolder?.mEffectSystem, effectSystem))
            {
                throw new InvalidOperationException("The reanimation belongs to a different EffectSystem.");
            }

            if (!effectSystem.mReanimationHolder.mReanimations.DataArrayContains(reanimation))
            {
                throw new InvalidOperationException("The reanimation is no longer active in its EffectSystem.");
            }
        }

        private static void ValidateParticleSystem(EffectSystem effectSystem, TodParticleSystem particleSystem)
        {
            if (!ReferenceEquals(particleSystem.mParticleHolder?.mEffectSystem, effectSystem))
            {
                throw new InvalidOperationException("The particle system belongs to a different EffectSystem.");
            }

            if (!effectSystem.mParticleHolder.mParticleSystems.DataArrayContains(particleSystem))
            {
                throw new InvalidOperationException("The particle system is no longer active in its EffectSystem.");
            }
        }

        private static void ValidateTrailSystem(EffectSystem effectSystem, EffectViewer.TodLib.Trail.Trail trail)
        {
            if (trail == null)
            {
                throw new ArgumentNullException(nameof(trail));
            }

            if (!ReferenceEquals(trail.mTrailHolder?.mEffectSystem, effectSystem))
            {
                throw new InvalidOperationException("The trail belongs to a different EffectSystem.");
            }

            if (!effectSystem.mTrailHolder.mTrails.DataArrayContains(trail))
            {
                throw new InvalidOperationException("The trail is no longer active in its EffectSystem.");
            }
        }
    }
}

