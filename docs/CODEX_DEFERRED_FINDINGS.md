# Codex Deferred Findings

Codex review loop で検出された軽微 (理論リスク / production-scale でのみ問題化 / overengineering 寄り) な指摘を集約。Phase 13 (final cleanup) または production scale issue が顕在化したタイミングで再評価する。

各エントリの形式:
- **検出 Phase / Loop**: いつ検出されたか
- **対象**: file:line
- **指摘内容**: Codex の判定
- **実害評価**: なぜ軽微と判断したか
- **将来 trigger**: いつ再評価すべきか

---

## Phase 8 (GameBootstrap 解体 + [Obsolete] 削除)

### F-8.2: GameBootstrap god-object 分割 (重大 → Phase 9 prerequisite に再分類)
- **検出**: Phase 8 Round B Codex 構造レビュー (`a6a0cd6903191dbe4`)
- **対象 (Phase 8 完了時点で 512 行に縮小)**: 旧 1370+ 行から 9 責務単位を 6 抽出 (Round C-F4) で 889 行 (~63%) 削減。残り 512 行の主要責務:
  - Awake / OnDestroy orchestration (~50 行)
  - [SerializeField] 群 (~70 行)
  - 内部 adapter class: `BootstrapModulePlacement` / `BootstrapObject3DRegistry` (~35 行)
  - `OnGraphLoaded` + `OnGraphLoadingHandler` (~50 行)
  - `OnScrollMenuNodeSelected` + `SpawnInputVisuals` + `BindObject3DProxyObservables` (~110 行)
  - `RegisterSceneObjects` (visual rebuild、~20 行)
  - 残りの薄い wrapper / Debug.Log / using 群
- **抽出済 (Round C-F4)**:
  - `NodeSpawnService` (graph mutation 部、Round C)
  - `SceneObjectRegistrationService` (Round D)
  - `NodeRegistrationOrchestrator` (NodeFactoryMap + 5 Register* メソッド、Round E)
  - `GraphAdapterWiring` (Factory + EventBus + Dispatcher + Translator + Persistence、Round F3)
  - `GameBootstrap.Ableton.cs` partial class (Ableton 11 メソッド、Round F2)
  - `GameBootstrap.SystemInit.cs` partial class (InitializeVerticalSliceSystems / AudioDeviceSelector / InteractionHandlers、Round F4)
- **指摘内容**: 重大 (Plan v5.3 の 80 行目標未達、現状 512 行 = ~6 倍超過)
- **実害評価**: 機能的には全 142 EditMode tests pass、Codex Axis 4 lifecycle 対称性も担保。ただし XR が "second composition root" 化しており、Bootstrap/Installer asmdef への移送が Phase 9 で必要。
- **将来 trigger (Phase 9 prerequisite として再分類)**: Phase 9 UI クラス分割の thin prerequisite slice として以下を先行抽出:
  - `BootstrapModulePlacement` / `BootstrapObject3DRegistry` を Bootstrap/Installer asmdef に移送
  - `OnGraphLoaded` の visual rebuild を `GraphLoadCoordinator` 等に分離
  - `OnScrollMenuNodeSelected` の visual 創出を `MenuNodeSpawnCoordinator` 等に分離 (NodeSpawnService の visual 側相棒)
  - これにより Phase 9 の UI Presentation クラス分割と並行して GameBootstrap が更に縮小される

### F-8.3: ModuleLifecycleProcessor の凝集肥大 (軽微)
- **検出**: Phase 8 Round B Codex 構造レビュー
- **対象**: `ModuleLifecycleProcessor.cs:55, 106, 141, 176, 35, 77, 89`
- **指摘内容**: AfterSetup + 3 種 instantiate + _instances + DestroyInstance + CleanupAll を一手に担う
- **実害評価**: 軽微。VFX/Shader/Object3D 凝集として acceptable
- **将来 trigger**: Object3DProxy 登録 / Collider 補完が肥大化したら Object3DLifecycleProcessor に分離

### F-8.4: NodeRuntime.State / EventBus internal expose (軽微)
- **検出**: Phase 8 Round B Codex 構造レビュー
- **対象**: `NodeRuntime.cs:96, 99` — HydrationPlanExecutor 都合で internal property 露出
- **指摘内容**: 内部実装の漏れ。BeginMutationScope() 等の明示 API に閉じる方が望ましい
- **実害評価**: 軽微。Graph.Runtime asmdef 内のみの visibility なので外部影響なし
- **将来 trigger**: Phase 9+ で NodeRuntime API 整理時に refactor

