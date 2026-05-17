#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Modules
{
    /// <summary>
    /// GPU-instanced cube swarm (Boids + self-rotation). Parameters: count, scale, bounds, colors, tuning.
    /// </summary>
    [PerformanceModule(NodeCategory.VFX, legacyTypeNamePrefix: "InstancedCubes_")]
    public sealed class InstancedCubesModule : MonoBehaviour, IPerformanceModule
    {
        [SerializeField] private ComputeShader? compute;
        [SerializeField] private Shader? shader;
        // M5: Resources.GetBuiltinResource("Cube.fbx") fallback は Player build で stripping される
        // ことがあるため、prefab で明示的に cube mesh asset を割り当てる。null なら Activate を skip。
        [SerializeField] private Mesh? cubeMesh;

        [Header("Runtime State")]
        [SerializeField] private float count = 5000f;
        [SerializeField] private float boundsRadius = 25f;
        [SerializeField] private float cubeScale = 0.4f;
        [SerializeField] private Color baseColor = new Color(0.9f, 0.9f, 1f, 1f);
        [SerializeField] private Color emissionColor = new Color(0.05f, 0.15f, 0.4f, 1f);

        // Boids tuning (not exposed as ports, module defaults)
        private const float MaxSpeed = 3f;
        private const float NeighborRadius = 3f;
        private const float SeparationDistance = 1.2f;
        private const float SeparationWeight = 1.6f;
        private const float AlignmentWeight = 1.0f;
        private const float CohesionWeight = 0.2f;
        private const float BoundsWeight = 3f;
        private const float MaxAngularSpeed = 4f;
        private const int MaxCount = 5000;
        private const int ThreadGroupSize = 64;

        private GraphicsBuffer? _boidBuffer;
        private GraphicsBuffer? _argsBuffer;
        private Material? _material;
        private int _kernel;
        private int _liveCount;
        private bool _initialized;

        // M2: struct field 増減で silent break しないよう Marshal.SizeOf<T>() で算出。
        // HLSL 側 (Assets/Sandbox/.../*.compute) の BoidData は float3+float3+float4+float3=52 bytes 想定。
        // Codex re-review (WARN 7): 想定 stride を const で残し、static ctor で実測値と照合する。
        private const int ExpectedBoidStride = sizeof(float) * 13;
        private static readonly int BoidStride = Marshal.SizeOf<BoidData>();

        static InstancedCubesModule()
        {
            if (BoidStride != ExpectedBoidStride)
            {
                Debug.LogError(
                    $"[InstancedCubesModule] BoidData stride mismatch: expected {ExpectedBoidStride} bytes " +
                    $"(float3+float3+float4+float3), got {BoidStride}. Compute shader と layout が乖離しています。");
            }
        }

        private static readonly int IdBoids = Shader.PropertyToID("_Boids");
        private static readonly int IdCount = Shader.PropertyToID("_Count");
        private static readonly int IdDeltaTime = Shader.PropertyToID("_DeltaTime");
        private static readonly int IdNeighborRadius = Shader.PropertyToID("_NeighborRadius");
        private static readonly int IdSeparationDistance = Shader.PropertyToID("_SeparationDistance");
        private static readonly int IdMaxSpeed = Shader.PropertyToID("_MaxSpeed");
        private static readonly int IdSeparationWeight = Shader.PropertyToID("_SeparationWeight");
        private static readonly int IdAlignmentWeight = Shader.PropertyToID("_AlignmentWeight");
        private static readonly int IdCohesionWeight = Shader.PropertyToID("_CohesionWeight");
        private static readonly int IdBoundsRadius = Shader.PropertyToID("_BoundsRadius");
        private static readonly int IdBoundsWeight = Shader.PropertyToID("_BoundsWeight");
        private static readonly int IdCenter = Shader.PropertyToID("_Center");
        private static readonly int IdCubeScale = Shader.PropertyToID("_CubeScale");
        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ModuleDefinition? definition;

        public string ModuleName => definition != null ? definition.moduleName : "InstancedCubes";

        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : (IReadOnlyList<ParamDefinition>)Array.Empty<ParamDefinition>();

        /// <summary>
        /// ランタイムから ModuleDefinition を設定する。Activate 前に呼ぶ想定。
        /// SO defaults を runtime field に反映し、SerializeField 値と SO の二重 source-of-truth を解消する (M1)。
        /// </summary>
        public void Initialize(ModuleDefinition def)
        {
            definition = def;
            ApplyDefaultsFromDefinition(def);
        }

        /// <summary>
        /// ModuleDefinition.parameters の defaultFloat / defaultColor / defaultBool を runtime field へ
        /// 反映する。Activate 直前に呼ばれるため、graph load で ConstFloat 接続前の初期描画は SO の意図する値で開始する。
        /// </summary>
        private void ApplyDefaultsFromDefinition(ModuleDefinition def)
        {
            foreach (var p in def.parameters)
            {
                switch (p.type)
                {
                    case ParamType.Float:
                        SetParam(p.name, p.defaultFloat);
                        break;
                    case ParamType.Color:
                        SetParam(p.name, (Color)p.defaultColor);
                        break;
                    case ParamType.Bool:
                        SetParam(p.name, p.defaultBool);
                        break;
                }
            }
        }

        public void SetParam(string paramName, object value)
        {
            try
            {
                switch (paramName)
                {
                    case "count":
                        count = Mathf.Clamp((float)value, 1, MaxCount);
                        ReallocateIfCountChanged();
                        break;
                    case "boundsRadius":
                        boundsRadius = Mathf.Max(0.1f, (float)value);
                        break;
                    case "cubeScale":
                        cubeScale = Mathf.Clamp((float)value, 0.01f, 2f);
                        break;
                    case "baseColor":
                        baseColor = (Color)value;
                        break;
                    case "emissionColor":
                        emissionColor = (Color)value;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InstancedCubesModule] SetParam {paramName} failed: {e.Message}");
            }
        }

        public void Activate()
        {
            if (_initialized) return;
            try
            {
                if (compute == null || shader == null)
                {
                    Debug.LogError("[InstancedCubesModule] compute or shader not assigned", this);
                    return;
                }
                if (cubeMesh == null)
                {
                    // M5: Player build で stripping され得る Resources fallback を撤去。prefab で明示割り当てが必須。
                    Debug.LogError("[InstancedCubesModule] cubeMesh not assigned. Set the Mesh field on the prefab.", this);
                    return;
                }

                _liveCount = Mathf.Max(1, Mathf.RoundToInt(count));
                _kernel = compute.FindKernel("CSBoids");
                InitBoidBuffer();
                InitArgsBuffer();
                InitMaterial();
                _initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InstancedCubesModule] Activate failed: {e}", this);
            }
        }

        public void Deactivate()
        {
            ReleaseBuffers();
            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
            _initialized = false;
        }

        /// <summary>
        /// 既存の GraphicsBuffer を解放する。Reallocate / Deactivate / OnDisable の共通処理。
        /// </summary>
        private void ReleaseBuffers()
        {
            _boidBuffer?.Release();
            _boidBuffer = null;
            _argsBuffer?.Release();
            _argsBuffer = null;
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void InitBoidBuffer()
        {
            // H1: 旧 buffer を確実に Release してから new 割り当て。
            _boidBuffer?.Release();
            _boidBuffer = null;

            var data = new BoidData[_liveCount];
            float spawnR = boundsRadius * 0.9f;
            for (int i = 0; i < _liveCount; i++)
            {
                data[i].position = UnityEngine.Random.insideUnitSphere * spawnR;
                data[i].velocity = UnityEngine.Random.insideUnitSphere * MaxSpeed;
                var q = UnityEngine.Random.rotationUniform;
                data[i].rotation = new Vector4(q.x, q.y, q.z, q.w);
                data[i].angularVelocity = UnityEngine.Random.insideUnitSphere * MaxAngularSpeed;
            }

            _boidBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _liveCount, BoidStride);
            _boidBuffer.SetData(data);
        }

        private void InitArgsBuffer()
        {
            if (cubeMesh == null) return;

            // H1: 旧 args buffer も Release。
            _argsBuffer?.Release();
            _argsBuffer = null;

            _argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            args[0].indexCountPerInstance = cubeMesh.GetIndexCount(0);
            args[0].instanceCount = (uint)_liveCount;
            args[0].startIndex = cubeMesh.GetIndexStart(0);
            args[0].baseVertexIndex = cubeMesh.GetBaseVertex(0);
            args[0].startInstance = 0;
            _argsBuffer.SetData(args);
        }

        private void InitMaterial()
        {
            _material = new Material(shader!) { name = "InstancedCubes (runtime)" };
            _material.SetBuffer(IdBoids, _boidBuffer);
        }

        private void ReallocateIfCountChanged()
        {
            if (!_initialized) return;
            int newCount = Mathf.RoundToInt(count);
            if (newCount == _liveCount) return;

            _liveCount = newCount;
            try
            {
                InitBoidBuffer();
                InitArgsBuffer();
                if (_material != null) _material.SetBuffer(IdBoids, _boidBuffer);
            }
            catch (Exception e)
            {
                Debug.LogError($"[InstancedCubesModule] Reallocation failed: {e}", this);
            }
        }

        private void LateUpdate()
        {
            if (!_initialized || compute == null || _boidBuffer == null || _argsBuffer == null
                || _material == null || cubeMesh == null)
                return;

            compute.SetBuffer(_kernel, IdBoids, _boidBuffer);
            compute.SetInt(IdCount, _liveCount);
            compute.SetFloat(IdDeltaTime, Time.deltaTime);
            compute.SetFloat(IdNeighborRadius, NeighborRadius);
            compute.SetFloat(IdSeparationDistance, SeparationDistance);
            compute.SetFloat(IdMaxSpeed, MaxSpeed);
            compute.SetFloat(IdSeparationWeight, SeparationWeight);
            compute.SetFloat(IdAlignmentWeight, AlignmentWeight);
            compute.SetFloat(IdCohesionWeight, CohesionWeight);
            compute.SetFloat(IdBoundsRadius, boundsRadius);
            compute.SetFloat(IdBoundsWeight, BoundsWeight);
            compute.SetVector(IdCenter, transform.position);

            int groups = Mathf.CeilToInt(_liveCount / (float)ThreadGroupSize);
            compute.Dispatch(_kernel, groups, 1, 1);

            _material.SetFloat(IdCubeScale, cubeScale);
            _material.SetColor(IdBaseColor, baseColor);
            _material.SetColor(IdEmissionColor, emissionColor);

            var rp = new RenderParams(_material)
            {
                worldBounds = new Bounds(transform.position, Vector3.one * (boundsRadius * 4f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMeshIndirect(rp, cubeMesh, _argsBuffer, 1);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BoidData
        {
            public Vector3 position;
            public Vector3 velocity;
            public Vector4 rotation;
            public Vector3 angularVelocity;
        }
    }
}
