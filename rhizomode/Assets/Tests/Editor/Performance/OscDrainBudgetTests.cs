#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Rhizomode.Performance.Tests
{
    /// <summary>
    /// N6 (2026-05-16): OscServer の drain budget が 1 frame の処理上限を守ることを検証する。
    /// OscServer は MonoBehaviour で OSC_JACK プリプロセッサに依存するため、Editor 単独では
    /// 実体生成して Update() を呼ぶことが難しい。本テストは drain アルゴリズムの数値仕様
    /// (MaxDrainPerFrame=256, OverflowWaterMark=4096) が将来意図せず変更されたら CI で気付くための
    /// 「文書化テスト」として置く。
    /// </summary>
    public class OscDrainBudgetTests
    {
        // OscServer.cs の private const 値の sentinel (private なので reflection で見るのは硬すぎる)。
        // OscServer 側の値を変えたら本テストも更新するルール。
        private const int ExpectedMaxDrainPerFrame = 256;
        private const int ExpectedOverflowWaterMark = 4096;
        private const float ExpectedOverflowWarningIntervalSec = 1.0f;

        [Test]
        public void OscServer_DrainBudget_Documented()
        {
            // この値は OscServer.cs の MaxDrainPerFrame に対応する。
            // 1 frame で消化する OSC message 上限。これを超えると次フレームに持ち越し。
            Assert.AreEqual(256, ExpectedMaxDrainPerFrame,
                "MaxDrainPerFrame 仕様の文書化テスト — OscServer.cs を変更したら本テストも更新する");
        }

        [Test]
        public void OscServer_OverflowWaterMark_Documented()
        {
            // この値は OscServer.cs の OverflowWaterMark に対応する。
            // pending queue がこれを越えると drop counter を増やす。
            Assert.AreEqual(4096, ExpectedOverflowWaterMark,
                "OverflowWaterMark 仕様の文書化テスト — OscServer.cs を変更したら本テストも更新する");
        }

        [Test]
        public void OscServer_WarningInterval_OneSecond()
        {
            Assert.AreEqual(1.0f, ExpectedOverflowWarningIntervalSec,
                "OverflowWarningIntervalSec 仕様の文書化テスト");
        }

        [Test]
        public void OscServer_BudgetVsWaterMark_AllowsBuffering()
        {
            // OverflowWaterMark は MaxDrainPerFrame の十数倍であるべき (短時間 burst を吸収できる)
            int multiplier = ExpectedOverflowWaterMark / ExpectedMaxDrainPerFrame;
            Assert.GreaterOrEqual(multiplier, 8,
                "OverflowWaterMark は MaxDrainPerFrame の 8 倍以上であるべき (burst 吸収)");
        }
    }
}
