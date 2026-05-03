using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Runtime;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Reanim;

namespace EffectViewer.Rendering
{
    public sealed class ReanimPreviewSimulation : ISeekableRenderFrameProvider, IDisposable
    {
        private const double UpdateStepSeconds = 1.0 / TodLibConstants.TICKS_PER_SECOND;
        private readonly EffectSystem _effectSystem = new();
        private readonly ProjectResourceProvider _resourceProvider;
        private readonly Dictionary<string, ReanimationParams> _reanimationParams;
        private readonly Dictionary<string, ReanimatorDefinition> _reanimationDefinitions;
        private readonly Dictionary<Reanimation, AttachedReanimationSeekState> _attachedReanimationSeekStates = [];
        private readonly string _reanimationType;
        private Reanimation _reanimation;
        private readonly float _x;
        private readonly float _y;
        private bool _isPaused;
        private bool _needsAttachmentRefresh;
        private double? _pendingAttachmentSeekElapsedSeconds;
        private double _accumulator;
        private bool _disposed;

        public IReadOnlyList<string> TrackNames { get; private set; }
        public IReadOnlyList<string> LayerTrackNames { get; private set; }
        public IReadOnlyList<string> LayerNames { get; private set; }
        public int MaxUpdateStepsPerFrame { get; set; } = 20;

        public ReanimPreviewSimulation(EffectProject project, string path, float x = 0f, float y = 0f)
        {
            _resourceProvider = new ProjectResourceProvider(project);
            ResourceHandler.SetProvider(_resourceProvider);
            _effectSystem.EffectSystemInitialize();
            (_reanimationParams, _reanimationDefinitions) = BuildProjectReanimations(project);
            ApplyProjectReanimations();
            string fullPath = ResolvePath(project, path);
            _reanimationType = ResolveReanimationType(project, path);
            _x = x;
            _y = y;
            SetDefinition(LoadDefinition(fullPath));
        }

        public RenderFrame GetFrame(double deltaSeconds)
        {
            if (_disposed)
            {
                return new RenderFrame();
            }

            ResourceHandler.SetProvider(_resourceProvider);
            ApplyProjectReanimations();
            if (_needsAttachmentRefresh)
            {
                RefreshAttachmentsForPendingSeek();
            }

            if (!_isPaused)
            {
                _accumulator += deltaSeconds;
                int guard = 0;
                int maxUpdateSteps = Math.Max(1, MaxUpdateStepsPerFrame);
                while (_accumulator >= UpdateStepSeconds && guard++ < maxUpdateSteps)
                {
                    Update();
                    _accumulator -= UpdateStepSeconds;
                }
            }

            return BuildFrame();
        }

        public RenderFrame GetFrameAtTime(double elapsedSeconds)
        {
            if (_disposed)
            {
                return new RenderFrame();
            }

            ResourceHandler.SetProvider(_resourceProvider);
            ApplyProjectReanimations();
            SetReanimationElapsedTime(_reanimation, elapsedSeconds);
            RefreshAttachmentsForSeek(_reanimation, elapsedSeconds, 0);
            _effectSystem.ProcessDeleteQueue();
            _pendingAttachmentSeekElapsedSeconds = null;
            _needsAttachmentRefresh = false;

            return BuildFrame();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _effectSystem.EffectSystemDispose();
            _reanimation = null;
        }

        public void SetDefinition(ReanimatorDefinition definition)
        {
            if (_disposed)
            {
                return;
            }

            ApplyProjectReanimations();
            _effectSystem.EffectSystemFreeAll();
            _reanimation = _effectSystem.mReanimationHolder.mReanimations.DataArrayAlloc();
            _reanimation.mReanimationHolder = _effectSystem.mReanimationHolder;
            InitializeReanimation(definition ?? new ReanimatorDefinition());
            TrackNames = BuildTrackNames(_reanimation.mDefinition);
            LayerTrackNames = BuildLayerTrackNames(_reanimation.mDefinition);
            LayerNames = BuildLayerNames(LayerTrackNames);
            _attachedReanimationSeekStates.Clear();
            _pendingAttachmentSeekElapsedSeconds = null;
            _accumulator = 0d;
            _needsAttachmentRefresh = true;
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
            _accumulator = 0d;
            _needsAttachmentRefresh = true;
        }