### F-8.5: GraphSaveLoadManager 責務多重 (軽微)
- **検出**: Phase 8 Round B Codex 構造レビュー
- **対象**: `GraphSaveLoadManager.cs:50, 116, 122, 126, 127, 132, 137, 145`
- **指摘内容**: I/O delegate + event 発火 + rollback + hydration orchestration を兼ねる
- **実害評価**: 軽微。"facade" として acceptable
- **将来 trigger**: GraphLoadUseCase / GraphSaveUseCase に load/save 各 transaction を抽出すれば facade を引き締められる (Phase 13 cleanup 候補)

### F-8.6: InternalsVisibleTo transitional grants の縮小 (軽微)
- **検出**: Phase 8 Round B Codex 構造レビュー
- **対象**: `InternalsVisibleTo.cs:21-23` (Core.Tests / XR / UI.GraphAdapter)
- **指摘内容**: XR の grant 範囲は具体 callsite より広い可能性あり、GameBootstrap 分割後に削除可否を確認
- **実害評価**: 軽微 (encapsulation の段階的縮小)
- **将来 trigger**:
  - GameBootstrap 分割 (F-8.2) 完了後に XR grant を削除可否確認
  - UI.GraphAdapter は Clear/MergePreset 用 command 経由に移行後に grant 削除
  - Core.Tests は legacy SerializationTests が残る間は正当

### F-8.7: Bootstrap nested adapter classes の ownership (軽微)
- **検出**: Phase 8 Round B Codex 構造レビュー
- **対象**: `GameBootstrap.cs:255 BootstrapModulePlacement, :275 BootstrapObject3DRegistry`
- **指摘内容**: Modules layer のインターフェースを XR layer のネストクラスで実装する構造は compile-safe だが、composition-root としては Bootstrap/Installer asmdef に移すのが自然
- **実害評価**: 軽微 (依存方向は妥当、配置のみの問題)
- **将来 trigger**: F-8.2 (GameBootstrap 分割) と同時に Bootstrap/Installer に移送

### F-8.8: SpawnInputVisuals の method-boundary mixing (軽微、Round C advisory)
- **検出**: Phase 8 Round C Codex review (`a2e7b092ebe2f1700`)
- **対象**: `GameBootstrap.cs:1266-1290` SpawnInputVisuals
- **指摘内容**: SpawnInputVisuals が `_nodeSpawnService.SpawnInputNodes(...)` で graph mutation を trigger し、その後 visual 作成を同じメソッド内で行う。separation は service 境界では達成されているが、caller method 内で graph mutation trigger と visual setup が混在
- **実害評価**: 軽微 (false positive 寄り)。SpawnInputVisuals は意図的に "spawn input nodes and create visuals for them" という coherent な処理単位。service 呼び出しと visual 作成を別メソッドに分けても可読性は向上しない (引数を InputSpawnResult リストで渡し合うだけになる)
- **将来 trigger**: Codex Round D+E review (`add2a30e993664a98`) で "pre-existing concern unchanged" と判定済 → Round D/E では悪化していない。Phase 9 で UI クラス分割する際に visual 作成部分が NodeVisualController 等に移送されたら自然に解消する可能性

### F-8.1: Codex の "Plan v5.x reference" false positive
- **検出**: Phase 8 Codex Loop 2 (`a3f8617e35567faf8`)
- **対象**: `GraphState.cs:17`, `GraphMutationApplier.cs:18, :22`
- **指摘内容**: doc comment 内の "Plan v5.3" 文字列を stale と判定
- **実害評価**: 誤検出。"Plan v5.3" は意図的な phase reference (どの計画版で書かれた comment か、どの phase で更新済かを示す trace)。コードを reviewer 視点で読みやすくするためのマーカーであり、削除はむしろ情報損失。
- **将来 trigger**: Plan v5.3 → v6.x へ計画自体が改訂された時に一括更新する (現状は v5.3 のまま運用)

## Phase 7 (Persistence + Hydrator)

### F-7.1: SaveGraph tmp pattern over-match risk
- **検出**: Phase 7 Codex Loop 5 (`a42675e573993b329`)
- **対象**: `JsonGraphRepository.cs:39` SweepOrphanTmpFiles
- **指摘内容**: `*.tmp-*` glob がファイル名に `.tmp-` を含む正規セーブファイルを誤検出する理論上の可能性
- **実害評価**: 軽微。SaveGraph は `<file>.json.tmp-{guid}` のみ生成、ユーザーが `live_set_x.tmp-foo.json` のようなファイル名を意図的に作らない限り誤検出ゼロ。最終的に Phase 7 Loop 5 で `*.json.tmp-*` に precise pattern 化済 (commit `<NEXT>`)。
- **将来 trigger**: 既に Loop 5 で resolve 済 → このエントリは記録のみ。

