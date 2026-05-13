#nullable enable

using System;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// オーディオ入力デバイスを選択するノード。
    /// IInlineButtonでデバイスを切り替え、選択中のデバイス名を出力する。
    /// AudioDriverBehaviourが毎フレームこのノードを検出し、AudioAnalyzerのデバイス切り替えを実行する。
    /// </summary>
    [NodeType("AudioDevice", "Audio Device", NodeCategory.Input)]
    public class AudioDeviceNode : NodeBase, IInlineButton
    {
        private readonly OutputPort<float> _indexOut;

        private string[] _deviceList = Array.Empty<string>();
        private int _selectedIndex;
        private string? _pendingDevice;

        /// <summary>現在選択中のデバイス名。デバイス未登録なら null。</summary>
        public string? SelectedDevice =>
            _selectedIndex >= 0 && _selectedIndex < _deviceList.Length
                ? _deviceList[_selectedIndex]
                : null;

        /// <summary>
        /// デバイス切り替えが要求された場合に非nullを返す。
        /// AudioDriverBehaviourが読み取り後にクリアする。
        /// </summary>
        public string? ConsumePendingDevice()
        {
            var device = _pendingDevice;
            _pendingDevice = null;
            return device;
        }

        /// <summary>
        /// 利用可能デバイス一覧を外部から設定する。
        /// AudioDriverBehaviourが毎フレーム注入する。
        /// </summary>
        public void SetDeviceList(string[] devices)
        {
            if (devices == null) return;

            // リストが変わった場合のみ更新
            if (DeviceListEquals(_deviceList, devices)) return;

            var wasEmpty = _deviceList.Length == 0;
            _deviceList = devices;

            // 現在のデバイスがリスト内にあればインデックスを維持、なければ0にリセット
            if (_selectedIndex >= _deviceList.Length)
                _selectedIndex = _deviceList.Length > 0 ? 0 : -1;

            // 初回リスト取得時に最初のデバイスを自動選択
            if (wasEmpty && _deviceList.Length > 0 && _pendingDevice == null)
            {
                _selectedIndex = 0;
                _pendingDevice = _deviceList[0];
            }
        }

        /// <inheritdoc />
        string IInlineButton.ButtonLabel =>
            SelectedDevice != null ? TruncateName(SelectedDevice) : "No Device";

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed()
        {
            if (_deviceList.Length == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _deviceList.Length;
            _pendingDevice = SelectedDevice;
            _indexOut.Emit(_selectedIndex);
        }

        public AudioDeviceNode(string id) : base(id, "AudioDevice")
        {
            _indexOut = RegisterOutput<float>("Index", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            // 初期値を出力
            _indexOut.Emit(_selectedIndex);
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new AudioDeviceParams
            {
                deviceName = SelectedDevice ?? "",
                selectedIndex = _selectedIndex
            });
            return data;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<AudioDeviceParams>(paramsJson);
                _selectedIndex = Mathf.Max(p.selectedIndex, 0);
                // デバイス名はロード後にSetDeviceListで照合される
                if (!string.IsNullOrEmpty(p.deviceName))
                    _pendingDevice = p.deviceName;
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        /// <summary>
        /// ロード後にデバイスリストが設定された際、保存されたデバイス名でインデックスを復元する。
        /// </summary>
        public void ResolveDeviceIndex()
        {
            if (_pendingDevice == null || _deviceList.Length == 0) return;

            for (var i = 0; i < _deviceList.Length; i++)
            {
                if (string.Equals(_deviceList[i], _pendingDevice, StringComparison.Ordinal))
                {
                    _selectedIndex = i;
                    _pendingDevice = _deviceList[i]; // 切り替え要求として発火
                    return;
                }
            }

            // デバイスが見つからなければ0番にフォールバック
            _selectedIndex = 0;
            _pendingDevice = _deviceList[0];
        }

        private static bool DeviceListEquals(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static string TruncateName(string name)
        {
            const int maxLength = 18;
            if (name.Length <= maxLength) return name;
            return name.Substring(0, maxLength - 1) + "…";
        }

        [Serializable]
        private struct AudioDeviceParams
        {
            public string deviceName;
            public int selectedIndex;
        }
    }
}
