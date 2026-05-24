#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// <c>INdiReceiverNode</c> に対応する VR visual presenter。
    /// </summary>
    /// <remarks>
    /// Klak.NDI 依存は本 class に閉じる (KLAK_NDI define で条件コンパイル)。NDI 未インストール時は
    /// 何もせず壊さない。node の <see cref="INdiReceiverNode.SourceName"/> を観測し、
    /// Klak.Ndi.NdiReceiver の ndiName を同期する。SourceName が空のときは <c>Klak.Ndi.NdiFinder</c>
    /// から auto-pick (他 node と衝突しないよう process-static set で round-robin)。
    ///
    /// 表示: 子 GameObject に Quad mesh を生成し、Klak.Ndi.NdiReceiver.targetRenderer に
    /// その MeshRenderer を流す。NdiReceiver の Update() が MaterialPropertyBlock で
    /// 受信フレームを Quad のマテリアルに毎フレーム blit する。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NdiReceiverPresenter : MonoBehaviour
    {
        private const string MainTexProperty = "_BaseMap"; // URP Unlit の main texture property
        private const float PreviewWorldWidth = 0.32f;
        private const float PreviewWorldHeight = 0.18f; // 16:9
        private const float PreviewYOffset = -0.12f; // node panel の下に並べる
        private const float SourceAutoPickPollSec = 1.5f;

        // 全 presenter で共有: SourceName auto-pick 時に他 node と被らないようにするための予約 set。
        // process-static (AppDomain 生存) — シーン遷移を跨いで生存するが、Disable/Destroy で release する。
        private static readonly HashSet<string> _claimedSources = new();

        private INdiReceiverNode? _node;
        private GameObject? _previewQuad;
        private MeshRenderer? _previewRenderer;
        private Material? _previewMaterial;

#if KLAK_NDI
        private Klak.Ndi.NdiReceiver? _receiver;
        private Klak.Ndi.NdiResources? _ndiResources;
#endif

        private float _nextAutoPickAt;
        private string _claimedSourceName = "";
        private static Shader? CachedUnlitShader;
        private static Mesh? SharedPreviewQuad;

        /// <summary>presenter を起動。<see cref="Detach"/> されるまで receiver を維持する。</summary>
        public void Attach(INdiReceiverNode node)
        {
            if (_node != null) return;
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _node.OnSourceNameChanged += HandleSourceNameChanged;

            CreatePreviewQuad();
#if KLAK_NDI
            CreateReceiver();
            ApplySourceNameToReceiver(_node.SourceName);
#else
            Debug.LogWarning("[NdiReceiverPresenter] KLAK_NDI define not set. NDI receive disabled.");
#endif
        }

        /// <summary>presenter を停止。receiver / preview Quad / Material を破棄する。</summary>
        public void Detach()
        {
            if (_node != null)
            {
                _node.OnSourceNameChanged -= HandleSourceNameChanged;
                _node = null;
            }

            ReleaseClaim();

#if KLAK_NDI
            if (_receiver != null)
            {
                Destroy(_receiver);
                _receiver = null;
            }
#endif
            if (_previewMaterial != null) { Destroy(_previewMaterial); _previewMaterial = null; }
            if (_previewQuad != null) { Destroy(_previewQuad); _previewQuad = null; }
            _previewRenderer = null;
        }

        private void OnDestroy() => Detach();

        private void Update()
        {
#if KLAK_NDI
            if (_node == null || _receiver == null) return;
            if (!string.IsNullOrEmpty(_node.SourceName)) return; // user / load 指定済 → auto-pick しない
            if (Time.unscaledTime < _nextAutoPickAt) return;
            _nextAutoPickAt = Time.unscaledTime + SourceAutoPickPollSec;

            var picked = PickFreeSource();
            if (!string.IsNullOrEmpty(picked))
            {
                Claim(picked);
                _node.SetSourceName(picked); // event → HandleSourceNameChanged → receiver.ndiName
            }
#endif
        }

        private void HandleSourceNameChanged(string newName)
        {
#if KLAK_NDI
            ApplySourceNameToReceiver(newName);
#endif
        }

#if KLAK_NDI
        private void ApplySourceNameToReceiver(string name)
        {
            if (_receiver == null) return;
            try { _receiver.ndiName = name ?? ""; }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] ndiName set failed: {e.Message}");
            }
        }

        private void CreateReceiver()
        {
            _ndiResources = ResolveNdiResources();
            if (_ndiResources == null)
            {
                Debug.LogWarning("[NdiReceiverPresenter] NdiResources asset not found. NDI receive disabled.");
                return;
            }

            try
            {
                _receiver = gameObject.AddComponent<Klak.Ndi.NdiReceiver>();
                _receiver.SetResources(_ndiResources);
                if (_previewRenderer != null)
                {
                    _receiver.targetRenderer = _previewRenderer;
                    _receiver.targetMaterialProperty = MainTexProperty;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] AddComponent NdiReceiver failed: {e.Message}");
            }
        }

        private static Klak.Ndi.NdiResources? ResolveNdiResources()
        {
            var loaded = Resources.FindObjectsOfTypeAll<Klak.Ndi.NdiResources>();
            if (loaded.Length > 0) return loaded[0];

#if UNITY_EDITOR
            const string ndiResourcesGuid = "69304b86950074db7ba8caba75214004";
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(ndiResourcesGuid);
            if (!string.IsNullOrEmpty(path))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Klak.Ndi.NdiResources>(path);
#endif
            return null;
        }

        private static string? PickFreeSource()
        {
            try
            {
                foreach (var src in Klak.Ndi.NdiFinder.sourceNames)
                {
                    if (string.IsNullOrEmpty(src)) continue;
                    if (_claimedSources.Contains(src)) continue;
                    return src;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] NdiFinder enumeration failed: {e.Message}");
            }
            return null;
        }
