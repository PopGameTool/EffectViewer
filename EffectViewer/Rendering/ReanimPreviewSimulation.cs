using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EffectViewer.Projects;
using EffectViewer.Runtime;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Reanim;

namespace EffectViewer.Rendering
{
    public sealed class ReanimPreviewSimulation : IRenderFrameProvider
    {
        private const double UpdateStepSeconds = 1.0 / TodLibConstants.TICKS_PER_SECOND;
        private readonly Reanimation _reanimation = new();
        private readonly float _x;
        private readonly float _y;
        private bool _isPaused;
        private double _accumulator;

        public IReadOnlyList<string> TrackNames { get; private set; }
        public IReadOnlyList<string> LayerTrackNames { get; private set; }
        public IReadOnlyList<string> LayerNames { get; private set; }

        public ReanimPreviewSimulation(EffectProject project, string path, float x = 0f, float y = 0f)
        {
            ResourceHandler.SetProvider(new ProjectResourceProvider(project));
            string fullPath = ResolvePath(project, path);
            _x = x;
            _y = y;
            SetDefinition(LoadDefinition(fullPath));
        }

        public RenderFrame GetFrame(double deltaSeconds)
        {
            if (!_isPaused)
            {
                _accumulator += deltaSeconds;
                int guard = 0;
                while (_accumulator >= UpdateStepSeconds && guard++ < 20)
                {
                    Update();
                    _accumulator -= UpdateStepSeconds;
                }
            }

            return BuildFrame();
        }

        public void SetDefinition(ReanimatorDefinition definition)
        {
            InitializeReanimation(definition ?? new ReanimatorDefinition());
            TrackNames = BuildTrackNames(_reanimation.mDefinition);
            LayerTrackNames = BuildLayerTrackNames(_reanimation.mDefinition);
            LayerNames = BuildLayerNames(LayerTrackNames);
            _accumulator = 0d;
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
            _accumulator = 0d;
        }

        public void SetAnimRate(float animRate)
        {
            _reanimation.mAnimRate = animRate;
        }

        public void SetFrameIndex(int frameIndex)
        {
            if (_reanimation.mFrameCount <= 0)
            {
                return;
            }

            int clampedFrame = Math.Clamp(
                frameIndex,
                _reanimation.mFrameStart,
                _reanimation.mFrameStart + _reanimation.mFrameCount - 1);
            int denominator = Math.Max(1, _reanimation.mFrameCount - 1);
            _reanimation.mAnimTime = Math.Clamp(
                (clampedFrame - _reanimation.mFrameStart) / (float)denominator,
                0f,
                1f);
            _reanimation.mLastFrameTime = _reanimation.mAnimTime;
        }

        public void SetLayer(string trackName)
        {
            if (_reanimation.mDefinition?.mTrackCount <= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(trackName))
            {
                SetFullTimeline();
                return;
            }

            if (!_reanimation.TrackExists(trackName))
            {
                return;
            }

            _reanimation.mDead = false;
            _reanimation.mLoopType = ReanimLoopType.Loop;
            _reanimation.SetFramesForLayer(trackName);
        }

        public void SetTrackVisible(int trackIndex, bool visible)
        {
            if (_reanimation.mTrackInstances is null ||
                trackIndex < 0 ||
                trackIndex >= _reanimation.mTrackInstances.Length)
            {
                return;
            }

            _reanimation.mTrackInstances[trackIndex].mRenderGroup = visible
                ? ReanimatorXnaHelpers.RENDER_GROUP_NORMAL
                : ReanimatorXnaHelpers.RENDER_GROUP_HIDDEN;
        }

        public void SetAllTracksVisible(bool visible)
        {
            if (_reanimation.mTrackInstances is null)
            {
                return;
            }

            for (int i = 0; i < _reanimation.mTrackInstances.Length; i++)
            {
                SetTrackVisible(i, visible);
            }
        }

