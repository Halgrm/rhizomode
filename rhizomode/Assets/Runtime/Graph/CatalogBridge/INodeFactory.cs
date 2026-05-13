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
    }
}