### F-7.2: SweepOrphanTmpFiles 同期 + 件数上限なし
- **検出**: Phase 7 Codex Loop 5
- **対象**: `JsonGraphRepository.cs:39-40` SweepOrphanTmpFiles
- **指摘内容**: tmp ファイルが大量にある場合、ctor で同期 sweep がメインスレッドをブロックする。タイムアウト・件数上限なし。
- **実害評価**: 低。ローカル開発フェーズではセーブ数は数十件、tmp 残骸も数件レベル。ms オーダーで完了する。
- **将来 trigger**: 
  - Cloud sync / 共有ストレージで SaveDirectoryPath が複数プロセス共有になった場合
  - ユーザーが 1000+ セーブを保有するヘビーユースケース
  - 起動時間プロファイルで sweep が顕著に出始めたら

### F-7.3: ctor sweep と SaveGraph の race (silent data loss)
- **検出**: Phase 7 Codex Loop 5
- **対象**: `JsonGraphRepository.cs:39, 42, 63`
- **指摘内容**: ctor sweep が SaveGraph 進行中の tmp を削除し、catch スワローで silent data loss が発生する可能性
- **実害評価**: 極小。Repository ctor は MonoBehaviour.Awake で 1 度のみ呼ばれ、SaveGraph はユーザー UI 操作 (Start 後) でしか起動しない → 並列実行経路が存在しない。
- **将来 trigger**:
  - Phase 10 (Audio 細分化) で background thread SaveGraph が導入された場合
  - 複数 Repository instance が同一 SaveDirectoryPath を共有する設計に変わった場合 (Phase 8 Installer で 1 instance に固定する想定だが、要確認)

---

## Phase 11 (Path Editor + GrabPoseSolver)

### F-11.1: MiniaturePathMapper.boxSize 上限クランプ未実装 (理論 risk)
- **検出**: Phase 11 Codex Loop 2 (fix commit `71b57b75` 後の re-review)
- **対象**: `MiniaturePathMapper.cs:53-54` (ctor)
- **指摘内容**: `boxSize = Mathf.Max(MinValidExtent, boxSize)` で下限クランプは入ったが、上限クランプはない。`boxSize` が極端に大きく、`maxExtent` が `MinValidExtent` (= 0.0001f) に張り付いた場合、`scale = boxSize / maxExtent` が Infinity / NaN にオーバーフローし得る。
- **実害評価**: 極小。`boxSize` は `PathControlPointVisualManager.miniatureBoxSize` の SerializeField で default 1.0、現実的範囲は 0.1〜10。Infinity overflow には `boxSize > 3.4e+38` (float.MaxValue / 0.0001f) が必要で、inspector で意図的に設定しない限り発火不可能。Loop 1 で MAJOR 扱いされた divide-by-zero (default 値で発火) とは性質が異なる。memory `feedback_codex_review_per_phase` 記載の "loop 半永久化パターン" に該当 (Phase 7 Loop 5 と同型)。
- **将来 trigger**:
  - inspector で boxSize を異常値に設定するワークフロー (例: editor script で programmatic に設定) が現れた場合
  - Phase 13 final cleanup で `[Range]` attribute による inspector 範囲固定と合わせて対応

---

## Phase 12 (Singleton 解消 + AbletonOscBridge 3 分割 + HealthAggregator wiring)

### F-12.1: node Setup() の transport 未注入時 警告文言の変更 (意図的・revert 不要)
- **検出**: Phase 12 Codex review (MINOR ×6)
- **対象**: `OscReceiverNode.cs:54` / `MidiCCNode.cs:54` / `AbletonTransportNode.cs:42` /
  `AbletonTempoNode.cs:42` / `AbletonTrackVolumeNode.cs:53` / `AbletonClipFireNode.cs:65`
- **指摘内容**: 旧 `"OscServer not found..."` → 新 `"OscSource not injected..."` 等、
  transport 未注入時の警告ログ文言が変わった。Codex は「挙動完全不変の break」と判定。
- **実害評価**: なし。**意図的な改善**。Phase 12 で singleton lookup (`OscServer.Instance`)
  を撤廃し LifecycleProcessor 注入に切り替えたため、"not found" は不正確で
  "not injected" の方が新アーキテクチャを正確に表す。ログ grep に依存した運用も現状なし。
- **将来 trigger**: なし。**revert しない**。本エントリは「意図的変更であり Codex 指摘を
  承知の上で据え置いた」記録のみ。Phase 13 cleanup でも対応不要。

---

## V3a (XrSceneReferences + Audio/OscMidi/Ableton Installers)

V3a は `GameBootstrap.Ableton.cs` (~380 行) を `AbletonBootstrapWiring` へ **verbatim 移送** する
sub-step。Codex review が以下 4 件を FAIL 判定したが、いずれも `git show HEAD~:...Ableton.cs` と
照合した結果 **移送前から存在する pre-existing 条件であり V3a の regression ではない**。
faithful move の原則に従い据え置き、Ableton wiring の本格的な SRP 整理 (元 `GameBootstrap.Ableton.cs`
の comment が示唆する `AbletonBootstrapCoordinator` 化) を行う専用タイミングで再評価する。

