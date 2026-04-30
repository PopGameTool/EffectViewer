using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Reanim.Attachment
{
    public static class GlobalMembersAttachment
    {
        public static void AttachmentUpdateAndMove(ref AttachmentID theAttachmentID, float theX, float theY)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
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

        public static void AttachmentUpdateAndSetMatrix(ref AttachmentID theAttachmentID, in Matrix4x4 theMatrix)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
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

        public static void AttachmentOverrideColor(AttachmentID theAttachmentID, in SexyColor theColor)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.OverrideColor(theColor);
            }
        }

        public static void AttachmentOverrideScale(AttachmentID theAttachmentID, float theScale)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.OverrideScale(theScale);
            }
        }

        public static void AttachmentCrossFade(AttachmentID theAttachmentID, string theCrossFadeName)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.CrossFade(theCrossFadeName);
            }
        }

        public static void AttachmentDraw(AttachmentID theAttachmentID, EffectViewer.TodLib.Graphics.Graphics g, bool theParentHidden)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.Draw(g, theParentHidden);
            }
        }

        public static void AttachmentDie(ref AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            theAttachmentID = AttachmentID.Null;
            if (aAttachment != null)
            {
                aAttachment.AttachmentDie();
            }
        }

        public static void AttachmentDetach(ref AttachmentID theAttachmentID)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            theAttachmentID = AttachmentID.Null;
            if (aAttachment != null)
            {
                aAttachment.Detach();
            }
        }

        public static ref AttachEffect AttachReanim(ref AttachmentID theAttachmentID, Reanimation theReanimation, float theOffsetX, float theOffsetY)
        {
            uint aReanimId = (uint)EffectSystem.gEffectSystem.mReanimationHolder.mReanimations.DataArrayGetID(theReanimation);
            ref AttachEffect aAttachEffect = ref CreateEffectAttachment(ref theAttachmentID, EffectType.Reanim, aReanimId, theOffsetX, theOffsetY);
            
            Debug.ASSERT(!theReanimation.mIsAttachment);
            theReanimation.mIsAttachment = true;

            return ref aAttachEffect;
        }

        public static ref AttachEffect AttachParticle(ref AttachmentID theAttachmentID, TodParticleSystem theParticleSystem, float theOffsetX, float theOffsetY)
        {
            if (theParticleSystem == null)
            {
                return ref Unsafe.NullRef<AttachEffect>();
            }

            uint aParticleId = (uint)EffectSystem.gEffectSystem.mParticleHolder.mParticleSystems.DataArrayGetID(theParticleSystem);
            ref AttachEffect aAttachEffect = ref CreateEffectAttachment(ref theAttachmentID, EffectType.Particle, aParticleId, theOffsetX, theOffsetY);
            
            Debug.ASSERT(!theParticleSystem.mIsAttachment);
            theParticleSystem.mIsAttachment = true;

            return ref aAttachEffect;
        }

        public static ref AttachEffect AttachTrail(ref AttachmentID theAttachmentID, EffectViewer.TodLib.Trail.Trail theTrail, float theOffsetX, float theOffsetY)
        {
            uint aTrailId = (uint)EffectSystem.gEffectSystem.mTrailHolder.mTrails.DataArrayGetID(theTrail);
            ref AttachEffect aAttachEffect = ref CreateEffectAttachment(ref theAttachmentID, EffectType.Trail, aTrailId, theOffsetX, theOffsetY);
            
            Debug.ASSERT(!theTrail.mIsAttachment);
            theTrail.mIsAttachment = true;

            return ref aAttachEffect;
        }

        public static void AttachmentDetachCrossFadeParticleType(AttachmentID theAttachmentID, string theParticleEffect, string theCrossFadeName)
        {
            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
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
                    TodParticleSystem aParticleSystem = EffectSystem.gEffectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet((ParticleSystemID)aAttachEffect.mEffectID);
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

        public static void AttachmentPropogateColor(AttachmentID theAttachmentID, in SexyColor theColor, bool theEnableAdditiveColor, in SexyColor theAdditiveColor, bool theEnableOverlayColor, in SexyColor theOverlayColor)
        {
            if (theAttachmentID == AttachmentID.Null)
            {
                return;
            }

            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment != null)
            {
                aAttachment.PropogateColor(theColor, theEnableAdditiveColor, theAdditiveColor, theEnableOverlayColor, theOverlayColor);
            }
        }

        public static Reanimation FindReanimAttachment(AttachmentID theAttachmentID)
        {
            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return null;
            }

            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref readonly AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Reanim)
                {
                    Reanimation aReanimation = EffectSystem.gEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null)
                    {
                        return aReanimation;
                    }
                }
            }

            return null;
        }

        public static EffectViewer.TodLib.Trail.Trail FindTrailAttachment(AttachmentID theAttachmentID)
        {
            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return null;
            }

            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Trail)
                {
                    EffectViewer.TodLib.Trail.Trail aTrail = EffectSystem.gEffectSystem.mTrailHolder.mTrails.DataArrayTryToGet((TrailID)aAttachEffect.mEffectID);
                    if (aTrail != null)
                    {
                        return aTrail;
                    }
                }
            }

            return null;
        }

        public static ref AttachEffect FindFirstAttachment(AttachmentID theAttachmentID)
        {
            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null || aAttachment.mNumEffects == 0)
            {
                return ref Unsafe.NullRef<AttachEffect>();
            }

            return ref aAttachment.mEffectArray[0];
        }

        public static void AttachmentReanimTypeDie(AttachmentID theAttachmentID, string theReanimType)
        {
            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null)
            {
                return;
            }

            for (int i = 0; i < aAttachment.mNumEffects; i++)
            {
                ref AttachEffect aAttachEffect = ref aAttachment.mEffectArray[i];
                if (aAttachEffect.mEffectType == EffectType.Reanim)
                {
                    Reanimation aReanimation = EffectSystem.gEffectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)aAttachEffect.mEffectID);
                    if (aReanimation != null && aReanimation.mReanimationType == theReanimType)
                    {
                        aReanimation.ReanimationDie();
                    }
                }
            }
        }

        public static bool IsFullOfAttachments(AttachmentID theAttachmentID)
        {
            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            return aAttachment != null && aAttachment.mNumEffects >= TodLibConstants.MAX_EFFECTS_PER_ATTACHMENT;
        }

        public static ref AttachEffect CreateEffectAttachment(ref AttachmentID theAttachmentID, EffectType theEffectType, uint theDataID, float theOffsetX, float theOffsetY)
        {
            Debug.ASSERT(EffectSystem.gEffectSystem != null);

            Attachment aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(theAttachmentID);
            if (aAttachment == null || aAttachment.mDead)
            {
                aAttachment = EffectSystem.gEffectSystem.mAttachmentHolder.AllocAttachment();
                theAttachmentID = EffectSystem.gEffectSystem.mAttachmentHolder.mAttachments.DataArrayGetID(aAttachment);
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
    }
}
