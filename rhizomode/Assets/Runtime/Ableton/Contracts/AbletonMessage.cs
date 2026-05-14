#nullable enable

namespace Rhizomode.Ableton.Contracts
{
    /// <summary>
    /// 受信した AbletonOSC メッセージ。バックグラウンドスレッドからメインスレッドへの
    /// キュー転送用。型不一致は 0 / 空文字フォールバックされ、受信側ノードが期待する型の
    /// 配列から読めばよい設計。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>AbletonLink.AbletonMessage</c> nested struct を
    /// <see cref="IAbletonLink"/> の戻り値型として Contracts 層に移送。
    /// </remarks>
    public readonly struct AbletonMessage
    {
        public readonly string Address;
        public readonly float[] FloatArgs;
        public readonly int[] IntArgs;
        public readonly string[] StringArgs;

        public AbletonMessage(string address, float[] floatArgs, int[] intArgs, string[] stringArgs)
        {
            Address = address;
            FloatArgs = floatArgs;
            IntArgs = intArgs;
            StringArgs = stringArgs;
        }
    }
}