### F-V3a.1: AbletonBootstrapWiring の panel イベント購読が Dispose で解除されない
- **検出**: V3a Codex review
- **対象**: `AbletonBootstrapWiring.cs` Wire() 内の `_abletonSetupPanel.OnConnectRequested +=` /
  `_abletonControlPanel.On*  +=` (inline lambda)、`Dispose()`
- **指摘内容**: 全 `+=` が inline lambda で対応する `-=` がない。container-owned singleton が
  panel を強参照し続ける。
- **実害評価**: 軽微・pre-existing。旧 `GameBootstrap.Ableton.cs` でも同一 lambda 購読で、
  旧 `GameBootstrap.OnDestroy` も abletonSetupPanel/abletonControlPanel の購読解除はしていなかった。
  wiring object・panel・scope GameObject は全てシーンアンロードで同時破棄されるため session 内 leak なし。
- **将来 trigger**: AbletonBootstrapCoordinator 化で named Action フィールド + Dispose 解除に整理。

### F-V3a.2: SpawnAbletonOuterFrame の Material instance が破棄されない
- **検出**: V3a Codex review
- **対象**: `AbletonBootstrapWiring.SpawnAbletonOuterFrame` `new Material(...)` → `sharedMaterial`
- **指摘内容**: 再 Connect 時・Dispose 時に Material instance が破棄されず leak。
- **実害評価**: 軽微・pre-existing。旧 `GameBootstrap.Ableton.cs:373` で同一コード。再 Connect は
  ライブ中に数回程度。
- **将来 trigger**: `Material? _abletonOuterFrameMaterialInstance` フィールド化 + 置換/Dispose 時に Destroy。

### F-V3a.3: 非同期 OnConnectRequested に CancellationToken / 再入ガードなし
- **検出**: V3a Codex review
- **対象**: `AbletonBootstrapWiring.cs` Wire() 内 `OnConnectRequested += async (h,sp,rp) => {...}`
- **指摘内容**: `await` 跨ぎで disposed チェックなし、Connect 連打で layout query / grid spawn が
  並行実行され得る。
- **実害評価**: 軽微・pre-existing。旧 `GameBootstrap.Ableton.cs:33` で同一。Connect は
  起動時 setup panel 上の単発操作で実運用上の連打経路は薄い。
- **将来 trigger**: AbletonBootstrapCoordinator 化で `SemaphoreSlim(1)` + CancellationTokenSource。

### F-V3a.4: health subscription と HealthAggregator の dispose 順序非対称
- **検出**: V3a Codex review (再掲)
- **対象**: `GameBootstrap.OnDestroy` — `_compositionRoot.Dispose()` が先、`_healthSubscription.Dispose()` が後
- **指摘内容**: scope dispose が HealthAggregator の Subject を先に破棄し、その後 subscription を破棄する。
- **実害評価**: なし・**既知 documented**。V2a 時点の `GameBootstrap.OnDestroy` コメントで既に
  「R3 は disposed Subject への購読解放を no-op として許容」と明記済。V3a で OnDestroy のこの部分は未変更。
- **将来 trigger**: V3d で StatusPanel subscription を container 側へ移管した際に対称化。

---

## V-final sub-step a (GameBootstrap god-object 解体 + Bootstrap.Services / Wiring 新設)

Vf-a で GameBootstrap.cs を 379 → 50 行 (87% 削減) の薄い shim 化、残置責務すべてを
Bootstrap.Services / Bootstrap.Wiring に移送。Codex review (`ad3f33ebd5fdf111b`) で
5/7 PASS、2 件の軽微 FAIL を以下に deferred 記録する。

### F-Vf-a.1: Bootstrap.Services 内のビジネスロジック (§15 transitional violation) **[RESOLVED 2026-05-15]**
- **検出**: Vf-a Codex review
- **対象 (解消前)**: `Bootstrap/Services/NodeSpawnService.cs`, `SceneObjectRegistrationService.cs`,
  `MenuNodeSpawnCoordinator.cs`, `GraphLoadCoordinator.cs`, `Object3DProxyBindService.cs`
- **指摘内容**: Plan v5.4 §15 「Bootstrap は業務ロジック禁止」に違反。グラフ変異 / UI visual 生成 /
  Object3D Proxy bind 等のビジネスロジックが Bootstrap asmdef 内に集約されている。
