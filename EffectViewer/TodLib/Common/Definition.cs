namespace EffectViewer.TodLib.Common
{
    internal static class Definition
    {
        public static float FloatTrackEvaluate(FloatParameterTrack theTrack, float theTimeValue, float theInterp)
        {
            if (theTrack.mCountNodes == 0)
            {
                return 0f;
            }

            if (theTimeValue < theTrack.mNodes[0].mTime)  // 如果当前时间小于第一个节点的开始时间
            {
                return TodCommon.TodCurveEvaluate(theInterp, theTrack.mNodes[0].mLowValue, theTrack.mNodes[0].mHighValue, theTrack.mNodes[0].mDistribution);
            }

            for (int i = 1; i < theTrack.mCountNodes; i++)
            {
                FloatParameterTrackNode aNodeNxt = theTrack.mNodes[i];
                if (theTimeValue <= aNodeNxt.mTime)  // 寻找首个开始时间大于当前时间的节点
                {
                    FloatParameterTrackNode aNodeCur = theTrack.mNodes[i - 1];
                    // 计算当前时间在〔当前节点至下一节点〕的过程中的进度
                    float aTimeFraction = (theTimeValue - aNodeCur.mTime) / (aNodeNxt.mTime - aNodeCur.mTime);
                    float aLeftValue = TodCommon.TodCurveEvaluate(theInterp, aNodeCur.mLowValue, aNodeCur.mHighValue, aNodeCur.mDistribution);
                    float aRightValue = TodCommon.TodCurveEvaluate(theInterp, aNodeNxt.mLowValue, aNodeNxt.mHighValue, aNodeNxt.mDistribution);
                    return TodCommon.TodCurveEvaluate(aTimeFraction, aLeftValue, aRightValue, aNodeCur.mCurveType);
                }
            }

            FloatParameterTrackNode aLastNode = theTrack.mNodes[theTrack.mCountNodes - 1];  // 如果当前时间大于最后一个节点的开始时间
            return TodCommon.TodCurveEvaluate(theInterp, aLastNode.mLowValue, aLastNode.mHighValue, aLastNode.mDistribution);
        }

        public static void FloatTrackSetDefault(FloatParameterTrack theTrack, float theValue)
        {
            if (theTrack.mNodes == null && theValue != 0f)  // 确保该参数轨道无节点（未被赋值过）且给定的默认值不为 0
            {
                theTrack.mCountNodes = 1;  // 默认参数轨道有且仅有 1 个节点
                FloatParameterTrackNode aNode = new FloatParameterTrackNode();
                theTrack.mNodes = [aNode];
                aNode.mTime = 0f;
                aNode.mLowValue = theValue;
                aNode.mHighValue = theValue;
                aNode.mCurveType = TodCurves.Constant;
                aNode.mDistribution = TodCurves.Linear;
            }
        }

        public static bool FloatTrackIsSet(FloatParameterTrack theTrack)
        {
            return theTrack.mCountNodes != 0 &&
                theTrack.mNodes[0].mCurveType != TodCurves.Constant;
        }

        public static bool FloatTrackIsConstantZero(FloatParameterTrack theTrack)
        {
            // 当轨道无节点，或仅存在一个节点且该节点的最大、最小值均为 0 时，认为该轨道上的值恒为零
            if (theTrack.mCountNodes == 0)
            {
                return true;
            }

            if (theTrack.mCountNodes != 1)
            {
                return false;
            }

            FloatParameterTrackNode aNode = theTrack.mNodes[0];
            return aNode.mLowValue == 0f && aNode.mHighValue == 0f;
        }
    }
}