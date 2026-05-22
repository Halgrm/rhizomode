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

        /// <summary>
        /// <see cref="AddEditModeListener"/> で登録した listener を解除する (F-Vf-c.1)。
        /// VerticalSliceBootstrapWiring など container 所有 disposable の Dispose から呼ぶ。
        /// </summary>
        public void RemoveEditModeListener(Action<bool> listener)
        {
            _editModeListeners.Remove(listener);
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

        /// <summary>
        /// F1 fix (Codex review FAIL): edit mode の参照カウントを取り、複数 source (Edit Path /
        /// Edit LookAt) が同時 ON のとき、片方 OFF だけでは listener に false を流さない。
        /// </summary>
        /// <remarks>
        /// 既存 listener signature <c>Action&lt;bool&gt;</c> はそのまま (Breaking Change 回避)。
        /// 内部で refcount を保持し、boundary (0 ↔ 1) 通過時のみ listener を呼ぶ。
        /// </remarks>
        private void NotifyEditMode(bool isEditing)
        {
            int previous = _editModeRefCount;
            if (isEditing) _editModeRefCount++;
            else _editModeRefCount = System.Math.Max(0, _editModeRefCount - 1);

            bool wasActive = previous > 0;
            bool nowActive = _editModeRefCount > 0;
            if (wasActive == nowActive) return; // 状態変化が無ければ listener を起こさない。

            foreach (var listener in _editModeListeners) listener?.Invoke(nowActive);
        }

        /// <summary>
        /// Phase 2-A (2026-05-18): "Place LookAt" toggle で marker 配置モードを開始/終了する。
        /// </summary>
        /// <remarks>
        /// 配置は <see cref="LookAtMarkerPlaceHandler"/> 側で 1 回押すと自動的に終了する仕様のため、
        /// ON にした瞬間 Manager に BeginPlacing を伝えるだけで足りる。OFF にされた場合は明示的に
        /// <see cref="LookAtMarkerVisualManager.EndPlacing"/> を呼んで配置モードを抜く。
        /// </remarks>
        private void OnLookAtPlaceToggleChanged(ChangeEvent<bool> e)
        {
            if (lookAtMarkerManager == null) return;
            if (e.newValue) lookAtMarkerManager.BeginPlacing();
            else lookAtMarkerManager.EndPlacing();
        }

        /// <summary>
        /// "Edit LookAt" toggle で配置済 marker の grab 編集モードを開始/終了する。
        /// edit 中は EdgeDrag/EdgeCut/NodeDelete を一時無効化する (Path edit と同じ <see cref="NotifyEditMode"/> 経由)。
        /// </summary>
        private void OnLookAtEditToggleChanged(ChangeEvent<bool> e)
        {
            if (lookAtMarkerManager == null) return;
            if (e.newValue)
            {
                lookAtMarkerManager.BeginEditing();
                NotifyEditMode(true);
            }
            else
            {
                lookAtMarkerManager.EndEditing();
                NotifyEditMode(false);
            }
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
            var motion = _selected.GetComponent<ICameraMotion>();
            if (motion == null) return;

            // ソース選択を CameraMotionSourceBinding に耐久記録する (セーブ/ロード対象)。
            var binding = _selected.GetComponent<CameraMotionSourceBinding>();
            if (string.IsNullOrEmpty(e.newValue) || e.newValue == NoSourceLabel)
            {
                binding?.Clear();
                return;
            }

            var target = FindFloatOutput(e.newValue);
            if (target == null) return;

            binding?.SetBinding(target.Value.NodeId, target.Value.PortName);
            SubscribeMotion(motion, target.Value);
        }

        /// <summary>表示名で Float 出力ポートを引く。見つからなければ null。</summary>
        private FloatOutputRef? FindFloatOutput(string displayName)
        {
            foreach (var p in _floatOutputs)
                if (p.DisplayName == displayName) return p;
            return null;
        }

        /// <summary>nodeId + portName で Float 出力ポートを引く (ロード復元用)。</summary>
        private FloatOutputRef? FindFloatOutputByPort(string nodeId, string portName)
        {
            foreach (var p in _floatOutputs)
                if (p.NodeId == nodeId && p.PortName == portName) return p;
            return null;
        }

        /// <summary>指定 Float 出力ポートを購読し、カメラの Motion を駆動する。</summary>
        private void SubscribeMotion(ICameraMotion motion, FloatOutputRef source)
        {
            if (_floatOutputCatalog == null) return;
            _progressSubscription = _floatOutputCatalog.Subscribe(
                source.NodeId,
                source.PortName,
                v =>
                {
                    motion.SetDrive(v);
                    // スライダーも追従させて現在値を可視化
                    if (_progressSlider != null) _progressSlider.SetValueWithoutNotify(Mathf.Clamp01(v));
                    if (_progressValue != null) _progressValue.text = $"{v:F2}";
                });
        }

        /// <summary>Float 出力ポート一覧を取得し、Motion ソース dropdown の選択肢を再構築する。</summary>
        private void PopulateMotionSourceDropdown()
        {
            RefreshFloatOutputs();
            if (_progressDropdown == null) return;
            var labels = new List<string> { NoSourceLabel };
            foreach (var p in _floatOutputs) labels.Add(p.DisplayName);
            _progressDropdown.choices = labels;
        }

        /// <summary>
        /// カメラの <see cref="CameraMotionSourceBinding"/> を読み、保存済みソースがあれば
        /// 購読を貼り直して dropdown を一致させる。未設定 / 解決不能なら (none) に戻す。
        /// </summary>
        private void BindMotionRowToSavedSource(ICameraMotion motion, CameraMotionSourceBinding? binding)
        {
            if (binding != null && binding.HasBinding)
            {
                var target = FindFloatOutputByPort(binding.NodeId, binding.PortName);
                if (target != null)
                {
                    SubscribeMotion(motion, target.Value);
                    _progressDropdown?.SetValueWithoutNotify(target.Value.DisplayName);
                    return;
                }
            }
            _progressDropdown?.SetValueWithoutNotify(NoSourceLabel);
        }

        /// <summary>
        /// Progress スライダーで手動駆動する。Source が選ばれていてもスライダー操作は通る (一時オーバーライド)。
        /// </summary>
        private void OnProgressSliderChanged(ChangeEvent<float> e)
        {
            if (_selected == null) return;
            var motion = _selected.GetComponent<ICameraMotion>();
            if (motion == null) return;
            motion.SetDrive(e.newValue);
            if (_progressValue != null) _progressValue.text = $"{e.newValue:F2}";
        }

        /// <summary>
        /// Dropdown を最新の Float 出力ポート一覧で再構築する。
        /// 編集モード後に LFO を追加したり等で必要。
        /// </summary>
        private void OnProgressRefreshClicked()
        {
            if (_selected == null) return;
            var motion = _selected.GetComponent<ICameraMotion>();
            if (motion == null || _progressDropdown == null) return;

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
