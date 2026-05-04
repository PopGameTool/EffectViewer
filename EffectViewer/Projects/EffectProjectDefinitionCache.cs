using System;
using System.Collections.Generic;
using System.IO;
using EffectViewer.Assets;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Projects
{
    public sealed class EffectProjectDefinitionCache
    {
        private readonly EffectProject _project;
        private readonly Dictionary<string, ReanimatorDefinition> _reanimsByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TodParticleDefinition> _particlesByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TrailDefinition> _trailsByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new();

        internal EffectProjectDefinitionCache(EffectProject project)
        {
            _project = project;
        }

        public void PreloadAll(IProgress<ProjectTransferProgress> progress = null)
        {
            if (_project?.Assets is null)
            {
                return;
            }

            int total = _project.Assets.Reanims.Count +
                _project.Assets.Particles.Count +
                _project.Assets.Trails.Count;
            int completed = 0;
            ReportPreloadProgress(progress, completed, total);

            foreach (ReanimAsset asset in _project.Assets.Reanims.Values)
            {
                TryGetReanimDefinition(asset, out _);
                ReportPreloadProgress(progress, ++completed, total);
            }

            foreach (EffectAsset asset in _project.Assets.Particles.Values)
            {
                TryGetParticleDefinition(asset, out _);
                ReportPreloadProgress(progress, ++completed, total);
            }

            foreach (EffectAsset asset in _project.Assets.Trails.Values)
            {
                TryGetTrailDefinition(asset, out _);
                ReportPreloadProgress(progress, ++completed, total);
            }
        }

        private static void ReportPreloadProgress(
            IProgress<ProjectTransferProgress> progress,
            int completed,
            int total)
        {
            if (progress is null || total <= 0)
            {
                return;
            }

            progress.Report(new ProjectTransferProgress
            {
                CompletedItems = completed,
                TotalItems = total
            });
        }

        public void Invalidate(EffectAssetKind kind, string path)
        {
            string fullPath = ResolvePath(path);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return;
            }

            string key = NormalizePath(fullPath);
            lock (_gate)
            {
                switch (kind)
                {
                    case EffectAssetKind.Reanim:
                        _reanimsByPath.Remove(key);
                        break;
                    case EffectAssetKind.Particle:
                        _particlesByPath.Remove(key);
                        break;
                    case EffectAssetKind.Trail:
                        _trailsByPath.Remove(key);
                        break;
                }
            }
        }

        public bool TryGetReanimDefinition(string assetId, out ReanimatorDefinition definition)
        {
            definition = null;
            return _project?.Assets?.Reanims.TryGetValue(assetId, out ReanimAsset asset) == true &&
                TryGetReanimDefinition(asset, out definition);
        }

        public bool TryGetReanimDefinition(ReanimAsset asset, out ReanimatorDefinition definition)
        {
            return TryGetDefinition(_reanimsByPath, asset?.Path, LoadReanimDefinition, out definition);
        }

        public bool TryGetReanimDefinitionByPath(string path, out ReanimatorDefinition definition)
        {
            return TryGetDefinition(_reanimsByPath, path, LoadReanimDefinition, out definition);
        }

        public ReanimatorDefinition GetReanimDefinitionClone(string assetId)
        {
            return TryGetReanimDefinition(assetId, out ReanimatorDefinition definition)
                ? CloneReanimDefinition(definition)
                : null;
        }

        public ReanimatorDefinition GetReanimDefinitionCloneByPath(string path)
        {
            return TryGetReanimDefinitionByPath(path, out ReanimatorDefinition definition)
                ? CloneReanimDefinition(definition)
                : null;
        }

        public void SetReanimDefinition(string path, ReanimatorDefinition definition)
        {
            SetDefinition(_reanimsByPath, path, CloneReanimDefinition(definition));
        }

        public bool TryGetParticleDefinition(string assetId, out TodParticleDefinition definition)
        {
            definition = null;
            return _project?.Assets?.Particles.TryGetValue(assetId, out EffectAsset asset) == true &&
                TryGetParticleDefinition(asset, out definition);
        }

        public bool TryGetParticleDefinition(EffectAsset asset, out TodParticleDefinition definition)
        {
            return TryGetDefinition(_particlesByPath, asset?.Path, LoadParticleDefinition, out definition);
        }

        public bool TryGetParticleDefinitionByPath(string path, out TodParticleDefinition definition)
        {
            return TryGetDefinition(_particlesByPath, path, LoadParticleDefinition, out definition);
        }

        public TodParticleDefinition GetParticleDefinitionClone(string assetId)
        {
            return TryGetParticleDefinition(assetId, out TodParticleDefinition definition)
                ? ParticleDefinitionUtility.Clone(definition)
                : null;
        }

        public TodParticleDefinition GetParticleDefinitionCloneByPath(string path)
        {
            return TryGetParticleDefinitionByPath(path, out TodParticleDefinition definition)
                ? ParticleDefinitionUtility.Clone(definition)
                : null;
        }

        public void SetParticleDefinition(string path, TodParticleDefinition definition)
        {
            SetDefinition(_particlesByPath, path, ParticleDefinitionUtility.Clone(definition));
        }

        public bool TryGetTrailDefinition(string assetId, out TrailDefinition definition)
        {
            definition = null;
            return _project?.Assets?.Trails.TryGetValue(assetId, out EffectAsset asset) == true &&
                TryGetTrailDefinition(asset, out definition);
        }

        public bool TryGetTrailDefinition(EffectAsset asset, out TrailDefinition definition)
        {
            return TryGetDefinition(_trailsByPath, asset?.Path, LoadTrailDefinition, out definition);
        }

        public bool TryGetTrailDefinitionByPath(string path, out TrailDefinition definition)
        {
            return TryGetDefinition(_trailsByPath, path, LoadTrailDefinition, out definition);
        }

        public TrailDefinition GetTrailDefinitionClone(string assetId)
        {
            return TryGetTrailDefinition(assetId, out TrailDefinition definition)
                ? CloneTrailDefinition(definition)
                : null;
        }

        public TrailDefinition GetTrailDefinitionCloneByPath(string path)
        {
            return TryGetTrailDefinitionByPath(path, out TrailDefinition definition)
                ? CloneTrailDefinition(definition)
                : null;
        }

        public void SetTrailDefinition(string path, TrailDefinition definition)
        {
            TrailDefinition clone = CloneTrailDefinition(definition);
            clone?.ApplyDefaults();
            SetDefinition(_trailsByPath, path, clone);
        }

        public (Dictionary<string, ReanimationParams> Parameters, Dictionary<string, ReanimatorDefinition> Definitions) CreateReanimationRuntimeDictionaries()
        {
            Dictionary<string, ReanimationParams> parameters = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ReanimatorDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);

            if (_project?.Assets?.Reanims is null)
            {
                return (parameters, definitions);
            }

            foreach (ReanimAsset asset in _project.Assets.Reanims.Values)
            {
                if (!TryGetReanimDefinition(asset, out ReanimatorDefinition definition) ||
                    definition?.mTrackCount <= 0)
                {
                    continue;
                }

                string fullPath = ResolvePath(asset.Path);
                foreach (string alias in BuildReanimationAliases(asset))
                {
                    if (parameters.ContainsKey(alias))
                    {
                        continue;
                    }

                    parameters[alias] = new ReanimationParams(alias, $"reanim/{alias}", fullPath);
                    definitions[alias] = definition;
                }
            }

            return (parameters, definitions);
        }

        public void ApplyReanimationGlobals()
        {
            (Dictionary<string, ReanimationParams> parameters, Dictionary<string, ReanimatorDefinition> definitions) =
                CreateReanimationRuntimeDictionaries();
            ReanimatorXnaHelpers.gReanimationParamArray = parameters;
            ReanimatorXnaHelpers.gReanimatorDefArray = definitions;
        }

        private bool TryGetDefinition<T>(
            Dictionary<string, T> cache,
            string path,
            Func<string, T> loader,
            out T definition)
            where T : class
        {
            definition = null;
            string fullPath = ResolvePath(path);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return false;
            }

            string key = NormalizePath(fullPath);
            lock (_gate)
            {
                if (cache.TryGetValue(key, out definition))
                {
                    return definition is not null;
                }
            }

            T loaded;
            try
            {
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                loaded = loader(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
            {
                return false;
            }

            lock (_gate)
            {
                cache[key] = loaded;
                definition = loaded;
            }

            return definition is not null;
        }

        private void SetDefinition<T>(Dictionary<string, T> cache, string path, T definition)
            where T : class
        {
            string fullPath = ResolvePath(path);
            if (string.IsNullOrWhiteSpace(fullPath) || definition is null)
            {
                return;
            }

            lock (_gate)
            {
                cache[NormalizePath(fullPath)] = definition;
            }
        }

        private static ReanimatorDefinition LoadReanimDefinition(string fullPath)
        {
            ReanimatorDefinition definition = null;
            ReanimatorXnaHelpers.ReanimationLoadDefinition(fullPath, ref definition);
            return definition ?? new ReanimatorDefinition();
        }

        private static TodParticleDefinition LoadParticleDefinition(string fullPath)
        {
            return TodParticleGlobal.TodParticleLoadADef(out TodParticleDefinition definition, fullPath)
                ? definition
                : ParticleDefinitionUtility.CreateEmpty();
        }

        private static TrailDefinition LoadTrailDefinition(string fullPath)
        {
            using FileStream stream = File.OpenRead(fullPath);
            TrailDefinition definition = TrailReader.Decode(stream) ?? new TrailDefinition();
            definition.ApplyDefaults();
            return definition;
        }

        private string ResolvePath(string path)
        {
            return ProjectPathUtility.ResolvePath(_project, path);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').Trim();
        }

        private static IEnumerable<string> BuildReanimationAliases(ReanimAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset.Id))
            {
                yield return asset.Id.Trim();
            }

            string fileName = Path.GetFileName(asset.Path);
            string baseName = StripReanimExtension(fileName);
            if (!string.IsNullOrWhiteSpace(baseName) &&
                !string.Equals(baseName, asset.Id, StringComparison.OrdinalIgnoreCase))
            {
                yield return baseName;
            }

            if (!string.IsNullOrWhiteSpace(fileName) &&
                !string.Equals(fileName, asset.Id, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fileName, baseName, StringComparison.OrdinalIgnoreCase))
            {
                yield return fileName;
            }
        }

        private static string StripReanimExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            const string compiledSuffix = ".reanim.compiled";
            if (fileName.EndsWith(compiledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^compiledSuffix.Length];
            }

            const string reanimSuffix = ".reanim";
            if (fileName.EndsWith(reanimSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^reanimSuffix.Length];
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        public static ReanimatorDefinition CloneReanimDefinition(ReanimatorDefinition source)
        {
            if (source is null)
            {
                return null;
            }

            int trackCount = Math.Min(source.mTrackCount, source.mTracks?.Length ?? 0);
            ReanimatorDefinition clone = new()
            {
                mFPS = source.mFPS,
                mTrackCount = (short)trackCount,
                mTracks = new ReanimatorTrack[trackCount]
            };

            if (source.mTracks is not null)
            {
                for (int i = 0; i < trackCount; i++)
                {
                    ReanimatorTrack sourceTrack = source.mTracks[i];
                    if (sourceTrack is null)
                    {
                        clone.mTracks[i] = new ReanimatorTrack(string.Empty, 0);
                        continue;
                    }

                    ReanimatorTrack track = new(sourceTrack.mName ?? string.Empty, sourceTrack.mTransformCount);
                    int count = Math.Min(sourceTrack.mTransformCount, sourceTrack.mTransforms?.Length ?? 0);
                    track.mTransforms = new ReanimatorTransform[count];
                    track.mTransformCount = (short)count;
                    for (int frameIndex = 0; frameIndex < count; frameIndex++)
                    {
                        track.mTransforms[frameIndex] = sourceTrack.mTransforms[frameIndex];
                    }

                    clone.mTracks[i] = track;
                }
            }

            clone.Init();
            return clone;
        }

        public static TrailDefinition CloneTrailDefinition(TrailDefinition source)
        {
            if (source is null)
            {
                return null;
            }

            TrailDefinition clone = new()
            {
                mImage = source.mImage,
                mMaxPoints = source.mMaxPoints,
                mMinPointDistance = source.mMinPointDistance,
                mTrailFlags = source.mTrailFlags
            };
            CopyTrack(source.mWidthOverLength, clone.mWidthOverLength);
            CopyTrack(source.mAlphaOverLength, clone.mAlphaOverLength);
            CopyTrack(source.mWidthOverTime, clone.mWidthOverTime);
            CopyTrack(source.mAlphaOverTime, clone.mAlphaOverTime);
            CopyTrack(source.mTrailDuration, clone.mTrailDuration);
            return clone;
        }

        private static void CopyTrack(FloatParameterTrack source, FloatParameterTrack target)
        {
            if (target is null)
            {
                return;
            }

            if (source?.mNodes is null || source.mCountNodes <= 0)
            {
                target.mNodes = [];
                target.mCountNodes = 0;
                return;
            }

            int count = Math.Min(source.mCountNodes, source.mNodes.Length);
            target.mNodes = new FloatParameterTrackNode[count];
            target.mCountNodes = count;
            for (int i = 0; i < count; i++)
            {
                FloatParameterTrackNode node = source.mNodes[i];
                target.mNodes[i] = new FloatParameterTrackNode
                {
                    mTime = node.mTime,
                    mLowValue = node.mLowValue,
                    mHighValue = node.mHighValue,
                    mCurveType = node.mCurveType,
                    mDistribution = node.mDistribution
                };
            }
        }
    }
}
