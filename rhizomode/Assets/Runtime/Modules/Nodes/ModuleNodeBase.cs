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
        /// Codex re-review fix (PARTIAL 1): Activate() が throw した場合に <see cref="_module"/> へ
        /// destroyed instance の参照が残らないよう、Deactivate→clear→Activate→assign の順に変更。
        /// 例外伝播後に observable から SetParam が飛んでも null check で safely skip される。
        /// </remarks>
        public IPerformanceModule? Module
        {
            get => _module;
            set
            {
                _module?.Deactivate();
                _module = null;
                if (value == null) return;
                value.Activate(); // throw 時は呼び出し側で rollback / destroy を行う
                _module = value;
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
