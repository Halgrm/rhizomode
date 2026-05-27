# Environment Scene Authoring Contract

rhizomode の環境シーン (concrete / Dark / Forest / Experience / Nature / Ruins / 等) を作る時の component 配置契約。

## 全体像

```
SampleScene.unity                  ← base / bootstrap (常時ロード)
  ├─ XR Origin, RootLifetimeScope, Game Manager, …  (全 rhizomode infra)
  ├─ Global Volume (SampleSceneProfile)            ← Bloom / Tonemapping / Vignette
  ├─ Directional Light + EnvOverridableDirectionalLight marker
  ├─ Main Camera (HMD) + EnvOverridableCamera marker
  └─ MirrorOutput camera + EnvOverridableCamera marker

[Environment Scene].unity          ← AdditiveSceneLoader が 1 つだけ additive ロード
  └─ SceneEnvironment_[Name] (1 root)
       ├─ SceneEnvironment          (必須)
       ├─ SceneVolumeOverride       (強く推奨)
       └─ SceneCameraOverride       (推奨)
```

## 必須 / 推奨 component

| Component | 必須度 | 役割 |
|---|---|---|
| `SceneEnvironment` | **必須** | skybox / ambient / fog / reflection / disableBaseDirectionalLight |
| `SceneVolumeOverride` | **強く推奨** | Bloom / Tonemapping / Vignette 等 post-FX を env-local profile で上書き。無いと SampleSceneProfile が漏れる |
| `SceneCameraOverride` | 推奨 | base scene の `EnvOverridableCamera` marker camera (Main HMD + Mirror) の clear flags / background color を env-local に |

## 適用ライフサイクル

`AdditiveSceneLoader` が env load 完了時に以下の順で適用する:

1. `SceneEnvironment.Apply()` — RenderSettings (skybox / ambient / fog / reflection)
2. `ApplyDirectionalLightToggle` — `disableBaseDirectionalLight=true` なら base scene の `EnvOverridableDirectionalLight` marker 付き Directional Light を一時 disable
3. `ApplyVolumeOverrides` — env 内の全 `SceneVolumeOverride.Apply()` (動的 Volume 生成、priority 100 で base を覆い被せる)
4. `ApplyCameraOverrides` — env 内の `SceneCameraOverride` + base の `EnvOverridableCamera` marker camera に clear flags / bg を適用 (`CameraOverrideSession` が snapshot)

unload 時は逆順:

1. Camera (session.Dispose → snapshot から復元)
2. Volume (動的 Volume を Destroy)
3. Directional Light (snapshot から復元)
4. SceneEnvironment (baseSceneEnvironment.Apply で fallback)

## SceneEnvironment 設定

| Field | 用途 | 典型値 |
|---|---|---|
| `skyboxMaterial` | env-local skybox material (null = camera が default skybox を描く) | concrete: 真っ黒 `Black_Skybox.mat` |
| `ambientMode` | Skybox / Trilight / Flat / Custom | concrete (skybox 遮断): Flat |
| `ambientSkyColor` | Flat 時の全方向 ambient 1 色 | concrete: `(0.03, 0.03, 0.04)` |
| `fogEnabled` | RenderSettings.fog | 屋内 = false、霧霊 = true |
| `reflectionIntensity` | skybox IBL → reflection probe contribution | concrete (skybox 遮断): 0 |
| `disableBaseDirectionalLight` | base scene の Directional Light を一時 disable | 屋内 env (concrete) = true |

## SceneVolumeOverride 設定

env-local `VolumeProfile` を inspector に割当てる。priority=100 (default、SampleScene Global Volume の priority 0 を覆い被せる)。

**重要 — Profile 作成契約**: base `SampleSceneProfile` が active にしている全 effect (Bloom / Vignette / Tonemapping / 等) を env profile にも **override 済 + weight=1** で含めること。理由は URP の Volume system が override property 単位で priority を見るため、env profile に該当 effect の override が無いと base 値が漏れる。

env で effect を「殺す」場合の典型値:

| Effect | env override 値 |
|---|---|
| Bloom | `intensity = 0`, `threshold = 999` |
| Vignette | `intensity = 0` |
| Tonemapping | `mode = None` |
| ColorAdjustments / etc. | env で使うなら override、不要なら neutral 値で override |

## SceneCameraOverride 設定

env-local の clear flags / background color を、以下の合算 camera に適用:

1. `targets` field に explicit に並べた `Camera` (env scene 内の camera 用)
2. base scene の `EnvOverridableCamera` marker 付き camera (SampleScene の Main HMD + Mirror) — **自動 wiring**

env scene は通常 `targets` を空のままで OK (marker が cross-scene wiring を解決する)。env scene 自身に camera がある場合のみ explicit targets を使う。

## SampleScene 側 marker

base scene (SampleScene.unity) の以下に marker を attach 済:

- `XR Origin > Camera Offset > Main Camera` (HMD) → `EnvOverridableCamera`
- `MirrorOutput` → `EnvOverridableCamera`
- `Directional Light` → `EnvOverridableDirectionalLight`

新規に env-override したい camera / light を base に追加した場合は marker も attach すること。

## 検証

`Tools > Rhizomode > Validate Env Scenes` メニューで全 env scene の契約準拠を一括チェックできる。

- `SceneEnvironment` 欠落 → error (全 env scene)
- `SceneVolumeOverride` 欠落 → warning (`concrete` は launch-critical で error)
- 1 env scene に複数同 component → warning

CI で `EnvSceneValidator.Validate()` を呼ぶことで build 時 gate 化可能 (Phase E5+ 検討事項)。

## 既存 env scene の状態 (2026-05-27 時点)

| Scene | SceneEnvironment | SceneVolumeOverride | SceneCameraOverride | 備考 |
|---|---|---|---|---|
| concrete | ✓ | ✓ (`Volume_Concrete.asset`) | ✓ | 室内モチーフ、bloom 遮断、skybox 黒、太陽光 off |
| Dark | ✓ | — | — | 暗い屋外、base post-FX 流用 |
| Forest | ✓ | — | — | 屋外 vegetation、base post-FX 流用 |
| Experience | — (要追加) | — | — | minimal scene、base に依存 |
| Nature | ✓ | — | — | base post-FX 流用 |
| Ruins | ✓ | — | — | dev.ameye.caustics 依存、base post-FX 流用 |

## 関連ファイル

- 設計プラン: `docs/plans/env-scene-isolation.md` (v0.3)
- ランタイム: `rhizomode/Assets/Runtime/Scene/Runtime/`
  - `SceneEnvironment.cs`
  - `SceneVolumeOverride.cs`
  - `SceneCameraOverride.cs`
  - `CameraOverrideSession.cs`
  - `EnvOverridableCamera.cs`
  - `EnvOverridableDirectionalLight.cs`
  - `AdditiveSceneLoader.cs`
- Editor: `rhizomode/Assets/Editor/Scene/EnvSceneValidator.cs`
- Test: `rhizomode/Assets/Tests/Editor/Scene/` (13 件)
