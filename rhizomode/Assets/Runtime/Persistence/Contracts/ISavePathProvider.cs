#nullable enable

namespace Rhizomode.Persistence.Contracts
{
    /// <summary>
    /// 永続化先のディレクトリパスを提供する contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 7: <c>JsonSavePathProvider</c> が Unity の
    /// <c>Application.persistentDataPath</c> を wrap して提供。テストでは fake で差し替え可。
    /// </remarks>
    public interface ISavePathProvider
    {
        /// <summary>セーブディレクトリの絶対パス。</summary>
        string SaveDirectoryPath { get; }
    }
}
