using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal static class FastMaths
{
    /// <summary>Absolute value</summary>
    /// <remarks>
    /// N.B. this method gives a wrong (negative) answer for <see cref="int.MinValue"/> instead of throwing an exception.
    /// Use it only where the range of possible inputs does not include <see cref="int.MinValue"/>.
    /// <para>
    /// In debug mode <see cref="FastMaths.Abs(int)"/> is slower than <see cref="Math.Abs(int)"/>.
    /// </para>
    /// In release mode <see cref="FastMaths.Abs(int)"/> is about 10 times faster than <see cref="Math.Abs(int)"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int a) => (a + (a >>= 31)) ^ a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Squared(this int a) => a * a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sqrt(int n) => Math.Sqrt(n).Round();


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Round(this double a) => (int)Math.Floor(a + 0.5);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Round2(this double a) => Convert.ToInt32(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TwoToThePowerOf(int n) => 1 << n;
}
