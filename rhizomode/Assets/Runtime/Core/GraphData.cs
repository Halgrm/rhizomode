#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Core
{
    /// <summary>
    /// グラフ全体のシリアライズ用DTO。セーブ/ロードのトップレベル構造。
    /// </summary>
    [System.Serializable]
    public class GraphData
    {
        public string version = "1.0";
        public List<NodeData> nodes = new();
        public List<EdgeData> edges = new();
    }
}
