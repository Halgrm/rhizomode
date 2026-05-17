#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.Modules;
using UnityEngine;

namespace Rhizomode.Nodes.Modules
{
    /// <summary>
    /// モジュールノードの共通基底クラス。ModuleDefinitionからポートを動的生成し、
    /// IPerformanceModuleへパラメータ値をリアクティブに転送する。
    /// </summary>
    public abstract class ModuleNodeBase : NodeBase
    {
        private readonly ModuleDefinition _definition;
        private IPerformanceModule? _module;

        /// <summary>
        /// 実行時に外部から注入される演出モジュールインスタンス。
        /// Activate() はセッター内で自動呼出しされる。
        /// </summary>
        /// <remarks>
        /// 注: 旧 (Codex re-review fix 試行版) の「Deactivate→null clear→Activate→assign」順は、
        /// VFX/Shader/InstancedCubes 全般で param subscription が起動直後に届かない regression を
        /// 招いたため revert。並行する Setup() の R3 lambda が `_module?.SetParam` で読む field が
        /// Activate 完了まで null のままだと、PrimeInitialEmission / 直後の ConnectPortsCommand emission を
        /// 取りこぼす (映像が真っ黒 / 反応しない)。
        ///
        /// Codex PARTIAL 1 (Activate throw 時の broken instance 参照) は、built-in 3 種が
        /// Activate 内 try-catch を持ち throw しないため実害無し。propagation の必要性が出たら
        /// ModuleLifecycleProcessor 側で setter の前に <c>module.Activate()</c> を明示呼ぶ形に
        /// 切り替える方が安全 (setter は assign のみに留める)。
        /// </remarks>
        public IPerformanceModule? Module
        {
            get => _module;
            set
            {
                // 旧モジュールがあれば停止してから差し替え
                _module?.Deactivate();
                _module = value;
                _module?.Activate();
            }
        }

        /// <summary>このノードが参照するModuleDefinition。</summary>
        public ModuleDefinition Definition => _definition;

        /// <param name="id">ノードID</param>
        /// <param name="nodeType">ノードタイプ文字列（"VFXModule" / "ShaderModule"）</param>
        /// <param name="definition">パラメータ構成を定義するScriptableObject</param>
        protected ModuleNodeBase(string id, string nodeType, ModuleDefinition definition)
            : base(id, nodeType)
        {
            _definition = definition;
            RegisterPortsFromDefinition();
        }

        /// <summary>
        /// ModuleDefinition.parametersとeventsから入力ポートを動的に登録する。
        /// </summary>
        private void RegisterPortsFromDefinition()
        {
            foreach (var param in _definition.parameters)
            {
                switch (param.type)
                {
                    case ParamType.Float:
                        RegisterInput<float>(param.name, ParamType.Float);
                        break;
                    case ParamType.Color:
                        RegisterInput<Color>(param.name, ParamType.Color);
                        break;
                    case ParamType.Bool:
                        RegisterInput<bool>(param.name, ParamType.Bool);
                        break;
                    default:
                        Debug.LogWarning(
                            $"[ModuleNodeBase] Unknown ParamType: {param.type} for param '{param.name}'");
                        break;
                }
            }

            // イベントはBool入力ポートとして登録（true時にSendEvent発火）
            foreach (var eventName in _definition.events)
            {
                RegisterInput<bool>(eventName, ParamType.Bool);
            }
        }

        /// <inheritdoc />
        public override bool IsInputPortEvent(string portName) => _definition.IsEvent(portName);

        /// <inheritdoc />
        public override void Setup(GraphState context)
        {
            foreach (var param in _definition.parameters)
            {
                SubscribeParam(context, param);
            }

            SubscribeEvents(context);
        }

        /// <summary>
        /// events配列のBool入力を購読し、true時にモジュールへイベント転送する。
        /// </summary>
        private void SubscribeEvents(GraphState context)
        {
            foreach (var eventName in _definition.events)
            {
                var name = eventName;
                AddSubscription(
                    context.GetInputObservable<bool>(this, name)
                        .Subscribe(v =>
                        {
                            if (v) _module?.SetParam(name, true);
                        }));
            }
        }

        /// <summary>
        /// 各パラメータの入力ObservableをSubscribeし、モジュールへ値を転送する。
        /// </summary>
        private void SubscribeParam(GraphState context, ParamDefinition param)
        {
            // ローカル変数にキャプチャし、ラムダ内でparamを参照しない
            var paramName = param.name;

            switch (param.type)
            {
                case ParamType.Float:
                    AddSubscription(
                        context.GetInputObservable<float>(this, paramName)
                            .Subscribe(v => _module?.SetParam(paramName, v)));
                    break;
                case ParamType.Color:
                    AddSubscription(
                        context.GetInputObservable<Color>(this, paramName)
                            .Subscribe(v => _module?.SetParam(paramName, v)));
                    break;
                case ParamType.Bool:
                    AddSubscription(
                        context.GetInputObservable<bool>(this, paramName)
                            .Subscribe(v => _module?.SetParam(paramName, v)));
                    break;
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();

            try
            {
                _module?.Deactivate();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ModuleNodeBase] Module deactivate failed: {Id} — {e.Message}");
            }

            _module = null;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            // ModuleNodeBaseのparamsJsonにはmoduleNameが保存されるが、
            // ノード生成時にファクトリ経由でModuleDefinitionが既に注入されているため、
            // パラメータ値の復元は不要 (R3 経由で ConstFloat ノードから再注入される)。
            // ただし L5 fix: saved graph の moduleName と注入された ModuleDefinition.moduleName が不一致だと、
            // 旧 SO を消した状態で別 module が無言で復活する事故が起き得るため、warning を残す。
            if (string.IsNullOrEmpty(paramsJson)) return;

            try
            {
                var saved = JsonUtility.FromJson<ModuleNodeParams>(paramsJson);
                if (!string.IsNullOrEmpty(saved.moduleName) && saved.moduleName != _definition.moduleName)
                {
                    Debug.LogWarning(
                        $"[ModuleNodeBase] moduleName mismatch on node {Id}: saved='{saved.moduleName}' " +
                        $"resolved='{_definition.moduleName}'. Saved graph may have referenced a deleted/renamed ModuleDefinition.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[ModuleNodeBase] paramsJson parse failed on node {Id}: {e.Message}");
            }
        }

        /// <inheritdoc />
        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(
                new ModuleNodeParams { moduleName = _definition.moduleName });
            return data;
        }

        [Serializable]
        private struct ModuleNodeParams
        {
            public string moduleName;
        }
    }
}
