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
}
