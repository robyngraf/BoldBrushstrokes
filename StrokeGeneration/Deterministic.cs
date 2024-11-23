using BrushStrokes;
using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal static class Deterministic
{
    /// <summary>
    /// Get a randomish number quickly
    /// </summary>
    /// <returns>A deterministic number from a random-looking sequence that rarely repeats, with a range of int.MinValue to int.MaxValue</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Randomish(int x, int y)
    {
        ulong v = Concat(y, x) * 11400714819323204462;
        return (int)((v + (ulong)x) >>> 32) ^ (int)((v + (ulong)y) >>> 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ChooseRandomish<T>(this T[] array, int x, int y) => array.DangerousGetManagedReferenceAt(RandomPositive(x, y) % array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RandomPositive(int x, int y) => MakePositive(Randomish(x, y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Concat(int high, int low) => (((ulong)high) << 32) | ((uint)low);

    /// <summary>
    /// Makes a number positive. Works for entire range of int.
    /// </summary>
    /// <remarks>Does not produce the same result as <see cref="Math.Abs(int)"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MakePositive(int a) => a & int.MaxValue;
}