#nullable enable

using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// Miniature 編集中に実空間の原点 + XYZ 軸 + Y=0 床グリッドを縮小表示する pure C# renderer。
    /// 「この小球は実空間で原点からどのくらい離れているか」を視覚化する基準。
    /// 生成した GameObject root を返し、破棄は caller (PathEditSession) が担当する。
    /// </summary>
    public sealed class CoordinateReferenceRenderer
    {
        private const float CoordAxisRealLength = 1.5f; // 実空間で 1.5m 分の軸
        private const float CoordAxisLineWidth = 0.020f; // 2cm
        private const float CoordOriginRadiusMultiplier = 1.2f;
        private const float GridRealHalfExtent = 3f; // 実空間 ±3m
        private const float GridRealCellSize = 1f;
        private const float GridLineWidth = 0.012f; // 1.2cm
        private const float GridMajorLineWidth = 0.018f; // 1.8cm

        private static readonly Color CoordOriginColor = new Color(1f, 1f, 0.4f, 1f);
        private static readonly Color CoordXColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color CoordYColor = new Color(0.3f, 1f, 0.3f, 1f);
        private static readonly Color CoordZColor = new Color(0.3f, 0.55f, 1f, 1f);
        private static readonly Color GridColor = new Color(0.7f, 0.75f, 0.8f, 0.85f);
        private static readonly Color GridMajorColor = new Color(0.95f, 0.95f, 1f, 1f);

        /// <summary>
        /// 実空間の原点 + XYZ 軸 + 床グリッドを miniature 空間に縮小表示する。
        /// </summary>
        public GameObject Render(MiniaturePathMapper mapper, float handleRadius)
        {
            var root = new GameObject("MiniatureCoordRef");
            var miniOrigin = mapper.ToMini(Vector3.zero);

            CreateOriginMarker(root.transform, miniOrigin, handleRadius);

            CreateCoordAxis(root.transform, mapper, "AxisX", miniOrigin, Vector3.right, CoordXColor);
            CreateCoordAxis(root.transform, mapper, "AxisY", miniOrigin, Vector3.up, CoordYColor);
            CreateCoordAxis(root.transform, mapper, "AxisZ", miniOrigin, Vector3.forward, CoordZColor);

            CreateGrid(root.transform, mapper);

            return root;
        }

        private static void CreateOriginMarker(Transform parent, Vector3 miniOrigin, float handleRadius)
        {
            var originGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            originGo.name = "CoordOrigin";
            originGo.transform.SetParent(parent, false);
            originGo.transform.position = miniOrigin;
            originGo.transform.localScale =
                Vector3.one * (handleRadius * CoordOriginRadiusMultiplier * 2f);
            var collider = originGo.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider); // ハンドルと誤グラブしないように
            var renderer = originGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateLineMaterial(CoordOriginColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void CreateCoordAxis(
            Transform parent,
            MiniaturePathMapper mapper,
            string name,
            Vector3 miniOrigin,
            Vector3 realDirection,
            Color color)
        {
            var realEnd = realDirection * CoordAxisRealLength;
            var miniEnd = mapper.ToMini(realEnd);
            CreateLine(parent, name, miniOrigin, miniEnd, color, CoordAxisLineWidth);
        }

        private static void CreateGrid(Transform parent, MiniaturePathMapper mapper)
        {
            int halfCount = Mathf.Max(1, Mathf.RoundToInt(GridRealHalfExtent / GridRealCellSize));
            for (int i = -halfCount; i <= halfCount; i++)
            {
                bool isMajor = i == 0;
                var color = isMajor ? GridMajorColor : GridColor;
                var width = isMajor ? GridMajorLineWidth : GridLineWidth;
                float c = i * GridRealCellSize;
                CreateLine(parent, $"GridZ_{i}",
                    mapper.ToMini(new Vector3(c, 0, -GridRealHalfExtent)),
                    mapper.ToMini(new Vector3(c, 0, GridRealHalfExtent)),
                    color, width);
                CreateLine(parent, $"GridX_{i}",
                    mapper.ToMini(new Vector3(-GridRealHalfExtent, 0, c)),
                    mapper.ToMini(new Vector3(GridRealHalfExtent, 0, c)),
                    color, width);
            }
        }

        private static void CreateLine(
            Transform parent, string name,
            Vector3 startMini, Vector3 endMini,
            Color color, float width)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, startMini);
            lr.SetPosition(1, endMini);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.material = CreateLineMaterial(color);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        /// <summary>
        /// URP/Unlit シェーダは LineRenderer の頂点カラーを無視するので、
        /// マテリアルの _BaseColor / _Color を明示的に塗っておく。
        /// </summary>
        internal static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}
