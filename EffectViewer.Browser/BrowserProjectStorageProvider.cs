using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using EffectViewer.Projects;

namespace EffectViewer.Browser
{
    [SupportedOSPlatform("browser")]
    internal sealed partial class BrowserProjectStorageProvider : IProjectStorageProvider, IProjectStoragePersistence
    {
        private const string ModuleName = "effectviewer.projectStorage";
        public const string PersistentRootPath = "/EffectViewer";
        private static Task<JSObject>? _moduleTask;

        public string ProjectsRootPath { get; } = Path.Combine(PersistentRootPath, "Projects");

        public async Task FlushAsync()
        {
            try
            {
                await EnsureLoadedAsync();
                await PersistCurrentStorageAsync();
            }
            catch (System.Exception ex)
            {
                throw new IOException("Could not persist browser project storage.", ex);
            }
        }

        private static Task<JSObject> EnsureLoadedAsync()
        {
            return _moduleTask ??= JSHost.ImportAsync(ModuleName, "/browserProjectStorage.js");
        }

        [JSImport("persistCurrentStorage", ModuleName)]
        private static partial Task PersistCurrentStorageAsync();
    }
}
