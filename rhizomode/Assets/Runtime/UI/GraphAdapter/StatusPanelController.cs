#nullable enable

using System.Collections.Generic;
using System.Text;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Observability.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// ステータスパネルの表示を制御する。グラフ統計・BPM・FPS・オーディオデバイスを
    /// 定期更新でUIToolkitラベルに反映する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class StatusPanelController : MonoBehaviour
    {
        private const float UpdateInterval = 0.5f;
        private const int FpsSampleCount = 30;

        private GraphContextBehaviour? _graphContext;
        private float _bpm;
        private string _audioDeviceName = "—";

        private Label? _nodeCountLabel;
        private Label? _edgeCountLabel;
        private Label? _activeModulesLabel;
        private Label? _bpmLabel;
        private Label? _fpsLabel;
        private Label? _audioDeviceLabel;
        private Label? _systemHealthLabel;

        // 各 system の health 状態。GameBootstrap が HealthAggregator.OnHealthChange を
        // 購読して SetHealth で更新する。表示は UpdateLabels (0.5s 間隔) で反映。
        private readonly Dictionary<string, HealthStatus> _systemHealth = new();

        private float _updateTimer;
        private readonly float[] _fpsSamples = new float[FpsSampleCount];
        private int _fpsSampleIndex;

        private bool _labelsCached;

        /// <summary>
        /// 依存を設定しパネルを使用可能にする。
        /// </summary>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            _graphContext = graphContext;
        }

        /// <summary>
        /// BPM表示値を外部から設定する。
        /// </summary>
        public void SetBPM(float bpm)
        {
            _bpm = bpm;
        }

        /// <summary>
        /// オーディオデバイス名を外部から設定する。
        /// </summary>
        public void SetAudioDevice(string deviceName)
        {
            _audioDeviceName = deviceName;
        }

        /// <summary>
        /// system health の状態変化を反映する。GameBootstrap が
        /// <c>HealthAggregator.OnHealthChange</c> を購読して呼ぶ。
        /// 実際のラベル更新は次の <see cref="UpdateLabels"/> tick (0.5s 間隔) で行う。
        /// </summary>
        public void SetHealth(HealthSnapshot snapshot)
        {
            _systemHealth[snapshot.SystemId] = snapshot.Status;
        }

        private void Update()
        {
            // ラベル参照の遅延キャッシュ（UIToolkit レイアウト完了後）
            if (!_labelsCached)
            {
                CacheLabels();
                if (_labelsCached) return; // 初回はキャッシュのみ
            }

            SampleFps();

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < UpdateInterval) return;
            _updateTimer = 0f;

            UpdateLabels();
        }

        private void CacheLabels()
        {
            var host = GetComponent<WorldPanelHost>();
            var root = host.Root;
            if (root == null) return;

            _nodeCountLabel = root.Q<Label>("node-count");
            _edgeCountLabel = root.Q<Label>("edge-count");
            _activeModulesLabel = root.Q<Label>("active-modules");
            _bpmLabel = root.Q<Label>("bpm-display");
            _fpsLabel = root.Q<Label>("fps-display");
            _audioDeviceLabel = root.Q<Label>("audio-device");
            _systemHealthLabel = root.Q<Label>("system-health");
            _labelsCached = true;
        }

        private void SampleFps()
        {
            // deltaTimeが0の場合（エディタ一時停止直後など）はスキップ
            if (Time.unscaledDeltaTime <= 0f) return;

            _fpsSamples[_fpsSampleIndex] = 1f / Time.unscaledDeltaTime;
            _fpsSampleIndex = (_fpsSampleIndex + 1) % FpsSampleCount;
        }

        private float CalculateAverageFps()
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < FpsSampleCount; i++)
            {
                if (_fpsSamples[i] > 0f)
                {
                    sum += _fpsSamples[i];
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        private void UpdateLabels()
        {
            if (_graphContext == null) return;

            var ctx = _graphContext.Context;

            UpdateNodeCount(ctx);
            UpdateEdgeCount(ctx);
            UpdateActiveModules(ctx);
            UpdateBpm();
            UpdateFps();
            UpdateAudioDevice();
            UpdateSystemHealth();
        }

        private void UpdateNodeCount(GraphState ctx)
        {
            if (_nodeCountLabel != null)
                _nodeCountLabel.text = $"Nodes: {ctx.Nodes.Count}";
        }

        private void UpdateEdgeCount(GraphState ctx)
        {
            if (_edgeCountLabel != null)
                _edgeCountLabel.text = $"Edges: {ctx.Edges.Count}";
        }

        private readonly StringBuilder _moduleSb = new();

        private void UpdateActiveModules(GraphState ctx)
        {
            if (_activeModulesLabel == null) return;

            // LINQ不使用（GC alloc回避）
            _moduleSb.Clear();
            foreach (var node in ctx.Nodes.Values)
            {
                if (!node.NodeType.EndsWith("Module")) continue;
                if (_moduleSb.Length > 0) _moduleSb.Append(", ");
                _moduleSb.Append(node.NodeType, 0, node.NodeType.Length - 6); // "Module" = 6 chars
            }

            _activeModulesLabel.text = _moduleSb.Length > 0
                ? $"Modules: {_moduleSb}"
                : "Modules: —";
        }

        private void UpdateBpm()
        {
            if (_bpmLabel != null)
            {
                _bpmLabel.text = _bpm > 0f
                    ? $"BPM: {_bpm:F0}"
                    : "BPM: —";
            }
        }

        private void UpdateFps()
        {
            if (_fpsLabel != null)
            {
                float fps = CalculateAverageFps();
                _fpsLabel.text = $"FPS: {fps:F0}";
            }
        }

        private void UpdateAudioDevice()
        {
            if (_audioDeviceLabel != null)
                _audioDeviceLabel.text = $"Audio: {_audioDeviceName}";
        }

        private readonly StringBuilder _healthSb = new();

        private void UpdateSystemHealth()
        {
            if (_systemHealthLabel == null) return;

            if (_systemHealth.Count == 0)
            {
                _systemHealthLabel.text = "Health: —";
                return;
            }

            // LINQ 不使用 (GC alloc 回避)。monitor 登録順 = 表示順 (Dictionary は削除しない限り
            // 挿入順を保持、HealthAggregator への Register は startup 時 1 回のみ)。
            _healthSb.Clear();
            _healthSb.Append("Health: ");
            foreach (var kvp in _systemHealth)
            {
                _healthSb.Append(kvp.Key);
                _healthSb.Append(HealthGlyph(kvp.Value));
                _healthSb.Append(' ');
            }
            _systemHealthLabel.text = _healthSb.ToString();
        }

        // Healthy=✓ / Unknown=– (未起動、正常) / Degraded=! / Failed=✗
        private static string HealthGlyph(HealthStatus status) => status switch
        {
            HealthStatus.Healthy => "✓",
            HealthStatus.Degraded => "!",
            HealthStatus.Failed => "✗",
            _ => "–",
        };
    }
}
