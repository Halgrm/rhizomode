#nullable enable

namespace Rhizomode.UI
{
    /// <summary>
    /// NDI 関連 presenter / window が必要とする依存を Bootstrap から push してもらう静的ホルダ。
    /// </summary>
    /// <remarks>
    /// <para>背景: Plan v5.4 §15 + <c>BoundaryViolationValidator</c> Rule 7「VContainer 参照は
    /// <c>Rhizomode.Bootstrap</c> のみ」。UI.Presentation の MonoBehaviour が <c>[Inject]</c> や
    /// <c>LifetimeScope.Find</c> を使うと build 時に <c>BuildFailedException</c> で弾かれる。</para>
    ///
    /// <para>解決策: 静的 context を UI.Presentation に置き (VContainer 依存なし)、
    /// <c>Rhizomode.Bootstrap</c> 側の wirer が <see cref="NdiReceiverHealth"/> と
    /// <see cref="NdiWindowsRoot"/> を Container から resolve して本クラスに push する
    /// (service locator パターン、テスト容易性を残すため public setter)。</para>
    ///
    /// <para>Test では <see cref="Reset"/> で session 間 leak を防ぐ。</para>
    /// </remarks>
    public static class NdiPresentationContext
    {
        /// <summary><see cref="NdiReceiverPresenter"/> が観察する health monitor (Singleton)。</summary>
        public static NdiReceiverHealth? Health { get; set; }

        /// <summary>NDI view window の生成 / 破棄を一手に担う registry。</summary>
        public static NdiWindowsRoot? WindowsRoot { get; set; }

        /// <summary>EditMode test 用: domain reload で reset されない static state を明示的に reset する。</summary>
        public static void Reset()
        {
            Health = null;
            WindowsRoot = null;
        }
    }
}
