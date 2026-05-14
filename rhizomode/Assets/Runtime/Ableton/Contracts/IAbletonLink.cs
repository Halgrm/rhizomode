#nullable enable

using R3;

namespace Rhizomode.Ableton.Contracts
{
    /// <summary>
    /// AbletonLive との OSC 双方向通信 (送信 + listener + 応答ストリーム) の契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>AbletonLink.Instance</c> singleton を解消。具体実装は
    /// <c>Ableton.Transport.AbletonLink</c>、node への注入は
    /// <c>Ableton.GraphAdapter.AbletonTransportLifecycleProcessor</c> が担う。
    /// 接続設定 (Reconnect) は composition root (GameBootstrap) の責務として本契約には含めない。
    /// </remarks>
    public interface IAbletonLink
    {
        /// <summary>指定 OSC アドレスの応答 Observable を取得する。</summary>
        Observable<AbletonMessage> GetAddressObservable(string address);

        /// <summary>Ableton のプロパティ listener を開始する (参照カウント管理)。</summary>
        void StartListening(string basePath, string property);

        /// <summary>Ableton のプロパティ listener を停止する (参照カウントが 0 で実送信)。</summary>
        void StopListening(string basePath, string property);

        /// <summary>引数なし OSC メッセージ送信。</summary>
        void Send(string address);

        /// <summary>int 引数付き OSC メッセージ送信。</summary>
        void Send(string address, int arg);

        /// <summary>float 引数付き OSC メッセージ送信。</summary>
        void Send(string address, float arg);

        /// <summary>int 2 個の引数付き OSC メッセージ送信 (track+scene 等)。</summary>
        void Send(string address, int arg1, int arg2);

        /// <summary>int+float 引数付き OSC メッセージ送信 (track volume set 等)。</summary>
        void SendIntFloat(string address, int arg1, float arg2);

        /// <summary>int 3 個の引数付き OSC メッセージ送信 (device parameter query 等)。</summary>
        void SendInt3(string address, int arg1, int arg2, int arg3);

        /// <summary>int 3 個 + float 1 個の引数付き OSC メッセージ送信 (device parameter set)。</summary>
        void SendInt3Float(string address, int arg1, int arg2, int arg3, float arg4);
    }
}
