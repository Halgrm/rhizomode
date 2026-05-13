#nullable enable

using System;

namespace Rhizomode.NodeCatalog.Contracts
{
    /// <summary>
    /// ノードクラスに付与する type registration マーカー (Plan v5.3-2: 最小 3 引数)。
    /// </summary>
    /// <remarks>
    /// NodeTypeAttributeScanner (NodeCatalog.Runtime) がアセンブリを reflection で走査し、
    /// 本属性が付いた NodeBase 派生クラスを <see cref="NodeTypeRegistration"/> に変換する。
    ///
    /// 表示情報 (Description / ColorKey / IconId) は本属性に含めない (Plan v5.2-3)。
    /// 表示 override は <c>NodeTypeDisplayOverridesAsset</c> SO で管理し、Scanner がマージする
    /// (Phase 4 の SO 実装は後続作業に延期)。
    ///
    /// 使用例:
    /// <code>
    /// [NodeType("ConstFloat", "Const Float", NodeCategory.Input)]
    /// public sealed class ConstFloatNode : NodeBase { ... }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NodeTypeAttribute : Attribute
    {
        public string TypeName { get; }
        public string Label { get; }
        public NodeCategory Category { get; }

        public NodeTypeAttribute(string typeName, string label, NodeCategory category)
        {
            TypeName = typeName;
            Label = label;
            Category = category;
        }
    }
}
