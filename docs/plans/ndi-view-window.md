# NDI View Window Separation — Design Plan v0.3

> v0.1 → v0.2: Codex review round 1 (FAIL 1 + WARN 8) を反映。
> v0.2 → v0.3: Codex confirm round 2 (3 WARN) を反映。
> 主な追加:
> - cascade hash を `string.GetHashCode()` 不使用の FNV-1a 32bit に固定 (session 間 stable)
> - cue switch hook の所有者 (`CueLoadCoordinator`) と発火順序を明示
> - asmdef 依存図を追加し `IXrHmdReference` の `Rhizomode.Input.Contracts` 配置が無循環であることを示す

## Goal

NDI 受信表示の責務を以下の 2 つに分離する:

- **NdiReceiverNode + NodeVisual (panel)**: source name の選択 / 接続状態の表示 / Active toggle 等 **property の編集と監視のみ**
- **NdiViewWindow (新)**: 独立 GameObject。中央 (HMD 正面) に出現、VR で **grabbable + 2-hand scale**、**transform を cue / graph save に永続化**

## 現状

- `NdiReceiverNode` (`Rhizomode.Nodes.Video`): pure data node、`SourceName` + event
- `NdiReceiverPresenter` (`Rhizomode.UI.Presentation`): node visual に attach、preview Quad を child GameObject として生成し Klak.NDI の `targetRenderer` に流す
- `INdiReceiverNode` (`Rhizomode.UI.Contracts`): 文字列のみ、UI/native 依存無し
- 永続化: `NdiReceiverParams { sourceName }` のみが paramsJson に乗る

## 問題

1. preview が node に密結合 → node 移動・縮尺で preview も連動
2. 観客 / ステージ全体で見るには小さ過ぎ、サイズ調整不可
3. 複数 NDI source 同時受信時にノード群と画面が一緒に散らかる
4. window 位置が cue 保存対象外

## 設計

### Architecture overview

```
NdiReceiverNode  (data, implements both interfaces)
   ├─ INdiReceiverNode             (existing: SourceName + event)
   └─ INdiViewWindowState  (NEW)   (window transform side-channel)

NdiReceiverPresenter
   ├─ node.AsNdiReceiver() / AsNdiViewWindowState() で 2 つの contract を取得
   ├─ NdiViewWindowFactory.Create(node, windowsRoot, hmdRef) で window 生成
   ├─ Klak.NDI receiver の targetRenderer = window.Renderer
   └─ preview Quad 生成は撤去

NdiWindowsRoot  (NEW, persistent in SampleScene)
   └─ 全 NdiViewWindow の親、registry を持つ

NdiViewWindow  (NEW, UI.Presentation)
   ├─ Quad mesh + MeshRenderer + BoxCollider
   ├─ WindowGrabHandle
   └─ OnTransformChanged → presenter → node.SetWindowTransform

WindowGrabHandle  (NEW, Interaction)
   ├─ 1-hand: translate + yaw (roll lock / pitch limited)
   └─ 2-hand: distance ratio で uniform scale、translate suppressed
```

### Side-channel: `INdiViewWindowState` (Open Q1 / WARN 1 解決)

`Pose` を `INdiReceiverNode` に出すと UI.Contracts の Unity 依存範囲が広がる + node の責務が source + visual の 2 つに分裂する。`feedback_cue_load_invariants` で確立した side-channel pattern (`INodeVisualRotationProvider`) と同じ流儀で**別 interface に切る**:

```csharp
// Rhizomode.UI.Contracts
public interface INdiViewWindowState
{
    // UnityEngine.Pose は使わず、Vector3 + Vector3 + float に分解。
    // 理由: Pose は SharedKernel に存在せず、test 容易性も劣る。
    Vector3 WindowPosition    { get; }
    Vector3 WindowEulerAngles { get; }
    float   WindowScale       { get; }

    event Action? OnWindowTransformChanged;

    void SetWindowTransform(Vector3 position, Vector3 eulerAngles, float scale);
}
```

`NdiReceiverNode` が両方の interface を実装 (data layer)。Presenter は `INodeView` に `AsNdiViewWindowState()` を追加し、cast で取得。

### Persistent graph-owned root: `NdiWindowsRoot` (Open Q2 / FAIL 2 解決)

env scene (additive load/unload される) ではなく、**SampleScene (base / 常駐) に常設 root を 1 個置く**:

