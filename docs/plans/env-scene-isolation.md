# Environment Scene Isolation — Design Plan v0.3

> v0.1 → v0.2: Codex review round 1 (FAIL 1 + WARN 8) を反映。
> v0.2 → v0.3: Codex confirm round 2 (WARN 1 + minor) を反映。
> CameraOverrideSession を Dict<Camera,_> ベースの strong-ref 設計に変更し
> InstanceIDToObject 経由の Editor/Runtime API 二重性を解消。
> SceneCameraOverride.Apply の null camera 警告ログを追加。

## Goal

任意の XRNodeVJTools 環境シーン (concrete / Forest / Dark / Ruins / …) が SampleScene の defaults を**漏れなく上書き**し、unload で**完全に reset** される仕組みを作る。

「環境を切替えた瞬間にビジュアルが完全にその環境の設定に従う」「unload で SampleScene の元状態に戻る」を保証する。

## 現状の漏れ (Problem statement)

`AdditiveSceneLoader` + `SceneEnvironment` (現行 v5.4) でカバーされているのは **RenderSettings の 6 項目だけ**:
- skybox / ambient (mode + 3 colors + intensity) / fog (6 props) / reflection (mode + intensity)

カバーされていない、SampleScene が環境シーンに影響を残す経路:

| # | 経路 | 具体例 | 観測された実害 |
|---|---|---|---|
| 1 | **Global Volume の post-FX profile** | `SampleSceneProfile` の Bloom / Tonemapping / Vignette が常時 active | concrete で「カメラが離れると bloom halo で明るく見える」(本セッション報告) |
| 2 | **Camera clear flags + backgroundColor** | env が skyboxMaterial = null にしても camera が Skybox clear のまま → Unity default 青空グラデが入る | 前 session の "skybox=null だけど背景が青" |
| 3 | **Reflection probes (baked / realtime)** | SampleScene に焼かれた reflection probe があれば env の reflectionIntensity=0 でも IBL に注入される可能性 | 未観測 (現在 SampleScene に probe 無し? 要確認) |
| 4 | **URP Renderer features** | `PC_Renderer.asset` の RendererFeature 一覧 (RibbedGlass / Caustics / etc.) は env で OFF できない | 未観測 |
| 5 | **Lighting settings** | Realtime GI / Mixed lighting mode / Baked GI 等は scene-level 設定 | env が realtime のみ想定でも SampleScene の mode が effective |
| 6 | **Directional light** | SampleScene の Directional Light は常時 active、env で消したくても消せない | 屋内 env (concrete 等) で太陽光が残る → Phase E3 で対応 |
| 7 | **AudioListener / fog density per-scene** | env-local fog 設定は SceneEnvironment にあるが他 audio / camera settings は無い | |
| 8 | **post-FX 自体の active state** | env が tonemapping を OFF にしたくても Global Volume の Tonemapping は止められない | concrete で auto-exposure 的な見え方の遠因 |

## 設計方針

3 つの component で責務を分割し、SampleScene base への revert を統一的に扱う。重要な不変条件:

- **コンポーネントは完全 inert**: `OnEnable` / `Awake` で `Apply` を呼ばない。state 変化は AdditiveSceneLoader 経由のみ
- **Loader-owned session**: snapshot / revert state は loader 側の `CameraOverrideSession` 等の object が所有。複数 env の連続 load / re-load でも壊れない
- **明示参照**: camera 列挙は `Camera.allCameras` ではなく serialize された明示参照リストで行う
- **Volume 完全性契約**: env volume profile は base が active な effect を **全て持つ + override 済 + weight=1** にすること。欠落で base が漏れる

## 設計案

### Component A: `SceneEnvironment` (既存)

RenderSettings を cover。**そのまま据置き**。

### Component B (新): `SceneVolumeOverride`

post-FX profile を env-local に差替える。`Volume` component を runtime に動的生成し、unload で `Destroy` する。

