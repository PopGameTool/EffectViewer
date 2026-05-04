namespace EffectViewer.Tests.TestUtilities;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "EffectViewer.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(params string[] parts)
    {
        string[] allParts = new string[parts.Length + 1];
        allParts[0] = Path;
        Array.Copy(parts, 0, allParts, 1, parts.Length);
        return System.IO.Path.Combine(allParts);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
