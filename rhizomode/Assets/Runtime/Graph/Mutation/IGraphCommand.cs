#nullable enable

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// 全グラフコマンドの基底 contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: 全 IGraphCommand record が <see cref="Origin"/> フィールドを持つ。
    /// <see cref="GraphCommandDispatcher"/> が origin を <c>CommandAuditLog</c> に記録する。
    ///
    /// 具体コマンド (Phase 2/3 で順次実装):
    ///   AddNodeCommand, RemoveNodeCommand, ConnectPortsCommand, DisconnectEdgeCommand,
    ///   MoveNodeCommand, SetNodeParamCommand, LoadGraphCommand
    /// </remarks>
    public interface IGraphCommand
    {
        CommandOrigin Origin { get; }
    }
}
