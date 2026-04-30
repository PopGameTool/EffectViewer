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
        private readonly Reanimation _reanimation;
        private readonly float _x;
        private readonly float _y;
        private double _accumulator;

        public IReadOnlyList<string> TrackNames { get; }
        public IReadOnlyList<string> LayerTrackNames { get; }
        public IReadOnlyList<string> LayerNames { get; }

        public ReanimPreviewSimulation(EffectProject project, string path, float x = 0f, float y = 0f)
        {
            ResourceHandler.SetProvider(new ProjectResourceProvider(project));
            string fullPath = ResolvePath(project, path);
            _reanimation = CreateReanimation(fullPath);
            _x = x;
            _y = y;
            TrackNames = BuildTrackNames(_reanimation.mDefinition);
            LayerTrackNames = BuildLayerTrackNames(_reanimation.mDefinition);
            LayerNames = BuildLayerNames(LayerTrackNames);
        }

        public RenderFrame GetFrame(double deltaSeconds)
        {
            _accumulator += deltaSeconds;
            int guard = 0;
            while (_accumulator >= UpdateStepSeconds && guard++ < 20)
            {
                Update();
                _accumulator -= UpdateStepSeconds;
            }

            return BuildFrame();
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

        private static Reanimation CreateReanimation(string fullPath)
        {
            ReanimatorDefinition definition = null;
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                ReanimatorXnaHelpers.ReanimationLoadDefinition(fullPath, ref definition);
            }

            Reanimation reanimation = new()
            {
                mDefinition = definition ?? new ReanimatorDefinition(),
                mLoopType = ReanimLoopType.Loop,
                mAnimRate = definition?.mFPS ?? 12f,
                mLastFrameTime = -1f,
                mOverlayMatrix = Matrix4x4.Identity,
                mColorOverride = SexyColor.White,
                mExtraAdditiveColor = SexyColor.White,
                mExtraOverlayColor = SexyColor.White
            };

            if (definition?.mTrackCount > 0)
            {
                reanimation.mFrameCount = definition.mTracks[0].mTransformCount;
                reanimation.mTrackInstances = new ReanimatorTrackInstance[definition.mTrackCount];
                for (int i = 0; i < reanimation.mTrackInstances.Length; i++)
                {
                    reanimation.mTrackInstances[i].Reset();
                }
            }

            return reanimation;
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
