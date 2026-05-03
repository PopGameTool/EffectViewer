using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace EffectViewer.Rendering.Export
{
    public static class WebpImageWriter
    {
        public static void Write(
            Stream stream,
            int width,
            int height,
            IReadOnlyList<byte[]> rgbaFrames,
            int fps,
            IProgress<PreviewExportProgress> progress = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "WebP dimensions must be positive.");
            }

            if (rgbaFrames is null || rgbaFrames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(rgbaFrames));
            }

            fps = Math.Clamp(fps, 1, 240);
            uint delayMilliseconds = (uint)Math.Max(1, (int)Math.Round(1000d / fps));
            Report(progress, PreviewExportProgressStage.WritingFrames, 0, rgbaFrames.Count);
            using Image<Rgba32> image = CreateFrame(rgbaFrames[0], width, height);
            WebpMetadata metadata = image.Metadata.GetWebpMetadata();
            metadata.FileFormat = WebpFileFormatType.Lossless;
            metadata.RepeatCount = 0;
            metadata.BackgroundColor = Color.Transparent;
            ConfigureFrame(image.Frames.RootFrame, delayMilliseconds);
            Report(progress, PreviewExportProgressStage.WritingFrames, 1, rgbaFrames.Count);

            for (int i = 1; i < rgbaFrames.Count; i++)
            {
                using Image<Rgba32> nextFrameImage = CreateFrame(rgbaFrames[i], width, height);
                ImageFrame<Rgba32> frame = image.Frames.AddFrame(nextFrameImage.Frames.RootFrame);
                ConfigureFrame(frame, delayMilliseconds);
                Report(progress, PreviewExportProgressStage.WritingFrames, i + 1, rgbaFrames.Count);
            }

            Report(progress, PreviewExportProgressStage.EncodingImage, 0, 1);
            image.SaveAsWebp(stream, new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossless,
                Quality = 100,
                Method = WebpEncodingMethod.BestQuality,
                TransparentColorMode = WebpTransparentColorMode.Preserve
            });
            Report(progress, PreviewExportProgressStage.EncodingImage, 1, 1);
        }

        private static Image<Rgba32> CreateFrame(byte[] rgbaPixels, int width, int height)
        {
            if (rgbaPixels is null || rgbaPixels.Length < width * height * 4)
            {
                throw new ArgumentException("RGBA pixel data is incomplete.", nameof(rgbaPixels));
            }

            return ImageSharpImage.LoadPixelData<Rgba32>(rgbaPixels, width, height);
        }

        private static void ConfigureFrame(ImageFrame<Rgba32> frame, uint delayMilliseconds)
        {
            WebpFrameMetadata frameMetadata = frame.Metadata.GetWebpMetadata();
            frameMetadata.FrameDelay = delayMilliseconds;
            frameMetadata.BlendMethod = WebpBlendMethod.Source;
            frameMetadata.DisposalMethod = WebpDisposalMethod.RestoreToBackground;
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
    }
}
