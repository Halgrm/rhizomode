#nullable enable

using System;

namespace Rhizomode.Core
{
    /// <summary>
    /// ノード間のエッジ。Subscriptionを保持し、Disposeで接続を切断する。
    /// </summary>
    public class Edge
    {
        public string Id { get; }
        public string FromNodeId { get; }
        public string FromPort { get; }
        public string ToNodeId { get; }
        public string ToPort { get; }
        public IDisposable? Subscription { get; internal set; }

        public Edge(string id, string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            Id = id;
            FromNodeId = fromNodeId;
            FromPort = fromPort;
            ToNodeId = toNodeId;
            ToPort = toPort;
        }
    }
}
