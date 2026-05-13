#nullable enable

using System;
using Rhizomode.Graph.Mutation;
using Rhizomode.Interaction.Contracts;

namespace Rhizomode.Interaction.GraphAdapter
{
    /// <summary>
    /// 空間操作 <see cref="IInteractionIntent"/> を <see cref="IGraphCommand"/> に変換して
    /// <see cref="GraphCommandDispatcher"/> 経由で実行する translator。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 5 (responsibility: 空間操作): 発行する全 command の
    /// <see cref="CommandOrigin.Interaction"/> を Origin field に固定する。CI で検証可能。
    ///
    /// 本クラスは <c>IGraphCommand</c> の record 生成のみを担い、実際の GraphState mutation は
    /// Dispatcher → GraphMutationApplier が行う。Interaction 層は intent 発火、ここで command 変換、
    /// Mutation 層で適用、と責務が階層化される。
    /// </remarks>
    public sealed class SpatialIntentToCommandTranslator : IIntentSink
    {
        private readonly GraphCommandDispatcher _dispatcher;
        private readonly Func<string> _nodeIdProvider;
        private readonly Func<string> _edgeIdProvider;

        public SpatialIntentToCommandTranslator(
            GraphCommandDispatcher dispatcher,
            Func<string>? nodeIdProvider = null,
            Func<string>? edgeIdProvider = null)
        {
            _dispatcher = dispatcher;
            _nodeIdProvider = nodeIdProvider ?? (() => Guid.NewGuid().ToString());
            _edgeIdProvider = edgeIdProvider ?? (() => Guid.NewGuid().ToString());
        }

        /// <summary>
        /// <see cref="IIntentSink.Emit"/> の実装 (= <see cref="Translate"/> と等価)。
        /// </summary>
        public bool Emit(IInteractionIntent intent) => Translate(intent);

        /// <summary>
        /// intent を解釈して該当する <see cref="IGraphCommand"/> を構築し、Dispatcher で実行する。
        /// </summary>
        /// <returns>command が発行された場合 true、intent が認識されなかった場合 false。</returns>
        public bool Translate(IInteractionIntent intent)
        {
            switch (intent)
            {
                case MoveNodeIntent move:
                    _dispatcher.Execute(new MoveNodeCommand(
                        CommandOrigin.Interaction, move.NodeId, move.NewPosition));
                    return true;

                case ConnectPortsIntent connect:
                    _dispatcher.Execute(new ConnectPortsCommand(
                        CommandOrigin.Interaction,
                        _edgeIdProvider(),
                        connect.FromNodeId, connect.FromPortName,
                        connect.ToNodeId, connect.ToPortName));
                    return true;

                case DisconnectEdgeIntent disconnect:
                    _dispatcher.Execute(new DisconnectEdgeCommand(
                        CommandOrigin.Interaction, disconnect.EdgeId));
                    return true;

                case SpawnNodeIntent spawn:
                    _dispatcher.Execute(new AddNodeCommand(
                        CommandOrigin.Interaction,
                        _nodeIdProvider(),
                        spawn.TypeName,
                        spawn.Position));
                    return true;

                case DeleteNodeIntent delete:
                    _dispatcher.Execute(new RemoveNodeCommand(
                        CommandOrigin.Interaction, delete.NodeId));
                    return true;

                case GrabIntent _:
                case ReleaseIntent _:
                    // Grab/Release は graph state を変えない (描画レイヤーのみ)。
                    // 必要に応じて UI ViewModel に hover 表示等を反映するが、Mutation は発行しない。
                    return false;

                default:
                    UnityEngine.Debug.LogWarning(
                        $"[SpatialIntentToCommandTranslator] Unhandled intent: {intent.GetType().Name}");
                    return false;
            }
        }
    }
}
