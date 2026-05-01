using System;
using System.IO;
using EffectViewer.Projects;
using EffectViewer.Runtime;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Particle;

namespace EffectViewer.Rendering
{
    public sealed class ParticlePreviewSimulation : IRenderFrameProvider, IDisposable
    {
        private const double UpdateStepSeconds = 1.0 / TodLibConstants.TICKS_PER_SECOND;
        private const int MaxRestartTicks = TodLibConstants.TICKS_PER_SECOND * 12;

        private readonly EffectProject _project;
        private readonly string _path;
        private readonly string _assetId;
        private readonly float _x;
        private readonly float _y;
        private readonly TodParticleHolder _holder = new();
        private TodParticleDefinition _definition;
        private TodParticleSystem _system;
        private double _accumulator;
        private int _ticksSinceRestart;
        private bool _disposed;

        public ParticlePreviewSimulation(EffectProject project, string path, string assetId, float x = 400f, float y = 300f)
        {
            _project = project;
            _path = path;
            _assetId = assetId;
            _x = x;
            _y = y;
            ResourceHandler.SetProvider(new ProjectResourceProvider(project));
            _holder.InitializeHolder();
            _definition = LoadDefinition();
            ParticleDefinitionUtility.ApplyRuntimeDefaults(_definition);
            Reset();
        }

        public ParticlePreviewSimulation(EffectProject project, TodParticleDefinition definition, string assetId, float x = 400f, float y = 300f)
        {
            _project = project;
            _path = string.Empty;
            _assetId = assetId;
            _x = x;
            _y = y;
            ResourceHandler.SetProvider(new ProjectResourceProvider(project));
            _holder.InitializeHolder();
            _definition = ParticleDefinitionUtility.Clone(definition);
            ParticleDefinitionUtility.ApplyRuntimeDefaults(_definition);
            Reset();
        }

        public RenderFrame GetFrame(double deltaSeconds)
        {
            if (_disposed)
            {
                return new RenderFrame();
            }

            _accumulator += deltaSeconds;
            int guard = 0;
            while (_accumulator >= UpdateStepSeconds && guard++ < 20)
            {
                Update();
                _accumulator -= UpdateStepSeconds;
            }

            return BuildFrame();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _system = null;
            _holder.Dispose();
        }

        private void Reset()
        {
            if (_disposed)
            {
                return;
            }

            _holder.mParticleSystems.DataArrayFreeAll();
            _holder.mEmitters.DataArrayFreeAll();
            _holder.mParticles.DataArrayFreeAll();
            _accumulator = 0;
            _ticksSinceRestart = 0;
            _system = _holder.AllocParticleSystemFromDef(_x, _y, 0, _definition, _assetId);
        }

        private void Update()
        {
            _ticksSinceRestart++;
            if (_system == null || _system.mDead || _ticksSinceRestart > MaxRestartTicks)
            {
                Reset();
                return;
            }

            _system.Update();
        }

        private RenderFrame BuildFrame()
        {
            FrameCaptureGraphics graphics = new()
            {
                mClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3),
                mColor = SexyColor.White,
                mDrawMode = DrawMode.Normal
            };

            _system?.Draw(graphics);
            return graphics.Frame;
        }

        private TodParticleDefinition LoadDefinition()
        {
            string fullPath = ResolvePath(_project, _path);
            if (_project.Assets.Particles.TryGetValue(_assetId, out EffectAsset asset))
            {
                fullPath = ResolvePath(_project, asset.Path);
            }

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

            return ParticleDefinitionUtility.CreateEmpty();
        }

        private static string ResolvePath(EffectProject project, string path)
        {
            return Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(project.RootPath)
                ? path
                : Path.Combine(project.RootPath, path);
        }

    }
}
