#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using Rhizomode.Ableton.Transport;
using Rhizomode.Ableton.Session;
using Rhizomode.Ableton.GraphAdapter;
using Rhizomode.Input.Contracts;
using Rhizomode.UI;
using Rhizomode.XR;
using UnityEngine;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// Ableton OSC 設定パネル + クリップグリッド + コントロールパネル + Macro listener の配線を
    /// 担う post-Build wiring。Plan v5.4 §15 (V3a): 旧 <c>GameBootstrap.Ableton.cs</c> 全体 +
    /// <c>InitializeAbletonOsc</c> を Bootstrap asmdef へ移送。
    /// </summary>
    /// <remarks>
    /// V2 踏襲: <see cref="AbletonInstaller"/> は本クラスを container に登録するのみ。副作用を伴う
    /// <see cref="Wire"/> は Build 後の eager step が駆動する。<see cref="Wire"/> は VR/Desktop の
    /// 入力ルーター (<see cref="IControllerInput"/>) と <see cref="SharedRaycastService"/> を要する —
    /// これらは V3c で InputInstaller / InteractionInstaller が container 化するまで GameBootstrap が
    /// 解決して渡す (一時的 Plan v5.4 違反)。
    ///
    /// Macro の Track / Device index は <see cref="XrSceneReferences"/> の初期値を複製し、以降は
    /// 本クラスの可変状態として扱う。<see cref="Dispose"/> で Macro listener 購読と外枠インスタンスを解放。
    /// </remarks>
    public sealed class AbletonBootstrapWiring : IDisposable
    {
        private readonly AbletonLink? _abletonLink;
        private readonly AbletonOscBridge? _abletonBridge;
        private readonly AbletonSetupPanel? _abletonSetupPanel;
        private readonly AbletonClipGridManager? _abletonGridManager;
        private readonly ClipFireRayHandler? _clipFireHandler;
        private readonly AbletonControlPanel? _abletonControlPanel;
        private readonly Transform? _abletonUiAnchor;
        private readonly Material? _abletonOuterFrameMaterial;
        private readonly float _abletonOuterFramePadding;
        private readonly float _abletonOuterFrameDepthOffset;
        private readonly float _abletonOuterFrameCornerRadius;

        private int _macroTrackIndex;
        private int _macroDeviceIndex;

        private IControllerInput? _activeInput;
        private GameObject? _abletonOuterFrameInstance;
        private IDisposable? _macroValueListenerSub;
        private bool _wired;

        public AbletonBootstrapWiring(XrSceneReferences refs)
        {
            _abletonLink = refs.AbletonLink;
            _abletonBridge = refs.AbletonBridge;
            _abletonSetupPanel = refs.AbletonSetupPanel;
            _abletonGridManager = refs.AbletonGridManager;
            _clipFireHandler = refs.ClipFireHandler;
            _abletonControlPanel = refs.AbletonControlPanel;
            _abletonUiAnchor = refs.AbletonUiAnchor;
            _abletonOuterFrameMaterial = refs.AbletonOuterFrameMaterial;
            _abletonOuterFramePadding = refs.AbletonOuterFramePadding;
            _abletonOuterFrameDepthOffset = refs.AbletonOuterFrameDepthOffset;
            _abletonOuterFrameCornerRadius = refs.AbletonOuterFrameCornerRadius;
            _macroTrackIndex = refs.MacroTrackIndex;
            _macroDeviceIndex = refs.MacroDeviceIndex;
        }

        /// <summary>
        /// Ableton OSC 設定パネルを表示し、Connect 押下時にレイアウト問い合わせ + グリッド生成を
        /// 実行する配線を張る。Skip 時は単純にパネルを閉じる。PlayerPrefs への host/port 保存は
        /// この層で行う (UI はプリミティブのみ扱うため)。
        /// </summary>
        public void Wire(IControllerInput? activeInput, SharedRaycastService? sharedRaycastService)
        {
            if (_wired) return;
            _activeInput = activeInput;

            if (_abletonBridge != null)
                _abletonBridge.Link = _abletonLink;

            if (_abletonSetupPanel == null)
            {
                _wired = true;
                return;
            }

            var host = PlayerPrefs.GetString("abl.host", "127.0.0.1");
            var sendPort = PlayerPrefs.GetInt("abl.sendPort", 11000);
            var recvPort = PlayerPrefs.GetInt("abl.recvPort", 11001);
            _abletonSetupPanel.SetInitialValues(host, sendPort, recvPort);

            _abletonSetupPanel.OnConnectRequested += async (h, sp, rp) =>
            {
                try
                {
                    PlayerPrefs.SetString("abl.host", h);
                    PlayerPrefs.SetInt("abl.sendPort", sp);
                    PlayerPrefs.SetInt("abl.recvPort", rp);
                    PlayerPrefs.Save();

                    _abletonSetupPanel.SetStatus("Connecting…", Color.yellow);
                    _abletonLink?.Reconnect(h, sp, rp);

                    var ok = _abletonBridge != null && await _abletonBridge.QueryLayoutAsync();
                    if (!ok)
                        _abletonSetupPanel.SetStatus("Timeout — empty grid", Color.red);

                    if (_abletonGridManager != null)
                    {
                        if (_abletonControlPanel != null)
                            _abletonGridManager.SetSpacing(
                                _abletonControlPanel.TrackHorizontalSpacing,
                                _abletonControlPanel.SceneVerticalSpacing);

                        var (gridPos, gridRot) = ResolveGridPose();
                        _abletonGridManager.SpawnGrid(gridPos, gridRot);
                        BuildControlPanel(gridPos, gridRot);
                        await PopulateMacrosAsync();
                        SpawnAbletonOuterFrame(gridPos, gridRot);
                    }

                    _abletonSetupPanel.Hide();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AbletonBootstrapWiring] Ableton connect flow failed: {e.Message}");
                    _abletonSetupPanel.SetStatus($"Error: {e.Message}", Color.red);
                }
            };

            _abletonSetupPanel.OnSkipRequested += () => _abletonSetupPanel.Hide();

            if (_abletonGridManager != null && _abletonBridge != null)
                _abletonGridManager.Initialize(_abletonBridge, _abletonLink);

            if (_clipFireHandler != null && _activeInput != null
                && sharedRaycastService != null && _abletonLink != null)
            {
                _clipFireHandler.Initialize(_activeInput, sharedRaycastService, _abletonLink);
            }

            WireControlPanelEvents();

            // 起動時 1 回だけ表示。アンカーがあればそこ、無ければプレイヤー前方
            ShowSetupPanel();

            _wired = true;
        }

        private void ShowSetupPanel()
        {
            if (_abletonSetupPanel == null) return;

            if (_abletonUiAnchor != null)
            {
                _abletonSetupPanel.ShowAt(_abletonUiAnchor.position, _abletonUiAnchor.rotation);
            }
            else if (_activeInput != null)
            {
                _abletonSetupPanel.Show(_activeInput.HeadPosition, _activeInput.HeadForward);
            }
        }

        private (Vector3 pos, Quaternion rot) ResolveGridPose()
        {
            if (_abletonUiAnchor != null)
                return (_abletonUiAnchor.position, _abletonUiAnchor.rotation);

            if (_activeInput != null)
            {
                var pos = _activeInput.HeadPosition + _activeInput.HeadForward * 0.8f;
                var rot = Quaternion.LookRotation(pos - _activeInput.HeadPosition);
                return (pos, rot);
            }

            return (Vector3.zero, Quaternion.identity);
        }

        private void WireControlPanelEvents()
        {
            if (_abletonControlPanel == null) return;

            _abletonControlPanel.OnMasterVolumeChanged += v =>
                _abletonLink?.Send("/live/master/set/volume", v);
            _abletonControlPanel.OnTempoChanged += v =>
                _abletonLink?.Send("/live/song/set/tempo", v);
            _abletonControlPanel.OnTrackVolumeChanged += (t, v) =>
                _abletonLink?.SendIntFloat("/live/track/set/volume", t, v);
            _abletonControlPanel.OnTrackStopRequested += t =>
                _abletonLink?.Send("/live/track/stop_all_clips", t);
            _abletonControlPanel.OnPlayRequested += () =>
                _abletonLink?.Send("/live/song/start_playing");
            _abletonControlPanel.OnStopRequested += () =>
                _abletonLink?.Send("/live/song/stop_playing");
            _abletonControlPanel.OnMacroChanged += (macroIdx, value) =>
                SendMacroValue(macroIdx, value);
            _abletonControlPanel.OnMacroTargetChangeRequested += (dt, dd) =>
                _ = RebindMacrosAsync(dt, dd);
        }

        /// <summary>
        /// Macro 対象 Track/Device を delta ぶんずらして再構築する。
        /// Track index は -1 (Master) を含む循環: -1 → 0 → ... → NumTracks-1 → -1。
        /// Device index は負を 0 にクランプ (上限取得は省略)。
        /// </summary>
        private async Task RebindMacrosAsync(int trackDelta, int deviceDelta)
        {
            if (_abletonBridge == null || _abletonControlPanel == null) return;

            var newTrack = _macroTrackIndex + trackDelta;
            var newDevice = Mathf.Max(0, _macroDeviceIndex + deviceDelta);

            var numTracks = _abletonBridge.NumTracks;
            if (numTracks > 0)
            {
                if (newTrack < -1) newTrack = numTracks - 1;
                else if (newTrack >= numTracks) newTrack = -1;
            }
            else if (newTrack < -1)
            {
                newTrack = -1;
            }

            UnsubscribeMacroValueListener();

            _macroTrackIndex = newTrack;
            _macroDeviceIndex = newDevice;
            _abletonControlPanel.SetMacroTargetLabel(newTrack, newDevice);

            await PopulateMacrosAsync();
        }

        /// <summary>
        /// 現在の Macro 対象に対する parameter/value listener を解除する。
        /// stop_listen を Live に送信し、ローカル Subscribe を Dispose。
        /// </summary>
        private void UnsubscribeMacroValueListener()
        {
            if (_abletonLink != null && _abletonBridge != null)
            {
                foreach (var m in _abletonBridge.Macros)
                {
                    _abletonLink.SendInt3(
                        "/live/device/stop_listen/parameter/value",
                        _macroTrackIndex, _macroDeviceIndex, m.ParamId);
                }
            }

            _macroValueListenerSub?.Dispose();
            _macroValueListenerSub = null;
        }

        /// <summary>
        /// AbletonControlPanel の Macro Knob 操作 → /live/device/set/parameter/value 送信。
        /// macroIdx は 0 始まり、ParamId に +1 して送る (0 = Device On はスキップ)。
        /// </summary>
        private void SendMacroValue(int macroIdx, float value)
        {
            if (_abletonLink == null) return;
            var paramId = macroIdx + 1;
            _abletonLink.SendInt3Float(
                "/live/device/set/parameter/value",
                _macroTrackIndex, _macroDeviceIndex, paramId, value);
        }

        /// <summary>
        /// Macro メタを Bridge から取得 → ControlPanel にセット → start_listen で双方向同期を確立。
        /// 失敗しても他の UI は動くよう例外を握る。
        /// </summary>
        private async Task PopulateMacrosAsync()
        {
            if (_abletonBridge == null || _abletonControlPanel == null) return;

            var count = _abletonControlPanel.MacroCount;

            try
            {
                await _abletonBridge.QueryMacrosAsync(_macroTrackIndex, _macroDeviceIndex, count);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbletonBootstrapWiring] QueryMacrosAsync failed: {e.Message}");
                return;
            }

            var macros = _abletonBridge.Macros;
            if (macros == null || macros.Length == 0) return;

            var names = new string[macros.Length];
            var values = new float[macros.Length];
            var mins = new float[macros.Length];
            var maxs = new float[macros.Length];
            for (var i = 0; i < macros.Length; i++)
            {
                names[i] = macros[i].Name ?? string.Empty;
                values[i] = macros[i].Value;
                mins[i] = macros[i].Min;
                maxs[i] = macros[i].Max;
            }

            _abletonControlPanel.BuildMacroStrip(names, values, mins, maxs);
            _abletonControlPanel.SetMacroTargetLabel(_macroTrackIndex, _macroDeviceIndex);
            SubscribeMacroValueListener(macros);
        }

        /// <summary>
        /// /live/device/get/parameter/value への listen を全 Macro 分張り、
        /// 応答が来たら ControlPanel.SetMacroValue で UI に反映 (Push やオートメーション対応)。
        /// </summary>
        private void SubscribeMacroValueListener(AbletonMacroMeta[] macros)
        {
            _macroValueListenerSub?.Dispose();
            _macroValueListenerSub = null;

            if (_abletonLink == null || _abletonControlPanel == null) return;

            foreach (var m in macros)
            {
                _abletonLink.SendInt3(
                    "/live/device/start_listen/parameter/value",
                    _macroTrackIndex, _macroDeviceIndex, m.ParamId);
            }

            var paramIdToIdx = new Dictionary<int, int>(macros.Length);
            for (var i = 0; i < macros.Length; i++)
                paramIdToIdx[macros[i].ParamId] = i;

            _macroValueListenerSub = _abletonLink
                .GetAddressObservable("/live/device/get/parameter/value")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 3) return;
                    if (msg.IntArgs[0] != _macroTrackIndex || msg.IntArgs[1] != _macroDeviceIndex) return;
                    var paramId = msg.IntArgs[2];
                    if (!paramIdToIdx.TryGetValue(paramId, out var idx)) return;

                    var v = msg.FloatArgs.Length > 3 ? msg.FloatArgs[3] : 0f;
                    _abletonControlPanel.SetMacroValue(idx, v);
                });
        }

        private void BuildControlPanel(Vector3 gridOrigin, Quaternion facing)
        {
            if (_abletonControlPanel == null || _abletonBridge == null) return;

            var tracks = _abletonBridge.Tracks;
            if (tracks == null || tracks.Length == 0) return;

            var hSpacing = _abletonControlPanel.TrackHorizontalSpacing;
            var vSpacing = _abletonControlPanel.SceneVerticalSpacing;
            var trackNames = new string[tracks.Length];
            for (var i = 0; i < tracks.Length; i++)
                trackNames[i] = tracks[i].Name ?? string.Empty;

            var panelWidth = Mathf.Max(0.4f, tracks.Length * hSpacing);
            var panelHeight = panelWidth * AbletonControlPanel.TextureAspectRatio;
            var rightAxis = facing * Vector3.right;
            var upAxis = facing * Vector3.up;
            var centerX = (tracks.Length - 1) * 0.5f * hSpacing;
            var verticalOffset = panelHeight * 0.5f + vSpacing * 0.6f;
            var panelPos = gridOrigin + rightAxis * centerX - upAxis * verticalOffset;

            _abletonControlPanel.Build(trackNames, panelPos, facing, panelWidth);
        }

        /// <summary>
        /// グリッド + コントロールパネル全体を囲む角丸フレームを生成する。
        /// マテリアル未設定時はスキップ。再 Connect 時は前のインスタンスを破棄。
        /// </summary>
        private void SpawnAbletonOuterFrame(Vector3 gridOrigin, Quaternion facing)
        {
            if (_abletonOuterFrameMaterial == null) return;
            if (_abletonBridge == null || _abletonControlPanel == null) return;

            var tracks = _abletonBridge.Tracks;
            if (tracks == null || tracks.Length == 0) return;

            var hSpacing = _abletonControlPanel.TrackHorizontalSpacing;
            var vSpacing = _abletonControlPanel.SceneVerticalSpacing;
            var controlPanelGap = vSpacing * 0.6f;

            var numTracks = tracks.Length;
            var numScenes = _abletonBridge.NumScenes;

            var gridWidth = (numTracks - 1) * hSpacing + hSpacing;
            var gridHeight = (numScenes - 1) * vSpacing + vSpacing;
            var gridCenterX = (numTracks - 1) * 0.5f * hSpacing;
            var gridCenterY = (numScenes - 1) * 0.5f * vSpacing;

            var ctrlWidth = Mathf.Max(0.4f, numTracks * hSpacing);
            var ctrlHeight = ctrlWidth * AbletonControlPanel.TextureAspectRatio;
            var ctrlCenterY = -controlPanelGap - ctrlHeight * 0.5f;

            var totalLeft = Mathf.Min(-hSpacing * 0.5f, gridCenterX - ctrlWidth * 0.5f);
            var totalRight = Mathf.Max(gridCenterX + gridWidth * 0.5f, gridCenterX + ctrlWidth * 0.5f);
            var totalTop = gridCenterY + gridHeight * 0.5f;
            var totalBottom = ctrlCenterY - ctrlHeight * 0.5f;

            var bboxWidth = totalRight - totalLeft + _abletonOuterFramePadding * 2f;
            var bboxHeight = totalTop - totalBottom + _abletonOuterFramePadding * 2f;
            var bboxCenterLocal = new Vector3(
                (totalLeft + totalRight) * 0.5f,
                (totalTop + totalBottom) * 0.5f,
                _abletonOuterFrameDepthOffset);

            if (_abletonOuterFrameInstance != null)
                UnityEngine.Object.Destroy(_abletonOuterFrameInstance);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "AbletonOuterFrame";
            var collider = frame.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            var worldCenter = gridOrigin + facing * bboxCenterLocal;
            frame.transform.SetPositionAndRotation(worldCenter, facing);
            frame.transform.localScale = new Vector3(bboxWidth, bboxHeight, 1f);

            var renderer = frame.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -10;

            var matInstance = new Material(_abletonOuterFrameMaterial);
            matInstance.renderQueue = 2990;
            matInstance.SetVector("_RectSize", new Vector4(bboxWidth, bboxHeight, 0f, 0f));
            matInstance.SetFloat("_CornerRadius", _abletonOuterFrameCornerRadius);
            renderer.sharedMaterial = matInstance;

            _abletonOuterFrameInstance = frame;
        }

        public void Dispose()
        {
            _macroValueListenerSub?.Dispose();
            _macroValueListenerSub = null;

            if (_abletonOuterFrameInstance != null)
            {
                UnityEngine.Object.Destroy(_abletonOuterFrameInstance);
                _abletonOuterFrameInstance = null;
            }
        }
    }
}
