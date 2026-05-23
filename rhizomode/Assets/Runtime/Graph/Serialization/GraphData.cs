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

        /// <summary>
        /// Cue 保存時に active だった Unity Scene の名前。空文字なら "scene 情報なし" (旧形式)。
        /// </summary>
        /// <remarks>
        /// 2026-05-23 追加 (append-only): cue 呼び出し時にビジュアル環境 (Forest / Nature / Ruins 等) も
        /// 同時に切り替えるため、active scene 名を記録する。Load 側は本値と
        /// <c>SceneManager.GetActiveScene().name</c> が一致しない場合、Pending スロット経由で
        /// 新シーン bootstrap 後に LoadGraph を再発火する。
        /// </remarks>
        public string sceneName = "";
    }
}
