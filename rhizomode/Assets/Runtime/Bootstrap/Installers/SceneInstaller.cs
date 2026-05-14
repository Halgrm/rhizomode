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
        }
    }
}