```csharp
// Rhizomode.UI.Presentation
[DisallowMultipleComponent]
public sealed class NdiWindowsRoot : MonoBehaviour
{
    private readonly Dictionary<string /*nodeId*/, NdiViewWindow> _windows = new();

    public NdiViewWindow CreateFor(INdiReceiverNode node, INdiViewWindowState state) { ... }
    public void DestroyFor(string nodeId) { ... }
    public bool TryGet(string nodeId, out NdiViewWindow w) { ... }

    private void OnDestroy()
    {
        // SampleScene unload (= 終了時のみ起きる) で全 window destroy。
        foreach (var w in _windows.Values) if (w != null) Destroy(w.gameObject);
        _windows.Clear();
    }
}
```

cleanup の 3 段:
1. **node delete**: presenter.Detach → root.DestroyFor(nodeId)
2. **graph unload / clear (cue 切替)**: 下の §"Cue switch hook ownership and ordering" 参照
3. **scene unload (SampleScene 終了)**: root.OnDestroy で残り全 destroy (defensive)

### Cue switch hook ownership and ordering (v0.3 で追加、WARN c 対応)

**Hook owner**: cue 切替の起点は既存 `Rhizomode.UI.Persistence.CueListPanelController` ↔ `IGraphPersistenceService` の経路。本 plan では `NdiWindowsRoot.OnGraphCleared` を `GraphState.Cleared` Event (R3 / `IGraphEventBus`) に subscribe する。

**Firing order** (cue B → cue A 切替時):

```
1. UI: ユーザーが Cue A をタップ
2. CueLoadCoordinator.LoadCue("A") が呼ばれる
3.   GraphCommandDispatcher.Submit(ClearGraphCommand)             ← graph 全 node / edge 消去
       ├─ GraphState 内部 dict を clear
       ├─ NodeDestroyed event を node 単位で発火 (presenter.Detach → root.DestroyFor 経由で window destroy)
       └─ GraphCleared event を 1 回発火 (defensive: 残った window を root が全 destroy)
4.   GraphSerializer.LoadFromJson(cueA.json)
       ├─ AddNodeCommand を node 数だけ発火 (presenter.Attach → root.CreateFor で window 再生成)
       └─ Restore window pose via INdiViewWindowState から
5. (env scene 切替が伴う場合) AdditiveSceneLoader.LoadScene(envName)
       ├─ NdiWindowsRoot は SampleScene 常設なので env unload では destroy されない
       └─ env load 後の Apply 順序 (Env → Volume → Camera) は別系統 (Plan env-scene-isolation v0.3)
```

**保証する不変条件**:
- Step 3 完了時点で root._windows は空 (NodeDestroyed + GraphCleared の二重保険)
- Step 4 で再生成された window は新 cue の pose で初期化される (古い window が残らない)
- env scene unload で window が宙ぶらりんにならない (root は base scene 常設)
- Step 5 は cue load と直列。env 非切替の cue 切替は Step 5 を skip

EditMode test (Phase F2): `NdiWindowsRoot` に dummy node を 3 個 register → `GraphCleared` イベント push → registry が空になることを確認。

### Window 親子付け

NdiViewWindow GameObject は `NdiWindowsRoot.transform` の child。位置・回転・スケールは world space で扱う (root scale = 1 固定)。env scene 切替で宙ぶらりんにならない / scope が明確。

### NdiViewWindow component

```csharp
public sealed class NdiViewWindow : MonoBehaviour
{
    public const float Aspect = 16f / 9f;
    private const float BaseWidth = 1.0f;   // scale=1 で 1.0m × 0.5625m

    [SerializeField] private MeshRenderer? targetRenderer;
    [SerializeField] private BoxCollider?  boxCollider;
    [SerializeField] private WindowGrabHandle? grabHandle;

    public MeshRenderer Renderer => targetRenderer!;

    /// <summary>外部から transform を即時適用 (presenter / cue load 経路)。</summary>
    public void ApplyTransform(Vector3 position, Vector3 eulerAngles, float scale) { ... }

    /// <summary>flicker 回避用: renderer を disable した状態で spawn し、ApplyTransform 後に有効化。</summary>
    public void SetRendererActive(bool active) => targetRenderer.enabled = active;

    public event Action<Vector3 /*pos*/, Vector3 /*euler*/, float /*scale*/>? OnTransformChanged;
}
```

### Scale clamp (WARN 3 解決)

