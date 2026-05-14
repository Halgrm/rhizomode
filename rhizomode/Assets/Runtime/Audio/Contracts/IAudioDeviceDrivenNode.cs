#nullable enable

namespace Rhizomode.Audio.Contracts
{
    /// <summary>
    /// audio device の切り替わり / 初期化を観測するノード contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: AudioDeviceNode (デバイス選択 UI 駆動) など、device 自体の状態を
    /// 表現するノードが実装する。<see cref="IAudioDrivenNode"/> が「フレーム単位の audio
    /// data」を受け取るのに対し、本 interface は「device の identity / capability」変化を
    /// 通知する。
    /// </remarks>
    public interface IAudioDeviceDrivenNode
    {
        /// <summary>
        /// device の切り替わり / 初期化 / shutdown 時に呼ばれる。
        /// </summary>
        /// <param name="deviceName">新 device 名 (shutdown 時 null)</param>
        /// <param name="sampleRate">sample rate Hz (shutdown 時 0)</param>
        void OnAudioDeviceChanged(string? deviceName, int sampleRate);
    }
}
