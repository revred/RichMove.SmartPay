using System;
using System.Runtime.CompilerServices;

namespace RichMove.SmartPay.Core.Guards;

internal static class Ensure
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T>(T? value, string paramName) where T : class
        => ArgumentNullException.ThrowIfNull(value, paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T>(T? value, string paramName) where T : struct
    {
        if (value == null) throw new ArgumentNullException(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
    }
}