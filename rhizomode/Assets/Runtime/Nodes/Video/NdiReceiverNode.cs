#nullable enable

using System;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.Nodes.Video
{
    /// <summary>
    /// NDI ネットワーク経由で受信した映像をノード自体に表示する Input node。
    /// </summary>
    /// <remarks>
    /// ノードを置く → presenter (UI.Presentation) が Klak.Ndi.NdiReceiver + child Quad を attach し、
    /// 受信フレームを Quad のマテリアルに live blit する。複数 node を置けば複数 NDI source を
    /// 同時受信できる (各 node が独自 Klak.Ndi.NdiReceiver instance を持つ)。
    ///
    /// SourceName が空のときは presenter が <see cref="Klak.Ndi.NdiFinder"/> のソース一覧から
    /// auto-pick して <see cref="SetSourceName"/> を呼び戻す。
    ///
    /// 本 node は output port を持たない (ParamType に Texture が無いため node 間でテクスチャを
    /// 受け渡せない)。「ノードを置いたら表示」要件の最小実装。port 経由の連携は ParamType
    /// 拡張時に検討。
    /// </remarks>
    [NodeType("NdiReceiver", "NDI Receiver", NodeCategory.Input)]
    public class NdiReceiverNode : NodeBase, INdiReceiverNode
    {
        private string _sourceName = "";

        public NdiReceiverNode(string id) : base(id, "NdiReceiver")
        {
        }

        /// <inheritdoc/>
        public string SourceName => _sourceName;

        /// <inheritdoc/>
        public event Action<string>? OnSourceNameChanged;

        /// <inheritdoc/>
        public void SetSourceName(string sourceName)
        {
            var resolved = sourceName ?? "";
            if (_sourceName == resolved) return;
            _sourceName = resolved;
            try { OnSourceNameChanged?.Invoke(_sourceName); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverNode] OnSourceNameChanged handler threw: {e.Message}");
            }
        }

        public override void Setup(GraphState context)
        {
            // output port なし → 何もしない。Klak.NDI 受信は presenter (UI 側) が担当する。
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new NdiReceiverParams
            {
                sourceName = _sourceName
            });
            return data;
        }

        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<NdiReceiverParams>(paramsJson);
                if (p != null && p.sourceName != null)
                    _sourceName = p.sourceName;
            }
            catch (Exception)
            {
                // 破損 JSON は無視して default ("") のまま
            }
        }

        [Serializable]
        private class NdiReceiverParams
        {
            public string sourceName = "";
        }
    }
}
