#nullable enable

namespace Rhizomode.Scene.Contracts
{
    /// <summary>
    /// <see cref="ISceneLoader"/> を必要とするノードが実装する契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: <c>Scene.GraphAdapter.SceneLoaderLifecycleProcessor</c> が
    /// 本 interface で型を判定し Loader を注入する。これにより Scene.GraphAdapter は
    /// 具体ノード型 (<c>SceneSwitchNode</c> / <c>SceneTriggerNode</c>) を知らずに済む。
    /// </remarks>
    public interface ISceneLoaderConsumer
    {
        ISceneLoader? Loader { get; set; }
    }
}
