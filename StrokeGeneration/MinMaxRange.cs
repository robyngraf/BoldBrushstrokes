using System.Numerics;
using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal struct MinMaxRange<T> where T : INumber<T>, IMinMaxValue<T>
{
    public T Min = T.MaxValue;
    public T Max = T.MinValue;

    public MinMaxRange() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value)
    {
        if (value < Min) Min = value;
        if (value > Max) Max = value;
    }

    public T Range => Max - Min;
}