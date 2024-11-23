using System.Reflection;
using PaintDotNet;

namespace BoldBrushEffect
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author => GetAttribute<AssemblyCopyrightAttribute>().Copyright;

        public string Copyright => GetAttribute<AssemblyDescriptionAttribute>().Description;

        public string DisplayName => GetAttribute<AssemblyProductAttribute>().Product;

        public Version Version => GetAssembly().GetName().Version!;

        public Uri WebsiteUri => new("https://forums.getpaint.net/topic/121378-bold-brushstrokes-v12-dec-17-2022/");

        private static T GetAttribute<T>() where T : Attribute => GetAssembly().GetCustomAttribute<T>()!;

        private static Assembly GetAssembly() => typeof(PluginSupportInfo).Assembly;
    }
}
