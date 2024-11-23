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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Bitmap = System.Drawing.Bitmap;
using EmbossEffect = PaintDotNet.Direct2D1.Effects.EmbossEffect;
using GaussianBlurEffect = PaintDotNet.Direct2D1.Effects.GaussianBlurEffect;
using IDeviceContext = PaintDotNet.Direct2D1.IDeviceContext;

[assembly: AssemblyTitle("Impasto plugin for Paint.NET")]
[assembly: AssemblyDescription("Paints over the image with many overlapping brush strokes")]
[assembly: AssemblyConfiguration("brush, brushstrokes, paint, painterly, painted, painting")]
[assembly: AssemblyCompany("Robot Graffiti")]
[assembly: AssemblyProduct("Impasto")]
[assembly: AssemblyCopyright("Impasto copyright ©2023 Robot Graffiti. ComputeSharp copyright ©2022 Sergio Pedri. TerraFX copyright © Tanner Gooding and Contributors.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.*")]
[assembly: SupportedOSPlatform("Windows")]

namespace ImpastoEffect;


[PluginSupportInfo(typeof(PluginSupportInfo))]
public sealed partial class Impasto : PropertyBasedGpuImageEffect
{
    public Impasto() : base(Name, GetIcon(), SubmenuName, new() { IsConfigurable = true }) { }

    private static string Name => typeof(Impasto).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product;

    private static IBitmapSource GetIcon()
    {
        using Bitmap bitmap = new(typeof(Impasto), "BoldBrush.png");
        using Surface surface = Surface.CopyFromBitmap(bitmap);
        return surface.CreateSharedBitmap();
    }

    private static string SubmenuName => SubmenuNames.Artistic;

    // Effect properties
    private EffectProperties Properties = new();

    // Algorithm tuning
    private const int NumberOfPasses = 3;
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
    IDeviceEffect? OverlayImpastoEffect;
    IDeviceEffect? RestoreEdgesEffect;
    EmbossEffect? EmbossEffect;
    OpacityEffect? OpacityEffect;

    protected override void OnInvalidateDeviceResources()
    {
        if (StrokeGeometryRealizations is not null)
            foreach (var stroke in StrokeGeometryRealizations.Values.SelectMany(x => x)) stroke.Dispose();
        StrokeGeometryRealizations = null;
        StrokeGeometryRealization = null;
        SourceBitmap = null;
        DeviceContext = null;
        OverlayImpastoEffect?.Dispose(); OverlayImpastoEffect = null;
        RestoreEdgesEffect?.Dispose(); RestoreEdgesEffect = null;
        EmbossEffect?.Dispose(); EmbossEffect = null;
        OpacityEffect?.Dispose(); OpacityEffect = null;
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
            new Int32Property(PropertyNames.EmphasisColour, unchecked((int)Colors.Blue.Bgra)),

            // Blending
            new Int32Property(PropertyNames.Blendiness, 50, 0, 100),
            new Int32Property(PropertyNames.PreserveFineDetails, 50, 0, 100),
            new StringProperty(PropertyNames.SpaceLabel, " "),
            new BooleanProperty(PropertyNames.Antialias, true),

            // Impasto
            new Int32Property(PropertyNames.ImpastoPercent, 25, 0, 100),
            new Int32Property(PropertyNames.ImpastoFineStrokesPercent, 25, 0, 100),
            new DoubleProperty(PropertyNames.ImpastoDirection, 90, 0, 360),
        };

        List<PropertyCollectionRule> propRules = new()
        {
            new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(PropertyNames.StrokeDirection, PropertyNames.DirectionType, DirectionTypeOptions.TowardsSimilarPixels, false),
            new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(PropertyNames.EmphasisColour, PropertyNames.Emphasis, EmphasisOptions.SpecifiedColour, true)
        };

