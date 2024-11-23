using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.Windows.Forms;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using PaintDotNet.Effects;
using ColorWheelControl = PaintDotNet.ColorBgra;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Rendering.Vector2Double;
using FolderControl = System.String;
using FilenameControl = System.String;
using ReseedButtonControl = System.Byte;
using RollControl = PaintDotNet.Rendering.Vector3Double;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;



namespace BoldBrushEffect
{
    // Old version from before Paint.NET 5.0

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Bold Brushstrokes")]
    public class BoldBrushEffectPlugin : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Bold Brushstrokes";
            }
        }

        public static Image StaticIcon
        {
            get
            {
                return new Bitmap(typeof(BoldBrushEffectPlugin), "BoldBrush.icon.png");
            }
        }

        public static string SubmenuName
        {
            get
            {
                return SubmenuNames.Artistic;
            }
        }

        public BoldBrushEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuName, new EffectOptions() { Flags = EffectFlags.Configurable | EffectFlags.SingleThreaded })
        {
        }

        public enum PropertyNames
        {
            Radius,
            StrokeWidthPercent,
            Blendiness,
            PreserveFineDetails,
            StrokeStart,
            StrokeEnd,
            Antialias,
            DirectionType,
            AngleAdjust,
            Emphasis,
            EmphasisColour
        }

        public enum StrokeOptions
        {
            Rough,
            Round,
            Flat,
            Pointy
        }

        public enum DirectionTypeOptions
        {
            TowardsSimilarPixels,
            DependsOnColour,
            OneDirection
        }

        public enum EmphasisOptions
        {
            SpecifiedColour,
            Colourful,
            Grey,
            Light,
            Dark,
            Smooth,
            Rough
        }


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            ColorBgra PrimaryColor = EnvironmentParameters.PrimaryColor.NewAlpha(byte.MaxValue);
            ColorBgra SecondaryColor = EnvironmentParameters.SecondaryColor.NewAlpha(byte.MaxValue);

            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.Radius, 20, 4, 200));
            props.Add(new Int32Property(PropertyNames.StrokeWidthPercent, 20, 1, 100));
            props.Add(new Int32Property(PropertyNames.Blendiness, 65, 0, 95));
            props.Add(new Int32Property(PropertyNames.PreserveFineDetails, 1, 0, 30));
            StrokeOptions StrokeStartDefault = (Enum.IsDefined(typeof(StrokeOptions), 1)) ? (StrokeOptions)1 : 0;
            props.Add(StaticListChoiceProperty.CreateForEnum<StrokeOptions>(PropertyNames.StrokeStart, StrokeStartDefault, false));
            props.Add(StaticListChoiceProperty.CreateForEnum<StrokeOptions>(PropertyNames.StrokeEnd, 0, false));
            props.Add(new BooleanProperty(PropertyNames.Antialias, true));
            props.Add(StaticListChoiceProperty.CreateForEnum<DirectionTypeOptions>(PropertyNames.DirectionType, 0, false));
            props.Add(new DoubleProperty(PropertyNames.AngleAdjust, 0, -180, 180));
            EmphasisOptions EmphasisDefault = (Enum.IsDefined(typeof(EmphasisOptions), 5)) ? (EmphasisOptions)5 : 0;
            props.Add(StaticListChoiceProperty.CreateForEnum<EmphasisOptions>(PropertyNames.Emphasis, EmphasisDefault, false));
            props.Add(new Int32Property(PropertyNames.EmphasisColour, ColorBgra.ToOpaqueInt32(Color.Blue), 0, 0xffffff));

            List<PropertyCollectionRule> propRules = new()
            {
                new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(PropertyNames.AngleAdjust, PropertyNames.DirectionType, DirectionTypeOptions.TowardsSimilarPixels, false),
                new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(PropertyNames.EmphasisColour, PropertyNames.Emphasis, EmphasisOptions.SpecifiedColour, true)
            };

            return new PropertyCollection(props, propRules);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Radius, ControlInfoPropertyNames.DisplayName, "Stroke length (px)");
            configUI.SetPropertyControlValue(PropertyNames.StrokeWidthPercent, ControlInfoPropertyNames.DisplayName, "Stroke chonkyness (% of length)");
            configUI.SetPropertyControlValue(PropertyNames.Blendiness, ControlInfoPropertyNames.DisplayName, "Stroke blending (% transparency of strokes)");
            configUI.SetPropertyControlValue(PropertyNames.PreserveFineDetails, ControlInfoPropertyNames.DisplayName, "Preserve fine details");
            configUI.SetPropertyControlValue(PropertyNames.StrokeStart, ControlInfoPropertyNames.DisplayName, "Stroke Start");
            PropertyControlInfo StrokeStartControl = configUI.FindControlForPropertyName(PropertyNames.StrokeStart);
            StrokeStartControl.SetValueDisplayName(StrokeOptions.Rough, "Rough");
            StrokeStartControl.SetValueDisplayName(StrokeOptions.Round, "Round");
            StrokeStartControl.SetValueDisplayName(StrokeOptions.Flat, "Flat");
            StrokeStartControl.SetValueDisplayName(StrokeOptions.Pointy, "Pointy");
            configUI.SetPropertyControlValue(PropertyNames.StrokeEnd, ControlInfoPropertyNames.DisplayName, "Stroke End");
            PropertyControlInfo StrokeEndControl = configUI.FindControlForPropertyName(PropertyNames.StrokeEnd);
            StrokeEndControl.SetValueDisplayName(StrokeOptions.Rough, "Rough");
            StrokeEndControl.SetValueDisplayName(StrokeOptions.Round, "Round");
            StrokeEndControl.SetValueDisplayName(StrokeOptions.Flat, "Flat");
            StrokeEndControl.SetValueDisplayName(StrokeOptions.Pointy, "Pointy");
            configUI.SetPropertyControlValue(PropertyNames.Antialias, ControlInfoPropertyNames.DisplayName, string.Empty);
            configUI.SetPropertyControlValue(PropertyNames.Antialias, ControlInfoPropertyNames.Description, "Antialias");
            configUI.SetPropertyControlValue(PropertyNames.DirectionType, ControlInfoPropertyNames.DisplayName, "Stroke Direction");
            PropertyControlInfo DirectionTypeControl = configUI.FindControlForPropertyName(PropertyNames.DirectionType);
            DirectionTypeControl.SetValueDisplayName(DirectionTypeOptions.TowardsSimilarPixels, "Towards similar pixels");
            DirectionTypeControl.SetValueDisplayName(DirectionTypeOptions.DependsOnColour, "Depends on colour");
            DirectionTypeControl.SetValueDisplayName(DirectionTypeOptions.OneDirection, "One direction");
            configUI.SetPropertyControlValue(PropertyNames.AngleAdjust, ControlInfoPropertyNames.DisplayName, "Stroke Angle Adjust");
            configUI.SetPropertyControlType(PropertyNames.AngleAdjust, PropertyControlType.AngleChooser);
            configUI.SetPropertyControlValue(PropertyNames.AngleAdjust, ControlInfoPropertyNames.DecimalPlaces, 3);
            configUI.SetPropertyControlValue(PropertyNames.Emphasis, ControlInfoPropertyNames.DisplayName, "Emphasis");
            PropertyControlInfo EmphasisControl = configUI.FindControlForPropertyName(PropertyNames.Emphasis);
            EmphasisControl.SetValueDisplayName(EmphasisOptions.SpecifiedColour, "Specified Colour");
            EmphasisControl.SetValueDisplayName(EmphasisOptions.Colourful, "Colourful");
            EmphasisControl.SetValueDisplayName(EmphasisOptions.Grey, "Grey");
            EmphasisControl.SetValueDisplayName(EmphasisOptions.Light, "Light");
            EmphasisControl.SetValueDisplayName(EmphasisOptions.Dark, "Dark");
            EmphasisControl.SetValueDisplayName(EmphasisOptions.Smooth, "Smooth");
            EmphasisControl.SetValueDisplayName(EmphasisOptions.Rough, "Rough");
            configUI.SetPropertyControlValue(PropertyNames.EmphasisColour, ControlInfoPropertyNames.DisplayName, "Emphasise this colour");
            configUI.SetPropertyControlType(PropertyNames.EmphasisColour, PropertyControlType.ColorWheel);

            return configUI;
        }

        protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
        {
            // Change the effect's window title
            props[ControlInfoPropertyNames.WindowTitle].Value = "Bold Brushstrokes";
            // Add help button to effect UI
            props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;
            props[ControlInfoPropertyNames.WindowHelpContent].Value = "Bold Brushstrokes v1.3\nCopyright ©2023 by Robot Graffiti\nAll rights reserved.";
            base.OnCustomizeConfigUIWindowProperties(props);
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken token, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Radius = token.GetProperty<Int32Property>(PropertyNames.Radius).Value;
            StrokeWidthPercent = token.GetProperty<Int32Property>(PropertyNames.StrokeWidthPercent).Value;
            Blendiness = token.GetProperty<Int32Property>(PropertyNames.Blendiness).Value;
            PreserveFineDetails = token.GetProperty<Int32Property>(PropertyNames.PreserveFineDetails).Value;
            StrokeStart = (byte)(int)token.GetProperty<StaticListChoiceProperty>(PropertyNames.StrokeStart).Value;
            StrokeEnd = (byte)(int)token.GetProperty<StaticListChoiceProperty>(PropertyNames.StrokeEnd).Value;
            Antialias = token.GetProperty<BooleanProperty>(PropertyNames.Antialias).Value;
            DirectionType = (byte)(int)token.GetProperty<StaticListChoiceProperty>(PropertyNames.DirectionType).Value;
            AngleAdjust = token.GetProperty<DoubleProperty>(PropertyNames.AngleAdjust).Value;
            Emphasis = (byte)(int)token.GetProperty<StaticListChoiceProperty>(PropertyNames.Emphasis).Value;
            EmphasisColour = ColorBgra.FromOpaqueInt32(token.GetProperty<Int32Property>(PropertyNames.EmphasisColour).Value);

            PreRender(dstArgs.Surface, srcArgs.Surface);

            base.OnSetRenderInfo(token, dstArgs, srcArgs);
        }

        protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface,SrcArgs.Surface,rois[i]);
            }
        }

        #region User Entered Code
        // Name: Bold Brushstrokes
        // Submenu: Artistic
        // Author: Robot Graffiti
        // Title: Bold Brushstrokes
        // Version: 1.3
        // Desc: Paints over the image with many overlapping brush strokes
        // Keywords: Brush, Brushstrokes, Painterly, Painted, Painting
        // URL: https://forums.getpaint.net/topic/121378-bold-brushstrokes-v10-nov-28-2022/
        // Help:
        // Force Single Threaded
        #region UICode
        IntSliderControl Radius = 20; // [4,200] Stroke length (px)
        IntSliderControl StrokeWidthPercent = 20; // [1,100] Stroke chonkyness (% of length)
        IntSliderControl Blendiness = 65; // [0,95] Stroke blending (% transparency of strokes)
        IntSliderControl PreserveFineDetails = 1; // [0,30] Preserve fine details
        ListBoxControl StrokeStart = 1; // Stroke Start|Rough|Round|Flat|Pointy
        ListBoxControl StrokeEnd = 0; // Stroke End|Rough|Round|Flat|Pointy
        CheckboxControl Antialias = true; // Antialias
        ListBoxControl DirectionType = 0; // Stroke Direction|Towards similar pixels|Depends on colour|One direction
        AngleControl AngleAdjust = 0; // [-180,180] {!DirectionType} Stroke Angle Adjust
        ListBoxControl Emphasis = 5; // Emphasis|Specified Colour|Colourful|Grey|Light|Dark|Smooth|Rough
        ColorWheelControl EmphasisColour = ColorBgra.FromBgr(255, 0, 0); // [Blue] {Emphasis} Emphasise this colour
        #endregion
        
        private struct Shapey
        {
            public Color DrawColor;
            public Color StartColor;
            public Color MidColor;
            public Color EndColor;
            public int X;
            public int Y;
            public int CloseX;
            public int CloseY;
            public float Sort1;
            public float Sort2;
        }
        
        private int ColourDifference(ColorBgra a, ColorBgra b)
        {
            return Math.Abs((int) a.R - (int) b.R) + Math.Abs((int) a.G - (int) b.G) + Math.Abs((int) a.B - (int) b.B);
        }
        private int ColourDifference(ColorBgra a, ColorBgra b, ColorBgra c)
        {
            return ColourDifference(a, b) + ColourDifference(b, c) + ColourDifference(c, a);
        }
        
        const int numberOfBuckets = 4;
        private delegate float SortFunction(Shapey s);
        
        static readonly object graphicsLock = new();
        
        (SortFunction sortFunction1, SortFunction sortFunction2) GetSortFunctions()
        {
            SortFunction bucketFunction;
            SortFunction bucketFunction2;
        
            switch (Emphasis) // Emphasis|Colour|Grey|Light|Dark
            {
                case 0: // Specified Colour
                    bucketFunction = s => -(ColourDifference(EmphasisColour, s.DrawColor));
                    bucketFunction2 = s => s.DrawColor.GetBrightness();
                    //shapeys = shapeys.OrderByDescending(s => ColourDifference(EmphasisColour, s.Color)).ToList();
                    break;
                    case 1: // Colourful
                    bucketFunction = s => s.DrawColor.GetSaturation();
                    bucketFunction2 = s => Math.Abs(s.DrawColor.GetBrightness() - 0.5f);
                    //shapeys = shapeys.OrderBy(s => s.Color.GetSaturation()).ThenBy(s => Math.Abs(s.Color.GetBrightness() - 0.5)).ToList();
                    break;
                case 2: // Grey
                    bucketFunction = s => -s.DrawColor.GetSaturation();
                    bucketFunction2 = s => -Math.Abs(s.DrawColor.GetBrightness() - 0.5f);
                    //shapeys = shapeys.OrderByDescending(s => s.Color.GetSaturation()).ThenByDescending(s => Math.Abs(s.Color.GetBrightness() - 0.5)).ToList();
                    break;
                case 3: // Light
                     bucketFunction = s => s.DrawColor.GetBrightness();
                     bucketFunction2 = s => s.DrawColor.GetSaturation();
                     //shapeys = shapeys.OrderBy(s => s.Color.GetBrightness()).ThenBy(s => s.Color.GetSaturation()).ToList();
                    break;
                case 4: // Dark
                     bucketFunction = s => -s.DrawColor.GetBrightness();
                     bucketFunction2 = s => s.DrawColor.GetSaturation();
               //shapeys = shapeys.OrderByDescending(s => s.Color.GetBrightness()).ThenBy(s => s.Color.GetSaturation()).ToList();
                    break;
                case 5: // Low Contrast
                   bucketFunction = s => -ColourDifference(s.DrawColor, s.MidColor, s.EndColor);
                   bucketFunction2 = s => Math.Abs(s.DrawColor.GetBrightness() - 0.5f);
                   //shapeys = shapeys.OrderByDescending(s => ColourDifference(s.Color, s.MidColor, s.EndColor)).ToList();
                    break;
                case 6: // High Contrast
                   bucketFunction = s => ColourDifference(s.DrawColor, s.MidColor, s.EndColor);
                   bucketFunction2 = s => Math.Abs(s.DrawColor.GetBrightness() - 0.5f);
                    //shapeys = shapeys.OrderBy(s => (s.Color.A * s.EndColor.A) * ColourDifference(s.Color, s.MidColor, s.EndColor)).ToList();
                    break;
                default: throw new NotImplementedException();
            }
        
            return (bucketFunction, bucketFunction2);
        }
        
        SortFunction SortFunction1;
        SortFunction SortFunction2;
        Pen pen = new(Color.Aqua);
        Pen blackPen = new(Color.Red);
        Pen greyPen = new(Color.Black);
        Pen whitePen = new(Color.Blue);
        int strokeWidthMain;
        
        Surface bump = null;
        
        // This single-threaded function is called after the UI changes and before the Render function is called
        // The purpose is to prepare anything you'll need in the Render function
        void PreRender(Surface dst, Surface src)
        {
            lock(graphicsLock)
            {
                (SortFunction1, SortFunction2) = GetSortFunctions();
                strokeWidthMain = Math.Max(1, (int) (Radius * StrokeWidthPercent / 50));
                using GraphicsPath roughLineCapPath = new();
        
                if (StrokeStart == 0 || StrokeEnd == 0)
                {
                    if (strokeWidthMain <= 10)
                    {
                        roughLineCapPath.AddPolygon(new[]{new PointF(-0.5f, -0.1f), new PointF(-0.33f, 1), new PointF(-0.16f, -0.1f), new PointF(0, 0.5f), new PointF(0.16f, -0.1f), new PointF(0.33f, 1), new PointF(0.5f, -0.1f)});
                    }
                    else if (strokeWidthMain < 25)
                    {
                        roughLineCapPath.AddPolygon(new[]
                        {
                            new PointF(-0.5f, -0.1f), new PointF(-0.4f, 0.5f),
                            new PointF(-0.31f, -0.1f), new PointF(-0.21f, 0.9f),
                            new PointF(-0.1f, -0.1f), new PointF(0, 0.5f),
                            new PointF(0.1f, -0.1f), new PointF(0.2f, 1),
                            new PointF(0.3f, -0.1f), new PointF(0.41f, 0.5f),
                            new PointF(0.5f, -0.1f)
                        });
                    }
                    else
                    {
                        roughLineCapPath.AddPolygon(new[]
                        {
                            new PointF(-0.5f, -0.1f), new PointF(-0.42f, 0.5f),
                            new PointF(-0.4f, -0.2f), new PointF(-0.35f, 0.6f),
                            new PointF(-0.3f, -0.1f), new PointF(-0.25f, 0.7f),
                            new PointF(-0.21f, -0.1f), new PointF(-0.15f, 0.9f),
                            new PointF(-0.1f, 0.3f), new PointF(-0.05f, 0.7f),
                            new PointF(-0.0f, -0.1f), new PointF(0.058f, 0.9f),
                            new PointF(0.1f, 0), new PointF(0.15f, 1),
                            new PointF(0.22f, -0.1f), new PointF(0.26f, 0.7f),
                            new PointF(0.3f, -0.1f), new PointF(0.36f, 0.6f),
                            new PointF(0.41f, -0.2f), new PointF(0.46f, 0.5f),
                            new PointF(0.5f, -0.1f)
                        });
                    }
                }
                
                using CustomLineCap roughLineCap = new(roughLineCapPath, null);
                roughLineCap.BaseInset = 0.1f;
                switch (StrokeStart)
                {
                    case 0:
                        pen.StartCap = LineCap.Custom;
                        pen.CustomStartCap = roughLineCap;
                        whitePen.CustomStartCap = roughLineCap;
                        blackPen.CustomStartCap = roughLineCap;
                        greyPen.CustomStartCap = roughLineCap;
                        break;
                    case 1:
                        pen.StartCap = LineCap.Round;
                        break;
                    case 2:
                        pen.StartCap = LineCap.Flat;
                        break;
                    case 3:
                        pen.StartCap = LineCap.Triangle;
                        break;
                }
                switch (StrokeEnd)
                {
                    case 0:
                        pen.EndCap = LineCap.Custom;
                        pen.CustomEndCap = roughLineCap;
                        whitePen.CustomEndCap = roughLineCap;
                        blackPen.CustomEndCap = roughLineCap;
                        greyPen.CustomEndCap = roughLineCap;
                        break;
                    case 1:
                        pen.EndCap = LineCap.Round;
                        break;
                    case 2:
                        pen.EndCap = LineCap.Flat;
                        break;
                    case 3:
                        pen.EndCap = LineCap.Triangle;
                        break;
                }
                
                if (bump == null) bump = new Surface(src.Size);
                bump.Fill(Color.Black);
            }
        }
        
        /*
        int RandomInt(int x, int y)
        {
           int t = (x^(x<<11));
           return (y^(y>>19))^(t^(t>>8));
        }
        */
        readonly int[,] randomVector = {{-41, 13, 82, -46, 1, 15, 174}, {12, 2, -15, 941, 62, 86, 33},{3, -411, 132, 382, -446, 155, 1574}, {162,  692, 860, 5, -175, 9481, 313}, {165, -56, 6, 41, 35, -23, 1}, {121, -152, 865, 9413, 624, 7, 336}, {1656, 8, -565, 414, 353, -232, 11}};
        int RandomInt(int x, int y)
        {
            int a = x + randomVector[(x & int.MaxValue) % 7, (y & int.MaxValue) % 5];
            int b = y + randomVector[(y & int.MaxValue) % 5, (x & int.MaxValue) % 7];
            int t = a^(a<<11);
            return ((b^(b>>19))^(t^(t>>8)));
        }
        
        void Render(Surface dst, Surface src, Rectangle rect)
        {
            dst.CopySurface(src, rect.Location, rect);
            //dst.Fill(rect, ColorBgra.Transparent);
            if (IsCancelRequested) return;
            
            const double stepAngle = 360 / 60;
            double halfRadius = Radius * 0.5;
        
            const int strokeWidthMultiplierMax = 4;
            for(int strokeWidthMultiplier = strokeWidthMultiplierMax; strokeWidthMultiplier > 0; strokeWidthMultiplier -= 1)
            {
                //if(strokeWidthMultiplier != 1) continue;
                int strokeWidth = (int) (strokeWidthMain * Math.Pow(strokeWidthMultiplier, 1.52));
                
                lock(graphicsLock)
                {
                    if (strokeWidth < 3)
                    {
                        pen.StartCap = LineCap.Flat;
                        pen.EndCap = LineCap.Flat;
                    }
                    else if(strokeWidth < 5)
                    {
                        if (StrokeStart == 0) pen.StartCap = LineCap.Triangle;
                        if (StrokeEnd == 0) pen.EndCap = LineCap.Triangle;
                    }
                    whitePen.EndCap = blackPen.EndCap = greyPen.EndCap = pen.EndCap;
                    whitePen.StartCap = blackPen.StartCap = greyPen.StartCap = pen.StartCap;
                }
        
                int skip;
                if (DirectionType == 0)
                {
                    skip = Math.Max(2, strokeWidth / 2);
                }
                else
                {
                    skip = Math.Max(1, strokeWidth / 2);
                }
        
                float higestSort1 = float.MinValue;
                float lowestSort1 = float.MaxValue;
        
                float higestSort2 = float.MinValue;
                float lowestSort2 = float.MaxValue;
        
                int top = Math.Max(src.Bounds.Top, rect.Top - Radius - strokeWidth);
                int bottom = rect.Bottom + Radius + strokeWidth;
                int left = Math.Max(src.Bounds.Left, rect.Left - Radius - strokeWidth);
                int right = rect.Right + Radius + strokeWidth;
        
                top = top - top % skip;
                left = left - left % skip;
                
                if (top < src.Bounds.Top) top += skip;
                if (top >= src.Bounds.Bottom) continue;
                if (left < src.Bounds.Left) left += skip;
                if (left >= src.Bounds.Right) continue;
        
                List<Shapey> shapeys = new();
        
                for (int ya = top; ya < bottom; ya += skip)
                {
                    if (IsCancelRequested) return;
                    if (ya >= src.Bounds.Bottom) break;
        
                    for (int xa = left; xa < right; xa += skip)
                    {
                        if (xa >= src.Bounds.Right) break;
                        
                        int y = ya + RandomInt(xa + strokeWidthMultiplier, ya) % skip;
                        if (y >= src.Bounds.Bottom) continue;
                        int x = xa + RandomInt(ya + strokeWidthMultiplier, xa) % skip;
                        if (x >= src.Bounds.Right) continue;
        
                        ColorBgra currentPixel = src[x,y];
                        if (currentPixel.A == 0) continue;
                        //var color = Color.FromArgb(255, currentPixel.R, currentPixel.G, currentPixel.B);                    
                        Color color = currentPixel;
                        ColorBgra midPixel;
                        ColorBgra endPixel;
                        int endX;
                        int endY;
                        switch (DirectionType)
                        {
                            case 0: // Toward similar pixels
                            {
                                int minDifference = int.MaxValue;
                                endX = x;
                                endY = y;
                                int previousCloseX = x;
                                int previousCloseY = y;
                                endPixel = color;
                                midPixel = color;
                                for (double angle = 0; angle < 360; angle += stepAngle)
                                {
                                    double radianAngle = angle * Math.PI / 180;
                                    (double offsetX, double offsetY) = Math.SinCos(radianAngle);
                                    
                                    int closeX = x + (int) (offsetX * Radius + 0.5);
                                    int closeY = y + (int) (offsetY * Radius + 0.5);
                                    int midX = x + (int) (offsetX * halfRadius + 0.5);
                                    int midY = y + (int) (offsetY * halfRadius + 0.5);
        
                                    if (previousCloseX == closeX && previousCloseY == closeY) continue;
                                    previousCloseX = closeX;
                                    previousCloseY = closeY;
                                    if (closeX < src.Bounds.Left) continue;
                                    if (closeY < src.Bounds.Top) continue;
                                    if (closeX >= src.Bounds.Right) continue;
                                    if (closeY >= src.Bounds.Bottom) continue;
                                    if(x == closeX && y == closeY) continue;
                                    
                                    ColorBgra comparePixel = src[closeX, closeY];
                                    if(comparePixel.A == 0) continue;
                                    ColorBgra midComparePixel = src[midX, midY];
                                    if(midComparePixel.A == 0) continue;
        
                                    var difference = (256 - comparePixel.A) * ColourDifference(currentPixel, comparePixel)
                                                    + (256 - midComparePixel.A) * ColourDifference(currentPixel, midComparePixel);
                                    if (difference < minDifference)
                                    {
                                        minDifference = difference;
                                        endX = closeX;
                                        endY = closeY;
                                        endPixel = comparePixel;
                                        midPixel = midComparePixel;
                                    }
                                }
                                if (minDifference == int.MaxValue) continue;
                            }
                            break;
                            case 1: // Hue
                            {
                                double angle = color.GetHue() + color.GetBrightness() * 36.0 + color.GetSaturation() * 36.0;
                                double radianAngle = (angle + AngleAdjust) * Math.PI / 180;
                                (double offsetX, double offsetY) = Math.SinCos(radianAngle);
                                
                                endX = Math.Clamp(x + (int) (offsetX * Radius + 0.5), src.Bounds.Left, src.Bounds.Right - 1);
                                endY = Math.Clamp(y + (int) (offsetY * Radius + 0.5), src.Bounds.Top, src.Bounds.Bottom - 1);
                                int midX = Math.Clamp(x + (int) (offsetX * halfRadius + 0.5), src.Bounds.Left, src.Bounds.Right - 1);
                                int midY = Math.Clamp(y + (int) (offsetY * halfRadius + 0.5), src.Bounds.Top, src.Bounds.Bottom - 1);
                                endPixel = src[endX, endY];
                                midPixel = src[midX, midY];
                            }
                            break;
                            case 2: // One Direction
                            {
                                double radianAngle = (AngleAdjust-90) * Math.PI / 180;
                                (double offsetX, double offsetY) = Math.SinCos(radianAngle);
                                
                                endX = Math.Clamp(x + (int) (offsetX * Radius + 0.5), src.Bounds.Left, src.Bounds.Right - 1);
                                endY = Math.Clamp(y + (int) (offsetY * Radius + 0.5), src.Bounds.Top, src.Bounds.Bottom - 1);
                                int midX = Math.Clamp(x + (int) (offsetX * halfRadius + 0.5), src.Bounds.Left, src.Bounds.Right - 1);
                                int midY = Math.Clamp(y + (int) (offsetY * halfRadius + 0.5), src.Bounds.Top, src.Bounds.Bottom - 1);
                                endPixel = src[endX, endY];
                                midPixel = src[midX, midY];
                            }
                            break;
                            default:
                            throw new NotImplementedException($"DirectionType: {DirectionType}");
                        }
                        var midColor = midPixel;
                        var endColor = endPixel;
                        
                        var c = ColorBgra.BlendColors4Fast(color, midColor, midColor, endColor);
                        var shapey = new Shapey{X = x, Y = y, DrawColor = c, StartColor = color, MidColor = midColor, EndColor = endColor, CloseX = endX, CloseY = endY};
                        float sort1 = SortFunction1(shapey);
                        float sort2 = SortFunction2(shapey);
                        
                        shapey.Sort1 = sort1;
                        shapey.Sort2 = sort2;
                        
                        if (sort1 > higestSort1) higestSort1 = sort1;
                        if (sort1 < lowestSort1) lowestSort1 = sort1;
                        if (sort2 > higestSort2) higestSort2 = sort2;
                        if (sort2 < lowestSort2) lowestSort2 = sort2;
        
                        shapeys.Add(shapey);
                    }
                }
                
                List<Shapey>[,] buckets = new List<Shapey>[numberOfBuckets,numberOfBuckets];
                
                // TODO: highest/lowest was a bad idea, it's inconsistent across area boundaries - but maybe it's OK since it's only visible at stupid brush sizes?
                float sort1Range = higestSort1 - lowestSort1;
                float sort2Range = higestSort2 - lowestSort2;
                foreach (Shapey shapey in shapeys)
                {
                    int bucket1 = (sort1Range <= float.Epsilon) ? 0 : (int)((shapey.Sort1 - lowestSort1) * (numberOfBuckets-0.9f) / sort1Range);
                    int bucket2 = (sort2Range <= float.Epsilon) ? 0 : (int)((shapey.Sort2 - lowestSort2) * (numberOfBuckets-0.9f) / sort2Range);
                    var bucket = buckets[bucket1, bucket2];
                    if (bucket == null) buckets[bucket1, bucket2] = bucket = new();
                    bucket.Add(shapey);
                }
        
                //int blendiness = Blendiness * (strokeWidthMultiplierMax - strokeWidthMultiplier - 1) / (strokeWidthMultiplierMax - 1);
                int opacity = 100 - Blendiness;
        
                lock(graphicsLock)
                using (Graphics g = new RenderArgs(dst).Graphics)
                using (Graphics gBump = new RenderArgs(bump).Graphics)
                using (Region clipRegion = new(rect))
                {
                    pen.Width = strokeWidth;            
                    blackPen.Width = strokeWidth;
                    greyPen.Width = strokeWidth;
                    whitePen.Width = strokeWidth;
                    g.Clip = clipRegion;
                    gBump.Clip = clipRegion;
                    if (Antialias)
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        gBump.SmoothingMode = SmoothingMode.AntiAlias;
                    }
                    foreach (List<Shapey> bucket in buckets)
                    {
                        if (bucket == null) continue;
                        foreach (Shapey shapey in bucket)
                        {
                            var c = shapey.DrawColor;
                            pen.Color = Color.FromArgb(c.A * opacity / 100, c.R, c.G, c.B);
                            g.DrawLine(pen, shapey.X, shapey.Y, shapey.CloseX, shapey.CloseY);
                            
                            gBump.DrawLine(blackPen, shapey.X, shapey.Y - 2, shapey.CloseX, shapey.CloseY);
                            gBump.DrawLine(whitePen, shapey.X, shapey.Y + 2, shapey.CloseX, shapey.CloseY);
                            gBump.DrawLine(greyPen, shapey.X, shapey.Y, shapey.CloseX, shapey.CloseY);
                        }
                    }
                }
            }
            lock (graphicsLock)
            {
                if (PreserveFineDetails > 0)
                {
                    for (int y = rect.Top; y < rect.Bottom; y++)
                    {
                        if (IsCancelRequested) return;
                        for (int x = rect.Left; x < rect.Right; x++)
                        {
                            ColorBgra dest = dst[x,y];
                            ColorBgra source = src[x,y];
                            ColorBgra bumpColour = bump[x,y];
                            // Restore alpha
                            dest.A = source.A;
        
                            //Brushstroke highlights
                            dest.R = (byte) Math.Clamp(dest.R + ((bumpColour.B >> 4) - (bumpColour.R >> 5)), 0, 255);                    
                            dest.G = (byte) Math.Clamp(dest.G + ((bumpColour.B >> 4) - (bumpColour.R >> 5)), 0, 255); 
                            dest.B = (byte) Math.Clamp(dest.B + ((bumpColour.B >> 5) - (bumpColour.R >> 5)), 0, 255); 
        
                            // Restore edges
                            int difference = (Math.Abs(source.R - dest.R) + Math.Abs(source.G - dest.G) + Math.Abs(source.B - dest.B));
                            byte intensity = (byte) Math.Min((difference * PreserveFineDetails) / 10, 255);
                            dest = ColorBgra.Blend(dest, source, intensity);
        
                            dst[x,y] = dest;
                        }
                    }
                }
                else
                {
                    for (int y = rect.Top; y < rect.Bottom; y++)
                    {
                        if (IsCancelRequested) return;
                        for (int x = rect.Left; x < rect.Right; x++)
                        {
                            ColorBgra dest = dst[x,y];
                            ColorBgra source = src[x,y];
                            ColorBgra bumpColour = bump[x,y];
        
                            // Restore alpha
                            dest.A = source.A;
                            
                            //Brushstroke highlights
                            dest.R = (byte) Math.Clamp(dest.R + ((bumpColour.B >> 4) - (bumpColour.R >> 5)), 0, 255);                    
                            dest.G = (byte) Math.Clamp(dest.G + ((bumpColour.B >> 4) - (bumpColour.R >> 5)), 0, 255); 
                            dest.B = (byte) Math.Clamp(dest.B + ((bumpColour.B >> 5) - (bumpColour.R >> 5)), 0, 255); 
        
                            dst[x,y] = dest;
                        }
                    }
                }
            }
        }
        
        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);
        
            pen?.Dispose(); pen = null;
            whitePen?.Dispose(); whitePen = null;
            blackPen?.Dispose(); blackPen = null;
            bump?.Dispose(); bump = null;
        }
        #endregion
    }
}
