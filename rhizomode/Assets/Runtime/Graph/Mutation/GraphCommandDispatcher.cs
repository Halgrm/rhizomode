#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Snapshot;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// グラフコマンドの dispatcher (Undo/Redo + audit)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 3: 全 mutation はこの dispatcher 経由でのみ実行される。
    /// <see cref="GraphMutationApplier"/> に command 適用を委譲し、Snapshot を Undo/Redo に積む。
    /// JSON 形式 (GraphData) からは独立 (Snapshot ベース)。
    ///
    /// 履歴サイズは <see cref="HistoryConfig"/> SO で調整可能。
    /// </remarks>
    public sealed class GraphCommandDispatcher
    {
        private readonly GraphMutationApplier _applier;
        private readonly CommandAuditLog _auditLog = new();
        private readonly LinkedList<(IGraphCommand Command, GraphSnapshot PreSnapshot)> _undoHistory = new();
        private readonly Stack<(IGraphCommand Command, GraphSnapshot PostSnapshot)> _redoStack = new();
        private readonly int _maxHistorySize;
        private GraphCommandScope? _activeScope;

        public CommandAuditLog AuditLog => _auditLog;
        public int UndoStackCount => _undoHistory.Count;
        public int RedoStackCount => _redoStack.Count;

        public GraphCommandDispatcher(GraphMutationApplier applier, int maxHistorySize = 64)
        {
            _applier = applier;
            _maxHistorySize = maxHistorySize < 1 ? 1 : maxHistorySize;
        }

        /// <summary>
        /// 連投 dispatch を atomic 単位として扱う <see cref="GraphCommandScope"/> を開始する。
        /// </summary>
        /// <remarks>
        /// F-Vf-d.2 (Codex review #3 NON_ATOMIC_MULTI_DISPATCH): scope 内で <see cref="GraphCommandScope.TryExecute"/>
        /// を呼び、全件成功なら <see cref="GraphCommandScope.Commit"/> で 1 ステップの Undo 履歴に積む。途中失敗 or
        /// 未 commit で Dispose されたら、scope 開始時の Snapshot に rollback される。
        ///
        /// F-Vf-d.2 re-review fix: nested scope は未サポート (Undo / audit の整合が壊れるため)。既に active な
        /// scope がある状態で本メソッドを呼ぶと <see cref="System.InvalidOperationException"/> を投げる。
        /// </remarks>
        public GraphCommandScope BeginScope()
        {
            if (_activeScope != null && !_activeScope.IsFinalized)
            {
                throw new System.InvalidOperationException(
                    "Nested GraphCommandScope is not supported. Finalize (Commit/Dispose) the active scope before starting a new one.");
            }
            _activeScope = new GraphCommandScope(this, _applier, _auditLog);
            return _activeScope;
        }

        /// <summary>
        /// scope 内で commit された sub-command 群を 1 ステップの Undo として履歴に積む。
        /// </summary>
        /// <remarks>F-Vf-d.2: <see cref="GraphCommandScope.Commit"/> 専用 API。直接呼ばないこと。</remarks>
        internal void RecordScopeUndoEntry(GraphSnapshot preSnapshot, CompositeCommand marker)
        {
            _undoHistory.AddLast(((IGraphCommand)marker, preSnapshot));
            while (_undoHistory.Count > _maxHistorySize)
            {
                _undoHistory.RemoveFirst();
            }
            _redoStack.Clear();
        }

        /// <summary>command を実行し、Undo 用に pre-snapshot を積む。</summary>
        /// <remarks>
        /// TODO (Phase 4 backlog): MoveNode / SetNodeParam は drag / Ableton から 60+ Hz で流れ得る。
        /// 毎回フル Snapshot を取ると List 2 個 + per-node 構造体の allocation が発生する。
        /// 高頻度コマンドは pre-snapshot をスキップし、逆コマンド (inverse-command) で undo するか
        /// list を pool 化して再利用する最適化を検討する。
        ///
        /// Codex re-review #4 fix (2026-05-16): scene shutdown 中の race で disposed graph に対して
        /// MainThreadGraphCommandQueue 由来の commands が dispatch され得る。冒頭で early return し、
        /// Undo / audit を汚さない。
        /// </remarks>
        public void Execute(IGraphCommand command)
        {
            if (_applier.IsGraphDisposed) return;

            var pre = _applier.CaptureSnapshot();
            _applier.Apply(command);
            _auditLog.Record(command);

            _undoHistory.AddLast((command, pre));
            while (_undoHistory.Count > _maxHistorySize)
            {
                _undoHistory.RemoveFirst();
            }
            _redoStack.Clear();
        }

        /// <summary>1 ステップ undo。state を pre-snapshot に戻す。</summary>
        public bool TryUndo()
        {
            if (_undoHistory.Count == 0) return false;
            var entry = _undoHistory.Last!.Value;
            _undoHistory.RemoveLast();

            var post = _applier.CaptureSnapshot();
            _applier.RestoreFromSnapshot(entry.PreSnapshot);
            _redoStack.Push((entry.Command, post));
            return true;
        }

        /// <summary>1 ステップ redo。state を post-snapshot に戻す。</summary>
        public bool TryRedo()
        {
            if (_redoStack.Count == 0) return false;
            var (command, postSnapshot) = _redoStack.Pop();

            var pre = _applier.CaptureSnapshot();
            _applier.RestoreFromSnapshot(postSnapshot);
            _undoHistory.AddLast((command, pre));
            while (_undoHistory.Count > _maxHistorySize)
            {
                _undoHistory.RemoveFirst();
            }
            return true;
        }
    }
}
