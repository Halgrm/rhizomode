#nullable enable

using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// ノードパネルの距離ベースLOD制御。3段階:
    /// 近距離: 高解像度(384px) + UI更新ON
    /// 中距離: 低解像度(192px) + UI更新ON
    /// 遠距離: UI更新OFF（最後のフレームがQuad上に残る）
    /// VR 90fps維持に必須。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class NodePanelLOD : MonoBehaviour
    {
        [Header("LOD更新")]
        [SerializeField, Range(0.1f, 1f), Tooltip("LOD判定の更新間隔（秒）")]
        private float updateInterval = 0.3f;

        [Header("LOD距離閾値")]
        [SerializeField, Range(0.5f, 5f), Tooltip("高解像度の最大距離（メートル）")]
        private float highResDistance = 1.5f;

        [SerializeField, Range(1f, 10f), Tooltip("低解像度の最大距離（メートル）")]
        private float lowResDistance = 3.0f;

        [SerializeField, Range(2f, 15f), Tooltip("UI無効化の距離（メートル）")]
        private float disableDistance = 4.0f;

        [SerializeField, Range(0.1f, 1f), Tooltip("LOD遷移のヒステリシス幅（メートル）")]
        private float hysteresis = 0.4f;

        [Header("テクスチャ解像度")]
        [SerializeField, Range(128, 1024), Tooltip("高解像度のテクスチャ幅")]
        private int highResWidth = 384;

        [SerializeField, Range(64, 512), Tooltip("低解像度のテクスチャ幅")]
        private int lowResWidth = 192;

        [SerializeField] private Transform? vrHeadTransform;

        private NodeVisualManager? _visualManager;
        private float _timer;

        /// <summary>
        /// LODシステムを初期化する。GameBootstrapから呼び出される。
        /// </summary>
        public void Initialize(NodeVisualManager visualManager, Transform headTransform)
        {
            _visualManager = visualManager;
            vrHeadTransform = headTransform;
        }

        private void Update()
        {
            if (_visualManager == null || vrHeadTransform == null) return;

            _timer += Time.deltaTime;
            if (_timer < updateInterval) return;
            _timer = 0f;

            UpdateLOD();
        }

        private void UpdateLOD()
        {
            var headPos = vrHeadTransform!.position;

            foreach (var kvp in _visualManager!.Visuals)
            {
                var controller = kvp.Value;
                if (controller == null) continue;

                var panelHost = controller.GetComponent<WorldPanelHost>();
                if (panelHost == null || !panelHost.IsInitialized) continue;

                var dist = Vector3.Distance(controller.transform.position, headPos);
                ApplyLOD(panelHost, dist);
            }
        }

        private void ApplyLOD(WorldPanelHost panelHost, float distance)
        {
            if (distance < highResDistance)
            {
                // 近距離: 高解像度 + UIオン
                if (!panelHost.IsUIActive) panelHost.SetUIActive(true);
                panelHost.ChangeResolution(highResWidth);
            }
            else if (distance < lowResDistance)
            {
                // 中距離: 低解像度 + UIオン
                if (!panelHost.IsUIActive) panelHost.SetUIActive(true);
                panelHost.ChangeResolution(lowResWidth);
            }
            else if (distance > disableDistance)
            {
                // 遠距離: UIオフ
                if (panelHost.IsUIActive) panelHost.SetUIActive(false);
            }
            else if (distance < disableDistance - hysteresis && !panelHost.IsUIActive)
            {
                // ヒステリシス復帰
                panelHost.SetUIActive(true);
                panelHost.ChangeResolution(lowResWidth);
            }
        }
    }
}
