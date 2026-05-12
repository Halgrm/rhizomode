"""
rhizomode プロジェクト構造解説 PDF 生成スクリプト
出力: docs/rhizomode_structure.pdf
"""
from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont
from reportlab.platypus import (
    SimpleDocTemplate,
    Paragraph,
    Spacer,
    Preformatted,
    Table,
    TableStyle,
    PageBreak,
)


pdfmetrics.registerFont(UnicodeCIDFont("HeiseiKakuGo-W5"))
pdfmetrics.registerFont(UnicodeCIDFont("HeiseiMin-W3"))

JP_SANS = "HeiseiKakuGo-W5"
JP_MINCHO = "HeiseiMin-W3"


def build_styles():
    base = getSampleStyleSheet()
    styles = {
        "title": ParagraphStyle(
            "title", parent=base["Title"], fontName=JP_SANS,
            fontSize=22, leading=28, spaceAfter=4,
        ),
        "subtitle": ParagraphStyle(
            "subtitle", parent=base["Normal"], fontName=JP_MINCHO,
            fontSize=11, leading=16, textColor=colors.HexColor("#555555"),
            spaceAfter=18,
        ),
        "h1": ParagraphStyle(
            "h1", parent=base["Heading1"], fontName=JP_SANS,
            fontSize=16, leading=22, spaceBefore=14, spaceAfter=8,
            textColor=colors.HexColor("#1f2a44"),
        ),
        "h2": ParagraphStyle(
            "h2", parent=base["Heading2"], fontName=JP_SANS,
            fontSize=13, leading=18, spaceBefore=10, spaceAfter=4,
            textColor=colors.HexColor("#2d3a5c"),
        ),
        "body": ParagraphStyle(
            "body", parent=base["BodyText"], fontName=JP_MINCHO,
            fontSize=10.5, leading=16, spaceAfter=6,
        ),
        "bullet": ParagraphStyle(
            "bullet", parent=base["BodyText"], fontName=JP_MINCHO,
            fontSize=10.5, leading=15, leftIndent=12, bulletIndent=2,
            spaceAfter=2,
        ),
        "code": ParagraphStyle(
            "code", parent=base["Code"], fontName="Courier",
            fontSize=9, leading=12, backColor=colors.HexColor("#f4f4f7"),
            borderColor=colors.HexColor("#dddde3"), borderWidth=0.5,
            borderPadding=6, leftIndent=0, rightIndent=0, spaceAfter=10,
        ),
        "caption": ParagraphStyle(
            "caption", parent=base["Italic"], fontName=JP_MINCHO,
            fontSize=9, leading=13, textColor=colors.HexColor("#666666"),
            spaceAfter=10,
        ),
    }
    return styles


def p(text, style):
    return Paragraph(text, style)


def bullets(items, style):
    return [Paragraph(f"・{t}", style) for t in items]


def code_block(text):
    return Preformatted(text, style=ParagraphStyle(
        "cb", fontName="Courier", fontSize=8.5, leading=11,
        backColor=colors.HexColor("#f4f4f7"),
        borderColor=colors.HexColor("#dddde3"), borderWidth=0.5,
        borderPadding=6, leftIndent=0, spaceAfter=10,
    ))


