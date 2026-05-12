#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// Ableton OSC接続設定UIを提供するパネル。起動時に1回表示し、
    /// host/portを編集→Connectで接続要求イベントを発火する。
    /// 拡張性: Section/Row階層なので、新しい設定項目はUXML/USSのみで追加可能。
    /// 将来動的にSectionを追加する場合は RegisterSection() を使う。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class AbletonSetupPanel : MonoBehaviour
    {
        private const float PanelSpawnDistance = 0.6f;
        private const float PanelWorldWidth = 0.32f;
        private const float PanelWorldHeight = 0.36f;
        private const int PanelTextureWidth = 480;
        private const int PanelTextureHeight = 540;

        [SerializeField] private VisualTreeAsset? panelUxml;
        [SerializeField] private StyleSheet? panelStyleSheet;

        private WorldPanelHost? _panelHost;

        private TextField? _hostField;
        private TextField? _sendPortField;
        private TextField? _recvPortField;
        private Label? _statusLabel;
        private Button? _connectBtn;
        private Button? _skipBtn;
        private VisualElement? _sectionsRoot;

        /// <summary>Connect押下で発火。引数: (host, sendPort, recvPort)</summary>
        public event Action<string, int, int>? OnConnectRequested;

        /// <summary>Skip押下で発火。</summary>
        public event Action? OnSkipRequested;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
            SetVisualActive(false);
        }

        /// <summary>
        /// 表示前に初期値を設定する。GameBootstrapからPlayerPrefs値を渡す想定。
        /// </summary>
        public void SetInitialValues(string host, int sendPort, int recvPort)
        {
            EnsureInitialized();
            if (_hostField != null) _hostField.value = host;
            if (_sendPortField != null) _sendPortField.value = sendPort.ToString();
            if (_recvPortField != null) _recvPortField.value = recvPort.ToString();
        }

        /// <summary>
        /// 状態メッセージを更新する。色は OK / Warn / Error から自動分類。
        /// </summary>
        public void SetStatus(string text, Color color)
        {
            EnsureInitialized();
            if (_statusLabel == null) return;

            _statusLabel.text = text;

            _statusLabel.RemoveFromClassList("abl-panel__status--ok");
            _statusLabel.RemoveFromClassList("abl-panel__status--warn");
            _statusLabel.RemoveFromClassList("abl-panel__status--error");

            if (color.r > 0.8f && color.g < 0.5f) _statusLabel.AddToClassList("abl-panel__status--error");
            else if (color.r > 0.7f && color.g > 0.7f) _statusLabel.AddToClassList("abl-panel__status--warn");
            else _statusLabel.AddToClassList("abl-panel__status--ok");
        }

        /// <summary>
        /// 拡張用: 動的にSection要素をsections-rootに追加する。
        /// 現在は未使用だがRefresh/Layout設定UI追加時に使う。
        /// </summary>
        public void RegisterSection(VisualElement section)
        {
            EnsureInitialized();
            _sectionsRoot?.Add(section);
        }

        /// <summary>
        /// パネルを表示する。プレイヤー前方に配置し、プレイヤー方向を向ける。
        /// </summary>
        public void Show(Vector3 headPosition, Vector3 headForward)
        {
            EnsureInitialized();

            var spawnPos = headPosition + headForward * PanelSpawnDistance;
            transform.position = spawnPos;
            transform.rotation = Quaternion.LookRotation(transform.position - headPosition);

            SetVisualActive(true);
            IsVisible = true;
        }

        /// <summary>
        /// 指定座標・回転でパネルを表示する。Editorで配置したアンカーから呼ぶ用途。
        /// </summary>
        public void ShowAt(Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            transform.SetPositionAndRotation(position, rotation);
            SetVisualActive(true);
            IsVisible = true;
        }

        public void Hide()
        {
            if (!IsVisible) return;
            SetVisualActive(false);
            IsVisible = false;
        }

        private void EnsureInitialized()
        {
            if (_panelHost == null) _panelHost = GetComponent<WorldPanelHost>();
            if (_panelHost == null || panelUxml == null) return;

            if (!_panelHost.IsInitialized)
            {
                _panelHost.Initialize(panelUxml, panelStyleSheet, PanelTextureWidth, PanelTextureHeight);
                _panelHost.Resize(PanelWorldWidth, PanelWorldHeight);
                CacheElements();
            }
        }

        private void CacheElements()
        {
            var root = _panelHost?.Root;
            if (root == null) return;

            _hostField = root.Q<TextField>("host-field");
            _sendPortField = root.Q<TextField>("send-port-field");
            _recvPortField = root.Q<TextField>("recv-port-field");
            _statusLabel = root.Q<Label>("status-label");
            _connectBtn = root.Q<Button>("connect-btn");
            _skipBtn = root.Q<Button>("skip-btn");
            _sectionsRoot = root.Q("sections-root");

            if (_connectBtn != null)
                _connectBtn.RegisterCallback<ClickEvent>(_ => HandleConnect());

            if (_skipBtn != null)
                _skipBtn.RegisterCallback<ClickEvent>(_ => HandleSkip());
        }

        private void HandleConnect()
        {
            try
            {
                var host = _hostField?.value ?? "127.0.0.1";
                var sendPort = ParseIntOrDefault(_sendPortField?.value, 11000);
                var recvPort = ParseIntOrDefault(_recvPortField?.value, 11001);
                OnConnectRequested?.Invoke(host, sendPort, recvPort);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbletonSetupPanel] Connect handler failed: {e.Message}");
            }
        }

        private void HandleSkip()
        {
            try
            {
                OnSkipRequested?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbletonSetupPanel] Skip handler failed: {e.Message}");
            }
        }

        private static int ParseIntOrDefault(string? s, int fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return int.TryParse(s, out var v) ? v : fallback;
        }

        private void SetVisualActive(bool active)
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = active;

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = active;
        }
    }
}