```csharp
public const float MinScale = 0.1f;
public const float MaxScale = 4.0f;
```

WindowGrabHandle 内で `Mathf.Clamp(newScale, MinScale, MaxScale)`。EditMode test で boundary 値検証。

### 2-hand scale formula (Q3 確認)

```
baseline   = max(distance_at_grab_start, 0.01f)     // epsilon
current    = distance(left, right)
newScale   = Clamp(_scaleBaseline * (current / baseline), MinScale, MaxScale)
```

両手 grab 中は translate / rotation を抑制 (両手 release 時に final scale を node.SetWindowTransform に commit)。frame 毎の commit は autosave 過剰なので grab end のみ。

### WindowGrabHandle vs NodeGrabHandler (Q4)

`NodeGrabHandler` は `NodeVisualManager` の collider dict に依存 → 流用不可。**新 `WindowGrabHandle` を `Rhizomode.Interaction` に置く**:

```csharp
public sealed class WindowGrabHandle : MonoBehaviour
{
    [SerializeField] private NdiViewWindow window = null!;
    // 1-hand state
    private bool _leftGrabbing, _rightGrabbing;
    private Vector3 _leftLastPos, _rightLastPos;
    private Quaternion _leftLastRot, _rightLastRot;
    // 2-hand state
    private float _baselineDistance;
    private float _scaleBaseline;

    // grab start / update / end は ControllerInputRouter + SharedRaycastService の
    // 既存 hit event を購読する pattern (NodeGrabHandler を参考に simplify)。

    // roll lock: 適用前に euler.z = 0 clamp、yaw 自由、pitch は -60..60 deg clamp (Q6)
}
```

XR plumbing は既存 `IRayProvider` + `IControllerInput` を再利用。

### paramsJson 拡張 + 旧形式 forward-compat (WARN 5)

```csharp
[Serializable]
private class NdiReceiverParams
{
    public string  sourceName        = "";
    public Vector3 windowPosition    = Vector3.zero;         // 初期 zero、解釈は spawn 側で HMD fallback
    public Vector3 windowEulerAngles = Vector3.zero;
    public float   windowScale       = 1.0f;
    public bool    hideFromMirror    = false;                // MirrorHidden toggle (WARN 6)
    public bool    hasExplicitWindowTransform = false;       // 旧 cue 区別フラグ
}
```

**Forward-compat 規約**:
- Field 追加のみ、削除/rename 禁止 (既存 `feedback_node_addition_protocol` ルール準拠)
- 旧 cue (sourceName のみ) を deserialize すると `hasExplicitWindowTransform = false` のまま → presenter は HMD-forward fallback を採用
- `SetWindowTransform` 呼出時に `hasExplicitWindowTransform = true` に flip → 以降 cue save で保存される
- EditMode test 必須:
  - `OldFormat_DeserializesWithSafeDefaults` (`{"sourceName":"CAM"}` → window default + flag=false)
  - `NewFormat_RoundTripsPreservesAll`
  - `SetWindowTransform_FlipsExplicitFlag`

### MirrorHidden toggle (Q6 / WARN 6)

`hideFromMirror: bool` (default false = mirror に映る)。Presenter spawn 時に値を読み、true なら `Rhizomode.Presentation.Layering.MirrorHiddenLayer.ApplyRecursive(window.gameObject)` を呼ぶ。runtime で切替えたい時は再 apply (現状はノード再 spawn でしか変更しない簡易設計)。

### Cascade offset (Q3 / WARN 7、v0.3 で hash 安定化)

複数 window が中央に重ならないよう、deterministic な位置オフセット。
**`string.GetHashCode()` は .NET Core 以降 process 起動毎に randomize される** ため、cue save → 別 session で load した時に同じ nodeId が違う offset を返す。これを避けるため **FNV-1a 32bit を自前実装** して使う:

```csharp
// session / process 跨ぎで stable な 32bit hash (FNV-1a)
internal static uint StableHash32(string s)
{
    const uint OffsetBasis = 2166136261u;
    const uint Prime       = 16777619u;
    if (string.IsNullOrEmpty(s)) return OffsetBasis;
    uint h = OffsetBasis;
    for (int i = 0; i < s.Length; i++)
    {
        h ^= s[i];
        h *= Prime;
    }
    return h;
}

private static Vector3 CascadeOffset(string nodeId, Vector3 hmdForward, Vector3 hmdRight)
{
    const int Slots = 8;
    int idx = (int)(StableHash32(nodeId) % (uint)Slots);
    // window collider が 1.0m × 0.5625m なので 1.2m spacing で重ねない。
    float side  = (idx % 2 == 0 ? 1f : -1f) * 1.2f * ((idx / 2) + 1);
    float depth = -0.3f * (idx % 4);  // 奥行きにも 30cm ずつ後退
    return hmdRight * side + hmdForward * depth;
}
```

