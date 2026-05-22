#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Cameras;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="CameraManagerPanelController"/> の partial: カメラ切替ブレンド制御。
    /// Rector の <c>CameraManager</c> から移植したブレンド形状 (<see cref="CameraBlend"/>) と
    /// ブレンド時間を、<see cref="CameraBlendController"/> 経由で全 CinemachineBrain に一括反映する。
    /// ブレンドはシーン全体で 1 つの設定のためカメラ選択とは独立した常設行とする。
    /// </summary>
    public partial class CameraManagerPanelController
    {
        /// <summary>Blend 形状 dropdown を enum 値で初期化し、現在値に同期する。</summary>
        private void InitBlendControls()
        {
            if (_blendController == null) return;

            if (_blendStyleDropdown != null)
            {
                _blendStyleDropdown.choices = new List<string>(Enum.GetNames(typeof(CameraBlend)));
                _blendStyleDropdown.SetValueWithoutNotify(_blendController.Blend.ToString());
            }

            _blendTimeSlider?.SetValueWithoutNotify(_blendController.BlendTime);
            if (_blendTimeValue != null)
                _blendTimeValue.text = _blendController.BlendTime.ToString("F2");
        }

        private void OnBlendStyleChanged(ChangeEvent<string> e)
        {
            if (_blendController == null) return;
            if (Enum.TryParse<CameraBlend>(e.newValue, out var blend))
                _blendController.SetBlend(blend);
        }

        private void OnBlendTimeChanged(ChangeEvent<float> e)
        {
            _blendController?.SetBlendTime(e.newValue);
            if (_blendTimeValue != null) _blendTimeValue.text = e.newValue.ToString("F2");
        }
    }
}
