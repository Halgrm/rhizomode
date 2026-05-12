#nullable enable

using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// GraphContextのMonoBehaviourラッパー。シーン上でGraphContextのライフサイクルを管理する。
    /// </summary>
    public class GraphContextBehaviour : MonoBehaviour
    {
        private GraphContext? _context;

        /// <summary>
        /// GraphContextインスタンスへのアクセス。初回アクセス時に生成される。
        /// </summary>
        public GraphContext Context => _context ??= new GraphContext();

        private void OnDestroy()
        {
            _context?.Dispose();
        }
    }
}
