using EffectViewer.Projects;

namespace EffectViewer.ViewModels
{
    public sealed class ProjectListItemViewModel
    {
        public ProjectListItemViewModel(ProjectInfo project)
        {
            Name = project.Name;
            ProjectPath = project.ProjectPath;
            DirectoryName = project.DirectoryName;
            LastModifiedText = project.LastModified == default
                ? string.Empty
                : project.LastModified.ToString("yyyy-MM-dd HH:mm");
        }

        public string Name { get; }
        public string ProjectPath { get; }
        public string DirectoryName { get; }
        public string LastModifiedText { get; }
    }
}
