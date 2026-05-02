using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using EffectViewer.Projects;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Rendering
{
    public sealed class TrailPreviewSimulation : IRenderFrameProvider
    {
        private const double UpdateStepSeconds = 1.0 / TodLibConstants.TICKS_PER_SECOND;

        private readonly TrailDefinition _definition;
        private readonly string _textureId;
        private readonly float _x;
        private readonly float _y;
        private double _accumulator;
        private int _tick;
        private Trail _trail;

        public TrailPreviewSimulation(EffectProject project, string path, string fallbackId, float x = 0f, float y = 0f)
            : this(LoadDefinition(project, path), fallbackId, x, y)
        {
        }

        public TrailPreviewSimulation(TrailDefinition definition, string fallbackId, float x = 0f, float y = 0f)
        {
            _definition = definition ?? new TrailDefinition();
            _definition.ApplyDefaults();
            _textureId = string.IsNullOrWhiteSpace(_definition.mImage) ? fallbackId : _definition.mImage;
            _x = x;
            _y = y;
            Reset();
        }

        private static TrailDefinition LoadDefinition(EffectProject project, string path)
        {
            string fullPath = TrailPreviewFrameBuilder.ResolvePath(project, path);
            return !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)
                ? TrailPreviewFrameBuilder.LoadDefinition(fullPath)
                : new TrailDefinition();
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

        private void Reset()
        {
            _accumulator = 0;
            _tick = 0;
            _trail = CreateTrail();
        }

        private void Update()
        {
            _tick++;

            _trail.Update();
            if (_trail.mDead)
            {
                Reset();
            }

            Vector2 point = BuildMovingPoint(_tick);
            _trail.AddPoint(point.X + _x, point.Y + _y);
        }

        private RenderFrame BuildFrame()
        {
            RenderFrame frame = new();
            List<RenderVertex> vertices = TrailPreviewFrameBuilder.BuildMesh(_trail);
            if (vertices.Count > 0)
            {
                frame.Meshes.Add(new RenderMeshCommand(new RenderTextureRef(_textureId), vertices, RenderBlendMode.Normal));
            }

            return frame;
        }

        private Trail CreateTrail()
        {
            Trail trail = new()
            {
                mDefinition = _definition,
                mTrailDuration = EvaluateDuration()
            };

            return trail;
        }

        private int EvaluateDuration()
        {
            float interp = TodCommon.RandRangeFloat(0f, 1f);
            int duration = (int)Definition.FloatTrackEvaluate(_definition.mTrailDuration, 0f, interp);
            return Math.Max(2, duration);
        }

        private static Vector2 BuildMovingPoint(int tick)
        {
            float t = (tick % 800) / 799f;
            t = (t > 0.5f) ? (1 - t) : t;
            float x = 120f + t * 1200f;
            float y = 280f + MathF.Sin(t * MathF.PI * 12f) * 92f;
            return new Vector2(x, y);
        }
    }
}
