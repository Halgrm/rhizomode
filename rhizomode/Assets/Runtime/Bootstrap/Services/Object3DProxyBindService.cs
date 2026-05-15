#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using Rhizomode.Nodes.Modules;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// Object3D Proxy の Observable 購読を <see cref="Object3DNode"/> に張る service。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 V-final (Vf-a): 旧 <c>GameBootstrap.BindObject3DProxyObservables</c> を service 化。
    /// Prefab 生成 + IPerformanceModule 注入は <see cref="ModuleLifecycleProcessor"/> が担当。
    /// 本サービスは GraphState 依存の Proxy 観測 bind のみを担う (Modules layer に GraphState 依存を
    /// 持ち込まないため Bootstrap 配下に置く)。
    ///
    /// <see cref="GraphSaveLoadBootstrapWiring"/> (OnGraphLoaded) と
    /// <see cref="MenuSpawnBootstrapWiring"/> (OnScrollMenuNodeSelected) が共有する。
    /// </remarks>
    public sealed class Object3DProxyBindService
    {
        private readonly GraphContextBehaviour _graphContext;
        private readonly ModuleLifecycleProcessor _moduleProcessor;

        public Object3DProxyBindService(
            GraphContextBehaviour graphContext,
            ModuleLifecycleProcessor moduleProcessor)
        {
            _graphContext = graphContext;
            _moduleProcessor = moduleProcessor;
        }

        public void Bind(Object3DNode node)
        {
            if (!_moduleProcessor.Instances.TryGetValue(node.Id, out var instance)) return;
            if (instance == null) return;

            var proxy = instance.GetComponent<Object3DProxy>();
            if (proxy == null) return;

            node.BindProxyObservables(_graphContext.Context, proxy.Position, proxy.Scale);
        }

        public void BindAllInGraph()
        {
            var ctx = _graphContext.Context;
            foreach (var node in ctx.Nodes.Values)
            {
                if (node is Object3DNode object3DNode)
                    Bind(object3DNode);
            }
        }
    }
}
