#nullable enable

using Rhizomode.Cameras.GraphAdapter;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.EntryPoints
{
    /// <summary>
    /// VContainer ITickable adapter for CameraDriverHost.
    /// </summary>
    public sealed class CameraDriverHostTickAdapter : ITickable
    {
        private readonly CameraDriverHost _host;

        public CameraDriverHostTickAdapter(CameraDriverHost host)
        {
            _host = host;
        }

        public void Tick() => _host.Tick();
    }
}
