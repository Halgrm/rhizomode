#nullable enable

namespace Rhizomode.UI
{
    /// <summary>
    /// シーン切替を跨いだ cue 復元のための pending スロット (process-static, single-slot)。
    /// </summary>
    /// <remarks>
    /// 2026-05-23 追加: cue は <c>GraphData.sceneName</c> を持ち、ロード時に active scene と
    /// 一致しない場合は <c>SceneManager.LoadSceneAsync</c> を発行する必要がある。シーン切替後の
    /// 新 bootstrap (<c>GraphSaveLoadBootstrapWiring.Wire</c>) が本スロットを <see cref="TryConsume"/>
    /// し、改めて <c>GraphSaveLoadManager.LoadGraph</c> を呼ぶことで cue を完了させる。
    ///
    /// process-static で持つ理由: <c>SceneManager.LoadScene</c> は VContainer の Lifetime.Singleton を
    /// 含む scene-scoped オブジェクトを全て破棄するため、DI で受け渡しできない。
    /// AppDomain は scene reload で破棄されないので static field は生存する (Domain Reload は
    /// Editor 起動時 / Play 開始時のみ発生)。
    ///
    /// 単一スロット (キューイングしない): 同時に複数の pending を抱えるユースケースが無いため
    /// 後勝ち。並列スレッドからの利用は想定しない (Unity main thread only)。
    /// </remarks>
    public static class PendingCueLoad
    {
        private static string? _cueName;

        /// <summary>シーン切替後に LoadGraph すべき cue 名を予約する。</summary>
        public static void Schedule(string cueName)
        {
            _cueName = cueName;
        }

        /// <summary>予約があれば取り出して slot をクリアする。</summary>
        public static bool TryConsume(out string cueName)
        {
            if (_cueName == null)
            {
                cueName = "";
                return false;
            }
            cueName = _cueName;
            _cueName = null;
            return true;
        }

        /// <summary>テスト用: slot を強制クリアする。</summary>
        public static void Clear()
        {
            _cueName = null;
        }

        /// <summary>現在 pending があるか (副作用なし)。</summary>
        public static bool HasPending => _cueName != null;
    }
}
