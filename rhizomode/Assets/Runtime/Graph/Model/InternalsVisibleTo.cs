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
// Transitional consumers (Phase 8 終了までに dispatcher/runtime 経由に移行):
//   - Rhizomode.XR: GameBootstrap が SceneObject 自動登録 + ScrollMenu Spawn 等で直接呼び出し
//   - Rhizomode.UI.GraphAdapter: GraphSaveLoadManager が LoadGraph 時に Clear を直接呼び出し
//   - Rhizomode.Interaction: NodeCreationHandler が直接 RegisterNode
//   これら 3 つは Phase 8 後段で NodeSpawnService / LoadGraphCommand / SpawnNodeIntent 経由に置換予定。

[assembly: InternalsVisibleTo("Rhizomode.Graph.Mutation")]
[assembly: InternalsVisibleTo("Rhizomode.Graph.Runtime")]
[assembly: InternalsVisibleTo("Rhizomode.Graph.Tests")]
[assembly: InternalsVisibleTo("Rhizomode.Core.Tests")]  // legacy Core test asmdef (Phase 1 で renamed 残り)
[assembly: InternalsVisibleTo("Rhizomode.XR")]
[assembly: InternalsVisibleTo("Rhizomode.UI.GraphAdapter")]
[assembly: InternalsVisibleTo("Rhizomode.Interaction")]
