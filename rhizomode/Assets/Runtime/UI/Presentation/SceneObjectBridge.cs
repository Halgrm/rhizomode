#nullable enable

using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// 任意のGameObjectにAddComponentするだけで、ノードグラフからTransformを制御可能にする。
    /// GameBootstrapが起動時に全SceneObjectBridgeを検出し、対応するノードを自動生成する。
    /// </summary>
    [AddComponentMenu("Rhizomode/Scene Object Bridge")]
    public class SceneObjectBridge : MonoBehaviour
    {
        [SerializeField] private bool exposePosition;
        [SerializeField] private bool exposeRotation;
        [SerializeField] private bool exposeScale = true;

        /// <summary>Position(X/Y/Z)をノードに公開するか。</summary>
        public bool ExposePosition => exposePosition;

        /// <summary>Rotation(X/Y/Z)をノードに公開するか。</summary>
        public bool ExposeRotation => exposeRotation;

        /// <summary>Scale(Size + X/Y/Z)をノードに公開するか。</summary>
        public bool ExposeScale => exposeScale;

        /// <summary>生成されたノードのID。GameBootstrapが設定する。</summary>
        public string? NodeId { get; set; }
    }
}