- **解消**: 4 phase に分けて各 service を本来の所属 asmdef へ細分化:
  - **Phase A**: `GraphLoadCoordinator` + `MenuNodeSpawnCoordinator` → `Rhizomode.UI.GraphAdapter`
    (namespace Rhizomode.UI)。同時に `InputSpawnResult` record を UI.GraphAdapter へ持ち上げ、
    `MenuNodeSpawnCoordinator` の `NodeSpawnService` 直接依存を解消 (caller が
    `SpawnInputNodes` を呼び結果を渡す形に refactor)。
  - **Phase B**: `Object3DProxyBindService` → `Rhizomode.Modules.Runtime` (namespace Rhizomode.Modules)。
    `GraphContextBehaviour` (Rhizomode.UI) 依存を `GraphState` 直接注入に変更し、UI 依存を Modules
    層に持ち込まない構造にした。
  - **Phase C**: `SceneObjectRegistrationService` → `Rhizomode.Scene.GraphAdapter`
    (namespace Rhizomode.Scene.GraphAdapter)。Scene.GraphAdapter asmdef refs に NodeCatalog.Contracts/
    Runtime + Nodes.Scene + UI.Presentation を追加。
  - **Phase D**: `NodeSpawnService` → `Rhizomode.Interaction` (namespace Rhizomode.Interaction)。
    Interaction asmdef refs に Graph.Runtime + UI.GraphAdapter を追加。IGraphCommand 経由化は
    `F-Vf-d.1` として deferred (NodeCatalog.Contracts ParamType→typeName mapping +
    ModuleNodeBase.IsEvent の抽象化 + ConstFloat/ConstColor 初期値再発行 hook を要するため別 phase)。
  - **Final**: `Bootstrap/Services/` ディレクトリ削除、各 Installer (UIGraphAdapter / Modules /
    Scene / Interaction) で再登録。XRInstaller の責務は VR 入力配線 wiring のみに純化。
- **§15 適合**: Bootstrap asmdef は Installers / Wiring / ITickable adapter のみを保持する状態に到達。

### F-Vf-d.2: F-Vf-d.1 Codex review 指摘 5 件 **[RESOLVED 2026-05-16]**
- **検出**: F-Vf-d.1 完了後 Codex review (commit 78f9c505 を対象)
- **対象 (解消前)**: `Interaction/NodeSpawnService.cs` + 周辺 DTO / scope 機構
- **指摘 5 件と解消内容**:
  - **#1 EDGE_IDENTITY (WARN)**: `InputSpawnResult.PrimaryEdge` / `TriggerEdge` が `Edge` 直接参照
    → 新 record `SpawnedEdgeInfo` (EdgeId + endpoints) を `UI.GraphAdapter` に追加し、
    Snapshot 復元後の stale reference リスクを排除。
  - **#3 NON_ATOMIC_MULTI_DISPATCH (FAIL)**: `AddNode` + `ConnectPorts` 連投で途中失敗時の
    rollback がなかった → `GraphCommandScope` (`Graph.Mutation`) + `Applier.TryApply` を追加し、
    1 入力 spawn 単位 (Const+Edge、Toggle+Trigger+2Edges) を atomic に扱う。失敗時は entry
    snapshot に rollback。Commit は単一 Undo entry (`CompositeCommand` marker) を積む。
  - **#4 COMMANDORIGIN_INVARIANT (FAIL)**: `NodeSpawnService` が `Rhizomode.Interaction` から
    `CommandOrigin.Interaction` を発行 (invariant 違反) → file を
    `Interaction/GraphAdapter/NodeSpawnService.cs` に移送、namespace を
    `Rhizomode.Interaction.GraphAdapter` に変更。Installer 登録も
    `InteractionGraphAdapterInstaller` に移送。`Interaction` asmdef から `Graph.Mutation`
    参照を除去 (本来不要だった)。
  - **#5 TYPENAME_DOUBLE_DEFINITION (WARN)**: `NodeSpawnService` の `"Toggle"` / `"Trigger"`
    定数が `ParamTypeNodeMap` と独立 → `ParamTypeNodeMap.GetSourceDescriptor` を
    `SourceNodeDescriptor` (TypeName + OutputPortName + TriggerSourceTypeName 等) に拡張、
    全 typeName / port 名解決を 1 箇所に集約。`NodeSpawnService` から typeName 定数を削除。
  - **#6 TEST_COVERAGE_GAP (FAIL)**: EditMode test 未整備 → 13 件追加:
    - `Tests/Editor/Graph/GraphCommandScopeTests.cs` (5 件): atomic commit / 失敗時 rollback /
      Dispose 時 rollback / Undo 復元 / 失敗後 TryExecute 拒否
    - `Tests/Editor/Interaction/NodeSpawnServiceTests.cs` (8 件): 既知 typeName spawn /
      未知 typeName 失敗 / Float port spawn + edge / Bool port + Toggle + Trigger / event port
      skip / PrimeInitialEmission subscriber 受信 / Menu spawn Undo / Input spawn ごと Undo
