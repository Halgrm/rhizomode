#nullable enable
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandbox.InstancedCubes
{
    /// <summary>
    /// Standalone GPU-instanced cube swarm (Boids + self-rotation).
    /// Drop on an empty GameObject in any scene, assign <see cref="compute"/> and <see cref="instanceShader"/>, press Play.
    /// Not wired to the rhizomode graph / module system — pure visual prototype.
    /// </summary>
    public sealed class InstancedCubeRenderer : MonoBehaviour
    {
        [Header("Required Assets")]
        [SerializeField] private ComputeShader? compute;
        [SerializeField] private Shader?        instanceShader;

        [Header("Swarm Size")]
        [SerializeField] private int   count           = 10000;
        [SerializeField] private float boundsRadius    = 25f;
        [SerializeField] private float initialSpread01 = 0.9f;

        [Header("Boids Tuning")]
        [SerializeField] private float maxSpeed           = 3f;
        [SerializeField] private float neighborRadius     = 3f;
        [SerializeField] private float separationDistance = 1.2f;
        [SerializeField] private float separationWeight   = 1.6f;
        [SerializeField] private float alignmentWeight    = 1.0f;
        [SerializeField] private float cohesionWeight     = 0.2f;
        [SerializeField] private float boundsWeight       = 3f;

        [Header("Visual")]
        [SerializeField] private float cubeScale     = 0.4f;
        [SerializeField] private Color baseColor     = new Color(0.9f, 0.9f, 1f, 1f);
        [SerializeField] private Color emissionColor = new Color(0.05f, 0.15f, 0.4f, 1f);

        [Header("Init Spin")]
        [SerializeField] private float maxAngularSpeed = 4f;

        private GraphicsBuffer? _boidBuffer;
        private GraphicsBuffer? _argsBuffer;
        private Material?       _material;
        private Mesh?           _cubeMesh;
        private int             _kernel;
        private int             _liveCount;

        private const int BoidStride = sizeof(float) * 13; // 3 + 3 + 4 + 3

        private static readonly int IdBoids              = Shader.PropertyToID("_Boids");
        private static readonly int IdCount              = Shader.PropertyToID("_Count");
        private static readonly int IdDeltaTime          = Shader.PropertyToID("_DeltaTime");
        private static readonly int IdNeighborRadius     = Shader.PropertyToID("_NeighborRadius");
        private static readonly int IdSeparationDistance = Shader.PropertyToID("_SeparationDistance");
        private static readonly int IdMaxSpeed           = Shader.PropertyToID("_MaxSpeed");
        private static readonly int IdSeparationWeight   = Shader.PropertyToID("_SeparationWeight");
        private static readonly int IdAlignmentWeight    = Shader.PropertyToID("_AlignmentWeight");
        private static readonly int IdCohesionWeight     = Shader.PropertyToID("_CohesionWeight");
        private static readonly int IdBoundsRadius       = Shader.PropertyToID("_BoundsRadius");
        private static readonly int IdBoundsWeight       = Shader.PropertyToID("_BoundsWeight");
        private static readonly int IdCenter             = Shader.PropertyToID("_Center");
        private static readonly int IdCubeScale          = Shader.PropertyToID("_CubeScale");
        private static readonly int IdBaseColor          = Shader.PropertyToID("_BaseColor");
        private static readonly int IdEmissionColor      = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            if (compute == null || instanceShader == null)
            {
                Debug.LogError("[InstancedCubeRenderer] compute or instanceShader is not assigned.", this);
                enabled = false;
                return;
            }

            _liveCount = Mathf.Max(1, count);
            _kernel    = compute.FindKernel("CSBoids");
            InitMesh();
            InitBoidBuffer();
            InitArgsBuffer();
            InitMaterial();
        }

        private void OnDisable()
        {
            _boidBuffer?.Release(); _boidBuffer = null;
            _argsBuffer?.Release(); _argsBuffer = null;
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
                _material = null;
            }
        }

        private void InitMesh()
        {
            _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (_cubeMesh == null)
            {
                // builtin path fallback (older Unity versions sometimes need this)
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = go.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }

        private void InitBoidBuffer()
        {
            var data = new BoidData[_liveCount];
            float spawnR = boundsRadius * Mathf.Clamp01(initialSpread01);
            for (int i = 0; i < _liveCount; i++)
            {
                data[i].position        = Random.insideUnitSphere * spawnR;
                data[i].velocity        = Random.insideUnitSphere * maxSpeed;
                var q = Random.rotationUniform;
                data[i].rotation        = new Vector4(q.x, q.y, q.z, q.w);
                data[i].angularVelocity = Random.insideUnitSphere * maxAngularSpeed;
            }

            _boidBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _liveCount, BoidStride);
            _boidBuffer.SetData(data);
        }

        private void InitArgsBuffer()
        {
            if (_cubeMesh == null) return;

            _argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            args[0].indexCountPerInstance = _cubeMesh.GetIndexCount(0);
            args[0].instanceCount         = (uint)_liveCount;
            args[0].startIndex            = _cubeMesh.GetIndexStart(0);
            args[0].baseVertexIndex       = _cubeMesh.GetBaseVertex(0);
            args[0].startInstance         = 0;
            _argsBuffer.SetData(args);
        }

        private void InitMaterial()
        {
            _material = new Material(instanceShader!) { name = "InstancedCubeIndirect (runtime)" };
            _material.SetBuffer(IdBoids, _boidBuffer);
        }

        private void Update()
        {
            if (compute == null || _boidBuffer == null || _argsBuffer == null || _material == null || _cubeMesh == null) return;

            compute.SetBuffer(_kernel, IdBoids, _boidBuffer);
            compute.SetInt   (IdCount,              _liveCount);
            compute.SetFloat (IdDeltaTime,          Time.deltaTime);
            compute.SetFloat (IdNeighborRadius,     neighborRadius);
            compute.SetFloat (IdSeparationDistance, separationDistance);
            compute.SetFloat (IdMaxSpeed,           maxSpeed);
            compute.SetFloat (IdSeparationWeight,   separationWeight);
            compute.SetFloat (IdAlignmentWeight,    alignmentWeight);
            compute.SetFloat (IdCohesionWeight,     cohesionWeight);
            compute.SetFloat (IdBoundsRadius,       boundsRadius);
            compute.SetFloat (IdBoundsWeight,       boundsWeight);
            compute.SetVector(IdCenter,             transform.position);

            int groups = Mathf.CeilToInt(_liveCount / 64f);
            compute.Dispatch(_kernel, groups, 1, 1);

            _material.SetFloat (IdCubeScale,     cubeScale);
            _material.SetColor (IdBaseColor,     baseColor);
            _material.SetColor (IdEmissionColor, emissionColor);
            // _Boids buffer binding is sticky on the material; re-bind once is enough.

            var rp = new RenderParams(_material)
            {
                worldBounds        = new Bounds(transform.position, Vector3.one * (boundsRadius * 4f)),
                shadowCastingMode  = ShadowCastingMode.Off,
                receiveShadows     = false,
                layer              = gameObject.layer,
            };
            Graphics.RenderMeshIndirect(rp, _cubeMesh, _argsBuffer, 1);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, boundsRadius);
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
