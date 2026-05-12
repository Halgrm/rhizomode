#nullable enable

namespace Rhizomode.NodeCatalog.Contracts
{
    /// <summary>
    /// ノードタイプのUI表示情報。ノード生成メニューとノード表示で使用。
    /// </summary>
    public class NodeTypeInfo
    {
        /// <summary>ノードタイプ名（PascalCase、NodeBase.NodeTypeと一致）。</summary>
        public string TypeName { get; }

        /// <summary>UIに表示するノード名。</summary>
        public string DisplayName { get; }

        /// <summary>ノードカテゴリ。色分けとメニュー分類に使用。</summary>
        public NodeCategory Category { get; }

        public NodeTypeInfo(string typeName, string displayName, NodeCategory category)
        {
            TypeName = typeName;
            DisplayName = displayName;
            Category = category;
        }
    }
}