#endif

        private void Claim(string sourceName)
        {
            ReleaseClaim();
            if (string.IsNullOrEmpty(sourceName)) return;
            _claimedSourceName = sourceName;
            _claimedSources.Add(sourceName);
        }

        private void ReleaseClaim()
        {
            if (!string.IsNullOrEmpty(_claimedSourceName))
            {
                _claimedSources.Remove(_claimedSourceName);
                _claimedSourceName = "";
            }
        }

        private void CreatePreviewQuad()
        {
            if (_previewQuad != null) return;
            _previewQuad = new GameObject("NdiReceiver_Preview");
            _previewQuad.transform.SetParent(transform, worldPositionStays: false);
            _previewQuad.transform.localPosition = new Vector3(0f, PreviewYOffset, 0f);
            _previewQuad.transform.localRotation = Quaternion.identity;
            _previewQuad.transform.localScale = new Vector3(PreviewWorldWidth, PreviewWorldHeight, 1f);

            var mf = _previewQuad.AddComponent<MeshFilter>();
            mf.sharedMesh = GetSharedPreviewQuad();

            _previewRenderer = _previewQuad.AddComponent<MeshRenderer>();
            _previewMaterial = new Material(GetUnlitShader());
            // 受信前のプレビューが真っ黒に見えないよう薄いグレーを下地に
            _previewMaterial.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.10f, 1f));
            _previewRenderer.sharedMaterial = _previewMaterial;
        }

        private static Mesh GetSharedPreviewQuad()
        {
            if (SharedPreviewQuad != null) return SharedPreviewQuad;
            SharedPreviewQuad = new Mesh
            {
                name = "NdiReceiver_PreviewQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                normals = new[]
                {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward
                }
            };
            return SharedPreviewQuad;
        }

        private static Shader GetUnlitShader()
        {
            if (CachedUnlitShader != null) return CachedUnlitShader;
            CachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Unlit/Texture");
            return CachedUnlitShader!;
        }
    }
}