EditMode test (Phase F1):
- `StableHash32_KnownVector_MatchesExpected`: 既知文字列 (例 "node-001") に対する hash 値を const と照合 (regression 検出)
- `StableHash32_SameStringAcrossInstances_IsEqual`: 別 process / session を想定したリテラル文字列 vs `new string([...].ToArray())` の同一性
- `CascadeOffset_SameNodeId_ReturnsSamePosition` (determinism)
- `CascadeOffset_AdjacentSlots_DoNotOverlap` (1.2m > 1.0m collider width)

### Initial spawn position (HMD null fallback) (WARN 8)

```csharp
private Vector3 ResolveHmdPosition(IXrHmdReference? hmd, Camera? mainCam)
{
    if (hmd != null && hmd.HasValidPose)  return hmd.Position;
    if (mainCam != null)                  return mainCam.transform.position;
    return new Vector3(0f, 1.5f, 1.5f);   // hardcoded fallback
}
```

`IXrHmdReference` は new contract (`Rhizomode.Input.Contracts`) として追加。既存 `IRayProvider` が HMD pose を持っていれば adapter で吸う。

### asmdef dependency diagram (v0.3 で追加、WARN g 対応)

新 / 拡張する asmdef とその依存方向:

```
Rhizomode.Input.Contracts                                  (既存)
    ↑ added: IXrHmdReference interface (Unity 依存なし、Vector3 + Quaternion + bool のみ)
    │
    ├─ Rhizomode.XR (既存)                — 実装 XrHmdAdapter を提供 (ControllerInputRouter 経由)
    │     ↑ 既に Input.Contracts を ref 済 (no new dep)
    │
    └─ Rhizomode.UI.Presentation (既存)
          ↑ 既に Input.Contracts を ref 済 (NdiReceiverPresenter で IRayProvider 使用済)
          │
          ├─ NdiViewWindow (NEW)              — Unity only、追加 ref なし
          ├─ NdiWindowsRoot (NEW)             — Unity only、追加 ref なし
          ├─ NdiViewWindowFactory (NEW)       — IXrHmdReference を ctor inject
          └─ NdiReceiverPresenter (拡張)      — INdiViewWindowState を node から cast

Rhizomode.UI.Contracts (既存)
    ↑ added: INdiViewWindowState interface (UnityEngine.Vector3 のみ依存、既存 INdiReceiverNode と同方針)

Rhizomode.Interaction (既存)
    ├─ WindowGrabHandle (NEW)                 — Input.Contracts (IControllerInput, IRayProvider) を既に ref 済
    └─ 新規参照なし

Rhizomode.Core.Tests (既存)
    ↑ added: cascade hash test / paramsJson roundtrip test (asmdef ref 変更なし)

Rhizomode.UI.Tests (既存)
    ↑ added: WindowGrabHandle test / NdiWindowsRoot registry test (UI.Presentation + Interaction を既に ref)
```

**循環無し検証**:
- `Input.Contracts` は最下層 (依存先なし) → 新 IXrHmdReference 追加は他 asmdef を引きずらない
- `UI.Presentation` → `Input.Contracts` は既存方向、逆向きの dep が無いので循環無し
- `Interaction` → `Input.Contracts` 既存、`Interaction` → `UI.Presentation` も既存 (NodeGrabHandler が NodeVisualController を参照)、`WindowGrabHandle` が `NdiViewWindow` (UI.Presentation) を参照する経路は既存方向 → 循環無し
- `UI.Contracts` → 何にも依存しない最下層 (今も) → INdiViewWindowState 追加で UnityEngine.Vector3 への依存を増やすのみ、既存 INdiReceiverNode と同方針

→ asmdef 境界違反無し。新規 asmdef は不要。

### TargetRenderer swap safety (WARN 9)

**順序を厳守**:
1. NdiViewWindow を `renderer.enabled = false` で spawn
2. `ApplyTransform(savedPose)` を呼んで pose 適用
3. Klak.NDI receiver の `targetRenderer = window.Renderer` を assign
4. `renderer.enabled = true` で表示開始
5. (presenter Detach 時) `receiver.targetRenderer = null` → window destroy の順

