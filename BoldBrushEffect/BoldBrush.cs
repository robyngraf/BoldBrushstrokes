using BrushStrokes;
using PaintDotNet;
using PaintDotNet.Direct2D1;
using PaintDotNet.Direct2D1.Effects;
using PaintDotNet.Effects;
using PaintDotNet.Effects.Gpu;
using PaintDotNet.Imaging;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Bitmap = System.Drawing.Bitmap;
using IDeviceContext = PaintDotNet.Direct2D1.IDeviceContext;

[assembly: AssemblyTitle("Bold Brushstrokes plugin for Paint.NET")]
[assembly: AssemblyDescription("Paints over the image with many overlapping brush strokes")]
[assembly: AssemblyConfiguration("brush, brushstrokes, paint, painterly, painted, painting")]
[assembly: AssemblyCompany("Robot Graffiti")]
[assembly: AssemblyProduct("Bold Brushstrokes")]
[assembly: AssemblyCopyright("Bold Brushstrokes copyright ©2023 Robot Graffiti. ComputeSharp copyright ©2022 Sergio Pedri. TerraFX copyright © Tanner Gooding and Contributors.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.*")]
[assembly: SupportedOSPlatform("Windows")]

namespace BoldBrushEffect;

[PluginSupportInfo(typeof(PluginSupportInfo))]
public sealed partial class BoldBrush : PropertyBasedGpuImageEffect
{
    public BoldBrush() : base(Name, GetIcon(), SubmenuName, new() { IsConfigurable = true }) { }

    private static string Name => typeof(BoldBrush).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product;

    private static IBitmapSource GetIcon()
    {
        using Bitmap bitmap = new(typeof(BoldBrush), "BoldBrush.png");
        using Surface surface = Surface.CopyFromBitmap(bitmap);
        return surface.CreateSharedBitmap();
    }

    private static string SubmenuName => SubmenuNames.Artistic;

    // Effect properties
    private EffectProperties Properties = new();

    // Algorithm tuning
    private const int NumberOfPasses = 2;
    private const int NumberOfBuckets = 6;
    private const int PrecomputeSampleCount = 15;

    // Runtime data
    private SortFunction? SortFunction1;
    private SortFunction? SortFunction2;
    private int StrokeWidthMain;
    private MinMaxRange<int> Sort1Range = new();
    private MinMaxRange<int> Sort2Range = new();

    // Disposables
    private IDeviceContext? DeviceContext;
    private IEffectInputBitmap<ColorBgra32>? SourceBitmap;
    private IGeometryRealization? StrokeGeometryRealizationRectangle;
    private IGeometryRealization[]? StrokeGeometryRealization;
    private IGeometryRealization[][]? RandomGeometryRealisations;
    private Dictionary<string, IGeometryRealization[]>? StrokeGeometryRealizations;
    IDeviceEffect? RestoreEdgesEffect;
    PassthroughEffect? BackgroundEffect;

    protected override void OnInvalidateDeviceResources()
    {
        if (StrokeGeometryRealizations is not null)
            foreach (var stroke in StrokeGeometryRealizations.Values.SelectMany(x => x)) stroke.Dispose();
        StrokeGeometryRealizations = null;
        StrokeGeometryRealization = null;
        SourceBitmap = null;
        DeviceContext = null;
        lock (effectsLock)
        {
            RestoreEdgesEffect?.Dispose(); RestoreEdgesEffect = null;
            BackgroundEffect?.Dispose(); BackgroundEffect = null;
        }
    }

