#nullable enable

using Rhizomode.Audio.Analysis;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Audio.GraphAdapter
{
    /// <summary>
    /// <see cref="AudioDriverHost"/> を MonoBehaviour 経由で Update tick で駆動する thin wrapper。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10D: 旧 ~150 行の AudioDriverBehaviour は <see cref="AudioDriverHost"/>
    /// に純粋ロジックを移送し、本 class は MonoBehaviour lifecycle (SerializeField の
    /// AudioAnalyzer + Update tick) のみ保持。Phase 5/12 で VContainer ITickable adapter に
    /// 置き換える際は本 MonoBehaviour を削除し、Bootstrap が AudioDriverHost を直接構築する。
    /// </remarks>
    public sealed class AudioDriverBehaviour : MonoBehaviour
    {
        [SerializeField] private AudioAnalyzer? audioAnalyzer;

        private AudioDriverHost? _host;

        /// <summary>AudioAnalyzer を外部から設定する (ランタイム生成時用)。</summary>
        public AudioAnalyzer? Analyzer
        {
            get => audioAnalyzer;
            set => audioAnalyzer = value;
        }

        /// <summary>
        /// 依存関係を設定し、内部の <see cref="AudioDriverHost"/> を生成する。
        /// </summary>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            if (audioAnalyzer == null)
            {
                Debug.LogWarning("[AudioDriverBehaviour] AudioAnalyzer is null at Initialize.");
                return;
            }
            _host = new AudioDriverHost(audioAnalyzer, graphContext);
        }

        private void Update()
        {
            // Phase 5/12 で本 Update() は ITickable.Tick() に移行予定。
            // _host が null (Initialize 未呼出 / Analyzer 未設定) なら no-op。
            _host?.Tick();
        }
    }
}
