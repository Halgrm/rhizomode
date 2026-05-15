#nullable enable

using Rhizomode.Input.Contracts;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// シーン上の <see cref="SceneObjectBridge"/> を検出して SceneObjectNode + visual を自動生成する
    /// post-Build wiring。Plan v5.4 §15 V-final (Vf-a): 旧 <c>GameBootstrap.RegisterSceneObjects</c> を
    /// Bootstrap asmdef へ移送。
    /// </summary>
    /// <remarks>
    /// 担当する配線:
    /// <list type="bullet">
    ///   <item><see cref="SceneObjectRegistrationService.RegisterTypeAndFactory"/></item>
    ///   <item><c>FindObjectsByType&lt;SceneObjectBridge&gt;</c> でシーンスキャン</item>
    ///   <item><see cref="SceneObjectRegistrationService.RegisterBridges"/> で SceneObjectNode 生成</item>
    ///   <item>各 result に対して <see cref="NodeVisualManager.CreateNodeVisual"/> + プレイヤー方向回転</item>
    /// </list>
    /// <see cref="Wire"/> は InteractionBootstrapWiring 完了後の eager step で駆動 — activeInput は
    /// visual rotation に使う。Wire 時に確定済 activeInput を受け取る。
    /// </remarks>
    public sealed class SceneObjectsBootstrapWiring
    {
        private readonly XrSceneReferences _refs;
        private readonly SceneObjectRegistrationService _service;

        private bool _wired;

        public SceneObjectsBootstrapWiring(
            XrSceneReferences refs,
            SceneObjectRegistrationService service)
        {
            _refs = refs;
            _service = service;
        }

        /// <summary>
        /// SceneObjectBridge を全検出し、対応するノードと visual を生成する。
        /// </summary>
        /// <param name="activeInput">visual rotation 用。Wire 時点で確定済を渡す。</param>
        public void Wire(IControllerInput? activeInput)
        {
            if (_wired) return;
            _wired = true;

            var visualManager = _refs.VisualManager;
            if (visualManager == null) return;

            _service.RegisterTypeAndFactory();

            var bridges = UnityEngine.Object.FindObjectsByType<SceneObjectBridge>(FindObjectsSortMode.None);
            var results = _service.RegisterBridges(bridges);

            foreach (var r in results)
            {
                var visual = visualManager.CreateNodeVisual(new NodeViewAdapter(r.Node), r.SpawnPosition);
                if (visual != null && activeInput != null)
                {
                    var headPos = activeInput.HeadPosition;
                    visual.transform.rotation = Quaternion.LookRotation(r.SpawnPosition - headPos);
                }
            }
        }
    }
}
