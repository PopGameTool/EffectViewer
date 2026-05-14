using System;
using System.Runtime.CompilerServices;
using EffectViewer.Runtime.Showcase;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.EffectRuntime.Graphics;
using EffectViewer.EffectRuntime.Particle;
using EffectViewer.EffectRuntime.Reanim;
using EffectViewer.EffectRuntime.Reanim.Attachment;
using EffectViewer.EffectRuntime.Trail;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaAttachmentApi
    {
        private readonly ShowcaseScene _scene;

        internal LuaAttachmentApi(ShowcaseScene scene)
        {
            _scene = scene;
        }

        public double attachment_update_and_move(double attachmentId, double x, double y)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            GlobalMembersAttachment.AttachmentUpdateAndMove(_scene.EffectSystem, ref id, (float)x, (float)y);
            return IdToNumber(id);
        }

        public double attachment_update_and_set_matrix(double attachmentId, ShowcaseMatrix matrix)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            GlobalMembersAttachment.AttachmentUpdateAndSetMatrix(_scene.EffectSystem, ref id, matrix?.ToMatrix4x4() ?? System.Numerics.Matrix4x4.Identity);
            return IdToNumber(id);
        }

        public void attachment_override_color(double attachmentId, double red, double green, double blue, DynValue alpha)
        {
            GlobalMembersAttachment.AttachmentOverrideColor(
                _scene.EffectSystem,
                IdFromNumber<AttachmentID>(attachmentId),
                new EffectColor(
                    LuaApiUtility.ClampColor(red),
                    LuaApiUtility.ClampColor(green),
                    LuaApiUtility.ClampColor(blue),
                    LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(alpha, 255))));
        }

        public void attachment_override_scale(double attachmentId, double scale)
        {
            GlobalMembersAttachment.AttachmentOverrideScale(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId), (float)scale);
        }

        public void attachment_cross_fade(double attachmentId, string crossFadeName)
        {
            GlobalMembersAttachment.AttachmentCrossFade(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId), crossFadeName);
        }

        public void attachment_draw(double attachmentId, LuaGraphicsApi graphics, bool parentHidden)
        {
            if (graphics is not null)
            {
                GlobalMembersAttachment.AttachmentDraw(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId), graphics.Graphics, parentHidden);
            }
        }

        public double attachment_die(double attachmentId)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            GlobalMembersAttachment.AttachmentDie(_scene.EffectSystem, ref id);
            return IdToNumber(id);
        }

        public double attachment_detach(double attachmentId)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            GlobalMembersAttachment.AttachmentDetach(_scene.EffectSystem, ref id);
            return IdToNumber(id);
        }

        public DynValue attach_reanim(double attachmentId, ShowcaseReanimation reanim, double offsetX, double offsetY)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            ref AttachEffect effect = ref GlobalMembersAttachment.AttachReanim(_scene.EffectSystem, ref id, reanim?.Reanimation, (float)offsetX, (float)offsetY);
            return AttachmentTuple(id, ref effect);
        }

        public DynValue attach_particle(double attachmentId, ShowcaseParticle particle, double offsetX, double offsetY)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            ref AttachEffect effect = ref GlobalMembersAttachment.AttachParticle(_scene.EffectSystem, ref id, particle?.ParticleSystem, (float)offsetX, (float)offsetY);
            return AttachmentTuple(id, ref effect);
        }

        public DynValue attach_trail(double attachmentId, ShowcaseTrail trail, double offsetX, double offsetY)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            ref AttachEffect effect = ref GlobalMembersAttachment.AttachTrail(_scene.EffectSystem, ref id, trail?.Trail, (float)offsetX, (float)offsetY);
            return AttachmentTuple(id, ref effect);
        }

        public void attachment_detach_cross_fade_particle_type(double attachmentId, string particleEffect, string crossFadeName)
        {
            GlobalMembersAttachment.AttachmentDetachCrossFadeParticleType(
                _scene.EffectSystem,
                IdFromNumber<AttachmentID>(attachmentId),
                particleEffect,
                crossFadeName);
        }

        public void attachment_propogate_color(
            double attachmentId,
            double red,
            double green,
            double blue,
            double alpha,
            bool enableAdditiveColor,
            double additiveRed,
            double additiveGreen,
            double additiveBlue,
            double additiveAlpha,
            bool enableOverlayColor,
            double overlayRed,
            double overlayGreen,
            double overlayBlue,
            double overlayAlpha)
        {
            GlobalMembersAttachment.AttachmentPropogateColor(
                _scene.EffectSystem,
                IdFromNumber<AttachmentID>(attachmentId),
                new EffectColor(LuaApiUtility.ClampColor(red), LuaApiUtility.ClampColor(green), LuaApiUtility.ClampColor(blue), LuaApiUtility.ClampColor(alpha)),
                enableAdditiveColor,
                new EffectColor(LuaApiUtility.ClampColor(additiveRed), LuaApiUtility.ClampColor(additiveGreen), LuaApiUtility.ClampColor(additiveBlue), LuaApiUtility.ClampColor(additiveAlpha)),
                enableOverlayColor,
                new EffectColor(LuaApiUtility.ClampColor(overlayRed), LuaApiUtility.ClampColor(overlayGreen), LuaApiUtility.ClampColor(overlayBlue), LuaApiUtility.ClampColor(overlayAlpha)));
        }

        public ShowcaseReanimation find_reanim_attachment(double attachmentId)
        {
            Reanimation reanimation = GlobalMembersAttachment.FindReanimAttachment(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId));
            return reanimation is null ? null : new ShowcaseReanimation(_scene, reanimation.mReanimationType, reanimation);
        }

        public ShowcaseTrail find_trail_attachment(double attachmentId)
        {
            Trail trail = GlobalMembersAttachment.FindTrailAttachment(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId));
            return trail is null ? null : new ShowcaseTrail(_scene, trail.mDefinition?.mImage, trail, trail.mTrailCenter.X, trail.mTrailCenter.Y);
        }

        public DynValue find_first_attachment(double attachmentId)
        {
            Attachment attachment = GetAttachment(IdFromNumber<AttachmentID>(attachmentId));
            if (attachment is null || attachment.mNumEffects == 0)
            {
                return DynValue.NewTuple(DynValue.Nil, DynValue.Nil);
            }

            return DynValue.NewTuple(UserData.Create(new ShowcaseAttachment(attachment)), DynValue.NewNumber(0));
        }

        public void attachment_reanim_type_die(double attachmentId, string reanimType)
        {
            GlobalMembersAttachment.AttachmentReanimTypeDie(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId), reanimType);
        }

        public bool is_full_of_attachments(double attachmentId)
        {
            return GlobalMembersAttachment.IsFullOfAttachments(_scene.EffectSystem, IdFromNumber<AttachmentID>(attachmentId));
        }

        public DynValue create_effect_attachment(double attachmentId, string effectType, double dataId, double offsetX, double offsetY)
        {
            AttachmentID id = IdFromNumber<AttachmentID>(attachmentId);
            EffectType parsed = Enum.TryParse(effectType, ignoreCase: true, out EffectType value) ? value : EffectType.Other;
            ref AttachEffect effect = ref GlobalMembersAttachment.CreateEffectAttachment(
                _scene.EffectSystem,
                ref id,
                parsed,
                dataId > 0 ? unchecked((uint)Math.Round(dataId)) : 0U,
                (float)offsetX,
                (float)offsetY);
            return AttachmentTuple(id, ref effect);
        }

        private DynValue AttachmentTuple(AttachmentID id, ref AttachEffect effect)
        {
            if (Unsafe.IsNullRef(ref effect))
            {
                return DynValue.NewTuple(DynValue.NewNumber(IdToNumber(id)), DynValue.Nil, DynValue.Nil);
            }

            Attachment attachment = GetAttachment(id);
            int index = attachment is null ? -1 : Math.Max(0, attachment.mNumEffects - 1);
            return DynValue.NewTuple(
                DynValue.NewNumber(IdToNumber(id)),
                attachment is null ? DynValue.Nil : UserData.Create(new ShowcaseAttachment(attachment)),
                index < 0 ? DynValue.Nil : DynValue.NewNumber(index));
        }

        private Attachment GetAttachment(AttachmentID id)
        {
            return _scene.EffectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(id);
        }

        private static double IdToNumber<TId>(TId id)
            where TId : unmanaged
        {
            uint raw = Unsafe.As<TId, uint>(ref id);
            return raw;
        }

        private static TId IdFromNumber<TId>(double id)
            where TId : unmanaged
        {
            uint raw = double.IsFinite(id) && id > 0 ? unchecked((uint)Math.Round(id)) : 0U;
            return Unsafe.As<uint, TId>(ref raw);
        }
    }
}
