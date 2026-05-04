using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using EffectViewer.Projects;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Rendering
{
    public static class TrailPreviewFrameBuilder
    {
        public static RenderFrame Build(EffectProject project, string path, string fallbackId)
        {
            string fullPath = ResolvePath(project, path);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Trail, fallbackId);
            }

            TrailDefinition definition = project?.Definitions?.TryGetTrailDefinitionByPath(path, out TrailDefinition cached) == true
                ? cached
                : LoadDefinition(fullPath);
            return Build(definition, fallbackId);
        }

        public static RenderFrame Build(TrailDefinition definition, string fallbackId)
        {
            if (definition is null)
            {
                return EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Trail, fallbackId);
            }

            string textureId = string.IsNullOrWhiteSpace(definition.mImage) ? fallbackId : definition.mImage;

            RenderFrame frame = new();
            List<Vector2> points = BuildPath(Math.Clamp(definition.mMaxPoints, 4, 20));
            List<RenderVertex> vertices = BuildMesh(points, definition, 0.45f);
            if (vertices.Count > 0)
            {
                frame.Meshes.Add(new RenderMeshCommand(new RenderTextureRef(textureId), vertices, RenderBlendMode.Normal));
            }

            return frame;
        }

        public static TrailDefinition LoadDefinition(string fullPath)
        {
            using FileStream stream = File.OpenRead(fullPath);
            TrailDefinition definition = TrailReader.Decode(stream);
            definition.ApplyDefaults();
            return definition;
        }

        public static List<Vector2> BuildPath(int count)
        {
            List<Vector2> points = new(count);
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : i / (float)(count - 1);
                float x = 140f + t * 560f;
                float y = 270f + MathF.Sin(t * MathF.PI * 2.2f) * 90f;
                points.Add(new Vector2(x, y));
            }

            return points;
        }

        public static List<RenderVertex> BuildMesh(IReadOnlyList<Vector2> points, TrailDefinition definition, float timeValue)
        {
            List<RenderVertex> vertices = [];
            if (points.Count < 2)
            {
                return vertices;
            }

            Vector2 previousNormal = default;
            bool havePreviousNormal = false;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (!havePreviousNormal)
                {
                    previousNormal = GetNormal(points, i);
                    havePreviousNormal = true;
                }

                Vector2 currentNormal = previousNormal;
                Vector2 nextNormal = GetNormal(points, i + 1);
                previousNormal = nextNormal;

                float u0 = 1f - i / (float)(points.Count - 1);
                float u1 = 1f - (i + 1) / (float)(points.Count - 1);
                float width0 = Evaluate(definition.mWidthOverLength, u0, 28f) * Evaluate(definition.mWidthOverTime, timeValue, 1f);
                float width1 = Evaluate(definition.mWidthOverLength, u1, 28f) * Evaluate(definition.mWidthOverTime, timeValue, 1f);
                float alpha0 = Math.Clamp(Evaluate(definition.mAlphaOverLength, u0, 1f) * Evaluate(definition.mAlphaOverTime, timeValue, 1f), 0f, 1f);
                float alpha1 = Math.Clamp(Evaluate(definition.mAlphaOverLength, u1, 1f) * Evaluate(definition.mAlphaOverTime, timeValue, 1f), 0f, 1f);

                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];
                Vector2 p0Top = p0 + currentNormal * width0;
                Vector2 p0Bottom = p0 - currentNormal * width0;
                Vector2 p1Top = p1 + nextNormal * width1;
                Vector2 p1Bottom = p1 - nextNormal * width1;
                Vector4 c0 = new(1f, 1f, 1f, alpha0);
                Vector4 c1 = new(1f, 1f, 1f, alpha1);

                vertices.Add(new RenderVertex(p0Top, new Vector2(u0, 1f), c0));
                vertices.Add(new RenderVertex(p0Bottom, new Vector2(u0, 0f), c0));
                vertices.Add(new RenderVertex(p1Top, new Vector2(u1, 1f), c1));

                vertices.Add(new RenderVertex(p1Top, new Vector2(u1, 1f), c1));
                vertices.Add(new RenderVertex(p0Bottom, new Vector2(u0, 0f), c0));
                vertices.Add(new RenderVertex(p1Bottom, new Vector2(u1, 0f), c1));
            }

            return vertices;
        }

        public static List<RenderVertex> BuildMesh(Trail trail)
        {
            List<RenderVertex> vertices = [];
            if (trail is null || trail.mDead || trail.mNumTrailPoints < 2 || trail.mDefinition is null)
            {
                return vertices;
            }

            float timeValue = trail.mTrailDuration <= 1
                ? 0f
                : trail.mTrailAge / (float)(trail.mTrailDuration - 1);

            Vector2 previousNormal = default;
            bool havePreviousNormal = false;
            for (int i = 0; i < trail.mNumTrailPoints - 1; i++)
            {
                if (!havePreviousNormal)
                {
                    if (!trail.GetNormalAtPoint(i, ref previousNormal))
                    {
                        continue;
                    }

                    havePreviousNormal = true;
                }

                Vector2 currentNormal = previousNormal;
                Vector2 nextNormal = default;
                if (!trail.GetNormalAtPoint(i + 1, ref nextNormal))
                {
                    nextNormal = previousNormal;
                }
                else
                {
                    previousNormal = nextNormal;
                }

                ref TrailPoint currentPoint = ref trail.mTrailPoints[i];
                ref TrailPoint nextPoint = ref trail.mTrailPoints[i + 1];
                float currentU = 1f - i / (float)(trail.mNumTrailPoints - 1);
                float nextU = 1f - (i + 1) / (float)(trail.mNumTrailPoints - 1);
                float widthCurrent = Definition.FloatTrackEvaluate(trail.mDefinition.mWidthOverLength, currentU, trail.mTrailInterp[(int)TrailTracks.WidthOverLength]) *
                    Definition.FloatTrackEvaluate(trail.mDefinition.mWidthOverTime, timeValue, trail.mTrailInterp[(int)TrailTracks.WidthOverTime]);
                float widthNext = Definition.FloatTrackEvaluate(trail.mDefinition.mWidthOverLength, nextU, trail.mTrailInterp[(int)TrailTracks.WidthOverLength]) *
                    Definition.FloatTrackEvaluate(trail.mDefinition.mWidthOverTime, timeValue, trail.mTrailInterp[(int)TrailTracks.WidthOverTime]);
                float alphaCurrent = Math.Clamp(Definition.FloatTrackEvaluate(trail.mDefinition.mAlphaOverLength, currentU, trail.mTrailInterp[(int)TrailTracks.AlphaOverLength]) *
                    Definition.FloatTrackEvaluate(trail.mDefinition.mAlphaOverTime, timeValue, trail.mTrailInterp[(int)TrailTracks.AlphaOverTime]) *
                    (trail.mColorOverride.mAlpha / 255f), 0f, 1f);
                float alphaNext = Math.Clamp(Definition.FloatTrackEvaluate(trail.mDefinition.mAlphaOverLength, nextU, trail.mTrailInterp[(int)TrailTracks.AlphaOverLength]) *
                    Definition.FloatTrackEvaluate(trail.mDefinition.mAlphaOverTime, timeValue, trail.mTrailInterp[(int)TrailTracks.AlphaOverTime]) *
                    (trail.mColorOverride.mAlpha / 255f), 0f, 1f);

                Vector2 currentPosition = trail.mTrailCenter + currentPoint.aPos;
                Vector2 nextPosition = trail.mTrailCenter + nextPoint.aPos;
                Vector2 currentTop = currentPosition + currentNormal * widthCurrent;
                Vector2 currentBottom = currentPosition - currentNormal * widthCurrent;
                Vector2 nextTop = nextPosition + nextNormal * widthNext;
                Vector2 nextBottom = nextPosition - nextNormal * widthNext;
                Vector4 currentColor = ToVector4(trail.mColorOverride, alphaCurrent);
                Vector4 nextColor = ToVector4(trail.mColorOverride, alphaNext);

                vertices.Add(new RenderVertex(currentTop, new Vector2(currentU, 1f), currentColor));
                vertices.Add(new RenderVertex(currentBottom, new Vector2(currentU, 0f), currentColor));
                vertices.Add(new RenderVertex(nextTop, new Vector2(nextU, 1f), nextColor));

                vertices.Add(new RenderVertex(nextTop, new Vector2(nextU, 1f), nextColor));
                vertices.Add(new RenderVertex(currentBottom, new Vector2(currentU, 0f), currentColor));
                vertices.Add(new RenderVertex(nextBottom, new Vector2(nextU, 0f), nextColor));
            }

            return vertices;
        }

        private static Vector2 GetNormal(IReadOnlyList<Vector2> points, int index)
        {
            Vector2 direction;
            if (index == 0)
            {
                direction = points[1] - points[0];
            }
            else if (index == points.Count - 1)
            {
                direction = points[index] - points[index - 1];
            }
            else
            {
                direction = Vector2.Normalize(points[index] - points[index - 1]) +
                            Vector2.Normalize(points[index + 1] - points[index]);
            }

            if (direction.LengthSquared() <= 0.0001f)
            {
                return new Vector2(0f, -1f);
            }

            direction = Vector2.Normalize(direction);
            return new Vector2(-direction.Y, direction.X);
        }

        private static float Evaluate(FloatParameterTrack track, float time, float fallback)
        {
            if (track is null || track.mCountNodes == 0 || track.mNodes is null)
            {
                return fallback;
            }

            if (time <= track.mNodes[0].mTime)
            {
                return Average(track.mNodes[0].mLowValue, track.mNodes[0].mHighValue);
            }

            for (int i = 1; i < track.mCountNodes; i++)
            {
                FloatParameterTrackNode next = track.mNodes[i];
                if (time <= next.mTime)
                {
                    FloatParameterTrackNode current = track.mNodes[i - 1];
                    float span = next.mTime - current.mTime;
                    float t = span <= 0f ? 0f : (time - current.mTime) / span;
                    float left = Average(current.mLowValue, current.mHighValue);
                    float right = Average(next.mLowValue, next.mHighValue);
                    return left + (right - left) * t;
                }
            }

            FloatParameterTrackNode last = track.mNodes[track.mCountNodes - 1];
            return Average(last.mLowValue, last.mHighValue);
        }

        private static float Average(float low, float high)
        {
            return (low + high) * 0.5f;
        }

        private static Vector4 ToVector4(SexyColor color, float alpha)
        {
            return new Vector4(
                color.mRed / 255f,
                color.mGreen / 255f,
                color.mBlue / 255f,
                alpha);
        }

        public static string ResolvePath(EffectProject project, string path)
        {
            return Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(project.RootPath)
                ? path
                : Path.Combine(project.RootPath, path);
        }
    }
}
