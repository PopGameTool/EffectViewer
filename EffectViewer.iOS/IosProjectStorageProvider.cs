using System.IO;
using EffectViewer.Projects;
using Foundation;

namespace EffectViewer.iOS
{
    internal sealed class IosProjectStorageProvider : IProjectStorageProvider
    {
        public string ProjectsRootPath { get; } = Path.Combine(
            NSSearchPath.GetDirectories(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User)[0],
            "EffectViewer",
            "Projects");
    }
}
