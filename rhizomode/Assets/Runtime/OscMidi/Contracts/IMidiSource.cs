#nullable enable

using R3;

namespace Rhizomode.OscMidi.Contracts
{
    /// <summary>
    /// MIDI CC 値のストリームを提供する transport の契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>MidiServer.Instance</c> singleton を解消し、
    /// 本 interface 経由で MIDI CC 値を取得する。具体実装は <c>OscMidi.Transport.MidiServer</c>、
    /// node への注入は <c>OscMidi.GraphAdapter.OscMidiTransportLifecycleProcessor</c> が担う。
    /// </remarks>
    public interface IMidiSource
    {
        /// <summary>指定 MIDI CC 番号・チャンネルの値変化 Observable を取得する (0-1 正規化済み)。</summary>
        Observable<float> GetCCObservable(int channel, int ccNumber);
    }
}
