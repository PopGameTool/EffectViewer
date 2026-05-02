using Avalonia.OpenGL;
using System;

namespace EffectViewer.Rendering.Gl
{
    public class ProcAddressEffectGlInterfaceFactory : IEffectGlInterfaceFactory
    {
        private readonly EffectGlApi _api;
        private readonly string _backendName;

        public ProcAddressEffectGlInterfaceFactory(EffectGlApi api, string backendName)
        {
            _api = api;
            _backendName = backendName;
        }

        public virtual IEffectGlInterface Create(GlInterface platformGlInterface)
        {
            ArgumentNullException.ThrowIfNull(platformGlInterface);

            return new AvaloniaProcAddressEffectGlInterface(
                _api,
                _backendName,
                platformGlInterface.GetProcAddress);
        }

        private sealed class AvaloniaProcAddressEffectGlInterface : ProcAddressEffectGlInterface
        {
            public AvaloniaProcAddressEffectGlInterface(
                EffectGlApi api,
                string backendName,
                Func<string, IntPtr> getProcAddress)
                : base(api, backendName, getProcAddress)
            {
            }
        }
    }
}
