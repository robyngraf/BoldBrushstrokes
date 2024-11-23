using ComputeSharp.D2D1;
using ComputeSharp;

namespace ImpastoEffect;

[D2DInputCount(2)]
[D2DInputSimple(0)]
[D2DInputSimple(1)]
[AutoConstructor]
internal readonly partial struct OverlayImpastoShader : ID2D1PixelShader
{
    private readonly float impastoFraction;
    public Float4 Execute()
    {
        Float4 image = D2D.GetInput(0);
        Float4 impasto = D2D.GetInput(1);
        return image + new Float4(impastoFraction * (impasto.RGB - 0.5f), 0);
        //return image + new Float4(impastoFraction * (impasto.RGB - 0.7647059f), 0);
    }
}