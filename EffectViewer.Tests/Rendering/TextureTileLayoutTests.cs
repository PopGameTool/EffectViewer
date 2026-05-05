using System.Linq;
using System.Numerics;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Tests.Rendering;

public sealed class TextureTileLayoutTests
{
    [Fact]
    public void CreateKeepsTextureAtDeviceLimitWhole()
    {
        TextureTileLayout layout = TextureTileLayout.Create(4096, 2048, 4096);

        Assert.False(layout.IsTiled);
        TextureTile tile = Assert.Single(layout.Tiles);
        Assert.Equal(4096, tile.UploadWidth);
        Assert.Equal(2048, tile.UploadHeight);
    }

    [Fact]
    public void CreateSplitsTextureOverDeviceLimitIntoBoundedTiles()
    {
        TextureTileLayout layout = TextureTileLayout.Create(4097, 4097, 4096);

        Assert.True(layout.IsTiled);
        Assert.Equal(4, layout.Tiles.Count);
        Assert.All(layout.Tiles, tile =>
        {
            Assert.InRange(tile.UploadWidth, 1, 4096);
            Assert.InRange(tile.UploadHeight, 1, 4096);
        });
    }

    [Fact]
    public void CopyTilePixelsIncludesNeighboringGutter()
    {
        byte[] source = new byte[5 * 4];
        for (int x = 0; x < 5; x++)
        {
            source[x * 4] = (byte)x;
            source[x * 4 + 3] = 255;
        }

        TextureUploadData data = new(5, 1, source);
        TextureTileLayout layout = TextureTileLayout.Create(5, 1, 4);
        TextureTile middle = layout.Tiles[1];

        byte[] pixels = TextureTileLayout.CopyTilePixels(data, middle);

        Assert.Equal(1, middle.UploadX);
        Assert.Equal(4, middle.UploadWidth);
        Assert.Equal([1, 2, 3, 4], pixels.Where((_, index) => index % 4 == 0).Select(value => (int)value).ToArray());
    }

    [Fact]
    public void ClipperSplitsFullQuadIntoTileBatches()
    {
        TextureTileLayout layout = TextureTileLayout.Create(5, 1, 4);
        RenderVertex[] vertices =
        [
            new(new Vector2(0, 0), new Vector2(0, 0), Vector4.One),
            new(new Vector2(10, 0), new Vector2(1, 0), Vector4.One),
            new(new Vector2(10, 10), new Vector2(1, 1), Vector4.One),
            new(new Vector2(0, 0), new Vector2(0, 0), Vector4.One),
            new(new Vector2(10, 10), new Vector2(1, 1), Vector4.One),
            new(new Vector2(0, 10), new Vector2(0, 1), Vector4.One)
        ];

        IReadOnlyList<TextureTileDrawBatch> batches = TextureTileClipper.CreateBatches(layout, vertices);

        Assert.Equal(layout.Tiles.Count, batches.Count);
        Assert.All(batches, batch =>
        {
            Assert.True(batch.Vertices.Count % 3 == 0);
            Assert.All(batch.Vertices, vertex =>
            {
                Assert.InRange(vertex.Uv.X, 0f, 1f);
                Assert.InRange(vertex.Uv.Y, 0f, 1f);
            });
        });
    }
}
