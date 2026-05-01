using System;
using System.Reflection;

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

        public virtual IEffectGlInterface Create(object platformGlInterface)
        {
            ArgumentNullException.ThrowIfNull(platformGlInterface);

            MethodInfo getProcAddress = platformGlInterface.GetType().GetMethod(
                "GetProcAddress",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: [typeof(string)],
                modifiers: null);

            if (getProcAddress == null || getProcAddress.ReturnType != typeof(IntPtr))
            {
                throw new InvalidOperationException(
                    $"Platform GL interface '{platformGlInterface.GetType().FullName}' does not expose GetProcAddress(string).");
            }

            return new ReflectionProcAddressEffectGlInterface(
                _api,
                _backendName,
                name => (IntPtr)getProcAddress.Invoke(platformGlInterface, [name]));
        }

        private sealed class ReflectionProcAddressEffectGlInterface : ProcAddressEffectGlInterface
        {
            public ReflectionProcAddressEffectGlInterface(
                EffectGlApi api,
                string backendName,
                Func<string, IntPtr> getProcAddress)
                : base(api, backendName, getProcAddress)
            {
            }
        }
    }
}
