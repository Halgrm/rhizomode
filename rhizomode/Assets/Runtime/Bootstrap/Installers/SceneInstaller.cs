#nullable enable

using Rhizomode.Scene.GraphAdapter;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Scene bounded context の LifecycleProcessor を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>SceneInstaller</c>。V3b で GameBootstrap.Awake が直接 new していた
    /// <see cref="SceneLoaderLifecycleProcessor"/> の構築をここへ移送。<see cref="XrSceneReferences"/>
    /// から <c>AdditiveSceneLoader</c> (ISceneLoader 実装) を受け取る。
    ///
    /// processor は IDisposable ではないため instance 登録。NodesInstaller が NodeRuntime の
    /// processor 配列へ明示順で組み込む。loader 未配置でも consumer ノードへ null を注入する
    /// fail-open 実装。
    ///
    /// F-Vf-a.1 Phase C: 旧 Bootstrap.Services.SceneObjectRegistrationService を Scene.GraphAdapter へ
    /// 移送した <see cref="SceneObjectRegistrationService"/> も本 Installer で登録 (XRInstaller から移動)。
    /// </remarks>
    internal sealed class SceneInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;

        public SceneInstaller(XrSceneReferences sceneRefs)
        {
            _sceneRefs = sceneRefs;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(new SceneLoaderLifecycleProcessor(_sceneRefs.SceneLoader));
            builder.Register<SceneObjectRegistrationService>(Lifetime.Singleton);
        }
    }
}
