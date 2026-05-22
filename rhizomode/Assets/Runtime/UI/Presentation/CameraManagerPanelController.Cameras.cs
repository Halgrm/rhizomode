#nullable enable

using System.Collections.Generic;
using Rhizomode.Cameras;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="CameraManagerPanelController"/> の partial: カメラ列挙 / 選択 / Live priority /
    /// FOV+Dutch+LookAt+Follow / Follow オフセット・Noise・Wander プロパティ行の callback。
    /// Phase 9 Round C で本体から分離。
    /// </summary>
    public partial class CameraManagerPanelController
    {
        private void DiscoverCameras()
        {
            _cameras.Clear();
            var found = FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cameras.AddRange(found);
        }

        private void RefreshCameraList()
        {
            if (_list == null) return;
            _list.Clear();
            _cameraButtons.Clear();

            foreach (var cam in _cameras)
            {
                if (cam == null) continue;
                var captured = cam;
                var button = new Button(() => HandleCameraClicked(captured))
                {
                    text = cam.name
                };
                button.AddToClassList("camera-button");
                _cameraButtons.Add(button);
                _list.Add(button);
            }
            UpdateLiveHighlights();
        }

        private void HandleCameraClicked(CinemachineCamera cam)
        {
            _selected = cam;
            foreach (var c in _cameras)
            {
                if (c == null) continue;
                c.Priority = (c == cam) ? LivePriority : DormantPriority;
            }
            UpdateLiveHighlights();
            ShowDetails(cam);
        }

        private void UpdateLiveHighlights()
        {
            for (int i = 0; i < _cameraButtons.Count && i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null) continue;
                bool isLive = cam.Priority.Value >= LivePriority;
                if (isLive) _cameraButtons[i].AddToClassList("camera-button--live");
                else _cameraButtons[i].RemoveFromClassList("camera-button--live");
            }
        }

        private void ShowDetails(CinemachineCamera cam)
        {
            if (_details == null) return;
            _details.style.display = DisplayStyle.Flex;
            if (_detailsTitle != null) _detailsTitle.text = cam.name;

            var lens = cam.Lens;
            _fovSlider?.SetValueWithoutNotify(lens.FieldOfView);
            if (_fovValue != null) _fovValue.text = $"{lens.FieldOfView:F0}";
            _dutchSlider?.SetValueWithoutNotify(lens.Dutch);
            if (_dutchValue != null) _dutchValue.text = $"{lens.Dutch:F0}";

            RefreshLookAtDropdown(cam);
            RefreshFollowRow(cam);
            ShowPropertyRows(cam);
            ShowMotionRow(cam);
            ShowEditRow(cam.GetComponent<PathCameraController>());
        }

        /// <summary>
        /// <see cref="ICameraMotion"/> を持つカメラに「Motion 駆動」行 (Source dropdown + Slider) を出す。
        /// ラベルは <c>MotionLabel</c> に従い動的に切替える (Path なら "Progress"、Orbital なら "Orbit")。
        /// </summary>
        /// <remarks>
        /// F-P4-001: 通常のカメラ選択でも <see cref="CameraMotionSourceBinding"/> を読み、
        /// 保存済みソースがあれば購読を貼り直す (binding を Motion ソースの権威とする)。
        /// </remarks>
        private void ShowMotionRow(CinemachineCamera cam)
        {
            // カメラ切替のたび、旧カメラ向けの Source 購読は必ず解除する
            _progressSubscription?.Dispose();
            _progressSubscription = null;

            var motion = cam.GetComponent<ICameraMotion>();
            if (motion == null)
            {
                if (_progressRow != null) _progressRow.style.display = DisplayStyle.None;
                return;
            }

            if (_progressRow != null) _progressRow.style.display = DisplayStyle.Flex;
            if (_progressSrcLabel != null) _progressSrcLabel.text = $"{motion.MotionLabel} src";
            if (_progressLabel != null) _progressLabel.text = motion.MotionLabel;

            PopulateMotionSourceDropdown();
            BindMotionRowToSavedSource(motion, cam.GetComponent<CameraMotionSourceBinding>());

            if (_progressSlider != null) _progressSlider.SetValueWithoutNotify(motion.Drive);
            if (_progressValue != null) _progressValue.text = $"{motion.Drive:F2}";
        }

        /// <summary>
        /// Edit Path 行は <see cref="PathCameraController"/> を持つカメラ (= spline path) のみ表示する。
        /// カメラ切替時は編集モードを強制終了する。
        /// </summary>
        private void ShowEditRow(PathCameraController? pathCam)
        {
            if (_editRow != null)
                _editRow.style.display = pathCam != null ? DisplayStyle.Flex : DisplayStyle.None;

            if (_editPathToggle != null && _editPathToggle.value)
            {
                _editPathToggle.SetValueWithoutNotify(false);
                StopEditing();
            }
        }

        /// <summary>
        /// Follow body (<see cref="CinemachineFollow"/> / <see cref="CinemachineOrbitalFollow"/>) を持つ
        /// カメラにのみ Follow ターゲット dropdown を出す。
        /// </summary>
        private void RefreshFollowRow(CinemachineCamera cam)
        {
            bool hasFollowBody = cam.GetComponent<CinemachineFollow>() != null
                                 || cam.GetComponent<CinemachineOrbitalFollow>() != null;
            if (_followRow != null)
                _followRow.style.display = hasFollowBody ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasFollowBody)
                RefreshFollowDropdown(cam);
        }

        private void HideDetails()
        {
            if (_details != null) _details.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// 選択中カメラの LookAt フィールドの現状に合わせて Look At dropdown を再構築する。
        /// </summary>
        private void RefreshLookAtDropdown(CinemachineCamera cam)
        {
            if (_lookAtDropdown == null) return;

            var labels = new List<string> { NoLookAtLabel };
            foreach (var t in LookAtTargetMarker.AllTargets)
            {
                if (t == null) continue;
                labels.Add(t.DisplayName);
            }
            _lookAtDropdown.choices = labels;

            // 現在の LookAt 設定を反映
            string current = NoLookAtLabel;
            if (cam.LookAt != null)
            {
                var marker = cam.LookAt.GetComponent<LookAtTargetMarker>();
                if (marker != null && labels.Contains(marker.DisplayName))
                    current = marker.DisplayName;
            }
            _lookAtDropdown.SetValueWithoutNotify(current);
        }

        private void OnFovChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var lens = _selected.Lens;
            lens.FieldOfView = Mathf.Clamp(e.newValue, 1f, 179f);
            _selected.Lens = lens;
            if (_fovValue != null) _fovValue.text = $"{lens.FieldOfView:F0}";
        }

        private void OnDutchChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var lens = _selected.Lens;
            lens.Dutch = e.newValue;
            _selected.Lens = lens;
            if (_dutchValue != null) _dutchValue.text = $"{lens.Dutch:F0}";
        }

        private void OnLookAtChanged(ChangeEvent<string> e)
        {
            if (_selected == null) return;
            if (string.IsNullOrEmpty(e.newValue) || e.newValue == NoLookAtLabel)
            {
                _selected.LookAt = null;
                return;
            }
            foreach (var t in LookAtTargetMarker.AllTargets)
            {
                if (t == null) continue;
                if (t.DisplayName == e.newValue)
                {
                    _selected.LookAt = t.transform;
                    return;
                }
            }
        }

        /// <summary>
        /// 選択中カメラの Follow フィールドの現状に合わせて Follow dropdown を再構築する。
        /// ターゲット候補は LookAt と同じ <see cref="LookAtTargetMarker"/> registry を共有する。
        /// </summary>
        private void RefreshFollowDropdown(CinemachineCamera cam)
        {
            if (_followDropdown == null) return;

            var labels = new List<string> { NoLookAtLabel };
            foreach (var t in LookAtTargetMarker.AllTargets)
            {
                if (t == null) continue;
                labels.Add(t.DisplayName);
            }
            _followDropdown.choices = labels;

            string current = NoLookAtLabel;
            if (cam.Follow != null)
            {
                var marker = cam.Follow.GetComponent<LookAtTargetMarker>();
                if (marker != null && labels.Contains(marker.DisplayName))
                    current = marker.DisplayName;
            }
            _followDropdown.SetValueWithoutNotify(current);
        }

        private void OnFollowChanged(ChangeEvent<string> e)
        {
            if (_selected == null) return;
            if (string.IsNullOrEmpty(e.newValue) || e.newValue == NoLookAtLabel)
            {
                _selected.Follow = null;
                return;
            }
            foreach (var t in LookAtTargetMarker.AllTargets)
            {
                if (t == null) continue;
                if (t.DisplayName == e.newValue)
                {
                    _selected.Follow = t.transform;
                    return;
                }
            }
        }

        /// <summary>
        /// グラフロード (キュー呼び出し) 後に呼ばれ、カメラ一覧の再列挙・ライブカメラの再選択・
        /// Motion 購読の貼り直しを行う。<see cref="GraphSaveLoadManager.OnGraphLoaded"/> に
        /// VerticalSliceBootstrapWiring が購読する。
        /// </summary>
        /// <remarks>
        /// ロードでグラフが Clear→再構築され旧 OutputPort が失効するため、
        /// 復元済みの <see cref="CameraMotionSourceBinding"/> を読み直して購読を再生成する必要がある。
        /// </remarks>
        public void OnCameraStateRestored()
        {
            if (!_initialized) return;

            DiscoverCameras();
            RefreshCameraList();

            var live = HighestPriorityCamera();
            if (live == null)
            {
                HideDetails();
                return;
            }

            _selected = live;
            // ShowDetails → ShowMotionRow 内で CameraMotionSourceBinding から購読を貼り直す。
            ShowDetails(live);
        }

        /// <summary>列挙済みカメラのうち Priority が最大のものを返す。空なら null。</summary>
        private CinemachineCamera? HighestPriorityCamera()
        {
            CinemachineCamera? best = null;
            int bestPriority = int.MinValue;
            foreach (var cam in _cameras)
            {
                if (cam == null) continue;
                if (cam.Priority.Value > bestPriority)
                {
                    bestPriority = cam.Priority.Value;
                    best = cam;
                }
            }
            return best;
        }

        /// <summary>選択カメラの構成に応じて Follow オフセット / ノイズ / Wander / Velocity FOV 行を出し分ける。</summary>
        private void ShowPropertyRows(CinemachineCamera cam)
        {
            ShowFollowOffsetRow(cam.GetComponent<CinemachineFollow>());
            ShowNoiseRow(cam.GetComponent<CinemachineBasicMultiChannelPerlin>());
            ShowWanderRow(cam.GetComponent<CinemachineRandomSphereTarget>());
            ShowVelocityFovRow(cam.GetComponent<CinemachineVelocityFov>());
        }

        /// <summary>CinemachineFollow を持つカメラに Follow オフセット (XYZ) 行を出す。</summary>
        private void ShowFollowOffsetRow(CinemachineFollow? follow)
        {
            bool show = follow != null;
            if (_followOffsetRow != null)
                _followOffsetRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            var offset = follow!.FollowOffset;
            SetSlider(_followOffsetX, _followOffsetXValue, offset.x);
            SetSlider(_followOffsetY, _followOffsetYValue, offset.y);
            SetSlider(_followOffsetZ, _followOffsetZValue, offset.z);
        }

        /// <summary>CinemachineBasicMultiChannelPerlin を持つカメラにノイズ振幅 / 周波数行を出す。</summary>
        private void ShowNoiseRow(CinemachineBasicMultiChannelPerlin? perlin)
        {
            bool show = perlin != null;
            if (_noiseRow != null)
                _noiseRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            SetSlider(_noiseAmpSlider, _noiseAmpValue, perlin!.AmplitudeGain);
            SetSlider(_noiseFreqSlider, _noiseFreqValue, perlin.FrequencyGain);
        }

        /// <summary>CinemachineRandomSphereTarget を持つカメラに speed/radius/period 行を出す。</summary>
        private void ShowWanderRow(CinemachineRandomSphereTarget? wander)
        {
            bool show = wander != null;
            if (_wanderRow != null)
                _wanderRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            SetSlider(_wanderSpeedSlider, _wanderSpeedValue, wander!.Speed);
            SetSlider(_wanderRadiusSlider, _wanderRadiusValue, wander.Radius);
            SetSlider(_wanderPeriodSlider, _wanderPeriodValue, wander.Period);
        }

        /// <summary>CinemachineVelocityFov を持つカメラに min/max FOV と速度レンジ行を出す。</summary>
        private void ShowVelocityFovRow(CinemachineVelocityFov? velFov)
        {
            bool show = velFov != null;
            if (_velocityFovRow != null)
                _velocityFovRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            SetSlider(_velFovMinSlider, _velFovMinValue, velFov!.MinFov);
            SetSlider(_velFovMaxSlider, _velFovMaxValue, velFov.MaxFov);
            SetSlider(_velFovMaxVelSlider, _velFovMaxVelValue, velFov.MaxVelocity);
        }

        /// <summary>スライダーと値ラベルを通知なしで現在値に同期する。</summary>
        private static void SetSlider(Slider? slider, Label? valueLabel, float value)
        {
            slider?.SetValueWithoutNotify(value);
            if (valueLabel != null) valueLabel.text = value.ToString("F2");
        }

        private void OnFollowOffsetChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var follow = _selected.GetComponent<CinemachineFollow>();
            if (follow == null) return;

            var offset = new Vector3(
                _followOffsetX?.value ?? 0f,
                _followOffsetY?.value ?? 0f,
                _followOffsetZ?.value ?? 0f);
            follow.FollowOffset = offset;

            if (_followOffsetXValue != null) _followOffsetXValue.text = offset.x.ToString("F2");
            if (_followOffsetYValue != null) _followOffsetYValue.text = offset.y.ToString("F2");
            if (_followOffsetZValue != null) _followOffsetZValue.text = offset.z.ToString("F2");
        }

        private void OnNoiseAmpChanged(ChangeEvent<float> e)
        {
            var perlin = _selected != null
                ? _selected.GetComponent<CinemachineBasicMultiChannelPerlin>()
                : null;
            if (perlin == null) return;
            perlin.AmplitudeGain = e.newValue;
            if (_noiseAmpValue != null) _noiseAmpValue.text = e.newValue.ToString("F2");
        }

        private void OnNoiseFreqChanged(ChangeEvent<float> e)
        {
            var perlin = _selected != null
                ? _selected.GetComponent<CinemachineBasicMultiChannelPerlin>()
                : null;
            if (perlin == null) return;
            perlin.FrequencyGain = e.newValue;
            if (_noiseFreqValue != null) _noiseFreqValue.text = e.newValue.ToString("F2");
        }

        private void OnWanderSpeedChanged(ChangeEvent<float> e)
        {
            var wander = _selected != null
                ? _selected.GetComponent<CinemachineRandomSphereTarget>()
                : null;
            if (wander == null) return;
            wander.Speed = e.newValue;
            if (_wanderSpeedValue != null) _wanderSpeedValue.text = e.newValue.ToString("F2");
        }

        private void OnWanderRadiusChanged(ChangeEvent<float> e)
        {
            var wander = _selected != null
                ? _selected.GetComponent<CinemachineRandomSphereTarget>()
                : null;
            if (wander == null) return;
            wander.Radius = e.newValue;
            if (_wanderRadiusValue != null) _wanderRadiusValue.text = e.newValue.ToString("F2");
        }

        private void OnWanderPeriodChanged(ChangeEvent<float> e)
        {
            var wander = _selected != null
                ? _selected.GetComponent<CinemachineRandomSphereTarget>()
                : null;
            if (wander == null) return;
            wander.Period = e.newValue;
            if (_wanderPeriodValue != null) _wanderPeriodValue.text = e.newValue.ToString("F2");
        }

        private void OnVelFovMinChanged(ChangeEvent<float> e)
        {
            var velFov = _selected != null
                ? _selected.GetComponent<CinemachineVelocityFov>()
                : null;
            if (velFov == null) return;
            velFov.MinFov = e.newValue;
            if (_velFovMinValue != null) _velFovMinValue.text = e.newValue.ToString("F2");
        }

        private void OnVelFovMaxChanged(ChangeEvent<float> e)
        {
            var velFov = _selected != null
                ? _selected.GetComponent<CinemachineVelocityFov>()
                : null;
            if (velFov == null) return;
            velFov.MaxFov = e.newValue;
            if (_velFovMaxValue != null) _velFovMaxValue.text = e.newValue.ToString("F2");
        }

        private void OnVelFovMaxVelChanged(ChangeEvent<float> e)
        {
            var velFov = _selected != null
                ? _selected.GetComponent<CinemachineVelocityFov>()
                : null;
            if (velFov == null) return;
            velFov.MaxVelocity = e.newValue;
            if (_velFovMaxVelValue != null) _velFovMaxVelValue.text = e.newValue.ToString("F2");
        }
    }
}
