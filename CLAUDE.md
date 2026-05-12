# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**rhizomode** is a VR live-performance tool where the artist builds node graphs in real-time 3D space. Construction-as-performance: connecting nodes IS the show. Solo performer, PCVR (Quest Link via SteamVR), Unity 6 + URP. Launch target: 2026-05-16.

Full specs: `docs/TECHNICAL_DESIGN.md` (Japanese), coding standards: `docs/CODING_GUIDELINES.md` (Japanese).

## Unity Project

The Unity project lives in `rhizomode/` (nested directory). Open `rhizomode/` as the Unity project root.

- **Unity 6** with URP 17.3.0
- **C# 9** with `#nullable enable` on all files
- **R3** (reactive extensions) for signal flow — installed via NuGet (`com.cysharp.r3` + NuGetForUnity)
- **Input System** 1.18.0

## Architecture

```
XR → UI → Nodes → Core
              Modules → Core
              Audio → Core
              ExternalInput → Core
```

Seven assembly definitions (no circular references):

| Assembly | Purpose |
|---|---|
| `Rhizomode.Core` | Type system (ParamType: Float/Color/Bool), ports (IOutputPort/IInputPort), GraphContext, NodeBase, Edge, serialization |
| `Rhizomode.Nodes` | Node implementations (depends: Core) |
| `Rhizomode.Modules` | IPerformanceModule implementations — VFXModule, ShaderModule (depends: Core) |
| `Rhizomode.UI` | VR UI — menus, node display, status panel, edge visuals (depends: Core, Nodes) |
| `Rhizomode.XR` | Controller input, ray interaction, handlers (depends: Core, UI) |
| `Rhizomode.Audio` | AudioAnalyzer, device management (depends: Core) |
| `Rhizomode.ExternalInput` | OSC/MIDI input via OscJack/Minis (depends: Core) |

## Key Design Patterns

- **Reactive push model**: Nodes build R3 Observable chains in `Setup(GraphContext)`. No update loops except Time/LFO/Noise nodes.
- **Interface boundaries**: Module communication via `IPerformanceModule`, ports via `IOutputPort`/`IInputPort`. Never depend on concrete types across asmdef boundaries.
- **Type flexibility via object**: Ports use `object` internally; type checking happens at connection time via `ParamType` enum.
- **Defensive runtime**: try-catch around all external calls, NaN/null fallback to defaults. **Video must never stop.**
- **ModuleDefinition (ScriptableObject)**: Defines parameters for performance modules. Module nodes auto-spawn ConstFloat/ConstColor nodes pre-connected to all params.
- **Module lifecycle separation**: Factories create node objects only. Prefab instantiation + IPerformanceModule injection happens separately via `InjectModuleIfNeeded` (menu creation) or `ReinjectModulesAfterLoad` (graph load).

## Directory Structure

```
Assets/
├─ Runtime/
│   ├─ Core/           # NodeBase, GraphContext, ports, Edge, ParamType, serialization
│   ├─ Nodes/          # Input/, Math/, Modules/, Time/, Utility/, Generators/
│   ├─ Modules/        # IPerformanceModule impls (VFXModule, ShaderModule)
│   ├─ UI/             # VR menus, node display, edge visuals, status panel, mirror output
│   ├─ XR/             # Controller input, ray interaction, interaction handlers
│   ├─ Audio/          # AudioAnalyzer, device selection
│   └─ ExternalInput/  # OscServer, MidiServer, OscReceiverNode, MidiCCNode
├─ Data/
│   ├─ ModuleDefinitions/   # ScriptableObjects
│   ├─ Environments/
│   └─ SavedGraphs/         # JSON save files (snake_case.json)
├─ Shaders/
├─ VFX/
└─ Scenes/
```

## Testing

EditMode tests only, targeting Core layer:
- Port connection and type validation
- Observable signal flow (connect → propagate → dispose)
- Serialization round-trip (JSON)

```bash
# Run tests from Unity Test Runner or via Unity MCP tools
```

Individual node behavior is verified manually in VR. No PlayMode tests required.

## Code Standards

- **1 file = 1 class**, filename = classname (PascalCase). Exception: private serialization DTO (例: NodeBase 派生内で paramsJson の deserialize 専用に使う `private sealed class Params { ... }` のような構造) は、外部から見えないため同一ファイル内で許容する。
- **No `#region`**. If you need regions, split the class.
- **Methods ≤ 30 lines**. Break into helpers if longer.
- **No magic numbers**. Use `const` or ScriptableObject fields.
- **Minimal public surface**. Use `private`/`internal` (asmdef boundaries enforce `internal`).
- **DI**: `[SerializeField]` for MonoBehaviours, constructor injection for pure C#.
- **Comments**: Explain "why", not "what". XML docs on all public APIs.
- **Bool naming**: `Is`/`Has`/`Can`/`Try` prefixes.
- **Method naming**: `TryConnect` (may fail), `GetInputObservable<T>` (return type in name).

## Breaking Change Rules

These are **forbidden** unless fixing critical bugs:
- Changing interface signatures (add/remove/modify methods)
- Changing public method signatures
- Renaming/removing serialized JSON fields or node port names
- Changing asmdef dependency direction

To add interface capability: create a new interface (e.g., `ISmoothableModule`), check with `is` cast. To add serialized fields: append only, with safe defaults.

## Naming Conventions

