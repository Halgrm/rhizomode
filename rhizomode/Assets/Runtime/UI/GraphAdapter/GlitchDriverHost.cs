#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Scene;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// Drives mirror-output glitch amount from GlitchNode instances in the graph.
    /// </summary>
    public sealed class GlitchDriverHost
    {
        private readonly GraphState _graphState;
        private MirrorOutputController? _mirror;

        public GlitchDriverHost(GraphState graphState)
        {
            _graphState = graphState;
        }

        public void Tick()
        {
            if (_graphState.IsDisposed) return;

            var mirror = GetMirror();
            if (mirror == null) return;

            mirror.SetGlitchAmount(GetMaxGlitchAmount());
        }

        private MirrorOutputController? GetMirror()
        {
            if (_mirror != null) return _mirror;
            _mirror = Object.FindFirstObjectByType<MirrorOutputController>();
            return _mirror;
        }

        private float GetMaxGlitchAmount()
        {
            var max = 0f;
            foreach (var node in _graphState.Nodes.Values)
            {
                if (node is not GlitchNode glitchNode) continue;
                max = Mathf.Max(max, glitchNode.CurrentAmount);
            }
            return max;
        }
    }
}
