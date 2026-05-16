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
    ///   <item><see cref="MenuSpawnBootstrapWiring"/> — ScrollMenu callback を Bootstrap 内に閉じる wiring</item>
    ///   <item><see cref="SceneObjectsBootstrapWiring"/> — SceneObjectBridge スキャン + visual 生成 wiring</item>
    /// </list>
    /// MenuSpawnBootstrapWiring.HandleSelection は InteractionBootstrapWiring が ScrollMenu の
    /// OnNodeTypeSelected += に渡す callback として使う。SetActiveInput は InteractionWiring.Wire 完了後の
    /// eager step で駆動する。
    ///
    /// F-Vf-a.1 完了後の本 Installer の責務は VR 入力配線 wiring のみ。各 graph mutation / visual service は
    /// 以下の Installer へ移送済:
    /// <list type="bullet">
    ///   <item><c>MenuNodeSpawnCoordinator</c> → UIGraphAdapterInstaller (UI.GraphAdapter asmdef)</item>
    ///   <item><c>Object3DProxyBindService</c> → ModulesInstaller (Modules.Runtime asmdef)</item>
    ///   <item><c>SceneObjectRegistrationService</c> → SceneInstaller (Scene.GraphAdapter asmdef)</item>
    ///   <item><c>NodeSpawnService</c> → InteractionGraphAdapterInstaller (Rhizomode.Interaction.GraphAdapter asmdef、F-Vf-d.2 で再移送)</item>
    /// </list>
    /// </remarks>
    internal sealed class XRInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<MenuSpawnBootstrapWiring>(Lifetime.Singleton);
            builder.Register<SceneObjectsBootstrapWiring>(Lifetime.Singleton);
        }
    }
}
