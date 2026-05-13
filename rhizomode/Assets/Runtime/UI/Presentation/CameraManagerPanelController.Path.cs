#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Cameras;
using Rhizomode.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="CameraManagerPanelController"/> の partial: PathCameraController 連動
    /// (Progress source dropdown / Slider / Edit toggle)。
    /// Phase 9 Round C で本体から分離。
    /// Round E (E5) で <see cref="IFloatOutputCatalog"/> 経由に切替、Graph.Model 直依存撤廃。
    /// </summary>
    public partial class CameraManagerPanelController
    {
        /// <summary>
        /// "Edit Path" トグルの状態変化を受け取り他ハンドラに通知する。
        /// 受信側 (GameBootstrap) は EdgeDragHandler/EdgeCutHandler/NodeDeleteHandler を一時停止させる。
        /// </summary>
        public void AddEditModeListener(Action<bool> listener)
        {
            _editModeListeners.Add(listener);
        }

        private void OnEditPathToggleChanged(ChangeEvent<bool> e)
        {
            if (e.newValue) StartEditing();
            else StopEditing();
        }

        private void StartEditing()
        {
            if (_selected == null || pathEditorManager == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null) return;
            pathEditorManager.BeginEdit(pathCam);
            NotifyEditMode(true);
        }

        private void StopEditing()
        {
            if (pathEditorManager == null) return;
            pathEditorManager.EndEdit();
            NotifyEditMode(false);
        }

        private void NotifyEditMode(bool isEditing)
        {
            foreach (var listener in _editModeListeners) listener?.Invoke(isEditing);
        }

        private void RefreshFloatOutputs()
        {
            _floatOutputs.Clear();
            if (_floatOutputCatalog == null) return;
            foreach (var entry in _floatOutputCatalog.GetFloatOutputs())
                _floatOutputs.Add(entry);
        }

        private void OnProgressSourceChanged(ChangeEvent<string> e)
        {
            _progressSubscription?.Dispose();
            _progressSubscription = null;

            if (_selected == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null) return;
            if (string.IsNullOrEmpty(e.newValue) || e.newValue == NoSourceLabel) return;
            if (_floatOutputCatalog == null) return;

            FloatOutputRef? target = null;
            foreach (var p in _floatOutputs)
            {
                if (p.DisplayName != e.newValue) continue;
                target = p;
                break;
            }
            if (target == null) return;

            _progressSubscription = _floatOutputCatalog.Subscribe(
                target.Value.NodeId,
                target.Value.PortName,
                v =>
                {
                    pathCam.SetProgress(v);
                    // スライダーも追従させて現在値を可視化
                    if (_progressSlider != null) _progressSlider.SetValueWithoutNotify(Mathf.Clamp01(v));
                    if (_progressValue != null) _progressValue.text = $"{v:F2}";
                });
        }

        /// <summary>
        /// Progress スライダーで手動駆動する。Source が選ばれていてもスライダー操作は通る (一時オーバーライド)。
        /// </summary>
        private void OnProgressSliderChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null) return;
            pathCam.SetProgress(e.newValue);
            if (_progressValue != null) _progressValue.text = $"{e.newValue:F2}";
        }

        /// <summary>
        /// Dropdown を最新の Float 出力ポート一覧で再構築する。
        /// 編集モード後に LFO を追加したり等で必要。
        /// </summary>
        private void OnProgressRefreshClicked()
        {
            if (_selected == null) return;
            var pathCam = _selected.GetComponent<PathCameraController>();
            if (pathCam == null || _progressDropdown == null) return;

            RefreshFloatOutputs();
            var labels = new List<string> { NoSourceLabel };
            foreach (var p in _floatOutputs) labels.Add(p.DisplayName);
            _progressDropdown.choices = labels;

            // 現在選択が一覧から消えていたら (none) に戻す
            if (!labels.Contains(_progressDropdown.value))
            {
                _progressDropdown.SetValueWithoutNotify(NoSourceLabel);
                _progressSubscription?.Dispose();
                _progressSubscription = null;
            }
        }
    }
}
