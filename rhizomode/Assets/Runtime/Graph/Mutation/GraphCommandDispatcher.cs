#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Snapshot;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// グラフコマンドの dispatcher (Undo/Redo + audit)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 3: 全 mutation はこの dispatcher 経由でのみ実行される。
    /// Undo は <see cref="GraphSnapshot"/> ベースで JSON 形式から独立。
    ///
    /// Phase 2 では skeleton: コマンド種別ごとの実行ロジックは Phase 3-5 で実装。
    /// Audit / Undo stack の枠組みのみ提供する。
    /// </remarks>
    public sealed class GraphCommandDispatcher
    {
        private readonly CommandAuditLog _auditLog = new();
        private readonly Stack<(IGraphCommand Command, GraphSnapshot PreSnapshot)> _undoStack = new();
        private readonly Stack<(IGraphCommand Command, GraphSnapshot PreSnapshot)> _redoStack = new();
        private readonly int _maxHistorySize;

        public CommandAuditLog AuditLog => _auditLog;
        public int UndoStackCount => _undoStack.Count;
        public int RedoStackCount => _redoStack.Count;

        public GraphCommandDispatcher(int maxHistorySize = 64)
        {
            _maxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// コマンドを実行する。実装は Phase 3-5 で各 command kind ごとに割り当てる。
        /// </summary>
        public void Execute(IGraphCommand command, GraphSnapshot preSnapshot, Action<IGraphCommand> apply)
        {
            _auditLog.Record(command);

            apply(command);

            _undoStack.Push((command, preSnapshot));
            if (_undoStack.Count > _maxHistorySize)
            {
                // 最古を捨てるため一旦 reverse、再構築
                var list = new List<(IGraphCommand, GraphSnapshot)>(_undoStack);
                _undoStack.Clear();
                for (var i = list.Count - 2; i >= 0; i--) _undoStack.Push(list[i]);
            }
            _redoStack.Clear();
        }

        /// <summary>1 ステップ undo (Snapshot で復元)。</summary>
        public bool TryUndo(Action<GraphSnapshot> restore)
        {
            if (_undoStack.Count == 0) return false;
            var (cmd, snap) = _undoStack.Pop();
            restore(snap);
            _redoStack.Push((cmd, snap));
            return true;
        }
    }
}
