#nullable enable

using System;
using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Modules
{
    /// <summary>
    /// <see cref="IPerformanceModule"/> 実装の data-driven 登録メタデータ。
    /// </summary>
    /// <remarks>
    /// <see cref="Bootstrap.NodeRegistrationOrchestrator"/> が reflection で
    /// 本属性を読み取り、ScrollMenu のカテゴリ・legacy saved graph alias・特殊 ModuleNode 派生型を
    /// 決定する。新 module 種別の追加は本属性付与のみで完結し、Orchestrator は触らずに済む。
    ///
    /// <para>
    /// 属性が未付与の IPerformanceModule 実装は <see cref="Category"/>=<see cref="NodeCategory.VFX"/>
    /// 既定 + alias なし + 汎用 ModuleNode で登録される (degraded fallback)。
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PerformanceModuleAttribute : Attribute
    {
        /// <summary>ScrollMenu 等で表示するカテゴリ。</summary>
        public NodeCategory Category { get; }

        /// <summary>
        /// 旧 saved graph 互換用の typeName prefix (例: "VFX_" / "Shader_")。
        /// null/empty なら legacy alias を登録しない (新規追加 module 推奨)。
        /// </summary>
        public string? LegacyTypeNamePrefix { get; }

        /// <summary>
        /// 特殊な追加ポートが必要な module 用の <c>ModuleNodeBase</c> 派生型。
        /// null なら汎用 <see cref="Nodes.Modules.ModuleNode"/> が使われる。
        /// </summary>
        public Type? CustomNodeType { get; }

        public PerformanceModuleAttribute(
            NodeCategory category,
            string? legacyTypeNamePrefix = null,
            Type? customNodeType = null)
        {
            Category = category;
            LegacyTypeNamePrefix = legacyTypeNamePrefix;
            CustomNodeType = customNodeType;
        }
    }
}
