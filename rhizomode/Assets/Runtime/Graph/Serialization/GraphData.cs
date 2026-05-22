#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Serialization
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

        /// <summary>
        /// カメラ状態 (レンズ / ターゲット / パス / Motion ソース / ライブカメラ)。
        /// Phase 4 で append-only 追加。旧セーブには存在しないため JsonUtility が空の既定値を保持する。
        /// </summary>
        public CameraStateData cameraState = new();
    }
}
