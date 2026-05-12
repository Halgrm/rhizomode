#nullable enable

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

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
        public ParamType PortType { get; }
        public bool IsHighlighted { get; set; }

        /// <summary>エッジ個別のマテリアルプロパティ。色・グロー強度をインスタンスごとに設定。</summary>
        public MaterialPropertyBlock? Mpb { get; set; }

        public EdgeVisual(
            string edgeId,
            GameObject go,
            LineRenderer line,
            string fromNodeId,
            string fromPort,
            string toNodeId,
            string toPort,
            ParamType portType)
        {
            EdgeId = edgeId;
            Go = go;
            Line = line;
            FromNodeId = fromNodeId;
            FromPort = fromPort;
            ToNodeId = toNodeId;
            ToPort = toPort;
            PortType = portType;
        }
    }
}
