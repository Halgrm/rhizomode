#nullable enable

using System.Collections.Concurrent;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// バックグラウンドスレッドから queue されたコマンドをメインスレッドで dispatch する queue。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: Audio / Ableton 受信スレッド等から発行されたコマンドを安全にメインスレッドで実行する。
    /// Bootstrap の ITickable adapter (<c>MainThreadCommandTicker</c>) が毎フレーム <see cref="Tick"/> を呼ぶ。
    /// </remarks>
    public sealed class MainThreadGraphCommandQueue
    {
        private readonly ConcurrentQueue<IGraphCommand> _queue = new();
        private readonly GraphCommandDispatcher _dispatcher;

        public MainThreadGraphCommandQueue(GraphCommandDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public int PendingCount => _queue.Count;

        /// <summary>任意のスレッドから呼び出し可能。</summary>
        public void Enqueue(IGraphCommand command) => _queue.Enqueue(command);

        /// <summary>
        /// メインスレッドからのみ呼び出すこと。queue にある全コマンドを順次 dispatch する。
        /// </summary>
        public void Tick()
        {
            while (_queue.TryDequeue(out var command))
            {
                _dispatcher.Execute(command);
            }
        }
    }
}
