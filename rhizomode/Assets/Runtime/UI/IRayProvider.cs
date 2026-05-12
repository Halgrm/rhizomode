#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// レイキャスト用のレイ情報を提供するインターフェース。
    /// IControllerInputとは独立して定義（breaking change回避）。
    /// </summary>
    public interface IRayProvider
    {
        /// <summary>レイの始点（右手コントローラー位置）。</summary>
        Vector3 RayOrigin { get; }

        /// <summary>レイの方向（右手コントローラー前方）。</summary>
        Vector3 RayDirection { get; }
    }
}
