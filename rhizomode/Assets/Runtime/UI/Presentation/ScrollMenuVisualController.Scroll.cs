#nullable enable

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="ScrollMenuVisualController"/> の partial: スクロールパネル
    /// (WorldPanelHost + UIToolkit) の構築および遅延 populate。
    /// Phase 9 Round B で本体から分離。
    /// </summary>
    public partial class ScrollMenuVisualController
    {
        private ScrollPanelEntry CreateScrollPanel(CategoryBarEntry bar, List<NodeTypeInfo> nodes)
        {
            var go = new GameObject($"Scroll_{bar.Category}");
            go.transform.SetParent(transform, false);

            float rad = bar.AngleDeg * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * arcRadius;
            float z = Mathf.Cos(rad) * arcRadius;
            float baseY = barWorldHeight * 0.5f + scrollGapAboveBar;
            var baseLocalPos = new Vector3(x, baseY, z);

            go.transform.localPosition = baseLocalPos;
            go.transform.localRotation = Quaternion.Euler(0f, bar.AngleDeg, 0f);

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();

            var panelHost = go.AddComponent<WorldPanelHost>();
            if (panelSettingsTemplate != null)
                panelHost.PanelSettingsTemplate = panelSettingsTemplate;

            var rayBridge = go.AddComponent<WorldPanelRayBridge>();

            if (scrollUxml != null)
                panelHost.Initialize(scrollUxml, scrollStyleSheet, scrollTextureWidth, scrollTextureHeight);

            // rootVisualElementは同一フレームではnullの場合がある。遅延バインドする
            StartCoroutine(PopulateScrollWhenReady(panelHost, nodes));

            var collider = go.GetComponent<BoxCollider>();

            return new ScrollPanelEntry(go, panelHost, rayBridge, collider, baseLocalPos);
        }

        /// <summary>
        /// panelHost.Rootが利用可能になるまで待ち、ノードボタンを生成する。
        /// </summary>
        private IEnumerator PopulateScrollWhenReady(WorldPanelHost panelHost, List<NodeTypeInfo> nodes)
        {
            // rootVisualElementが準備できるまで最大10フレーム待機
            const int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                var root = panelHost.Root;
                if (root != null)
                {
                    PopulateNodeList(root, nodes);
                    yield break;
                }
                yield return null;
            }

            Debug.LogWarning("[ScrollMenu] Root was not ready after retry — scroll panel will be empty");
        }

        private void PopulateNodeList(VisualElement root, List<NodeTypeInfo> nodes)
        {
            var scrollBody = root.Q("scroll-body");
            var viewport = root.Q("node-viewport");
            var nodeList = root.Q("node-list");
            if (scrollBody == null || viewport == null || nodeList == null) return;

            const float itemHeight = 44f;
            float totalContentHeight = nodes.Count * itemHeight;

            // ノードボタン
            foreach (var info in nodes)
            {
                var typeName = info.TypeName;
                var button = new Button(() => OnNodeTypeSelected?.Invoke(typeName))
                {
                    text = info.DisplayName
                };
                button.AddToClassList("scroll-node-button");
                nodeList.Add(button);
            }

            // 右サイドバーにスライダーをC#で生成
            var sidebar = new VisualElement();
            sidebar.AddToClassList("scroll-sidebar");

            var slider = new Slider(0f, 1f, SliderDirection.Vertical);
            slider.value = 0f;
            slider.style.flexGrow = 1;

            slider.RegisterValueChangedCallback(evt =>
            {
                var viewportH = viewport.resolvedStyle.height;
                if (viewportH < 1f) viewportH = scrollTextureHeight;
                var maxScroll = Mathf.Max(0, totalContentHeight - viewportH);
                // スライダー上=0(先頭), 下=1(末尾) にするため反転
                var offset = (1f - evt.newValue) * maxScroll;
                nodeList.style.top = -offset;
            });

            sidebar.Add(slider);
            scrollBody.Add(sidebar);
        }
    }
}
