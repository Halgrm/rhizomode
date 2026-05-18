#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// VR で空中配置する LookAt marker (<see cref="LookAtMarkerVisual"/>) の生成・破棄を管理する MonoBehaviour facade。
    /// CameraManagerPanel の "Place LookAt" / "Edit LookAt" toggle から駆動される。
    /// </summary>
    /// <remarks>
    /// Phase 2-A (2026-05-18): <see cref="PathControlPointVisualManager"/> と同設計で、
    /// place mode と edit mode を分離する。
    /// - <see cref="IsPlacing"/>: 次の Right-Select で marker を空中生成
    /// - <see cref="IsEditing"/>: 既存 marker を Right-Grip で掴んで移動
    /// 永続化は Path Phase 4 と同タイミングで導入予定 (現状はランタイムのみ)。
    /// </remarks>
    public class LookAtMarkerVisualManager : MonoBehaviour
    {
        private const float DefaultHandleRadius = 0.06f;
        private const string DefaultNamePrefix = "LookAt";

        [SerializeField, Tooltip("配置するハンドル球の半径 (m)")]
        private float handleRadius = DefaultHandleRadius;

        [SerializeField, Tooltip("ハンドル球用 Material。null なら Sphere primitive のデフォルトを使う。")]
        private Material? handleMaterial;

        private readonly List<LookAtMarkerVisual> _visuals = new();
        private int _spawnCounter;

        /// <summary>Place mode 中かどうか (次の Right-Select で marker 生成)。</summary>
        public bool IsPlacing { get; private set; }

        /// <summary>Edit mode 中かどうか (既存 marker を grab 可能)。</summary>
        public bool IsEditing { get; private set; }

        public IReadOnlyList<LookAtMarkerVisual> Visuals => _visuals;

        public void BeginPlacing() => IsPlacing = true;
        public void EndPlacing() => IsPlacing = false;
        public void BeginEditing() => IsEditing = true;
        public void EndEditing() => IsEditing = false;

        /// <summary>
        /// 指定座標に新規 marker を生成する。Place handler が raycast hit / 前方一定距離で位置を決めて呼ぶ。
        /// </summary>
        public LookAtMarkerVisual Spawn(Vector3 worldPosition)
        {
            _spawnCounter++;
            var name = $"{DefaultNamePrefix}_{_spawnCounter:D2}";

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * (handleRadius * 2f);
            MirrorHiddenLayer.ApplyRecursive(go);

            if (handleMaterial != null)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = handleMaterial;
            }

            var visual = go.AddComponent<LookAtMarkerVisual>();
            visual.Initialize(name);
            _visuals.Add(visual);
            return visual;
        }

        /// <summary>指定 marker を破棄する (CameraManagerPanel の delete 等から呼ぶ予定)。</summary>
        public bool Despawn(LookAtMarkerVisual visual)
        {
            if (visual == null) return false;
            if (!_visuals.Remove(visual)) return false;
            UnityEngine.Object.Destroy(visual.gameObject);
            return true;
        }

        /// <summary>Right-grip での grab 経路用。当該 Collider が管理下の marker なら Visual を返す。</summary>
        public LookAtMarkerVisual? GetVisualByCollider(Collider collider)
        {
            foreach (var v in _visuals)
            {
                if (v == null) continue;
                if (v.GetComponent<Collider>() == collider) return v;
            }
            return null;
        }

        private void OnDestroy()
        {
            foreach (var v in _visuals)
            {
                if (v != null && v.gameObject != null)
                    UnityEngine.Object.Destroy(v.gameObject);
            }
            _visuals.Clear();
        }
    }
}