def table(data, col_widths, header=True):
    style_cmds = [
        ("FONTNAME", (0, 0), (-1, -1), JP_MINCHO),
        ("FONTSIZE", (0, 0), (-1, -1), 9.5),
        ("LEADING", (0, 0), (-1, -1), 13),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#cccccc")),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]
    if header:
        style_cmds += [
            ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#1f2a44")),
            ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
            ("FONTNAME", (0, 0), (-1, 0), JP_SANS),
        ]
    t = Table(data, colWidths=col_widths, repeatRows=1 if header else 0)
    t.setStyle(TableStyle(style_cmds))
    return t


def build_story(styles):
    s = []

    s.append(p("rhizomode", styles["title"]))
    s.append(p("VRライブパフォーマンス・ノードグラフツール — プロジェクト構造解説",
               styles["subtitle"]))
    s.append(p("ローンチ目標: 2026-05-16  /  Unity 6 + URP  /  PCVR (Quest Link)",
               styles["caption"]))

    s.append(p("1. プロジェクト概要", styles["h1"]))
    s.append(p(
        "rhizomodeは、VR空間内でノードグラフをライブビルドし、"
        "リアルタイム3D演出を構築・制御するソロパフォーマンスツール。"
        "<b>「構築プロセスそのものが演出」</b>というコンセプトで、"
        "ノードを繋ぐ行為自体を観客に見せる。",
        styles["body"]))
    s.extend(bullets([
        "ユーザー: 1人 (本人専用)",
        "デバイス: PCVR固定 (Quest Link via SteamVR)",
        "言語: C# 9 (#nullable enable 全ファイル)",
        "リアクティブ基盤: R3 (NuGetForUnity経由)",
        "Input: Unity Input System 1.18.0",
    ], styles["bullet"]))

    s.append(p("2. レイヤー構成", styles["h1"]))
    s.append(p(
        "上位ほどユーザー入力に近く、下位ほど純粋ロジック。"
        "下位レイヤーは上位を知らない (依存は一方向)。",
        styles["body"]))
    s.append(code_block(
        "┌─────────────────────────────────────────┐\n"
        "│           VR UI Layer                   │\n"
        "│  Menu / Node Display / Status Panel     │\n"
        "├─────────────────────────────────────────┤\n"
        "│           XR Layer                      │\n"
        "│  Controller Input / Ray Interaction     │\n"
        "├─────────────────────────────────────────┤\n"
        "│           Node Graph Layer              │\n"
        "│  NodeBase / Ports / Edges / Context     │\n"
        "├─────────────────────────────────────────┤\n"
        "│           Performance Modules           │\n"
        "│  VFXModule / ShaderModule / ...         │\n"
        "├─────────────────────────────────────────┤\n"
        "│           Audio Layer                   │\n"
        "│  AudioAnalyzer / Device Selection       │\n"
        "├─────────────────────────────────────────┤\n"
        "│           Core Layer                    │\n"
        "│  Type / Serialization / Signal Flow     │\n"
        "└─────────────────────────────────────────┘"
    ))

    s.append(p("3. Assembly Definition (7アセンブリ)", styles["h1"]))
    s.append(p(
        "asmdefレベルで依存方向を強制。循環参照は構造上不可能。",
        styles["body"]))
    s.append(table([
        ["アセンブリ", "役割", "依存先"],
        ["Rhizomode.Core",
         "型システム (ParamType)、ポート、GraphContext、NodeBase、Edge、シリアライズ",
         "(なし)"],
        ["Rhizomode.Nodes",
         "ノード実装 (Input/Math/Time/Utility/Modules)",
         "Core"],
        ["Rhizomode.Modules",
         "IPerformanceModule 実装 (VFXModule, ShaderModule)",
         "Core"],
        ["Rhizomode.UI",
         "VR内UI (メニュー、ノード表示、エッジ、ステータスパネル)",
         "Core, Nodes"],
        ["Rhizomode.XR",
         "コントローラー入力、レイ操作、各種ハンドラー",
         "Core, UI"],
        ["Rhizomode.Audio",
         "AudioAnalyzer、デバイス管理",
         "Core"],
        ["Rhizomode.ExternalInput",
         "OSC (OscJack) / MIDI (Minis) 入力",
         "Core"],
    ], col_widths=[42 * mm, 90 * mm, 30 * mm]))

    s.append(Spacer(1, 8))
    s.append(p("依存方向:", styles["h2"]))
    s.append(code_block(
        "XR  ──▶  UI  ──▶  Nodes  ──▶  Core\n"
        "                  Modules ──▶  Core\n"
        "                  Audio   ──▶  Core\n"
        "                  ExternalInput ──▶ Core"
    ))

    s.append(PageBreak())

    s.append(p("4. キーとなる設計パターン", styles["h1"]))

    s.append(p("4.1 Reactive Push モデル", styles["h2"]))
    s.append(p(
        "ノードは <b>Setup(GraphContext)</b> 内でR3 Observableチェーンを構築。"
        "値が変化したときだけ下流が発火する。Updateループは "
        "Time / LFO / Noise ノードのみが持つ。",
        styles["body"]))

    s.append(p("4.2 インターフェース境界", styles["h2"]))
    s.append(p(
        "モジュール通信は <b>IPerformanceModule</b>、"
        "ポートは <b>IOutputPort / IInputPort</b> 経由。"
        "asmdef境界を越えて具象型に依存しない。",
        styles["body"]))

    s.append(p("4.3 object経由の型柔軟性", styles["h2"]))
    s.append(p(
        "ポート内部値は <b>object</b>。型チェックは接続時に "
        "<b>ParamType</b> enum (Float/Color/Bool) で行う。"
        "後からVector3等を増やしてもポートインターフェースは変えない。",
        styles["body"]))

    s.append(p("4.4 Defensive Runtime", styles["h2"]))
    s.append(p(
        "外部呼び出しは全て try-catch、NaN / null はデフォルト値にフォールバック。"
        "<b>映像は絶対に止めない</b> がパフォーマンス上の鉄則。",
        styles["body"]))

    s.append(p("4.5 ModuleDefinition (ScriptableObject)", styles["h2"]))
    s.append(p(
        "演出モジュールのパラメータ定義。モジュールノードを生成すると "
        "ConstFloat / ConstColor が自動スポーンされ、全パラメータに事前接続される。"
        "= ライブ中にゼロ接続のモジュールが存在しない。",
        styles["body"]))

    s.append(p("4.6 モジュールライフサイクル分離", styles["h2"]))
    s.append(p(
        "Factory はノードオブジェクトを作るだけ。プレハブ生成と "
        "IPerformanceModule の注入は別タイミング:",
        styles["body"]))
    s.extend(bullets([
        "メニュー作成時:  InjectModuleIfNeeded",
        "グラフロード時:  ReinjectModulesAfterLoad",
    ], styles["bullet"]))

    s.append(p("5. ディレクトリ構造", styles["h1"]))
    s.append(code_block(
        "rhizomode/                        (Unityプロジェクトルート)\n"
        "└─ Assets/\n"
        "   ├─ Runtime/\n"
        "   │  ├─ Core/         NodeBase, GraphContext, Ports, Edge, ParamType\n"
        "   │  ├─ Nodes/        Input/ Math/ Modules/ Time/ Utility/ Generators/\n"
        "   │  ├─ Modules/      IPerformanceModule 実装\n"
        "   │  ├─ UI/           VRメニュー, ノード表示, エッジ, ステータス, ミラー\n"
        "   │  ├─ XR/           コントローラー入力, レイ, インタラクションハンドラー\n"
        "   │  ├─ Audio/        AudioAnalyzer, デバイス選択\n"
        "   │  └─ ExternalInput/ OscServer, MidiServer, OscReceiver/MidiCC ノード\n"
        "   ├─ Data/\n"
        "   │  ├─ ModuleDefinitions/   ScriptableObject (各モジュール定義)\n"
        "   │  ├─ Environments/\n"
        "   │  └─ SavedGraphs/         live_set_*.json\n"
        "   ├─ Shaders/\n"
        "   ├─ VFX/\n"
        "   └─ Scenes/"
    ))

    s.append(PageBreak())

    s.append(p("6. VR UI パイプライン", styles["h1"]))
    s.append(p(
        "VRコントローラーのレイがUIToolkitのWorldSpaceパネルを操作する流れ。"
        "Unity 6 のUIToolkitはWorldSpace未対応のため、"
        "RenderTexture + reflectionでイベントを注入している。",
        styles["body"]))
    s.append(code_block(
        "ControllerInputRouter  (IRayProvider + IControllerInput)\n"
        "         │  RayOrigin / RayDirection\n"
        "         ▼\n"
        "SharedRaycastService   (毎フレーム Physics.Raycast、結果を共有)\n"
        "         │  RaycastHit\n"
        "         ▼\n"
        "WorldPanelRayBridge    (reflection で UIToolkit イベント注入)\n"
        "         │  PointerDown / PointerUp / Hover\n"
        "         ▼\n"
        "UIToolkit Panel        (WorldPanelHost 上の RenderTexture)"
    ))
    s.extend(bullets([
        "ノードは MeshCollider 付き Quad。前面のみレイキャスト可 "
        "(プレイヤー方向を向いて生成)",
        "PanelSettings はテーマ付きテンプレートからクローン (Unity 6 要件)",
        "メニュー非表示は SetActive(false) ではなく "
        "MeshRenderer/MeshCollider.enabled トグル "
        "(UIDocument破壊防止)",
    ], styles["bullet"]))

    s.append(p("7. 型システム (ParamType)", styles["h1"]))
    s.append(table([
        ["型", "用途", "備考"],
        ["Float", "連続値。パラメータ制御全般。",
         "ConstFloat は 0〜1 固定、Remap で変換"],
        ["Color", "色。HSV ホイール入力。", "—"],
        ["Bool", "トリガー / ゲート",
         "VFX SendEvent、Activate/Deactivate、条件分岐"],
        ["Vector3", "後日追加予定", "object経由なのでI/F変更不要"],
    ], col_widths=[24 * mm, 60 * mm, 76 * mm]))

    s.append(p("8. 実装状況 (2026-04-28 時点)", styles["h1"]))
    s.append(table([
        ["フェーズ", "内容", "状態"],
        ["Week 1", "Core / asmdef / R3 / シリアライズ", "完了"],
        ["Week 2", "XR入力 / 巻物メニュー / WorldSpace ノード", "完了"],
        ["Week 3", "エッジ接続・切断 / ノード削除・グラブ", "完了"],
        ["Week 4", "全24ノード実装 / Audio / OSC・MIDI", "完了"],
        ["Week 5", "セーブロード / ミラー出力 / Spout・NDI", "完了"],
        ["Week 6", "バグ修正 / モジュール自動スポーン", "完了"],
        ["Week 6.5〜", "演出VFX/Shader制作 / 通しリハ / v0.3.0タグ", "進行中"],
    ], col_widths=[26 * mm, 110 * mm, 24 * mm]))

    s.append(Spacer(1, 8))
    s.append(p(
        "実装済24ノード: ConstFloat, ConstColor, AudioTrigger, BeatDetector, "
        "TapTempo, OscReceiver, MidiCC, Multiply, Add, Remap, Smooth, "
        "Time, Timer, Delay, LFO, Noise, Threshold, Toggle, "
        "ColorToFloats, FloatsToColor, ColorToHSV, HSVToColor, SceneObject, "
        "VFXModuleNode, ShaderModuleNode, FloatMonitor, BoolMonitor, ColorMonitor",
        styles["caption"]))

    s.append(p("9. パフォーマンス予算", styles["h1"]))
    s.extend(bullets([
        "ノード上限: 約60個 (モジュール5〜10 + Math/Control 20〜50)",
        "VR: 90fps 必達、ミラー出力: 60fps",
        "VRレンダリング: Single Pass Instanced (URP)",
        "ShaderModule は MaterialPropertyBlock 使用 (マテリアル複製なし)、"
        "LateUpdate でバッチ化",
    ], styles["bullet"]))

    s.append(p("10. 破壊的変更ルール (重要)", styles["h1"]))
    s.append(p(
        "以下は<b>クリティカルバグ修正以外で禁止</b>:",
        styles["body"]))
    s.extend(bullets([
        "インターフェースシグネチャの追加・削除・変更",
        "publicメソッドのシグネチャ変更",
        "シリアライズJSONフィールド名 / ノードポート名のリネーム・削除",
        "asmdef依存方向の変更",
    ], styles["bullet"]))
    s.append(p(
        "拡張は<b>新インターフェース追加 + is キャスト</b>、"
        "またはシリアライズフィールドの<b>末尾追加 + 安全なデフォルト</b>で対応する。",
        styles["body"]))

    return s


def main():
    here = Path(__file__).resolve().parent.parent
    out = here / "docs" / "rhizomode_structure.pdf"
    out.parent.mkdir(parents=True, exist_ok=True)

    doc = SimpleDocTemplate(
        str(out),
        pagesize=A4,
        leftMargin=18 * mm, rightMargin=18 * mm,
        topMargin=18 * mm, bottomMargin=16 * mm,
        title="rhizomode — Project Structure",
        author="rhizomode",
    )
    styles = build_styles()
    doc.build(build_story(styles))
    print(f"wrote: {out}")


if __name__ == "__main__":
    main()
