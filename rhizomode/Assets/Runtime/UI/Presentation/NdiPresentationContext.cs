#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// Static bridge for NDI presenter dependencies supplied by bootstrap wiring.
    /// </summary>
    public static class NdiPresentationContext
    {
        /// <summary><see cref="NdiReceiverPresenter"/> health monitor singleton.</summary>
        public static NdiReceiverHealth? Health { get; set; }

        /// <summary>Registry and factory for NDI view windows.</summary>
        public static NdiWindowsRoot? WindowsRoot { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            Reset();
        }

        /// <summary>Publishes bootstrap-owned NDI presentation dependencies.</summary>
        public static void Reinitialize(NdiWindowsRoot? root, NdiReceiverHealth? health)
        {
            WindowsRoot = root;
            Health = health;
        }

        /// <summary>Restores the windows root reference after domain reload clears static state.</summary>
        public static NdiWindowsRoot? EnsureResolved()
        {
            Health ??= new NdiReceiverHealth();
            if (WindowsRoot != null) return WindowsRoot;
            WindowsRoot = Object.FindFirstObjectByType<NdiWindowsRoot>();
            return WindowsRoot;
        }

        /// <summary>Clears static state between EditMode tests.</summary>
        public static void Reset()
        {
            Health = null;
            WindowsRoot = null;
        }
    }
}
