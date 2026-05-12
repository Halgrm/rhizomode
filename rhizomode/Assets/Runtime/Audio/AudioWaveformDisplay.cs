#nullable enable

using UnityEngine;

namespace Rhizomode.Audio
{
    /// <summary>
    /// CustomRenderTexture + CRT シェーダーで音声波形を GPU 描画する。
    /// 任意の GameObject に追加するだけで動作する（Quad メッシュを自動生成）。
    /// AudioAnalyzer がグローバルシェーダーバッファに配信するデータを表示する。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AudioWaveformDisplay : MonoBehaviour
    {
        [Header("表示設定")]
        [SerializeField] private int textureWidth = 512;
        [SerializeField] private int textureHeight = 128;
        [SerializeField] private Color lineColor = new(0.3f, 0.8f, 1f, 1f);
        [SerializeField, Range(0f, 4f)] private float intensity = 1.5f;
        [SerializeField, Range(0.001f, 0.05f)] private float lineWidth = 0.015f;

        [Header("シェーダー（未指定時は自動検索）")]
        [SerializeField] private Shader? crtShader;
        [SerializeField] private Shader? displayShader;

        private CustomRenderTexture? _crt;
        private Material? _crtMaterial;
        private Material? _displayMaterial;

        private void Awake()
        {
            SetupQuad();
            SetupCrt();
        }

        private void SetupQuad()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null)
                mf.sharedMesh = CreateQuadMesh();
        }

        private void SetupCrt()
        {
            // CRT シェーダー
            var shader = crtShader != null
                ? crtShader
                : Shader.Find("Rhizomode/CrtWaveform");

            if (shader == null)
            {
                Debug.LogError("[AudioWaveformDisplay] CrtWaveform shader not found");
                return;
            }

            _crtMaterial = new Material(shader);
            _crtMaterial.SetColor("_LineColor", lineColor);
            _crtMaterial.SetFloat("_Intensity", intensity);
            _crtMaterial.SetFloat("_Delta", lineWidth);

            // CustomRenderTexture
            _crt = new CustomRenderTexture(textureWidth, textureHeight, RenderTextureFormat.ARGB32);
            _crt.initializationMode = CustomRenderTextureUpdateMode.Realtime;
            _crt.updateMode = CustomRenderTextureUpdateMode.Realtime;
            _crt.material = _crtMaterial;

            // 表示マテリアル
            var dispShader = displayShader != null
                ? displayShader
                : Shader.Find("Universal Render Pipeline/Unlit");
            if (dispShader == null) dispShader = Shader.Find("Unlit/Texture");

            if (dispShader != null)
            {
                _displayMaterial = new Material(dispShader);
                _displayMaterial.mainTexture = _crt;

                var mr = GetComponent<MeshRenderer>();
                mr.material = _displayMaterial;
            }
        }

        private void OnValidate()
        {
            if (_crtMaterial == null) return;
            _crtMaterial.SetColor("_LineColor", lineColor);
            _crtMaterial.SetFloat("_Intensity", intensity);
            _crtMaterial.SetFloat("_Delta", lineWidth);
        }

        private void OnDestroy()
        {
            if (_crt != null)
            {
                _crt.Release();
                Destroy(_crt);
            }

            if (_crtMaterial != null) Destroy(_crtMaterial);
            if (_displayMaterial != null) Destroy(_displayMaterial);
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "AudioDisplayQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0)
                },
                uv = new[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