        return new PropertyCollection(properties, propRules);
    }

    protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
    {
        TabContainerControlInfo ui = new(props[PropertyNames.TabContainer]);

        {
            TabPageControlInfo tab = new() { Text = "Size/shape" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeLength]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeWidthPercent]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeStyle]));
            ui.AddTab(tab);
        }
        {
            TabPageControlInfo tab = new() { Text = "Direction" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.DirectionType]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.StrokeDirection]));
            ui.AddTab(tab);
        }
        {
            TabPageControlInfo tab = new() { Text = "Order" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.Emphasis]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.EmphasisColour]));
            ui.AddTab(tab);
        }
        {
            TabPageControlInfo tab = new() { Text = "Blending" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.Blendiness]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.PreserveFineDetails]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.SpaceLabel]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.Antialias]));
            ui.AddTab(tab);
        }
        {
            TabPageControlInfo tab = new() { Text = "Impasto" };
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.ImpastoPercent]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.ImpastoFineStrokesPercent]));
            tab.AddChildControl(PropertyControlInfo.CreateFor(props[PropertyNames.ImpastoDirection]));
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

        ui.SetPropertyControlValue(PropertyNames.Antialias, ControlInfoPropertyNames.DisplayName, string.Empty);
        ui.SetPropertyControlValue(PropertyNames.Antialias, ControlInfoPropertyNames.Description, "Antialias");

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
        EmphasisControl.SetValueDisplayName(EmphasisOptions.SpecifiedColour, "This colour");

        ui.SetPropertyControlValue(PropertyNames.EmphasisColour, ControlInfoPropertyNames.DisplayName, string.Empty);
        ui.SetPropertyControlType(PropertyNames.EmphasisColour, PropertyControlType.ColorWheel);

        ui.SetPropertyControlType(PropertyNames.SpaceLabel, PropertyControlType.Label);

        ui.SetPropertyControlValue(PropertyNames.ImpastoPercent, ControlInfoPropertyNames.DisplayName, "Edge highlights");
        ui.SetPropertyControlValue(PropertyNames.ImpastoPercent, ControlInfoPropertyNames.ShowHeaderLine, false);
        ui.SetPropertyControlValue(PropertyNames.ImpastoFineStrokesPercent, ControlInfoPropertyNames.DisplayName, "Stroke highlights");
        ui.SetPropertyControlValue(PropertyNames.ImpastoFineStrokesPercent, ControlInfoPropertyNames.ShowHeaderLine, false);

        ui.SetPropertyControlValue(PropertyNames.ImpastoDirection, ControlInfoPropertyNames.DisplayName, string.Empty);
        ui.SetPropertyControlType(PropertyNames.ImpastoDirection, PropertyControlType.AngleChooser);
        ui.SetPropertyControlValue(PropertyNames.ImpastoDirection, ControlInfoPropertyNames.DecimalPlaces, 0);

        return ui;
    }
    protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
    {
        // Change the effect's window width
        //props[ControlInfoPropertyNames.WindowWidthScale].Value = 3.0;
        // Add help button to effect UI
        props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;

        var version = typeof(Impasto).Assembly.GetName().Version!;

        props[ControlInfoPropertyNames.WindowHelpContent].Value = @$"Impasto v{version.Major}.{version.Minor}
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
        SourceBitmap = Environment.GetSourceBitmapBgra32();

        DeviceContext = dc;

        StrokeGeometryRealizations = SVGLoader.LoadGeometryDictionary(dc);
        RandomGeometryRealisations = StrokeGeometryRealizations.ValuesWhereKeyDoesNotContain("Rectangle", "Novelty").ToArray().SwapAxes();
        StrokeGeometryRealizationRectangle = StrokeGeometryRealizations["Rectangle"][0];

        PreRender();
    }

    void PreRender()
    {
        ThrowIfNull(StrokeGeometryRealizations);
        ThrowIfNullOrDisposed(DeviceContext);

        StrokeGeometryRealization = Properties.StrokeStyle == "Random" ? null : StrokeGeometryRealizations[Properties.StrokeStyle];

        DeviceContext.AntialiasMode = Properties.Antialias ? AntialiasMode.PerPrimitive : AntialiasMode.Aliased;

        (SortFunction1, SortFunction2) = GetSortFunctions();
        StrokeWidthMain = Math.Max(1, Properties.Radius * Properties.StrokeWidthPercent / 50);

        (Sort1Range, Sort2Range) = PrecomputeSortRanges(); // This has to go after StrokeWidthMain & sort functions are set. Least potential for bugs if it's after everything is set.
    }

    private (MinMaxRange<int> sortRange1, MinMaxRange<int> sortRange2) PrecomputeSortRanges()
    {
        ThrowIfNullOrDisposed(SourceBitmap);
        ThrowIfNull(SortFunction1);
        ThrowIfNull(SortFunction2);

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

        ThrowIfNullOrDisposed(SourceBitmap);
        ThrowIfNullOrDisposed(StrokeGeometryRealizationRectangle);
        ThrowIfNull(SortFunction1);
        ThrowIfNull(SortFunction2);

        var sourceBounds = SourceBitmap.Bounds();
        using IBitmapLock<ColorBgra32> sourceBitmapLock = SourceBitmap.Lock(sourceBounds);
        var sourceRegion = sourceBitmapLock.AsRegionPtr();

        ICommandList impastoCommandList = dc.CreateCommandList();

        // Clear canvas
        impastoCommandList.Clear(dc);

        for (int strokeWidthMultiplier = NumberOfPasses; strokeWidthMultiplier > 0; strokeWidthMultiplier -= 1)
        {
            if (IsCancelRequested) return Environment.SourceImage;

            double factor = Math.Pow(strokeWidthMultiplier, 1.5);
            int strokeWidth = (int)(StrokeWidthMain * factor);
            int radius = (int)(Properties.Radius * factor);

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
                ThrowIfNull(RandomGeometryRealisations);
                var geometries = RandomGeometryRealisations[geometryLOD];
                geometryGetter = stroke => geometries.ChooseRandomish(stroke.StartPosition.X, stroke.EndPosition.Y);
            }
            else
            {
                ThrowIfNull(StrokeGeometryRealization);
                var strokeToUse = StrokeGeometryRealization[geometryLOD];
                geometryGetter = stroke => strokeToUse;
            }

            {
                // Draw impasto brushstroke shadows
                using var targetScope = dc.UseTarget(impastoCommandList);
                using var drawScope = dc.UseBeginDraw();
                using var blendScope = dc.UsePrimitiveBlend(PrimitiveBlend.Copy);
                using ISolidColorBrush brushBlack = dc.CreateSolidColorBrush(ColorBgra32.FromBgra(30, 10, 0, 255));
                using ISolidColorBrush brushWhite = dc.CreateSolidColorBrush(ColorBgra32.FromBgra(235, 245, 255, 255));
                using ISolidColorBrush brushTransparent = dc.CreateSolidColorBrush(Colors.Transparent);
                Vector2Int32 offsetBlack = new(0, -1);
                Vector2Int32 offsetWhite = new(0, 1);

                //foreach (Stroke stroke in buckets.Cast<List<Stroke>>().Where(bucket => bucket is not null).Where((x, i) => i > NumberOfBuckets * NumberOfBuckets * 2 / 3).SelectMany(x => x))
                foreach (List<Stroke> bucket in buckets)
                {
                    if (bucket == null) continue;
                    foreach (Stroke stroke in bucket)
                    {
                        var geometry = geometryGetter(stroke);
                        DrawBrushstrokeWithOffset(dc, geometry, stroke, brushBlack, widthRatio, offsetBlack);
                        DrawBrushstrokeWithOffset(dc, geometry, stroke, brushWhite, widthRatio, offsetWhite);
                        DrawBrushstroke(dc, geometry, stroke, brushTransparent, widthRatio);
                    }
                }
            }
        }

        impastoCommandList.Close();

        NeedsUpdate = false;
        return CreateShaderGraph(dc, impastoCommandList);
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

    bool NeedsUpdate = false;

    protected override void OnUpdateOutput(IDeviceContext dc)
    {
        if (!NeedsUpdate) return;
        if (OverlayImpastoEffect?.IsDisposed == true) return;
        ThrowIfNullOrDisposed(OverlayImpastoEffect);
        ThrowIfNullOrDisposed(RestoreEdgesEffect);
        ThrowIfNullOrDisposed(EmbossEffect);
        ThrowIfNullOrDisposed(OpacityEffect);

        RestoreEdgesEffect.SetValues(new RestoreFineDetailsAndResetAlphaShader((float)Math.Pow(Properties.PreserveFineDetails * 0.01, 3)));
        OverlayImpastoEffect.SetValues(new OverlayImpastoShader(Properties.ImpastoPercent * 0.01f));
        EmbossEffect.Properties.Direction.SetValue((float)Properties.ImpastoDirection);
        OpacityEffect.Properties.Opacity.SetValue(Properties.ImpastoFineStrokesPercent * 0.01f);

        NeedsUpdate = false;
    }

    static void ThrowIfNull([NotNull] object? o, [CallerArgumentExpression("o")] string oName = "", [CallerMemberName] string caller = "")
    {
        if (o is null) throw new NullReferenceException($"{oName} is null in {caller}");
    }

    static void ThrowIfNullOrDisposed([NotNull] IIsDisposed? o, [CallerArgumentExpression("o")] string oName = "", [CallerMemberName] string caller = "")
    {
        if (o is null) throw new NullReferenceException($"{oName} is null in {caller}");
        if (o.IsDisposed) throw new ObjectDisposedException($"{oName} is already disposed in {caller}");
    }
    

    private IDeviceEffect CreateShaderGraph(IDeviceContext dc, ICommandList impastoCommandList)
    {
        // TODO: deluminate? ("flatten colours")
        // Posterize whole image not just background
        // Restore colours after posterizing

        // or... rotate to HSL, posterize, flatten L, rotate back

        // PaintDotNet.Direct2D1.Effects.RgbToHueEffect


        // TODO: adjust colour space
        //PaintDotNet.Direct2D1.Effects.SrgbToLinearEffect
        //PaintDotNet.Direct2D1.Effects.LinearToSrgbEffect


        SrgbToLinearEffect strokes = new(dc);
        strokes.Properties.Input.Set(impastoCommandList);

        BorderEffect borderEffect = new(dc);
        borderEffect.Properties.Input.Set(Environment.SourceImage);

        SrgbToLinearEffect sourceImage = new(dc);
        sourceImage.Properties.Input.Set(borderEffect);

        GaussianBlurEffect gaussianBlurEffect = new(dc);
        gaussianBlurEffect.Properties.Input.Set(sourceImage);
        gaussianBlurEffect.Properties.Optimization.SetValue(GaussianBlurOptimization.Speed);
        gaussianBlurEffect.Properties.StandardDeviation.SetValue(20);

        SrgbToLinearEffect sourceImage2 = new(dc);
        sourceImage2.Properties.Input.Set(borderEffect);

        IDeviceEffect restoreEdgesEffect = dc.CreateEffect<RestoreFineDetailsAndResetAlphaShader>();
        restoreEdgesEffect.SetInput(0, sourceImage2);
        restoreEdgesEffect.SetInput(1, gaussianBlurEffect);
        restoreEdgesEffect.SetValues(new RestoreFineDetailsAndResetAlphaShader(0.1f));

        /*
        PosterizeEffect posterizeEffect = new(dc);
        posterizeEffect.Properties.Input.Set(restoreEdgesEffect2);
        posterizeEffect.Properties.RedValueCount.SetValue(8);
        posterizeEffect.Properties.GreenValueCount.SetValue(8);
        posterizeEffect.Properties.BlueValueCount.SetValue(8);
        */

        // Blur and cache impasto strokes
        GaussianBlurEffect gaussianBlurEffect2 = new(dc);
        gaussianBlurEffect2.Properties.Input.Set(strokes);
        gaussianBlurEffect2.Properties.StandardDeviation.SetValue(1);
        gaussianBlurEffect2.Properties.Cached.SetValue(true);

        OpacityEffect opacityEffect = new(dc);
        opacityEffect.Properties.Input.Set(gaussianBlurEffect);
        opacityEffect.Properties.Opacity.SetValue(Properties.ImpastoFineStrokesPercent * 0.01f);

        // Draw impasto strokes onto copy of image for impasto effect
        CompositeEffect compositeEffect = new(dc);
        compositeEffect.Properties.Destination.Set(restoreEdgesEffect);
        compositeEffect.Properties.Sources.Add(opacityEffect);

        // Get impasto edges
        EmbossEffect embossEffect = new(dc);
        embossEffect.Properties.Input.Set(compositeEffect);
        embossEffect.Properties.Height.SetValue(3);
        embossEffect.Properties.Direction.SetValue((float)Properties.ImpastoDirection);

        // Copy impasto edges onto the image that had edges restored
        IDeviceEffect overlayImpastoEffect = dc.CreateEffect<OverlayImpastoShader>();
        overlayImpastoEffect.SetInput(0, restoreEdgesEffect);
        overlayImpastoEffect.SetInput(1, embossEffect);
        overlayImpastoEffect.SetValues(new OverlayImpastoShader(Properties.ImpastoPercent * 0.01f));

        LinearToSrgbEffect linearToSrgbEffect = new(dc);
        linearToSrgbEffect.Properties.Input.Set(overlayImpastoEffect);

        RestoreEdgesEffect?.Dispose();
        RestoreEdgesEffect = restoreEdgesEffect;

        OverlayImpastoEffect?.Dispose();
        OverlayImpastoEffect = overlayImpastoEffect;

        EmbossEffect?.Dispose();
        EmbossEffect = embossEffect;

        OpacityEffect?.Dispose();
        OpacityEffect = opacityEffect;

        return linearToSrgbEffect;
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

    private static void DrawBrushstrokeWithOffset(IDeviceContext dc, IGeometryRealization shape, Stroke stroke, ISolidColorBrush brush, float widthRatio, Vector2Int32 offset)
    {
        var startPosition = stroke.StartPosition + offset;
        var vector = stroke.EndPosition - stroke.StartPosition; // in page space
        Matrix3x2Float transform = new
        (   //x axis in page space  y axis in page space 
            vector.Y * widthRatio,  -vector.X * widthRatio, // x axis in geometry space (stroke width) gets multiplied by this
            vector.X,               vector.Y,               // y axis in geometry space (stroke direction) gets multiplied by this
            startPosition.X,        startPosition.Y         // translate origin
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

    (SortFunction sortFunction1, SortFunction sortFunction2) GetSortFunctions()
    {
        SortFunction bucketFunction;
        SortFunction bucketFunction2;

        switch (Properties.Emphasis)
        {
            case EmphasisOptions.SpecifiedColour: // Specified Colour
                bucketFunction = s =>
                {
                    ColorBgra32 emphasisColour = Properties.EmphasisColour;
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