#nullable enable

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// GraphContextのMonoBehaviourラッパー。シーン上でGraphContextのライフサイクルを管理する。
    /// </summary>
    public class GraphContextBehaviour : MonoBehaviour
    {
        private GraphState? _context;

        /// <summary>
        /// GraphContextインスタンスへのアクセス。初回アクセス時に生成される。
        /// </summary>
        public GraphState Context => _context ??= new GraphState();

        private void OnDestroy()
        {
            _context?.Dispose();
        }
    }
}
