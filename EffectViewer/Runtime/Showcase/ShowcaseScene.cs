using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseScene : IRenderFrameProvider, IDisposable
    {
        private const double UpdateStepSeconds = 1.0 / TodLibConstants.TICKS_PER_SECOND;

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

            if (EffectSystem.gEffectSystem != null)
            {
                EffectSystem.gEffectSystem.EffectSystemDispose();
            }

            _effectSystem.EffectSystemInitialize();
        }

        public IReadOnlyList<ShowcaseReanimation> Reanimations => _reanims;
        public IReadOnlyList<ShowcaseParticle> Particles => _particles;
        public IReadOnlyList<ShowcaseTrail> Trails => _trails;

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

            Reanimation reanimation = EffectSystem.gEffectSystem.mReanimationHolder.mReanimations.DataArrayAlloc();
            reanimation.mReanimationHolder = EffectSystem.gEffectSystem.mReanimationHolder;
            reanimation.mRenderOrder = _reanims.Count + _particles.Count + _trails.Count;
            InitializeReanimation(reanimation, id, ResolveAssetPath(asset.Path), (float)x, (float)y);

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

            TodParticleDefinition definition = LoadParticleDefinition(asset);
            TodParticleSystem system = EffectSystem.gEffectSystem.mParticleHolder.AllocParticleSystemFromDef(
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
            Trail trail = EffectSystem.gEffectSystem.mTrailHolder.AllocTrailFromDef(
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
            while (_accumulator >= UpdateStepSeconds && guard++ < 20)
            {
                Update(UpdateStepSeconds);
                _accumulator -= UpdateStepSeconds;
            }

            FrameCaptureGraphics graphics = new()
            {
                mClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3),
                mColor = SexyColor.White,
                mDrawMode = DrawMode.Normal
            };

            if (_scriptCallbacks?.TryDraw(graphics) != true)
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
            if (ReferenceEquals(EffectSystem.gEffectSystem, _effectSystem))
            {
                _effectSystem.EffectSystemDispose();
            }
        }

        internal void AttachReanimation(Reanimation parent, string trackName, ShowcaseReanimation child, float offsetX, float offsetY)
        {
            EnsureAttachTarget(parent, trackName);
            if (child?.Reanimation is null)
            {
                throw new InvalidOperationException("The child reanimation is not valid.");
            }

            int trackIndex = parent.FindTrackIndex(trackName);
            parent.GetTrackBasePoseMatrix(trackIndex, out Matrix4x4 basePoseMatrix);
            Vector2 position = Vector2.Transform(new Vector2(offsetX, offsetY), basePoseMatrix);
            ref ReanimatorTrackInstance track = ref parent.GetTrackInstanceByName(trackName);
            GlobalMembersAttachment.AttachReanim(ref track.mAttachmentID, child.Reanimation, position.X, position.Y);
        }

        internal void AttachParticle(Reanimation parent, string trackName, ShowcaseParticle child, float offsetX, float offsetY)
        {
            EnsureAttachTarget(parent, trackName);
            parent.AttachParticleToTrack(trackName, child?.ParticleSystem, offsetX, offsetY);
        }

        internal void AttachTrail(Reanimation parent, string trackName, ShowcaseTrail child, float offsetX, float offsetY)
        {
            EnsureAttachTarget(parent, trackName);
            if (child?.Trail is null)
            {
                throw new InvalidOperationException("The child trail is not valid.");
            }

            child.PrepareForAttachment();
            int trackIndex = parent.FindTrackIndex(trackName);
            parent.GetTrackBasePoseMatrix(trackIndex, out Matrix4x4 basePoseMatrix);
            Vector2 position = Vector2.Transform(new Vector2(offsetX, offsetY), basePoseMatrix);
            ref ReanimatorTrackInstance track = ref parent.GetTrackInstanceByName(trackName);
            GlobalMembersAttachment.AttachTrail(ref track.mAttachmentID, child.Trail, position.X, position.Y);
        }

        internal void DetachTrack(Reanimation parent, string trackName)
        {
            EnsureAttachTarget(parent, trackName);
            ref ReanimatorTrackInstance track = ref parent.GetTrackInstanceByName(trackName);
            GlobalMembersAttachment.AttachmentDetach(ref track.mAttachmentID);
        }

        private void Update(double deltaSeconds)
        {
            _scriptCallbacks?.Update(deltaSeconds);
            EffectSystem.gEffectSystem.Update();
            foreach (ShowcaseTrail trail in _trails)
            {
                trail.UpdateAttachedPath();
                trail.UpdateStandalonePath();
            }

            EffectSystem.gEffectSystem.ProcessDeleteQueue();
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
                TodParticleSystem system = particle.ParticleSystem;
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

            if (string.IsNullOrWhiteSpace(trackName))
            {
                throw new ArgumentException("A track name is required.", nameof(trackName));
            }

            if (!parent.TrackExists(trackName))
            {
                throw new InvalidOperationException($"Track '{trackName}' was not found on reanim '{parent.mReanimationType}'.");
            }
        }

        private void EnsureAlive()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ShowcaseScene));
            }
        }

        private void InitializeReanimation(Reanimation reanimation, string id, string fullPath, float x, float y)
        {
            ReanimatorDefinition definition = null;
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                ReanimatorXnaHelpers.ReanimationLoadDefinition(fullPath, ref definition);
            }

            reanimation.mReanimationType = id;
            reanimation.mDefinition = definition ?? new ReanimatorDefinition();
            reanimation.mLoopType = ReanimLoopType.Loop;
            reanimation.mAnimRate = definition?.mFPS ?? 12f;
            reanimation.mLastFrameTime = -1f;
            reanimation.mOverlayMatrix = Matrix4x4.Identity;
            reanimation.mColorOverride = SexyColor.White;
            reanimation.mExtraAdditiveColor = SexyColor.White;
            reanimation.mExtraOverlayColor = SexyColor.White;
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
                        ReanimatorXnaHelpers.gReanimationParamArray != null &&
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

        private TodParticleDefinition LoadParticleDefinition(EffectAsset asset)
        {
            string fullPath = ResolveAssetPath(asset.Path);
            try
            {
                if (!string.IsNullOrWhiteSpace(fullPath) &&
                    File.Exists(fullPath) &&
                    TodParticleGlobal.TodParticleLoadADef(out TodParticleDefinition definition, fullPath))
                {
                    return definition;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
            {
            }

            return new TodParticleDefinition
            {
                mEmitterDefs = [],
                mEmitterDefCount = 0
            };
        }

        private TrailDefinition LoadTrailDefinition(EffectAsset asset)
        {
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
