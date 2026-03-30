#nullable enable

using R3;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// コントローラー入力のインターフェース。XR層が実装し、UI層が消費する。
    /// </summary>
    public interface IControllerInput
    {
        /// <summary>ノード生成メニュー呼び出し（Left X）。</summary>
        Observable<Unit> OnOpenMenu { get; }

        /// <summary>ノード削除（Right A）。</summary>
        Observable<Unit> OnDeleteNode { get; }

        /// <summary>エッジ切断（Right B）。</summary>
        Observable<Unit> OnCutEdge { get; }

        /// <summary>レイ選択 / エッジドラッグ / UI操作（Right Trigger）。</summary>
        Observable<bool> OnSelect { get; }

        /// <summary>ノードのグラブ移動（Right Grip）。</summary>
        Observable<bool> OnGrab { get; }

        /// <summary>HMDの現在位置。</summary>
        Vector3 HeadPosition { get; }

        /// <summary>HMDの現在回転。</summary>
        Quaternion HeadRotation { get; }

        /// <summary>HMDの前方ベクトル。</summary>
        Vector3 HeadForward { get; }
    }
}
