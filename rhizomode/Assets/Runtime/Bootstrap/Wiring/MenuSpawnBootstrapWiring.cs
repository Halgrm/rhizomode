#nullable enable

using Rhizomode.Input.Contracts;
using Rhizomode.Interaction;
using Rhizomode.Modules;
using Rhizomode.Nodes.Modules;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// ScrollMenu のノード選択コールバックを受け取り、graph mutation + visual 創出 + Object3D Proxy bind を
    /// 一括で結ぶ post-Build wiring。Bootstrap.Wiring は §15 で許容される orchestration 層 — 実体ロジックは
    /// 別 asmdef に分散している。
    /// </summary>
    /// <remarks>
    /// 担当する配線 (F-Vf-a.1 完了後の所属層):
    /// <list type="bullet">
    ///   <item><see cref="NodeSpawnService.TrySpawnFromMenu"/> — Rhizomode.Interaction (graph mutation)</item>
    ///   <item>ScrollMenu.CloseMenu (spawn 成功時)</item>
    ///   <item><see cref="Object3DProxyBindService.Bind"/> — Rhizomode.Modules.Runtime (Object3D node の場合)</item>
    ///   <item><see cref="MenuNodeSpawnCoordinator.CreatePrimaryVisual"/> +
    ///     <see cref="MenuNodeSpawnCoordinator.SpawnInputVisuals"/> — Rhizomode.UI.GraphAdapter (visual)</item>
    /// </list>
    /// <see cref="InteractionBootstrapWiring"/> が ScrollMenu の OnNodeTypeSelected += に
    /// <see cref="HandleSelection"/> を渡すため、本 wiring 自体に Wire メソッドはなく、activeInput のみ
    /// <see cref="SetActiveInput"/> で後付け注入する (Interaction.Wire 完了後に activeInput が確定する)。
    /// </remarks>
    public sealed class MenuSpawnBootstrapWiring
    {
        private readonly XrSceneReferences _refs;
        private readonly NodeSpawnService _nodeSpawnService;
        private readonly MenuNodeSpawnCoordinator _menuCoordinator;
        private readonly Object3DProxyBindService _proxyBindService;

        private IControllerInput? _activeInput;

        public MenuSpawnBootstrapWiring(
            XrSceneReferences refs,
            NodeSpawnService nodeSpawnService,
            MenuNodeSpawnCoordinator menuCoordinator,
            Object3DProxyBindService proxyBindService)
        {
            _refs = refs;
            _nodeSpawnService = nodeSpawnService;
            _menuCoordinator = menuCoordinator;
            _proxyBindService = proxyBindService;
        }

        /// <summary>
        /// InteractionBootstrapWiring.Wire 完了後に確定する activeInput を後付け注入する。
        /// </summary>
        public void SetActiveInput(IControllerInput? activeInput)
        {
            _activeInput = activeInput;
        }

        /// <summary>
        /// ScrollMenu の OnNodeTypeSelected コールバック。InteractionBootstrapWiring が
        /// scrollMenuVisual.OnNodeTypeSelected += に渡す。
        /// </summary>
        public void HandleSelection(string nodeType)
        {
            var graphContext = _refs.GraphContext;
            if (graphContext == null || _activeInput == null) return;
            if (_refs.VisualManager == null) return;

            Debug.Log($"[MenuSpawnBootstrapWiring] HandleSelection: {nodeType}");

            var headPos = _activeInput.HeadPosition;
            var headFwd = _activeInput.HeadForward;
            var spawnResult = _nodeSpawnService.TrySpawnFromMenu(nodeType, headPos, headFwd);
            if (spawnResult == null) return;

            // ノード生成後にスクロールメニューを閉じる
            _refs.ScrollMenuInteraction?.CloseMenu();

            // Object3D の Proxy 観測 bind
            if (spawnResult.Node is Object3DNode obj3d) _proxyBindService.Bind(obj3d);

            // visual 創出 + 入力ノード自動 spawn (graph mutation) + その visual 構築。
            // Plan v5.4 §15 F-Vf-a.1 Phase A/D: graph mutation (SpawnInputNodes) は Rhizomode.Interaction が担当、
            // visual creation (SpawnInputVisuals) は Rhizomode.UI.GraphAdapter が担当 — 結果データ
            // (InputSpawnResult list) のみ本 wiring が橋渡しする。
            _menuCoordinator.CreatePrimaryVisual(spawnResult.Node, spawnResult.Position, headPos);
            var inputResults = _nodeSpawnService.SpawnInputNodes(spawnResult.Node, spawnResult.Position, headPos);
            _menuCoordinator.SpawnInputVisuals(inputResults, headPos);

            Debug.Log($"[MenuSpawnBootstrapWiring] Node setup complete: {spawnResult.Node.NodeType}");
        }
    }
}
