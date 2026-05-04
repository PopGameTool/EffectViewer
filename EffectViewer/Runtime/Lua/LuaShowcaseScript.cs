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
        private readonly bool _hasContext;
        private readonly DynValue _context;
        private readonly DynValue _update;
        private readonly DynValue _draw;
        private readonly IList<string> _logs;
        private int _frame;
        private double _elapsedSeconds;
        private bool _updateFailed;
        private bool _drawFailed;
        private bool _disposed;

        public LuaShowcaseScript(Script script, bool hasContext, DynValue context, DynValue update, DynValue draw, IList<string> logs)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _hasContext = hasContext;
            _context = context;
            _update = update;
            _draw = draw;
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        }

        public bool HasUpdate => _update.Type == DataType.Function;

        public bool HasDraw => _draw.Type == DataType.Function;

        public void Update(double deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            _elapsedSeconds += deltaSeconds;
            _frame++;

            if (!HasUpdate || _updateFailed)
            {
                return;
            }

            try
            {
                if (!_hasContext)
                {
                    _script.Call(_update, deltaSeconds, _elapsedSeconds, _frame);
                }
                else
                {
                    _script.Call(_update, _context, deltaSeconds, _elapsedSeconds, _frame);
                }
            }
            catch (ScriptRuntimeException ex)
            {
                _updateFailed = true;
                _logs.Add($"update failed: {ex.DecoratedMessage ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _updateFailed = true;
                _logs.Add($"update failed: {ex.Message}");
            }
        }

        public bool TryDraw(FrameCaptureGraphics graphics)
        {
            if (_disposed || !HasDraw || _drawFailed)
            {
                return false;
            }

            try
            {
                LuaGraphicsApi api = new(graphics);
                if (!_hasContext)
                {
                    _script.Call(_draw, api, _elapsedSeconds, _frame);
                }
                else
                {
                    _script.Call(_draw, _context, api, _elapsedSeconds, _frame);
                }
                return true;
            }
            catch (ScriptRuntimeException ex)
            {
                _drawFailed = true;
                _logs.Add($"draw failed: {ex.DecoratedMessage ?? ex.Message}");
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
