#nullable enable

using UnityEngine;

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// ノードのシリアライズ用DTO。JsonUtility互換。
    /// </summary>
    /// <remarks>
    /// <c>rotation</c> は visual transform の rotation を保持する append-only field (cue 復元時の表裏破綻 fix)。
    /// 旧形式 (rotation 欠落 / Length != 4 / 全成分 0) は <see cref="HasRotation"/> が false を返し、
    /// caller (GraphLoadCoordinator) が LookRotation fallback に流す。
    /// </remarks>
    [System.Serializable]
    public class NodeData
    {
        public string id = "";
        public string type = "";
        public float[] position = new float[3];
        public float[] rotation = new float[4];
        public string paramsJson = "{}";
        public string groupId = "";

        public Vector3 ToVector3()
        {
            return new Vector3(
                position.Length > 0 ? position[0] : 0f,
                position.Length > 1 ? position[1] : 0f,
                position.Length > 2 ? position[2] : 0f
            );
        }

        public static float[] FromVector3(Vector3 v)
        {
            return new[] { v.x, v.y, v.z };
        }

        /// <summary>rotation が有効に保存されているか (旧形式・未初期化セーブと区別する)。</summary>
        public bool HasRotation =>
            rotation != null && rotation.Length == 4 &&
            (rotation[0] != 0f || rotation[1] != 0f || rotation[2] != 0f || rotation[3] != 0f);

        public Quaternion ToQuaternion()
        {
            if (!HasRotation) return Quaternion.identity;
            return new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        }

        public static float[] FromQuaternion(Quaternion q)
        {
            return new[] { q.x, q.y, q.z, q.w };
        }
    }
}
