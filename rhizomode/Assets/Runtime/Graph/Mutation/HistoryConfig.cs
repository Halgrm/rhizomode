#nullable enable

using UnityEngine;

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// Undo/Redo 履歴設定 (ScriptableObject)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: <see cref="GraphCommandDispatcher"/> の max history size をライブ調整可能にする。
    /// 設定値は SO (Live 調整値) として <c>Assets/Data/Config/HistoryConfig.asset</c> に置く (Plan v5.3-1)。
    /// </remarks>
    [CreateAssetMenu(fileName = "HistoryConfig", menuName = "Rhizomode/Graph/HistoryConfig")]
    public sealed class HistoryConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Undo stack の最大サイズ。これを超えると最古から捨てられる。")]
        private int maxHistorySize = 64;

        public int MaxHistorySize => Mathf.Max(1, maxHistorySize);
    }
}
