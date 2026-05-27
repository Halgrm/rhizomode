#nullable enable

using System.Runtime.CompilerServices;

// Rhizomode.Interaction.Tests から WindowGrabHandle の internal static (LockRollClampPitch /
// ComputeTwoHandScale) を pure-math テストするため公開対象に追加。
[assembly: InternalsVisibleTo("Rhizomode.Interaction.Tests")]