| Target | Convention | Example |
|---|---|---|
| Classes/Interfaces | PascalCase | `BeatDetectorNode`, `IPerformanceModule` |
| Private fields | _camelCase | `_spectrum` |
| SerializeField | camelCase | `[SerializeField] private VisualEffect vfxGraph` |
| Constants | PascalCase | `DefaultBPM`, `SnapRadius` |
| Node type strings | PascalCase | `"BeatDetector"` |
| Save files | snake_case.json | `live_set_20260401.json` |

## Git Conventions

- **Branch**: `main` (always working), `feature/xxx` per feature
- **Commits**: Conventional format with Japanese or English body
  - Prefixes: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`
- **1 commit = 1 logical change**
- **Release tags**: `v0.1.0` (vertical slice), `v0.2.0` (all nodes + UI), `v0.3.0` (mirror + audio, launch)

## Performance Budget

- Max ~60 nodes (5-10 modules + 20-50 math/control nodes)
- VR: 90fps mandatory, Mirror output: 60fps
- VR rendering: Single Pass Instanced (URP)
- ShaderModule uses MaterialPropertyBlock (no material instances), batched to LateUpdate

## Current Implementation Status (2026-04-05)

### Completed

**Week 1 — 基盤 (Core)** ✅
- asmdef構成、Git初期化、NuGet (R3) 設定
- Core信号フロー: `NodeBase` → `OutputPort<T>` / `InputPort<T>` → `Edge` → `GraphContext`
- `ParamType` (Float/Color/Bool)、`IPerformanceModule`、`ModuleDefinition`
- シリアライズ: `GraphData` / `NodeData` / `EdgeData` JSON往復テスト合格

**Week 2 — XR + 最小UI** ✅
- `ControllerInputRouter`: InputSystem → R3 Observable変換、`IRayProvider`実装
- `ScrollMenuVisualController` / `ScrollMenuInteractionHandler`: 巻物式メニュー（左手操作）
- `NodeVisualManager` / `NodeVisualController` / `WorldPanelHost`: ノードWorldSpace表示
- `GameBootstrap`: 全システムの初期化・相互接続

**Week 3 — ノード操作** ✅
- `EdgeDragHandler`: エッジ接続（2クリックステートマシン方式。ドラッグ不要）
- `EdgeCutHandler`: エッジ切断（レイ近接→Right-B）
- `NodeDeleteHandler`: ノード削除（レイ選択→Right-A）
- `NodeGrabHandler`: ノードグラブ移動（Right-Grip + Left-Grip）
- `EdgeVisualManager` / `EdgeVisual`: LineRendererによるエッジ描画（カスタムグローシェーダー対応）

**Week 4 — ノード実装 + Audio** ✅
- 全24ノードタイプ実装:
  - Input: ConstFloat, ConstColor, AudioTrigger, BeatDetector, TapTempo, OscReceiver, MidiCC
  - Math: Multiply, Add, Remap, Smooth (Lerp/EaseOut切替)
  - Time: Time, Timer, Delay, LFO (4波形), Noise
  - Utility: Threshold, Toggle, ColorToFloats, FloatsToColor, ColorToHSV, HSVToColor, SceneObject
  - Monitors: FloatMonitor, BoolMonitor, ColorMonitor
  - Modules: VFXModuleNode, ShaderModuleNode (ModuleDefinitionから動的ポート生成)
- VFXModule / ShaderModule: IPerformanceModule実装
- AudioAnalyzer: FFT解析 + 帯域レベル取得
- ExternalInput: OscServer (OscJack), MidiServer (Minis) — #if条件コンパイル

**Week 5 — 縦スライス統合** ✅
- GraphSaveLoadManager: JSON セーブ/ロード
- MirrorOutputController: VR視点カメラ＋Lerpダンピング → RenderTexture
- Spout/NDI出力: SpoutSenderController, NdiSenderController
- AudioDeviceSelector: UIToolkitパネルでデバイス選択
- StatusPanelController: ノード数/エッジ数/BPM/FPS/Audio表示
- PresetManager: サブグラフテンプレート

**Week 6 — バグ修正** ✅
- Emit-after-Dispose guard (OutputPort/InputPort)
- 重複エッジ・自己接続防止 (GraphContext)
- モジュールライフサイクル分離 (Factory → InjectModuleIfNeeded → ReinjectModulesAfterLoad)
- メニューオープン中のEdgeDrag/EdgeCut/NodeDelete無効化
- ShaderModule SetPropertyBlock LateUpdateバッチ化
- NodeVisualController deferred bind リトライ上限
- モジュールノード自動スポーン (ConstFloat/ConstColor プリコネクト)

### VR UIパイプライン（重要な設計知識）

```
ControllerInputRouter (IRayProvider + IControllerInput)
  ↓ RayOrigin/RayDirection
SharedRaycastService (毎フレーム Physics.Raycast、結果を共有)
  ↓ RaycastHit
WorldPanelRayBridge (reflection経由でUIToolkitイベント注入)
  ↓ PointerDown/PointerUp/Hover
UIToolkit Panel (WorldPanelHost上のRenderTexture)
```

- ノードは `MeshCollider` 付きQuad。前面のみレイキャスト可（プレイヤー方向を向いて生成）
- `PanelSettings`はテーマ付きテンプレートからクローン（Unity 6要件）
- メニュー非表示は `SetActive(false)` ではなく `MeshRenderer/MeshCollider.enabled` トグル（UIDocument破壊防止）

### 残りのタスク（Week 6.5 〜5/16）

- パフォーマンス用VFX Graph / Shaderアセット制作（コンテンツ）
- 通しリハーサル
- タグ打ち（v0.3.0）
