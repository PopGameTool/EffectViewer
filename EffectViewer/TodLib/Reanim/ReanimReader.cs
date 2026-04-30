using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EffectViewer.TodLib.Reanim
{
    public class ReanimReader
    {
        public static ReanimatorDefinition Decode(Stream stream)
        {
            ReanimatorDefinition reanim = new();
            XmlReaderSettings settings = new()
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            };
            string text;
            using (StreamReader sr = new(stream))
            {
                text = sr.ReadToEnd().Replace("&", "&amp;");
            }

            ReanimScaleType doScale = ReanimScaleType.ScaleFromPC;
            List<ReanimatorTrack> aTotalTracks = [];

            using (XmlReader reader = XmlReader.Create(new StringReader(text), settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "doScale")
                        {
                            doScale = (ReanimScaleType)(byte)sbyte.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                        }
                        else if (reader.Name == "fps")
                        {
                            reanim.mFPS = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                        }
                        else if (reader.Name == "track")
                        {
                            aTotalTracks.Add(ReadReanimTrackFromXml(reader));
                        }
                    }
                }
            }

            for (int i = 0; i < aTotalTracks.Count; i++)
            {
                ReanimatorTrack track = aTotalTracks[i];
                bool isGround = ReanimatorXnaHelpers.ReanimatorTrackNameToId(track.mName) == Reanimation.ReanimTrackId__ground;
                for (int j = 0; j < track.mTransformCount; j++)
                {
                    if (!isGround)
                    {
                        ReanimatorTransform transform = track.mTransforms[j];
                        if (doScale == ReanimScaleType.InvertAndScale)
                        {
                            transform.mTransX = transform.mTransX == ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER
                                ? transform.mTransX
                                : transform.mTransX * 1.875f;
                            transform.mTransY = transform.mTransY == ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER
                                ? transform.mTransY
                                : transform.mTransY * 1.875f;
                        }
                    }
                }
            }

            reanim.mTrackCount = (short)aTotalTracks.Count;
            reanim.mTracks = [.. aTotalTracks];
            return reanim;
        }

        private static string ReadElementText(XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                return string.Empty;
            }

            if (!reader.Read())
            {
                throw new InvalidDataException("Unexpected end of XML element");
            }

            if (reader.NodeType != XmlNodeType.Text)
            {
                throw new InvalidDataException($"Expected text node, got {reader.NodeType}");
            }

            string value = reader.Value;

            if (!reader.Read() || reader.NodeType != XmlNodeType.EndElement)
            {
                throw new InvalidDataException("Expected end element");
            }

            return value;
        }

        private static ReanimatorTrack ReadReanimTrackFromXml(XmlReader reader)
        {
            string name = string.Empty;
            List<ReanimatorTransform> transforms = new List<ReanimatorTransform>();

            if (!reader.IsEmptyElement)
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        break;
                    }
                    else if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "name")
                        {
                            name = Reanimation.ToLower(ReadElementText(reader));
                        }
                        else if (reader.Name == "t")
                        {
                            transforms.Add(ReadReanimTransformFromXml(reader));
                        }
                    }
                }
            }

            ReanimatorTrack track = new ReanimatorTrack(name, transforms.Count);
            for (int i = 0; i < transforms.Count; i++)
            {
                track.mTransforms[i] = transforms[i];
            }

            return track;
        }

        private static ReanimatorTransform ReadReanimTransformFromXml(XmlReader reader)
        {
            ReanimatorTransform transform = new ReanimatorTransform();
            if (!reader.IsEmptyElement)
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        break;
                    }
                    else if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                        case "x":
                            transform.mTransX = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "y":
                            transform.mTransY = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "kx":
                            transform.mSkewX = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "ky":
                            transform.mSkewY = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "sx":
                            transform.mScaleX = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "sy":
                            transform.mScaleY = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "f":
                            transform.mFrame = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "a":
                            transform.mAlpha = float.Parse(ReadElementText(reader), CultureInfo.InvariantCulture);
                            break;
                        case "i":
                            transform.mImage = ReadElementText(reader);
                            break;
                        case "font":
                            transform.mFont = ReadElementText(reader);
                            break;
                        case "text":
                            transform.mText = ReadElementText(reader);
                            break;
                        }
                    }
                }
            }

            return transform;
        }

        internal enum ReanimScaleType
        {
            NoScale,
            InvertAndScale,
            ScaleFromPC = 0xFF
        }
    }
}