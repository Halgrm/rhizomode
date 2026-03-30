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
```

Six assembly definitions (no circular references):

| Assembly | Purpose |
|---|---|
| `Rhizomode.Core` | Type system (ParamType: Float/Color/Bool), ports (IOutputPort/IInputPort), GraphContext, NodeBase, Edge, serialization |
| `Rhizomode.Nodes` | Node implementations (depends: Core) |
| `Rhizomode.Modules` | IPerformanceModule implementations — VFXModule, ShaderModule (depends: Core) |
| `Rhizomode.UI` | VR UI — menus, node display, status panel (depends: Core, Nodes) |
| `Rhizomode.XR` | Controller input, ray interaction (depends: Core, UI) |
| `Rhizomode.Audio` | AudioAnalyzer, device management (depends: Core) |

## Key Design Patterns

- **Reactive push model**: Nodes build R3 Observable chains in `Setup(GraphContext)`. No update loops except Time/LFO/Noise nodes.
- **Interface boundaries**: Module communication via `IPerformanceModule`, ports via `IOutputPort`/`IInputPort`. Never depend on concrete types across asmdef boundaries.
- **Type flexibility via object**: Ports use `object` internally; type checking happens at connection time via `ParamType` enum.
- **Defensive runtime**: try-catch around all external calls, NaN/null fallback to defaults. **Video must never stop.**
- **ModuleDefinition (ScriptableObject)**: Defines parameters for performance modules. Module nodes auto-spawn ConstFloat/ConstColor nodes pre-connected to all params.

## Directory Structure (Target)

```
Assets/
├─ Runtime/
│   ├─ Core/           # NodeBase, GraphContext, ports, Edge, ParamType, serialization
│   ├─ Nodes/          # Input/, Math/, Modules/, Time/, Utility/
│   ├─ Modules/        # IPerformanceModule impls + prefabs
│   ├─ UI/             # VR menus, color picker, status panel
│   ├─ XR/             # Controller input, ray interaction
│   └─ Audio/          # AudioAnalyzer, device selection
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

- **1 file = 1 class**, filename = classname (PascalCase). No exceptions.
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
- ShaderModule uses MaterialPropertyBlock (no material instances)
