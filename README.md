# NodeXRVJTools

> **構築プロセスそのものが演出（Construction as Performance）**
> VR空間内でノードグラフをライブビルドし、リアルタイム3D演出を構築・制御するソロパフォーマンスツール。

---

## 概要

**NodeXRVJTools** は、VRヘッドセットを被ったパフォーマー自身が、ライブの最中に3D空間へ手でノードを並べ、ポート同士を繋ぎ、エフェクトのグラフを「ゼロから組み立てていく」ことそのものを演出として観客に見せる——という一点に賭けたパフォーマンスツールです。

一般的なVJツールが「あらかじめ用意したシーンを切り替える」のに対し、NodeXRVJTools では **ノードを繋ぐ瞬間が、映像に命が吹き込まれる瞬間** になります。AudioTrigger をビートに繋ぐ、LFO を色に繋ぐ、Threshold でゲートを作る——その配線作業のすべてがステージ上で起きる、いわば「即興のパッチング」です。

そのため設計の軸はただひとつ、**映像は絶対に止めない**こと。本番中にノードを抜き差ししても、外部入力が壊れた値を送ってきても、グラフが途中状態でも、レンダリングが破綻しないように全体が組まれています。

### 一行で言うと

> VR空間で、リアルタイムにノードグラフを組み立てながら3D演出を駆動する、ソロ・PCVR専用のライブパフォーマンスツール。

### 特徴

- **構築 = 演出** — ノードの生成・接続・切断・移動のすべてがVR内のジェスチャーで完結し、それ自体がショーになる。
- **リアクティブな信号フロー** — 値が変わったときだけ下流が発火する R3（Reactive Extensions）ベースのプッシュモデル。無駄なUpdateループを持たない。
- **絶対に止まらない映像** — 外部呼び出しは全て try-catch、NaN / null はデフォルト値にフォールバック。本番での事故を構造的に潰す Defensive Runtime 設計。
- **厳格なレイヤー分離** — asmdef（アセンブリ定義）レベルで依存方向を一方向に強制し、循環参照を構造的に不可能にしている。
- **モジュール式の演出** — VFX Graph / Shader を `IPerformanceModule` として抽象化し、`ModuleDefinition`（ScriptableObject）でパラメータを宣言的に定義。
- **外部連携** — Audio（FFT解析・帯域レベル）、OSC、MIDI を入力ノードとして取り込み、Spout / NDI でミラー出力を外部に送出。

### 基本情報

| 項目 | 内容 |
|---|---|
| ユーザー | 1人（ソロパフォーマー専用） |
| デバイス | PCVR 固定（Quest Link via SteamVR） |
| エンジン | Unity 6 + URP 17.3.0 |
| 言語 | C# 9（全ファイル `#nullable enable`） |
| リアクティブ基盤 | R3（NuGetForUnity 経由） |
| 入力 | Input System 1.18.0 |
| ローンチ目標 | 2026-05-16 |

---

## アーキテクチャ

依存は上から下への一方向のみ。上ほどユーザー入力に近く、下ほど純粋ロジック。

```
VR UI Layer      Menu / Node Display / Status Panel
XR Layer         Controller Input / Ray Interaction
Node Graph       NodeBase / Ports / Edges / GraphContext
Modules          VFXModule / ShaderModule / …
Audio            AudioAnalyzer / Device Selection
Core             Type System / Serialization / Signal Flow
```

asmdef の依存方向（循環参照は構造的に不可能）:

```
XR ──▶ UI ──▶ Nodes ──▶ Core
              Modules ──▶ Core
              Audio ──▶ Core
              ExternalInput ──▶ Core
```

> 注: 上記は概念上の7アセンブリ構成です。v5.4 の大規模リファクタにより、現在の実装はこれを 48 asmdef（`SharedKernel` 最下層 + `Graph.*` 分割 + 各システムの `Contracts/Impl/GraphAdapter` 等）へさらに細分化しています。詳細は `CLAUDE.md` を参照してください。

---

## 主要な設計パターン

1. **Reactive Push モデル** — ノードは `Setup(GraphContext)` 内で R3 Observable チェーンを構築。値が変わったときだけ下流が発火する。Update ループを持つのは Time / LFO / Noise のみ。
2. **インターフェース境界** — モジュール通信は `IPerformanceModule`、ポートは `IOutputPort` / `IInputPort`。asmdef を越えて具象型に依存しない。
3. **object 経由の型柔軟性** — ポート内部値は `object`。型チェックは接続時に `ParamType` enum（Float / Color / Bool）で行う。後から Vector3 等を増やしてもポートインターフェースは変えない。
4. **Defensive Runtime** — 外部呼び出しは全て try-catch、NaN / null はデフォルト値にフォールバック。**映像は絶対に止めない**。
5. **ModuleDefinition（ScriptableObject）** — 演出モジュールのパラメータ定義。モジュールノードを生成すると ConstFloat / ConstColor が自動スポーンされ、全パラメータに事前接続される。
6. **モジュールライフサイクル分離** — Factory はノードオブジェクトを作るだけ。プレハブ生成と `IPerformanceModule` 注入は別タイミング（`InjectModuleIfNeeded` / `ReinjectModulesAfterLoad`）。

