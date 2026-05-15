#nullable enable

using System;
using Rhizomode.Audio.Analysis;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.UI;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// <see cref="AudioDeviceSelector"/> UI と <see cref="AudioAnalyzer"/> を相互配線する
    /// post-Build wiring。Plan v5.4 §15 (V3a): 旧 <c>GameBootstrap.InitializeAudioDeviceSelector</c>
    /// を Bootstrap asmdef へ移送。
    /// </summary>
    /// <remarks>
    /// V2 踏襲: <see cref="AudioInstaller"/> は本クラスを container に登録するのみ。副作用を伴う
    /// <see cref="Wire"/> は Build 後の eager step (<c>EntryPointBootstrapper</c>) が明示的に駆動する。
    /// イベント購読は <see cref="Dispose"/> で解除する (container が Lifetime.Singleton として Dispose)。
    /// </remarks>
    public sealed class AudioDeviceSelectorWiring : IDisposable
    {
        private readonly AudioAnalyzer? _analyzer;
        private readonly AudioDeviceSelector? _selector;
        private readonly StatusPanelController? _statusPanel;

        private Action<string>? _onDeviceSelected;
        private Action? _onRefreshRequested;
        private bool _wired;

        public AudioDeviceSelectorWiring(XrSceneReferences refs)
        {
            _analyzer = refs.AudioAnalyzer;
            _selector = refs.AudioDeviceSelector;
            _statusPanel = refs.StatusPanel;
        }

        /// <summary>
        /// AudioDeviceSelector を analyzer のデバイス一覧で初期化し、選択 / リフレッシュ要求を配線する。
        /// selector または analyzer が未配置なら何もしない (fail-open)。
        /// </summary>
        public void Wire()
        {
            if (_wired) return;
            if (_selector == null || _analyzer == null) return;

            var analyzer = _analyzer;
            _selector.Initialize(analyzer.AvailableDevices, analyzer.CurrentDevice);

            _onDeviceSelected = deviceName =>
            {
                analyzer.Initialize(deviceName);
                _selector.SetCurrentDevice(analyzer.CurrentDevice);
                _statusPanel?.SetAudioDevice(deviceName);
            };
            _selector.OnDeviceSelected += _onDeviceSelected;

            _onRefreshRequested = () => _selector.UpdateDeviceList(analyzer.AvailableDevices);
            _selector.OnRefreshRequested += _onRefreshRequested;

            if (analyzer.CurrentDevice != null)
                _statusPanel?.SetAudioDevice(analyzer.CurrentDevice);

            _wired = true;
        }

        public void Dispose()
        {
            if (_selector != null)
            {
                if (_onDeviceSelected != null)
                    _selector.OnDeviceSelected -= _onDeviceSelected;
                if (_onRefreshRequested != null)
                    _selector.OnRefreshRequested -= _onRefreshRequested;
            }

            _onDeviceSelected = null;
            _onRefreshRequested = null;
            _wired = false;
        }
    }
}
