#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// オーディオデバイス選択UIを管理する。UIToolkitのVisualElement階層を生成し、
    /// デバイス選択イベントを発火する。Audio層への直接依存を避け、
    /// GameBootstrap経由でAudioAnalyzerと接続する設計。
    /// </summary>
    public class AudioDeviceSelector : MonoBehaviour
    {
        private const string RootClassName = "audio-device-selector";
        private const string HeaderClassName = "audio-device-header";
        private const string DeviceButtonClassName = "audio-device-button";
        private const string DeviceButtonSelectedClassName = "audio-device-button--selected";
        private const string NoDeviceClassName = "audio-device-no-device";
        private const string RefreshButtonClassName = "audio-device-refresh";
        private const string HeaderText = "Audio Device";
        private const string NoDeviceText = "No audio devices found";
        private const string RefreshText = "↻ Refresh";

        private string[] _availableDevices = Array.Empty<string>();
        private string? _currentDevice;
        private VisualElement? _root;
        private VisualElement? _deviceListContainer;
        private readonly List<Button> _deviceButtons = new();

        /// <summary>デバイスが選択された時に発火する。引数はデバイス名。</summary>
        public event Action<string>? OnDeviceSelected;

        /// <summary>デバイスリストの更新が要求された時に発火する。</summary>
        public event Action? OnRefreshRequested;

        /// <summary>現在選択中のデバイス名。未選択なら null。</summary>
        public string? CurrentDevice => _currentDevice;

        /// <summary>
        /// 利用可能デバイスと現在のデバイスで初期化する。
        /// GameBootstrapからAudioAnalyzer.AvailableDevicesを渡す想定。
        /// </summary>
        /// <param name="availableDevices">利用可能なデバイス名の配列。</param>
        /// <param name="currentDevice">現在選択中のデバイス名。未選択なら null。</param>
        public void Initialize(string[] availableDevices, string? currentDevice)
        {
            _availableDevices = availableDevices ?? Array.Empty<string>();
            _currentDevice = currentDevice;

            if (_root != null)
            {
                RebuildDeviceList();
            }
        }

        /// <summary>
        /// デバイスリストを更新する。デバイス追加・削除時にGameBootstrapから呼ぶ。
        /// </summary>
        /// <param name="availableDevices">最新のデバイス一覧。</param>
        public void UpdateDeviceList(string[] availableDevices)
        {
            _availableDevices = availableDevices ?? Array.Empty<string>();
            RebuildDeviceList();
        }

        /// <summary>
        /// メニューに埋め込み可能なVisualElement階層を構築して返す。
        /// NodeCreationMenuControllerのサブパネルとして使用する想定。
        /// </summary>
        /// <returns>デバイス選択UIのルートVisualElement。</returns>
        public VisualElement BuildUI()
        {
            _root = new VisualElement();
            _root.AddToClassList(RootClassName);

            // ヘッダー
            var header = new Label(HeaderText);
            header.AddToClassList(HeaderClassName);
            _root.Add(header);

            // デバイスリストのコンテナ
            _deviceListContainer = new VisualElement();
            _root.Add(_deviceListContainer);

            // リフレッシュボタン
            var refreshButton = new Button(HandleRefreshClicked)
            {
                text = RefreshText
            };
            refreshButton.AddToClassList(RefreshButtonClassName);
            _root.Add(refreshButton);

            RebuildDeviceList();
            return _root;
        }

        /// <summary>
        /// 現在選択中のデバイスを外部から設定する（ハイライト更新のみ、イベントは発火しない）。
        /// AudioAnalyzer側でデバイス変更が完了した後の状態同期に使う。
        /// </summary>
        /// <param name="deviceName">選択されたデバイス名。</param>
        public void SetCurrentDevice(string? deviceName)
        {
            _currentDevice = deviceName;
            UpdateButtonHighlights();
        }

        private void RebuildDeviceList()
        {
            if (_deviceListContainer == null) return;

            _deviceListContainer.Clear();
            _deviceButtons.Clear();

            if (_availableDevices.Length == 0)
            {
                AddNoDeviceLabel();
                return;
            }

            foreach (var device in _availableDevices)
            {
                AddDeviceButton(device);
            }

            UpdateButtonHighlights();
        }

        private void AddNoDeviceLabel()
        {
            var label = new Label(NoDeviceText);
            label.AddToClassList(NoDeviceClassName);
            _deviceListContainer?.Add(label);
        }

        private void AddDeviceButton(string deviceName)
        {
            // クロージャでデバイス名をキャプチャ
            var captured = deviceName;
            var button = new Button(() => HandleDeviceClicked(captured))
            {
                text = TruncateDeviceName(deviceName)
            };
            button.AddToClassList(DeviceButtonClassName);
            button.tooltip = deviceName;

            _deviceButtons.Add(button);
            _deviceListContainer?.Add(button);
        }

        private void UpdateButtonHighlights()
        {
            for (var i = 0; i < _deviceButtons.Count; i++)
            {
                var button = _deviceButtons[i];
                var isSelected = i < _availableDevices.Length
                    && string.Equals(_availableDevices[i], _currentDevice, StringComparison.Ordinal);

                if (isSelected)
                {
                    button.AddToClassList(DeviceButtonSelectedClassName);
                }
                else
                {
                    button.RemoveFromClassList(DeviceButtonSelectedClassName);
                }
            }
        }

        private void HandleDeviceClicked(string deviceName)
        {
            _currentDevice = deviceName;
            UpdateButtonHighlights();

            try
            {
                OnDeviceSelected?.Invoke(deviceName);
            }
            catch (Exception e)
            {
                // 映像を止めないための防御的キャッチ
                Debug.LogError($"[AudioDeviceSelector] OnDeviceSelected handler failed: {e.Message}");
            }
        }

        private void HandleRefreshClicked()
        {
            try
            {
                OnRefreshRequested?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioDeviceSelector] OnRefreshRequested handler failed: {e.Message}");
            }
        }

        /// <summary>
        /// 長いデバイス名を表示用に切り詰める。フルネームはtooltipで確認可能。
        /// </summary>
        private static string TruncateDeviceName(string name)
        {
            const int maxLength = 32;
            if (name.Length <= maxLength) return name;
            return name.Substring(0, maxLength - 1) + "…";
        }
    }
}
