#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Modules.Ferrofluid
{
    /// <summary>
    /// 磁性流体の球体プールを管理する MonoBehaviour。
    /// </summary>
    /// <remarks>
    /// Phase B: Count 指定で N 個の sphere を spawn / despawn。同一 material を共有するため
    /// GPU instancing で batch される。各球体は <see cref="FerrofluidBall"/> を持ち、
    /// Phase C で attractor / random velocity を受け取る。
    ///
    /// VFX Graph ではなく GameObject pool を採用した理由:
    /// - .vfx 資産をプログラム生成するのは困難
    /// - 各球体に独立した state (velocity / attractor 影響) を持たせる必要がある
    /// - Unity の SRP batcher / instancing が material 同一なら自動でまとめる
    /// </remarks>
    public sealed class FerrofluidBallSpawner : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField, Range(1, 100)] private int count = 8;
        [SerializeField] private Mesh? sphereMesh;
        [SerializeField] private Material? ballMaterial;

        [Header("Spawn Distribution")]
        [SerializeField, Range(0.1f, 20f)] private float spawnRadius = 3f;
        [SerializeField, Range(0.05f, 2f)] private float minBallSize = 0.4f;
        [SerializeField, Range(0.1f, 4f)] private float maxBallSize = 1.2f;
        [SerializeField] private int randomSeed = 42;

        [Header("Triggers")]
        [Tooltip("Wave trigger 時に球体が引き寄せられる target。null なら spawner 自身の位置。")]
        [SerializeField] private Transform? attractorTarget;
        [Tooltip("RandomMove trigger 時に各球体に付与する初速度の大きさ。")]
        [SerializeField, Range(0.1f, 20f)] private float randomMoveSpeed = 5f;

        private readonly List<FerrofluidBall> _balls = new();

        public IReadOnlyList<FerrofluidBall> Balls => _balls;

        public int Count
        {
            get => count;
            set
            {
                var clamped = Mathf.Clamp(value, 0, 200);
                if (clamped == count) return;
                count = clamped;
                if (isActiveAndEnabled) Rebuild();
            }
        }

        /// <summary>Wave trigger: 全球体の vertex displacement を立ち上げ、attractor へ引き寄せる。</summary>
        [ContextMenu("Trigger Wave")]
        public void TriggerWave()
        {
            var attractorPos = attractorTarget != null ? attractorTarget.position : transform.position;
            foreach (var b in _balls)
            {
                if (b == null) continue;
                b.AttractorWorldPos = attractorPos;
                b.TriggerWave();
            }
        }

        /// <summary>RandomMove trigger: 全球体にランダム方向の初速度を付与。</summary>
        [ContextMenu("Trigger Random Move")]
        public void TriggerRandomMove()
        {
            foreach (var b in _balls)
            {
                if (b == null) continue;
                b.TriggerRandomMove(randomMoveSpeed);
            }
        }

        /// <summary>OutlineFX trigger: 全球体の rim を一瞬強く光らせる。</summary>
        [ContextMenu("Trigger Outline FX")]
        public void TriggerOutlineFx()
        {
            foreach (var b in _balls)
            {
                if (b == null) continue;
                b.TriggerOutlineFx();
            }
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ClearAll();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                Rebuild();
        }

        private void Rebuild()
        {
            ClearAll();
            var rng = new System.Random(randomSeed);
            for (int i = 0; i < count; i++)
            {
                var ball = CreateBall(i, rng);
                _balls.Add(ball);
            }
        }

        private FerrofluidBall CreateBall(int index, System.Random rng)
        {
            var go = new GameObject($"Ball_{index:D2}");
            go.transform.SetParent(transform, worldPositionStays: false);

            // 球面内のランダム位置 (uniform distribution in sphere)
            var u = (float)rng.NextDouble();
            var v = (float)rng.NextDouble();
            var w = (float)rng.NextDouble();
            var theta = 2f * Mathf.PI * u;
            var phi = Mathf.Acos(2f * v - 1f);
            var r = spawnRadius * Mathf.Pow(w, 1f / 3f);
            go.transform.localPosition = new Vector3(
                r * Mathf.Sin(phi) * Mathf.Cos(theta),
                r * Mathf.Sin(phi) * Mathf.Sin(theta),
                r * Mathf.Cos(phi));

            // サイズ random
            var size = Mathf.Lerp(minBallSize, maxBallSize, (float)rng.NextDouble());
            go.transform.localScale = Vector3.one * size;

            // Mesh / Material 付与
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = sphereMesh != null ? sphereMesh : GetDefaultSphereMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ballMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // FerrofluidBall コンポーネント (Phase C で attractor / velocity を受ける)
            var ball = go.AddComponent<FerrofluidBall>();
            ball.Initialize(size);
            return ball;
        }

        private void ClearAll()
        {
            foreach (var ball in _balls)
            {
                if (ball != null)
                {
                    if (Application.isPlaying) Destroy(ball.gameObject);
                    else DestroyImmediate(ball.gameObject);
                }
            }
            _balls.Clear();
        }

        private static Mesh? _defaultSphereMesh;
        private static Mesh GetDefaultSphereMesh()
        {
            if (_defaultSphereMesh != null) return _defaultSphereMesh;
            // Unity のデフォルト sphere mesh を取得
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _defaultSphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(tmp);
            else DestroyImmediate(tmp);
            return _defaultSphereMesh!;
        }
    }
}
