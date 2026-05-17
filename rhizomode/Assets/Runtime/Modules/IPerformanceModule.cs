#nullable enable

using System.Collections.Generic;

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

namespace Rhizomode.Modules
{
    /// <summary>
    /// 演出モジュールのインターフェース。VFX・Shader・GPU instancing 等の統一的な制御口。
    /// </summary>
    /// <remarks>
    /// 新しい module 種別を追加する手順 (GPU-instanced cube swarm 等):
    /// 1. 本 interface を実装する MonoBehaviour を作る (prefab に貼る)
    /// 2. <see cref="ModuleDefinition"/> SO を作って prefab を割り当てる
    /// 3. <see cref="Initialize"/> で SO を受け取って自身の参照を解決する
    ///
    /// Bootstrap / Lifecycle 層は本 interface を <c>GetComponent&lt;IPerformanceModule&gt;()</c> で
    /// 一律に取り扱うため、コード変更は不要 (data-driven extension)。
    /// </remarks>
    public interface IPerformanceModule
    {
        string ModuleName { get; }
        IReadOnlyList<ParamDefinition> Params { get; }

        /// <summary>
        /// パラメータ値を設定する。valueはParamTypeに対応する型でboxedされている。
        /// 未知のparamNameの場合は警告を出してスキップすること。
        /// </summary>
        void SetParam(string paramName, object value);

        /// <summary>
        /// ModuleDefinition を受け取って初期化する。<see cref="Activate"/> より前に呼ばれる。
        /// 必要なら GetComponent で同 prefab 内の Renderer 等を解決する。
        /// </summary>
        void Initialize(ModuleDefinition def);

        void Activate();
        void Deactivate();
    }
}