---

## 型システム

| 型 | 用途 | 備考 |
|---|---|---|
| Float | 連続値。パラメータ制御全般 | ConstFloat は 0〜1、Remap で変換 |
| Color | 色 | HSVホイール入力 |
| Bool | トリガー / ゲート | VFX SendEvent、Activate / Deactivate、条件分岐 |
| Vector3 | 後日追加予定 | object 経由なので I/F 変更不要 |

---

## ノード一覧（24タイプ）

- **Input**: ConstFloat, ConstColor, AudioTrigger, BeatDetector, TapTempo, OscReceiver, MidiCC
- **Math**: Multiply, Add, Remap, Smooth（Lerp / EaseOut 切替）
- **Time**: Time, Timer, Delay, LFO（4波形）, Noise
- **Utility**: Threshold, Toggle, ColorToFloats, FloatsToColor, ColorToHSV, HSVToColor, SceneObject
- **Monitor**: FloatMonitor, BoolMonitor, ColorMonitor
- **Module**: VFXModuleNode, ShaderModuleNode（`ModuleDefinition` から動的ポート生成）

---

## VR UI パイプライン

VRコントローラーのレイが UIToolkit の WorldSpace パネルを操作する流れ。Unity 6 の UIToolkit は WorldSpace 未対応のため、RenderTexture + reflection でイベント注入している。

```
ControllerInputRouter (IRayProvider + IControllerInput)
       │ RayOrigin / RayDirection
       ▼
SharedRaycastService (毎フレーム Physics.Raycast、結果を共有)
       │ RaycastHit
       ▼
WorldPanelRayBridge (reflection で UIToolkit イベント注入)
       │ PointerDown / PointerUp / Hover
       ▼
UIToolkit Panel (WorldPanelHost 上の RenderTexture)
```

---

## パフォーマンス予算

- VR **90fps 必達** / ミラー出力 **60fps**
- VR レンダリング: **Single Pass Instanced**（URP）
- ShaderModule は **MaterialPropertyBlock** 使用（マテリアル複製なし）、LateUpdate でバッチ化

---

## プロジェクト構成

```
.
├─ NodeXRVJTools/        Unity プロジェクト本体（Assets / Packages / ProjectSettings）
├─ docs/             技術設計・コーディング規約・各種監査ドキュメント
├─ scripts/          asmdef 境界検証・構造図生成スクリプト
├─ CLAUDE.md         Claude Code 向けプロジェクトガイド（実装状況の最新ソース）
└─ AGENTS.md         Codex 向けプロジェクトガイド
```

### 開発環境

- **Unity 6** with URP 17.3.0
- **C# 9**（全ファイル `#nullable enable`）
- **R3**（リアクティブ拡張）— NuGetForUnity 経由（`com.cysharp.r3`）
- **Input System** 1.18.0

---

## ドキュメント

| ファイル | 内容 |
|---|---|
| `docs/TECHNICAL_DESIGN.md` | 技術設計の全仕様（日本語） |
| `docs/CODING_GUIDELINES.md` | コーディング規約（日本語） |
| `docs/NodeXRVJTools_structure_brief.md` | 構造説明用のデザインブリーフ |
| `docs/SCENE_AUTHORING.md` | シーンオーサリング手順 |
| `docs/NDI_USAGE.md` | NDI 出力の使い方 |
| `CLAUDE.md` | 実装状況・アーキテクチャの最新まとめ |

---

## テスト

EditMode テストのみ。Core 層を対象に以下を検証します。

- ポート接続と型バリデーション
- Observable 信号フロー（接続 → 伝播 → 破棄）
- シリアライズ往復（JSON）

個々のノードの挙動は VR 内で手動確認。PlayMode テストは不要。

---

## コーディング規約（抜粋）

- **1 ファイル = 1 クラス**、ファイル名 = クラス名（PascalCase）
- **`#region` 禁止**。必要ならクラスを分割する
- **メソッドは 30 行以内**
- **マジックナンバー禁止**。`const` または ScriptableObject フィールドを使う
- **public 表面を最小化**。`private` / `internal` を基本とする

詳細は `docs/CODING_GUIDELINES.md` を参照してください。

---
