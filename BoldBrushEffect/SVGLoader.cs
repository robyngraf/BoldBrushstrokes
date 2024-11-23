using PaintDotNet;
using PaintDotNet.Direct2D1;
using System.IO;
using System.Text;
using System.Xml;
using IDeviceContext = PaintDotNet.Direct2D1.IDeviceContext;

namespace BoldBrushEffect
{
    internal static class SVGLoader
    {
        public static IEnumerable<string> GetHumanReadableSvgNames() => GetSvgResourceNames().Select(PrettifySvgName);

        public static Dictionary<string, IGeometryRealization[]> LoadGeometryDictionary(IDeviceContext dc) => GetSvgResourceNames().ToDictionary(PrettifySvgName, path => LoadStrokeGeometry(dc, path));

        private static IEnumerable<string> GetSvgResourceNames() =>
            typeof(SVGLoader).Assembly
            .GetManifestResourceNames()
            .Where(name => name.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase));

        private static IGeometryRealization[] LoadStrokeGeometry(IDeviceContext dc, string resourcePath)
        {
            using Stream stream = GetResourceStream(resourcePath);
            var xml = new XmlDocument();
            xml.Load(stream);
            string pathData = "";
            try
            {
                pathData = xml.GetElementsByTagName("path")[0]!.Attributes!["d"]!.Value;
            }
            catch (NullReferenceException e)
            {
                throw new Exception("Cannot find path data in stroke SVG", e);
            }
            using var strokeGeometry = dc.Factory.CreateGeometryFromPathMarkup(pathData);

            return new[] { dc.CreateFilledGeometryRealization(strokeGeometry, 0.1f), dc.CreateFilledGeometryRealization(strokeGeometry, 0.02f) };

            // TODO: create flipped geometries?
            //Matrix3x2Float t = new(-1, 0, 0, 1, 0, 0);
            //using var flippedGeometry = dc.Factory.CreateTransformedGeometry(strokeGeometry, t);
        }
        private static string PrettifySvgName(string path) => InitCapitalise(path.Split('_', '.').Skip(2).SkipLast(1).Join(' '));

        private static string InitCapitalise(string s)
        {
            StringBuilder sb = new(s);
            sb[0] = char.ToUpperInvariant(sb[0]);
            return sb.ToString();
        }

        private static Stream GetResourceStream(string resourcePath)
        {
            var stream = typeof(BoldBrush).Assembly.GetManifestResourceStream(resourcePath);
            if (stream is null) throw new ArgumentException($"{resourcePath} not found in {typeof(BoldBrush).Assembly.FullName}");
            return stream;
        }

    }
}
