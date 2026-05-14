using System;
using System.Diagnostics;

namespace EffectViewer.EffectRuntime.Common
{
    internal class Debug
    {
        public static void Assert(bool condition)
        {
            if (!condition)
            {
                //Debugger.Break();
                //throw new Exception("ASSERT failed!");
            }
        }

        public static void Log(string message)
        {
            Log(DebugType.Log, message);
        }

        public static void Log(DebugType type, string message)
        {

        }
    }
}