これで「window 表示中に targetRenderer が無効化されて 1 frame 黒」を防ぐ。

### Cue load の flicker 防止 (Q7 / WARN 9 / OK e)

順序を厳守 (上記と同じだが cue load 経路):
1. node restore (`RestoreParamsFromJson` で transform 読み込み)
2. presenter.Attach → window spawn (renderer disabled)
3. `INdiViewWindowState.WindowPosition` 等を読み window.ApplyTransform
4. `Klak.NDI receiver.targetRenderer` assign
5. window renderer enable

window が「default 位置で 1 frame 描画 → 移動」の flicker を回避。

### Detach order (WARN f)

```csharp
public void Detach()
{
    // 1. unsubscribe
    if (_node != null) {
        _node.OnSourceNameChanged -= HandleSourceNameChanged;
        _windowState!.OnWindowTransformChanged -= HandleWindowTransformChanged;
    }
    // 2. NDI receiver tear down
    if (_receiver != null) {
        _receiver.targetRenderer = null;   // swap-safe 順序
        Destroy(_receiver);
        _receiver = null;
    }
    // 3. window 破棄 (registry 経由)
    _windowsRoot?.DestroyFor(_node?.NodeId ?? "");
    // 4. clear refs (idempotent re-call ガード)
    _node = null;
    _windowState = null;
}
```

Idempotent: 2 度呼ばれても null check で no-op。OnDestroy も Detach を呼ぶが node==null で早期 return。

## 実装フェーズ + commit 分割 (WARN 10)

### Phase F1 — Contracts + data + tests (1st commit)
- `INdiViewWindowState` 新規 (`Rhizomode.UI.Contracts`)
- `INodeView.AsNdiViewWindowState()` 追加
- `NdiReceiverNode` が `INdiViewWindowState` を実装、`NdiReceiverParams` 拡張 (5 field)
- EditMode test (`Rhizomode.Core.Tests`):
  - 旧形式 `{"sourceName":"CAM"}` → window default + `hasExplicitWindowTransform=false`
  - 新形式 round-trip 全 field 保存
  - `SetWindowTransform` で event 発火 + flag flip
  - cascade offset の determinism + 隣接 slot で重ならない (boundary 検証)
- compile + tests + commit (atomic)

