#nullable enable

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// スプライン節点 (BezierKnot) のシリアライズ用 DTO。位置・タンジェントはローカル空間。
    /// </summary>
    [System.Serializable]
    public class KnotData
    {
        /// <summary>節点位置 (x, y, z)。</summary>
        public float[] position = new float[3];

        /// <summary>入力タンジェント (x, y, z)。</summary>
        public float[] tangentIn = new float[3];

        /// <summary>出力タンジェント (x, y, z)。</summary>
        public float[] tangentOut = new float[3];

        /// <summary>節点回転 (quaternion x, y, z, w)。</summary>
        public float[] rotation = { 0f, 0f, 0f, 1f };
    }
}
