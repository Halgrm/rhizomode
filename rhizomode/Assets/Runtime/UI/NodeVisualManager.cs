#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// GraphContext内の全ノードの視覚化を管理する。ノード追加・削除に応じて
    /// WorldSpaceパネルの生成・破棄を行う。
    /// </summary>
    public class NodeVisualManager : MonoBehaviour
    {
        [Header("ノードサイズ")]
        [SerializeField, Range(0.05f, 0.5f), Tooltip("ノードパネルのワールド幅（メートル）")]
        private float defaultNodeWidth = 0.18f;

        [SerializeField, Range(0.03f, 0.3f), Tooltip("ノードパネルのワールド高さ（メートル）")]
        private float defaultNodeHeight = 0.10f;

        [Header("テクスチャ解像度")]
        [SerializeField, Range(128, 1024), Tooltip("ノードテクスチャの幅（ピクセル）")]
        private int textureWidth = 384;

        [Header("レイアウト設定（ピクセル）")]
        [SerializeField, Range(16, 64), Tooltip("ヘッダー高さ")]
        private int headerPixelHeight = 32;

        [SerializeField, Range(16, 48), Tooltip("ポート行の高さ")]
        private int portRowPixelHeight = 28;

        [SerializeField, Range(4, 24), Tooltip("ポートコンテナのパディング")]
        private int portContainerPadding = 12;

        [SerializeField, Range(16, 64), Tooltip("インライン要素の高さ")]
        private int inlineElementPixelHeight = 30;

        [SerializeField, Range(40, 200), Tooltip("最小テクスチャ高さ")]
        private int minTextureHeight = 80;

        [SerializeField] private VisualTreeAsset? nodeUxml;
        [SerializeField] private VisualTreeAsset? portUxml;
        [SerializeField] private StyleSheet? nodeStyleSheet;
        [SerializeField] private PanelSettings? panelSettingsTemplate;

        private readonly Dictionary<string, NodeVisualController> _visuals = new();
        private readonly Dictionary<Collider, NodeVisualController> _colliderToVisual = new();
        private NodeTypeRegistry? _typeRegistry;

        /// <summary>全ノードVisualへの読み取り専用アクセス。</summary>
        public IReadOnlyDictionary<string, NodeVisualController> Visuals => _visuals;

        /// <summary>
        /// NodeTypeRegistryを設定する。
        /// </summary>
        public void Initialize(NodeTypeRegistry typeRegistry)
        {
            _typeRegistry = typeRegistry;
        }

        /// <summary>
        /// ノードの視覚表現を生成する。
        /// </summary>
        public NodeVisualController? CreateNodeVisual(NodeBase node, Vector3 spawnPosition)
        {
            if (_typeRegistry == null || nodeUxml == null)
            {
                Debug.LogError("[NodeVisualManager] Not initialized or nodeUxml not set.");
                return null;
            }

            var typeInfo = _typeRegistry.GetInfo(node.NodeType);
            if (typeInfo == null)
            {
                Debug.LogWarning($"[NodeVisualManager] Unknown node type: {node.NodeType}");
                typeInfo = new NodeTypeInfo(node.NodeType, node.NodeType, NodeCategory.Utility);
            }

            try
            {
                // ポート数＋インライン要素からパネルサイズを算出
                var (texHeight, worldHeight) = CalculateNodeSize(node);

                var go = CreateNodeGameObject(node.Id, spawnPosition);
                var panelHost = go.GetComponent<WorldPanelHost>();
                panelHost.Initialize(nodeUxml, nodeStyleSheet, textureWidth, texHeight);
                panelHost.Resize(defaultNodeWidth, worldHeight);

                var controller = go.GetComponent<NodeVisualController>();
                controller.Bind(node, typeInfo);

                _visuals[node.Id] = controller;

                // Collider→Visual逆引きキャッシュに登録
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    _colliderToVisual[collider] = controller;

                return controller;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NodeVisualManager] Failed to create visual for {node.Id}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// ノードの視覚表現を破棄する。
        /// </summary>
        public void DestroyNodeVisual(string nodeId)
        {
            if (!_visuals.TryGetValue(nodeId, out var controller))
                return;

            _visuals.Remove(nodeId);

            if (controller != null && controller.gameObject != null)
            {
                // Colliderキャッシュからも除去
                var collider = controller.GetComponent<Collider>();
                if (collider != null)
                    _colliderToVisual.Remove(collider);

                Destroy(controller.gameObject);
            }
        }

        /// <summary>
        /// ノードIDからVisualControllerを取得する。
        /// </summary>
        public NodeVisualController? GetVisual(string nodeId)
        {
            return _visuals.TryGetValue(nodeId, out var controller) ? controller : null;
        }

        /// <summary>
        /// ColliderからVisualControllerを取得する。GetComponentを毎フレーム呼ぶ代わりに使用。
        /// </summary>
        public NodeVisualController? GetVisualByCollider(Collider collider)
        {
            return _colliderToVisual.TryGetValue(collider, out var controller) ? controller : null;
        }

        /// <summary>
        /// 全ノードVisualを破棄する。
        /// </summary>
        public void Clear()
        {
            foreach (var controller in _visuals.Values)
            {
                if (controller != null && controller.gameObject != null)
                    Destroy(controller.gameObject);
            }
            _visuals.Clear();
            _colliderToVisual.Clear();
        }

        /// <summary>
        /// 全ビジュアルを再構築する。グラフロード後にGraphContextの全ノードから再生成する。
        /// </summary>
        public void RebuildAllVisuals(GraphState context)
        {
            Clear();

            foreach (var node in context.Nodes.Values)
            {
                var spawnPos = node.Position;
                CreateNodeVisual(node, spawnPos);
            }
        }

        /// <summary>
        /// ノードのポート数とインライン要素からテクスチャ高さ・ワールド高さを算出する。
        /// </summary>
        private (int texHeight, float worldHeight) CalculateNodeSize(NodeBase node)
        {
            var ports = node.GetPortDefinitions();
            int inputCount = 0;
            int outputCount = 0;
            foreach (var p in ports)
            {
                if (p.direction == PortDirection.Input) inputCount++;
                else outputCount++;
            }

            // 入力・出力は横並びなので、多い方の行数
            int portRows = Mathf.Max(inputCount, outputCount);

            int inlineCount = 0;
            if (node is IInlineSlider) inlineCount++;
            if (node is IInlineButton) inlineCount++;
            if (node is IInlineMonitor) inlineCount++;
            if (node is IInlineColorPicker) inlineCount += 3; // H/S/Vスライダー + プレビュー

            int pixelHeight = headerPixelHeight
                              + portContainerPadding
                              + portRows * portRowPixelHeight
                              + inlineCount * inlineElementPixelHeight;

            pixelHeight = Mathf.Max(pixelHeight, minTextureHeight);

            // ワールド高さ: 幅を基準にアスペクト比で算出
            float worldHeight = defaultNodeWidth * ((float)pixelHeight / textureWidth);

            return (pixelHeight, worldHeight);
        }

        private GameObject CreateNodeGameObject(string nodeId, Vector3 position)
        {
            var go = new GameObject($"Node_{nodeId}");
            go.transform.position = position;

            // 必要なコンポーネントを追加
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();

            // NOTE: XRGrabInteractable removed — XRI trigger-select conflicts with
            // EdgeDragHandler's trigger-based edge connection. Grab will be reimplemented
            // via custom NodeGrabHandler using Grip button (OnGrab).

            var panelHost = go.AddComponent<WorldPanelHost>();
            if (panelSettingsTemplate != null)
            {
                panelHost.PanelSettingsTemplate = panelSettingsTemplate;
            }

            go.AddComponent<WorldPanelRayBridge>();
            go.AddComponent<NodeVisualController>();

            return go;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
