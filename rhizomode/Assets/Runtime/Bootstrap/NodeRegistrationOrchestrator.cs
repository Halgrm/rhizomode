#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using Rhizomode.Nodes.Ableton;
using Rhizomode.Nodes.OscMidi;
using Rhizomode.Nodes.Audio;
using Rhizomode.Nodes.Scene;
using Rhizomode.Nodes.Generators;
using Rhizomode.Nodes.Input;
using Rhizomode.Nodes.Math;
using Rhizomode.Nodes.Modules;
using Rhizomode.Nodes.Time;
using Rhizomode.Nodes.Utility;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// 起動時のノード type / factory 登録を集約する orchestrator。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 F-8.2 抽出 3/N (Codex Round C advisory への対応): GameBootstrap.NodeFactoryMap +
    /// RegisterNodeTypes + RegisterFactories + RegisterModuleTypes + RegisterObject3DTypes を集約。
    /// 起動時に 1 度だけ <see cref="RegisterAll"/> を呼び、Object3D prefab map を
    /// <c>ModuleLifecycleProcessor</c> に渡す。
    ///
    /// 配置: Rhizomode.Bootstrap asmdef (Plan v5.4 §15)。V2a で XR asmdef から移送 (internal 化)。
    /// <c>CatalogInstaller</c> が VContainer Configure 時に構築し、Build 後に
    /// <c>EntryPointBootstrapper</c> が <see cref="RegisterAll"/> を明示的に駆動する。
    /// </remarks>
    internal sealed class NodeRegistrationOrchestrator
    {
        /// <summary>
        /// 静的ノード factory 辞書。typeName → factory delegate。
        /// SceneDark/SceneWhite/SceneNature の 3 件は Phase 5 で SceneTriggerCatalog SO に
        /// 置換予定だがハードコードで残置 (旧 GameBootstrap.NodeFactoryMap と同一)。
        /// </summary>
        private static readonly Dictionary<string, Func<string, NodeBase>> StaticFactoryMap = new()
        {
            ["ConstFloat"] = id => new ConstFloatNode(id),
            ["ConstColor"] = id => new ConstColorNode(id),
            ["AudioDevice"] = id => new AudioDeviceNode(id),
            ["AudioTrigger"] = id => new AudioTriggerNode(id),
            ["BeatDetector"] = id => new BeatDetectorNode(id),
            ["TapTempo"] = id => new TapTempoNode(id),
            ["OscReceiver"] = id => new OscReceiverNode(id),
            ["MidiCC"] = id => new MidiCCNode(id),
            ["Multiply"] = id => new MultiplyNode(id),
            ["Smooth"] = id => new SmoothNode(id),
            ["Time"] = id => new TimeNode(id),
            ["LFO"] = id => new LfoNode(id),
            ["Noise"] = id => new NoiseNode(id),
            ["Threshold"] = id => new ThresholdNode(id),
            ["Toggle"] = id => new ToggleNode(id),
            ["AudioMonitor"] = id => new AudioMonitorNode(id),
            ["FloatMonitor"] = id => new FloatMonitorNode(id),
            ["BoolMonitor"] = id => new BoolMonitorNode(id),
            ["ColorMonitor"] = id => new ColorMonitorNode(id),
            ["Add"] = id => new AddNode(id),
            ["Remap"] = id => new RemapNode(id),
            ["Delay"] = id => new DelayNode(id),
            ["Timer"] = id => new TimerNode(id),
            ["ColorToFloats"] = id => new ColorToFloatsNode(id),
            ["FloatsToColor"] = id => new FloatsToColorNode(id),
            ["ColorToHSV"] = id => new ColorToHSVNode(id),
            ["HSVToColor"] = id => new HSVToColorNode(id),
            ["AudioBand"] = id => new AudioBandNode(id),
            ["SpectrumMonitor"] = id => new SpectrumMonitorNode(id),
            ["Trigger"] = id => new TriggerNode(id),
            ["SceneSwitch"] = id => new SceneSwitchNode(id),
            ["SceneDark"] = id => new SceneTriggerNode(id, "SceneDark", 0),
            ["SceneWhite"] = id => new SceneTriggerNode(id, "SceneWhite", 1),
            ["SceneNature"] = id => new SceneTriggerNode(id, "SceneNature", 2),
            ["AbletonTempo"] = id => new AbletonTempoNode(id),
            ["AbletonTransport"] = id => new AbletonTransportNode(id),
            ["AbletonTrackVolume"] = id => new AbletonTrackVolumeNode(id),
            ["AbletonClipFire"] = id => new AbletonClipFireNode(id),
        };

        private readonly NodeTypeRegistry _typeRegistry;
        private readonly GraphState _graphState;
        private readonly ModuleDefinition[]? _moduleDefinitions;
        private readonly Object3DPrefabList? _object3DPrefabs;

        /// <summary>登録した Object3D prefab の逆引きマップ (instantiate 用)。</summary>
        public Dictionary<string, GameObject> Object3DPrefabMap { get; } = new();

        public NodeRegistrationOrchestrator(
            NodeTypeRegistry typeRegistry,
            GraphState graphState,
            ModuleDefinition[]? moduleDefinitions,
            Object3DPrefabList? object3DPrefabs)
        {
            _typeRegistry = typeRegistry;
            _graphState = graphState;
            _moduleDefinitions = moduleDefinitions;
            _object3DPrefabs = object3DPrefabs;
        }

        /// <summary>
        /// 全 type / factory を順番に登録する。
        /// </summary>
        public void RegisterAll()
        {
            RegisterStaticTypesFromScanner();
            RegisterStaticFactories();
            RegisterModuleTypes();
            RegisterObject3DTypes();
            // CinemachineModule 用 factory は未実装のため type 登録も skip (Phase 4F 注記参照)
        }

        /// <summary>
        /// [NodeType] 属性付きクラスを Scanner で発見して NodeTypeRegistry に流し込む。
        /// 動的 SceneTrigger 3 件 (SceneDark/SceneWhite/SceneNature) は UI menu 用に手動補完。
        /// </summary>
        private void RegisterStaticTypesFromScanner()
        {
            var scanner = new NodeTypeAttributeScanner();
            foreach (var registration in scanner.Scan())
            {
                var d = registration.Display;
                _typeRegistry.Register(new NodeTypeInfo(d.TypeName, d.Label, d.Category));
            }

            _typeRegistry.Register(new NodeTypeInfo("SceneDark", "Dark", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneWhite", "White", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneNature", "Nature", NodeCategory.Scene));
        }

        /// <summary>
        /// NodeTypeRegistry に登録済の typeName に対応する factory を StaticFactoryMap から取得して GraphState に登録。
        /// </summary>
        private void RegisterStaticFactories()
        {
            foreach (var typeName in _typeRegistry.AllTypes.Keys)
            {
                if (!StaticFactoryMap.TryGetValue(typeName, out var factory))
                {
                    Debug.LogError($"[NodeRegistrationOrchestrator] No factory for registered type: {typeName}");
                    continue;
                }
                _graphState.RegisterNodeFactory(typeName, factory);
            }
        }

        /// <summary>
        /// ModuleDefinition 配列から VFX_/Shader_ の動的 typeName とファクトリを登録。
        /// Prefab 注入は ModuleLifecycleProcessor が AfterSetup で実施するため、ここは factory のみ。
        /// </summary>
        private void RegisterModuleTypes()
        {
            if (_moduleDefinitions == null) return;

            foreach (var def in _moduleDefinitions)
            {
                if (def == null) continue;
                var capturedDef = def;

                var hasVfx = def.prefab != null && def.prefab.GetComponent<VFXModule>() != null;
                var hasShader = def.prefab != null && def.prefab.GetComponent<ShaderModule>() != null;
                if (!hasVfx && !hasShader) { hasVfx = true; hasShader = true; }

                if (hasVfx)
                {
                    var typeName = $"VFX_{def.moduleName}";
                    _typeRegistry.Register(new NodeTypeInfo(typeName, $"VFX: {def.moduleName}", NodeCategory.VFX));
                    _graphState.RegisterNodeFactory(typeName, id => new VFXModuleNode(id, capturedDef));
                }
                if (hasShader)
                {
                    var typeName = $"Shader_{def.moduleName}";
                    _typeRegistry.Register(new NodeTypeInfo(typeName, $"Shader: {def.moduleName}", NodeCategory.Shader));
                    _graphState.RegisterNodeFactory(typeName, id => new ShaderModuleNode(id, capturedDef));
                }
            }
        }

        /// <summary>
        /// Object3DPrefabList から Object3D_ の動的 typeName とファクトリを登録、prefab 逆引きマップも populate。
        /// </summary>
        private void RegisterObject3DTypes()
        {
            if (_object3DPrefabs == null) return;

            foreach (var prefab in _object3DPrefabs.Prefabs)
            {
                if (prefab == null) continue;
                var prefabName = prefab.name;
                var typeName = $"Object3D_{prefabName}";
                var capturedName = prefabName;

                Object3DPrefabMap[prefabName] = prefab;
                _typeRegistry.Register(new NodeTypeInfo(typeName, $"3D: {prefabName}", NodeCategory.Scene));
                _graphState.RegisterNodeFactory(typeName, id => new Object3DNode(id, capturedName));
            }
        }
    }
}
