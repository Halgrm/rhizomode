#nullable enable

using System;
using R3;

namespace Rhizomode.Graph.Events
{
    /// <summary>
    /// グラフ変更通知のイベントバス。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: id ベース通知。subscriber は id を受け取り、必要に応じて
    /// <see cref="Rhizomode.Graph.Model.GraphState"/> から最新を fetch する設計。
    ///
    /// 3 種類の Observable:
    /// - <see cref="OnNodeAdded"/> / <see cref="OnNodeRemoved"/>: id
    /// - <see cref="OnEdgeAdded"/> / <see cref="OnEdgeRemoved"/>: id
    /// - <see cref="OnGraphChanged"/>: <see cref="GraphChangeSet"/> 全体 (scope batched)
    ///
    /// MutationScope 内では個別イベントを抑制し、Dispose 時に OnGraphChanged で一括通知する。
    ///
    /// Codex review fix: 内部 5 Subject の dispose 漏れ防止のため <see cref="IDisposable"/> を実装。
    /// 所有者 (Bootstrap / Installer) は OnDestroy / Lifetime 終了時に必ず Dispose を呼ぶ。
    /// </remarks>
    public sealed class GraphEventBus : IDisposable
    {
        private readonly Subject<string> _onNodeAdded = new();
        private readonly Subject<string> _onNodeRemoved = new();
        private readonly Subject<string> _onEdgeAdded = new();
        private readonly Subject<string> _onEdgeRemoved = new();
        private readonly Subject<GraphChangeSet> _onGraphChanged = new();

        public Observable<string> OnNodeAdded => _onNodeAdded;
        public Observable<string> OnNodeRemoved => _onNodeRemoved;
        public Observable<string> OnEdgeAdded => _onEdgeAdded;
        public Observable<string> OnEdgeRemoved => _onEdgeRemoved;
        public Observable<GraphChangeSet> OnGraphChanged => _onGraphChanged;

        public void EmitNodeAdded(string nodeId) => _onNodeAdded.OnNext(nodeId);
        public void EmitNodeRemoved(string nodeId) => _onNodeRemoved.OnNext(nodeId);
        public void EmitEdgeAdded(string edgeId) => _onEdgeAdded.OnNext(edgeId);
        public void EmitEdgeRemoved(string edgeId) => _onEdgeRemoved.OnNext(edgeId);
        public void EmitGraphChanged(GraphChangeSet changeSet) => _onGraphChanged.OnNext(changeSet);

        public void Dispose()
        {
            _onNodeAdded.Dispose();
            _onNodeRemoved.Dispose();
            _onEdgeAdded.Dispose();
            _onEdgeRemoved.Dispose();
            _onGraphChanged.Dispose();
        }
    }
}
