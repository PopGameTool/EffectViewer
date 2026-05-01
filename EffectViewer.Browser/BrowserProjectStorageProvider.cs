using System.IO;
using EffectViewer.Projects;

namespace EffectViewer.Browser
{
    internal sealed class BrowserProjectStorageProvider : IProjectStorageProvider
    {
        public string ProjectsRootPath { get; } = Path.Combine("EffectViewer", "Projects");
    }
}
