#nullable enable

using UnityEngine;

namespace Rhizomode.Modules.Ferrofluid
{
    /// <summary>
    /// 個別の磁性流体球体。Spawner から spawn される。
    /// </summary>
    /// <remarks>
    /// Phase B: position / scale を保持するだけ。
    /// Phase C で attractor / velocity / wave intensity を受け取って動く。
    /// </remarks>
    public sealed class FerrofluidBall : MonoBehaviour
    {
        public float BaseSize { get; private set; }
        public Vector3 Velocity { get; set; }
        public float WaveIntensity { get; set; }

        /// <summary>Wave trigger 時に attract される世界座標 (Spawner が設定)。</summary>
        public Vector3? AttractorWorldPos { get; set; }

        /// <summary>Wave 振幅の現在値。trigger で 1 に跳ね上がって exponential 減衰。</summary>
        private float _waveEnvelope;

        /// <summary>OutlineFX のパルス強度 (rim intensity を一時的に上げる)。</summary>
        private float _outlineFx;

        private MaterialPropertyBlock? _mpb;
        private MeshRenderer? _renderer;
        private Vector3 _attractorBias;

        private static readonly int DisplacementId = Shader.PropertyToID("_Displacement");
        private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        private const float BaseRimIntensity = 12f;
        private const float OutlineFxBoost = 80f; // パルス時の追加 intensity (派手に)
        private const float WaveDecayPerSec = 0.6f; // 半減期 ~1.2s (波立ちはゆっくり)
        private const float OutlineDecayPerSec = 0.5f; // 半減期 ~1.4s (リム閃光もゆっくり)
        private const float AttractorPullStrength = 6f;

        private static readonly Color BaseRimColor = new Color(0.1f, 0.4f, 1.5f, 1f);
        private static readonly Color OutlineFxFlashColor = new Color(1.0f, 1.5f, 2.5f, 1f);

        public void Initialize(float size)
        {
            BaseSize = size;
        }

        /// <summary>Wave trigger: vertex displacement を立ち上げて減衰。</summary>
        public void TriggerWave()
        {
            _waveEnvelope = 1f;
        }

        /// <summary>Random Move trigger: ランダム方向に velocity を付与。</summary>
        public void TriggerRandomMove(float speed)
        {
            // 球面一様分布
            var dir = UnityEngine.Random.onUnitSphere;
            Velocity += dir * speed;
        }

        /// <summary>Outline FX trigger: rim を一瞬強く光らせる。</summary>
        public void TriggerOutlineFx()
        {
            _outlineFx = 1f;
        }

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (_renderer == null || _mpb == null) return;

            var dt = Time.deltaTime;

            // Wave envelope 減衰
            _waveEnvelope = Mathf.Max(0f, _waveEnvelope - dt * WaveDecayPerSec);
            WaveIntensity = _waveEnvelope;

            // Outline pulse 減衰
            _outlineFx = Mathf.Max(0f, _outlineFx - dt * OutlineDecayPerSec);

            // Attractor 引力 (wave 中だけ作用)
            if (AttractorWorldPos.HasValue && _waveEnvelope > 0.01f)
            {
                var toAttractor = AttractorWorldPos.Value - transform.position;
                _attractorBias = toAttractor.normalized * AttractorPullStrength * _waveEnvelope;
                Velocity += _attractorBias * dt;
            }

            // Velocity 適用 + drag (half-life ~0.35s)
            if (Velocity.sqrMagnitude > 1e-6f)
            {
                transform.localPosition += Velocity * dt;
                Velocity *= Mathf.Exp(-dt * 2.0f);
            }

            // Material property を MPB 経由で per-ball 設定 (material 共有のため)
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(DisplacementId, WaveIntensity);
            _mpb.SetFloat(RimIntensityId, BaseRimIntensity + OutlineFxBoost * _outlineFx);
            // OutlineFx 中は rim color を白寄りに lerp (リム閃光を視覚的に強調)
            _mpb.SetColor(RimColorId, Color.Lerp(BaseRimColor, OutlineFxFlashColor, _outlineFx));
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
