using System;

namespace EffectViewer.Rendering
{
    public enum ViewportBackgroundMode
    {
        Theme,
        Light,
        Dark
    }

    public static class ViewportBackgroundSettings
    {
        private static ViewportBackgroundMode _mode = ViewportBackgroundMode.Theme;

        public static event EventHandler ModeChanged;

        public static ViewportBackgroundMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                ModeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
