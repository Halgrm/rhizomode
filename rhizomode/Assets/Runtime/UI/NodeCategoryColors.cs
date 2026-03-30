#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// カテゴリごとの表示色を定義する。
    /// </summary>
    public static class NodeCategoryColors
    {
        public static readonly Color Input = new(0.2f, 0.4f, 0.9f);
        public static readonly Color Math = new(0.2f, 0.75f, 0.3f);
        public static readonly Color Module = new(0.6f, 0.3f, 0.8f);
        public static readonly Color Time = new(0.9f, 0.8f, 0.2f);
        public static readonly Color Utility = new(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// カテゴリに対応する色を返す。
        /// </summary>
        public static Color GetColor(NodeCategory category) => category switch
        {
            NodeCategory.Input => Input,
            NodeCategory.Math => Math,
            NodeCategory.Module => Module,
            NodeCategory.Time => Time,
            NodeCategory.Utility => Utility,
            _ => Utility
        };
    }
}
