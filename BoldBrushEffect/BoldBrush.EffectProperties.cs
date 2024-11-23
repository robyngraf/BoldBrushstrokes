using BrushStrokes;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.PropertySystem;

namespace BoldBrushEffect;

public sealed partial class BoldBrush
{
    private readonly record struct EffectProperties
    {
        public readonly int Radius;
        public readonly int StrokeWidthPercent;
        public readonly int Blendiness;
        public readonly int PreserveFineDetails;
        public readonly BackgroundOptions Background;
        public readonly string StrokeStyle = "Random";
        public readonly DirectionTypeOptions DirectionType;
        public readonly double StrokeDirection;
        public readonly EmphasisOptions Emphasis;

        public EffectProperties() { }

        public EffectProperties(PropertyBasedEffectConfigToken? token)
        {
            if (token is null) return;
            Radius = token.GetProperty<Int32Property>(PropertyNames.StrokeLength).Value;
            StrokeWidthPercent = token.GetProperty<Int32Property>(PropertyNames.StrokeWidthPercent).Value;
            Blendiness = token.GetProperty<Int32Property>(PropertyNames.Blendiness).Value;
            PreserveFineDetails = token.GetProperty<Int32Property>(PropertyNames.PreserveFineDetails).Value;
            Background = (BackgroundOptions)token.GetProperty<StaticListChoiceProperty>(PropertyNames.Background).Value;
            StrokeStyle = (string)token.GetProperty<StaticListChoiceProperty>(PropertyNames.StrokeStyle).Value;
            DirectionType = (DirectionTypeOptions)token.GetProperty<StaticListChoiceProperty>(PropertyNames.DirectionType).Value;
            StrokeDirection = token.GetProperty<DoubleProperty>(PropertyNames.StrokeDirection).Value;
            Emphasis = (EmphasisOptions)token.GetProperty<StaticListChoiceProperty>(PropertyNames.Emphasis).Value;
        }

        public (int, int, int, string, DirectionTypeOptions, double, EmphasisOptions) DrawingProperties =>
            new(Radius, StrokeWidthPercent, Blendiness, StrokeStyle, DirectionType, StrokeDirection, Emphasis);

        public (int, BackgroundOptions) ShaderProperties => (PreserveFineDetails, Background);
    }
}