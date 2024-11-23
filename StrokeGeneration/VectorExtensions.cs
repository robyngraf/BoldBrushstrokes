using PaintDotNet.Rendering;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal static class VectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int32 Round(this Vector2Double a) => new(a.X.Round(), a.Y.Round());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int32 Round(this Vector2 a) => new((int)MathF.Round(a.X), (int)MathF.Round(a.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point2Int32 Clamp(this Point2Int32 a, RectInt32 b) => new(ClampExclusiveOfMax(a.X, b.Left, b.Right), ClampExclusiveOfMax(a.Y, b.Top, b.Bottom));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ClampExclusiveOfMax(int value, int min, int max)
    {
        if (value < min) return min;
        if (value >= max) return max - 1;
        return value;
    }
}
