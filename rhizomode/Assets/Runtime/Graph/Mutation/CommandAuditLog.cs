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
