using System.Globalization;
using System.IO;
using System.Xml;

namespace EffectViewer.TodLib.Trail
{
    internal class TrailReader
    {
        public static TrailDefinition Decode(Stream stream)
        {
            TrailDefinition trail = new();
            string text;
            using (StreamReader sr = new(stream))
            {
                text = ("<?xml version=\"1.0\" encoding=\"utf-8\"?><root>" + sr.ReadToEnd().Replace("&", "&amp;") + "</root>");
            }
            XmlDocument aXmlDocument = new();
            aXmlDocument.LoadXml(text);
            XmlNode aRoot = aXmlDocument.SelectSingleNode("/root");
            foreach (XmlNode node in aRoot.ChildNodes)
            {
                switch (node.Name)
                {
                case "MaxPoints":
                    trail.mMaxPoints = int.Parse(node.InnerText, CultureInfo.InvariantCulture);
                    break;
                case "MinPointDistance":
                    trail.mMinPointDistance = float.Parse(node.InnerText, CultureInfo.InvariantCulture);
                    break;
                case "Loops":
                    SexyParticleReader.SetBit(ref trail.mTrailFlags, 0, node.InnerText == "1");
                    break;
                case "Image":
                    trail.mImage = node.InnerText;
                    break;
                case "WidthOverLength":
                    SexyParticleReader.ReadTrackNode(node.InnerText, trail.mWidthOverLength);
                    break;
                case "WidthOverTime":
                    SexyParticleReader.ReadTrackNode(node.InnerText, trail.mWidthOverTime);
                    break;
                case "AlphaOverLength":
                    SexyParticleReader.ReadTrackNode(node.InnerText, trail.mAlphaOverLength);
                    break;
                case "AlphaOverTime":
                    SexyParticleReader.ReadTrackNode(node.InnerText, trail.mAlphaOverTime);
                    break;
                case "TrailDuration":
                    SexyParticleReader.ReadTrackNode(node.InnerText, trail.mTrailDuration);
                    break;
                }
            }
            return trail;
        }
    }
}
