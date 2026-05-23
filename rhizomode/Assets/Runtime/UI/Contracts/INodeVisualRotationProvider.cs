#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// ノード visual の世界回転を id 経由で取得するための contract。
    /// </summary>
    /// <remarks>
    /// cue 復元時にノードの表裏が反転する bug 修正のため、save 時に visual の rotation を
    /// <c>NodeData.rotation</c> へ書き出すための adapter 境界。Save 側は <see cref="TryGetRotation"/>
    /// で visual から rotation を吸い上げ、Load 側は <c>NodeData.rotation</c> を
    /// <c>GraphLoadCoordinator.Rebuild</c> 経由で visual transform に適用する。
    ///
    /// 実装は <c>NodeVisualManager</c> (UI.Presentation asmdef) が担う — visual transform を
    /// 内部 dictionary で保持しているため、id → rotation の resolve は O(1)。
    /// 本 contract は UI.Contracts に置き、UI.GraphAdapter (Save/Load facade) が UI.Presentation
    /// に直接依存することなく rotation を取得できるようにする。
    /// </remarks>
    public interface INodeVisualRotationProvider
    {
        /// <summary>指定ノードの visual rotation を取得する。visual 未存在なら false。</summary>
        bool TryGetRotation(string nodeId, out Quaternion rotation);
    }
}
