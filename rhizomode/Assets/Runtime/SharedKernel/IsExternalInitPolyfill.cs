#nullable enable

// C# 9 の record / init-only setters は System.Runtime.CompilerServices.IsExternalInit を必要とするが、
// Unity 6 の .NET Standard 2.1 / Mono ランタイムには含まれていない。
// このファイルは polyfill として最小定義を提供する。

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
