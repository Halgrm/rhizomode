#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Core;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// 左手Xボタンによるノード生成メニューの呼び出しと、
    /// 選択後のノードインスタンス生成を管理する。
    /// </summary>
    public class NodeCreationHandler : MonoBehaviour
    {
        private const float NodeSpawnOffsetFromMenu = 0.3f;

        [SerializeField] private NodeCreationMenuController? menuController;
        [SerializeField] private NodeVisualManager? visualManager;
        [SerializeField] private GraphContextBehaviour? graphContext;

        private IControllerInput? _controllerInput;
        private IDisposable? _menuSubscription;
        private readonly Dictionary<string, Func<string, NodeBase>> _nodeFactories = new();

        /// <summary>
        /// コントローラー入力を設定し、メニュー呼び出しを購読する。
        /// </summary>
        public void Initialize(IControllerInput controllerInput)
        {
            _controllerInput = controllerInput;

            _menuSubscription = controllerInput.OnOpenMenu
                .Subscribe(_ => ToggleMenu());

            if (menuController != null)
            {
                menuController.OnNodeTypeSelected += OnNodeTypeSelected;
            }
        }

        /// <summary>
        /// ノードファクトリを登録する。メニュー選択後のノード生成に使用。
        /// </summary>
        public void RegisterNodeFactory(string nodeType, Func<string, NodeBase> factory)
        {
            _nodeFactories[nodeType] = factory;
        }

        private void ToggleMenu()
        {
            if (_controllerInput == null || menuController == null) return;

            if (menuController.IsVisible)
            {
                menuController.Hide();
            }
            else
            {
                menuController.Show(
                    _controllerInput.HeadPosition,
                    _controllerInput.HeadForward,
                    _controllerInput.HeadRotation
                );
            }
        }

        private void OnNodeTypeSelected(string nodeType)
        {
            if (graphContext == null || visualManager == null || _controllerInput == null)
            {
                Debug.LogWarning("[NodeCreationHandler] Not fully initialized.");
                return;
            }

            if (!_nodeFactories.TryGetValue(nodeType, out var factory))
            {
                Debug.LogWarning($"[NodeCreationHandler] No factory for node type: {nodeType}");
                return;
            }

            try
            {
                var nodeId = Guid.NewGuid().ToString();
                var node = factory(nodeId);

                // メニュー位置の少し前方にスポーン
                var headPos = _controllerInput.HeadPosition;
                var spawnPos = headPos + _controllerInput.HeadForward * NodeSpawnOffsetFromMenu;
                node.Position = spawnPos;

                graphContext.Context.RegisterNode(node);
                var visual = visualManager.CreateNodeVisual(node, spawnPos);

                // プレイヤーに向ける（Quadの-Z面が表）
                if (visual != null)
                {
                    visual.transform.rotation = Quaternion.LookRotation(spawnPos - headPos);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NodeCreationHandler] Node creation failed: {nodeType} — {e.Message}");
            }
        }

        private void OnDestroy()
        {
            _menuSubscription?.Dispose();

            if (menuController != null)
            {
                menuController.OnNodeTypeSelected -= OnNodeTypeSelected;
            }
        }
    }
}
