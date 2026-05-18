#if VIEWPORT_RENDER_STATS
using System;
using System.Globalization;

namespace EffectViewer.Controls
{
    public readonly record struct ViewportRenderStats(
        string BackendName,
        long FrameCount,
        int SampleCount,
        double Fps,
        double LastFrameIntervalMs,
        double AverageFrameIntervalMs,
        double P95FrameIntervalMs,
        double AverageProviderMs,
        double AverageRenderMs,
        double AveragePublishMs)
    {
        public string ToOverlayText()
        {
            string backendName = string.IsNullOrWhiteSpace(BackendName) ? "Viewport" : BackendName;
            if (SampleCount == 0)
            {
                return backendName + Environment.NewLine + "warming up";
            }

            string publishText = AveragePublishMs > 0.001d
                ? string.Format(CultureInfo.InvariantCulture, "  publish avg {0:0.00} ms", AveragePublishMs)
                : string.Empty;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}fps {2:0.0}  frame avg/p95 {3:0.00}/{4:0.00} ms{1}provider avg {5:0.00} ms  render avg {6:0.00} ms{7}",
                backendName,
                Environment.NewLine,
                Fps,
                AverageFrameIntervalMs,
                P95FrameIntervalMs,
                AverageProviderMs,
                AverageRenderMs,
                publishText);
        }
    }

    public interface IViewportRenderStatsProvider
    {
        ViewportRenderStats GetRenderStats();
    }

    public sealed class ViewportRenderStatsTracker
    {
        private const int MaxSamples = 240;

        private readonly double[] _frameIntervalsMs = new double[MaxSamples];
        private readonly double[] _providerMs = new double[MaxSamples];
        private readonly double[] _renderMs = new double[MaxSamples];
        private readonly double[] _publishMs = new double[MaxSamples];
        private readonly string _defaultBackendName;
        private string _backendName;
        private int _sampleIndex;
        private int _sampleCount;

        public ViewportRenderStatsTracker(string defaultBackendName)
        {
            _defaultBackendName = string.IsNullOrWhiteSpace(defaultBackendName) ? "Viewport" : defaultBackendName;
            _backendName = _defaultBackendName;
        }

        public long FrameCount { get; private set; }

        public void RecordFrame(
            string backendName,
            double frameIntervalMs,
            double providerMs,
            double renderMs,
            double publishMs = 0d)
        {
            if (!string.IsNullOrWhiteSpace(backendName))
            {
                _backendName = backendName;
            }

            _frameIntervalsMs[_sampleIndex] = Math.Max(0d, frameIntervalMs);
            _providerMs[_sampleIndex] = Math.Max(0d, providerMs);
            _renderMs[_sampleIndex] = Math.Max(0d, renderMs);
            _publishMs[_sampleIndex] = Math.Max(0d, publishMs);
            _sampleIndex = (_sampleIndex + 1) % MaxSamples;
            _sampleCount = Math.Min(_sampleCount + 1, MaxSamples);
            FrameCount++;
        }

        public ViewportRenderStats GetStats()
        {
            int count = _sampleCount;
            double averageFrameInterval = Average(_frameIntervalsMs, count);
            double fps = averageFrameInterval > 0.001d ? 1000d / averageFrameInterval : 0d;
            int lastIndex = _sampleIndex == 0 ? MaxSamples - 1 : _sampleIndex - 1;
            string backendName = string.IsNullOrWhiteSpace(_backendName) ? _defaultBackendName : _backendName;

            return new ViewportRenderStats(
                backendName,
                FrameCount,
                count,
                fps,
                count > 0 ? _frameIntervalsMs[lastIndex] : 0d,
                averageFrameInterval,
                Percentile(_frameIntervalsMs, count, 0.95d),
                Average(_providerMs, count),
                Average(_renderMs, count),
                Average(_publishMs, count));
        }

        private static double Average(double[] samples, int count)
        {
            if (count <= 0)
            {
                return 0d;
            }

            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                sum += samples[i];
            }

            return sum / count;
        }

        private static double Percentile(double[] samples, int count, double percentile)
        {
            if (count <= 0)
            {
                return 0d;
            }

            double[] sorted = new double[count];
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted);
            int index = (int)Math.Ceiling(percentile * count) - 1;
            return sorted[Math.Clamp(index, 0, count - 1)];
        }
    }
}
#endif