```csharp
[DisallowMultipleComponent]
public sealed class SceneVolumeOverride : MonoBehaviour
{
    [SerializeField] private VolumeProfile? envProfile;
    [SerializeField] private int   priority = 100;
    [SerializeField] private float weight   = 1f;

    // Loader 専用 API。components は inert。
    internal void Apply()
    {
        if (envProfile == null) return;
        if (_runtimeVolume != null) return; // 二重 apply 防止
        _runtimeVolume = gameObject.AddComponent<Volume>();
        _runtimeVolume.isGlobal      = true;
        _runtimeVolume.sharedProfile = envProfile;
        _runtimeVolume.priority      = priority;
        _runtimeVolume.weight        = weight;
    }

    internal void Revert()
    {
        if (_runtimeVolume == null) return;
        Destroy(_runtimeVolume);   // toggle ではなく Destroy (Codex 推奨: 4)
        _runtimeVolume = null;
    }

    private Volume? _runtimeVolume;

    // 注意: OnEnable / OnDestroy 等の Unity message から Apply/Revert を呼ばない。
    // 全ては AdditiveSceneLoader 経由で制御する。
}
```

### Volume profile 作成契約 (env 作者向け)

env の VolumeProfile は base SampleScene profile (`SampleSceneProfile`) に active な effect を **全て** override 済で含めること。欠落すると base が priority 経由でブレンドして漏れる:

| Effect | 必須 override 値 (env 側で「殺す」場合) |
|---|---|
| Bloom | active=true, intensity=0 (override 済), threshold=999 |
| Vignette | active=true, intensity=0 (or env で調整した値) |
| Tonemapping | active=true, mode=None (or env で必要な mode) |
| ColorAdjustments / etc. | env で使うなら override、不要なら active=true で neutral 値 |

`Volume.priority` だけで殺せない理由は、URP の Volume system が **同じ override property** に対して priority で勝者を決める仕様のため。base の Bloom intensity に env が override を入れて初めて勝てる。

→ `Editor/EnvVolumeProfileValidator.cs` が env profile を import 時に検証 (base に存在するが env に override が無い effect を警告)。

### Component C (新): `SceneCameraOverride`

main camera / mirror camera 等の clear flags / backgroundColor を env-local に。state は **session 側に持たせて component 自体は inert**。

```csharp
[DisallowMultipleComponent]
public sealed class SceneCameraOverride : MonoBehaviour
{
    [SerializeField] private CameraClearFlags clearFlags     = CameraClearFlags.Skybox;
    [SerializeField] private Color            backgroundColor = Color.black;

    /// <summary>
    /// この env で override 対象とする camera の明示参照 (Cinemachine の brain を持つ Unity Camera を指定)。
    /// `Camera.allCameras` を使わないのは disabled / cinemachine virtual / 後から spawn される NDI sender
    /// camera 等を漏らさず、また誤って捕まえないため。serialized list なら scene 設計者の責任範囲が明確。
    /// </summary>
    [SerializeField] private List<Camera> targets = new();

    internal IReadOnlyList<Camera> Targets => targets;
    internal CameraClearFlags ClearFlags    => clearFlags;
    internal Color            BackgroundColor => backgroundColor;

    // 完全 inert。OnEnable / OnDisable から Apply 呼ばない。
}
```

