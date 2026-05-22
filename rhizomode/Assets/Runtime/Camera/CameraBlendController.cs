#nullable enable

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// シーン内の全 <see cref="CinemachineBrain"/> の DefaultBlend を一括制御する。
    /// CameraManagerPanel から選択された <see cref="CameraBlend"/> とブレンド時間を、
    /// 複数の Brain へ同時に反映する。Rector の <c>CameraManager</c> のブレンド制御部を移植。
    /// </summary>
    /// <remarks>
    /// rhizomode のシーンには Brain が複数ある (PreviewCamera / MirrorOutput)。
    /// どちらか一方だけ更新すると配信側とプレビュー側でブレンドが食い違うため、
    /// 起動時に <see cref="CinemachineBrain"/> を全列挙してまとめて書き込む。
    /// </remarks>
    public class CameraBlendController : MonoBehaviour
    {
        private const float DefaultBlendTime = 1f;

        private readonly List<CinemachineBrain> _brains = new();

        private CameraBlend _blend = CameraBlend.EaseInOut;
        private float _blendTime = DefaultBlendTime;

        /// <summary>現在選択中のブレンド形状。</summary>
        public CameraBlend Blend => _blend;

        /// <summary>現在のブレンド時間 (秒)。Cut の場合は無視される。</summary>
        public float BlendTime => _blendTime;

        private void Awake()
        {
            DiscoverBrains();
            Apply();
        }

        /// <summary>シーン内の全 Brain を再列挙する。グラフロード後など構成変化時に呼ぶ。</summary>
        public void DiscoverBrains()
        {
            _brains.Clear();
            var found = FindObjectsByType<CinemachineBrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _brains.AddRange(found);
        }

        /// <summary>ブレンド形状を切り替え、全 Brain に即時反映する。</summary>
        public void SetBlend(CameraBlend blend)
        {
            _blend = blend;
            Apply();
        }

        /// <summary>ブレンド時間 (秒) を更新し、全 Brain に即時反映する。負値は 0 にクランプ。</summary>
        public void SetBlendTime(float seconds)
        {
            // UI スライダー以外からの呼び出しで NaN/Infinity が Brain.DefaultBlend へ
            // 流れ込むのを防ぐ (defensive)。
            if (!float.IsFinite(seconds)) return;
            _blendTime = Mathf.Max(0f, seconds);
            Apply();
        }

        private void Apply()
        {
            var def = ToBlendDefinition(_blend, _blendTime);
            foreach (var brain in _brains)
            {
                if (brain == null) continue;
                brain.DefaultBlend = def;
            }
        }

        /// <summary>
        /// <see cref="CameraBlend"/> を Cinemachine の <see cref="CinemachineBlendDefinition"/> へ写像する。
        /// Cut はブレンド時間が無意味なため常に 0 を渡す。
        /// </summary>
        public static CinemachineBlendDefinition ToBlendDefinition(CameraBlend blend, float time)
        {
            return blend switch
            {
                CameraBlend.Cut => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f),
                CameraBlend.EaseInOut => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, time),
                CameraBlend.EaseIn => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseIn, time),
                CameraBlend.EaseOut => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseOut, time),
                CameraBlend.HardIn => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.HardIn, time),
                CameraBlend.HardOut => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.HardOut, time),
                CameraBlend.Linear => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Linear, time),
                _ => new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, time),
            };
        }
    }
}
