using System;
using System.Collections.Generic;
using EffectViewer.Rendering;
using EffectViewer.Runtime.Showcase;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Lua
{
    internal sealed class LuaShowcaseScript : IShowcaseScriptCallbacks
    {
        private readonly Script _script;
        private readonly DynValue _update;
        private readonly DynValue _draw;
        private readonly IList<string> _logs;
        private int _frame;
        private double _elapsedSeconds;
        private bool _updateFailed;
        private bool _drawFailed;
        private bool _disposed;

        public LuaShowcaseScript(Script script, DynValue update, DynValue draw, IList<string> logs)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _update = update;
            _draw = draw;
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        }

        public void Update(double deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            _elapsedSeconds += deltaSeconds;
            _frame++;

            if (_update.Type != DataType.Function || _updateFailed)
            {
                return;
            }

            try
            {
                _script.Call(_update, deltaSeconds, _elapsedSeconds, _frame);
            }
            catch (ScriptRuntimeException ex)
            {
                _updateFailed = true;
                _logs.Add($"update failed: {ex.DecoratedMessage}");
            }
            catch (Exception ex)
            {
                _updateFailed = true;
                _logs.Add($"update failed: {ex.Message}");
            }
        }

        public bool TryDraw(FrameCaptureGraphics graphics)
        {
            if (_disposed || _draw.Type != DataType.Function || _drawFailed)
            {
                return false;
            }

            try
            {
                _script.Call(_draw, new LuaGraphicsApi(graphics), _elapsedSeconds, _frame);
                return true;
            }
            catch (ScriptRuntimeException ex)
            {
                _drawFailed = true;
                _logs.Add($"draw failed: {ex.DecoratedMessage}");
            }
            catch (Exception ex)
            {
                _drawFailed = true;
                _logs.Add($"draw failed: {ex.Message}");
            }

            return false;
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
