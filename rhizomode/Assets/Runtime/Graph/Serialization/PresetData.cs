#nullable enable

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// プリセット（部分グラフテンプレート）のシリアライズ用DTO。
    /// グラフデータにメタデータ（名前）を付加する。
    /// </summary>
    [System.Serializable]
    public class PresetData
    {
        /// <summary>プリセット名（ファイル名およびメニュー表示用）。</summary>
        public string presetName = "";

        /// <summary>プリセットのグラフデータ（ノード群とエッジ群）。</summary>
        public GraphData graphData = new();
    }
}
