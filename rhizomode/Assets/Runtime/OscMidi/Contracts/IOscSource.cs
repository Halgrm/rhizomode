#nullable enable

using R3;

namespace Rhizomode.OscMidi.Contracts
{
    /// <summary>
    /// OSC 受信値のストリームを提供する transport の契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>OscServer.Instance</c> singleton を解消し、
    /// 本 interface 経由で OSC 値を取得する。具体実装は <c>OscMidi.Transport.OscServer</c>、
    /// node への注入は <c>OscMidi.GraphAdapter.OscMidiTransportLifecycleProcessor</c> が担う。
    /// </remarks>
    public interface IOscSource
    {
        /// <summary>指定 OSC アドレスの値変化 Observable を取得する。</summary>
        Observable<float> GetAddressObservable(string address);
    }
}
