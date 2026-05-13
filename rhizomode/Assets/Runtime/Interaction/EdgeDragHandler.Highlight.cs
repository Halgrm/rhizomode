#nullable enable

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="EdgeDragHandler"/> の partial: スナップターゲット / ソースポートの
    /// ハイライト状態管理。
    /// Phase 9 Round D で本体から分離。
    /// </summary>
    public partial class EdgeDragHandler
    {
        private void UpdateSnapHighlight()
        {
            if (_sharedRaycast == null || _visualManager == null) return;

            if (!_sharedRaycast.HasHit)
            {
                // ソースポートのハイライトは残す
                ClearTargetHighlight();
                return;
            }

            var hit = _sharedRaycast.CurrentHit;
            var nodeVisual = _visualManager.GetVisualByCollider(hit.collider);
            if (nodeVisual?.Node == null || nodeVisual.Node.Id == _sourceNodeId)
            {
                ClearTargetHighlight();
                return;
            }

            // ターゲットノードの互換ポートをハイライト
            var (portName, _) = FindNearestCompatibleInputPort(nodeVisual, hit.point);
            if (portName == null)
            {
                ClearTargetHighlight();
                return;
            }

            // 前回と同じなら何もしない
            if (_highlightedNodeId == nodeVisual.Node.Id && _highlightedPortName == portName)
                return;

            ClearTargetHighlight();
            nodeVisual.SetPortHighlight(portName, true);
            _highlightedNodeId = nodeVisual.Node.Id;
            _highlightedPortName = portName;
        }

        private void ClearTargetHighlight()
        {
            // ソースポート以外のハイライトをクリア
            if (_highlightedNodeId != null && _highlightedPortName != null &&
                _highlightedNodeId != _sourceNodeId && _visualManager != null)
            {
                var visual = _visualManager.GetVisual(_highlightedNodeId);
                visual?.SetPortHighlight(_highlightedPortName, false);
            }

            // ソースのハイライトに戻す
            if (_sourceNodeId != null && _sourcePortName != null)
            {
                _highlightedNodeId = _sourceNodeId;
                _highlightedPortName = _sourcePortName;
            }
            else
            {
                _highlightedNodeId = null;
                _highlightedPortName = null;
            }
        }

        private void ClearHighlight()
        {
            if (_highlightedNodeId != null && _highlightedPortName != null && _visualManager != null)
            {
                var visual = _visualManager.GetVisual(_highlightedNodeId);
                visual?.SetPortHighlight(_highlightedPortName, false);
            }
            _highlightedNodeId = null;
            _highlightedPortName = null;
        }
    }
}
