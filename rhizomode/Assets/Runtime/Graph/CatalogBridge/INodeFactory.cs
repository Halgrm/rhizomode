#nullable enable

using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.CatalogBridge
{
    /// <summary>
    /// ノードのインスタンスを生成する contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: NodeCatalog.Runtime が <c>AttributeScannerNodeFactory</c> で実装。
    /// Graph.Mutation の AddNode コマンドが本 interface 経由でノードを生成する。
    /// </remarks>
    public interface INodeFactory
    {
        /// <summary>typeName に対応する factory が登録されているか。</summary>
        bool CanCreate(string typeName);

        /// <summary>指定された typeName と id でノードインスタンスを生成する。</summary>
        /// <returns>未知の typeName の場合 null。</returns>
        NodeBase? Create(string typeName, string nodeId);

        /// <summary>
        /// paramsJson 付きの生成 overload (Codex re-review #5 fix, 2026-05-16)。
        /// SceneObjectNode のような「ports / 構造が constructor 引数で決まるノード」を
        /// hydration / Snapshot restore 経由で正しく復元できるようにする。
        /// </summary>
        /// <remarks>
        /// 既定実装は paramsJson を無視して 2 引数版に委譲。paramsJson を実際に消費するのは
        /// <see cref="GraphStateBackedNodeFactory"/> + <see cref="GraphState.CreateNodeWithId"/> 経路のみ。
        /// </remarks>
        NodeBase? Create(string typeName, string nodeId, string paramsJson) =>
            Create(typeName, nodeId);
    }
}
