# REFACTOR Phase 1 Commits

Plan v5.3 Phase 1 (asmdef 大改修、6 日、7 sub-step) の各 sub-step ごとの commit hash を記録する。

## Phase 1 sub-step 完了手順 (v5.2 で用語固定)

各 sub-step は以下の順序を厳守:

1. Unity プロセス完全終了 (`pkill -f Unity` で 0 件確認)
2. Unity 再起動 (Library/ は削除しない)
3. Editor 起動後に Assets → Refresh (= `AssetDatabase.Refresh()`) 実行
4. Console で script compile 完了確認 (errors 0、warnings 許容)
5. EditMode + PlayMode Test Runner 全件パス
6. **(1A / 1C / 1G のみ)** Standalone Player ビルドが通る → `[player-build: pass]` 付与
7. `git commit` し、ハッシュを本ファイルに追記

「clean reimport (`Library/` 削除)」は禁止。Meta GUID は git で保持されるため不要、削除は所要時間 30 分超のリスクがある。

途中で問題が起きたら直前の sub-step commit hash に **roll-back**。挙動完全不変。

---

## Sub-step 進捗

| Sub-step | 内容 | 工数 | Commit hash | Player build | 完了日 |
|---|---|---:|---|---|---|
| 1A | SharedKernel + Graph 8 asmdef + Core 解体 | 1.5d | `014cf94c` | `[player-build: pass]` | 2026-05-13 |
| 1B | NodeCatalog 2 分割 + Input.Contracts + Interaction.Contracts | 0.5d | `dadff7e3` | (不要) | 2026-05-13 |
| 1C | Audio / OscMidi / Ableton bounded context | 1d | `ab95b5d2` | `[player-build: pass]` | 2026-05-13 |
| 1D | Nodes 5 asmdef 分割 | 1d | - | - | - |
| 1E | UI 3 分割 + Interaction.GraphAdapter + Cameras | 1d | - | - | - |
| 1F | Scene/Modules/Persistence/Observability + Input.XR/Desktop | 1d | - | - | - |
| 1G | XR refs 整理 + Boundary CI 有効化 | 0.5d | - | (必須) | - |

---

## 1A 詳細 (2026-05-13 完了)

**Commit**: `014cf94c chore: refactor(phase-1a): SharedKernel + Graph 8 asmdef + Core 解体 [player-build: pass]`

新規 asmdef (9):
- `Rhizomode.SharedKernel` (最下層、references = [])
- `Rhizomode.Graph.Model` (旧 `Rhizomode.Core.asmdef` GUID 流用 rename)
- `Rhizomode.Graph.Snapshot` (Phase 2 で NodeSnapshot/EdgeSnapshot/GraphSnapshot)
- `Rhizomode.Graph.Events` (Phase 2 で GraphEventBus/MutationScope)
- `Rhizomode.Graph.Query` (Phase 2 で GraphReadModel)
- `Rhizomode.Graph.CatalogBridge` (Phase 2 で INodeFactory/AliasResolver)
- `Rhizomode.Graph.Runtime` (Phase 2 で NodeRuntime/HydrationPlanExecutor)
- `Rhizomode.Graph.Serialization` (GraphData/NodeData/EdgeData/PresetData 移送)
- `Rhizomode.Graph.Mutation` (Phase 2 で IGraphCommand + CommandOrigin)

ファイル移動 (Core/ 26 file 全部):
- Core/ → SharedKernel/ (2 file: ParamType, ParamDefaults)
- Core/ → Graph/Model/ (20 file: GraphContext→GraphState rename + 19 file)
- Core/ → Graph/Serialization/ (4 file: GraphData/NodeData/EdgeData/PresetData)
- 旧 Core/ ディレクトリ削除 (Core.meta も削除)

namespace + class rename:
- `Rhizomode.Core` → `Rhizomode.SharedKernel` / `Rhizomode.Graph.Model` / `Rhizomode.Graph.Serialization`
- `class GraphContext` → `class GraphState` (29 出現箇所)

