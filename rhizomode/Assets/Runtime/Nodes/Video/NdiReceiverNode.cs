#nullable enable

using System;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.SharedKernel;
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
    public class NdiReceiverNode : NodeBase, INdiReceiverNode, INdiViewWindowState, IInlineMonitor
    {
        private string _sourceName = "";

        // window transform side-channel (paramsJson に乗る、Plan v0.3)
        private Vector3 _windowPosition;
        private Vector3 _windowEulerAngles;
        private float _windowScale = 1.0f;
        private bool _hasExplicitWindowTransform;
        private bool _hideFromMirror;

        public NdiReceiverNode(string id) : base(id, "NdiReceiver")
        {
        }

        /// <inheritdoc/>
        public string SourceName => _sourceName;

        /// <inheritdoc/>
        public event Action<string>? OnSourceNameChanged;

        /// <inheritdoc cref="IInlineMonitor.MonitorType"/>
        public ParamType MonitorType => ParamType.Float;

        /// <inheritdoc cref="IInlineMonitor.MonitorDisplayValue"/>
        /// <remarks>
        /// node panel に source name + Active 状態を表示する property UI。
        /// 空 source name の場合は "(searching)" を出して presenter の auto-pick 中であることを示す。
        /// </remarks>
        public string MonitorDisplayValue
        {
            get
            {
                if (string.IsNullOrEmpty(_sourceName))
                    return "(searching)";
                if (_hideFromMirror)
                    return _sourceName + "  [hidden]";
                return _sourceName;
            }
        }

        /// <inheritdoc cref="IInlineMonitor.MonitorColor"/>
        /// <remarks>Float monitor として使うので Color は未使用 (white 固定)。</remarks>
        public Color MonitorColor => Color.white;

        /// <inheritdoc cref="INdiViewWindowState.WindowPosition"/>
        public Vector3 WindowPosition => _windowPosition;

        /// <inheritdoc cref="INdiViewWindowState.WindowEulerAngles"/>
        public Vector3 WindowEulerAngles => _windowEulerAngles;

        /// <inheritdoc cref="INdiViewWindowState.WindowScale"/>
        public float WindowScale => _windowScale;

        /// <inheritdoc cref="INdiViewWindowState.HasExplicitWindowTransform"/>
        public bool HasExplicitWindowTransform => _hasExplicitWindowTransform;

        /// <inheritdoc cref="INdiViewWindowState.HideFromMirror"/>
        public bool HideFromMirror => _hideFromMirror;

        /// <inheritdoc cref="INdiViewWindowState.OnWindowTransformChanged"/>
        public event Action? OnWindowTransformChanged;

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

        /// <inheritdoc cref="INdiViewWindowState.SetWindowTransform"/>
        public void SetWindowTransform(Vector3 position, Vector3 eulerAngles, float scale)
        {
            // NaN/Inf 防御 (Klak.NDI / grab handle のバグで Infinity が来てもグラフ全体を壊さない)
            if (!IsFinite(position) || !IsFinite(eulerAngles) || !float.IsFinite(scale)) return;
            _windowPosition = position;
            _windowEulerAngles = eulerAngles;
            _windowScale = scale;
            _hasExplicitWindowTransform = true;
            try { OnWindowTransformChanged?.Invoke(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverNode] OnWindowTransformChanged handler threw: {e.Message}");
            }
        }

        /// <inheritdoc cref="INdiViewWindowState.SetHideFromMirror"/>
        public void SetHideFromMirror(bool hide)
        {
            if (_hideFromMirror == hide) return;
            _hideFromMirror = hide;
            try { OnWindowTransformChanged?.Invoke(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverNode] OnWindowTransformChanged handler threw: {e.Message}");
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
                sourceName = _sourceName,
                windowPosition = _windowPosition,
                windowEulerAngles = _windowEulerAngles,
                windowScale = _windowScale,
                hasExplicitWindowTransform = _hasExplicitWindowTransform,
                hideFromMirror = _hideFromMirror,
            });
            return data;
        }

        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<NdiReceiverParams>(paramsJson);
                if (p == null) return;
                if (p.sourceName != null) _sourceName = p.sourceName;
                // 旧 cue (sourceName のみ) は hasExplicitWindowTransform=false で初期化済みのまま →
                // factory が HMD-forward + cascade fallback を採用する (Plan v0.3 §paramsJson)。
                _windowPosition = p.windowPosition;
                _windowEulerAngles = p.windowEulerAngles;
                _windowScale = p.windowScale > 0f ? p.windowScale : 1f;
                _hasExplicitWindowTransform = p.hasExplicitWindowTransform;
                _hideFromMirror = p.hideFromMirror;
            }
            catch (Exception)
            {
                // 破損 JSON は無視して default のまま
            }
        }

        private static bool IsFinite(Vector3 v)
            => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        [Serializable]
        private class NdiReceiverParams
        {
            public string sourceName = "";
            // Plan v0.3 で追加。旧 cue では欠落 → JsonUtility が default 値を入れる:
            //  Vector3 = (0,0,0)、float = 0、bool = false。これを presenter が
            //  HasExplicitWindowTransform で見分けて fallback を適用する。
            public Vector3 windowPosition;
            public Vector3 windowEulerAngles;
            public float windowScale = 1.0f;
            public bool hasExplicitWindowTransform;
            public bool hideFromMirror;
        }
    }
}
