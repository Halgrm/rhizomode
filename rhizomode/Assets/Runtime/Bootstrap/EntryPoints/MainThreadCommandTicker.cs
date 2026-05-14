#nullable enable

using Rhizomode.Graph.Mutation;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.EntryPoints
{
    /// <summary>
    /// VContainer ITickable adapter — 毎フレーム <see cref="MainThreadGraphCommandQueue.Tick"/> を呼ぶ。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 EntryPoints tick order #1。バックグラウンドスレッド (Audio / OSC 受信等) から
    /// queue されたグラフコマンドを、他のどの tick adapter よりも先にメインスレッドへ反映する。
    /// 詳細は EntryPoints/TickOrder.md を参照。
    /// </remarks>
    public sealed class MainThreadCommandTicker : ITickable
    {
        private readonly MainThreadGraphCommandQueue _queue;

        public MainThreadCommandTicker(MainThreadGraphCommandQueue queue)
        {
            _queue = queue;
        }

        public void Tick() => _queue.Tick();
    }
}