### Phase F2 — NdiViewWindow + WindowGrabHandle + Root + Factory (2nd commit)
- `NdiWindowsRoot` 新規 (`Rhizomode.UI.Presentation`) — SampleScene root に Inspector 経由で配置
- `NdiViewWindow` 新規 (同 asmdef)
- `WindowGrabHandle` 新規 (`Rhizomode.Interaction`) — grab + 2-hand scale + roll lock + pitch clamp
- `NdiViewWindowFactory` 新規 (DI'd MonoBehaviour) — `IXrHmdReference` を取って HMD-forward + cascade で初期 pose
- `NdiReceiverPresenter` refactor:
  - preview Quad / preview material / preview collider / ApplyPreviewTransform を全削除
  - `_node.AsNdiViewWindowState()` で window state を取得
  - factory 経由で window spawn → render disabled → ApplyTransform → receiver.targetRenderer = window.Renderer → render enable
  - Detach の order を WARN f に従い厳格化
- `INodeView` / `NodeViewAdapter` に `AsNdiViewWindowState()` 追加
- EditMode test (`Rhizomode.UI.Tests`):
  - WindowGrabHandle 2-hand scale formula (baseline ratio + clamp)
  - WindowGrabHandle roll lock + pitch clamp
  - NdiWindowsRoot registry (CreateFor / DestroyFor / TryGet)
  - Detach 順序のテスト (event unsubscribed 後に node = null になる)
- compile + tests + commit

### Phase F3 — Manual canary + docs + memory (3rd commit)
- PlayMode canary (manual):
  1. NDI source を network broadcast (OBS NDI plugin 等)
  2. NdiReceiver node spawn → 中央 (HMD 正面 1.5m) に window 出現、source name auto-pick
  3. 右手 grip で window 移動 → 位置追従、roll は 0 固定、yaw 自由
  4. 両手 grip → distance 倍化 で window scale 2x、release で確定
  5. cue save → 別 cue → 元 cue load → window が同 pose / scale で復元、flicker 無し
  6. 2 個 spawn して cascade offset を目視確認
  7. node delete → window 消滅
- `docs/NDI_USAGE.md` 新規 (使用者向け)
- `~/.claude/projects/.../memory/feedback_ndi_view_window.md` 新規
- commit + push

## リスクと回避 (v0.2)

| リスク | 緩和策 |
|---|---|
| `INdiViewWindowState` を node に追加することで test 容易性が落ちる | pure data interface、Unity engine 依存は `Vector3 / float` のみ。EditMode test で MonoBehaviour 起こさず検証可能 |
| 旧 cue 読込で window が default zero 位置に出る | `hasExplicitWindowTransform = false` で fallback (HMD forward + cascade) を採用 |
| scale clamp 上限 4f は内容次第で不足 | const として明示し、後続 PR で content-defined 化できる |
| `IXrHmdReference` 新規 interface の依存先肥大 | `Rhizomode.Input.Contracts` に置く + 既存 `IRayProvider` の adapter で吸う |
| cascade offset の hash 衝突 (同じ nodeId に複数 window) | nodeId は graph 単位 unique なので衝突は理論上発生しない。assertion で異常検出 |
| Klak.NDI receiver の targetRenderer swap で 1 frame 黒 | "新先 ready → swap → 旧 teardown" 順序 + renderer enable は最後 |
| 2-hand scale 中に片手 release | release した瞬間に "1-hand mode" に降格 (translate 復活)、scale は最後の値で stay |
| SampleScene 不在で NdiWindowsRoot が無い | factory が null check → ログ警告 + window spawn skip (fail-open) |
| MirrorHidden toggle が runtime 変更で window 残骸を残す | 設計上は spawn 時のみ反映、runtime 切替は明示 OFF |

## Codex review v0.2 → v0.3 反映表

| 指摘 | 反映 |
|---|---|
| WARN 6 / a / e: cascade hash 不安定 | `StableHash32` (FNV-1a 32bit) を自前実装、`string.GetHashCode()` 不使用。test 4 件 (regression / 同値性 / determinism / 衝突無し) |
| WARN c: cue switch hook ownership | §"Cue switch hook ownership and ordering" 新設。`GraphState.Cleared` Event を `NdiWindowsRoot.OnGraphCleared` で subscribe、firing order 5 step + 不変条件 4 項目を明示 |
| WARN g: asmdef 依存図 | §"asmdef dependency diagram" 新設。`Input.Contracts` 最下層 / 既存 ref のみ追加 / 循環無し検証を明文化 |

## Codex review v0.1 → v0.2 反映表

| 指摘 | 反映 |
|---|---|
| FAIL 2: scene-root 配置で env unload 漏れ | `NdiWindowsRoot` (SampleScene 常設) + registry + 3 段 cleanup |
| WARN 1: `Pose` on `INdiReceiverNode` | 別 interface `INdiViewWindowState` に分離、`Pose` 不使用 |
| WARN 3: scale clamp 上限 | `MinScale = 0.1f`, `MaxScale = 4.0f` 明示 + test |
| WARN 5: paramsJson 旧形式 default | `hasExplicitWindowTransform` flag + EditMode test 3 件必須 |
| WARN 6: MirrorHidden 固定 | `hideFromMirror: bool` toggle (default false) |
| WARN 7: cascade offset 定量化 | nodeId hash + 1.2m side spacing + 30cm depth、test で determinism + 衝突回避 |
| WARN 8: HMD ref fallback | `IXrHmdReference` > `Camera.main` > `(0, 1.5, 1.5)` の 3 段 |
| WARN 9: targetRenderer swap | "renderer disabled → ApplyTransform → assign → enable" 順序 |
| WARN 10: commit 単位 | F1 / F2 / F3 の 3 commit に分割 |
| OK a: `INdiViewWindowState` 推奨 | 採用 |
| OK b: 旧 JSON deserialization test | F1 で 3 test 必須化 |
| OK c: 2-hand scale formula | baseline + clamp + grab end commit を明文化 |
| OK d: NDI native buffer 安全 | renderer-disable + swap 順序で対処 |
| OK e: cue load flicker | renderer disabled で spawn → ApplyTransform → enable |
| OK f: detach idempotent | unsubscribe → null assign → Destroy 順 + 2 度呼出 ガード |

## Open questions (v0.2)

すべて v0.1 → v0.2 で確定。新規 open は無し。Codex の confirm pass で「OK / 残課題」を最終確認。
