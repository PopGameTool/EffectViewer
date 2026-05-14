using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace EffectViewer.EffectRuntime.Reanim
{
    public class ReanimatorDefinition
    {
        public ReanimatorTrack[] mTracks;
        public short mTrackCount;
        public float mFPS;
        private FrozenDictionary<string, int> mTrackIndexMap;

        public ReanimatorDefinition()
        {
            mFPS = 12f;
            mTrackCount = 0;
        }

        public void Init()
        {
            mTrackIndexMap = BuildMap(mTracks).ToFrozenDictionary();
        }

        private static Dictionary<string, int> BuildMap(ReanimatorTrack[] tracks)
        {
            if (tracks == null)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
            Dictionary<string, int> map = new(tracks.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tracks.Length; i++)
            {
                string name = tracks[i].mName;
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                {
                    map[name] = i;
                }
            }
            return map;
        }

        public int FindTrackIndexFast(string theTrackName)
        {
            FrozenDictionary<string, int> map = mTrackIndexMap;
            return map != null && map.TryGetValue(theTrackName, out int idx) ? idx : -1;
        }
    }
}