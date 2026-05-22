#nullable enable

namespace Rhizomode.Graph.Serialization
{
    /// <summary>
    /// 単一カメラのシリアライズ用 DTO。GameObject 名をキーにシーン上のカメラへ復元する。
    /// </summary>
    [System.Serializable]
    public class CameraEntryData
    {
        /// <summary>カメラ GameObject 名 (復元キー)。</summary>
        public string name = "";

        /// <summary>視野角 (度)。</summary>
        public float fov = 60f;

        /// <summary>Dutch 角 (度)。</summary>
        public float dutch;

        /// <summary>LookAt ターゲットの DisplayName。空なら未設定。</summary>
        public string lookAtTarget = "";

        /// <summary>Follow ターゲットの DisplayName。空なら未設定。</summary>
        public string followTarget = "";

        /// <summary>Motion 駆動ソースのノード ID。空なら未接続。</summary>
        public string motionSourceNodeId = "";

        /// <summary>Motion 駆動ソースの出力ポート名。空なら未接続。</summary>
        public string motionSourcePort = "";

        /// <summary>ソース未接続カメラ用のフォールバック Drive 値 (0..1)。</summary>
        public float motionDrive;
    }
}
