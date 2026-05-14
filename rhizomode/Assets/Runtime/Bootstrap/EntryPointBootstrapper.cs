#nullable enable

using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Graph.Mutation;
using Rhizomode.Observability.Runtime;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// VContainer の <see cref="RootLifetimeScope"/> を起動する factory。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §19 hard rule: VContainer / VContainer.Unity を参照してよいのは Bootstrap asmdef
    /// のみ。GameBootstrap (XR asmdef) は VContainer 型に一切触れず、本 factory に host を渡すだけ。
    ///
    /// V1 transitional shape: GameBootstrap が pure-C# host を構築し終えた後に
    /// <see cref="Launch"/> を呼ぶ。子 GameObject を「非アクティブ生成 → AddComponent →
    /// SetHosts → アクティブ化」の順で構築するため、RootLifetimeScope の Awake (= VContainer
    /// Build) 時点で host は必ず揃っており、MonoBehaviour 実行順序に依存しない。
    /// V2 以降で GameBootstrap を解体したら、本 factory も RootLifetimeScope のシーン直接配置に
    /// 置き換える。
    /// </remarks>
    public static class EntryPointBootstrapper
    {
        /// <summary>
        /// <paramref name="parent"/> の子として RootLifetimeScope を生成し、host を注入して起動する。
        /// </summary>
        public static RootLifetimeScope Launch(
            Transform parent,
            MainThreadGraphCommandQueue commandQueue,
            AudioDriverBehaviour? audioDriver,
            HealthAggregator healthAggregator)
        {
            var scopeGo = new GameObject("RootLifetimeScope");
            scopeGo.transform.SetParent(parent, false);
            scopeGo.SetActive(false);
            var rootScope = scopeGo.AddComponent<RootLifetimeScope>();
            rootScope.SetHosts(commandQueue, audioDriver, healthAggregator);
            scopeGo.SetActive(true);
            return rootScope;
        }
    }
}
