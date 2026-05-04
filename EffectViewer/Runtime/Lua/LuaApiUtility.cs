using System;
using EffectViewer.Runtime.Showcase;
using EffectViewer.TodLib.Graphics;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Lua
{
    internal static class LuaApiUtility
    {
        public static bool IsNil(DynValue value)
        {
            return value.Type is DataType.Nil or DataType.Void;
        }

        public static double NumberOr(DynValue value, double fallback)
        {
            return IsNil(value) ? fallback : value.CastToNumber() ?? fallback;
        }

        public static int IntOr(DynValue value, int fallback)
        {
            return (int)Math.Round(NumberOr(value, fallback));
        }

        public static string StringOr(DynValue value, string fallback = null)
        {
            return IsNil(value) ? fallback : value.CastToString();
        }

        public static bool BoolOr(DynValue value, bool fallback)
        {
            return IsNil(value) ? fallback : value.CastToBool();
        }

        public static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }

        public static SexyColor MergeColor(SexyColor current, DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            return new SexyColor(
                IsNil(red) ? current.mRed : ClampColor(NumberOr(red, current.mRed)),
                IsNil(green) ? current.mGreen : ClampColor(NumberOr(green, current.mGreen)),
                IsNil(blue) ? current.mBlue : ClampColor(NumberOr(blue, current.mBlue)),
                IsNil(alpha) ? current.mAlpha : ClampColor(NumberOr(alpha, current.mAlpha)));
        }

        public static DynValue ColorTuple(SexyColor color)
        {
            return DynValue.NewTuple(
                DynValue.NewNumber(color.mRed),
                DynValue.NewNumber(color.mGreen),
                DynValue.NewNumber(color.mBlue),
                DynValue.NewNumber(color.mAlpha));
        }

        public static DynValue RectangleTuple(Rectangle rect)
        {
            return DynValue.NewTuple(
                DynValue.NewNumber(rect.X),
                DynValue.NewNumber(rect.Y),
                DynValue.NewNumber(rect.Width),
                DynValue.NewNumber(rect.Height));
        }

        public static DynValue UserDataOrNil(object value)
        {
            return value is null ? DynValue.Nil : UserData.Create(value);
        }

        public static Image ImageFrom(ShowcaseImage image)
        {
            return image?.Image;
        }
    }
}
