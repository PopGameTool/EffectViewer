using System;
using System.Collections.Generic;

namespace EffectViewer.EffectRuntime.Filter
{
    public class FilterEffect
    {
        public static List<Dictionary<Image, Image>> gFilterMap = new();

        public static void FilterEffectInitForApp()
        {
            for (int i = 0; i < (int)FilterEffectType.FilterEffectCount; i++)
            {
                gFilterMap.Add(new Dictionary<Image, Image>());
            }
        }

        public static void FilterEffectDisposeForApp()
        {
            gFilterMap = null;
        }

        public static bool FilterEffectInitTexture(Image image, FilterEffectType theFilterEffect)
        {
            if (!gFilterMap[(int)theFilterEffect].ContainsKey(image))
            {
                gFilterMap[(int)theFilterEffect][image] = FilterEffectCreateTexture(image, theFilterEffect);
                return true;
            }
            return false;
        }

        public static Image FilterEffectGetImage(Image theImage, FilterEffectType theFilterEffect)
        {
            if (theFilterEffect == FilterEffectType.None)
            {
                return theImage;
            }

            FilterEffectInitTexture(theImage, theFilterEffect);
            return gFilterMap[(int)theFilterEffect][theImage];
        }

        private static Image FilterEffectCreateTexture(Image theTexture, FilterEffectType theFilterEffect)
        {
            return new();
        }

        private static void FilterEffectDoWashedOut(Image theImage)
        {
            FilterEffectDoLumSat(theImage, 1.8f, 0.2f);
        }

        private static void FilterEffectDoLessWashedOut(Image theImage)
        {
            FilterEffectDoLumSat(theImage, 1.2f, 0.3f);
        }

        private static void FilterEffectDoWhite(Image theImage)
        {

        }

        private static void FilterEffectDoLumSat(Image theImage, float aLum, float aSat)
        {

        }

        private static void RgbToHsl(float r, float g, float b, out float h, out float s, out float l)
        {
            float num = Math.Max(r, g);
            num = Math.Max(num, b);
            float num2 = Math.Min(r, g);
            num2 = Math.Min(num2, b);
            h = l = s = 0f;
            if ((l = (num2 + num) / 2f) <= 0f)
            {
                return;
            }

            float num3;
            if ((s = num3 = num - num2) > 0f)
            {
                s /= l <= 0.5f ? num + num2 : 2f - num - num2;
                float num4 = (num - r) / num3;
                float num5 = (num - g) / num3;
                float num6 = (num - b) / num3;
                if (r == num)
                {
                    h = g == num2 ? 5f + num6 : 1f - num5;
                }
                else if (g == num)
                {
                    h = b == num2 ? 1f + num4 : 3f - num6;
                }
                else
                {
                    h = r == num2 ? 3f + num5 : 5f - num4;
                }

                h /= 6f;
            }
        }

        private static void HslToRgb(float h, float sl, float l, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            float num = l <= 0.5f ? l * (1f + sl) : l + sl - (l * sl);
            if (num <= 0f)
            {
                r = g = b = 0f;
                return;
            }

            float num2 = l + l - num;
            float num3 = (num - num2) / num;
            h *= 6f;
            int num4 = EffectUtility.ClampInt((int)h, 0, 5);
            float num5 = h - num4;
            float num6 = num * num3 * num5;
            float num7 = num2 + num6;
            float num8 = num - num6;
            switch (num4)
            {
            case 0:
                r = num;
                g = num7;
                b = num2;
                return;
            case 1:
                r = num8;
                g = num;
                b = num2;
                return;
            case 2:
                r = num2;
                g = num;
                b = num7;
                return;
            case 3:
                r = num2;
                g = num8;
                b = num;
                return;
            case 4:
                r = num7;
                g = num2;
                b = num;
                return;
            case 5:
                r = num;
                g = num2;
                b = num8;
                return;
            default:
                return;
            }
        }
    }
}