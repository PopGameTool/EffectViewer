using System.Numerics;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseTriVertex
    {
        public ShowcaseTriVertex(
            double pos_x,
            double pos_y,
            double pos_z,
            double red,
            double green,
            double blue,
            double alpha,
            double coordinate_x,
            double coordinate_y)
        {
            this.pos_x = pos_x;
            this.pos_y = pos_y;
            this.pos_z = pos_z;
            this.red = red;
            this.green = green;
            this.blue = blue;
            this.alpha = alpha;
            this.coordinate_x = coordinate_x;
            this.coordinate_y = coordinate_y;
        }

        public double pos_x { get; set; }
        public double pos_y { get; set; }
        public double pos_z { get; set; }
        public double red { get; set; }
        public double green { get; set; }
        public double blue { get; set; }
        public double alpha { get; set; }
        public double coordinate_x { get; set; }
        public double coordinate_y { get; set; }

        internal TriVertex ToTriVertex()
        {
            return new TriVertex
            {
                Position = new Vector3((float)pos_x, (float)pos_y, (float)pos_z),
                Color = new SexyColor(
                    LuaApiUtility.ClampColor(red),
                    LuaApiUtility.ClampColor(green),
                    LuaApiUtility.ClampColor(blue),
                    LuaApiUtility.ClampColor(alpha)),
                TextureCoordinate = new Vector2((float)coordinate_x, (float)coordinate_y)
            };
        }
    }
}
