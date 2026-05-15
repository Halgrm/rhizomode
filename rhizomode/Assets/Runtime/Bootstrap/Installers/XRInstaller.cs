#nullable enable

using Rhizomode.Bootstrap.Wiring;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — XR bounded context のサービスと wiring を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>XRInstaller</c>。V-final (Vf-a) で旧 GameBootstrap.OnScrollMenuNodeSelected +
    /// RegisterSceneObjects + BindObject3DProxyObservables を移送した以下を <see cref="Lifetime.Singleton"/>
    /// 登録する:
    /// <list type="bullet">
    ///   <item><see cref="NodeSpawnService"/> — graph mutation (Scroll menu spawn + input auto-spawn)</item>
    ///   <item><see cref="MenuNodeSpawnCoordinator"/> — menu spawn の visual 創出</item>
    ///   <item><see cref="SceneObjectRegistrationService"/> — SceneObjectBridge → SceneObjectNode 生成</item>
    ///   <item><see cref="Object3DProxyBindService"/> — Object3D Proxy 観測 bind の共通 service</item>
    ///   <item><see cref="MenuSpawnBootstrapWiring"/> — ScrollMenu callback を Bootstrap 内に閉じる wiring</item>
    ///   <item><see cref="SceneObjectsBootstrapWiring"/> — SceneObjectBridge スキャン + visual 生成 wiring</item>
    /// </list>
    /// MenuSpawnBootstrapWiring.HandleSelection は InteractionBootstrapWiring が ScrollMenu の
    /// OnNodeTypeSelected += に渡す callback として使う。SetActiveInput は InteractionWiring.Wire 完了後の
    /// eager step で駆動する。
    /// </remarks>
    internal sealed class XRInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<NodeSpawnService>(Lifetime.Singleton);
            builder.Register<MenuNodeSpawnCoordinator>(Lifetime.Singleton);
            builder.Register<SceneObjectRegistrationService>(Lifetime.Singleton);
            builder.Register<Object3DProxyBindService>(Lifetime.Singleton);
            builder.Register<MenuSpawnBootstrapWiring>(Lifetime.Singleton);
            builder.Register<SceneObjectsBootstrapWiring>(Lifetime.Singleton);
        }
    }
}