- **CI invariant 状況**: feedback_command_origin の Roslyn invariant test は本 phase でも未実装
  (post-launch 検討)。ファイル位置 + namespace で boundary を明示することで、当面は visual
  inspection ベース。
- **§13 + §15 適合**: 全 graph mutation IGraphCommand 経由 + atomic transaction + GraphAdapter
  boundary 整合の 3 つを完全達成。
- **検証**: EditMode 155/155 + PlayMode 1/1 PASS、warnings 0。

### F-Vf-d.1: NodeSpawnService の IGraphCommand 経由化 **[RESOLVED 2026-05-16]**
- **検出**: F-Vf-a.1 Phase D 移送時に identify
- **対象 (解消前)**: `Interaction/NodeSpawnService.cs`
- **指摘内容**: NodeSpawnService は `NodeRuntime.RegisterNode` + `AddEdge` を直接呼び、
  `IGraphCommand` (AddNodeCommand / ConnectPortsCommand) 経由になっていなかった。Plan v5.4 の
  「全 graph mutation は IGraphCommand 経由」原則 (§13) に対する transitional 違反。
- **解消**: 5 step 構成で `GraphCommandDispatcher` 経由に置換:
  - **Step 1**: `NodeBase.IsInputPortEvent(string portName)` virtual を追加 (default false)、
    `ModuleNodeBase` で `Definition.IsEvent(portName)` を返す override を実装。
    NodeSpawnService から `ModuleNodeBase` への具体依存を解消。
  - **Step 2**: `NodeBase.PrimeInitialEmission()` virtual を追加 (default no-op)、
    `ConstFloatNode` / `ConstColorNode` で `_valueOut.Emit(...)` を呼ぶ override を実装。
    旧 `cf.Value = cf.Value` setter ハックを抽象 API に置換。
  - **Step 3**: `Rhizomode.NodeCatalog.Contracts.ParamTypeNodeMap.GetSourceTypeName(ParamType)`
    static helper を追加 (Float→ConstFloat / Color→ConstColor / Bool→Toggle)。
    Interaction asmdef は Nodes.* 具体型を knowing せず typeName 文字列で AddNodeCommand を発行可能に。
  - **Step 4**: NodeSpawnService の ctor を `(GraphState, GraphCommandDispatcher)` に変更、
    `TrySpawnFromMenu` / `SpawnInputNodes` 内で `AddNodeCommand` + `ConnectPortsCommand` を
    `CommandOrigin.Interaction` 付きで発行。`Interaction` asmdef refs に `Rhizomode.Graph.Mutation`
    を追加。Toggle/Trigger 分岐は typeName 文字列ベース。FindEdge は edge id 一致で逆引き。
  - **Step 5**: EditMode 142/142 + PlayMode 1/1 PASS、warnings 0 で commit。
- **§13 適合**: 全 graph mutation が IGraphCommand + CommandAuditLog 経由に統合された。Plan v5.4 §13
  「全 mutation IGraphCommand 経由」原則を完全達成。新 record (`AddNodeFromMenuCommand` /
  `AutoSpawnInputsCommand`) は不要 (既存 `AddNodeCommand` + `ConnectPortsCommand` 連投で代替)。

### F-Vf-d.3: CommandAuditLog の transactional rollback **[RESOLVED 2026-05-16]**
- **検出**: F-Vf-d.2 Codex re-review (commit 498266a9 + b129a4c2 後の 軽微 WARN 残)
- **対象 (解消前)**: `Graph.Mutation/CommandAuditLog.cs`, `GraphCommandScope.TryExecute/Dispose`, `GraphCommandDispatcher.RecordScopeUndoEntry`
- **指摘内容**: `GraphCommandScope` の rollback は graph state のみ復元し、`CommandAuditLog` の sub-command
  Record は巻き戻されなかった (失敗 scope の sub-command 群が audit log に残る)。また `CompositeCommand`
  marker が audit Record されないため、audit trace に scope boundary が見えなかった。
- **解消内容**:
  - `CommandAuditLog.SaveCheckpoint() / RollbackToCheckpoint(int)` (internal) を追加。trace +
    countByOrigin + byKindOrigin の 3 つを整合させて巻き戻し、count が 0 になったら entry 削除。
  - `GraphCommandScope.ctor` で `auditLog.SaveCheckpoint()` を `_auditCheckpoint` に保存。
  - `GraphCommandScope.TryExecute` 失敗時 + `Dispose` 暗黙 rollback 時に `RollbackToCheckpoint`
    を呼んで graph state と audit log を同時に巻き戻す。
  - `GraphCommandScope.Commit` で `_auditLog.Record(marker)` を追加 (scope boundary を 1 entry として記録)。
