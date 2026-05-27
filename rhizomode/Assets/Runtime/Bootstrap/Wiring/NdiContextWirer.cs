#nullable enable

using Rhizomode.UI;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// VContainer Build 完了直後に <see cref="NdiPresentationContext"/> に
    /// 依存を push する wirer (BoundaryValidator Rule 7 対応)。
    /// </summary>
    /// <remarks>
    /// UI.Presentation は VContainer を参照しないため <c>[Inject]</c> が使えない。
    /// 本 wirer (Rhizomode.Bootstrap、VContainer 参照可) が <see cref="IInitializable.Initialize"/>
    /// で container から resolve した依存を static context に書き込む service locator パターン。
    /// </remarks>
    internal sealed class NdiContextWirer : IInitializable
    {
        private readonly NdiReceiverHealth _health;
        private readonly NdiWindowsRoot _windowsRoot;

        public NdiContextWirer(NdiReceiverHealth health, NdiWindowsRoot windowsRoot)
        {
            _health = health;
            _windowsRoot = windowsRoot;
        }

        public void Initialize()
        {
            NdiPresentationContext.Health = _health;
            NdiPresentationContext.WindowsRoot = _windowsRoot;
        }
    }
}
