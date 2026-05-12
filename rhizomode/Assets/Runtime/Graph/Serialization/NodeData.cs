#nullable enable

using UnityEngine;

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// ノードのシリアライズ用DTO。JsonUtility互換。
    /// </summary>
    [System.Serializable]
    public class NodeData
    {
        public string id = "";
        public string type = "";
        public float[] position = new float[3];
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
    }
}
