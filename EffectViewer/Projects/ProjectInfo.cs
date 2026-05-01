using System;

namespace EffectViewer.Projects
{
    public sealed class ProjectInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string DirectoryName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}
