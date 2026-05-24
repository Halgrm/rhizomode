#nullable enable

using NUnit.Framework;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.UI.Tests
{
    /// <summary>
    /// <see cref="NodeVisualManager.RegisterAuxiliaryCollider"/> / <see cref="NodeVisualManager.UnregisterAuxiliaryCollider"/>
    /// の登録 / 解決 / 解除契約検証 (NDI Receiver の preview Quad を grab 可能にするために導入)。
    /// </summary>
    public class NodeVisualManagerAuxiliaryColliderTests
    {
        private GameObject? _managerGo;
        private GameObject? _nodeGo;
        private GameObject? _auxGo;

        [TearDown]
        public void TearDown()
        {
            if (_auxGo != null) { Object.DestroyImmediate(_auxGo); _auxGo = null; }
            if (_nodeGo != null) { Object.DestroyImmediate(_nodeGo); _nodeGo = null; }
            if (_managerGo != null) { Object.DestroyImmediate(_managerGo); _managerGo = null; }
        }

        [Test]
        public void RegisterAuxiliaryCollider_ResolvesToController()
        {
            var manager = CreateManager();
            var controller = CreateController();
            var aux = CreateAuxCollider();

            manager.RegisterAuxiliaryCollider(aux, controller);

            var resolved = manager.GetVisualByCollider(aux);
            Assert.AreSame(controller, resolved,
                "補助 collider 登録後は GetVisualByCollider が controller を返す (grab handler が親 node として扱える)");
        }

        [Test]
        public void UnregisterAuxiliaryCollider_RemovesMapping()
        {
            var manager = CreateManager();
            var controller = CreateController();
            var aux = CreateAuxCollider();

            manager.RegisterAuxiliaryCollider(aux, controller);
            manager.UnregisterAuxiliaryCollider(aux);

            Assert.IsNull(manager.GetVisualByCollider(aux),
                "Unregister 後は GetVisualByCollider が null を返す (stale 参照防止)");
        }

        [Test]
        public void RegisterAuxiliaryCollider_NullCollider_DoesNotThrow()
        {
            var manager = CreateManager();
            var controller = CreateController();
            Assert.DoesNotThrow(() => manager.RegisterAuxiliaryCollider(null!, controller));
        }

        [Test]
        public void RegisterAuxiliaryCollider_NullController_DoesNotThrow()
        {
            var manager = CreateManager();
            var aux = CreateAuxCollider();
            Assert.DoesNotThrow(() => manager.RegisterAuxiliaryCollider(aux, null!));
        }

        [Test]
        public void UnregisterAuxiliaryCollider_Unknown_DoesNotThrow()
        {
            var manager = CreateManager();
            var aux = CreateAuxCollider();
            Assert.DoesNotThrow(() => manager.UnregisterAuxiliaryCollider(aux),
                "未登録 collider の解除は no-op (二重 Detach 等に耐性)");
        }

        private NodeVisualManager CreateManager()
        {
            _managerGo = new GameObject("ManagerHost");
            return _managerGo.AddComponent<NodeVisualManager>();
        }

        private NodeVisualController CreateController()
        {
            _nodeGo = new GameObject("NodeHost");
            _nodeGo.AddComponent<MeshFilter>();
            _nodeGo.AddComponent<MeshRenderer>();
            _nodeGo.AddComponent<BoxCollider>();
            _nodeGo.AddComponent<WorldPanelHost>();
            return _nodeGo.AddComponent<NodeVisualController>();
        }

        private Collider CreateAuxCollider()
        {
            _auxGo = new GameObject("AuxQuad");
            return _auxGo.AddComponent<BoxCollider>();
        }
    }
}
