using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EffectViewer.TodLib.Particle
{
    public static class SexyParticleReader
    {
        public static TodParticleDefinition Decode(Stream stream)
        {
            TodParticleDefinition particles = new();
            string text;
            using (StreamReader sr = new(stream))
            {
                text = ("<?xml version=\"1.0\" encoding=\"utf-8\"?><root>" + sr.ReadToEnd().Replace("&", "&amp;") + "</root>");
            }
            XmlDocument aXmlDocument = new();
            aXmlDocument.LoadXml(text);
            XmlNode aRoot = aXmlDocument.SelectSingleNode("/root");
            List<TodEmitterDefinition> aEmitterDefs = [];
            List<ParticleField> aTempEmitterField = [];
            List<ParticleField> aTempEmitterSystemField = [];
            foreach (XmlNode anEmitterNode in aRoot.ChildNodes)
            {
                if (anEmitterNode.Name != "Emitter")
                {
                    continue;
                }
                aTempEmitterField.Clear();
                aTempEmitterSystemField.Clear();
                TodEmitterDefinition emitter = new TodEmitterDefinition();
                foreach (XmlNode node in anEmitterNode.ChildNodes)
                {
                    switch (node.Name)
                    {
                    case "Name":
                        emitter.mName = node.InnerText;
                        break;
                    case "Image":
                        emitter.mImage = node.InnerText;
                        break;
                    case "ImageCol":
                        emitter.mImageCol = int.Parse(node.InnerText, CultureInfo.InvariantCulture);
                        break;
                    case "ImageRow":
                        emitter.mImageRow = int.Parse(node.InnerText, CultureInfo.InvariantCulture);
                        break;
                    case "ImageFrames":
                        emitter.mImageFrames = int.Parse(node.InnerText, CultureInfo.InvariantCulture);
                        break;
                    case "Animated":
                        emitter.mAnimated = int.Parse(node.InnerText, CultureInfo.InvariantCulture);
                        break;
                    case "RandomLaunchSpin":
                        SetBit(ref emitter.mParticleFlags, 0, node.InnerText == "1");
                        break;
                    case "AlignLaunchSpin":
                        SetBit(ref emitter.mParticleFlags, 1, node.InnerText == "1");
                        break;
                    case "AlignToPixel":
                        SetBit(ref emitter.mParticleFlags, 2, node.InnerText == "1");
                        break;
                    case "SystemLoops":
                        SetBit(ref emitter.mParticleFlags, 3, node.InnerText == "1");
                        break;
                    case "ParticleLoops":
                        SetBit(ref emitter.mParticleFlags, 4, node.InnerText == "1");
                        break;
                    case "ParticlesDontFollow":
                        SetBit(ref emitter.mParticleFlags, 5, node.InnerText == "1");
                        break;
                    case "RandomStartTime":
                        SetBit(ref emitter.mParticleFlags, 6, node.InnerText == "1");
                        break;
                    case "DieIfOverloaded":
                        SetBit(ref emitter.mParticleFlags, 7, node.InnerText == "1");
                        break;
                    case "Additive":
                        SetBit(ref emitter.mParticleFlags, 8, node.InnerText == "1");
                        break;
                    case "FullScreen":
                        SetBit(ref emitter.mParticleFlags, 9, node.InnerText == "1");
                        break;
                    case "SoftwareOnly":
                        SetBit(ref emitter.mParticleFlags, 10, node.InnerText == "1");
                        break;
                    case "HardwareOnly":
                        SetBit(ref emitter.mParticleFlags, 11, node.InnerText == "1");
                        break;
                    case "EmitterType":
                        emitter.mEmitterType = Enum.Parse<EmitterType>(node.InnerText, true);
                        break;
                    case "OnDuration":
                        emitter.mOnDuration = node.InnerText;
                        break;
                    case "SystemDuration":
                        ReadTrackNode(node.InnerText, emitter.mSystemDuration);
                        break;
                    case "CrossFadeDuration":
                        ReadTrackNode(node.InnerText, emitter.mCrossFadeDuration);
                        break;
                    case "SpawnRate":
                        ReadTrackNode(node.InnerText, emitter.mSpawnRate);
                        break;
                    case "SpawnMinActive":
                        ReadTrackNode(node.InnerText, emitter.mSpawnMinActive);
                        break;
                    case "SpawnMaxActive":
                        ReadTrackNode(node.InnerText, emitter.mSpawnMaxActive);
                        break;
                    case "SpawnMaxLaunched":
                        ReadTrackNode(node.InnerText, emitter.mSpawnMaxLaunched);
                        break;
                    case "EmitterRadius":
                        ReadTrackNode(node.InnerText, emitter.mEmitterRadius);
                        break;
                    case "EmitterOffsetX":
                        ReadTrackNode(node.InnerText, emitter.mEmitterOffsetX);
                        break;
                    case "EmitterOffsetY":
                        ReadTrackNode(node.InnerText, emitter.mEmitterOffsetY);
                        break;
                    case "EmitterBoxX":
                        ReadTrackNode(node.InnerText, emitter.mEmitterBoxX);
                        break;
                    case "EmitterBoxY":
                        ReadTrackNode(node.InnerText, emitter.mEmitterBoxY);
                        break;
                    case "EmitterPath":
                        ReadTrackNode(node.InnerText, emitter.mEmitterPath);
                        break;
                    case "EmitterSkewX":
                        ReadTrackNode(node.InnerText, emitter.mEmitterSkewX);
                        break;
                    case "EmitterSkewY":
                        ReadTrackNode(node.InnerText, emitter.mEmitterSkewY);
                        break;
                    case "ParticleDuration":
                        ReadTrackNode(node.InnerText, emitter.mParticleDuration);
                        break;
                    case "SystemRed":
                        ReadTrackNode(node.InnerText, emitter.mSystemRed);
                        break;
                    case "SystemGreen":
                        ReadTrackNode(node.InnerText, emitter.mSystemGreen);
                        break;
                    case "SystemBlue":
                        ReadTrackNode(node.InnerText, emitter.mSystemBlue);
                        break;
                    case "SystemAlpha":
                        ReadTrackNode(node.InnerText, emitter.mSystemAlpha);
                        break;
                    case "SystemBrightness":
                        ReadTrackNode(node.InnerText, emitter.mSystemBrightness);
                        break;
                    case "LaunchSpeed":
                        ReadTrackNode(node.InnerText, emitter.mLaunchSpeed);
                        break;
                    case "LaunchAngle":
                        ReadTrackNode(node.InnerText, emitter.mLaunchAngle);
                        break;
                    case "Field":
                        aTempEmitterField.Add(ReadField(node));
                        break;
                    case "SystemField":
                        aTempEmitterSystemField.Add(ReadField(node));
                        break;
                    case "ParticleRed":
                        ReadTrackNode(node.InnerText, emitter.mParticleRed);
                        break;
                    case "ParticleGreen":
                        ReadTrackNode(node.InnerText, emitter.mParticleGreen);
                        break;
                    case "ParticleBlue":
                        ReadTrackNode(node.InnerText, emitter.mParticleBlue);
                        break;
                    case "ParticleAlpha":
                        ReadTrackNode(node.InnerText, emitter.mParticleAlpha);
                        break;
                    case "ParticleBrightness":
                        ReadTrackNode(node.InnerText, emitter.mParticleBrightness);
                        break;
                    case "ParticleSpinAngle":
                        ReadTrackNode(node.InnerText, emitter.mParticleSpinAngle);
                        break;
                    case "ParticleSpinSpeed":
                        ReadTrackNode(node.InnerText, emitter.mParticleSpinSpeed);
                        break;
                    case "ParticleScale":
                        ReadTrackNode(node.InnerText, emitter.mParticleScale);
                        break;
                    case "ParticleStretch":
                        ReadTrackNode(node.InnerText, emitter.mParticleStretch);
                        break;
                    case "CollisionReflect":
                        ReadTrackNode(node.InnerText, emitter.mCollisionReflect);
                        break;
                    case "CollisionSpin":
                        ReadTrackNode(node.InnerText, emitter.mCollisionSpin);
                        break;
                    case "ClipTop":
                        ReadTrackNode(node.InnerText, emitter.mClipTop);
                        break;
                    case "ClipBottom":
                        ReadTrackNode(node.InnerText, emitter.mClipBottom);
                        break;
                    case "ClipLeft":
                        ReadTrackNode(node.InnerText, emitter.mClipLeft);
                        break;
                    case "ClipRight":
                        ReadTrackNode(node.InnerText, emitter.mClipRight);
                        break;
                    case "AnimationRate":
                        ReadTrackNode(node.InnerText, emitter.mAnimationRate);
                        break;
                    }
                }
                emitter.mParticleFields = [.. aTempEmitterField];
                emitter.mParticleFieldCount = aTempEmitterField.Count;
                emitter.mSystemFields = [.. aTempEmitterSystemField];
                emitter.mSystemFieldCount = aTempEmitterSystemField.Count;
                aEmitterDefs.Add(emitter);
            }

            particles.mEmitterDefCount = aEmitterDefs.Count;
            particles.mEmitterDefs = [..aEmitterDefs];
            return particles;
        }

        public static void SetBit(ref int flags, int index, bool value)
        {
            if (value)
            {
                flags |= (1 << index);
            }
            else
            {
                flags &= ~(1 << index);
            }
        }

        private static ParticleField ReadField(XmlNode root)
        {
            ParticleField field = new();
            XmlNodeList childnodes = root.ChildNodes;
            foreach (XmlNode node in childnodes)
            {
                switch (node.Name)
                {
                case "FieldType":
                    field.mFieldType = Enum.Parse<ParticleFieldType>(node.InnerText, true);
                    break;
                case "X":
                    ReadTrackNode(node.InnerText, field.mX);
                    break;
                case "Y":
                    ReadTrackNode(node.InnerText, field.mY);
                    break;
                }
            }
            return field;
        }

        public static void ReadTrackNode(string inText, FloatParameterTrack realans)
        {
            List<FloatParameterTrackNode> ans = new List<FloatParameterTrackNode>();
            int length = inText.Length;
            int i = 0;
            while (i < length)
            {
                FloatParameterTrackNode node = new FloatParameterTrackNode();
                //Min, Max, Distribution
                char next = inText[i];
                if (next == '[')
                {
                    //Read First Number
                    i++;
                    int j = i;
                    while (true)
                    {
                        j++;
                        char next2 = inText[j];
                        if (next2 == ' ' || next2 == ']') break;
                    }
                    float n = float.Parse(inText[i..j], CultureInfo.InvariantCulture);
                    node.mLowValue = n;
                    //Check Next Token
                    if (inText[j] == ']')
                    {
                        node.mHighValue = n;
                        node.mDistribution = 0; //Only one number => Constant
                        i = j + 1;
                    }
                    else
                    {
                        //Is Distribution?
                        i = ++j;
                        char next2 = inText[i];
                        if (next2 >= 'A' && next2 <= 'Z')
                        {
                            while (true)
                            {
                                j++;
                                if (inText[j] == ' ') break;
                            }
                            node.mDistribution = Enum.Parse<TodCurves>(inText[i..j], true);
                            i = ++j;
                        }
                        else
                        {
                            node.mDistribution = TodCurves.Linear;
                        }
                        //Last Number
                        while (true)
                        {
                            j++;
                            if (inText[j] == ']') break;
                        }
                        n = float.Parse(inText[i..j], CultureInfo.InvariantCulture);
                        node.mHighValue = n;
                        i = ++j;
                    }
                }
                else if (next == '.' || next == '-' || (next >= '0' && next <= '9'))
                {
                    //Read a number
                    int j = i;
                    while (true)
                    {
                        j++;
                        if (j >= length) break;
                        char next2 = inText[j];
                        if (next2 == ' ' || next2 == ',') break;
                    }
                    float n = float.Parse(inText[i..j], CultureInfo.InvariantCulture);
                    node.mLowValue = n;
                    node.mHighValue = n;
                    node.mDistribution = TodCurves.Linear;
                    i = j;
                }
                else
                {
                    node.mLowValue = 0;
                    node.mHighValue = 0;
                    node.mDistribution = TodCurves.Linear;
                }
                //Time
                if (i >= length)
                {
                    node.mTime = -10000;
                    node.mCurveType = TodCurves.Linear;
                    ans.Add(node);
                    break;
                }
                next = inText[i];
                if (next == ',')
                {
                    i++;
                    int j = i;
                    while (true)
                    {
                        j++;
                        if (j >= length || inText[j] == ' ') break;
                    }
                    node.mTime = float.Parse(inText[i..j], CultureInfo.InvariantCulture);
                    i = j;
                }
                else
                {
                    node.mTime = -10000;
                }
                //CurveType
                if ((++i) >= length)
                {
                    node.mCurveType = TodCurves.Linear;
                    ans.Add(node);
                    break;
                }
                next = inText[i];
                if (next < 'A' || next > 'Z')
                {
                    node.mCurveType = TodCurves.Linear;
                }
                else
                {
                    int j = i;
                    while (true)
                    {
                        j++;
                        if (j >= length || inText[j] == ' ') break;
                    }
                    node.mCurveType = Enum.Parse<TodCurves>(inText[i..j], true);
                    i = ++j;
                }
                ans.Add(node);
            }
            realans.mNodes = [.. ans];
            realans.mCountNodes = ans.Count;
            //Default Times
            int tNum = realans.mNodes.Length;
            if (tNum == 0)
            {
                return;
            }
            if (realans.mNodes[0].mTime < -1000) realans.mNodes[0].mTime = 0;
            if (tNum != 1 && realans.mNodes[tNum - 1].mTime < -1000) realans.mNodes[tNum - 1].mTime = 100;
            float delta = 0, last = 0;
            for (i = 0; i < tNum; i++)
            {
                if (realans.mNodes[i].mTime >= -1000)
                {
                    last = realans.mNodes[i].mTime;
                    //Find the delta
                    if (i < tNum - 1)
                    {
                        int j = i + 1;
                        while (realans.mNodes[j].mTime < -1000) j++;
                        delta = (realans.mNodes[j].mTime - realans.mNodes[i].mTime) / delta;
                    }
                }
                else
                {
                    realans.mNodes[i].mTime = last + delta;
                }
                realans.mNodes[i].mTime /= 100;
            }
        }
    }
}