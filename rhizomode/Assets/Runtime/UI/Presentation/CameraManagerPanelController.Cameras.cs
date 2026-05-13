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
    /// FOV+Dutch+LookAt の callback。
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

            var pathCam = cam.GetComponent<PathCameraController>();
            if (pathCam != null)
            {
                if (_progressRow != null) _progressRow.style.display = DisplayStyle.Flex;
                if (_editRow != null) _editRow.style.display = DisplayStyle.Flex;
                RefreshFloatOutputs();
                if (_progressDropdown != null)
                {
                    var labels = new List<string> { NoSourceLabel };
                    foreach (var p in _floatOutputs) labels.Add(p.DisplayName);
                    _progressDropdown.choices = labels;
                    _progressDropdown.SetValueWithoutNotify(NoSourceLabel);
                }
                // 既存の Source 購読を解除し、Slider を現在値で初期化
                _progressSubscription?.Dispose();
                _progressSubscription = null;
                if (_progressSlider != null) _progressSlider.SetValueWithoutNotify(pathCam.Progress);
                if (_progressValue != null) _progressValue.text = $"{pathCam.Progress:F2}";
                // 別カメラを選んだら編集モードは強制終了
                if (_editPathToggle != null && _editPathToggle.value)
                {
                    _editPathToggle.SetValueWithoutNotify(false);
                    StopEditing();
                }
            }
            else
            {
                if (_progressRow != null) _progressRow.style.display = DisplayStyle.None;
                if (_editRow != null) _editRow.style.display = DisplayStyle.None;
                if (_editPathToggle != null && _editPathToggle.value)
                {
                    _editPathToggle.SetValueWithoutNotify(false);
                    StopEditing();
                }
            }
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
    }
}
