#nullable enable

namespace Rhizomode.Nodes.Audio
{
    /// <summary>
    /// タップ間隔の平均から BPM・拍位相を推定する pure 値型 (deterministic、テスト容易)。
    /// </summary>
    /// <remarks>
    /// BeatDetectorNode / TapTempoNode が同じロジックを重複実装していたのを集約。
    /// 時刻は呼び出し側が <c>now</c> として渡す (Time.time や AudioClock.Now 等)。
    /// <see cref="MaxTapHistory"/> 内の直近 N タップから interval を平均し BPM を出す。
    /// 前回タップから <see cref="TapTimeoutSec"/> 以上空くと履歴をリセット (新セッション扱い)。
    /// </remarks>
    public struct TempoTracker
    {
        /// <summary>履歴の最大タップ数。8 タップで安定 BPM 推定する経験則。</summary>
        public const int MaxTapHistory = 8;

        /// <summary>これより長い間タップが無いと履歴を破棄 (新セッション扱い)。</summary>
        public const float TapTimeoutSec = 3f;

        /// <summary>初期 BPM (タップ 1 回目までの出力に使う)。</summary>
        public const float DefaultBpm = 120f;

        /// <summary>BeatInterval として扱う最小値 (これ以下なら BPM 計算は no-op、ゼロ除算回避)。</summary>
        public const float MinBeatIntervalSec = 0.001f;

        private float[] _tapTimes; // lazy 確保: struct default のときは null
        private int _tapCount;
        private float _lastTapTime;
        private float _bpm;
        private float _beatInterval;
        private float _phaseOrigin;

        /// <summary>現在の推定 BPM (1 タップ未満なら <see cref="DefaultBpm"/>)。</summary>
        public float Bpm => _bpm > 0f ? _bpm : DefaultBpm;

        /// <summary>拍と拍の間隔 (秒)。<see cref="MinBeatIntervalSec"/> 以下なら未確定。</summary>
        public float BeatInterval => _beatInterval;

        /// <summary>有効なタップ履歴件数 (timeout 後は 0 にリセットされる)。</summary>
        public int TapCount => _tapCount;

        /// <summary>
        /// 新規タップを記録し、十分な履歴があれば BPM を更新する。
        /// </summary>
        /// <param name="now">タップ発生時刻 (秒)。caller が時刻ソースを選ぶ。</param>
        /// <returns>BPM が更新されたら true (caller が BPM 出力を emit する判断に使う)。</returns>
        public bool OnTap(float now)
        {
            EnsureBuffer();

            // timeout 超過 → 新セッション扱い (履歴を捨てる)
            if (now - _lastTapTime > TapTimeoutSec)
                _tapCount = 0;

            // ring buffer 風に append (上限超過なら 1 個ずらして末尾追加)
            if (_tapCount < MaxTapHistory)
            {
                _tapTimes[_tapCount] = now;
                _tapCount++;
            }
            else
            {
                for (var i = 1; i < MaxTapHistory; i++)
                    _tapTimes[i - 1] = _tapTimes[i];
                _tapTimes[MaxTapHistory - 1] = now;
            }

            _lastTapTime = now;

            if (_tapCount < 2) return false;

            var totalInterval = _tapTimes[_tapCount - 1] - _tapTimes[0];
            var avgInterval = totalInterval / (_tapCount - 1);
            if (avgInterval <= MinBeatIntervalSec) return false;

            _bpm = 60f / avgInterval;
            _beatInterval = avgInterval;
            _phaseOrigin = now;
            return true;
        }

        /// <summary>
        /// 現在 phase + 拍境界を跨いだか判定する。
        /// </summary>
        /// <param name="now">現在時刻 (秒)。OnTap と同じ時刻ソースを使うこと。</param>
        /// <param name="deltaTimeSec">前フレームからの経過時間。phase の前回値推定に使う。</param>
        /// <returns>(phase 0..1, このフレームで拍境界を跨いだか)。BeatInterval 未確定なら (0, false)。</returns>
        public (float phase, bool isBeat) Tick(float now, float deltaTimeSec)
        {
            if (_beatInterval <= MinBeatIntervalSec) return (0f, false);

            var elapsed = now - _phaseOrigin;
            var phase = (elapsed % _beatInterval) / _beatInterval;

            var prevElapsed = elapsed - deltaTimeSec;
            if (prevElapsed < 0f) prevElapsed = 0f;
            var prevPhase = (prevElapsed % _beatInterval) / _beatInterval;

            // phase が wrap した (前回より小さい値になった) → 拍境界跨ぎ
            var isBeat = phase < prevPhase;
            return (phase, isBeat);
        }

        private void EnsureBuffer()
        {
            if (_tapTimes == null)
                _tapTimes = new float[MaxTapHistory];
        }
    }
}
