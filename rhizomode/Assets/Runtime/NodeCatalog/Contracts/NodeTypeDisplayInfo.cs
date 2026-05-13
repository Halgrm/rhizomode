#nullable enable

namespace Rhizomode.NodeCatalog.Contracts
{
    /// <summary>
    /// ノードタイプの表示メタデータ (UnityEngine 非依存)。
    /// </summary>
    /// <remarks>
    /// Plan v5.0-A: <c>UnityEngine.Color</c> / <c>UnityEngine.Sprite</c> をここに含めない。
    /// 実体解決は UI.Presentation 側の <c>NodeCategoryPalette</c> / <c>NodeIconCatalog</c> SO で行う。
    ///
    /// <see cref="ColorKey"/> / <see cref="IconId"/> は文字列識別子 (palette/catalog SO のキー)。
    /// <see cref="Description"/> は UI tooltip 等で表示。
    ///
    /// Phase 4 で <see cref="NodeTypeAttribute"/> + display override SO のマージ結果として構築される。
    /// </remarks>
    public sealed class NodeTypeDisplayInfo
    {
        public string TypeName { get; }
        public string Label { get; }
        public string Description { get; }
        public NodeCategory Category { get; }
        public string ColorKey { get; }
        public string IconId { get; }

        public NodeTypeDisplayInfo(
            string typeName,
            string label,
            string description,
            NodeCategory category,
            string colorKey,
            string iconId)
        {
            TypeName = typeName;
            Label = label;
            Description = description;
            Category = category;
            ColorKey = colorKey;
            IconId = iconId;
        }
    }
}
