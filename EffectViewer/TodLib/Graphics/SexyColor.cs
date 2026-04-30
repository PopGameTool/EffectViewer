using Avalonia.Media;
using System;

namespace EffectViewer.TodLib.Graphics
{
    public struct SexyColor
    {
        public int mRed;
        public int mGreen;
        public int mBlue;
        public int mAlpha;

        public static SexyColor Black { get; } = new SexyColor(0, 0, 0);
        public static SexyColor White { get; } = new SexyColor(255, 255, 255);

        public SexyColor(int theColor) : this((theColor >> 16) & 0xFF, (theColor >> 8) & 0xFF, (theColor >> 0) & 0xFF, (theColor >> 24) & 0xFF)
        {

        }

        public SexyColor(int theColor, int theAlpha) : this((theColor >> 16) & 0xFF, (theColor >> 8) & 0xFF, (theColor >> 0) & 0xFF, theAlpha)
        {

        }

        public SexyColor(int theRed, int theGreen, int theBlue) : this(theRed, theGreen, theBlue, 0xFF)
        {

        }

        public SexyColor(int theRed, int theGreen, int theBlue, int theAlpha)
        {
            mRed = theRed;
            mGreen = theGreen;
            mBlue = theBlue;
            mAlpha = theAlpha;
        }

        public SexyColor(in Color theColor) : this(theColor.R, theColor.G, theColor.B, theColor.A)
        {

        }

        public SexyColor(ReadOnlySpan<byte> theElements) : this(theElements[0], theElements[1], theElements[2], 255)
        {

        }

        public SexyColor(ReadOnlySpan<int> theElements) : this(theElements[0], theElements[1], theElements[2], 255)
        {

        }

        public readonly int this[int theIdx] => theIdx switch
        {
            0 => mRed,
            1 => mGreen,
            2 => mBlue,
            3 => mAlpha,
            _ => 0,
        };

        public static bool operator ==(in SexyColor theColor1, in SexyColor theColor2)
        {
            return theColor1.mRed == theColor2.mRed &&
                theColor1.mGreen == theColor2.mGreen &&
                theColor1.mBlue == theColor2.mBlue &&
                theColor1.mAlpha == theColor2.mAlpha;
        }

        public static bool operator !=(in SexyColor theColor1, in SexyColor theColor2)
        {
            return theColor1.mRed != theColor2.mRed ||
                theColor1.mGreen != theColor2.mGreen ||
                theColor1.mBlue != theColor2.mBlue ||
                theColor1.mAlpha != theColor2.mAlpha;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is SexyColor color && this == color;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(mRed, mGreen, mBlue, mAlpha);
        }

        public static implicit operator SexyColor(in Color theColor)
        {
            return new SexyColor(theColor);
        }

        public static implicit operator Color(in SexyColor theColor)
        {
            return new Color((byte)theColor.mAlpha, (byte)theColor.mRed, (byte)theColor.mGreen, (byte)theColor.mBlue);
        }
    }
}
