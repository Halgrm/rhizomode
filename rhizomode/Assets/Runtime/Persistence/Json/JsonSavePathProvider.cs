#nullable enable

using System.IO;
using Rhizomode.Persistence.Contracts;
using UnityEngine;

namespace Rhizomode.Persistence.Json
{
    /// <summary>
    /// Unity の <see cref="Application.persistentDataPath"/> 配下に SavedGraphs ディレクトリを
    /// 確保する <see cref="ISavePathProvider"/>。
    /// </summary>
    public sealed class JsonSavePathProvider : ISavePathProvider
    {
        private const string DefaultSubdirectory = "SavedGraphs";

        public string SaveDirectoryPath { get; }

        public JsonSavePathProvider(string? subdirectory = null)
        {
            var dir = subdirectory ?? DefaultSubdirectory;
            SaveDirectoryPath = Path.Combine(Application.persistentDataPath, dir);
            EnsureDirectory();
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectoryPath))
            {
                Directory.CreateDirectory(SaveDirectoryPath);
            }
        }
    }
}
