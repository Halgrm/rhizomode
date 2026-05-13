#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.Interaction.Contracts
{
    /// <summary>
    /// 空間操作 (VR コントローラー由来) の intent を表す record group。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 5: <c>Interaction</c> asmdef 配下の handler は <see cref="IInteractionIntent"/>
    /// を emit するだけで、<c>Graph.Mutation.Dispatcher</c> を直接呼ばない (CI で grep 検証)。
    /// <c>Interaction.GraphAdapter</c> 配下の <c>SpatialIntentToCommandTranslator</c> が intent を
    /// 受け取り、<c>IGraphCommand</c> (Origin=Interaction) に変換して dispatch する。
    ///
    /// この層は <c>Graph.Model</c> / <c>Graph.Mutation</c> を知らない (SharedKernel のみ参照)。
    /// </remarks>
    public interface IInteractionIntent
    {
    }

    /// <summary>ノードをグラブで掴む。</summary>
    public sealed record GrabIntent(string NodeId) : IInteractionIntent;

    /// <summary>ノードを離す。</summary>
    public sealed record ReleaseIntent(string NodeId) : IInteractionIntent;

    /// <summary>ノードを別の位置へ移動する。</summary>
    public sealed record MoveNodeIntent(string NodeId, RzVector3 NewPosition) : IInteractionIntent;

    /// <summary>2 つのポートを接続する。</summary>
    public sealed record ConnectPortsIntent(
        string FromNodeId, string FromPortName,
        string ToNodeId, string ToPortName) : IInteractionIntent;

    /// <summary>エッジを切断する。</summary>
    public sealed record DisconnectEdgeIntent(string EdgeId) : IInteractionIntent;

    /// <summary>新規ノードをスポーンする (Scroll Menu / Preset 経由)。</summary>
    public sealed record SpawnNodeIntent(string TypeName, RzVector3 Position) : IInteractionIntent;

    /// <summary>ノードを削除する。</summary>
    public sealed record DeleteNodeIntent(string NodeId) : IInteractionIntent;
}
