#nullable enable

using System;
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
    /// Plan v5.3 F-8.2 / F-8.7 prerequisite: 旧 GameBootstrap の private nested class
    /// BootstrapModulePlacement を Bootstrap asmdef に移送 (composition-root の責務分離)。
    /// IControllerInput を直接 inject せず、Func provider を受けることで GameBootstrap が
    /// `_activeInput` を遅延解決 (VR/desktop 切替後に確定するため) する事情を吸収する。
    ///
    /// FreshSpawn: head + forward * offset (VFX=1.5、Object3D=1.0)
    /// Deserialize: node.Position (snapshot 上の位置をそのまま使う)
    /// </remarks>
    public sealed class BootstrapModulePlacement : IModulePlacementService
    {
        private const float VfxForwardOffset = 1.5f;
        private const float Object3DForwardOffset = 1.0f;

        private readonly Func<IControllerInput?> _inputProvider;

        public BootstrapModulePlacement(Func<IControllerInput?> inputProvider)
        {
            _inputProvider = inputProvider;
        }

        public Vector3 GetSpawnPosition(NodeBase node, NodeInitMode mode)
        {
            var input = _inputProvider();
            if (mode != NodeInitMode.FreshSpawn || input == null)
                return node.Position;

            var headPos = input.HeadPosition;
            var headFwd = input.HeadForward;
            var offset = node is Object3DNode ? Object3DForwardOffset : VfxForwardOffset;
            return headPos + headFwd * offset;
        }
    }
}
