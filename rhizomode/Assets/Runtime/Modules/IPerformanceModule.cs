#nullable enable

using System.Collections.Generic;

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;

namespace Rhizomode.Modules
{
    /// <summary>
    /// 演出モジュールのインターフェース。VFX・Shader・Cinemachine等の統一的な制御口。
    /// </summary>
    public interface IPerformanceModule
    {
        string ModuleName { get; }
        IReadOnlyList<ParamDefinition> Params { get; }

        /// <summary>
        /// パラメータ値を設定する。valueはParamTypeに対応する型でboxedされている。
        /// 未知のparamNameの場合は警告を出してスキップすること。
        /// </summary>
        void SetParam(string paramName, object value);

        void Activate();
        void Deactivate();
    }
}
