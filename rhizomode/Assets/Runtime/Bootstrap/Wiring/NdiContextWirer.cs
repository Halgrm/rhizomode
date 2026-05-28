#nullable enable

using System;
using Rhizomode.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// Publishes bootstrap-owned NDI dependencies to the UI presentation context.
    /// </summary>
    internal sealed class NdiContextWirer : IInitializable
    {
        private readonly IObjectResolver _resolver;

        public NdiContextWirer(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public void Initialize()
        {
            var health = TryResolve<NdiReceiverHealth>();
            var windowsRoot = TryResolve<NdiWindowsRoot>();
            NdiPresentationContext.Reinitialize(windowsRoot, health);

            if (health == null)
                Debug.LogWarning("[NdiContextWirer] NdiReceiverHealth not registered.");
            if (windowsRoot == null)
                Debug.LogWarning("[NdiContextWirer] NdiWindowsRoot not registered.");
        }

        private T? TryResolve<T>() where T : class
        {
            try { return _resolver.Resolve<T>(); }
            catch (Exception) { return null; }
        }
    }
}
