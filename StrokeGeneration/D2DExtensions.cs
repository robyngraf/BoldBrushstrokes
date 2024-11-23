using ComputeSharp.D2D1;
using ComputeSharp.D2D1.Interop;
using PaintDotNet.Direct2D1;
using IDeviceContext = PaintDotNet.Direct2D1.IDeviceContext;

namespace BrushStrokes;

internal static class D2DExtensions
{
    public static IDeviceEffect CreateEffect<T>(this IDeviceContext dc) where T : unmanaged, ID2D1PixelShader
    {
        dc.Factory.RegisterEffectFromBlob(D2D1PixelShaderEffect.GetRegistrationBlob<T>(out Guid shaderEffectID));
        return dc.CreateEffect(shaderEffectID);
    }

    public static void SetValues<T>(this IDeviceEffect effect, T shader) where T : unmanaged, ID2D1PixelShader => effect.SetValue(D2D1PixelShaderEffectProperty.ConstantBuffer, D2D1PixelShader.GetConstantBuffer(shader));

    public static void Clear(this ICommandList impastoCommandList, IDeviceContext dc)
    {
        using var targetScope = dc.UseTarget(impastoCommandList);
        using var drawScope = dc.UseBeginDraw();
        dc.Clear();
    }
}