```csharp
// Loader が所有する session object。env 1 つにつき 1 instance。
internal sealed class CameraOverrideSession : IDisposable
{
    // Camera を strong ref で保持。InstanceID 経由の Editor/Runtime API 二重性を回避し、
    // Unity の null-check operator (==) が destroyed camera を弾く。
    // Session は loader-owned で env unload 時に Dispose されるため leak リスクは限定的。
    private readonly Dictionary<Camera, (CameraClearFlags flags, Color bg)> _snapshot = new();

    public void Apply(IReadOnlyList<SceneCameraOverride> overrides)
    {
        foreach (var o in overrides)
        {
            int idx = -1;
            foreach (var cam in o.Targets)
            {
                idx++;
                if (cam == null)
                {
                    Debug.LogWarning(
                        $"[SceneCameraOverride] '{o.gameObject.scene.name}/{o.gameObject.name}' " +
                        $"targets[{idx}] is null. シーン編集中に参照が外れた可能性 (missing reference)。" +
                        " Inspector で再アサインすること。");
                    continue;
                }
                if (!_snapshot.ContainsKey(cam))
                    _snapshot[cam] = (cam.clearFlags, cam.backgroundColor);
                cam.clearFlags      = o.ClearFlags;
                cam.backgroundColor = o.BackgroundColor;
            }
        }
    }

    public void Revert()
    {
        foreach (var (cam, snap) in _snapshot)
        {
            if (cam == null) continue; // destroyed between Apply and Revert
            cam.clearFlags      = snap.flags;
            cam.backgroundColor = snap.bg;
        }
        _snapshot.Clear();
    }

    public void Dispose() => Revert();
}
```

Loader は env unload 時に `session.Dispose()` を呼んで完全 revert する。Session 自体は loader 側 field で 1 個保持し、env load 毎に作り直す → snapshot リーク無し / multi-env 同時 active も将来サポート可能。

**Why strong refs over `InstanceIDToObject`:** `EditorUtility.InstanceIDToObject` は Editor only、`Resources.InstanceIDToObject` は Unity 2022+ runtime API。Dict key を `Camera` 直接にすれば API 二重性が完全に消え、null/destroyed の判定も Unity の `==` operator で自然に動く。Session が loader-owned + Dispose 必須なので strong ref のリーク窓は env load の生存期間に限定される。

### `AdditiveSceneLoader` 拡張

load / unload の hook で Apply / Revert を loader 主導で呼ぶ:

```csharp
// 既存
private SceneEnvironment? baseSceneEnvironment;

// 新規 — loader 所有
private CameraOverrideSession? _cameraSession;
private SceneVolumeOverride[]  _activeVolumeOverrides = Array.Empty<SceneVolumeOverride>();

private void ApplyLoadedSceneOverrides(UnityScene scene)
{
    ApplyEnvironmentSettings(scene);         // 既存 SceneEnvironment
    ApplyVolumeOverrides(scene);             // 新規
    ApplyCameraOverrides(scene);             // 新規
}

private void ApplyVolumeOverrides(UnityScene scene)
{
    _activeVolumeOverrides = FindAllOfType<SceneVolumeOverride>(scene);
    if (_activeVolumeOverrides.Length == 0)
        Debug.LogWarning($"[AdditiveSceneLoader] env scene '{scene.name}' has no SceneVolumeOverride; " +
                         "SampleScene global post-FX (Bloom / Vignette / Tonemapping) will bleed through.");
    foreach (var vo in _activeVolumeOverrides) vo.Apply();
}

private void ApplyCameraOverrides(UnityScene scene)
{
    _cameraSession?.Dispose(); // 念のため前回 session を revert
    _cameraSession = new CameraOverrideSession();
    var overrides = FindAllOfType<SceneCameraOverride>(scene);
    _cameraSession.Apply(overrides);
}

private void RevertOverridesToBase()
{
    foreach (var vo in _activeVolumeOverrides) vo.Revert();
    _activeVolumeOverrides = Array.Empty<SceneVolumeOverride>();
    _cameraSession?.Dispose();
    _cameraSession = null;
    baseSceneEnvironment?.Apply();
}
```

Apply 順序の規約: **SceneEnvironment → SceneVolumeOverride → SceneCameraOverride**。
Revert 順序: 逆順 (Camera → Volume → Env)。

## 環境シーン契約 (Documentation)

`docs/SCENE_AUTHORING.md` (新規) に明文化:

