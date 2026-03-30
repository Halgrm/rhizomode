#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Core;
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
        private const float DefaultNodeWidth = 0.20f;
        private const float DefaultNodeHeight = 0.12f;

        [SerializeField] private VisualTreeAsset? nodeUxml;
        [SerializeField] private VisualTreeAsset? portUxml;
        [SerializeField] private StyleSheet? nodeStyleSheet;

        private readonly Dictionary<string, NodeVisualController> _visuals = new();
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
                var go = CreateNodeGameObject(node.Id, spawnPosition);
                var panelHost = go.GetComponent<WorldPanelHost>();
                panelHost.Initialize(nodeUxml, nodeStyleSheet);

                var controller = go.GetComponent<NodeVisualController>();
                controller.Bind(node, typeInfo);

                _visuals[node.Id] = controller;
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
        }

        private GameObject CreateNodeGameObject(string nodeId, Vector3 position)
        {
            var go = new GameObject($"Node_{nodeId}");
            go.transform.position = position;

            // 必要なコンポーネントを追加
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            var panelHost = go.AddComponent<WorldPanelHost>();
            var controller = go.AddComponent<NodeVisualController>();

            // portUxmlをSerializeFieldで設定できないため、直接参照
            if (portUxml != null)
            {
                // NodeVisualControllerのportUxmlはSerializeFieldなので、
                // ランタイム生成時はリフレクション回避のためfallbackを使用
            }

            return go;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
