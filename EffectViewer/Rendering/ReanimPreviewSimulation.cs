using System;
using System.IO;
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

        public ReanimPreviewSimulation(EffectProject project, string path, float x = 0f, float y = 0f)
        {
            ResourceHandler.SetProvider(new ProjectResourceProvider(project));
            string fullPath = ResolvePath(project, path);
            _reanimation = CreateReanimation(fullPath);
            _x = x;
            _y = y;
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

        private static string ResolvePath(EffectProject project, string path)
        {
            return Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(project.RootPath)
                ? path
                : Path.Combine(project.RootPath, path);
        }
    }
}
