#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

namespace Rhizomode.Modules.Ferrofluid
{
    /// <summary>
    /// 磁性流体の球体プールを rhizomode ノードグラフから driving するモジュール。
    /// </summary>
    /// <remarks>
    /// 同一 GameObject の <see cref="FerrofluidBallSpawner"/> を制御する。
    /// パラメータ仕様 (ModuleDefinition で定義):
    /// - "Count" (Float): 球体の数 (1-100、内部で int に round)
    /// - "WaveTrigger" (Bool, event): true 受信で wave 発火
    /// - "MoveTrigger" (Bool, event): true 受信で全球体にランダム速度
    /// - "OutlineTrigger" (Bool, event): true 受信で rim pulse
    ///
    /// イベントポート (WaveTrigger 等) は <see cref="IsEvent"/> = true で
    /// 立ち上がりエッジ駆動 (memory feedback_node_addition_protocol 参照)。
    /// </remarks>
    [PerformanceModule(NodeCategory.VFX)]
    public sealed class FerrofluidModule : MonoBehaviour, IPerformanceModule
    {
        private const string ParamCount = "Count";
        private const string ParamWaveTrigger = "WaveTrigger";
        private const string ParamMoveTrigger = "MoveTrigger";
        private const string ParamOutlineTrigger = "OutlineTrigger";

        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;
        [SerializeField] private FerrofluidBallSpawner? spawner;

        public string ModuleName => definition != null ? definition.moduleName : "Ferrofluid";

        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : EmptyParams;

        public void Initialize(ModuleDefinition def)
        {
            definition = def;
            if (spawner == null) spawner = GetComponentInChildren<FerrofluidBallSpawner>();
        }

        public void Activate()
        {
            if (spawner != null) spawner.enabled = true;
        }

        public void Deactivate()
        {
            if (spawner != null) spawner.enabled = false;
        }

        public void SetParam(string paramName, object value)
        {
            if (spawner == null)
            {
                spawner = GetComponentInChildren<FerrofluidBallSpawner>();
                if (spawner == null) return;
            }

            try
            {
                switch (paramName)
                {
                    case ParamCount:
                        spawner.Count = Mathf.RoundToInt(ToFinite((float)value, spawner.Count));
                        break;
                    case ParamWaveTrigger:
                        if ((bool)value) spawner.TriggerWave();
                        break;
                    case ParamMoveTrigger:
                        if ((bool)value) spawner.TriggerRandomMove();
                        break;
                    case ParamOutlineTrigger:
                        if ((bool)value) spawner.TriggerOutlineFx();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FerrofluidModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        private static float ToFinite(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }
    }
}