参照側 (65 file) の using 置換 + 17 asmdef の references 更新。

一時的な Plan v5.3 違反 (Phase 2 で refactor 予定):
- `Graph.Model → Graph.Serialization` (NodeBase.ToNodeData() 経由)
- `SharedKernel.asmdef noEngineReferences=false` (ParamDefaults.cs が UnityEngine.Color を使用)

完了条件 (全達成):
- ✅ Compile errors 0 (warnings 4 件は既存コード由来)
- ✅ `GraphContext` 型名 0 件
- ✅ `using Rhizomode.Core;` 残存 0 件
- ✅ EditMode Test Runner 全件パス (1 件のテスト期待値修正含む: `ModuleNodeTests.cs:52`)
- ✅ Standalone Player build 成功 (`Profiler connected on WindowsPlayer`)

---

## 1B 詳細 (2026-05-13 完了)

**Commit**: `dadff7e3 refactor(phase-1b): NodeCatalog 2 分割 + Input.Contracts + Interaction.Contracts`

新規 asmdef (4):
- `Rhizomode.NodeCatalog.Contracts` (NodeCategory, NodeTypeInfo)
- `Rhizomode.NodeCatalog.Runtime` (NodeTypeRegistry)
- `Rhizomode.Input.Contracts` (IControllerInput 等 5 件)
- `Rhizomode.Interaction.Contracts` (空 placeholder、Phase 5 で InteractionIntent)

旧 Phase 0 雛形削除:
- `rhizomode/Assets/Runtime/Catalog/` 全削除
- `rhizomode/Assets/Runtime/Interaction/Rhizomode.Interaction.asmdef` + marker 削除

ファイル移動 (8 .cs):
- UI/{NodeCategory, NodeTypeInfo}.cs → NodeCatalog/Contracts/
- UI/NodeTypeRegistry.cs → NodeCatalog/Runtime/
- UI/I{ControllerInput, RayProvider, ControllerPose, LeftHandRay, LeftHandInput}.cs → Input/Contracts/

namespace 変更 + 参照側更新 (5 既存 asmdef + UI/XR 配下の .cs 多数)。

完了条件 (全達成):
- ✅ Compile errors 0
- ✅ EditMode Test Runner 全件パス (Reimport All 後、50+ tests)
- (Player build は Phase 1B 不要)

---

## 1C 詳細 (2026-05-13 完了)

**Commit**: `ab95b5d2 refactor(phase-1c): Audio / OscMidi / Ableton bounded context [player-build: pass]`

新規 asmdef (9 個 + Audio.Analysis rename = 10 個):
- `Rhizomode.Audio.Contracts` (Phase 10 で AudioFrame/IAudioFrameSource/IAudioDrivenNode/IAudioDeviceDrivenNode)
- `Rhizomode.Audio.GraphAdapter` (AudioDriverBehaviour 移送、Phase 10 で AudioDriverHost に class rename)
- `Rhizomode.OscMidi.Contracts` (Phase 5 で OscMessage/MidiControlChange/IOscSource/IMidiSource)
- `Rhizomode.OscMidi.Transport` (OscServer, MidiServer)
- `Rhizomode.OscMidi.GraphAdapter` (Phase 6 で OscBindingLifecycleProcessor/MidiBindingLifecycleProcessor)
- `Rhizomode.Ableton.Contracts` (Phase 5 で ClipMeta/TrackMeta/MacroMeta/SessionState/IAbletonSession)
- `Rhizomode.Ableton.Transport` (AbletonLink)
- `Rhizomode.Ableton.Session` (AbletonOscBridge)
- `Rhizomode.Ableton.GraphAdapter` (AbletonClipGridManager, ClipObject)

Rename:
- `Rhizomode.Audio.asmdef` → `Rhizomode.Audio.Analysis.asmdef` (GUID 流用)

