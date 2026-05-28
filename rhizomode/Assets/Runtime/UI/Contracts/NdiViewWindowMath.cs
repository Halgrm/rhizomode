#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// Deterministic placement math for NDI view windows.
    /// </summary>
    public static class NdiViewWindowMath
    {
        /// <summary>Number of deterministic cascade slots.</summary>
        public const int CascadeSlots = 8;

        /// <summary>Lateral slot spacing in meters.</summary>
        public const float SideSpacing = 0.35f;

        /// <summary>Depth slot spacing in meters.</summary>
        public const float DepthSpacing = 0.15f;

        private const float MaxSideOffset = 1.0f;

        /// <summary>FNV-1a 32-bit hash. Stable across sessions and processes.</summary>
        public static uint StableHash32(string s)
        {
            const uint OffsetBasis = 2166136261u;
            const uint Prime = 16777619u;
            if (string.IsNullOrEmpty(s)) return OffsetBasis;

            uint h = OffsetBasis;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= Prime;
            }

            return h;
        }

        /// <summary>
        /// Returns a local HMD-space cascade offset for the given node id.
        /// </summary>
        public static Vector3 CascadeOffset(string nodeId, Vector3 hmdForward, Vector3 hmdRight)
        {
            if (!IsFinite(hmdForward) || !IsFinite(hmdRight)) return Vector3.zero;
            if (hmdForward.sqrMagnitude < 1e-4f) return Vector3.zero;

            int idx = (int)(StableHash32(nodeId) % (uint)CascadeSlots);
            int sideMultiplier = (idx / 2) + 1;
            float sideMagnitude = Mathf.Min(SideSpacing * sideMultiplier, MaxSideOffset);
            float side = (idx % 2 == 0 ? 1f : -1f) * sideMagnitude;
            float depth = -DepthSpacing * (idx % 4);

            return hmdRight * side + hmdForward * depth;
        }

        private static bool IsFinite(Vector3 v)
            => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
