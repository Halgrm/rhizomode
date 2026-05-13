#nullable enable

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// グラフコマンドの発行元 (audit + CI 検証用)。
    /// </summary>
    /// <remarks>
    /// Plan v5.1-2: 当初 SharedKernel に置いたが、graph command 監査概念のため Graph.Mutation 配下に降ろした。
    ///
    /// CI 検証 (Phase 1G/Boundary CI、Roslyn/reflection ベースの単体テスト):
    /// - Interaction.GraphAdapter 配下が new するコマンドの Origin == <see cref="Interaction"/>
    /// - UI.GraphAdapter 配下が new するコマンドの Origin == <see cref="Ui"/>
    /// - 各 *.GraphAdapter は対応する Origin を使う
    /// - <see cref="Test"/> は Tests asmdef 配下のみ許可
    /// - 同一 command kind が 2 種類以上の Origin から発行されたら warning
    /// </remarks>
    public enum CommandOrigin
    {
        /// <summary>空間操作 (Interaction.GraphAdapter)。</summary>
        Interaction,

        /// <summary>panel/button (UI.GraphAdapter)。</summary>
        Ui,

        /// <summary>Audio.GraphAdapter (将来用)。</summary>
        Audio,

        /// <summary>OscMidi.GraphAdapter。</summary>
        OscMidi,

        /// <summary>Ableton.GraphAdapter。</summary>
        Ableton,

        /// <summary>Scene.GraphAdapter。</summary>
        Scene,

        /// <summary>Modules.Runtime。</summary>
        Module,

        /// <summary>将来の Command Replay 用。</summary>
        Replay,

        /// <summary>ユニットテスト用。Tests asmdef 配下のみ許可。</summary>
        Test
    }
}
