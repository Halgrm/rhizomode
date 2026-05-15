#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Input.Contracts;
using Rhizomode.Modules;
using Rhizomode.Nodes.Modules;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// Module/Object3D Prefab の world 配置位置を head pose から解決する <see cref="IModulePlacementService"/>。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 V-final (Vf-b): 旧 ctor の <c>Func&lt;IControllerInput?&gt;</c> closure を廃止し、
    /// <see cref="SetActiveInput"/> による後付け注入に refactor。これにより本サービスは container 登録可能な
    /// プレーンな singleton になり、<see cref="EntryPointBootstrapper.Launch"/> 内で local closure を
    /// 生成する必要がなくなる (BootstrapObject3DRegistry も同様)。
    ///
    /// activeInput は <see cref="InteractionBootstrapWiring.Wire"/> 完了後に確定するため、
    /// container resolve 直後ではなく Launch の eager step の最後に <see cref="SetActiveInput"/> を呼ぶ
    /// プロトコルが必要。activeInput が未設定の間 (degraded scene 含む) は <c>FreshSpawn</c> でも
    /// <c>node.Position</c> をそのまま返す (Live 配置失敗より静かに動く方を選ぶ)。
    ///
    /// FreshSpawn: head + forward * offset (VFX=1.5、Object3D=1.0)
    /// Deserialize: node.Position (snapshot 上の位置をそのまま使う)
    /// </remarks>
    public sealed class BootstrapModulePlacement : IModulePlacementService
    {
        private const float VfxForwardOffset = 1.5f;
        private const float Object3DForwardOffset = 1.0f;

        private IControllerInput? _activeInput;

        /// <summary>
        /// <see cref="InteractionBootstrapWiring.Wire"/> 完了後の eager step で呼ぶ。
        /// 以降の <see cref="GetSpawnPosition"/> が head pose を参照できるようになる。
        /// </summary>
        public void SetActiveInput(IControllerInput? activeInput)
        {
            _activeInput = activeInput;
        }

        public Vector3 GetSpawnPosition(NodeBase node, NodeInitMode mode)
        {
            if (mode != NodeInitMode.FreshSpawn || _activeInput == null)
                return node.Position;

            var headPos = _activeInput.HeadPosition;
            var headFwd = _activeInput.HeadForward;
            var offset = node is Object3DNode ? Object3DForwardOffset : VfxForwardOffset;
            return headPos + headFwd * offset;
        }
    }
}
