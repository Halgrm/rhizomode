#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Core
{
    /// <summary>
    /// 全ノードの抽象基底クラス。ポート登録とSetup/Dispose lifecycle を提供する。
    /// </summary>
    public abstract class NodeBase : IDisposable
    {
        private readonly Dictionary<string, IInputPort> _inputPorts = new();
        private readonly Dictionary<string, IOutputPort> _outputPorts = new();
        private readonly List<IDisposable> _subscriptions = new();
        private List<PortDefinition>? _cachedPortDefinitions;

        public string Id { get; }
        public string NodeType { get; }
        public Vector3 Position { get; set; }

        public IReadOnlyDictionary<string, IInputPort> InputPorts => _inputPorts;
        public IReadOnlyDictionary<string, IOutputPort> OutputPorts => _outputPorts;

        protected NodeBase(string id, string nodeType)
        {
            Id = id;
            NodeType = nodeType;
        }

        /// <summary>
        /// 入力ポートを登録する。コンストラクタ内で呼ぶこと。
        /// </summary>
        protected InputPort<T> RegisterInput<T>(string name, ParamType type)
        {
            // ポート名重複時は既存を返す（上書きによるリーク防止）
            if (_inputPorts.TryGetValue(name, out var existing))
            {
                Debug.LogWarning($"[NodeBase] Duplicate input port '{name}' on {NodeType} — returning existing");
                return (InputPort<T>)existing;
            }
            var port = new InputPort<T>(type);
            _inputPorts[name] = port;
            return port;
        }

        /// <summary>
        /// 出力ポートを登録する。コンストラクタ内で呼ぶこと。
        /// </summary>
        protected OutputPort<T> RegisterOutput<T>(string name, ParamType type)
        {
            // ポート名重複時は既存を返す（上書きによるリーク防止）
            if (_outputPorts.TryGetValue(name, out var existing))
            {
                Debug.LogWarning($"[NodeBase] Duplicate output port '{name}' on {NodeType} — returning existing");
                return (OutputPort<T>)existing;
            }
            var port = new OutputPort<T>(type);
            _outputPorts[name] = port;
            return port;
        }

        /// <summary>
        /// Setup()内で作成したsubscriptionを登録する。Dispose時に自動解放される。
        /// </summary>
        protected void AddSubscription(IDisposable subscription)
        {
            _subscriptions.Add(subscription);
        }

        public IInputPort? GetInputPort(string name)
        {
            return _inputPorts.TryGetValue(name, out var port) ? port : null;
        }

        public IOutputPort? GetOutputPort(string name)
        {
            return _outputPorts.TryGetValue(name, out var port) ? port : null;
        }

        /// <summary>
        /// 登録済みポートからPortDefinitionリストを生成する。シリアライズ・UI表示用。
        /// 結果はキャッシュされる（ポートはコンストラクタ後に確定するため）。
        /// </summary>
        public IReadOnlyList<PortDefinition> GetPortDefinitions()
        {
            if (_cachedPortDefinitions != null) return _cachedPortDefinitions;

            _cachedPortDefinitions = new List<PortDefinition>();
            foreach (var kvp in _inputPorts)
                _cachedPortDefinitions.Add(new PortDefinition(kvp.Key, kvp.Value.Type, PortDirection.Input));
            foreach (var kvp in _outputPorts)
                _cachedPortDefinitions.Add(new PortDefinition(kvp.Key, kvp.Value.Type, PortDirection.Output));
            return _cachedPortDefinitions;
        }

        /// <summary>
        /// GraphContextから呼ばれる。R3 Observableチェーンを構築する。
        /// </summary>
        public abstract void Setup(GraphContext context);

        /// <summary>
        /// ノード削除時に呼ばれる。全subscription・全ポートを解放する。
        /// </summary>
        public virtual void Dispose()
        {
            foreach (var sub in _subscriptions)
                sub.Dispose();
            _subscriptions.Clear();

            foreach (var port in _inputPorts.Values)
                if (port is IDisposable d) d.Dispose();
            foreach (var port in _outputPorts.Values)
                if (port is IDisposable d) d.Dispose();
        }

        /// <summary>
        /// JSON文字列からノード固有パラメータを復元する。
        /// サブクラスでオーバーライドして独自パラメータを復元すること。
        /// </summary>
        /// <param name="paramsJson">NodeData.paramsJsonに保存されたJSON文字列</param>
        public virtual void RestoreParamsFromJson(string paramsJson)
        {
            // 基底は何もしない。サブクラスで必要に応じてオーバーライド。
        }

        /// <summary>
        /// ノード固有パラメータのシリアライズ。サブクラスでオーバーライドする。
        /// </summary>
        public virtual NodeData ToNodeData()
        {
            return new NodeData
            {
                id = Id,
                type = NodeType,
                position = new[] { Position.x, Position.y, Position.z },
                paramsJson = "{}",
                groupId = ""
            };
        }
    }
}
