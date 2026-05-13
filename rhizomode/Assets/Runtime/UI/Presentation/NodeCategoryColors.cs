#nullable enable

using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// カテゴリごとの表示色を定義するScriptableObject。デザイナーがInspectorから色を調整可能。
    /// </summary>
    [CreateAssetMenu(menuName = "Rhizomode/NodeCategoryColors")]
    public class NodeCategoryColors : ScriptableObject
    {
        [Header("カテゴリカラー")]
        [SerializeField, ColorUsage(false), Tooltip("入力ノードの色")]
        private Color inputColor = new(0.2f, 0.4f, 0.9f);

        [SerializeField, ColorUsage(false), Tooltip("演算ノードの色")]
        private Color mathColor = new(0.2f, 0.75f, 0.3f);

        [SerializeField, ColorUsage(false), Tooltip("VFXノードの色")]
        private Color vfxColor = new(0.88f, 0.42f, 0.62f);

        [SerializeField, ColorUsage(false), Tooltip("シェーダーノードの色")]
        private Color shaderColor = new(0.6f, 0.3f, 0.8f);

        [SerializeField, ColorUsage(false), Tooltip("時間ノードの色")]
        private Color timeColor = new(0.9f, 0.8f, 0.2f);

        [SerializeField, ColorUsage(false), Tooltip("ユーティリティノードの色")]
        private Color utilityColor = new(0.5f, 0.5f, 0.5f);

        [SerializeField, ColorUsage(false), Tooltip("シーンノードの色")]
        private Color sceneColor = new(0.3f, 0.7f, 0.7f);

        /// <summary>
        /// カテゴリに対応する色を返す。
        /// </summary>
        public Color GetColor(NodeCategory category) => category switch
        {
            NodeCategory.Input => inputColor,
            NodeCategory.Math => mathColor,
            NodeCategory.VFX => vfxColor,
            NodeCategory.Shader => shaderColor,
            NodeCategory.Time => timeColor,
            NodeCategory.Utility => utilityColor,
            NodeCategory.Scene => sceneColor,
            _ => utilityColor
        };
    }
}
