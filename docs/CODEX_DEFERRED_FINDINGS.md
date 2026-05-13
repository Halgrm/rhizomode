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

## 運用

- 新規エントリは `### F-<phase>.<番号>: <タイトル>` 形式で追加
- Phase 13 (final cleanup) で本ファイルを総点検し、各エントリの最終判断 (修正 / 永続 deferral / 削除) を決める
- "将来 trigger" が現実化したら速やかに当該エントリを修正対応に格上げする
