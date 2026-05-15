#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Modules;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Object3D Proxy の Observable 購読を <see cref="Object3DNode"/> に張る service。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 F-Vf-a.1 Phase B: 旧 Rhizomode.Bootstrap.Object3DProxyBindService を
    /// Modules.Runtime asmdef へ移送。同時に <c>GraphContextBehaviour</c> (Rhizomode.UI) 依存を
    /// <see cref="GraphState"/> 直接注入に置き換え、UI 依存を Modules 層に持ち込まない構造にした。
    ///
    /// Prefab 生成 + IPerformanceModule 注入は <see cref="ModuleLifecycleProcessor"/> が担当。
    /// 本サービスは GraphState 依存の Proxy 観測 bind のみを担う。
    ///
    /// 旧 <c>GameBootstrap.BindObject3DProxyObservables</c> を service 化したもの。
    /// </remarks>
    public sealed class Object3DProxyBindService
    {
        private readonly GraphState _graphState;
        private readonly ModuleLifecycleProcessor _moduleProcessor;

        public Object3DProxyBindService(
            GraphState graphState,
            ModuleLifecycleProcessor moduleProcessor)
        {
            _graphState = graphState;
            _moduleProcessor = moduleProcessor;
        }

        public void Bind(Object3DNode node)
        {
            if (!_moduleProcessor.Instances.TryGetValue(node.Id, out var instance)) return;
            if (instance == null) return;

            var proxy = instance.GetComponent<Object3DProxy>();
            if (proxy == null) return;

            node.BindProxyObservables(_graphState, proxy.Position, proxy.Scale);
        }

        public void BindAllInGraph()
        {
            foreach (var node in _graphState.Nodes.Values)
            {
                if (node is Object3DNode object3DNode)
                    Bind(object3DNode);
            }
        }
    }
}
