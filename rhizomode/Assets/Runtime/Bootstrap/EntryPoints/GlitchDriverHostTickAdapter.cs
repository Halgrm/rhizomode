#nullable enable

using Rhizomode.UI;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.EntryPoints
{
    /// <summary>
    /// VContainer ITickable adapter for GlitchDriverHost.
    /// </summary>
    public sealed class GlitchDriverHostTickAdapter : ITickable
    {
        private readonly GlitchDriverHost _host;

        public GlitchDriverHostTickAdapter(GlitchDriverHost host)
        {
            _host = host;
        }

        public void Tick() => _host.Tick();
    }
}