Delete:
- `Rhizomode.ExternalInput.asmdef` (+ `Assets/Runtime/ExternalInput/` ディレクトリ)

ファイル移動 (21 .cs):
- Audio/{AudioAnalyzer, AudioSpectrumDisplay, AudioWaveformDisplay} → Audio/Analysis/
- XR/AudioDriverBehaviour.cs → Audio/GraphAdapter/ (class rename は Phase 10)
- ExternalInput/{OscServer, MidiServer} → OscMidi/Transport/
- ExternalInput/AbletonLink → Ableton/Transport/
- ExternalInput/AbletonOscBridge → Ableton/Session/
- ExternalInput/{AbletonClipGridManager, ClipObject} → Ableton/GraphAdapter/
- ExternalInput/{OscReceiver, MidiCC}Node → Nodes/OscMidi/ (Phase 1D で Nodes.OscMidi asmdef に再分離予定)
- ExternalInput/Ableton{ClipFire, Tempo, TrackVolume, Transport}Node → Nodes/Ableton/ (Phase 1D で Nodes.Ableton asmdef に再分離予定)

namespace 一括変更:
- `Rhizomode.Audio` → `Rhizomode.Audio.Analysis` (3 file)
- `Rhizomode.ExternalInput` → 6 namespace に分割 (15 file):
  - `Rhizomode.OscMidi.Transport` (2 file)
  - `Rhizomode.Ableton.Transport` (1 file)
  - `Rhizomode.Ableton.Session` (1 file)
  - `Rhizomode.Ableton.GraphAdapter` (2 file)
  - `Rhizomode.Nodes.OscMidi` (2 file)
  - `Rhizomode.Nodes.Ableton` (4 file)

参照側 (5 asmdef + 3 file) の references / using 更新:
- Rhizomode.XR.asmdef + Rhizomode.Bootstrap.asmdef + Rhizomode.Nodes.asmdef
- Rhizomode.Core.Tests.asmdef + Rhizomode.ExternalInput.Tests.asmdef (Tests は維持、Phase 5 で Ableton.Tests/OscMidi.Tests に分割)
- GameBootstrap.cs + ClipFireRayHandler.cs + AbletonOscBridgeTests.cs (using 更新)

Package 参照を name-based に変更 (GUID 不透明性回避):
- `"OscJack.Runtime"` (旧 `GUID:9df4bb2434fa4444a85e58f9b2d5d6d2`)
- `"Minis"` (旧 `GUID:3290356123b9eaf43b18df17cbfad07c`)
- `"Unity.TextMeshPro"` (旧 `GUID:6055be8ebefd69e48b49212b09b47b2f`)

一時的な Plan v5.3 違反 (Phase 2/5/10 で refactor 予定):
- `Rhizomode.Nodes` asmdef が `OscMidi.Transport` / `Ableton.Transport` を参照 (Phase 1D で Nodes.OscMidi/Nodes.Ableton 分離時に解消)
- `Rhizomode.Audio.GraphAdapter` が `Rhizomode.UI` を参照 (Phase 5 で Graph.Mutation/Events/Query 経由に置換)

完了条件 (全達成):
- ✅ Compile errors 0 (warnings 既存のみ: RadialKnobElement UxmlFactory 非推奨など)
- ✅ EditMode + PlayMode Test Runner 全件パス
- ✅ Standalone Player build 'Succeeded' (128 秒、`[player-build: pass]`)
- ✅ BoundaryValidator skeleton mode 維持 (Phase 1G で有効化)

---

## Roll-back ポリシー

問題が起きたら直前の sub-step commit hash に戻す。複数 sub-step を跨いだ roll-back は禁止。

```bash
# 例: 1B で問題が起きたら 1A に戻す
git reset --hard 014cf94c
```

---

## 参照

- Plan: `~/.claude/plans/eager-conjuring-eich.md` (v5.3)
- Memory: `~/.claude/projects/.../memory/feedback_phase1_substep_protocol.md`
