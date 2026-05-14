#nullable enable

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// C# 9 record init setter polyfill。.NET Standard 2.1 / Unity (.NET Framework 4.8 互換)
    /// では <see cref="IsExternalInit"/> が標準提供されないため、asmdef 単位で internal に
    /// 定義する。
    /// </summary>
    internal static class IsExternalInit { }
}
