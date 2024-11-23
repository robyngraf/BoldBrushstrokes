using PaintDotNet.Imaging;
using PaintDotNet.Rendering;

namespace BrushStrokes;

internal struct Stroke
{
    public ColorBgra32 DrawColor;
    public ColorBgra32 StartColor;
    public ColorBgra32 MidColor;
    public ColorBgra32 EndColor;
    public Point2Int32 StartPosition;
    public Point2Int32 EndPosition;
    public int Sort1;
    public int Sort2;
}
