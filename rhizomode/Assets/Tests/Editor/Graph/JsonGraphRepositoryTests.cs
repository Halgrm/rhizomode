#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using Rhizomode.Graph.Serialization;
using Rhizomode.Persistence.Contracts;
using Rhizomode.Persistence.Json;

namespace Rhizomode.Graph.Tests
{
    public class JsonGraphRepositoryTests
    {
        private sealed class TempPathProvider : ISavePathProvider, IDisposable
        {
            public string SaveDirectoryPath { get; }
            public TempPathProvider()
            {
                SaveDirectoryPath = Path.Combine(Path.GetTempPath(), "rhizomode_test_" + Guid.NewGuid());
                Directory.CreateDirectory(SaveDirectoryPath);
            }
            public void Dispose()
            {
                if (Directory.Exists(SaveDirectoryPath))
                    Directory.Delete(SaveDirectoryPath, recursive: true);
            }
        }

        [Test]
        public void SaveThenLoad_RoundTripsGraphData()
        {
            using var pathProv = new TempPathProvider();
            var repo = new JsonGraphRepository(pathProv);

            var data = new GraphData
            {
                version = "1.0",
                nodes =
                {
                    new NodeData
                    {
                        id = "n1", type = "Stub",
                        position = new[] { 1f, 2f, 3f },
                        paramsJson = "{\"v\":42}"
                    }
                },
                edges =
                {
                    new EdgeData { id = "e1", from = "n1", fromPort = "Out", to = "n2", toPort = "In" }
                }
            };

            Assert.IsTrue(repo.SaveGraph("test_save", data));

            var loaded = repo.LoadGraph("test_save");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("1.0", loaded!.version);
            Assert.AreEqual(1, loaded.nodes.Count);
            Assert.AreEqual("n1", loaded.nodes[0].id);
            Assert.AreEqual("Stub", loaded.nodes[0].type);
            Assert.AreEqual("{\"v\":42}", loaded.nodes[0].paramsJson);
            Assert.AreEqual(1, loaded.edges.Count);
            Assert.AreEqual("e1", loaded.edges[0].id);
        }

        [Test]
        public void LoadGraph_NonExistentFile_ReturnsNull()
        {
            using var pathProv = new TempPathProvider();
            var repo = new JsonGraphRepository(pathProv);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*"));

            var result = repo.LoadGraph("nonexistent");
            Assert.IsNull(result);
        }

        [Test]
        public void GetSaveFiles_ReturnsSavedFilesInDescendingOrder()
        {
            using var pathProv = new TempPathProvider();
            var repo = new JsonGraphRepository(pathProv);

            repo.SaveGraph("a", new GraphData());
            repo.SaveGraph("b", new GraphData());
            repo.SaveGraph("c", new GraphData());

            var files = repo.GetSaveFiles();

            Assert.AreEqual(3, files.Length);
            CollectionAssert.AreEqual(new[] { "c.json", "b.json", "a.json" }, files);
        }

        [Test]
        public void DeleteSave_RemovesFile()
        {
            using var pathProv = new TempPathProvider();
            var repo = new JsonGraphRepository(pathProv);

            repo.SaveGraph("target", new GraphData());
            Assert.IsTrue(File.Exists(Path.Combine(pathProv.SaveDirectoryPath, "target.json")));

            Assert.IsTrue(repo.DeleteSave("target"));
            Assert.IsFalse(File.Exists(Path.Combine(pathProv.SaveDirectoryPath, "target.json")));
        }

        [Test]
        public void DeleteSave_NonExistentFile_ReturnsFalse()
        {
            using var pathProv = new TempPathProvider();
            var repo = new JsonGraphRepository(pathProv);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*"));

            Assert.IsFalse(repo.DeleteSave("nonexistent"));
        }

        [Test]
        public void SaveGraph_AppendsJsonExtensionAutomatically()
        {
            using var pathProv = new TempPathProvider();
            var repo = new JsonGraphRepository(pathProv);

            repo.SaveGraph("plain", new GraphData());

            Assert.IsTrue(File.Exists(Path.Combine(pathProv.SaveDirectoryPath, "plain.json")));
        }
    }
}
