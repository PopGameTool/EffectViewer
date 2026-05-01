using System.IO;
using Android.Content;
using EffectViewer.Projects;

namespace EffectViewer.Android
{
    internal sealed class AndroidProjectStorageProvider : IProjectStorageProvider
    {
        public AndroidProjectStorageProvider(Context context)
        {
            ProjectsRootPath = Path.Combine(context.FilesDir?.AbsolutePath ?? string.Empty, "EffectViewer", "Projects");
        }

        public string ProjectsRootPath { get; }
    }
}
