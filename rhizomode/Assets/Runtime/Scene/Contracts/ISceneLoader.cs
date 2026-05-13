#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.Scene.Contracts
{
    /// <summary>
    /// シーンのAdditive読み込み/切り替えを抽象化するインターフェース。
    /// ノードから直接SceneManagerを呼ばず、このインターフェース経由で操作する。
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>登録済みシーン数。</summary>
        int SceneCount { get; }

        /// <summary>
        /// 指定インデックスのシーンをAdditiveで読み込む。
        /// 既にロード済みの別シーンがあればアンロードしてから切り替える。
        /// 範囲外のインデックスは無視する。
        /// </summary>
        void LoadScene(int index);

        /// <summary>
        /// 現在ロード中のAdditiveシーンをアンロードする。何もロードされていなければ何もしない。
        /// </summary>
        void UnloadCurrentScene();

        /// <summary>指定インデックスのシーン名を返す。範囲外はnull。</summary>
        string? GetSceneName(int index);
    }
}
