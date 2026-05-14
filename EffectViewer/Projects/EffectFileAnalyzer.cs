using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EffectViewer.EffectRuntime.Particle;
using EffectViewer.EffectRuntime.Reanim;
using EffectViewer.EffectRuntime.Trail;

namespace EffectViewer.Projects
{
    public sealed class EffectFileAnalyzer
    {
        public EffectFileSummary Analyze(EffectProject project, EffectAssetKind kind, string path)
        {
            string fullPath = ResolvePath(project, path);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return new EffectFileSummary
                {
                    Kind = kind.ToString(),
                    Error = "File not found."
                };
            }

            try
            {
                return kind switch
                {
                    EffectAssetKind.Reanim => AnalyzeReanim(project, fullPath),
                    EffectAssetKind.Particle => AnalyzeParticle(project, fullPath),
                    EffectAssetKind.Trail => AnalyzeTrail(project, fullPath),
                    _ => new EffectFileSummary { Kind = kind.ToString(), Error = "Unsupported effect type." }
                };
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                return new EffectFileSummary
                {
                    Kind = kind.ToString(),
                    Error = ex.Message
                };
            }
        }

        private static EffectFileSummary AnalyzeReanim(EffectProject project, string fullPath)
        {
            ReanimatorDefinition definition;
            if (project?.Definitions?.TryGetReanimDefinitionByPath(fullPath, out definition) != true)
            {
                using FileStream stream = File.OpenRead(fullPath);
                definition = ReanimReader.Decode(stream);
            }

            HashSet<string> images = new(StringComparer.OrdinalIgnoreCase);
            int frameCount = 0;
            if (definition.mTracks != null)
            {
                foreach (ReanimatorTrack track in definition.mTracks)
                {
                    frameCount = Math.Max(frameCount, track.mTransformCount);
                    if (track.mTransforms is null)
                    {
                        continue;
                    }

                    foreach (ReanimatorTransform transform in track.mTransforms)
                    {
                        if (!string.IsNullOrWhiteSpace(transform.mImage))
                        {
                            images.Add(transform.mImage);
                        }
                    }
                }
            }

            return WithMissingImages(project, new EffectFileSummary
            {
                IsLoaded = true,
                Kind = "Reanim",
                TrackCount = definition.mTrackCount,
                FrameCount = frameCount,
                Fps = definition.mFPS,
                ImageIds = images.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList()
            });
        }

        private static EffectFileSummary AnalyzeParticle(EffectProject project, string fullPath)
        {
            ParticleDefinition definition;
            if (project?.Definitions?.TryGetParticleDefinitionByPath(fullPath, out definition) != true)
            {
                using FileStream stream = File.OpenRead(fullPath);
                definition = ParticleDefinitionCodec.Decode(stream);
            }

            HashSet<string> images = new(StringComparer.OrdinalIgnoreCase);
            int fieldCount = 0;
            if (definition.mEmitterDefs != null)
            {
                foreach (ParticleEmitterDefinition emitter in definition.mEmitterDefs)
                {
                    if (!string.IsNullOrWhiteSpace(emitter.mImage))
                    {
                        images.Add(emitter.mImage);
                    }

                    fieldCount += emitter.mParticleFieldCount + emitter.mSystemFieldCount;
                }
            }

            return WithMissingImages(project, new EffectFileSummary
            {
                IsLoaded = true,
                Kind = "Particle",
                EmitterCount = definition.mEmitterDefCount,
                FieldCount = fieldCount,
                ImageIds = images.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList()
            });
        }

        private static EffectFileSummary AnalyzeTrail(EffectProject project, string fullPath)
        {
            TrailDefinition definition;
            if (project?.Definitions?.TryGetTrailDefinitionByPath(fullPath, out definition) != true)
            {
                using FileStream stream = File.OpenRead(fullPath);
                definition = TrailReader.Decode(stream);
            }

            List<string> images = [];
            if (!string.IsNullOrWhiteSpace(definition.mImage))
            {
                images.Add(definition.mImage);
            }

            return WithMissingImages(project, new EffectFileSummary
            {
                IsLoaded = true,
                Kind = "Trail",
                MaxPoints = definition.mMaxPoints,
                MinPointDistance = definition.mMinPointDistance,
                ImageIds = images
            });
        }

        private static EffectFileSummary WithMissingImages(EffectProject project, EffectFileSummary summary)
        {
            List<ImageReferenceResolution> resolutions = [];
            List<string> resolved = [];
            List<string> missing = [];

            foreach (string id in summary.ImageIds)
            {
                if (project.Assets.TryGetImage(id, out Assets.ImageAsset asset))
                {
                    resolved.Add(asset.Id);
                    resolutions.Add(new ImageReferenceResolution(id, asset.Id));
                }
                else
                {
                    missing.Add(id);
                    resolutions.Add(new ImageReferenceResolution(id, string.Empty));
                }
            }

            summary.ResolvedImageIds = resolved
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            summary.ImageResolutions = resolutions
                .OrderBy(item => item.RequestedId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            summary.MissingImageIds = missing
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return summary;
        }

        private static string ResolvePath(EffectProject project, string path)
        {
            return Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(project.RootPath)
                ? path
                : Path.Combine(project.RootPath, path);
        }
    }
}
