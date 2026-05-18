#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Rhizomode.Cameras;
using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="ScrollMenuVisualController"/> の partial: カテゴリバー (Quad + TextMesh) の構築。
    /// Phase 9 Round B で本体から分離。
    /// </summary>
    public partial class ScrollMenuVisualController
    {
        private void CreateCategoryBars()
        {
            var active = new List<CategoryDefinition>();
            foreach (var c in categoryDefinitions)
            {
                if (_typeRegistry != null && _typeRegistry.GetByCategory(c.category).Any())
                    active.Add(c);
            }

            int count = active.Count;
            if (count == 0) return;

            float startAngle = -arcSpanDegrees * 0.5f;
            float step = count > 1 ? arcSpanDegrees / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angleDeg = startAngle + step * i;
                var def = active[i];
                Debug.Log($"[ScrollMenu] CreateBar: {def.category} label=\"{def.label}\" angle={angleDeg:F1}");
                _bars.Add(CreateBar(def.category, def.label, def.accent, angleDeg));
            }
        }

        private CategoryBarEntry CreateBar(NodeCategory category, string label, Color accent, float angleDeg)
        {
            var go = new GameObject($"Bar_{category}");
            go.transform.SetParent(transform, false);

            float rad = angleDeg * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(Mathf.Sin(rad) * arcRadius, 0f, Mathf.Cos(rad) * arcRadius);
            go.transform.localRotation = Quaternion.Euler(0f, angleDeg, 0f);
            go.transform.localScale = new Vector3(barWorldWidth, barWorldHeight, 1f);

            // 共有Quadメッシュ
            var mf = go.AddComponent<MeshFilter>();
            if (SharedQuadMesh == null) SharedQuadMesh = CreateQuadMesh();
            mf.sharedMesh = SharedQuadMesh;

            // Unlitマテリアル（バーごとに1つ、軽量）
            var mr = go.AddComponent<MeshRenderer>();
            if (CachedUnlitShader == null)
                CachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            var baseColor = new Color(0f, 0f, 0f, 0.7f);
            var mat = new Material(CachedUnlitShader!);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.color = baseColor;
            mr.material = mat;

            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(1f, 1f, 0.01f);

            // 左端にアクセントカラーの小さいバーを子として追加
            CreateAccentStrip(go.transform, accent);

            // テキストラベル（テクスチャ生成）
            CreateBarLabel(go.transform, label);

            var highlightColor = new Color(
                Mathf.Lerp(baseColor.r, accent.r, 0.6f),
                Mathf.Lerp(baseColor.g, accent.g, 0.6f),
                Mathf.Lerp(baseColor.b, accent.b, 0.6f),
                0.9f);

            // Accent / Label の子 GameObject も含めて MirrorHidden layer に揃える。
            MirrorHiddenLayer.ApplyRecursive(go);

            return new CategoryBarEntry(category, go, col, mat, baseColor, highlightColor, angleDeg);
        }

        private static void CreateAccentStrip(Transform parent, Color color)
        {
            var strip = new GameObject("Accent");
            strip.transform.SetParent(parent, false);
            // 左端に細い帯（親のローカル空間は-0.5..0.5）
            strip.transform.localPosition = new Vector3(-0.48f, 0f, -0.001f);
            strip.transform.localScale = new Vector3(0.04f, 0.8f, 1f);

            var mf = strip.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedQuadMesh;

            var mr = strip.AddComponent<MeshRenderer>();
            var mat = new Material(CachedUnlitShader!);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3001;
            mat.color = color;
            mr.material = mat;
        }

        /// <summary>TextMeshでバーにラベルを追加（親の非一様スケールを打ち消す）。</summary>
        private void CreateBarLabel(Transform parent, string text)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);

            // 親スケール(barWorldWidth, barWorldHeight, 1)を打ち消して均一ワールドスケールにする
            float uniformSize = 0.001f;
            labelGo.transform.localScale = new Vector3(
                uniformSize / barWorldWidth,
                uniformSize / barWorldHeight,
                uniformSize);
            labelGo.transform.localPosition = new Vector3(0.05f, 0f, -0.002f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 100;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 1f, 1f, 0.75f);
            tm.fontStyle = FontStyle.Normal;

            Debug.Log($"[ScrollMenu] Label created: \"{text}\" go={labelGo.name} parent={parent.name}");
        }

        private static Mesh CreateQuadMesh()
        {
            return new Mesh
            {
                name = "ScrollMenu_Quad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward }
            };
        }
    }
}
