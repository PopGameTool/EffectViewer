using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.EffectRuntime.Graphics;
using EffectViewer.EffectRuntime.Particle;
using EffectViewer.EffectRuntime.Reanim;
using EffectViewer.EffectRuntime.Trail;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseScene : IRenderFrameProvider, IDisposable
    {
        private const double UpdateStepSeconds = 1.0 / EffectConstants.TICKS_PER_SECOND;

        private readonly EffectProject _project;
        private readonly EffectSystem _effectSystem = new();
        private readonly List<ShowcaseReanimation> _reanims = [];
        private readonly List<ShowcaseParticle> _particles = [];
        private readonly List<ShowcaseTrail> _trails = [];
        private readonly ProjectResourceProvider _resourceProvider;
        private IShowcaseScriptCallbacks _scriptCallbacks;
        private double _accumulator;
        private bool _disposed;

        public ShowcaseScene(EffectProject project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _resourceProvider = new ProjectResourceProvider(project);
            ResourceHandler.SetProvider(_resourceProvider);
            project.Definitions.ApplyReanimationGlobals();
            _effectSystem.EffectSystemInitialize();
        }

        public IReadOnlyList<ShowcaseReanimation> Reanimations => _reanims;
        public IReadOnlyList<ShowcaseParticle> Particles => _particles;
        public IReadOnlyList<ShowcaseTrail> Trails => _trails;
        public int MaxUpdateStepsPerFrame { get; set; } = 20;
        internal EffectSystem EffectSystem => _effectSystem;

        internal void SetScriptCallbacks(IShowcaseScriptCallbacks callbacks)
        {
            EnsureAlive();
            _scriptCallbacks?.Dispose();
            _scriptCallbacks = callbacks;
        }

        public ShowcaseReanimation AddReanimation(string id, double x, double y)
        {
            EnsureAlive();
            ResourceHandler.SetProvider(_resourceProvider);
            if (!_project.Assets.Reanims.TryGetValue(id, out ReanimAsset asset))
            {
                throw new InvalidOperationException($"Reanim '{id}' was not found in the current project.");
            }

            Reanimation reanimation = _effectSystem.mReanimationHolder.mReanimations.DataArrayAlloc();
            reanimation.mReanimationHolder = _effectSystem.mReanimationHolder;
            reanimation.mRenderOrder = _reanims.Count + _particles.Count + _trails.Count;
            InitializeReanimation(reanimation, id, asset, (float)x, (float)y);

            ShowcaseReanimation instance = new(this, id, reanimation);
            _reanims.Add(instance);
            return instance;
        }

        public ShowcaseParticle AddParticle(string id, double x, double y)
        {
            EnsureAlive();
            ResourceHandler.SetProvider(_resourceProvider);
            if (!_project.Assets.Particles.TryGetValue(id, out EffectAsset asset))
            {
                throw new InvalidOperationException($"Particle '{id}' was not found in the current project.");
            }

            ParticleDefinition definition = LoadParticleDefinition(asset);
            ParticleSystem system = _effectSystem.mParticleHolder.AllocParticleSystemFromDef(
                (float)x,
                (float)y,
                _reanims.Count + _particles.Count + _trails.Count,
                definition,
                id);

            ShowcaseParticle instance = new(this, id, system);
            _particles.Add(instance);
            return instance;
        }

        public ShowcaseTrail AddTrail(string id, double x, double y)
        {
            EnsureAlive();
            ResourceHandler.SetProvider(_resourceProvider);
            if (!_project.Assets.Trails.TryGetValue(id, out EffectAsset asset))
            {
                throw new InvalidOperationException($"Trail '{id}' was not found in the current project.");
            }

            TrailDefinition definition = LoadTrailDefinition(asset);
            Trail trail = _effectSystem.mTrailHolder.AllocTrailFromDef(
                _reanims.Count + _particles.Count + _trails.Count,
                definition);
            trail.mTrailCenter = Vector2.Zero;

            ShowcaseTrail instance = new(this, id, trail, (float)x, (float)y);
            _trails.Add(instance);
            return instance;
        }

        public void Clear()
        {
            if (_disposed)
            {
                return;
            }

            _reanims.Clear();
            _particles.Clear();
            _trails.Clear();
            _effectSystem.EffectSystemFreeAll();
            _accumulator = 0;
        }

        public RenderFrame GetFrame(double deltaSeconds)
        {
            RenderFrame frame = new();
            if (_disposed)
            {
                return frame;
            }

            ResourceHandler.SetProvider(_resourceProvider);
            _accumulator += deltaSeconds;
            int guard = 0;
            int maxUpdateSteps = Math.Max(1, MaxUpdateStepsPerFrame);
            while (_accumulator >= UpdateStepSeconds && guard++ < maxUpdateSteps)
            {
                Update(UpdateStepSeconds);
                _accumulator -= UpdateStepSeconds;
            }

            FrameCaptureGraphics graphics = new()
            {
                mClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3),
                mColor = EffectColor.White,
                mDrawMode = DrawMode.Normal
            };

            if (_scriptCallbacks?.HasDraw == true)
            {
                _scriptCallbacks.TryDraw(graphics);
            }
            else
            {
                Draw(graphics);
            }

            return graphics.Frame;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scriptCallbacks?.Dispose();
            _scriptCallbacks = null;
            _reanims.Clear();
            _particles.Clear();
            _trails.Clear();
            _effectSystem.EffectSystemDispose();
        }

        internal void AttachReanimation(Reanimation parent, string trackName, ShowcaseReanimation child, float offsetX, float offsetY)
        {
            EnsureAttachTarget(parent, trackName);
            if (child?.Reanimation is null)
            {
                throw new InvalidOperationException("The child reanimation is not valid.");
            }

            EnsureSameEffectSystem(child.Reanimation);
            int trackIndex = parent.FindTrackIndex(trackName);
            parent.GetTrackBasePoseMatrix(trackIndex, out Matrix4x4 basePoseMatrix);
            Vector2 position = Vector2.Transform(new Vector2(offsetX, offsetY), basePoseMatrix);
            ref ReanimatorTrackInstance track = ref parent.GetTrackInstanceByName(trackName);
            GlobalMembersAttachment.AttachReanim(_effectSystem, ref track.mAttachmentID, child.Reanimation, position.X, position.Y);
        }

        internal void AttachParticle(Reanimation parent, string trackName, ShowcaseParticle child, float offsetX, float offsetY)
        {
            EnsureAttachTarget(parent, trackName);
            if (child?.ParticleSystem is null)
            {
                throw new InvalidOperationException("The child particle is not valid.");
            }

            EnsureSameEffectSystem(child.ParticleSystem);
            parent.AttachParticleToTrack(trackName, child.ParticleSystem, offsetX, offsetY);
        }

        internal void AttachTrail(Reanimation parent, string trackName, ShowcaseTrail child, float offsetX, float offsetY)
        {
            EnsureAttachTarget(parent, trackName);
            if (child?.Trail is null)
            {
                throw new InvalidOperationException("The child trail is not valid.");
            }

            EnsureSameEffectSystem(child.Trail);
            child.PrepareForAttachment();
            int trackIndex = parent.FindTrackIndex(trackName);
            parent.GetTrackBasePoseMatrix(trackIndex, out Matrix4x4 basePoseMatrix);
            Vector2 position = Vector2.Transform(new Vector2(offsetX, offsetY), basePoseMatrix);
            ref ReanimatorTrackInstance track = ref parent.GetTrackInstanceByName(trackName);
            GlobalMembersAttachment.AttachTrail(_effectSystem, ref track.mAttachmentID, child.Trail, position.X, position.Y);
        }

        internal void DetachTrack(Reanimation parent, string trackName)
        {
            EnsureAttachTarget(parent, trackName);
            ref ReanimatorTrackInstance track = ref parent.GetTrackInstanceByName(trackName);
            GlobalMembersAttachment.AttachmentDetach(_effectSystem, ref track.mAttachmentID);
        }

        internal double GetReanimationId(ShowcaseReanimation reanimation)
        {
            return reanimation?.Reanimation is not null && _effectSystem.mReanimationHolder.mReanimations.DataArrayContains(reanimation.Reanimation)
                ? IdToNumber(_effectSystem.mReanimationHolder.mReanimations.DataArrayGetID(reanimation.Reanimation))
                : 0d;
        }

        internal double GetParticleSystemId(ShowcaseParticle particle)
        {
            return particle?.ParticleSystem is not null && _effectSystem.mParticleHolder.mParticleSystems.DataArrayContains(particle.ParticleSystem)
                ? IdToNumber(_effectSystem.mParticleHolder.mParticleSystems.DataArrayGetID(particle.ParticleSystem))
                : 0d;
        }

        internal double GetEmitterId(ShowcaseParticleEmitter emitter)
        {
            return emitter?.Emitter is not null && _effectSystem.mParticleHolder.mEmitters.DataArrayContains(emitter.Emitter)
                ? IdToNumber(_effectSystem.mParticleHolder.mEmitters.DataArrayGetID(emitter.Emitter))
                : 0d;
        }

        internal double GetParticleId(ShowcaseParticleInstance particle)
        {
            return particle?.Particle is not null && _effectSystem.mParticleHolder.mParticles.DataArrayContains(particle.Particle)
                ? IdToNumber(_effectSystem.mParticleHolder.mParticles.DataArrayGetID(particle.Particle))
                : 0d;
        }

        internal double GetAttachmentId(ShowcaseAttachment attachment)
        {
            return attachment?.Attachment is not null && _effectSystem.mAttachmentHolder.mAttachments.DataArrayContains(attachment.Attachment)
                ? IdToNumber(_effectSystem.mAttachmentHolder.mAttachments.DataArrayGetID(attachment.Attachment))
                : 0d;
        }

        internal double GetTrailId(ShowcaseTrail trail)
        {
            return trail?.Trail is not null && _effectSystem.mTrailHolder.mTrails.DataArrayContains(trail.Trail)
                ? IdToNumber(_effectSystem.mTrailHolder.mTrails.DataArrayGetID(trail.Trail))
                : 0d;
        }

        internal Reanimation GetReanimationById(double id)
        {
            return _effectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet(IdFromNumber<ReanimationID>(id));
        }

        internal ParticleSystem GetParticleSystemById(double id)
        {
            return _effectSystem.mParticleHolder.mParticleSystems.DataArrayTryToGet(IdFromNumber<ParticleSystemID>(id));
        }

        internal ParticleEmitter GetEmitterById(double id)
        {
            return _effectSystem.mParticleHolder.mEmitters.DataArrayTryToGet(IdFromNumber<ParticleEmitterID>(id));
        }

        internal ParticleInstance GetParticleById(double id)
        {
            return _effectSystem.mParticleHolder.mParticles.DataArrayTryToGet(IdFromNumber<ParticleID>(id));
        }

        internal Attachment GetAttachmentById(double id)
        {
            return _effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(IdFromNumber<AttachmentID>(id));
        }

        internal Trail GetTrailById(double id)
        {
            return _effectSystem.mTrailHolder.mTrails.DataArrayTryToGet(IdFromNumber<TrailID>(id));
        }

        private void Update(double deltaSeconds)
        {
            if (_scriptCallbacks?.HasUpdate == true)
            {
                _scriptCallbacks.Update(deltaSeconds);
            }
            else
            {
                _effectSystem.Update();
                foreach (ShowcaseTrail trail in _trails)
                {
                    trail.UpdateAttachedPath();
                    trail.UpdateStandalonePath();
                }
            }

            _effectSystem.ProcessDeleteQueue();
        }

        private void Draw(FrameCaptureGraphics graphics)
        {
            foreach (ShowcaseReanimation reanimation in _reanims)
            {
                if (!reanimation.Reanimation.mIsAttachment)
                {
                    reanimation.Reanimation.Draw(graphics);
                }
            }

            foreach (ShowcaseParticle particle in _particles)
            {
                ParticleSystem system = particle.ParticleSystem;
                if (system is { mIsAttachment: false, mDead: false })
                {
                    system.Draw(graphics);
                }
            }

            foreach (ShowcaseTrail trailInstance in _trails)
            {
                Trail trail = trailInstance.Trail;
                if (trail is { mIsAttachment: false, mDead: false })
                {
                    trail.Draw(graphics);
                }
            }
        }

        private void EnsureAttachTarget(Reanimation parent, string trackName)
        {
            EnsureAlive();
            if (parent is null)
            {
                throw new InvalidOperationException("The parent reanimation is not valid.");
            }

            EnsureSameEffectSystem(parent);

            if (string.IsNullOrWhiteSpace(trackName))
            {
                throw new ArgumentException("A track name is required.", nameof(trackName));
            }

            if (!parent.TrackExists(trackName))
            {
                throw new InvalidOperationException($"Track '{trackName}' was not found on reanim '{parent.mReanimationType}'.");
            }
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

        private void EnsureSameEffectSystem(Reanimation reanimation)
        {
            if (!ReferenceEquals(reanimation.mReanimationHolder?.mEffectSystem, _effectSystem))
            {
                throw new InvalidOperationException("The reanimation belongs to a different showcase scene.");
            }

            if (!_effectSystem.mReanimationHolder.mReanimations.DataArrayContains(reanimation))
            {
                throw new InvalidOperationException("The reanimation is no longer active in this showcase scene.");
            }
        }

        private void EnsureSameEffectSystem(ParticleSystem particleSystem)
        {
            if (!ReferenceEquals(particleSystem.mParticleHolder?.mEffectSystem, _effectSystem))
            {
                throw new InvalidOperationException("The particle belongs to a different showcase scene.");
            }

            if (!_effectSystem.mParticleHolder.mParticleSystems.DataArrayContains(particleSystem))
            {
                throw new InvalidOperationException("The particle is no longer active in this showcase scene.");
            }
        }

        private void EnsureSameEffectSystem(Trail trail)
        {
            if (!ReferenceEquals(trail.mTrailHolder?.mEffectSystem, _effectSystem))
            {
                throw new InvalidOperationException("The trail belongs to a different showcase scene.");
            }

            if (!_effectSystem.mTrailHolder.mTrails.DataArrayContains(trail))
            {
                throw new InvalidOperationException("The trail is no longer active in this showcase scene.");
            }
        }

        private void EnsureAlive()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ShowcaseScene));
            }
        }

        private void InitializeReanimation(Reanimation reanimation, string id, ReanimAsset asset, float x, float y)
        {
            _project.Definitions.TryGetReanimDefinition(asset, out ReanimatorDefinition definition);

            reanimation.mReanimationType = id;
            reanimation.mDefinition = definition ?? new ReanimatorDefinition();
            reanimation.mLoopType = ReanimLoopType.Loop;
            reanimation.mAnimRate = definition?.mFPS ?? 12f;
            reanimation.mLastFrameTime = -1f;
            reanimation.mOverlayMatrix = Matrix4x4.Identity;
            reanimation.mColorOverride = EffectColor.White;
            reanimation.mExtraAdditiveColor = EffectColor.White;
            reanimation.mExtraOverlayColor = EffectColor.White;
            reanimation.SetPosition(x, y);

            if (definition?.mTrackCount > 0)
            {
                reanimation.mFrameCount = definition.mTracks[0].mTransformCount;
                reanimation.mTrackInstances = new ReanimatorTrackInstance[definition.mTrackCount];
                for (int i = 0; i < reanimation.mTrackInstances.Length; i++)
                {
                    reanimation.mTrackInstances[i].Reset();
                    string trackName = definition.mTracks[i].mName;
                    reanimation.mTrackInstances[i].mIsAttacher =
                        ReanimatorUtility.gReanimationParamArray != null &&
                        !string.IsNullOrEmpty(trackName) &&
                        trackName.StartsWith(Reanimation.Attacher, StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                reanimation.mFrameCount = 0;
                reanimation.mTrackInstances = [];
            }
        }

        private ParticleDefinition LoadParticleDefinition(EffectAsset asset)
        {
            ParticleDefinition cached = _project.Definitions.GetParticleDefinitionCloneByPath(asset.Path);
            if (cached is not null)
            {
                return cached;
            }

            string fullPath = ResolveAssetPath(asset.Path);
            try
            {
                if (!string.IsNullOrWhiteSpace(fullPath) &&
                    File.Exists(fullPath) &&
                    ParticleUtility.LoadDefinition(out ParticleDefinition definition, fullPath))
                {
                    return definition;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
            {
            }

            return new ParticleDefinition
            {
                mEmitterDefs = [],
                mEmitterDefCount = 0
            };
        }

        private TrailDefinition LoadTrailDefinition(EffectAsset asset)
        {
            TrailDefinition cached = _project.Definitions.GetTrailDefinitionCloneByPath(asset.Path);
            if (cached is not null)
            {
                cached.ApplyDefaults();
                return cached;
            }

            string fullPath = ResolveAssetPath(asset.Path);
            try
            {
                if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
                {
                    return Rendering.TrailPreviewFrameBuilder.LoadDefinition(fullPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
            {
            }

            TrailDefinition definition = new();
            definition.ApplyDefaults();
            return definition;
        }

        private string ResolveAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(assetPath) || string.IsNullOrWhiteSpace(_project.RootPath)
                ? assetPath
                : Path.Combine(_project.RootPath, assetPath);
        }
    }
}
