using System;
using System.Collections.Generic;
using System.IO;
using EffectViewer.EffectRuntime.Common;

namespace EffectViewer.EffectRuntime.Reanim
{
    internal class ReanimatorUtility
    {
        public const float DEFAULT_FIELD_PLACEHOLDER = EffectConstants.DEFAULT_FIELD_PLACEHOLDER;

        public static short NO_BASE_POSE = -2;

        public static float EPSILON = 0.000001f;

        public static int RENDER_GROUP_HIDDEN = -1;

        public static int RENDER_GROUP_NORMAL = 0;

        public static float SECONDS_PER_UPDATE = 0.01f;

        public static Dictionary<string, ReanimatorDefinition> gReanimatorDefArray = new Dictionary<string, ReanimatorDefinition>();

        public static Dictionary<string, ReanimationParams> gReanimationParamArray;

        public static List<string> gReanimTrackIds = new();

        public static string ReanimatorTrackNameToId(string theName)
        {
            string text = theName.ToLowerInvariant();
            int num = gReanimTrackIds.IndexOf(text);
            if (num == -1)
            {
                gReanimTrackIds.Add(text);
            }

            return text;
        }

        public static void ReanimationFillInMissingData(ref float thePrev, ref float theValue)
        {
            if (theValue == DEFAULT_FIELD_PLACEHOLDER)
            {
                theValue = thePrev;
                return;
            }

            thePrev = theValue;
        }

        public static bool ReanimationLoadDefinition(string theFilename, ref ReanimatorDefinition theDefinition)
        {
            using (Stream fileStream = File.OpenRead(theFilename))
            {
                theDefinition = ReanimReader.Decode(fileStream);
            }

            for (int i = 0; i < theDefinition.mTrackCount; i++)
            {
                ReanimatorTrack reanimatorTrack = theDefinition.mTracks[i];
                float num2 = 0f;
                float num3 = 0f;
                float num4 = 0f;
                float num5 = 0f;
                float num6 = 1f;
                float num7 = 1f;
                float num8 = 0f;
                float num9 = 1f;
                string anImage = null;
                string anImageName = string.Empty;
                string aFont = null;
                string aText = string.Empty;
                for (int j = 0; j < reanimatorTrack.mTransformCount; j++)
                {
                    ReanimatorTransform reanimatorTransform = reanimatorTrack.mTransforms[j];
                    ReanimationFillInMissingData(ref num2, ref reanimatorTransform.mTransX);
                    ReanimationFillInMissingData(ref num3, ref reanimatorTransform.mTransY);
                    ReanimationFillInMissingData(ref num4, ref reanimatorTransform.mSkewX);
                    ReanimationFillInMissingData(ref num5, ref reanimatorTransform.mSkewY);
                    ReanimationFillInMissingData(ref num6, ref reanimatorTransform.mScaleX);
                    ReanimationFillInMissingData(ref num7, ref reanimatorTransform.mScaleY);
                    ReanimationFillInMissingData(ref num8, ref reanimatorTransform.mFrame);
                    ReanimationFillInMissingData(ref num9, ref reanimatorTransform.mAlpha);
                    if (reanimatorTransform.mImage == null)
                    {
                        reanimatorTransform.mImage = anImage;
                    }
                    else
                    {
                        anImage = reanimatorTransform.mImage;
                    }

                    if (reanimatorTransform.mFont == null)
                    {
                        reanimatorTransform.mFont = aFont;
                    }
                    else
                    {
                        aFont = reanimatorTransform.mFont;
                    }

                    if (string.IsNullOrEmpty(reanimatorTransform.mText))
                    {
                        reanimatorTransform.mText = aText;
                    }
                    else
                    {
                        aText = reanimatorTransform.mText;
                    }

                    reanimatorTrack.mTransforms[j] = reanimatorTransform;
                }
            }

            theDefinition.Init();

            return true;
        }

        public static void ReanimationFreeDefinition(ref ReanimatorDefinition theDefinition)
        {
            for (int i = 0; i < theDefinition.mTrackCount; i++)
            {
                ReanimatorTrack reanimatorTrack = theDefinition.mTracks[i];
                string text = null;
                for (int j = 0; j < reanimatorTrack.mTransformCount; j++)
                {
                    ReanimatorTransform reanimatorTransform = reanimatorTrack.mTransforms[j];
                    if (!string.IsNullOrEmpty(reanimatorTransform.mText) && reanimatorTransform.mText == text)
                    {
                        reanimatorTransform.mText = "";
                    }
                    else
                    {
                        text = reanimatorTransform.mText;
                    }

                    reanimatorTrack.mTransforms[j] = reanimatorTransform;
                }
            }
        }

