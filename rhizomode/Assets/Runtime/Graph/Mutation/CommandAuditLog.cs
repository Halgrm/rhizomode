#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// コマンド発行の audit log (origin 別の発行統計 + dispatch trace)。
    /// </summary>
    /// <remarks>
    /// Plan v5.0/v5.1: <see cref="GraphCommandDispatcher"/> が Execute 時に origin を流す。
    /// 同一 command kind が 2 種類以上の Origin から発行されたら warning (Adapter 重複検出)。
    /// </remarks>
    public sealed class CommandAuditLog
    {
        private readonly Dictionary<CommandOrigin, int> _countByOrigin = new();
        private readonly Dictionary<(string commandKind, CommandOrigin origin), int> _byKindOrigin = new();
        private readonly List<(string kind, CommandOrigin origin)> _trace = new();

        public IReadOnlyDictionary<CommandOrigin, int> CountByOrigin => _countByOrigin;
        public IReadOnlyList<(string Kind, CommandOrigin Origin)> Trace => _trace;

        public void Record(IGraphCommand command)
        {
            _countByOrigin[command.Origin] = _countByOrigin.GetValueOrDefault(command.Origin) + 1;
            var kind = command.GetType().Name;
            var key = (kind, command.Origin);
            _byKindOrigin[key] = _byKindOrigin.GetValueOrDefault(key) + 1;
            _trace.Add((kind, command.Origin));
        }

        /// <summary>
        /// 現時点の audit log size を checkpoint として返す。
        /// </summary>
        /// <remarks>
        /// F-Vf-d.3 (Codex review WARN: audit not transactional): <see cref="GraphCommandScope"/> が
        /// scope 開始時に呼び、失敗 rollback 時に <see cref="RollbackToCheckpoint"/> でこの位置まで戻す。
        /// 戻り値は不透明な整数 token として扱う (実装は trace.Count)。
        /// </remarks>
        internal int SaveCheckpoint() => _trace.Count;

        /// <summary>
        /// 指定 checkpoint 以降に <see cref="Record"/> された entry を全て巻き戻す。
        /// </summary>
        /// <remarks>
        /// F-Vf-d.3: scope 失敗時に呼ばれる。trace + countByOrigin + byKindOrigin の 3 つを整合させて
        /// 巻き戻す (count はネガティブにならない範囲で減算、0 になったら entry を削除)。
        /// 不正な checkpoint (負 or 現在 size より大) は no-op。
        /// </remarks>
        internal void RollbackToCheckpoint(int checkpoint)
        {
            if (checkpoint < 0 || checkpoint >= _trace.Count) return;

            for (var i = _trace.Count - 1; i >= checkpoint; i--)
            {
                var (kind, origin) = _trace[i];

                var byOrigin = _countByOrigin.GetValueOrDefault(origin) - 1;
                if (byOrigin <= 0) _countByOrigin.Remove(origin);
                else _countByOrigin[origin] = byOrigin;

                var key = (kind, origin);
                var byKindOrigin = _byKindOrigin.GetValueOrDefault(key) - 1;
                if (byKindOrigin <= 0) _byKindOrigin.Remove(key);
                else _byKindOrigin[key] = byKindOrigin;
            }
            _trace.RemoveRange(checkpoint, _trace.Count - checkpoint);
        }

        /// <summary>同一 command kind が複数の Origin から発行されたケースを列挙する。</summary>
        public IEnumerable<string> DetectMultiOriginCommands()
        {
            var byKind = new Dictionary<string, HashSet<CommandOrigin>>();
            foreach (var ((kind, origin), _) in _byKindOrigin)
            {
                if (!byKind.TryGetValue(kind, out var set))
                {
                    set = new HashSet<CommandOrigin>();
                    byKind[kind] = set;
                }
                set.Add(origin);
            }
            foreach (var (kind, origins) in byKind)
            {
                if (origins.Count > 1) yield return kind;
            }
        }
    }
}