- **テスト追加** (3 件、`GraphCommandScopeTests`):
  - `Scope_FailedRollback_AuditAlsoRolledBack`: 失敗 scope の audit が rollback されること
  - `Scope_Committed_AuditContainsCompositeCommandMarker`: 成功時 last entry が CompositeCommand
  - `Scope_DisposeWithoutCommit_AuditRolledBack`: 暗黙 rollback も audit を巻き戻す
- **検証**: EditMode 161/161 + PlayMode 1/1 PASS、warnings 0。

### F-Vf-c.1: VerticalSliceBootstrapWiring.Dispose に edit-mode listener 解除が欠落 — 解消済 (2026-05-16)
- **検出**: Vf-c Codex review (3 度目の試行、commit `1a0ba0fb`、`abeacbe1eda90a1c5`)
- **対象**: `Bootstrap/Wiring/VerticalSliceBootstrapWiring.cs:67-72` Wire 内
  `cameraManagerPanel.AddEditModeListener(isEditing => { ... })` の lambda 登録に対し、
  `Dispose` 側 (line 112-116) では `_healthSubscription.Dispose()` のみで listener 解除がない。
- **fix 内容 (2026-05-16)**:
  - `CameraManagerPanelController.Path.cs` に `RemoveEditModeListener(Action<bool>)` API を新設
    (`_editModeListeners.Remove(listener)`)。
  - `VerticalSliceBootstrapWiring` に `_editModePanel` / `_editModeListener` field を追加し、
    Wire 時に lambda を field 保持して登録、`Dispose` 時に `RemoveEditModeListener` で解除する。
- **過去の実害評価 (記録のため残置)**: 軽微 (V3d 由来の既存条件・regression ではない)。
  scene-bound entity 同士の関係で listener leak は scene の lifetime 内に閉じていた。
  ただし別 LifetimeScope を導入してシーン unload より早い phase で wiring を破棄する設計に
  変わった場合の予防策として fix 済。

### F-Vf-a.2: VisualManager / EdgeVisualManager 欠落時の VContainer exception 露出
- **検出**: Vf-a Codex review
- **対象**: `RootLifetimeScope.Configure` (NodeVisualManager / EdgeVisualManager 条件登録) +
  `EntryPointBootstrapper.Launch` (MenuNodeSpawnCoordinator / GraphLoadCoordinator 等を無条件 resolve)
- **指摘内容**: scene refs が NodeVisualManager / EdgeVisualManager を持たない場合、
  `Configure` が `RegisterInstance` をスキップするが、`Launch` が constructor injection を
  要求するサービス群を無条件 `container.Resolve<>` で取得するため VContainer resolve exception が
  露出する。旧 GameBootstrap の guarded construction パスからの軽い regression。
- **実害評価**: Low-Medium。SampleScene では VisualManager / EdgeVisualManager は常に配線済 (実害なし)。
  degraded scene (テスト fixture や別シーン) を作る場合に exception で boot 失敗。
- **将来 trigger**:
  - GameBootstrap.Awake で sceneRefs.VisualManager / EdgeVisualManager の null チェックを追加し、
    null なら degraded boot として `_compositionRoot = null` のまま return する
  - または RootLifetimeScope.Configure で VisualManager / EdgeVisualManager を hard requirement として
    `throw new InvalidOperationException` し、Configure 直前で diagnostic を出す
  - Vf-c (RootLifetimeScope シーン直接配置) で sceneRefs の hard requirement を整理する際に同時対応

### F-Vf-e.1: BoundaryValidator 限定有効化の段階拡張 (2026-05-16 追記)
- **対象**: `Assets/Editor/BoundaryViolationValidator.cs` の有効 rule 数
- **背景**: shell `scripts/verify-asmdef-boundaries.sh` の planned full rule 集合と突合した結果、
  現状違反なしの 2 rule (`Audio.Analysis` / `Ableton.Session`) が Editor 側で未有効化だった
  ことが判明。2026-05-16 に Rule 5b/5c として有効化し EnabledRuleCount を 8 → 10 に bump。
- **残存 (Phase 5/7 cleanup 後に有効化予定、いずれも現状実違反あり)**:
  - `Audio.GraphAdapter ⊄ UI.Presentation` (`Audio/GraphAdapter/Rhizomode.Audio.GraphAdapter.asmdef:12`)
  - `Rhizomode.Interaction ⊄ Graph.*` (`Interaction/Rhizomode.Interaction.asmdef:10-12` で
    Graph.Model / Graph.Runtime / Graph.Serialization を参照)
  - `Rhizomode.UI.Presentation ⊄ NodeCatalog.Runtime` (`UI/Presentation/Rhizomode.UI.Presentation.asmdef:9`)
- **将来 trigger**: Phase 5 で Interaction.GraphAdapter / UI への完全分離が済んだら、それぞれ追加。

