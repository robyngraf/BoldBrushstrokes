using PaintDotNet;
using PaintDotNet.Imaging;
using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal static class ColourExtensions
{
    public static ColorRgba128Float ToLinear(this ColorBgra32 color)
    {
        ColorRgba128Float c = color;
        c.R = Math.Clamp((float)Math.Pow(c.R, 2.2), 0, 255);
        c.G = Math.Clamp((float)Math.Pow(c.G, 2.2), 0, 255);
        c.B = Math.Clamp((float)Math.Pow(c.B, 2.2), 0, 255);
        return c;
    }

    //ColorPrgba128Float

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DistanceTo(this ColorBgra32 a, ColorBgra32 b) => DistanceTo(a.ToPremultiplied(), b.ToPremultiplied());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DistanceTo(this ColorPbgra32 a, ColorPbgra32 b) => FastMaths.Sqrt((a.R - b.R).Squared() + (a.G - b.G).Squared() + (a.B - b.B).Squared() + (a.A - b.A).Squared());

    public static int RoundTripDistanceTo(this ColorBgra32 a, ColorBgra32 b, ColorBgra32 c)
    {
        var ap = a.ToPremultiplied();
        var bp = b.ToPremultiplied();
        var cp = c.ToPremultiplied();
        return DistanceTo(ap, bp) + DistanceTo(bp, cp) + DistanceTo(cp, ap);
    }

    /// <summary>
    /// Hue as a value from 0 to 360
    /// </summary>
    public static int GetHue(this ColorBgra32 color)
    {
        (int r, int g, int b) = (color.R, color.G, color.B);
        if (r == g && g == b) return 0;

        (int min, int max) = MinMaxRgb(r, g, b);

        int delta = max - min;
        int hue;

        if (r == max)
            hue = (g - b) * 60 / delta;
        else if (g == max)
            hue = (b - r) * 60 / delta + 120;
        else
            hue = (r - g) * 60 / delta + 240;

        return (hue + 360) % 360;
    }

    /// <summary>
    /// Saturation as a value from 0 - 255
    /// </summary>
    public static int GetSaturation(this ColorBgra32 color)
    {
        (int r, int g, int b) = (color.R, color.G, color.B);
        if (r == g && g == b) return 0;

        (int min, int max) = MinMaxRgb(r, g, b);

        int div = max + min;
        if (div > byte.MaxValue)
            div = byte.MaxValue * 2 - max - min;

        return (max - min) * 255 / div;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int min, int max) MinMaxRgb(int r, int g, int b)
    {
        (int min, int max) = g < r ? (g, r) : (r, g);
        if (b > max) max = b;
        else if (b < min) min = b;
        return (min, max);
    }
}
