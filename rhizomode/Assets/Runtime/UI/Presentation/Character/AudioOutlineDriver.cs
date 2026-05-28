#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Presentation.Character
{
    public sealed class AudioOutlineDriver : MonoBehaviour
    {
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private const int SampleCount = 1024;

        [SerializeField] private SkinnedMeshRenderer? body;
        [SerializeField] private MeshRenderer[]? attachments;
        [SerializeField, Min(0f)] private float baseWidth = 0.5f;
        [SerializeField, Min(0f)] private float audioGain = 5.0f;
        [SerializeField, Range(0f, 30f)] private float smoothing = 8f;

        private MaterialPropertyBlock _block = null!;
        private float _smoothedLevel;
        private float[] _samplesBuffer = null!;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _samplesBuffer = new float[SampleCount];
        }

        private void LateUpdate()
        {
            float level = GetRmsLevel();
            float coefficient = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _smoothedLevel = Mathf.Lerp(_smoothedLevel, level, coefficient);

            float width = baseWidth + _smoothedLevel * audioGain;
            ApplyToRenderers(width);
        }

        private float GetRmsLevel()
        {
            AudioListener.GetOutputData(_samplesBuffer, 0);

            float sum = 0f;
            for (int i = 0; i < _samplesBuffer.Length; i++)
            {
                sum += _samplesBuffer[i] * _samplesBuffer[i];
            }

            return Mathf.Sqrt(sum / _samplesBuffer.Length);
        }

        private void ApplyToRenderers(float width)
        {
            ApplyToRenderer(body, width);

            if (attachments == null)
            {
                return;
            }

            foreach (MeshRenderer attachment in attachments)
            {
                ApplyToRenderer(attachment, width);
            }
        }

        private void ApplyToRenderer(Renderer? renderer, float width)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(_block);
            _block.SetFloat(OutlineWidthId, width);
            renderer.SetPropertyBlock(_block);
        }
    }
}
