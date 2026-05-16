#nullable enable

using Rhizomode.Graph.Snapshot;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// 7 つの core グラフコマンド record。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: Undo には <see cref="GraphSnapshot"/> を使い、JSON 形式 (GraphData) からは独立。
    /// 各 record は immutable で、<see cref="GraphCommandDispatcher"/> 経由でのみ実行される。
    /// </remarks>
    public sealed record AddNodeCommand(
        CommandOrigin Origin,
        string NodeId,
        string TypeName,
        RzVector3 Position) : IGraphCommand;

    public sealed record RemoveNodeCommand(
        CommandOrigin Origin,
        string NodeId) : IGraphCommand;

    public sealed record ConnectPortsCommand(
        CommandOrigin Origin,
        string EdgeId,
        string FromNodeId,
        string FromPortName,
        string ToNodeId,
        string ToPortName) : IGraphCommand;

    public sealed record DisconnectEdgeCommand(
        CommandOrigin Origin,
        string EdgeId) : IGraphCommand;

    public sealed record MoveNodeCommand(
        CommandOrigin Origin,
        string NodeId,
        RzVector3 NewPosition) : IGraphCommand;

    public sealed record SetNodeParamCommand(
        CommandOrigin Origin,
        string NodeId,
        string ParamName,
        ParamValue Value) : IGraphCommand;

    public sealed record LoadGraphCommand(
        CommandOrigin Origin,
        GraphSnapshot Snapshot) : IGraphCommand;

    /// <summary>
    /// 複数 <see cref="IGraphCommand"/> を 1 つの atomic 単位として Undo 履歴に積むためのマーカー record。
    /// </summary>
    /// <remarks>
    /// F-Vf-d.2: <see cref="GraphMutationScope.Commit"/> が pre-scope snapshot と一緒に本 record を Dispatcher に渡し、
    /// scope 内 sub-command 群を 1 ステップの Undo として扱う。<see cref="GraphMutationApplier.TryApply"/> 上では
    /// 何もしない (sub-command は scope 内で逐次適用済 + Undo/Redo は Snapshot 復元ベース)。
    /// </remarks>
    public sealed record CompositeCommand(
        CommandOrigin Origin,
        System.Collections.Generic.IReadOnlyList<IGraphCommand> Children) : IGraphCommand;
}