        private void Update()
        {
            if (_reanimation.mFrameCount == 0 || _reanimation.mDead)
            {
                return;
            }

            _reanimation.mLastFrameTime = _reanimation.mAnimTime;
            _reanimation.mAnimTime += ReanimatorXnaHelpers.SECONDS_PER_UPDATE * _reanimation.mAnimRate / _reanimation.mFrameCount;
            while (_reanimation.mAnimTime >= 1f)
            {
                _reanimation.mLoopCount++;
                _reanimation.mAnimTime -= 1f;
            }
        }

        private RenderFrame BuildFrame()
        {
            FrameCaptureGraphics graphics = new()
            {
                mClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3),
                mColor = SexyColor.White,
                mDrawMode = DrawMode.Normal,
                mTransX = _x,
                mTransY = _y
            };

            _reanimation.Draw(graphics);
            return graphics.Frame;
        }

        private static ReanimatorDefinition LoadDefinition(string fullPath)
        {
            ReanimatorDefinition definition = null;
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                ReanimatorXnaHelpers.ReanimationLoadDefinition(fullPath, ref definition);
            }

            return definition ?? new ReanimatorDefinition();
        }

        private void InitializeReanimation(ReanimatorDefinition definition)
        {
            _reanimation.Reset();
            _reanimation.mDefinition = definition ?? new ReanimatorDefinition();
            _reanimation.mLoopType = ReanimLoopType.Loop;
            _reanimation.mAnimRate = definition?.mFPS ?? 12f;
            _reanimation.mLastFrameTime = -1f;
            _reanimation.mOverlayMatrix = Matrix4x4.Identity;
            _reanimation.mColorOverride = SexyColor.White;
            _reanimation.mExtraAdditiveColor = SexyColor.White;
            _reanimation.mExtraOverlayColor = SexyColor.White;

            if (definition?.mTrackCount > 0)
            {
                _reanimation.mFrameCount = definition.mTracks[0].mTransformCount;
                _reanimation.mTrackInstances = new ReanimatorTrackInstance[definition.mTrackCount];
                for (int i = 0; i < _reanimation.mTrackInstances.Length; i++)
                {
                    _reanimation.mTrackInstances[i].Reset();
                }
            }
        }

        private void SetFullTimeline()
        {
            if (_reanimation.mDefinition?.mTrackCount <= 0 ||
                _reanimation.mDefinition.mTracks is null ||
                _reanimation.mDefinition.mTracks.Length == 0)
            {
                return;
            }

            _reanimation.mFrameStart = 0;
            _reanimation.mFrameCount = _reanimation.mDefinition.mTracks[0].mTransformCount;
            _reanimation.mAnimTime = 0f;
            _reanimation.mLastFrameTime = -1f;
            _reanimation.mLoopCount = 0;
            _reanimation.mDead = false;
        }

        private static IReadOnlyList<string> BuildTrackNames(ReanimatorDefinition definition)
        {
            if (definition?.mTracks is null || definition.mTrackCount <= 0)
            {
                return [];
            }

            return definition.mTracks
                .Take(definition.mTrackCount)
                .Select((track, index) => string.IsNullOrWhiteSpace(track.mName)
                    ? $"Track {index}"
                    : track.mName)
                .ToArray();
        }

        private static IReadOnlyList<string> BuildLayerTrackNames(ReanimatorDefinition definition)
        {
            if (definition?.mTracks is null || definition.mTrackCount <= 0)
            {
                return [];
            }

            return definition.mTracks
                .Take(definition.mTrackCount)
                .Select(track => track.mName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
        }

        private static IReadOnlyList<string> BuildLayerNames(IReadOnlyList<string> trackNames)
        {
            string[] layerNames = trackNames
                .Where(name => name.StartsWith("anim_", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return layerNames.Length > 0 ? layerNames : trackNames.ToArray();
        }

        private static string ResolvePath(EffectProject project, string path)
        {
            return Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(project.RootPath)
                ? path
                : Path.Combine(project.RootPath, path);
        }
    }
}
