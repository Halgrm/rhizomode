#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="GraphState"/> から <see cref="IFloatOutputCatalog"/> を提供する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 9 Round E (E5): UI.Presentation 配下が GraphState を触らず Float 出力を
    /// 列挙 / 購読できるようにするための bridge。
    /// </remarks>
    public sealed class GraphStateFloatOutputCatalog : IFloatOutputCatalog
    {
        private readonly Func<GraphState> _stateProvider;

        public GraphStateFloatOutputCatalog(Func<GraphState> stateProvider)
        {
            _stateProvider = stateProvider;
        }

        public IReadOnlyList<FloatOutputRef> GetFloatOutputs()
        {
            var state = _stateProvider();
            var list = new List<FloatOutputRef>();
            foreach (var node in state.Nodes.Values)
            {
                foreach (var kv in node.OutputPorts)
                {
                    if (kv.Value.Type != ParamType.Float) continue;
                    var display = $"{node.NodeType} · {kv.Key}";
                    list.Add(new FloatOutputRef(node.Id, kv.Key, display));
                }
            }
            return list;
        }

        public IDisposable? Subscribe(string nodeId, string portName, Action<float> callback)
        {
            var state = _stateProvider();
            if (!state.Nodes.TryGetValue(nodeId, out var node)) return null;
            var port = node.GetOutputPort(portName);
            if (port is not OutputPort<float> floatPort) return null;
            return floatPort.Observable.Subscribe(callback);
        }
    }
}
