#nullable enable

using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// ICameraMotion カメラの Motion 駆動ソース (グラフの Float 出力ポート) を耐久保持するデータホルダ。
    /// パネルの購読は transient なので、選択ソースの nodeId/portName をこのコンポーネントに記録し、
    /// カメラ切替・セーブ/ロードを越えて保持する。
    /// </summary>
    /// <remarks>
    /// このコンポーネント自体は購読を行わない (グラフ層に依存しない)。
    /// CameraManagerPanel が値を読み書きし、CameraStatePersistenceService がセーブ/ロードで往復させる。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CameraMotionSourceBinding : MonoBehaviour
    {
        [SerializeField] private string nodeId = "";
        [SerializeField] private string portName = "";

        /// <summary>駆動ソースのノード ID。未接続なら空文字。</summary>
        public string NodeId => nodeId;

        /// <summary>駆動ソースの出力ポート名。未接続なら空文字。</summary>
        public string PortName => portName;

        /// <summary>駆動ソースが設定されているか。</summary>
        public bool HasBinding => !string.IsNullOrEmpty(nodeId) && !string.IsNullOrEmpty(portName);

        /// <summary>駆動ソースを設定する。</summary>
        public void SetBinding(string sourceNodeId, string sourcePortName)
        {
            nodeId = sourceNodeId ?? "";
            portName = sourcePortName ?? "";
        }

        /// <summary>駆動ソースを解除する。</summary>
        public void Clear()
        {
            nodeId = "";
            portName = "";
        }
    }
}
