#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// パスカメラのスプライン節点のシリアライズ用 DTO。
    /// </summary>
    [System.Serializable]
    public class CameraPathData
    {
        /// <summary>パスを持つカメラの GameObject 名 (復元キー)。</summary>
        public string cameraName = "";

        /// <summary>スプライン節点 (ローカル空間)。</summary>
        public List<KnotData> knots = new();
    }
}