    protected override PropertyCollection OnCreatePropertyCollection()
    {
        List<Property> properties = new()
        {
            // Size/shape
            new Int32Property(PropertyNames.StrokeLength, 20, 4, 100),
            new Int32Property(PropertyNames.StrokeWidthPercent, 40, 1, 100),
            new StaticListChoiceProperty(PropertyNames.StrokeStyle, SVGLoader.GetHumanReadableSvgNames().Prepend("Random").ToArray()),

            // Direction
            StaticListChoiceProperty.CreateForEnum(PropertyNames.DirectionType, DirectionTypeOptions.TowardsSimilarPixels),
            new DoubleProperty(PropertyNames.StrokeDirection, 0, 0, 360),

            // Order
            StaticListChoiceProperty.CreateForEnum(PropertyNames.Emphasis,EmphasisOptions.Smooth),

            // Blending
            new Int32Property(PropertyNames.Blendiness, 50, 0, 100),
            new Int32Property(PropertyNames.PreserveFineDetails, 50, 0, 100),
            StaticListChoiceProperty.CreateForEnum(PropertyNames.Background,BackgroundOptions.Blur)
        };

        List<PropertyCollectionRule> propRules = new()
        {
            new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(PropertyNames.StrokeDirection, PropertyNames.DirectionType, DirectionTypeOptions.TowardsSimilarPixels, false),
        };

        return new PropertyCollection(properties, propRules);
    }

    protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
    {
        TabContainerControlInfo ui = new(props[PropertyNames.TabContainer]);

        {
            TabPageControlInfo tab = new() { Text = "Size & shape" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeLength]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeWidthPercent]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeStyle]));
            ui.AddTab(tab);
        }
        {
            TabPageControlInfo tab = new() { Text = "Direction & colour" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.DirectionType]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeDirection]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.Emphasis]));
            ui.AddTab(tab);
        }
        {
            TabPageControlInfo tab = new() { Text = "Blending & background" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.Blendiness]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.PreserveFineDetails]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.Background]));
            ui.AddTab(tab);
        }

        ui.SetPropertyControlValue(PropertyNames.DirectionType, ControlInfoPropertyNames.Multiline, false);
        ui.SetPropertyControlValue(PropertyNames.StrokeStyle, ControlInfoPropertyNames.Multiline, false);
        ui.SetPropertyControlValue(PropertyNames.Emphasis, ControlInfoPropertyNames.Multiline, false);

        ui.SetPropertyControlValue(PropertyNames.DirectionType, ControlInfoPropertyNames.DisplayName, string.Empty);
        ui.SetPropertyControlValue(PropertyNames.StrokeStyle, ControlInfoPropertyNames.DisplayName, string.Empty);
        ui.SetPropertyControlValue(PropertyNames.Emphasis, ControlInfoPropertyNames.DisplayName, string.Empty);

        ui.SetPropertyControlValue(PropertyNames.StrokeLength, ControlInfoPropertyNames.DisplayName, "Stroke length (px)");
        ui.SetPropertyControlValue(PropertyNames.StrokeLength, ControlInfoPropertyNames.ShowHeaderLine, false);

        ui.SetPropertyControlValue(PropertyNames.StrokeWidthPercent, ControlInfoPropertyNames.DisplayName, "Thin ⬅     ➔ Fat");
        ui.SetPropertyControlValue(PropertyNames.StrokeWidthPercent, ControlInfoPropertyNames.ShowHeaderLine, false);

        ui.SetPropertyControlValue(PropertyNames.Blendiness, ControlInfoPropertyNames.DisplayName, "Stroke blending (% transparency of strokes)");
        ui.SetPropertyControlValue(PropertyNames.Blendiness, ControlInfoPropertyNames.ShowHeaderLine, false);

        ui.SetPropertyControlValue(PropertyNames.PreserveFineDetails, ControlInfoPropertyNames.DisplayName, "Preserve fine details");
        ui.SetPropertyControlValue(PropertyNames.PreserveFineDetails, ControlInfoPropertyNames.ShowHeaderLine, false);

        ui.SetPropertyControlValue(PropertyNames.StrokeStyle, ControlInfoPropertyNames.Description, "Stroke shape");

        ui.SetPropertyControlValue(PropertyNames.DirectionType, ControlInfoPropertyNames.Description, "Stroke direction");
        PropertyControlInfo DirectionTypeControl = ui.FindControlForPropertyName(PropertyNames.DirectionType);
        DirectionTypeControl.SetValueDisplayName(DirectionTypeOptions.TowardsSimilarPixels, "Towards similar pixels");
        DirectionTypeControl.SetValueDisplayName(DirectionTypeOptions.DependsOnColour, "Depends on colour");
        DirectionTypeControl.SetValueDisplayName(DirectionTypeOptions.OneDirection, "This direction");

        ui.SetPropertyControlValue(PropertyNames.StrokeDirection, ControlInfoPropertyNames.DisplayName, string.Empty);
        ui.SetPropertyControlType(PropertyNames.StrokeDirection, PropertyControlType.AngleChooser);
        ui.SetPropertyControlValue(PropertyNames.StrokeDirection, ControlInfoPropertyNames.DecimalPlaces, 0);

        ui.SetPropertyControlValue(PropertyNames.Emphasis, ControlInfoPropertyNames.Description, "Paint these strokes last");
        PropertyControlInfo EmphasisControl = ui.FindControlForPropertyName(PropertyNames.Emphasis);
        EmphasisControl.SetValueDisplayName(EmphasisOptions.PrimaryColour, "Primary colour");

        ui.SetPropertyControlValue(PropertyNames.Background, ControlInfoPropertyNames.Description, "Background");
        ui.SetPropertyControlValue(PropertyNames.Background, ControlInfoPropertyNames.Multiline, false);
        PropertyControlInfo BackgroundControl = ui.FindControlForPropertyName(PropertyNames.Background);
        BackgroundControl.SetValueDisplayName(BackgroundOptions.SecondaryColour, "Secondary colour");

        return ui;
    }
    protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
    {
        // Change the effect's window width
        //props[ControlInfoPropertyNames.WindowWidthScale].Value = 3.0;
        // Add help button to effect UI
        props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;

        var version = typeof(BoldBrush).Assembly.GetName().Version!;

        props[ControlInfoPropertyNames.WindowHelpContent].Value = @$"Bold Brushstrokes v{version.Major}.{version.Minor}
Copyright ©2023 Robot Graffiti
All rights reserved.

Contains portions of:
- ComputeSharp copyright ©2022 Sergio Pedri.
- TerraFX copyright © Tanner Gooding and Contributors.";
    }

    /*
     *  
     * Before first render:
     * OnInitializeRenderInfo
     * OnSetToken
     * OnSetDeviceContext
     * 
     * Before render after value changes:
     * OnSetToken
     */

    protected override void OnSetToken(PropertyBasedEffectConfigToken? token)
    {
        Properties = new(token);

        if (DeviceContext is not null) PreRender();
    }

    protected override void OnSetDeviceContext(IDeviceContext dc)
    {
        DeviceContext = dc;
        dc.AntialiasMode = AntialiasMode.PerPrimitive;

        SourceBitmap = Environment.GetSourceBitmapBgra32();

        StrokeGeometryRealizations = SVGLoader.LoadGeometryDictionary(dc);
        RandomGeometryRealisations = StrokeGeometryRealizations.ValuesWhereKeyDoesNotContain("Rectangle", "Novelty").ToArray().SwapAxes();
        StrokeGeometryRealizationRectangle = StrokeGeometryRealizations["Rectangle"][0];

        PreRender();
    }

    void PreRender()
    {
        StrokeGeometryRealizations.ShouldNotBeNull();
        DeviceContext.ShouldNotBeNullOrDisposed();
        StrokeGeometryRealization = Properties.StrokeStyle == "Random" ? null : StrokeGeometryRealizations[Properties.StrokeStyle];

        (SortFunction1, SortFunction2) = GetSortFunctions();
        StrokeWidthMain = Math.Max(1, Properties.Radius * Properties.StrokeWidthPercent / 50);

        (Sort1Range, Sort2Range) = PrecomputeSortRanges(); // This has to go after StrokeWidthMain & sort functions are set. Least potential for bugs if it's after everything is set.
    }

    private (MinMaxRange<int> sortRange1, MinMaxRange<int> sortRange2) PrecomputeSortRanges()
    {
        SourceBitmap.ShouldNotBeNullOrDisposed();
        SortFunction1.ShouldNotBeNull();
        SortFunction2.ShouldNotBeNull();

        var bounds = SourceBitmap.Bounds();
        using var sourceBitmap = SourceBitmap.Lock(bounds);
        var sourceRegion = sourceBitmap.AsRegionPtr();

        int stride = Math.Min(SourceBitmap.Size.Width, SourceBitmap.Size.Height) / PrecomputeSampleCount;
        var strokes = Artist.GenerateStrokes(bounds, bounds, stride, sourceRegion, Properties.Radius, SortFunction1, SortFunction2, Properties.DirectionType, Properties.StrokeDirection);
        return GetSortRanges(strokes);
    }

    protected override InspectTokenAction OnInspectTokenChanges(PropertyBasedEffectConfigToken oldToken, PropertyBasedEffectConfigToken newToken)
    {
        EffectProperties oldProperties = new(oldToken);
        EffectProperties newProperties = new(newToken);

        NeedsUpdate = false;
        /*
        if (oldProperties == newProperties)
            return InspectTokenAction.None;
        */

        if (oldProperties.DrawingProperties == newProperties.DrawingProperties)
        {
            NeedsUpdate = true;
            return InspectTokenAction.UpdateOutput;
        }

        return InspectTokenAction.RecreateOutput;
    }

    protected override IDeviceImage OnCreateOutput(IDeviceContext dc)
    {
        // TODO: novelty brush shapes? Skull, heart, curly?

        SourceBitmap.ShouldNotBeNullOrDisposed();
        StrokeGeometryRealizationRectangle.ShouldNotBeNullOrDisposed();
        SortFunction1.ShouldNotBeNull();
        SortFunction2.ShouldNotBeNull();

        var sourceBounds = SourceBitmap.Bounds();
        using IBitmapLock<ColorBgra32> sourceBitmapLock = SourceBitmap.Lock(sourceBounds);
        var sourceRegion = sourceBitmapLock.AsRegionPtr();

        ICommandList strokeCommandList = dc.CreateCommandList();

        for (int strokeScale = NumberOfPasses; strokeScale > 0; strokeScale -= 1)
        {
            if (IsCancelRequested) return Environment.SourceImage;

            int strokeWidthMultiplier = FastMaths.TwoToThePowerOf(strokeScale) - 1;
            int strokeWidth = StrokeWidthMain * strokeWidthMultiplier;
            int radius = Properties.Radius * strokeWidthMultiplier;

            int stride;
            if (Properties.DirectionType == 0)
            {
                stride = Math.Max(2, strokeWidth / 2);
            }
            else
            {
                stride = Math.Max(1, strokeWidth / 2);
            }

            RectInt32 selectionBounds = Environment.Selection.RenderBounds;
            List<Stroke> strokes = Artist.GenerateStrokes(sourceBounds, selectionBounds, stride, sourceRegion, radius, SortFunction1, SortFunction2, Properties.DirectionType, Properties.StrokeDirection);

            if (strokes.Count == 0) continue;
            List<Stroke>[,] buckets = SortStrokesIntoBuckets(strokes);

            int geometryLOD = strokeWidth < 5 ? 0 : 1;
            float widthRatio = strokeWidth / (float)radius;
            Func<Stroke, IGeometryRealization> geometryGetter;
            if (strokeWidth < 2)
            {
                var strokeToUse = StrokeGeometryRealizationRectangle;
                geometryGetter = stroke => strokeToUse;
            }
            else if (Properties.StrokeStyle == "Random")
            {
                RandomGeometryRealisations.ShouldNotBeNull();
                var geometries = RandomGeometryRealisations[geometryLOD];
                geometryGetter = stroke => geometries.ChooseRandomish(stroke.StartPosition.X, stroke.EndPosition.Y);
            }
            else
            {
                StrokeGeometryRealization.ShouldNotBeNull();
                var strokeToUse = StrokeGeometryRealization[geometryLOD];
                geometryGetter = stroke => strokeToUse;
            }

            { // Draw brushstrokes
                using var targetScope = dc.UseTarget(strokeCommandList);
                using var drawScope = dc.UseBeginDraw();
                using ISolidColorBrush brush = dc.CreateSolidColorBrush(default);
                //brush.Opacity = (100 - (Blendiness * (NumberOfPasses - strokeWidthMultiplier) / (NumberOfPasses - 1))) * 0.01f;
                brush.Opacity = (100 - Properties.Blendiness) * 0.01f;
                foreach (List<Stroke> bucket in buckets)
                {
                    if (bucket == null) continue;
                    foreach (Stroke stroke in bucket)
                    {
                        var geometry = geometryGetter(stroke);
                        brush.Color = stroke.DrawColor.ToLinear();
                        DrawBrushstroke(dc, geometry, stroke, brush, widthRatio);
                    }
                }
            }
        }

        strokeCommandList.Close();

        NeedsUpdate = false;
        return CreateShaderGraph(dc, strokeCommandList);
    }

    private List<Stroke>[,] SortStrokesIntoBuckets(List<Stroke> strokes)
    {
        List<Stroke>[,] buckets;

        int sort1Range = Sort1Range.Range;
        int sort2Range = Sort2Range.Range;
        if (sort1Range == 0)
        {
            if (sort2Range == 0)
            {   // Every stroke is the same priority (like if the effect is being run on a white square)
                buckets = new List<Stroke>[1, 1] { { strokes } };
            }
            else
            {   // Sort1Range is useless
                buckets = new List<Stroke>[NumberOfBuckets, 1];
                foreach (Stroke stroke in strokes)
                {
                    int bucketIndex = Math.Clamp((stroke.Sort2 - Sort2Range.Min) * (NumberOfBuckets - 1) / sort2Range, 0, NumberOfBuckets - 1);
                    var bucket = buckets[bucketIndex, 0];
                    if (bucket == null) buckets[bucketIndex, 0] = bucket = new(1024);
                    bucket.Add(stroke);
                }
            }
        }
        else
        {
            if (sort2Range == 0)
            {   // Sort2Range is useless
                buckets = new List<Stroke>[NumberOfBuckets, 1];
                foreach (Stroke stroke in strokes)
                {
                    int bucketIndex = Math.Clamp((stroke.Sort1 - Sort1Range.Min) * (NumberOfBuckets - 1) / sort1Range, 0, NumberOfBuckets - 1);
                    var bucket = buckets[bucketIndex, 0];
                    if (bucket == null) buckets[bucketIndex, 0] = bucket = new(128);
                    bucket.Add(stroke);
                }
            }
            else
            {   // Both sortranges are in use
                buckets = new List<Stroke>[NumberOfBuckets, NumberOfBuckets];
                foreach (Stroke stroke in strokes)
                {
                    int bucketIndex1 = Math.Clamp((stroke.Sort1 - Sort1Range.Min) * (NumberOfBuckets - 1) / sort1Range, 0, NumberOfBuckets - 1);
                    int bucketIndex2 = Math.Clamp((stroke.Sort2 - Sort2Range.Min) * (NumberOfBuckets - 1) / sort2Range, 0, NumberOfBuckets - 1);
                    var bucket = buckets[bucketIndex1, bucketIndex2];
                    if (bucket == null) buckets[bucketIndex1, bucketIndex2] = bucket = new(128);
                    bucket.Add(stroke);
                }
            }
        }

        return buckets;
    }

    private IDeviceEffect CreateShaderGraph(IDeviceContext dc, ICommandList strokeCommandList)
    {
        PassthroughEffect strokeCache = new(dc);
        strokeCache.Properties.Input.Set(strokeCommandList);
        strokeCache.Properties.Cached.SetValue(true);

        PassthroughEffect background = new(dc);
        background.Properties.Input.Set(CreateBackgroundShader(dc));

        CompositeEffect brushstrokesDrawnOverBackground = new(dc);
        brushstrokesDrawnOverBackground.Properties.Destination.Set(background);
        brushstrokesDrawnOverBackground.Properties.Sources.Add(strokeCache);

        SrgbToLinearEffect originalBackground = new(dc);
        originalBackground.Properties.Input.Set(Environment.SourceImage);

        UnPremultiplyEffect originalBackgroundWithStraightAlpha = new(dc);
        originalBackgroundWithStraightAlpha.Properties.Input.Set(originalBackground);

        UnPremultiplyEffect brushstrokesDrawnOverBackgroundWithStraightAlpha = new(dc);
        brushstrokesDrawnOverBackgroundWithStraightAlpha.Properties.Input.Set(brushstrokesDrawnOverBackground);

        // Compare brushstrokes with original image to restore edges
        IDeviceEffect restoredEdges = dc.CreateEffect<RestoreFineDetailsAndResetAlphaShader>();
        restoredEdges.SetInput(0, originalBackgroundWithStraightAlpha);
        restoredEdges.SetInput(1, brushstrokesDrawnOverBackgroundWithStraightAlpha);
        restoredEdges.SetValues(new RestoreFineDetailsAndResetAlphaShader((float)Math.Pow(Properties.PreserveFineDetails * 0.01, 3)));

        LinearToSrgbEffect linearToSrgbEffect = new(dc);
        linearToSrgbEffect.Properties.Input.Set(restoredEdges);

        lock (effectsLock)
        {
            RestoreEdgesEffect?.Dispose();
            RestoreEdgesEffect = restoredEdges;
            BackgroundEffect?.Dispose();
            BackgroundEffect = background;
        }

        return linearToSrgbEffect;
    }

    private IDeviceImage CreateBackgroundShader(IDeviceContext dc)
    {
        IDeviceImage background;
        switch (Properties.Background)
        {
            case BackgroundOptions.Original:
                {
                    SrgbToLinearEffect sourceImage = new(dc);
                    sourceImage.Properties.Input.Set(Environment.SourceImage);
                    background = sourceImage;
                    break;
                }
            case BackgroundOptions.Blur:
                {
                    BorderEffect clampEdges = new(dc);
                    clampEdges.Properties.Input.Set(Environment.SourceImage);
                    SrgbToLinearEffect linear = new(dc);
                    linear.Properties.Input.Set(clampEdges);
                    GaussianBlurEffect blurEffect = new(dc);
                    blurEffect.Properties.Input.Set(linear);
                    blurEffect.Properties.Optimization.SetValue(GaussianBlurOptimization.Speed);
                    blurEffect.Properties.StandardDeviation.SetValue(20f);

                    UnPremultiplyEffect un1 = new(dc);
                    un1.Properties.Input.Set(blurEffect);
                    SrgbToLinearEffect linear2 = new(dc);
                    linear2.Properties.Input.Set(Environment.SourceImage);
                    UnPremultiplyEffect un2 = new(dc);
                    un2.Properties.Input.Set(linear2);

                    IDeviceEffect resetAlpha = dc.CreateEffect<ResetAlphaShader>();
                    resetAlpha.SetInput(0, un2);
                    resetAlpha.SetInput(1, un1);

                    background = resetAlpha;
                    break;
                }
            case BackgroundOptions.SecondaryColour:
                {
                    FloodEffect2 backgroundColour = new(dc);
                    backgroundColour.Properties.Color.SetValue(GetEffectEnvironmentParameters().SecondaryColor);
                    background = backgroundColour;
                    break;
                }
            case BackgroundOptions.Transparent:
                {
                    FloodEffect2 backgroundColour = new(dc);
                    backgroundColour.Properties.Color.SetValue(Colors.Transparent);
                    background = backgroundColour;
                    break;
                }
            default: throw new NotImplementedException();
        }

        return background;
    }

    bool NeedsUpdate = false;
    readonly object effectsLock = new();

    protected override void OnUpdateOutput(IDeviceContext dc)
    {
        if (!NeedsUpdate) return;
        lock (effectsLock)
        {
            if (RestoreEdgesEffect?.IsDisposed == true) return;
            RestoreEdgesEffect.ShouldNotBeNullOrDisposed();
            BackgroundEffect.ShouldNotBeNullOrDisposed();

            BackgroundEffect.Properties.Input.Get()?.Dispose();
            BackgroundEffect.Properties.Input.Set(CreateBackgroundShader(dc));
            RestoreEdgesEffect.SetValues(new RestoreFineDetailsAndResetAlphaShader((float)Math.Pow(Properties.PreserveFineDetails * 0.01, 3)));
            NeedsUpdate = false;
        }
    }

    private static void DrawBrushstroke(IDeviceContext dc, IGeometryRealization shape, Stroke stroke, ISolidColorBrush brush, float widthRatio)
    {
        var vector = stroke.EndPosition - stroke.StartPosition; // in page space
        Matrix3x2Float transform = new
        (   //x axis in page space  y axis in page space 
            vector.Y * widthRatio,  -vector.X * widthRatio, // x axis in geometry space (stroke width) gets multiplied by this
            vector.X,               vector.Y,               // y axis in geometry space (stroke direction) gets multiplied by this
            stroke.StartPosition.X, stroke.StartPosition.Y  // translate origin
        );
        dc.Transform = transform;
        dc.DrawGeometryRealization(shape, brush);
    }

    private static (MinMaxRange<int> sortRange1, MinMaxRange<int> sortRange2) GetSortRanges(List<Stroke> strokes)
    {
        (MinMaxRange<int> sortRange1, MinMaxRange<int> sortRange2) = (new(), new());
        foreach (Stroke stroke in strokes)
        {
            sortRange1.Add(stroke.Sort1);
            sortRange2.Add(stroke.Sort2);
        }
        return (sortRange1, sortRange2);
    }

    private EffectEnvironmentParameters GetEffectEnvironmentParameters() => EffectEnvironmentParameters.CreateFrom(Environment);

    (SortFunction sortFunction1, SortFunction sortFunction2) GetSortFunctions()
    {
        SortFunction bucketFunction;
        SortFunction bucketFunction2;

        switch (Properties.Emphasis)
        {
            case EmphasisOptions.PrimaryColour: // Specified Colour
                bucketFunction = s =>
                {
                    ColorBgra32 emphasisColour = GetEffectEnvironmentParameters().PrimaryColor;
                    return -emphasisColour.DistanceTo(s.DrawColor);
                };
                bucketFunction2 = s => s.DrawColor.Intensity;
                break;
            case EmphasisOptions.Colourful: // Colourful
                bucketFunction = s => s.DrawColor.GetSaturation();
                bucketFunction2 = s => FastMaths.Abs(s.DrawColor.Intensity - 127);
                break;
            case EmphasisOptions.Grey: // Grey
                bucketFunction = s => -s.DrawColor.GetSaturation();
                bucketFunction2 = s => -FastMaths.Abs(s.DrawColor.Intensity - 127);
                break;
            case EmphasisOptions.Light: // Light
                bucketFunction = s => s.DrawColor.Intensity;
                bucketFunction2 = s => s.DrawColor.GetSaturation();
                break;
            case EmphasisOptions.Dark: // Dark
                bucketFunction = s => -s.DrawColor.Intensity;
                bucketFunction2 = s => s.DrawColor.GetSaturation();
                break;
            case EmphasisOptions.Smooth: // Low Contrast
                bucketFunction = s => -s.DrawColor.RoundTripDistanceTo(s.MidColor, s.EndColor);
                bucketFunction2 = s => FastMaths.Abs(s.DrawColor.Intensity - 127);
                break;
            case EmphasisOptions.Rough: // High Contrast
                bucketFunction = s => s.DrawColor.RoundTripDistanceTo(s.MidColor, s.EndColor);
                bucketFunction2 = s => FastMaths.Abs(s.DrawColor.Intensity - 127);
                break;
            default: throw new NotImplementedException();
        }

        return (bucketFunction, bucketFunction2);
    }
}