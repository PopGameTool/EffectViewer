using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using EffectViewer.Rendering;
using EffectViewer.Rendering.Export;
using EffectViewer.Tests.TestUtilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EffectViewer.Tests.Rendering;

public sealed class PreviewExportWriterTests
{
    [Fact]
    public void WritePngRendersCroppedPreviewWithTransparentBackground()
    {
        TestTextureSource textures = new TestTextureSource()
            .Add("red", 1, 1, [255, 0, 0, 255]);
        RenderFrame frame = CreateSpriteFrame("red", new Vector2(4, 3), new Vector2(2, 2));
        PreviewExportOptions options = new()
        {
            ReferenceWidth = 8,
            ReferenceHeight = 6,
            CanvasScale = 1
        };

        using MemoryStream stream = new();
        PreviewExportWriter.WritePng(stream, frame, textures, options);

        using Image<Rgba32> image = Image.Load<Rgba32>(stream.ToArray());
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        AssertPixel(image[0, 0], 255, 0, 0, 255);
    }

    [Fact]
    public void WritePngSequenceZipUsesNormalizedFrameCountAndStableNames()
    {
        TestTextureSource textures = new TestTextureSource()
            .Add("red", 1, 1, [255, 0, 0, 255])
            .Add("blue", 1, 1, [0, 0, 255, 255]);
        List<int> requestedFrames = [];
        PreviewExportOptions options = new()
        {
            Format = PreviewExportFormat.PngSequenceZip,
            ReferenceWidth = 8,
            ReferenceHeight = 6,
            Fps = 2,
            DurationSeconds = 1
        };

        using MemoryStream stream = new();
        PreviewExportWriter.WritePngSequenceZip(
            stream,
            frameIndex =>
            {
                requestedFrames.Add(frameIndex);
                return CreateSpriteFrame(frameIndex == 0 ? "red" : "blue", new Vector2(4, 3), new Vector2(2, 2));
            },
            textures,
            options);

        stream.Position = 0;
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);

        Assert.Equal([0, 1], requestedFrames);
        Assert.Equal(["frame_0001.png", "frame_0002.png"], archive.Entries.Select(entry => entry.FullName).ToArray());
        AssertPixel(LoadFirstPixel(archive.GetEntry("frame_0001.png")!), 255, 0, 0, 255);
        AssertPixel(LoadFirstPixel(archive.GetEntry("frame_0002.png")!), 0, 0, 255, 255);
    }

    [Fact]
    public void WriteGifCapturesAnimationFrames()
    {
        TestTextureSource textures = new TestTextureSource()
            .Add("red", 1, 1, [255, 0, 0, 255]);
        PreviewExportOptions options = new()
        {
            Format = PreviewExportFormat.Gif,
            ReferenceWidth = 8,
            ReferenceHeight = 6,
            Fps = 4,
            DurationSeconds = 0.5
        };

        using MemoryStream stream = new();
        PreviewExportWriter.WriteGif(
            stream,
            _ => CreateSpriteFrame("red", new Vector2(4, 3), new Vector2(2, 2)),
            textures,
            options);

        byte[] bytes = stream.ToArray();
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(bytes, 0, 6));
        using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
        Assert.Equal(2, image.Frames.Count);
    }

    [Fact]
    public void WriteWebpCapturesAnimationFrames()
    {
        TestTextureSource textures = new TestTextureSource()
            .Add("red", 1, 1, [255, 0, 0, 255]);
        PreviewExportOptions options = new()
        {
            Format = PreviewExportFormat.Webp,
            ReferenceWidth = 8,
            ReferenceHeight = 6,
            Fps = 4,
            DurationSeconds = 0.5
        };

        using MemoryStream stream = new();
        PreviewExportWriter.WriteWebp(
            stream,
            _ => CreateSpriteFrame("red", new Vector2(4, 3), new Vector2(2, 2)),
            textures,
            options);

        byte[] bytes = stream.ToArray();
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
        using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
        Assert.Equal(2, image.Frames.Count);
    }

    [Fact]
    public void PreviewExportOptionsClampUnsafeValuesAndFrameRanges()
    {
        PreviewExportOptions options = new()
        {
            Format = PreviewExportFormat.PngSequenceZip,
            CanvasScale = 100,
            ReferenceWidth = -1,
            ReferenceHeight = 99999,
            Fps = 1000,
            UseFrameRange = true,
            StartFrameIndex = 10,
            EndFrameIndex = 3,
            SourceFps = 0
        };

        PreviewExportOptions normalized = options.Normalized();

        Assert.Equal(16, normalized.CanvasScale);
        Assert.Equal(16, normalized.ReferenceWidth);
        Assert.Equal(4096, normalized.ReferenceHeight);
        Assert.Equal(240, normalized.Fps);
        Assert.Equal(10, normalized.StartFrameIndex);
        Assert.Equal(10, normalized.EndFrameIndex);
        Assert.Equal(1, normalized.FrameCount);
    }

    private static RenderFrame CreateSpriteFrame(string textureId, Vector2 position, Vector2 size)
    {
        RenderFrame frame = new();
        frame.Sprites.Add(new RenderSpriteCommand(
            new RenderTextureRef(textureId),
            position,
            size,
            new Vector4(0, 0, 1, 1),
            Vector4.One,
            RenderBlendMode.Normal));
        return frame;
    }

    private static Rgba32 LoadFirstPixel(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);
        return image[0, 0];
    }

    private static void AssertPixel(Rgba32 pixel, byte red, byte green, byte blue, byte alpha)
    {
        Assert.Equal(red, pixel.R);
        Assert.Equal(green, pixel.G);
        Assert.Equal(blue, pixel.B);
        Assert.Equal(alpha, pixel.A);
    }
}
