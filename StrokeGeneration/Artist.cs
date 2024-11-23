using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BrushStrokes;

internal delegate int SortFunction(Stroke s);

internal enum DirectionTypeOptions
{
    TowardsSimilarPixels,
    DependsOnColour,
    OneDirection
}

internal static class Artist
{
    // Algorithm tuning
    private const int NumberOfAngleSteps = 18;
    private const double StepAngleInRadians = Math.PI / (NumberOfAngleSteps + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DegreesToRadians(double angle) => angle * (Math.PI / 180d);

    private static readonly Vector2[] SinCos = PrecalculateFunction(x => new Vector2((float)Math.Sin(x), (float)Math.Cos(x)), StepAngleInRadians, NumberOfAngleSteps);
    private static T[] PrecalculateFunction<T>(Func<double, T> function, double step, int numberOfSteps)
        => Enumerable.Range(0, numberOfSteps)
        .Select(i => function(i * step))
        .ToArray();

    public static List<Stroke> GenerateStrokes(RectInt32 srcBounds, RectInt32 sampleArea, int stride, RegionPtr<ColorBgra32> sourceRegion, int radius, SortFunction SortFunction1, SortFunction SortFunction2, DirectionTypeOptions DirectionType, double StrokeDirection)
    {
        // Precalculate stroke angles before sampling pixels
        Vector2Int32[]? directions = null;
        Vector2Double normal = default;
        switch (DirectionType)
        {
            case DirectionTypeOptions.TowardsSimilarPixels:
                {
                    List<Vector2Int32> d = new();
                    Vector2Int32 previousVector = Vector2Int32.Zero;
                    foreach (var norm in SinCos)
                    {
                        Vector2Int32 vector = (norm * radius).Round();
                        if (vector == previousVector) continue;
                        previousVector = vector;
                        d.Add(vector);
                    }
                    directions = d.ToArray();
                }
                break;
            case DirectionTypeOptions.OneDirection:
                {
                    (normal.X, normal.Y) = Math.SinCos(DegreesToRadians(StrokeDirection - 90));
                }
                break;
        }

        var rows = ParallelEnumerable.Range(0, sampleArea.Height / stride);
        var strokes = rows.SelectMany(row =>
        {
            int y = sampleArea.Top + row * stride;
            List<Stroke> newStrokes = new();
            for (int x = sampleArea.Left; x < sampleArea.Right; x += stride)
            {
                Point2Int32 samplePosition = new(x + (Deterministic.Randomish(y + stride, x) % stride), y + (Deterministic.Randomish(x + stride, y) % stride));

                ColorBgra32 currentPixel = sourceRegion[samplePosition.Clamp(srcBounds)];
                //if (currentPixel.A == 0) continue;

                ColorPrgba128Float currentPixelPL = currentPixel.ToLinear().ToPremultiplied();

                Point2Int32 start = default;
                ColorBgra32 startColor = default;
                ColorBgra32 midColor = default;
                ColorBgra32 endColor = default;
                Point2Int32 end = default;

                switch (DirectionType)
                {
                    case DirectionTypeOptions.TowardsSimilarPixels:
                        {
                            float minDifference = float.MaxValue;
                            for (int i = directions!.Length - 1; i >= 0; i--)
                            {
                                Vector2Int32 offset = directions.DangerousGetReferenceAt(i);
                                Point2Int32 close = samplePosition + offset;
                                Point2Int32 closeSample = close.Clamp(srcBounds);
                                //if (samplePosition == closeSample) continue;

                                ColorBgra32 comparePixel = sourceRegion[closeSample];
                                //if (comparePixel.A == 0) continue;

                                Point2Int32 mid = samplePosition - offset;
                                var midSample = mid.Clamp(srcBounds);
                                ColorBgra32 midComparePixel = sourceRegion[midSample];
                                //if (midComparePixel.A == 0) continue;

                                var difference = currentPixel.DistanceTo(comparePixel) + currentPixel.DistanceTo(midComparePixel);
                                if (difference < minDifference)
                                {
                                    minDifference = difference;
                                    end = close;
                                    endColor = comparePixel;
                                    start = mid;
                                    startColor = midComparePixel;
                                }
                            }
                            if (minDifference == float.MaxValue) continue;
                            midColor = currentPixel;
                        }
                        break;
                    case DirectionTypeOptions.DependsOnColour:
                        {
                            // angle should fade from AngleAdjust - 90 at white, to GetHue() in colours, back to AngleAdjust - 90 at black
                            var greyness = 1f - currentPixel.GetSaturation();
                            var pumpedUpSaturation = 1f - greyness * greyness;
                            double angle = StrokeDirection - 90d + (((currentPixel.GetHue() + 180) % 360) - 180) * pumpedUpSaturation;
                            (normal.X, normal.Y) = Math.SinCos(DegreesToRadians(angle));
                            start = samplePosition;
                            startColor = currentPixel;
                        }
                        goto case DirectionTypeOptions.OneDirection;
                    case DirectionTypeOptions.OneDirection:
                        {
                            end = samplePosition + (normal * radius).Round();
                            endColor = sourceRegion[end.Clamp(srcBounds)];
                            if (endColor.A == 0) continue;
                            midColor = sourceRegion[(samplePosition + (normal * (radius * 0.5)).Round()).Clamp(srcBounds)];
                            if (midColor.A == 0) continue;
                            start = samplePosition;
                            startColor = currentPixel;
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
                ColorBgra32 strokeColour = DirectionType switch
                {
                    DirectionTypeOptions.TowardsSimilarPixels => ColorBgra.BlendColors4Fast(startColor, midColor, midColor, endColor),
                    DirectionTypeOptions.DependsOnColour or DirectionTypeOptions.OneDirection => midColor,
                    _ => throw new NotImplementedException()
                };
                var stroke = new Stroke { StartPosition = start, EndPosition = end, DrawColor = strokeColour, StartColor = startColor, MidColor = midColor, EndColor = endColor };

                stroke.Sort1 = SortFunction1(stroke);
                stroke.Sort2 = SortFunction2(stroke);

                newStrokes.Add(stroke);
            }

            return newStrokes;
        });
        return strokes.ToList();
    }
}
