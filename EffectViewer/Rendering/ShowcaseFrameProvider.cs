using System;
using System.Collections.Generic;
using System.Numerics;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Runtime;

namespace EffectViewer.Rendering
{
    public sealed class ShowcaseFrameProvider : IRenderFrameProvider, IDisposable
    {
        private readonly List<IRenderFrameProvider> _providers = [];
        private bool _disposed;

        public ShowcaseFrameProvider(EffectProject project, IEnumerable<SceneObject> sceneObjects)
        {
            foreach (SceneObject sceneObject in sceneObjects)
            {
                IRenderFrameProvider provider = CreateProvider(project, sceneObject);
                if (provider != null)
                {
                    _providers.Add(provider);
                }
            }
        }

        public RenderFrame GetFrame(double deltaSeconds)
        {
            RenderFrame combined = new();
            if (_disposed)
            {
                return combined;
            }

            foreach (IRenderFrameProvider provider in _providers)
            {
                RenderFrame frame = provider.GetFrame(deltaSeconds);
                AppendFrame(combined, frame);
            }

            return combined;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (IRenderFrameProvider provider in _providers)
            {
                if (provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _providers.Clear();
        }

        private static IRenderFrameProvider CreateProvider(EffectProject project, SceneObject sceneObject)
        {
            float x = (float)sceneObject.X;
            float y = (float)sceneObject.Y;
            return sceneObject.Kind switch
            {
                EffectAssetKind.Reanim when project.Assets.Reanims.TryGetValue(sceneObject.Id, out ReanimAsset reanim) =>
                    new ReanimPreviewSimulation(project, reanim.Path, x, y),
                EffectAssetKind.Particle when project.Assets.Particles.TryGetValue(sceneObject.Id, out EffectAsset particle) =>
                    new ParticlePreviewSimulation(project, particle.Path, sceneObject.Id, x, y),
                EffectAssetKind.Trail when project.Assets.Trails.TryGetValue(sceneObject.Id, out EffectAsset trail) =>
                    new TrailPreviewSimulation(project, trail.Path, sceneObject.Id, x, y),
                _ => null
            };
        }

        private static void AppendFrame(RenderFrame target, RenderFrame source)
        {
            if (source is null)
            {
                return;
            }

            foreach (RenderSpriteCommand sprite in source.Sprites)
            {
                target.Sprites.Add(sprite);
            }

            foreach (RenderMeshCommand mesh in source.Meshes)
            {
                target.AddMesh(mesh.Texture, source.GetMeshVertices(mesh), mesh.BlendMode);
            }
        }
    }
}
