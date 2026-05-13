#nullable enable

using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="EdgeDragHandler"/> の partial: 出力 / 入力ポートの最近傍探索。
    /// Phase 9 Round D で本体から分離。
    /// Round E (E3+E4) で INodeView 経由のポート列挙に変更し Graph.Model 依存撤廃。
    /// </summary>
    public partial class EdgeDragHandler
    {
        private (string? portName, ParamType type) FindNearestOutputPort(
            NodeVisualController nodeVisual, Vector3 hitPoint)
        {
            if (nodeVisual.Node == null) return (null, ParamType.Float);

            string? closestPort = null;
            var closestDist = float.MaxValue;
            var closestType = ParamType.Float;

            foreach (var port in nodeVisual.Node.OutputPorts)
            {
                var portPos = nodeVisual.GetPortWorldPosition(port.PortName);
                var dist = Vector3.Distance(portPos, hitPoint);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPort = port.PortName;
                    closestType = port.PortType;
                }
            }

            // ポートから遠すぎる場合はUI操作を優先（スライダー等）
            if (closestDist > portSelectThreshold)
                return (null, ParamType.Float);

            return (closestPort, closestType);
        }

        private (string? portName, ParamType type) FindNearestCompatibleInputPort(
            NodeVisualController nodeVisual, Vector3 hitPoint)
        {
            if (nodeVisual.Node == null) return (null, ParamType.Float);

            string? closestPort = null;
            var closestDist = float.MaxValue;
            var closestType = ParamType.Float;

            foreach (var port in nodeVisual.Node.InputPorts)
            {
                if (port.PortType != _sourceParamType) continue;

                var portPos = nodeVisual.GetPortWorldPosition(port.PortName);
                var dist = Vector3.Distance(portPos, hitPoint);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPort = port.PortName;
                    closestType = port.PortType;
                }
            }

            // ポートから遠すぎる場合は選択しない（FindNearestOutputPortと同じ閾値チェック）
            if (closestDist > portSelectThreshold)
                return (null, ParamType.Float);

            return (closestPort, closestType);
        }
    }
}
