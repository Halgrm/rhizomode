#nullable enable

using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// ゲーム起動時に全システムの初期化と相互接続を行う。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GraphContextBehaviour? graphContext;
        [SerializeField] private NodeVisualManager? visualManager;
        [SerializeField] private NodeCreationMenuController? menuController;
        [SerializeField] private ControllerInputRouter? controllerInput;
        [SerializeField] private NodeCreationHandler? creationHandler;
        [SerializeField] private EdgeVisualManager? edgeVisualManager;
        [SerializeField] private EdgeDragHandler? edgeDragHandler;
        [SerializeField] private EdgeCutHandler? edgeCutHandler;
        [SerializeField] private NodeDeleteHandler? nodeDeleteHandler;
        [SerializeField] private NodeGrabHandler? nodeGrabHandler;

        private NodeTypeRegistry? _typeRegistry;

        private void Awake()
        {
            _typeRegistry = new NodeTypeRegistry();
            RegisterNodeTypes();
            InitializeSystems();
        }

        private void RegisterNodeTypes()
        {
            if (_typeRegistry == null) return;

            // 入力系
            _typeRegistry.Register(new NodeTypeInfo("ConstFloat", "Const Float", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("AudioTrigger", "Audio Trigger", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("BeatDetector", "Beat Detector", NodeCategory.Input));
            _typeRegistry.Register(new NodeTypeInfo("TapTempo", "Tap Tempo", NodeCategory.Input));

            // 数学/信号処理系
            _typeRegistry.Register(new NodeTypeInfo("Multiply", "Multiply", NodeCategory.Math));
            _typeRegistry.Register(new NodeTypeInfo("Smooth", "Smooth", NodeCategory.Math));

            // 時間系
            _typeRegistry.Register(new NodeTypeInfo("Time", "Time", NodeCategory.Time));

            // ユーティリティ系
            _typeRegistry.Register(new NodeTypeInfo("Threshold", "Threshold", NodeCategory.Utility));
            _typeRegistry.Register(new NodeTypeInfo("Toggle", "Toggle", NodeCategory.Utility));
        }

        private void InitializeSystems()
        {
            if (_typeRegistry == null) return;

            if (visualManager != null)
            {
                visualManager.Initialize(_typeRegistry);
            }

            if (menuController != null)
            {
                menuController.Initialize(_typeRegistry);
            }

            if (creationHandler != null)
            {
                RegisterNodeFactories();

                if (controllerInput != null)
                {
                    creationHandler.Initialize(controllerInput);
                }
            }

            if (graphContext != null)
            {
                RegisterGraphContextFactories();
            }

            InitializeInteractionHandlers();
        }

        private void InitializeInteractionHandlers()
        {
            if (controllerInput == null) return;

            IRayProvider rayProvider = controllerInput;

            if (edgeVisualManager != null && visualManager != null)
            {
                edgeVisualManager.Initialize(visualManager);
            }

            if (edgeDragHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null)
            {
                edgeDragHandler.Initialize(
                    rayProvider, controllerInput, visualManager,
                    graphContext, edgeVisualManager);
            }

            if (edgeCutHandler != null && edgeVisualManager != null && graphContext != null)
            {
                edgeCutHandler.Initialize(
                    controllerInput, rayProvider,
                    edgeVisualManager, graphContext);
            }

            if (nodeDeleteHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null)
            {
                nodeDeleteHandler.Initialize(
                    controllerInput, rayProvider, visualManager,
                    graphContext, edgeVisualManager);
            }

            if (nodeGrabHandler != null && visualManager != null)
            {
                nodeGrabHandler.Initialize(
                    controllerInput, rayProvider, visualManager);
            }
        }

        private void RegisterNodeFactories()
        {
            if (creationHandler == null) return;

            foreach (var typeName in _typeRegistry!.AllTypes.Keys)
            {
                var name = typeName;
                creationHandler.RegisterNodeFactory(name, id => new DummyNode(id, name));
            }
        }

        private void RegisterGraphContextFactories()
        {
            if (graphContext == null || _typeRegistry == null) return;

            foreach (var typeName in _typeRegistry.AllTypes.Keys)
            {
                var name = typeName;
                graphContext.Context.RegisterNodeFactory(name, id => new DummyNode(id, name));
            }
        }
    }
}
