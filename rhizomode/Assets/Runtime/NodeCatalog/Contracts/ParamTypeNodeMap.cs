#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.NodeCatalog.Contracts
{
    /// <summary>
    /// ParamType → source ノード spawn 情報 (typeName / output port / optional trigger source) の解決 helper。
    /// </summary>
    /// <remarks>
    /// F-Vf-d.1 で導入、F-Vf-d.2 で <see cref="SourceNodeDescriptor"/> 化。
    /// NodeSpawnService が auto-spawn する source ノードの typeName + output port + Toggle 用 trigger source を
    /// 一括で解決するため、"Toggle" / "Trigger" / "Value" / "State" 文字列の重複定義を解消した。
    /// Interaction.GraphAdapter は Nodes.Standard 具体型を knowing せず、descriptor 経由で AddNodeCommand を
    /// 発行する設計。
    /// </remarks>
    public static class ParamTypeNodeMap
    {
        /// <summary>
        /// 指定 <see cref="ParamType"/> の入力ポートに繋ぐ source ノード descriptor を返す。
        /// </summary>
        /// <returns>descriptor。サポート外の型は null。</returns>
        public static SourceNodeDescriptor? GetSourceDescriptor(ParamType type) => type switch
        {
            ParamType.Float => new SourceNodeDescriptor("ConstFloat", "Value"),
            ParamType.Color => new SourceNodeDescriptor("ConstColor", "Value"),
            ParamType.Bool => new SourceNodeDescriptor(
                "Toggle", "State",
                triggerSourceTypeName: "Trigger",
                triggerOutputPortName: "Trigger",
                triggerTargetPortName: "Trigger"),
            _ => null
        };
    }

    /// <summary>
    /// auto-spawn される source ノードの shape (typeName + output port + optional trigger source)。
    /// </summary>
    public sealed class SourceNodeDescriptor
    {
        /// <summary>spawn する source ノードの typeName (e.g., "ConstFloat" / "Toggle")。</summary>
        public string TypeName { get; }

        /// <summary>source ノードの出力ポート名 (target ポートと TryConnect する側)。</summary>
        public string OutputPortName { get; }

        /// <summary>source が Toggle のとき、Trigger ポートに繋ぐ追加 source ノードの typeName。それ以外は null。</summary>
        public string? TriggerSourceTypeName { get; }

        /// <summary>追加 trigger source の出力ポート名。<see cref="TriggerSourceTypeName"/> が null のとき null。</summary>
        public string? TriggerOutputPortName { get; }

        /// <summary>source ノード側の Trigger 入力ポート名 (e.g., Toggle の "Trigger")。</summary>
        public string? TriggerTargetPortName { get; }

        public SourceNodeDescriptor(
            string typeName,
            string outputPortName,
            string? triggerSourceTypeName = null,
            string? triggerOutputPortName = null,
            string? triggerTargetPortName = null)
        {
            TypeName = typeName;
            OutputPortName = outputPortName;
            TriggerSourceTypeName = triggerSourceTypeName;
            TriggerOutputPortName = triggerOutputPortName;
            TriggerTargetPortName = triggerTargetPortName;
        }
    }
}
