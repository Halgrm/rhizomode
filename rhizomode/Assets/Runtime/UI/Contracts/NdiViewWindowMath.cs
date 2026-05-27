#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// NDI window の deterministic 配置計算ヘルパー。
    /// </summary>
    /// <remarks>
    /// <para>用途: 複数 NDI receiver node が同時 spawn されたとき、各 window を中央
    /// (HMD 正面) で重ねず cascade 状にずらして配置する。同じ nodeId は session 跨ぎで
    /// 同じ offset を返すことが要件 (cue save → 別 process load の再現性)。</para>
    ///
    /// <para><b>StableHash32:</b> <see cref="string.GetHashCode"/> は .NET Core 以降
    /// process 起動毎に randomize される (security 対策)。これを cascade index に
    /// 使うと session を跨いで window 位置が変わってしまうため、自前の FNV-1a 32bit
    /// 実装を使う。FNV-1a は単純で衝突耐性も十分、salt 無しで deterministic。</para>
    /// </remarks>
    public static class NdiViewWindowMath
    {
        /// <summary>cascade 配置の slot 数 (= 同時表示で重ねず置ける window 数の目安)。</summary>
        public const int CascadeSlots = 8;

        /// <summary>side spacing (window collider 1.0m × 0.5625m に対して 1.2m なら重ならない)。</summary>
        public const float SideSpacing = 1.2f;

        /// <summary>奥行きへの後退量 (slot ごとに 30cm 後ろへ)。</summary>
        public const float DepthSpacing = 0.3f;

        /// <summary>FNV-1a 32bit hash。session / process 跨ぎで stable。</summary>
        public static uint StableHash32(string s)
        {
            const uint OffsetBasis = 2166136261u;
            const uint Prime = 16777619u;
            if (string.IsNullOrEmpty(s)) return OffsetBasis;
            uint h = OffsetBasis;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= Prime;
            }
            return h;
        }

        /// <summary>
        /// 指定 nodeId に対する cascade 位置オフセット (HMD 基準座標系)。
        /// 同じ nodeId は何度呼んでも同じ値を返す。
        /// </summary>
        public static Vector3 CascadeOffset(string nodeId, Vector3 hmdForward, Vector3 hmdRight)
        {
            int idx = (int)(StableHash32(nodeId) % (uint)CascadeSlots);
            // slot 0,2,4,6 は右、1,3,5,7 は左。slot 番号が大きいほど中央から遠ざかる。
            float side = (idx % 2 == 0 ? 1f : -1f) * SideSpacing * ((idx / 2) + 1);
            float depth = -DepthSpacing * (idx % 4); // 0,30,60,90cm の 4 段で奥にずれる
            return hmdRight * side + hmdForward * depth;
        }
    }
}