> XRNodeVJTools の環境シーンは以下の component を含めること:
> - **必須**: `SceneEnvironment` 1 個 (RenderSettings)
> - **強く推奨**: `SceneVolumeOverride` 1 個 (これが無いと `SampleSceneProfile` の Bloom / Vignette / Tonemapping が残る)
> - **推奨**: `SceneCameraOverride` 1 個 (skybox material 不在の場合に camera clear 色を制御)
> - これらは同 GameObject に置いて OK (例: `SceneEnvironment_Concrete` という 1 個の root に 3 component)
> - VolumeProfile は **SampleSceneProfile が持つ全 effect を override 済で含める** こと
> - SceneCameraOverride の `targets` には HMD camera + Mirror output camera を **明示参照** で入れること

Editor 側で `SceneValidator` が以下を検出して warning/error:
- env scene に `SceneEnvironment` 無し → warning (全 env)、 error (launch-critical = concrete / Forest 等)
- env scene に `SceneVolumeOverride` 無し → warning (全 env)、 error (launch-critical)
- VolumeProfile が base profile の active effect を欠落 → warning

## 実装フェーズ

### Phase E1 — Component + AdditiveSceneLoader 拡張 (本体)
- `SceneVolumeOverride.cs` 新規作成 (`Rhizomode.Scene.Runtime`)
- `SceneCameraOverride.cs` 新規作成 (同 asmdef)
- `CameraOverrideSession.cs` 新規作成 (`internal sealed class`、純 C# object)
- `AdditiveSceneLoader.cs` に Apply / Revert + warning ログ統合
- EditMode test (`Rhizomode.Scene.Tests`):
  - `CameraOverrideSession.Apply → Revert` ラウンドトリップで camera state 完全復元
  - 複数 SceneCameraOverride が同一 camera を被ったときの "最初の snapshot を保持" 挙動
  - `SceneVolumeOverride.Apply` で Volume が `priority=100` で生成され `Revert` で `Destroy` される
  - load → unload → load のサイクルで snapshot リーク無し
- 既存テスト不変

### Phase E2 — concrete シーンで移行検証 (canary)
- 現状の `Volume_Concrete_Override` GameObject を `SceneVolumeOverride` 化 (priority / profile はそのまま)
- `SceneCameraOverride` を追加し、`targets` に HMD camera + Mirror output camera を明示参照
- 受入基準 (**PlayMode manual canary, 必須**):
  1. SampleScene boot → デフォルト環境 (Dark 等) → bloom halo 視認可能
  2. concrete additive load → bloom halo 消失 (camera が遠ざかっても室内輝度一定)
  3. concrete unload → bloom halo 復活、camera bg が base に戻る
  4. concrete reload → 2 と同じ状態再現 (snapshot リーク無し)
  5. 各 step で `uloop screenshot` を取り、目視で確認
- canary 失敗時は Phase E1 に戻って revise

### Phase E3 — Directional light + 他 env 移行
- env-local directional light の on/off 切替えを `SceneEnvironment` に追加 (新 field `disableBaseDirectionalLight: bool`)
- 既存 env (Dark / Forest / Experience / Ruins) に `SceneVolumeOverride` + `SceneCameraOverride` を遡及配置
- `Editor/SceneValidator` 拡張: 上記契約違反を検出

### Phase E4 — Documentation + memory 更新
- `docs/SCENE_AUTHORING.md` を新規作成
- `CLAUDE.md` の Architecture セクションに env scene 契約を追記
- `~/.claude/projects/.../memory/feedback_env_scene_isolation.md` に「3 component 契約 + CameraOverrideSession + Volume profile 完全性」のメモを追加

### Phase E5 (deferred, canary 後判断) — Reflection probe / Renderer features
- E2 canary で concrete が完全 isolation 達成できていれば E5 は不要 (deferred)
- 漏れが残る場合: ReflectionProbeOverride / RendererFeatureToggle を追加検討

## 想定リスクと回避

| リスク | 緩和策 |
|---|---|
| env profile が base の effect を欠落 → base が漏れる | `EnvVolumeProfileValidator` で import 時警告 + `docs/SCENE_AUTHORING.md` で明文化 |
| Cinemachine 経由の clear が別経路を持つ | E2 canary で実機確認。問題あれば `CinemachineBrain.OutputCamera` を SceneCameraOverride.targets に明示追加 |
| 複数 env を同時 active にしたい future requirement | `CameraOverrideSession` はインスタンス化されており stateful global を持たないので拡張容易 |
| Volume override の Bloom intensity=0 でも HDR + Tonemapping が dim scene を boost | env profile に Tonemapping override (mode=None) を必ず含める契約で対応 |
| `InstanceIDToObject` の Editor / Runtime API 二重性 | v0.3 で解消: `CameraOverrideSession` は `Dictionary<Camera, _>` の strong ref ベースに変更 |
| SceneCameraOverride.targets が missing reference 化 | Apply 時に null チェック + scene/GameObject/index 付き warning ログ |
| strong ref で camera GC が止まる | Session は loader-owned で env unload 時 `Dispose` 必須 → 生存期間は env load 中のみ。runtime camera は scene unload で破棄され、Unity == null check で revert がスキップされる |

## テスト戦略

- **EditMode** (`Rhizomode.Scene.Tests`):
  - `CameraOverrideSession` の Apply/Revert ラウンドトリップ (純 C# テスト、Unity Object 必要)
  - `SceneVolumeOverride` の Apply 後の Volume 生成 / Revert 後の Destroy
  - Loader の load → unload → load サイクルで snapshot dict が空に戻ること
- **PlayMode canary** (必須、Phase E2 受入基準):
  - 上記 E2 受入手順 1〜5 を実機 (Editor PlayMode で代用) で実行 → screenshot 比較
  - 期待結果は `docs/plans/env-scene-isolation-canary.md` に画像と共に記録

## Codex review v0.2 → v0.3 で反映した修正

| 指摘 | 反映 |
|---|---|
| WARN 10: `CameraOverrideSession` の snippet に `EditorUtility.InstanceIDToObject` が残存 | Dict key を `Camera` strong ref に変更、InstanceID 経由の lookup を全廃。Editor/Runtime API 二重性も解消 |
| PASS 1 inconsistency: Apply で null target の warning log が空文だった | scene name / GameObject name / index 付きで明示警告に強化 |
| New Q recommendation: strong refs vs InstanceID | Codex 推奨通り strong refs を採用、リスクは "leak window = env 生存期間のみ + Unity null-check で破棄済 cam を弾く" で十分小さい |

## Codex review v0.1 → v0.2 で反映した修正

| 指摘 | 反映 |
|---|---|
| FAIL 5: 静的 `_snapshot` | `CameraOverrideSession` (loader 所有) に移送、components は state を持たない |
| WARN 1: loader-owned apply/revert ordering | "Apply: Env → Volume → Camera / Revert: 逆順" を明示 |
| WARN 3: Volume 完全性契約 | env profile 作成契約セクション + import validator を追加 |
| WARN 4: Camera enumeration | `Camera.allCameras` を排除、`SceneCameraOverride.targets` を明示 list 化 |
| WARN 6: lifecycle race | "components are inert; OnEnable から Apply 呼ばない" を明示ルール化 |
| WARN 7: PlayMode test | Phase E2 受入基準として manual PlayMode canary を必須化 |
| WARN 8: silent omission | `ApplyVolumeOverrides` で警告ログ、`SceneValidator` で error 化 |
| WARN 9: directional light deferred | Phase E3 に明示繰り上げ (元案 deferred) |
| Q.c: snapshot fix 具体 API | `CameraOverrideSession` クラス全コード採用 |
| Q.d: baseVolumeOverride 不要 | 廃止、SampleScene Global Volume を mutate しない方針明示 |
| Q.e 1-5: open questions | すべて Codex 推奨で確定 |

## Open questions (v0.2 で解決済)

すべて v0.1 の Open questions は本 v0.2 で確定済。新規 open は無し。
