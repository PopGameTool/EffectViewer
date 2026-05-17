using System.Numerics;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.EffectRuntime.Graphics;
using EffectViewer.Rendering;
using InlineArray3TriVertex = System.Runtime.CompilerServices.InlineArray3<EffectViewer.EffectRuntime.Common.TriVertex>;

namespace EffectViewer.Tests.Rendering;

public sealed class FrameCaptureGraphicsTests
{
    [Fact]
    public void DrawTrianglesTexClipsTriangleToCurrentClipRect()
    {
        FrameCaptureGraphics graphics = new()
        {
            mClipRect = new System.Drawing.Rectangle(0, 0, 10, 10)
        };
        Image image = new()
        {
            mId = "texture",
            mWidth = 20,
            mHeight = 20
        };
        InlineArray3TriVertex triangle = new();
        triangle[0] = CreateVertex(-10, 0, 0, 0);
        triangle[1] = CreateVertex(10, 0, 1, 0);
        triangle[2] = CreateVertex(10, 10, 1, 1);

        graphics.DrawTrianglesTex(image, [triangle]);

        RenderMeshCommand mesh = Assert.Single(graphics.Frame.Meshes);
        Assert.True(mesh.VertexCount > 0);
        Assert.True(mesh.VertexCount % 3 == 0);
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            RenderVertex vertex = mesh.GetVertex(i);
            Assert.InRange(vertex.Position.X, 0f, 10f);
            Assert.InRange(vertex.Position.Y, 0f, 10f);
        }
    }

    private static TriVertex CreateVertex(float x, float y, float u, float v)
    {
        return new TriVertex
        {
            Position = new Vector3(x, y, 0),
            TextureCoordinate = new Vector2(u, v)
        };
    }
}
