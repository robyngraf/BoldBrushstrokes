﻿using ComputeSharp.D2D1;
using ComputeSharp;

namespace ImpastoEffect;

/// <summary>
/// Straight alpha input, premultiplied alpha output
/// </summary>
[D2DInputCount(2)]
[D2DInputSimple(0)]
[D2DInputSimple(1)]
[AutoConstructor]
internal readonly partial struct RestoreFineDetailsAndResetAlphaShader
    : ID2D1PixelShader
{
    private readonly float preserveFineDetailsFraction;

    public float4 Execute()
    {
        float4 original = D2D.GetInput(0);
        float4 strokes = D2D.GetInput(1);

        // Restore edges
        float deviation = Hlsl.Distance(strokes, original);
        float amountOfOriginalToPutBack = Hlsl.Clamp(deviation * preserveFineDetailsFraction * 10, 0, 1);
        float4 withDetailsRestored = Hlsl.Lerp(strokes, original, amountOfOriginalToPutBack);

        // Restore alpha and premultiply
        float4 withAlphaRestored = new(withDetailsRestored.RGB * original.A, original.A);

        return withAlphaRestored;
    }
}