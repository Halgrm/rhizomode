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
    ///
    /// Codex Phase 10 review Axis 5 fix: 旧 AudioDriverBehaviour は Update() 内で
    /// audioAnalyzer + _graphContext の両方を毎フレーム guard していたため、Initialize() より
    /// 後に audioAnalyzer が SerializeField 経由で割り当てられても動作した。本実装も
    /// late-binding を維持するため、_host の構築は Update 内で lazy に行う。
    /// </remarks>
    public sealed class AudioDriverBehaviour : MonoBehaviour
    {
        [SerializeField] private AudioAnalyzer? audioAnalyzer;

        private GraphContextBehaviour? _graphContext;
        private AudioDriverHost? _host;

        /// <summary>AudioAnalyzer を外部から設定する (ランタイム生成時用)。</summary>
        public AudioAnalyzer? Analyzer
        {
            get => audioAnalyzer;
            set
            {
                if (!ReferenceEquals(audioAnalyzer, value))
                {
                    audioAnalyzer = value;
                    // Analyzer 切替時に host を再構築する必要があるため null 化
                    _host = null;
                }
            }
        }

        /// <summary>
        /// graphContext を保持する。host 構築は Update 内で audioAnalyzer も揃った時点で行う。
        /// </summary>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            _graphContext = graphContext;
            _host = null; // 既存 host を破棄して次 Update で再構築
        }

        private void Update()
        {
            if (_host == null)
            {
                if (audioAnalyzer == null || _graphContext == null) return;
                _host = new AudioDriverHost(audioAnalyzer, _graphContext);
            }

            // Phase 5/12 で本 Update() は ITickable.Tick() に移行予定。
            _host.Tick();
        }
    }
}
