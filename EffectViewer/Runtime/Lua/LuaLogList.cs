using System;
using System.Collections.ObjectModel;

namespace EffectViewer.Runtime.Lua
{
    internal sealed class LuaLogList : Collection<string>
    {
        private readonly Action<string> _logAdded;

        public LuaLogList(Action<string> logAdded)
        {
            _logAdded = logAdded;
        }

        protected override void InsertItem(int index, string item)
        {
            base.InsertItem(index, item);
            _logAdded?.Invoke(item);
        }

        protected override void SetItem(int index, string item)
        {
            base.SetItem(index, item);
            _logAdded?.Invoke(item);
        }
    }
}
