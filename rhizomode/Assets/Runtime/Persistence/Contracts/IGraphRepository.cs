#nullable enable

using Rhizomode.Graph.Serialization;

namespace Rhizomode.Persistence.Contracts
{
    /// <summary>
    /// グラフ (GraphData) の永続化 contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 7: <c>Persistence.Json.JsonGraphRepository</c> が実装、
    /// <c>UI.GraphAdapter.GraphSaveLoadManager</c> が consumer。Serialize/Hydrate ロジックは
    /// <c>Graph.Serialization</c> に分離 — 本 interface は I/O のみ責務。
    /// </remarks>
    public interface IGraphRepository
    {
        /// <summary>指定ファイル名で <see cref="GraphData"/> を保存する。</summary>
        /// <returns>成功で true。</returns>
        bool SaveGraph(string fileName, GraphData data);

        /// <summary>指定ファイル名から <see cref="GraphData"/> を読み込む。</summary>
        /// <returns>成功で <see cref="GraphData"/>、ファイル未発見・パース失敗で null。</returns>
        GraphData? LoadGraph(string fileName);

        /// <summary>保存済みファイル名 (拡張子付き) を新しい順で返す。</summary>
        string[] GetSaveFiles();

        /// <summary>指定ファイルを削除する。</summary>
        /// <returns>削除成功で true。</returns>
        bool DeleteSave(string fileName);
    }
}
