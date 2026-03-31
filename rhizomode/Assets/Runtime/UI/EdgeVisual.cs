#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 1本のエッジの視覚表現データ。EdgeVisualManagerが管理する。
    /// </summary>
    public class EdgeVisual
    {
        public string EdgeId { get; }
        public GameObject Go { get; }
        public LineRenderer Line { get; }
        public string FromNodeId { get; }
        public string FromPort { get; }
        public string ToNodeId { get; }
        public string ToPort { get; }
        public bool IsHighlighted { get; set; }

        public EdgeVisual(
            string edgeId,
            GameObject go,
            LineRenderer line,
            string fromNodeId,
            string fromPort,
            string toNodeId,
            string toPort)
        {
            EdgeId = edgeId;
            Go = go;
            Line = line;
            FromNodeId = fromNodeId;
            FromPort = fromPort;
            ToNodeId = toNodeId;
            ToPort = toPort;
        }
    }
}
