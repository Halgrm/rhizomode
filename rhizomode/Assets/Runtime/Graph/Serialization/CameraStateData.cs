#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// カメラ状態のシリアライズ用 DTO。GraphData に append され、キュー (セーブファイル) ごとに
    /// カメラ構成を保持する。
    /// </summary>
    [System.Serializable]
    public class CameraStateData
    {
        /// <summary>将来のスキーマ移行用バージョン。</summary>
        public int schemaVersion = 1;

        /// <summary>ロード時にライブ (高 Priority) にするカメラの GameObject 名。空ならライブ復元なし。</summary>
        public string liveCameraName = "";

        /// <summary>各カメラのレンズ / ターゲット / Motion ソース。</summary>
        public List<CameraEntryData> cameras = new();

        /// <summary>パスカメラのスプライン節点。</summary>
        public List<CameraPathData> paths = new();
    }
}
