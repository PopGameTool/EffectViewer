using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Tests.TestUtilities;

internal sealed class TestTextureSource : ITextureSource
{
    private readonly Dictionary<string, TextureUploadData> _textures = new();

    public TestTextureSource Add(string id, int width, int height, byte[] rgbaPixels)
    {
        _textures[id] = new TextureUploadData(width, height, rgbaPixels);
        return this;
    }

    public bool TryLoad(RenderTextureRef texture, out TextureUploadData data)
    {
        return _textures.TryGetValue(texture.Id, out data!);
    }
}
