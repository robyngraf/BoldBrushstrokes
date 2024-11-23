using ComputeSharp.D2D1;
using ComputeSharp;

namespace BoldBrushEffect;

[D2DInputCount(2)]
[D2DInputSimple(0)]
[D2DInputSimple(1)]
[AutoConstructor]
internal readonly partial struct ResetAlphaShader
    : ID2D1PixelShader
{
    public float4 Execute()
    {
        float4 original = D2D.GetInput(0);
        float4 toBeRestored = D2D.GetInput(1);

        return new(toBeRestored.RGB * original.A, original.A);
    }
}