#nullable enable

// C# 9 record / init-only setters polyfill for this asmdef (compiler looks per-assembly).
// See Rhizomode.SharedKernel for the original copy.

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
