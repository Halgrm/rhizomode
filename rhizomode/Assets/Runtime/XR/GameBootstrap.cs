#nullable enable

using Rhizomode.Bootstrap;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// シーン上に置く起動 entry point。<see cref="EntryPointBootstrapper.Launch"/> を呼んで全 wiring を
    /// 駆動し、OnDestroy で composition root を Dispose するだけの薄い shim。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 V-final (Vf-a): 旧 god-object (1370+ 行 → 512 行 → 本コミットで 50 行未満) を解体。
    /// 残置していた InitializeSystems / InitializeVerticalSliceSystems / OnGraphLoaded /
    /// OnScrollMenuNodeSelected / RegisterSceneObjects / BindObject3DProxyObservables / ConfigureSaveLoad /
    /// OnGraphLoadingHandler は Bootstrap.Wiring / Bootstrap.Services に全移送。
    ///
    /// Vf-c で本ファイル自体を削除し、<see cref="RootLifetimeScope"/> をシーン直接配置に切り替える予定。
    /// </remarks>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Composition Root")]
        [Tooltip("Plan v5.4 §15: scene 参照を集約する MonoBehaviour。各 Installer がここから参照を取る。")]
        [SerializeField] private XrSceneReferences? sceneRefs;

        private CompositionRoot? _compositionRoot;

        private void Awake()
        {
            if (sceneRefs == null || sceneRefs.GraphContext == null)
            {
                Debug.LogWarning(
                    "[GameBootstrap] Boot skipped — sceneRefs or sceneRefs.GraphContext is not set (degraded boot).");
                return;
            }

            _compositionRoot = EntryPointBootstrapper.Launch(transform, sceneRefs);
        }

        private void OnDestroy()
        {
            // child RootLifetimeScope の OnDestroy が VContainer container を Dispose する
            // (全 Lifetime.Singleton wiring / processor / HealthAggregator / ITickable adapter が解放)。
            _compositionRoot?.Dispose();
            _compositionRoot = null;
        }
    }
}
