using BrushStrokes;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.PropertySystem;

namespace ImpastoEffect;

public sealed partial class Impasto
{
    private readonly record struct EffectProperties
    {
        public readonly int Radius;
        public readonly int StrokeWidthPercent;
        public readonly int Blendiness;
        public readonly int PreserveFineDetails;
        public readonly string StrokeStyle = "Random";
        public readonly bool Antialias;
        public readonly DirectionTypeOptions DirectionType;
        public readonly double StrokeDirection;
        public readonly EmphasisOptions Emphasis;
        public readonly ColorBgra32 EmphasisColour;
        public readonly int ImpastoPercent;
        public readonly int ImpastoFineStrokesPercent;
        public readonly double ImpastoDirection;

        public EffectProperties() { }

        public EffectProperties(PropertyBasedEffectConfigToken? token)
        {
            if (token is null) return;
            Radius = token.GetProperty<Int32Property>(PropertyNames.StrokeLength).Value;
            StrokeWidthPercent = token.GetProperty<Int32Property>(PropertyNames.StrokeWidthPercent).Value;
            Blendiness = token.GetProperty<Int32Property>(PropertyNames.Blendiness).Value;
            PreserveFineDetails = token.GetProperty<Int32Property>(PropertyNames.PreserveFineDetails).Value;
            StrokeStyle = (string)token.GetProperty<StaticListChoiceProperty>(PropertyNames.StrokeStyle).Value;
            Antialias = token.GetProperty<BooleanProperty>(PropertyNames.Antialias).Value;
            DirectionType = (DirectionTypeOptions)token.GetProperty<StaticListChoiceProperty>(PropertyNames.DirectionType).Value;
            StrokeDirection = token.GetProperty<DoubleProperty>(PropertyNames.StrokeDirection).Value;
            Emphasis = (EmphasisOptions)token.GetProperty<StaticListChoiceProperty>(PropertyNames.Emphasis).Value;
            EmphasisColour = ColorBgra32.FromUInt32(unchecked((uint)token.GetProperty<Int32Property>(PropertyNames.EmphasisColour).Value));

            ImpastoPercent = token.GetProperty<Int32Property>(PropertyNames.ImpastoPercent).Value;
            ImpastoFineStrokesPercent = token.GetProperty<Int32Property>(PropertyNames.ImpastoFineStrokesPercent).Value;
            ImpastoDirection = token.GetProperty<DoubleProperty>(PropertyNames.ImpastoDirection).Value;
        }

        public (int, int, int, string, bool, DirectionTypeOptions, double, EmphasisOptions, ColorBgra32) DrawingProperties =>
            new(Radius, StrokeWidthPercent, Blendiness, StrokeStyle, Antialias, DirectionType, StrokeDirection, (Emphasis, EmphasisColour));

        public (int, int, int, double) ShaderProperties => new(PreserveFineDetails, ImpastoPercent, ImpastoFineStrokesPercent, ImpastoDirection);
    }
}