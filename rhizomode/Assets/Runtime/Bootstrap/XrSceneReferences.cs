#nullable enable

using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Cameras;
using Rhizomode.Input.Desktop;
using Rhizomode.Input.XR;
using Rhizomode.Modules;
using Rhizomode.OscMidi.Transport;
using Rhizomode.Ableton.Transport;
using Rhizomode.Ableton.Session;
using Rhizomode.Ableton.GraphAdapter;
using Rhizomode.Scene.Runtime;
using Rhizomode.UI;
using Rhizomode.XR;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// Plan v5.4 §15: シーン配置の参照を集約する MonoBehaviour。GameBootstrap の散在した
    /// <c>[SerializeField]</c> 群をここへ段階移送し、各 Installer がここから参照を取る。
    /// </summary>
    /// <remarks>
    /// V3a で transport (Audio / OSC / MIDI / Ableton) context 分を移送。V3b-d で Scene /
    /// Modules / Nodes / Input / Interaction / UI / Cameras / XR context の参照を順次追加する。
    /// V-final で RootLifetimeScope と共にシーン直接配置の composition root を構成し、
    /// GameBootstrap を解体する。
    ///
    /// 本コンポーネントは「参照の器」であり業務ロジックを持たない (Plan v5.4 §15 — Bootstrap は
    /// 業務ロジック禁止)。getter は読み取り専用。<see cref="MacroTrackIndex"/> /
    /// <see cref="MacroDeviceIndex"/> は wiring 側が初期値として複製し、以降は wiring 側の
    /// 可変状態として扱う。
    /// </remarks>
    public sealed class XrSceneReferences : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioDriverBehaviour? audioDriver;
        [SerializeField] private AudioDeviceSelector? audioDeviceSelector;

        [Header("OSC / MIDI Transport")]
        [Tooltip("シーンに配置した OscServer。未設定なら OSC 入力ノードは非アクティブ。")]
        [SerializeField] private OscServer? oscServer;
        [Tooltip("シーンに配置した MidiServer。未設定なら MIDI 入力ノードは非アクティブ。")]
        [SerializeField] private MidiServer? midiServer;

        [Header("Ableton OSC")]
        [SerializeField] private AbletonLink? abletonLink;
        [SerializeField] private AbletonOscBridge? abletonBridge;
        [SerializeField] private AbletonSetupPanel? abletonSetupPanel;
        [SerializeField] private AbletonClipGridManager? abletonGridManager;
        [SerializeField] private ClipFireRayHandler? clipFireHandler;
        [SerializeField] private AbletonControlPanel? abletonControlPanel;

        [Header("Ableton UI Anchor (Editor で位置・回転調整)")]
        [Tooltip("Ableton UI 全体の基準アンカー。未設定ならプレイヤー前方にフォールバック。Z+ がパネル正面。")]
        [SerializeField] private Transform? abletonUiAnchor;

        [Header("Ableton Outer Frame (Grid + Control 全体を囲む枠)")]
        [Tooltip("UnlitRoundedFrame シェーダーを使ったマテリアル。null なら外枠を生成しない。")]
        [SerializeField] private Material? abletonOuterFrameMaterial;
        [Tooltip("コンテンツ端からの余白 (m)。フレームはこの分外側に広がる。")]
        [SerializeField, Range(0.01f, 0.2f)] private float abletonOuterFramePadding = 0.06f;
        [Tooltip("フレームの背後オフセット (m)。すべての UI の最も奥に置く。")]
        [SerializeField, Range(0.0f, 0.05f)] private float abletonOuterFrameDepthOffset = 0.012f;
        [Tooltip("外枠の角丸半径 (m)。")]
        [SerializeField, Range(0.0f, 0.2f)] private float abletonOuterFrameCornerRadius = 0.04f;

        [Header("Ableton Macro (どの Track / Device の Rack の Macro を VR から触るか)")]
        [Tooltip("Track index。-1 = Master Track、0..N-1 = 通常 Track。wiring 側が初期値として複製する。")]
        [SerializeField] private int macroTrackIndex = -1;
        [Tooltip("Track 内のどの Device の Macro を扱うか。0 = 最初のデバイス。")]
        [SerializeField, Range(0, 16)] private int macroDeviceIndex = 0;

        [Header("Status (V3d で UI Installer 側へ移送予定)")]
        [Tooltip("health 表示先。Audio / Ableton wiring が transitional に参照する。")]
        [SerializeField] private StatusPanelController? statusPanel;

        [Header("Scene / Modules / Nodes (V3b)")]
        [Tooltip("追加シーンの加算ロード。SceneLoaderLifecycleProcessor が ISceneLoader として消費。")]
        [SerializeField] private AdditiveSceneLoader? sceneLoader;
        [Tooltip("VFX/Shader モジュール定義。CatalogInstaller が動的 typeName / factory を登録。")]
        [SerializeField] private ModuleDefinition[]? moduleDefinitions;
        [Tooltip("Object3D prefab 一覧。CatalogInstaller が Object3D_ typeName / factory を登録。")]
        [SerializeField] private Object3DPrefabList? object3DPrefabs;

        [Header("Input / Interaction (V3c)")]
        [Tooltip("VR 入力ルーター。desktopInput がアクティブならそちらが優先される。")]
        [SerializeField] private ControllerInputRouter? controllerInput;
        [Tooltip("デスクトップデバッグ入力ルーター。GameObject がアクティブなら VR より優先。")]
        [SerializeField] private DesktopInputRouter? desktopInput;
        [SerializeField] private SharedRaycastService? sharedRaycastService;
        [SerializeField] private NodeVisualManager? visualManager;
        [SerializeField] private EdgeVisualManager? edgeVisualManager;
        [SerializeField] private EdgeDragHandler? edgeDragHandler;
        [SerializeField] private EdgeCutHandler? edgeCutHandler;
        [SerializeField] private NodeDeleteHandler? nodeDeleteHandler;
        [SerializeField] private NodeGrabHandler? nodeGrabHandler;
        [SerializeField] private PathControlPointGrabHandler? pathControlPointGrabHandler;
        [SerializeField] private PathControlPointVisualManager? pathEditorManager;
        [SerializeField] private Object3DGrabHandler? object3DGrabHandler;
        [SerializeField] private UIRaycastDriver? uiRaycastDriver;
        [SerializeField] private ScrollMenuVisualController? scrollMenuVisual;
        [SerializeField] private ScrollMenuInteractionHandler? scrollMenuInteraction;

        [Header("UI / Cameras (V3d)")]
        [Tooltip("VR 視点の RenderTexture 出力。Spout/NDI/Desktop blitter のソース。")]
        [SerializeField] private MirrorOutputController? mirrorOutput;
        [SerializeField] private SpoutSenderController? spoutSender;
        [SerializeField] private NdiSenderController? ndiSender;
        [SerializeField] private DesktopMirrorBlitter? desktopBlitter;
        [SerializeField] private CameraManagerPanelController? cameraManagerPanel;
        [Tooltip("デスクトップデバッグ専用 cinemachine プレビュー。VR モードでは非アクティブ化。")]
        [SerializeField] private CinemachinePreviewMonitor? cinemachinePreview;

        public AudioDriverBehaviour? AudioDriver => audioDriver;
        public AudioDeviceSelector? AudioDeviceSelector => audioDeviceSelector;
        public OscServer? OscServer => oscServer;
        public MidiServer? MidiServer => midiServer;
        public AbletonLink? AbletonLink => abletonLink;
        public AbletonOscBridge? AbletonBridge => abletonBridge;
        public AbletonSetupPanel? AbletonSetupPanel => abletonSetupPanel;
        public AbletonClipGridManager? AbletonGridManager => abletonGridManager;
        public ClipFireRayHandler? ClipFireHandler => clipFireHandler;
        public AbletonControlPanel? AbletonControlPanel => abletonControlPanel;
        public Transform? AbletonUiAnchor => abletonUiAnchor;
        public Material? AbletonOuterFrameMaterial => abletonOuterFrameMaterial;
        public float AbletonOuterFramePadding => abletonOuterFramePadding;
        public float AbletonOuterFrameDepthOffset => abletonOuterFrameDepthOffset;
        public float AbletonOuterFrameCornerRadius => abletonOuterFrameCornerRadius;
        public int MacroTrackIndex => macroTrackIndex;
        public int MacroDeviceIndex => macroDeviceIndex;
        public StatusPanelController? StatusPanel => statusPanel;
        public AdditiveSceneLoader? SceneLoader => sceneLoader;
        public ModuleDefinition[]? ModuleDefinitions => moduleDefinitions;
        public Object3DPrefabList? Object3DPrefabs => object3DPrefabs;
        public ControllerInputRouter? ControllerInput => controllerInput;
        public DesktopInputRouter? DesktopInput => desktopInput;
        public SharedRaycastService? SharedRaycastService => sharedRaycastService;
        public NodeVisualManager? VisualManager => visualManager;
        public EdgeVisualManager? EdgeVisualManager => edgeVisualManager;
        public EdgeDragHandler? EdgeDragHandler => edgeDragHandler;
        public EdgeCutHandler? EdgeCutHandler => edgeCutHandler;
        public NodeDeleteHandler? NodeDeleteHandler => nodeDeleteHandler;
        public NodeGrabHandler? NodeGrabHandler => nodeGrabHandler;
        public PathControlPointGrabHandler? PathControlPointGrabHandler => pathControlPointGrabHandler;
        public PathControlPointVisualManager? PathEditorManager => pathEditorManager;
        public Object3DGrabHandler? Object3DGrabHandler => object3DGrabHandler;
        public UIRaycastDriver? UIRaycastDriver => uiRaycastDriver;
        public ScrollMenuVisualController? ScrollMenuVisual => scrollMenuVisual;
        public ScrollMenuInteractionHandler? ScrollMenuInteraction => scrollMenuInteraction;
        public MirrorOutputController? MirrorOutput => mirrorOutput;
        public SpoutSenderController? SpoutSender => spoutSender;
        public NdiSenderController? NdiSender => ndiSender;
        public DesktopMirrorBlitter? DesktopBlitter => desktopBlitter;
        public CameraManagerPanelController? CameraManagerPanel => cameraManagerPanel;
        public CinemachinePreviewMonitor? CinemachinePreview => cinemachinePreview;
    }
}
