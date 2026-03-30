#nullable enable

namespace Rhizomode.Core
{
    /// <summary>
    /// エッジのシリアライズ用DTO。JsonUtility互換。
    /// フィールド名はTECHNICAL_DESIGN.md 9.1のJSON仕様に準拠。
    /// </summary>
    [System.Serializable]
    public class EdgeData
    {
        public string id = "";
        public string from = "";
        public string fromPort = "";
        public string to = "";
        public string toPort = "";
    }
}
