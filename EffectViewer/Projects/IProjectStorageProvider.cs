using System.Threading.Tasks;

namespace EffectViewer.Projects
{
    public interface IProjectStorageProvider
    {
        string ProjectsRootPath { get; }
    }

    public interface IProjectStoragePersistence
    {
        Task FlushAsync();
    }
}
