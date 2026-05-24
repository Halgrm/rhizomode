#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 保存済み Cue (graph snapshot) の一覧を WorldPanel に表示し、Save / Recall / Delete を捌く。
    /// Recall 時は MirrorOutput を一時 freeze して観客側の glitch を隠す。
    /// </summary>
    /// <remarks>
    /// 配置先 GameObject は <see cref="WorldPanelHost"/> を持つ。UXML/USS は SerializeField で
    /// 注入する。Service の DI は <see cref="Initialize"/> 経由 (StatusPanelController と同じ
    /// transitional パターン)。Freeze→LoadGraph は同期完了するが、Module/VFX の warm-up が
    /// 1〜2 frame 走るため <see cref="UnfreezeDelayFrames"/> 待ってから Unfreeze する。
    /// </remarks>
    [RequireComponent(typeof(WorldPanelHost))]
    public class CueListPanelController : MonoBehaviour
    {
        private const int PanelTextureWidth = 512;
        private const int PanelTextureHeight = 640;
        private const int UnfreezeDelayFrames = 2;

        [SerializeField] private VisualTreeAsset? panelUxml;
        [SerializeField] private StyleSheet? panelStyleSheet;

        [Header("Status message timing")]
        [Tooltip("ステータスラベルが自動で消えるまでの秒数。")]
        [SerializeField, Range(0.5f, 10f)] private float statusFadeSeconds = 3.5f;

        private CueLibraryService? _service;
        private WorldPanelHost? _panelHost;
        private readonly List<Texture2D> _thumbnailTextures = new();
        private Coroutine? _activeUnfreezeCoroutine;
        private bool _isLoading;

        private ScrollView? _listRoot;
        private ScrollView? _sceneTabsRoot;
        private TextField? _saveNameField;
        private Button? _saveButton;
        private Button? _undoButton;
        private Button? _redoButton;
        private Label? _statusLabel;

        private bool _initialized;
        private float _statusClearAt;

        /// <summary>
        /// 現在選択中の scene フィルタ。null/empty なら "All" (全件表示)。
        /// </summary>
        /// <remarks>
        /// "シーンと演出を選んで cue る" mode (user 要望): scene タブで cue を絞り込む。
        /// Quick slot (1-9) も本フィルタ後の一覧を対象とする (表示と一致させる)。
        /// </remarks>
        private string? _selectedScene;

        /// <summary>
        /// 依存サービスを注入してパネルを初期化する。Wiring から呼ばれる。
        /// </summary>
        public void Initialize(CueLibraryService service)
        {
            _service = service;
            EnsurePanel();
            RefreshList();
            PrimeDefaultName();
        }

        private void EnsurePanel()
        {
            if (_initialized) return;
            if (_panelHost == null) _panelHost = GetComponent<WorldPanelHost>();
            if (_panelHost == null || panelUxml == null) return;

            if (!_panelHost.IsInitialized)
                _panelHost.Initialize(panelUxml, panelStyleSheet, PanelTextureWidth, PanelTextureHeight);

            CacheElements();
            _initialized = true;
        }

        private void CacheElements()
        {
            var root = _panelHost?.Root;
            if (root == null) return;

            _listRoot = root.Q<ScrollView>("cue-list-root");
            _sceneTabsRoot = root.Q<ScrollView>("scene-tabs");
            _saveNameField = root.Q<TextField>("save-name-field");
            _saveButton = root.Q<Button>("save-btn");
            _undoButton = root.Q<Button>("undo-btn");
            _redoButton = root.Q<Button>("redo-btn");
            _statusLabel = root.Q<Label>("status-label");

            if (_saveButton != null)
                _saveButton.RegisterCallback<ClickEvent>(_ => HandleSaveClicked());
            if (_undoButton != null)
                _undoButton.RegisterCallback<ClickEvent>(_ => HandleUndoClicked());
            if (_redoButton != null)
                _redoButton.RegisterCallback<ClickEvent>(_ => HandleRedoClicked());

            RefreshHistoryButtons();
        }

        private void RefreshHistoryButtons()
        {
            if (_service == null) return;
            if (_undoButton != null) _undoButton.SetEnabled(_service.CanUndo);
            if (_redoButton != null) _redoButton.SetEnabled(_service.CanRedo);
        }

        private void HandleUndoClicked()
        {
            if (_service == null) return;

            bool ok;
            try { ok = _service.TryUndo(); }
            catch (Exception e)
            {
                Debug.LogError($"[CueListPanel] Undo failed: {e.Message}");
                ShowStatus($"undo failed: {e.Message}", isError: true);
                return;
            }

            ShowStatus(ok ? "undone" : "nothing to undo", isError: !ok);
            RefreshHistoryButtons();
        }

        private void HandleRedoClicked()
        {
            if (_service == null) return;

            bool ok;
            try { ok = _service.TryRedo(); }
            catch (Exception e)
            {
                Debug.LogError($"[CueListPanel] Redo failed: {e.Message}");
                ShowStatus($"redo failed: {e.Message}", isError: true);
                return;
            }

            ShowStatus(ok ? "redone" : "nothing to redo", isError: !ok);
            RefreshHistoryButtons();
        }

        private void PrimeDefaultName()
        {
            if (_saveNameField == null || _service == null) return;
            _saveNameField.SetValueWithoutNotify(_service.SuggestNewName());
        }

        /// <summary>cue 一覧を再構築する。古いサムネ Texture2D は破棄する。</summary>
        /// <remarks>
        /// F6 fix (Codex C-A): Image.image を null にして VisualElement reference を切り、
        /// _listRoot.Clear() で hierarchy から外してから Texture を Destroy する。
        /// 旧順 (Destroy → Clear) では同フレーム内 layout pass で破棄済 Texture を Image が
        /// 参照する race があった。
        /// </remarks>
        public void RefreshList()
        {
            if (_listRoot == null || _service == null) return;

            DetachThumbnailImages();
            _listRoot.Clear();
            DisposeThumbnails();

            // フィルタ前に scene タブを再構築 — 新規 cue 保存 / 削除で scene 集合が変動する。
            RebuildSceneTabs();

            var cues = _service.ListCuesByScene(_selectedScene);
            foreach (var name in cues)
                _listRoot.Add(BuildCueRow(name));
        }

        /// <summary>
        /// scene-tabs ScrollView を作り直す。"All" + 出現する scene 名一覧 (alphabetical) でボタンを並べる。
        /// </summary>
        /// <remarks>
        /// 旧形式 cue (sceneName 空) は scene タブに現れず "All" でしか表示されない。Save 時に
        /// active scene が自動で記録されるため、新規保存後は自動で該当 scene タブに分類される。
        /// </remarks>
        private void RebuildSceneTabs()
        {
            if (_sceneTabsRoot == null || _service == null) return;
            _sceneTabsRoot.Clear();

            _sceneTabsRoot.Add(BuildSceneTab(label: "All", sceneName: null));

            foreach (var scene in _service.ListScenesInCues())
                _sceneTabsRoot.Add(BuildSceneTab(label: scene, sceneName: scene));
        }

        private Button BuildSceneTab(string label, string? sceneName)
        {
            var btn = new Button(() => HandleSceneTabClicked(sceneName)) { text = label };
            btn.AddToClassList("cue-panel__scene-tab");
            // 選択中タブをハイライト (null↔null も等価扱い)。
            if (string.Equals(_selectedScene ?? "", sceneName ?? "", StringComparison.Ordinal))
                btn.AddToClassList("cue-panel__scene-tab--selected");
            return btn;
        }

        private void HandleSceneTabClicked(string? sceneName)
        {
            // 同じタブ再クリックは no-op (rebuild 抑止)
            if (string.Equals(_selectedScene ?? "", sceneName ?? "", StringComparison.Ordinal)) return;
            _selectedScene = sceneName;
            RefreshList();
        }

        private bool IsTextInputFocused()
        {
            var root = _panelHost?.Root;
            var focused = root?.focusController?.focusedElement;
            if (focused == null) return false;
            // TextField の子 (TextElement) にフォーカスが行くこともあるので親も辿る。
            return focused is TextField
                || (focused is VisualElement ve && ve.GetFirstAncestorOfType<TextField>() != null);
        }

        private void DetachThumbnailImages()
        {
            if (_listRoot == null) return;
            foreach (var row in _listRoot.Children())
            {
                var img = row.Q<Image>(className: "cue-row__thumb");
                if (img != null) img.image = null;
            }
        }

        private VisualElement BuildCueRow(string cueName)
        {
            var row = new VisualElement();
            row.AddToClassList("cue-row");

            var thumb = new Image();
            thumb.AddToClassList("cue-row__thumb");
            var thumbTex = _service?.LoadThumbnail(cueName);
            if (thumbTex != null)
            {
                thumb.image = thumbTex;
                _thumbnailTextures.Add(thumbTex);
            }
            row.Add(thumb);

            var nameLabel = new Label(cueName);
            nameLabel.AddToClassList("cue-row__name");
            row.Add(nameLabel);

            var loadBtn = new Button(() => HandleLoadClicked(cueName)) { text = "Load" };
            loadBtn.AddToClassList("cue-row__load-btn");
            row.Add(loadBtn);

            var deleteBtn = new Button(() => HandleDeleteClicked(cueName)) { text = "✕" };
            deleteBtn.AddToClassList("cue-row__delete-btn");
            row.Add(deleteBtn);

            return row;
        }

        private void DisposeThumbnails()
        {
            foreach (var tex in _thumbnailTextures)
            {
                if (tex != null) Destroy(tex);
            }
            _thumbnailTextures.Clear();
        }

        private void OnDestroy() => DisposeThumbnails();

        private void HandleSaveClicked()
        {
            if (_service == null || _saveNameField == null) return;
            var name = (_saveNameField.value ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus("name is empty", isError: true);
                return;
            }

            bool ok;
            try { ok = _service.SaveAs(name); }
            catch (Exception e)
            {
                Debug.LogError($"[CueListPanel] Save failed: {e.Message}");
                ShowStatus($"save failed: {e.Message}", isError: true);
                return;
            }

            if (!ok)
            {
                ShowStatus("save failed", isError: true);
                return;
            }

            ShowStatus($"saved: {name}", isError: false);
            RefreshList();
            PrimeDefaultName();
        }

        private void HandleLoadClicked(string cueName) =>
            BeginLoad(cueName, statusPrefix: "recalled");

        /// <summary>
        /// Load 経路の共通化。F2 fix (Codex C-D): 連打 / Click+QuickSlot 同時発火に対する race を防ぐ。
        /// </summary>
        /// <remarks>
        /// - <see cref="_isLoading"/> guard で UnfreezeDelay 完了までの再入を弾く。
        /// - 既存 unfreeze coroutine が走行中なら stop して、新規 freeze が古い coroutine の発火で
        ///   Mirror カメラが enabled=true に戻る race を回避する。
        /// </remarks>
        private void BeginLoad(string cueName, string statusPrefix)
        {
            if (_service == null || _isLoading) return;
            _isLoading = true;

            if (_activeUnfreezeCoroutine != null)
            {
                StopCoroutine(_activeUnfreezeCoroutine);
                _activeUnfreezeCoroutine = null;
            }

            _service.FreezeOutput();
            bool ok;
            try { ok = _service.LoadCue(cueName); }
            catch (Exception e)
            {
                Debug.LogError($"[CueListPanel] Load failed: {e.Message}");
                _service.UnfreezeOutput();
                _isLoading = false;
                ShowStatus($"{statusPrefix}: {e.Message}", isError: true);
                return;
            }

            if (!ok)
            {
                _service.UnfreezeOutput();
                _isLoading = false;
                ShowStatus($"{statusPrefix}: load failed", isError: true);
                return;
            }

            _activeUnfreezeCoroutine = StartCoroutine(UnfreezeAfterDelay());
            ShowStatus($"{statusPrefix}: {cueName}", isError: false);
        }

        private IEnumerator UnfreezeAfterDelay()
        {
            for (int i = 0; i < UnfreezeDelayFrames; i++) yield return null;
            _service?.UnfreezeOutput();
            _activeUnfreezeCoroutine = null;
            _isLoading = false;
        }

        private void HandleDeleteClicked(string cueName)
        {
            if (_service == null) return;

            bool ok;
            try { ok = _service.Delete(cueName); }
            catch (Exception e)
            {
                Debug.LogError($"[CueListPanel] Delete failed: {e.Message}");
                ShowStatus($"delete failed: {e.Message}", isError: true);
                return;
            }

            if (!ok)
            {
                ShowStatus($"delete failed: {cueName}", isError: true);
                return;
            }

            ShowStatus($"deleted: {cueName}", isError: false);
            RefreshList();
            PrimeDefaultName();
        }

        private void ShowStatus(string text, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = text;
            _statusLabel.RemoveFromClassList("cue-panel__status--ok");
            _statusLabel.RemoveFromClassList("cue-panel__status--error");
            _statusLabel.AddToClassList(isError ? "cue-panel__status--error" : "cue-panel__status--ok");
            _statusClearAt = Time.unscaledTime + statusFadeSeconds;
        }

        private void Update()
        {
            if (_statusLabel != null && _statusClearAt > 0f && Time.unscaledTime >= _statusClearAt)
            {
                _statusLabel.text = "";
                _statusLabel.RemoveFromClassList("cue-panel__status--ok");
                _statusLabel.RemoveFromClassList("cue-panel__status--error");
                _statusClearAt = 0f;
            }

            RefreshHistoryButtons();
            PollQuickSlots();
        }

        private void PollQuickSlots()
        {
            if (_service == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // F7 fix (Codex C-C): TextField (cue 名入力欄) フォーカス中は quick slot を無効化。
            // 入力中の数字キーが load を誤発火するのを防ぐ。
            if (IsTextInputFocused()) return;

            // F2 ガード: ロード中は次の slot を受け付けない (BeginLoad 側でも guard 済だが
            // 早期 return で freeze/coroutine 操作のコストも避ける)。
            if (_isLoading) return;

            // Digit row 1-9 → slot 0..8。0 (10 番目) は予約として開けておく。
            var slot = -1;
            if (kb.digit1Key.wasPressedThisFrame) slot = 0;
            else if (kb.digit2Key.wasPressedThisFrame) slot = 1;
            else if (kb.digit3Key.wasPressedThisFrame) slot = 2;
            else if (kb.digit4Key.wasPressedThisFrame) slot = 3;
            else if (kb.digit5Key.wasPressedThisFrame) slot = 4;
            else if (kb.digit6Key.wasPressedThisFrame) slot = 5;
            else if (kb.digit7Key.wasPressedThisFrame) slot = 6;
            else if (kb.digit8Key.wasPressedThisFrame) slot = 7;
            else if (kb.digit9Key.wasPressedThisFrame) slot = 8;

            if (slot < 0) return;
            HandleQuickSlotLoad(slot);
        }

        private void HandleQuickSlotLoad(int slot)
        {
            if (_service == null) return;

            // 表示中の scene フィルタを尊重 — quick slot は「画面に見えている 1-9 番目」と一致させる。
            // "All" タブ (_selectedScene=null) のときは全件、scene タブ選択中はその scene の cue 群が対象。
            var cues = _service.ListCuesByScene(_selectedScene);
            if (slot >= cues.Count)
            {
                ShowStatus($"slot {slot + 1}: empty", isError: true);
                return;
            }

            BeginLoad(cues[slot], statusPrefix: $"slot {slot + 1}");
        }
    }
}
