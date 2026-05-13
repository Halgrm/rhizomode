#nullable enable

// Plan v5.3 F-8.2 抽出 5/N (Round F2): GameBootstrap god-object の Ableton 関連 ~360 行を partial
// class に分離。SerializeField field は main partial に残し、メソッドのみここに移送。
// SRP の完全分離 (別 class への抽出) は Phase 9+ で AbletonBootstrapCoordinator として行う想定。

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using Rhizomode.Ableton.Session;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    public partial class GameBootstrap
    {
        /// <summary>
        /// Ableton OSC設定パネルを表示し、Connect押下時にレイアウト問い合わせ＋
        /// グリッド生成を実行する。Skip時は単純にパネルを閉じる。
        /// PlayerPrefsへのhost/port保存はこの層で行う（UIはプリミティブのみ扱うため）。
        /// </summary>
        private void InitializeAbletonOsc()
        {
            if (abletonSetupPanel == null) return;

            var host = PlayerPrefs.GetString("abl.host", "127.0.0.1");
            var sendPort = PlayerPrefs.GetInt("abl.sendPort", 11000);
            var recvPort = PlayerPrefs.GetInt("abl.recvPort", 11001);
            abletonSetupPanel.SetInitialValues(host, sendPort, recvPort);

            abletonSetupPanel.OnConnectRequested += async (h, sp, rp) =>
            {
                try
                {
                    PlayerPrefs.SetString("abl.host", h);
                    PlayerPrefs.SetInt("abl.sendPort", sp);
                    PlayerPrefs.SetInt("abl.recvPort", rp);
                    PlayerPrefs.Save();

                    abletonSetupPanel.SetStatus("Connecting…", Color.yellow);
                    abletonLink?.Reconnect(h, sp, rp);

                    var ok = abletonBridge != null && await abletonBridge.QueryLayoutAsync();
                    if (!ok)
                        abletonSetupPanel.SetStatus("Timeout — empty grid", Color.red);

                    if (abletonGridManager != null)
                    {
                        if (abletonControlPanel != null)
                            abletonGridManager.SetSpacing(
                                abletonControlPanel.TrackHorizontalSpacing,
                                abletonControlPanel.SceneVerticalSpacing);

                        var (gridPos, gridRot) = ResolveGridPose();
                        abletonGridManager.SpawnGrid(gridPos, gridRot);
                        BuildControlPanel(gridPos, gridRot);
                        await PopulateMacrosAsync();
                        SpawnAbletonOuterFrame(gridPos, gridRot);
                    }

                    abletonSetupPanel.Hide();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameBootstrap] Ableton connect flow failed: {e.Message}");
                    abletonSetupPanel.SetStatus($"Error: {e.Message}", Color.red);
                }
            };

            abletonSetupPanel.OnSkipRequested += () => abletonSetupPanel.Hide();

            if (abletonGridManager != null && abletonBridge != null)
                abletonGridManager.Initialize(abletonBridge);

            if (clipFireHandler != null && _activeInput != null
                && sharedRaycastService != null && abletonLink != null)
            {
                clipFireHandler.Initialize(_activeInput, sharedRaycastService, abletonLink);
            }

            WireControlPanelEvents();

            // 起動時1回だけ表示。アンカーがあればそこ、無ければプレイヤー前方
            ShowSetupPanel();
        }

        private void ShowSetupPanel()
        {
            if (abletonSetupPanel == null) return;

            if (abletonUiAnchor != null)
            {
                abletonSetupPanel.ShowAt(abletonUiAnchor.position, abletonUiAnchor.rotation);
            }
            else if (_activeInput != null)
            {
                abletonSetupPanel.Show(_activeInput.HeadPosition, _activeInput.HeadForward);
            }
        }

        private (Vector3 pos, Quaternion rot) ResolveGridPose()
        {
            if (abletonUiAnchor != null)
                return (abletonUiAnchor.position, abletonUiAnchor.rotation);

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
            if (abletonControlPanel == null) return;

            abletonControlPanel.OnMasterVolumeChanged += v =>
                abletonLink?.Send("/live/master/set/volume", v);
            abletonControlPanel.OnTempoChanged += v =>
                abletonLink?.Send("/live/song/set/tempo", v);
            abletonControlPanel.OnTrackVolumeChanged += (t, v) =>
                abletonLink?.SendIntFloat("/live/track/set/volume", t, v);
            abletonControlPanel.OnTrackStopRequested += t =>
                abletonLink?.Send("/live/track/stop_all_clips", t);
            abletonControlPanel.OnPlayRequested += () =>
                abletonLink?.Send("/live/song/start_playing");
            abletonControlPanel.OnStopRequested += () =>
                abletonLink?.Send("/live/song/stop_playing");
            abletonControlPanel.OnMacroChanged += (macroIdx, value) =>
                SendMacroValue(macroIdx, value);
            abletonControlPanel.OnMacroTargetChangeRequested += (dt, dd) =>
                _ = RebindMacrosAsync(dt, dd);
        }

        /// <summary>
        /// Macro 対象 Track/Device を delta ぶんずらして再構築する。
        /// Track index は -1 (Master) を含む循環: -1 → 0 → ... → NumTracks-1 → -1。
        /// Device index は負を 0 にクランプ (上限取得は省略)。
        /// </summary>
        private async Task RebindMacrosAsync(int trackDelta, int deviceDelta)
        {
            if (abletonBridge == null || abletonControlPanel == null) return;

            var newTrack = macroTrackIndex + trackDelta;
            var newDevice = Mathf.Max(0, macroDeviceIndex + deviceDelta);

            // Track 範囲循環: -1 (Master) と 0..NumTracks-1
            var numTracks = abletonBridge.NumTracks;
            if (numTracks > 0)
            {
                if (newTrack < -1) newTrack = numTracks - 1;
                else if (newTrack >= numTracks) newTrack = -1;
            }
            else if (newTrack < -1)
            {
                newTrack = -1;
            }

            // 既存 listener を停止
            UnsubscribeMacroValueListener();

            macroTrackIndex = newTrack;
            macroDeviceIndex = newDevice;
            abletonControlPanel.SetMacroTargetLabel(newTrack, newDevice);

            // 新ターゲットで再 Query → 再 Build → 再 Subscribe
            await PopulateMacrosAsync();
        }

        /// <summary>
        /// 現在の Macro 対象に対する parameter/value listener を解除する。
        /// stop_listen を Live に送信し、ローカル Subscribe を Dispose。
        /// </summary>
        private void UnsubscribeMacroValueListener()
        {
            if (abletonLink != null && abletonBridge != null)
            {
                foreach (var m in abletonBridge.Macros)
                {
                    abletonLink.SendInt3(
                        "/live/device/stop_listen/parameter/value",
                        macroTrackIndex, macroDeviceIndex, m.ParamId);
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
            if (abletonLink == null) return;
            var paramId = macroIdx + 1;
            abletonLink.SendInt3Float(
                "/live/device/set/parameter/value",
                macroTrackIndex, macroDeviceIndex, paramId, value);
        }

        /// <summary>
        /// Macro メタを Bridge から取得 → ControlPanel にセット → start_listen で双方向同期を確立。
        /// 失敗しても他の UI は動くよう例外を握る。
        /// </summary>
        private async Task PopulateMacrosAsync()
        {
            if (abletonBridge == null || abletonControlPanel == null) return;

            var count = abletonControlPanel.MacroCount;

            try
            {
                await abletonBridge.QueryMacrosAsync(macroTrackIndex, macroDeviceIndex, count);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBootstrap] QueryMacrosAsync failed: {e.Message}");
                return;
            }

            var macros = abletonBridge.Macros;
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

            abletonControlPanel.BuildMacroStrip(names, values, mins, maxs);
            abletonControlPanel.SetMacroTargetLabel(macroTrackIndex, macroDeviceIndex);
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

            if (abletonLink == null || abletonControlPanel == null) return;

            // 各 Macro に start_listen
            foreach (var m in macros)
            {
                abletonLink.SendInt3(
                    "/live/device/start_listen/parameter/value",
                    macroTrackIndex, macroDeviceIndex, m.ParamId);
            }

            // ParamId → macroIdx の逆引きを構築
            var paramIdToIdx = new Dictionary<int, int>(macros.Length);
            for (var i = 0; i < macros.Length; i++)
                paramIdToIdx[macros[i].ParamId] = i;

            _macroValueListenerSub = abletonLink
                .GetAddressObservable("/live/device/get/parameter/value")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 3) return;
                    if (msg.IntArgs[0] != macroTrackIndex || msg.IntArgs[1] != macroDeviceIndex) return;
                    var paramId = msg.IntArgs[2];
                    if (!paramIdToIdx.TryGetValue(paramId, out var idx)) return;

                    var v = msg.FloatArgs.Length > 3 ? msg.FloatArgs[3] : 0f;
                    abletonControlPanel.SetMacroValue(idx, v);
                });
        }

        private void BuildControlPanel(Vector3 gridOrigin, Quaternion facing)
        {
            if (abletonControlPanel == null || abletonBridge == null) return;

            var tracks = abletonBridge.Tracks;
            if (tracks == null || tracks.Length == 0) return;

            var hSpacing = abletonControlPanel.TrackHorizontalSpacing;
            var vSpacing = abletonControlPanel.SceneVerticalSpacing;
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

            abletonControlPanel.Build(trackNames, panelPos, facing, panelWidth);
        }

        /// <summary>
        /// グリッド + コントロールパネル全体を囲む角丸フレームを生成する。
        /// マテリアル未設定時はスキップ。再Connect時は前のインスタンスを破棄。
        /// </summary>
        private void SpawnAbletonOuterFrame(Vector3 gridOrigin, Quaternion facing)
        {
            if (abletonOuterFrameMaterial == null) return;
            if (abletonBridge == null || abletonControlPanel == null) return;

            var tracks = abletonBridge.Tracks;
            if (tracks == null || tracks.Length == 0) return;

            var hSpacing = abletonControlPanel.TrackHorizontalSpacing;
            var vSpacing = abletonControlPanel.SceneVerticalSpacing;
            var controlPanelGap = vSpacing * 0.6f;

            var numTracks = tracks.Length;
            var numScenes = abletonBridge.NumScenes;

            // グリッドのローカル範囲 (origin が左下)
            var gridWidth = (numTracks - 1) * hSpacing + hSpacing;     // 左右余白込み
            var gridHeight = (numScenes - 1) * vSpacing + vSpacing;
            var gridCenterX = (numTracks - 1) * 0.5f * hSpacing;
            var gridCenterY = (numScenes - 1) * 0.5f * vSpacing;

            // コントロールパネル領域 (グリッド下、横はグリッド中心揃え)
            var ctrlWidth = Mathf.Max(0.4f, numTracks * hSpacing);
            var ctrlHeight = ctrlWidth * AbletonControlPanel.TextureAspectRatio;
            var ctrlCenterY = -controlPanelGap - ctrlHeight * 0.5f;

            // 全体バウンディング
            var totalLeft = Mathf.Min(-hSpacing * 0.5f, gridCenterX - ctrlWidth * 0.5f);
            var totalRight = Mathf.Max(gridCenterX + gridWidth * 0.5f, gridCenterX + ctrlWidth * 0.5f);
            var totalTop = gridCenterY + gridHeight * 0.5f;
            var totalBottom = ctrlCenterY - ctrlHeight * 0.5f;

            var bboxWidth = totalRight - totalLeft + abletonOuterFramePadding * 2f;
            var bboxHeight = totalTop - totalBottom + abletonOuterFramePadding * 2f;
            var bboxCenterLocal = new Vector3(
                (totalLeft + totalRight) * 0.5f,
                (totalTop + totalBottom) * 0.5f,
                abletonOuterFrameDepthOffset);

            if (_abletonOuterFrameInstance != null)
                Destroy(_abletonOuterFrameInstance);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "AbletonOuterFrame";
            var collider = frame.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // gridOrigin/facing をワールドアンカーとし、ローカルオフセットを加える
            var worldCenter = gridOrigin + facing * bboxCenterLocal;
            frame.transform.SetPositionAndRotation(worldCenter, facing);
            frame.transform.localScale = new Vector3(bboxWidth, bboxHeight, 1f);

            var renderer = frame.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Z 値が近接するため明示的に SortingOrder を負にして必ずクリップより手前ではなく
            // 後ろに描画 (Transparent Queue 内では SortingOrder が距離より優先される)
            renderer.sortingOrder = -10;

            var matInstance = new Material(abletonOuterFrameMaterial);
            // フレーム自身の Render Queue も明示的に下げる (Transparent=3000 → 2990)
            matInstance.renderQueue = 2990;
            matInstance.SetVector("_RectSize", new Vector4(bboxWidth, bboxHeight, 0f, 0f));
            matInstance.SetFloat("_CornerRadius", abletonOuterFrameCornerRadius);
            renderer.sharedMaterial = matInstance;

            _abletonOuterFrameInstance = frame;
        }
    }
}
