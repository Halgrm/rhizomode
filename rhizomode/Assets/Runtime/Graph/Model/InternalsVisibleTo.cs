#nullable enable

using System.Runtime.CompilerServices;

// Plan v5.3 Phase 8: GraphState の mutation メソッド (RegisterNode/RemoveNode/TryConnect/
// Disconnect/Clear) を internal 化するため、正当な consumer asmdef を InternalsVisibleTo で許可。
//
// 正規 consumers (Plan v5.3 で許可):
//   - Rhizomode.Graph.Mutation: GraphMutationApplier が IGraphCommand を実行する唯一の経路
//   - Rhizomode.Graph.Runtime: NodeRuntime が BeforeSetup → Setup → AfterSetup orchestration
//   - Rhizomode.Graph.Tests: EditMode 単体テスト
//
// Transitional consumers:
//   - Rhizomode.UI.GraphAdapter: PresetManager が MergePreset を直接呼び出し
//     (将来的に NodeRuntime + HydrationPlan 経由化で撤去予定)
//   - (Rhizomode.Interaction は Round B で NodeCreationHandler 削除により撤去済)
//   - (Rhizomode.XR は Round F4 で撤去 — 全 mutation 経路が NodeRuntime / NodeSpawnService 経由化済)

[assembly: InternalsVisibleTo("Rhizomode.Graph.Mutation")]
[assembly: InternalsVisibleTo("Rhizomode.Graph.Runtime")]
[assembly: InternalsVisibleTo("Rhizomode.Graph.Tests")]
[assembly: InternalsVisibleTo("Rhizomode.Core.Tests")]  // legacy Core test asmdef (Phase 1 で renamed 残り)
[assembly: InternalsVisibleTo("Rhizomode.UI.GraphAdapter")]