        public void SetAnimRate(float animRate)
        {
            if (_reanimation is null)
            {
                return;
            }

            _reanimation.mAnimRate = animRate;
            _needsAttachmentRefresh = true;
        }

        public void SetFrameIndex(int frameIndex)
        {
            if (_reanimation is null || _reanimation.mFrameCount <= 0)
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
            _pendingAttachmentSeekElapsedSeconds = GetElapsedSecondsForFrame(_reanimation, clampedFrame);
            _needsAttachmentRefresh = true;
        }

        public void SetLayer(string trackName)
        {
            if (_reanimation?.mDefinition?.mTrackCount <= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(trackName))
            {
                SetFullTimeline();
                _needsAttachmentRefresh = true;
                return;
            }

            if (!_reanimation.TrackExists(trackName))
            {
                return;
            }

            _reanimation.mDead = false;
            _reanimation.mLoopType = ReanimLoopType.Loop;
            _reanimation.SetFramesForLayer(trackName);
            _needsAttachmentRefresh = true;
        }

        public void SetTrackVisible(int trackIndex, bool visible)
        {
            if (_reanimation?.mTrackInstances is null ||
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
            if (_reanimation?.mTrackInstances is null)
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
            if (_reanimation is null || _reanimation.mFrameCount == 0 || _reanimation.mDead)
            {
                return;
            }

            _effectSystem.Update();
            _effectSystem.ProcessDeleteQueue();
            _needsAttachmentRefresh = false;
        }

        private void RefreshAttachments()
        {
            if (_reanimation is null ||
                _reanimation.mFrameCount == 0 ||
                _reanimation.mDead ||
                _reanimation.mTrackInstances is null)
            {
                return;
            }

            for (int i = 0; i < _reanimation.mTrackInstances.Length; i++)
            {
                ref ReanimatorTrackInstance track = ref _reanimation.mTrackInstances[i];
                track.mBlendCounter = 0;
                if (track.mIsAttacher)
                {
                    _reanimation.UpdateAttacherTrack(i);
                }

                if (track.mAttachmentID != AttachmentID.Null)
                {
                    _reanimation.GetAttachmentOverlayMatrix(i, out Matrix4x4 matrix);
                    Attachment attachment = _effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(track.mAttachmentID);
                    if (attachment != null)
                    {
                        attachment.SetMatrix(matrix);
                    }
                    else
                    {
                        track.mAttachmentID = AttachmentID.Null;
                    }
                }
            }

            _effectSystem.ProcessDeleteQueue();
            _reanimation.mLastFrameTime = _reanimation.mAnimTime;
            _needsAttachmentRefresh = false;
        }

        private void RefreshAttachmentsForPendingSeek()
        {
            if (_pendingAttachmentSeekElapsedSeconds is not double elapsedSeconds)
            {
                RefreshAttachments();
                return;
            }

            RefreshAttachmentsForSeek(_reanimation, elapsedSeconds, 0);
            _effectSystem.ProcessDeleteQueue();
            _pendingAttachmentSeekElapsedSeconds = null;
            _needsAttachmentRefresh = false;
        }

        private void SetReanimationElapsedTime(Reanimation reanimation, double elapsedSeconds)
        {
            if (reanimation is null || reanimation.mFrameCount <= 0)
            {
                return;
            }

            float animRate = reanimation.mAnimRate;
            if (!float.IsFinite(animRate) || animRate == 0f)
            {
                reanimation.mAnimTime = animRate < 0f ? 0.9999999f : 0f;
            }
            else
            {
                int timelineFrameCount = reanimation.mLoopType is ReanimLoopType.PlayOnceFullLastFrame
                    or ReanimLoopType.LoopFullLastFrame
                    or ReanimLoopType.PlayOnceFullLastFrameAndHold
                    ? reanimation.mFrameCount
                    : reanimation.mFrameCount - 1;
                timelineFrameCount = Math.Max(1, timelineFrameCount);
                double rawAnimTime = Math.Max(0d, elapsedSeconds) * animRate / timelineFrameCount;
                double animTime = rawAnimTime;
                if (reanimation.mLoopType is ReanimLoopType.Loop or ReanimLoopType.LoopFullLastFrame)
                {
                    animTime -= Math.Floor(animTime);
                    if (animTime <= 0d && rawAnimTime > 0d)
                    {
                        animTime = 0.9999999d;
                    }
                }
                else if (animRate > 0f)
                {
                    animTime = Math.Clamp(animTime, 0d, 1d);
                }
                else
                {
                    animTime = 1d - Math.Clamp(-animTime, 0d, 1d);
                }

                reanimation.mAnimTime = (float)Math.Clamp(animTime, 0d, 0.9999999d);
            }

            reanimation.mLastFrameTime = -1f;
            reanimation.mDead = false;
            _accumulator = 0d;
            _needsAttachmentRefresh = true;
        }

        private void RefreshAttachmentsForSeek(Reanimation reanimation, double elapsedSeconds, int depth)
        {
            if (reanimation?.mTrackInstances is null ||
                reanimation.mFrameCount == 0 ||
                reanimation.mDead ||
                depth > 16)
            {
                return;
            }

            for (int i = 0; i < reanimation.mTrackInstances.Length; i++)
            {
                ref ReanimatorTrackInstance track = ref reanimation.mTrackInstances[i];
                track.mBlendCounter = 0;
                double? attacherStateStartElapsedSeconds = null;
                if (track.mIsAttacher)
                {
                    attacherStateStartElapsedSeconds = GetAttacherStateStartElapsedSeconds(reanimation, i, elapsedSeconds);
                    reanimation.UpdateAttacherTrack(i);
                }

                if (track.mAttachmentID == AttachmentID.Null)
                {
                    continue;
                }

                reanimation.GetAttachmentOverlayMatrix(i, out Matrix4x4 matrix);
                Attachment attachment = _effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet(track.mAttachmentID);
                if (attachment is null)
                {
                    track.mAttachmentID = AttachmentID.Null;
                    continue;
                }

                attachment.SetMatrix(matrix);
                if (attacherStateStartElapsedSeconds is double startElapsedSeconds)
                {
                    Reanimation child = GlobalMembersAttachment.FindReanimAttachment(_effectSystem, track.mAttachmentID);
                    SetAttachedReanimationSeekState(child, startElapsedSeconds);
                }

                SeekAttachmentEffects(attachment, elapsedSeconds, depth + 1);
            }

            reanimation.mLastFrameTime = reanimation.mAnimTime;
        }

        private void SeekAttachmentEffects(Attachment attachment, double parentElapsedSeconds, int depth)
        {
            if (attachment is null || depth > 16)
            {
                return;
            }

            for (int i = 0; i < attachment.mNumEffects; i++)
            {
                ref readonly AttachEffect attachEffect = ref attachment.mEffectArray[i];
                switch (attachEffect.mEffectType)
                {
                    case EffectType.Reanim:
                    {
                        Reanimation child = _effectSystem.mReanimationHolder.mReanimations.DataArrayTryToGet((ReanimationID)attachEffect.mEffectID);
                        if (child is null || child.mDead)
                        {
                            break;
                        }

                        double childElapsedSeconds = GetAttachedReanimationElapsedSeconds(child, parentElapsedSeconds);
                        SetReanimationElapsedTime(child, childElapsedSeconds);
                        RefreshAttachmentsForSeek(child, childElapsedSeconds, depth + 1);
                        break;
                    }

                    case EffectType.Attachment:
                    {
                        Attachment childAttachment = _effectSystem.mAttachmentHolder.mAttachments.DataArrayTryToGet((AttachmentID)attachEffect.mEffectID);
                        SeekAttachmentEffects(childAttachment, parentElapsedSeconds, depth + 1);
                        break;
                    }
                }
            }
        }

        private double GetAttachedReanimationElapsedSeconds(Reanimation reanimation, double parentElapsedSeconds)
        {
            if (!_attachedReanimationSeekStates.TryGetValue(reanimation, out AttachedReanimationSeekState state) ||
                state.HasFrameRangeChanged(reanimation))
            {
                state = new AttachedReanimationSeekState(reanimation, parentElapsedSeconds);
                _attachedReanimationSeekStates[reanimation] = state;
            }

            return Math.Max(0d, parentElapsedSeconds - state.StartElapsedSeconds);
        }

        private void SetAttachedReanimationSeekState(Reanimation reanimation, double startElapsedSeconds)
        {
            if (reanimation is null)
            {
                return;
            }

            _attachedReanimationSeekStates[reanimation] = new AttachedReanimationSeekState(
                reanimation,
                Math.Max(0d, startElapsedSeconds));
        }

        private double GetAttacherStateStartElapsedSeconds(Reanimation reanimation, int trackIndex, double elapsedSeconds)
        {
            if (reanimation?.mDefinition?.mTracks is null ||
                trackIndex < 0 ||
                trackIndex >= reanimation.mDefinition.mTrackCount ||
                reanimation.mFrameCount <= 0)
            {
                return elapsedSeconds;
            }

            ReanimatorTrack track = reanimation.mDefinition.mTracks[trackIndex];
            if (track?.mTransforms is null || track.mTransformCount <= 0)
            {
                return elapsedSeconds;
            }

            reanimation.GetFrameTime(out ReanimatorFrameTime frameTime);
            int firstFrame = Math.Clamp(reanimation.mFrameStart, 0, track.mTransformCount - 1);
            int lastFrame = Math.Clamp(reanimation.mFrameStart + reanimation.mFrameCount - 1, firstFrame, track.mTransformCount - 1);
            int currentFrame = Math.Clamp(frameTime.mAnimFrameBeforeInt, firstFrame, lastFrame);
            string currentKey = GetAttacherKey(track, currentFrame);
            if (string.IsNullOrEmpty(currentKey))
            {
                return elapsedSeconds;
            }

            int startFrame = currentFrame;
            for (int frame = currentFrame - 1; frame >= firstFrame; frame--)
            {
                if (!string.Equals(GetAttacherKey(track, frame), currentKey, StringComparison.Ordinal))
                {
                    break;
                }

                startFrame = frame;
            }

            double currentLocalElapsedSeconds = GetFrameOffsetSeconds(
                reanimation,
                currentFrame + Math.Clamp(frameTime.mFraction, 0f, 1f));
            double startLocalElapsedSeconds = GetFrameOffsetSeconds(reanimation, startFrame);
            return Math.Max(0d, elapsedSeconds - currentLocalElapsedSeconds + startLocalElapsedSeconds);
        }

        private static string GetAttacherKey(ReanimatorTrack track, int frameIndex)
        {
            if (track?.mTransforms is null ||
                frameIndex < 0 ||
                frameIndex >= track.mTransformCount)
            {
                return string.Empty;
            }

            ReanimatorTransform transform = track.mTransforms[frameIndex];
            return transform.mFrame < 0f ? string.Empty : transform.mText ?? string.Empty;
        }

        private static double GetElapsedSecondsForFrame(Reanimation reanimation, int frameIndex)
        {
            return GetFrameOffsetSeconds(reanimation, frameIndex);
        }

        private static double GetFrameOffsetSeconds(Reanimation reanimation, double frame)
        {
            if (reanimation is null)
            {
                return 0d;
            }

            double framesPerSecond = Math.Abs(reanimation.mAnimRate);
            if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0d)
            {
                framesPerSecond = Math.Abs(reanimation.mDefinition?.mFPS ?? 0f);
            }

            if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0d)
            {
                framesPerSecond = 12d;
            }

