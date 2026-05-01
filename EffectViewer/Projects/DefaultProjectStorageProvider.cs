using System;
using System.IO;

namespace EffectViewer.Projects
{
    public sealed class DefaultProjectStorageProvider : IProjectStorageProvider
    {
        public DefaultProjectStorageProvider()
            : this(CreateDefaultProjectsRootPath())
        {
        }

        public DefaultProjectStorageProvider(string projectsRootPath)
        {
            ProjectsRootPath = projectsRootPath;
        }

        public string ProjectsRootPath { get; }

        private static string CreateDefaultProjectsRootPath()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = Environment.CurrentDirectory;
            }

            return Path.Combine(basePath, "EffectViewer", "Projects");
        }
    }
}
