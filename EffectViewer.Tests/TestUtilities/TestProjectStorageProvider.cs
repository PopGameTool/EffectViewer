using EffectViewer.Projects;

namespace EffectViewer.Tests.TestUtilities;

internal sealed class TestProjectStorageProvider : IProjectStorageProvider
{
    public TestProjectStorageProvider(string projectsRootPath)
    {
        ProjectsRootPath = projectsRootPath;
    }

    public string ProjectsRootPath { get; }
}
