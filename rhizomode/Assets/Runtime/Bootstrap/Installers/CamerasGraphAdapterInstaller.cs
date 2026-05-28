#nullable enable

using Rhizomode.Bootstrap.EntryPoints;
using Rhizomode.Cameras.GraphAdapter;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// Registers camera graph adapter services.
    /// </summary>
    internal sealed class CamerasGraphAdapterInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<CameraDriverHost>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CameraDriverHostTickAdapter>(Lifetime.Singleton);
        }
    }
}
