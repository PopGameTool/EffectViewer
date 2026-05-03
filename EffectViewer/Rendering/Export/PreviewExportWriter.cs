using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Rendering.Export
{
    public static class PreviewExportWriter
    {
        public static void WritePng(
            Stream stream,
            RenderFrame frame,
            ITextureSource textureSource,
            PreviewExportOptions options,
            IProgress<PreviewExportProgress> progress = null)
        {
            options = (options ?? new PreviewExportOptions()).Normalized();
            RenderFrame[] frames = [frame ?? new RenderFrame()];
            PreviewExportCanvas canvas = PreviewExportLayout.CreateCanvas(frames, options);
            Report(progress, PreviewExportProgressStage.RasterizingFrames, 0, 1);
            byte[] pixels = PreviewFrameRasterizer.Render(
                frames[0],
                textureSource,
                canvas.Width,
                canvas.Height,
                canvas.Transform,
                transparentBackground: true,
                options.ReferenceWidth,
                options.ReferenceHeight);
            Report(progress, PreviewExportProgressStage.RasterizingFrames, 1, 1);
            Report(progress, PreviewExportProgressStage.EncodingImage, 0, 1);
            PngImageWriter.Write(stream, canvas.Width, canvas.Height, pixels);
            Report(progress, PreviewExportProgressStage.EncodingImage, 1, 1);
        }

        public static void WritePngSequenceZip(
            Stream stream,
            Func<int, RenderFrame> frameFactory,
            ITextureSource textureSource,
            PreviewExportOptions options,
            Func<bool> shouldStop = null,
            IProgress<PreviewExportProgress> progress = null)
        {
            ArgumentNullException.ThrowIfNull(frameFactory);
            options = (options ?? new PreviewExportOptions()).Normalized();
            List<RenderFrame> frames = CaptureFrames(frameFactory, options.FrameCount, shouldStop, progress);
            PreviewExportCanvas canvas = PreviewExportLayout.CreateCanvas(frames, options);

            using ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true);
            int frameCount = frames.Count;
            int digits = Math.Max(4, frameCount.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
            Report(progress, PreviewExportProgressStage.WritingFrames, 0, frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"frame_{(i + 1).ToString().PadLeft(digits, '0')}.png", CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                byte[] pixels = PreviewFrameRasterizer.Render(
                    frames[i],
                    textureSource,
                    canvas.Width,
                    canvas.Height,
                    canvas.Transform,
                    transparentBackground: true,
                    options.ReferenceWidth,
                    options.ReferenceHeight);
                PngImageWriter.Write(entryStream, canvas.Width, canvas.Height, pixels);
                Report(progress, PreviewExportProgressStage.WritingFrames, i + 1, frameCount);
            }
        }

        public static void WriteGif(
            Stream stream,
            Func<int, RenderFrame> frameFactory,
            ITextureSource textureSource,
            PreviewExportOptions options,
            Func<bool> shouldStop = null,
            IProgress<PreviewExportProgress> progress = null)
        {
            ArgumentNullException.ThrowIfNull(frameFactory);
            options = (options ?? new PreviewExportOptions()).Normalized();
            List<RenderFrame> capturedFrames = CaptureFrames(frameFactory, options.FrameCount, shouldStop, progress);
            PreviewExportCanvas canvas = PreviewExportLayout.CreateCanvas(capturedFrames, options);

            List<byte[]> frames = new(capturedFrames.Count);
            Report(progress, PreviewExportProgressStage.RasterizingFrames, 0, capturedFrames.Count);
            for (int i = 0; i < capturedFrames.Count; i++)
            {
                frames.Add(PreviewFrameRasterizer.Render(
                    capturedFrames[i],
                    textureSource,
                    canvas.Width,
                    canvas.Height,
                    canvas.Transform,
                    transparentBackground: true,
                    options.ReferenceWidth,
                    options.ReferenceHeight));
                Report(progress, PreviewExportProgressStage.RasterizingFrames, i + 1, capturedFrames.Count);
            }

            Report(progress, PreviewExportProgressStage.EncodingImage, 0, 1);
            GifImageWriter.Write(stream, canvas.Width, canvas.Height, frames, options.Fps);
            Report(progress, PreviewExportProgressStage.EncodingImage, 1, 1);
        }

        public static void WriteWebp(
            Stream stream,
            Func<int, RenderFrame> frameFactory,
            ITextureSource textureSource,
            PreviewExportOptions options,
            Func<bool> shouldStop = null,
            IProgress<PreviewExportProgress> progress = null)
        {
            ArgumentNullException.ThrowIfNull(frameFactory);
            options = (options ?? new PreviewExportOptions()).Normalized();
            List<RenderFrame> capturedFrames = CaptureFrames(frameFactory, options.FrameCount, shouldStop, progress);
            PreviewExportCanvas canvas = PreviewExportLayout.CreateCanvas(capturedFrames, options);

            List<byte[]> frames = new(capturedFrames.Count);
            Report(progress, PreviewExportProgressStage.RasterizingFrames, 0, capturedFrames.Count);
            for (int i = 0; i < capturedFrames.Count; i++)
            {
                frames.Add(PreviewFrameRasterizer.Render(
                    capturedFrames[i],
                    textureSource,
                    canvas.Width,
                    canvas.Height,
                    canvas.Transform,
                    transparentBackground: true,
                    options.ReferenceWidth,
                    options.ReferenceHeight));
                Report(progress, PreviewExportProgressStage.RasterizingFrames, i + 1, capturedFrames.Count);
            }

            WebpImageWriter.Write(stream, canvas.Width, canvas.Height, frames, options.Fps, progress);
        }

        private static List<RenderFrame> CaptureFrames(
            Func<int, RenderFrame> frameFactory,
            int frameCount,
            Func<bool> shouldStop = null,
            IProgress<PreviewExportProgress> progress = null)
        {
            List<RenderFrame> frames = new(Math.Max(1, frameCount));
            Report(progress, PreviewExportProgressStage.CapturingFrames, 0, Math.Max(1, frameCount));
            for (int i = 0; i < Math.Max(1, frameCount); i++)
            {
                if (i > 0 && shouldStop?.Invoke() == true)
                {
                    break;
                }

                RenderFrame frame = frameFactory(i) ?? new RenderFrame();
                if (shouldStop?.Invoke() == true && frames.Count > 0 && IsEmpty(frame))
                {
                    break;
                }

                frames.Add(frame);
                Report(progress, PreviewExportProgressStage.CapturingFrames, i + 1, Math.Max(1, frameCount));
            }

            if (frames.Count == 0)
            {
                frames.Add(new RenderFrame());
                Report(progress, PreviewExportProgressStage.CapturingFrames, 1, 1);
            }

            return frames;
        }

        private static void Report(
            IProgress<PreviewExportProgress> progress,
            PreviewExportProgressStage stage,
            int completedItems,
            int totalItems)
        {
            progress?.Report(new PreviewExportProgress
            {
                Stage = stage,
                CompletedItems = Math.Max(0, completedItems),
                TotalItems = Math.Max(1, totalItems)
            });
        }

        private static bool IsEmpty(RenderFrame frame)
        {
            return frame is null || frame.Sprites.Count == 0 && frame.Meshes.Count == 0;
        }
    }
}