### F-Vf-e.2: CommandOrigin invariant の source-scan CI 実装 — 実装済 (2026-05-16)
- **対象**: `Assets/Tests/Editor/Graph/CommandOriginAuditTests.cs`
- **背景**: memory `feedback_command_origin.md` で約束していた「Boundary CI で Origin を検証」が
  F-Vf-d.2 (Interaction.GraphAdapter 物理移送) 後も未実装で残っていた。Roslyn は重い + Unity から
  扱いにくいため、`Application.dataPath` + `Directory.GetFiles` + regex の source-scan で代用。
- **検証内容**:
  - 各 `Runtime/*/GraphAdapter` 配下 .cs に出現する `CommandOrigin.X` が adapter の期待値と一致 (`[TestCase]`)
  - `CommandOrigin.Test` が Runtime 配下で使われていない
  - 行頭 `//`/`///` の comment 行は scan 対象外 (XML doc cref の false positive を防止)
- **将来 trigger**: 「同一 command kind が 2 種類以上の Origin から発行されたら warning」と
  「`CommandOrigin.X` の網羅性チェック」は未実装 (memory 約束の Adapter 重複 warning に相当)。
  Adapter 数が増えるか duplicate emission が検出されたタイミングで Roslyn / runtime-replay 方式に拡張。

---

## modules-review (2026-05-17, commit a10d8662 → 後続 fix)

### F-mr.1: InstancedCubesModule の compute / shader asset 未追跡 (deferred)
- **検出**: Codex re-review (a10d8662) PARTIAL 2
- **対象**: `Assets/Prefabs/Modules/InstanceCubesModule.prefab:47-48` — compute (guid `4bab4dac7921b354793d04caa5b82c12`) と shader (guid `562c3efa5af1fc148ac204504aa3a5b1`) の参照先 (`Assets/Sandbox/...` / `Assets/Shaders/Content/Glass/disp/dispshad_instanced.shader`) が untracked
- **指摘内容**: クリーン checkout では参照欠落、`InstancedCubesModule.Activate()` が "compute or shader not assigned" で停止
- **実害評価**: 軽微 — module 単体が停止するだけで Video 全体は止まらない (映像継続原則準拠の degrade)。ローカル開発者は assets を持っているため動作。
- **将来 trigger**: InstancedCubes を production module として配布する判断時に Sandbox/ + Shaders/Content/Glass/disp/ を tracked に移送 + .meta 含めてコミット。それまでは個人の Sandbox として残置可。

### F-mr.2: ModuleDefinition.prefab 未設定時の factory 未登録 (deferred)
- **検出**: Codex review (d2316a46) PARTIAL 4 / re-review (a10d8662) WARN 3
- **対象**: `Assets/Runtime/Bootstrap/NodeRegistrationOrchestrator.cs:140-150`
- **指摘内容**: prefab 未設定 SO は `Module_X` / legacy alias の factory が登録されないため、saved graph に該当 typeName があると load 失敗
- **実害評価**: 現状 `Assets/Data/SavedGraphs/` は空。production deployment 前に SO 全部に prefab を割当する運用前提なら問題なし。
- **将来 trigger**: saved graph 機能が再有効化されるタイミングで migration pass を実装するか、prefab missing factory も canonical typeName だけ登録する fallback を追加。



- 新規エントリは `### F-<phase>.<番号>: <タイトル>` 形式で追加
- Phase 13 (final cleanup) で本ファイルを総点検し、各エントリの最終判断 (修正 / 永続 deferral / 削除) を決める
- "将来 trigger" が現実化したら速やかに当該エントリを修正対応に格上げする

---

## LookAt Phase 2-A (2026-05-18, Codex review loop)

### F-LookAt.1: Place fallback distance の実効性不足
- **検出**: LookAt Phase 2-A Codex re-review (1 loop 後)
- **対象**: `rhizomode/Assets/Runtime/Interaction/LookAtMarkerPlaceHandler.cs:73-75` (TryPlace の fallback 経路)
- **指摘内容**: raycast hit が無いときの fallback 位置を `Mathf.Max(FallbackPlaceDistance=1.5f, FallbackMinDistance=0.5f)` で計算するが、定数のままだと `Max` は実質 no-op で、コントローラ前方ベクトルが体内方向 (HMD 後方) を向いた場合の対策になっていない
- **実害評価**: 通常 VR 利用では右手コントローラが体を向くケースは少なく、起きても marker を再 grab で動かせる。映像配信に直接影響しない。fix には HMD forward / コントローラ direction の dot 判定など追加ロジックが必要で複雑化
- **将来 trigger**: live 中に「自分の頭に marker が刺さる」体験が報告されたら fix に格上げ。HMD forward は `IControllerInput.HeadForward` / `HeadPosition` で取れるので、内積 ≤ 0 なら直近の有効方向 (memo: 前回 fallback direction) または HMD forward に置換する形が候補
