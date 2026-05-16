#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Snapshot;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// 連投 <see cref="IGraphCommand"/> を atomic 単位として実行する scope。
    /// </summary>
    /// <remarks>
    /// F-Vf-d.2 (Codex review #3 NON_ATOMIC_MULTI_DISPATCH 解消): NodeSpawnService の AddNode + ConnectPorts
    /// 連投を 1 つの "全成功 or 全 rollback" にまとめる primitive。<see cref="GraphCommandDispatcher.BeginScope"/>
    /// で取得し、<see cref="TryExecute"/> を順次呼ぶ。途中 false (Applier 失敗) を返した時点で entry-time の
    /// <see cref="GraphSnapshot"/> に rollback され、以降の TryExecute は受け付けない。全件成功なら
    /// <see cref="Commit"/> を呼ぶことで 1 ステップ Undo 履歴に積まれる。
    ///
    /// Commit せず Dispose した場合も entry snapshot に rollback される (using-statement で安全に使える)。
    /// <see cref="CompositeCommand"/> record が Undo 履歴の representative marker として渡される。
    ///
    /// 命名注: 同じ project に <c>Rhizomode.Graph.Events.GraphMutationScope</c> (Plan v5.3 で導入、
    /// GraphChangeSet を 1 ChangeSet に束ねるための batching scope) が既に存在するため、本クラスは
    /// "command" scope と命名し責務の差を明示する。
    /// </remarks>
    public sealed class GraphCommandScope : IDisposable
    {
        private readonly GraphCommandDispatcher _dispatcher;
        private readonly GraphMutationApplier _applier;
        private readonly CommandAuditLog _auditLog;
        private readonly GraphSnapshot _entrySnapshot;
        private readonly int _auditCheckpoint;
        private readonly List<IGraphCommand> _committed = new();
        private bool _isFinalized;
        private bool _hasFailed;

        internal GraphCommandScope(
            GraphCommandDispatcher dispatcher,
            GraphMutationApplier applier,
            CommandAuditLog auditLog)
        {
            _dispatcher = dispatcher;
            _applier = applier;
            _auditLog = auditLog;
            _entrySnapshot = applier.CaptureSnapshot();
            _auditCheckpoint = auditLog.SaveCheckpoint();
        }

        /// <summary>scope 内で発生した最初の Apply 失敗以降は true。</summary>
        public bool HasFailed => _hasFailed;

        /// <summary>scope が Commit 済 or Dispose 済なら true (further TryExecute は no-op)。</summary>
        public bool IsFinalized => _isFinalized;

        /// <summary>これまで適用済の sub-command 件数 (audit + テスト用)。</summary>
        public int CommittedCount => _committed.Count;

        /// <summary>
        /// command を適用する。失敗時は scope 全体を rollback し以降の TryExecute は no-op。
        /// </summary>
        /// <returns>適用に成功した場合 true。失敗 (or 既に Finalized) なら false。</returns>
        public bool TryExecute(IGraphCommand command)
        {
            if (_isFinalized || _hasFailed) return false;

            if (_applier.TryApply(command))
            {
                _committed.Add(command);
                _auditLog.Record(command);
                return true;
            }

            // 失敗 — scope 全体を rollback (graph state + audit log の両方)
            _applier.RestoreFromSnapshot(_entrySnapshot);
            _auditLog.RollbackToCheckpoint(_auditCheckpoint);
            _committed.Clear();
            _hasFailed = true;
            return false;
        }

        /// <summary>
        /// scope 内 sub-command 群を 1 ステップの Undo として履歴に積む。
        /// </summary>
        /// <remarks>失敗済 or sub-command 0 件なら no-op。Commit 後は Finalized 状態 (Dispose 安全)。</remarks>
        public void Commit()
        {
            if (_isFinalized) return;
            _isFinalized = true;

            if (_hasFailed || _committed.Count == 0) return;

            var marker = new CompositeCommand(_committed[0].Origin, _committed.AsReadOnly());
            _dispatcher.RecordScopeUndoEntry(_entrySnapshot, marker);
            // F-Vf-d.3: scope boundary を audit にも記録 (CompositeCommand 1 件として)。
            _auditLog.Record(marker);
        }

        /// <summary>未 Commit で破棄された場合に entry snapshot へ rollback する。</summary>
        public void Dispose()
        {
            if (_isFinalized) return;
            _isFinalized = true;

            if (_hasFailed || _committed.Count == 0) return;

            // 暗黙 rollback (Commit 忘れの保険) — graph state + audit log の両方
            _applier.RestoreFromSnapshot(_entrySnapshot);
            _auditLog.RollbackToCheckpoint(_auditCheckpoint);
            _committed.Clear();
        }
    }
}
