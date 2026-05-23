#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="NodeVisualManager"/> の partial — 距離 LOD + Panel Budget (N5, 2026-05-16)。
    /// </summary>
    /// <remarks>
    /// 60 ノード時、各ノードが UIDocument / RenderTexture / PanelSettings / Material を作るため重い。
    /// 距離に応じて:
    /// <list type="bullet">
    /// <item>Near (&lt; <c>lodNearDistance</c>): full quality (textureWidth)</item>
    /// <item>Mid (&lt; <c>lodFarDistance</c>): mid resolution (<c>midLodTextureWidth</c>)</item>
    /// <item>Far (&gt;= <c>lodFarDistance</c>): UIDocument 無効化 + <c>farLodTextureWidth</c></item>
    /// </list>
    /// さらに「近い順に <c>maxActivePanels</c> 個」のソフト budget で活性を制限する。
    /// LOD 計算は <c>lodUpdateIntervalSec</c> 間隔で間引き、毎フレームのコストを抑える。
    /// </remarks>
    public partial class NodeVisualManager
    {
        private readonly List<(NodeVisualController Controller, float Distance)> _lodSortBuffer = new();

        private void Update()
        {
            if (!lodEnabled) return;
            if (Time.unscaledTime < _nextLodTime) return;
            _nextLodTime = Time.unscaledTime + lodUpdateIntervalSec;

            ApplyLod();
        }

        private void ApplyLod()
        {
            if (_visuals.Count == 0) return;
            var viewer = ResolveViewer();
            if (viewer == null) return;

            var viewerPos = viewer.position;

            _lodSortBuffer.Clear();
            foreach (var kvp in _visuals)
            {
                var ctrl = kvp.Value;
                if (ctrl == null) continue;
                var dist = Vector3.Distance(ctrl.transform.position, viewerPos);
                _lodSortBuffer.Add((ctrl, dist));
            }

            // 近い順にソート (budget 適用順)
            _lodSortBuffer.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            int activatedCount = 0;
            foreach (var (ctrl, dist) in _lodSortBuffer)
            {
                var host = ctrl.GetComponent<WorldPanelHost>();
                if (host == null) continue;

                // budget 越え or far 距離 → UI 無効化 (最後のフレームは Quad に残る)
                bool withinBudget = activatedCount < maxActivePanels;
                bool withinDistance = dist < lodFarDistance;
                bool uiActive = withinBudget && withinDistance;

                // cue 透明ノード fix: 初回 paint 未完了の panel を deactivate すると RT が
                // 空のまま固着 → 透明 quad に。少なくとも 1 回 layout+paint を経るまで強制 active。
                if (!uiActive && !host.HasRenderedAtLeastOnce)
                    uiActive = true;

                host.SetUIActive(uiActive);
                if (uiActive) activatedCount++;

                // UI 無効ノードに ChangeResolution を呼ぶと RT Release/Create で
                // 黒画面化し、UIDocument が再描画しないため最後のフレームが失われる。
                if (uiActive)
                {
                    int targetWidth = ResolveLodTextureWidth(dist);
                    host.ChangeResolution(targetWidth);
                }
            }
        }

        private int ResolveLodTextureWidth(float distance)
        {
            if (distance < lodNearDistance) return textureWidth;
            if (distance < lodFarDistance) return midLodTextureWidth;
            return farLodTextureWidth;
        }

        private Transform? ResolveViewer()
        {
            if (viewerOverride != null) return viewerOverride;
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        /// <summary>外部から viewer transform を注入する (Bootstrap で head transform を渡す用途)。</summary>
        public void SetViewer(Transform? viewer)
        {
            viewerOverride = viewer;
        }
    }
}
