#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.CatalogBridge
{
    /// <summary>
    /// <see cref="GraphState"/> 内部の <c>_nodeFactories</c> / <c>_nodeFactoriesWithParams</c> dictionary を
    /// <see cref="INodeFactory"/> contract として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Codex BUG #5 fix (2026-05-16): <see cref="Bootstrap.NodeRegistrationOrchestrator"/> が動的 typeName
    /// (SceneDark/SceneWhite/SceneNature/VFX_*/Shader_*/Object3D_*) を <see cref="GraphState.RegisterNodeFactory"/>
    /// で GraphState 内部 dict のみに登録していたため、<see cref="Graph.Mutation.GraphMutationApplier"/> が
    /// 使う <see cref="INodeFactory"/> 経由のメニュー spawn / Snapshot restore / hydration で
    /// これらの型が生成できなかった。本 adapter を <see cref="NodeCatalog.Runtime.CompositeNodeFactory"/> に
    /// 含めることで、GraphState dict 側の登録も INodeFactory contract に露出する。
    ///
    /// 参照は lazy: <see cref="GraphState"/> インスタンスを保持して問い合わせ時にクエリするため、
    /// adapter 構築時点で dict が空でも、後続の RegisterNodeFactory 呼び出しが反映される
    /// (composite が installer 段階で構築され、Orchestrator.RegisterAll は Launch 後に走るため必須)。
    /// </remarks>
    public sealed class GraphStateBackedNodeFactory : INodeFactory
    {
        private readonly GraphState _graphState;

        public GraphStateBackedNodeFactory(GraphState graphState)
        {
            _graphState = graphState;
        }

        public bool CanCreate(string typeName) => _graphState.HasFactoryFor(typeName);

        public NodeBase? Create(string typeName, string nodeId) =>
            _graphState.CreateNodeWithId(typeName, nodeId);

        /// <summary>
        /// <see cref="NodeCatalog.Runtime.CompositeNodeFactory.DetectDuplicateTypeNames"/> が
        /// reflection で参照する optional プロパティ。
        /// </summary>
        public IEnumerable<string> AllTypeNames => _graphState.RegisteredFactoryTypeNames;
    }
}
