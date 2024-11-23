using PaintDotNet;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal static class Throw
{
    public static void ShouldNotBeNull([NotNull] this object? o, [CallerArgumentExpression("o")] string oName = "", [CallerMemberName] string caller = "")
    {
        if (o is null) throw new NullReferenceException($"{oName} is null in {caller}");
    }

    public static void ShouldNotBeNullOrDisposed([NotNull] this IIsDisposed? o, [CallerArgumentExpression("o")] string oName = "", [CallerMemberName] string caller = "")
    {
        if (o is null) throw new NullReferenceException($"{oName} is null in {caller}");
        if (o.IsDisposed) throw new ObjectDisposedException($"{oName} is already disposed in {caller}");
    }
}