        public static void ReanimatorLoadDefinitions(Dictionary<string, ReanimationParams> theReanimationParamArray)
        {
            gReanimationParamArray = theReanimationParamArray;
            gReanimatorDefArray.Clear();

            foreach ((_, ReanimationParams reanimationParams) in theReanimationParamArray)
            {
                ReanimatorEnsureDefinitionLoaded(reanimationParams.mReanimationType, true);
            }
        }

        public static void ReanimatorFreeDefinitions()
        {
            gReanimatorDefArray.Clear();
            gReanimatorDefArray = null;
            gReanimationParamArray = null;
        }

        public static void ForceReanimatorEnsureDefinitionLoaded(string theReanimType, bool theIsPreloading)
        {
            if (gReanimatorDefArray is null)
            {
                return;
            }

            gReanimatorDefArray.TryGetValue(theReanimType, out ReanimatorDefinition reanimatorDefinition);
            if (reanimatorDefinition != null && reanimatorDefinition.mTracks != null &&
                reanimatorDefinition.mTrackCount != 0)
            {
                gReanimatorDefArray.Remove(theReanimType);
            }

            ReanimatorEnsureDefinitionLoaded(theReanimType, theIsPreloading);
        }

        public static void ReanimatorEnsureDefinitionLoaded(string theReanimType, bool theIsPreloading)
        {
            if (gReanimatorDefArray is null ||
                gReanimationParamArray is null ||
                string.IsNullOrWhiteSpace(theReanimType))
            {
                return;
            }

            gReanimatorDefArray.TryGetValue(theReanimType, out ReanimatorDefinition reanimatorDefinition);
            if (reanimatorDefinition != null && reanimatorDefinition.mTracks != null &&
                reanimatorDefinition.mTrackCount != 0)
            {
                return;
            }

            if (!gReanimationParamArray.TryGetValue(theReanimType, out ReanimationParams reanimationParams))
            {
                return;
            }

            string fileName = string.IsNullOrWhiteSpace(reanimationParams.mResolvedFileName)
                ? reanimationParams.mReanimFileName
                : reanimationParams.mResolvedFileName;
            ReanimationLoadDefinition(fileName, ref reanimatorDefinition);
            gReanimatorDefArray[theReanimType] = reanimatorDefinition;
        }

        public static void BlendTransform(ref ReanimatorTransform theResult, in ReanimatorTransform theTransform1, in ReanimatorTransform theTransform2, float theBlendFactor)
        {
            // 不能out，因为调用该函数的地方传入的前两个参数是同一个结构体
            theResult.mTransX = EffectUtility.FloatLerp(theTransform1.mTransX, theTransform2.mTransX, theBlendFactor);
            theResult.mTransY = EffectUtility.FloatLerp(theTransform1.mTransY, theTransform2.mTransY, theBlendFactor);
            theResult.mScaleX = EffectUtility.FloatLerp(theTransform1.mScaleX, theTransform2.mScaleX, theBlendFactor);
            theResult.mScaleY = EffectUtility.FloatLerp(theTransform1.mScaleY, theTransform2.mScaleY, theBlendFactor);
            theResult.mAlpha = EffectUtility.FloatLerp(theTransform1.mAlpha, theTransform2.mAlpha, theBlendFactor);

            float aSkewX2 = theTransform2.mSkewX;
            float aSkewY2 = theTransform2.mSkewY;

            while (aSkewX2 > theTransform1.mSkewX + 180f)
            {
                aSkewX2 = theTransform1.mSkewX;
            }
            while (aSkewX2 < theTransform1.mSkewX - 180f)
            {
                aSkewX2 = theTransform1.mSkewX;
            }
            while (aSkewY2 > theTransform1.mSkewY + 180f)
            {
                aSkewY2 = theTransform1.mSkewY;
            }
            while (aSkewY2 < theTransform1.mSkewY - 180f)
            {
                aSkewY2 = theTransform1.mSkewY;
            }

            theResult.mSkewX = EffectUtility.FloatLerp(theTransform1.mSkewX, aSkewX2, theBlendFactor);
            theResult.mSkewY = EffectUtility.FloatLerp(theTransform1.mSkewY, aSkewY2, theBlendFactor);
            theResult.mFrame = theTransform1.mFrame;
            theResult.mFont = theTransform1.mFont;
            theResult.mText = theTransform1.mText;
            theResult.mImage = theTransform1.mImage;
        }
    }
}
