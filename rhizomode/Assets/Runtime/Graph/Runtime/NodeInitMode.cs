#nullable enable

namespace Rhizomode.Graph.Runtime
{
    /// <summary>
    /// ノード初期化の文脈。<see cref="INodeLifecycleProcessor"/> が default 値適用判断などに使う。
    /// </summary>
    /// <remarks>
    /// Plan v5.3-2: <c>NodeDefaultLifecycleProcessor</c> は <see cref="FreshSpawn"/> の時のみ
    /// default 値を適用する (Deserialize 後の上書き防止)。
    /// </remarks>
    public enum NodeInitMode
    {
        /// <summary>新規スポーン (Scroll Menu / Preset Spawn)。default 値を適用してよい。</summary>
        FreshSpawn,

        /// <summary>JSON Deserialize 復元。default 値で saved value を上書きしない。</summary>
        Deserialize,

        /// <summary>Preset import (保存形式マージ)。default 値は適用しない。</summary>
        PresetImport,

        /// <summary>Undo/Redo 復元。default 値は適用しない。</summary>
        UndoRedo
    }
}