            return Math.Max(0d, frame - reanimation.mFrameStart) / framesPerSecond;
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

            _reanimation?.Draw(graphics);
            return graphics.Frame;
        }

        private static ReanimatorDefinition LoadDefinition(string fullPath)
        {
            ReanimatorDefinition definition = null;
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                try
                {
                    ReanimatorXnaHelpers.ReanimationLoadDefinition(fullPath, ref definition);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
                {
                }
            }

            return definition ?? new ReanimatorDefinition();
        }

        private void InitializeReanimation(ReanimatorDefinition definition)
        {
            _reanimation.Reset();
            _reanimation.mReanimationHolder = _effectSystem.mReanimationHolder;
            _reanimation.mReanimationType = _reanimationType;
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
                    string trackName = definition.mTracks[i].mName;
                    _reanimation.mTrackInstances[i].mIsAttacher =
                        ReanimatorXnaHelpers.gReanimationParamArray != null &&
                        !string.IsNullOrEmpty(trackName) &&
                        trackName.StartsWith(Reanimation.Attacher, StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                _reanimation.mFrameCount = 0;
                _reanimation.mTrackInstances = [];
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

        private void ApplyProjectReanimations()
        {
            ReanimatorXnaHelpers.gReanimationParamArray = _reanimationParams;
            ReanimatorXnaHelpers.gReanimatorDefArray = _reanimationDefinitions;
        }

        private static (Dictionary<string, ReanimationParams> Parameters, Dictionary<string, ReanimatorDefinition> Definitions) BuildProjectReanimations(EffectProject project)
        {
            Dictionary<string, ReanimationParams> parameters = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ReanimatorDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);

            if (project?.Assets?.Reanims is not null)
            {
                foreach ((_, ReanimAsset asset) in project.Assets.Reanims)
                {
                    ReanimatorDefinition definition = LoadDefinition(ResolvePath(project, asset.Path));
                    if (definition.mTrackCount <= 0)
                    {
                        continue;
                    }

                    foreach (string alias in BuildReanimationAliases(asset))
                    {
                        if (parameters.ContainsKey(alias))
                        {
                            continue;
                        }

                        parameters[alias] = new ReanimationParams(alias, $"reanim/{alias}");
                        definitions[alias] = definition;
                    }
                }
            }

            return (parameters, definitions);
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

        private static string ResolveReanimationType(EffectProject project, string path)
        {
            if (project?.Assets?.Reanims is not null)
            {
                foreach ((_, ReanimAsset asset) in project.Assets.Reanims)
                {
                    if (PathsEqual(asset.Path, path))
                    {
                        return asset.Id;
                    }
                }
            }

            return StripReanimExtension(Path.GetFileName(path));
        }

        private static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = (left ?? string.Empty).Replace('\\', '/').Trim();
            string normalizedRight = (right ?? string.Empty).Replace('\\', '/').Trim();
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class AttachedReanimationSeekState
        {
            private readonly string _reanimationType;
            private readonly int _frameStart;
            private readonly int _frameCount;

            public AttachedReanimationSeekState(Reanimation reanimation, double startElapsedSeconds)
            {
                _reanimationType = reanimation?.mReanimationType ?? string.Empty;
                _frameStart = reanimation?.mFrameStart ?? 0;
                _frameCount = reanimation?.mFrameCount ?? 0;
                StartElapsedSeconds = startElapsedSeconds;
            }

            public double StartElapsedSeconds { get; }

            public bool HasFrameRangeChanged(Reanimation reanimation)
            {
                return reanimation is null ||
                    !string.Equals(_reanimationType, reanimation.mReanimationType, StringComparison.Ordinal) ||
                    _frameStart != reanimation.mFrameStart ||
                    _frameCount != reanimation.mFrameCount;
            }
        }
    }
}
