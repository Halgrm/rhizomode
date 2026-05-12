#nullable enable

using UnityEngine;

namespace Rhizomode.Input.Contracts
{
    /// <summary>
    /// コントローラーのフル姿勢（位置＋回転）を提供する。
    /// IRayProviderにRotationを追加するとbreaking changeになるため、別インターフェースとして定義。
    /// </summary>
    public interface IControllerPose : IRayProvider
    {
        /// <summary>右手コントローラーの回転。</summary>
        Quaternion ControllerRotation { get; }
    }
}
