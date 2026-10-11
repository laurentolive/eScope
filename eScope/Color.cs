using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace eScope
{
    using ScottPlot.Colormaps;
    using System;

    public class ColorRGB
    {
        static int SignalColorIdx = 0;
        static int MarkerColorIdx = 0;

        // mid-lightness colors, readable on the light and the dark plot background
        static List<string> ColorList = new List<string> { "#E5484D", "#3B82F6", "#22A06B", "#A855F7", "#F97316", "#0EA5E9", "#D4A72C", "#8B8D98", "#EC4899", "#14B8A6" };

        public uint color;
        public ColorRGB()
        {
            color = (uint)((255U << 24) | (255U << 16) | (0 << 8) | 0);
        }

        public ColorRGB(System.Drawing.Color color)
        {
            uint colorValue = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        public System.Drawing.Color ToColor()
        {
            byte alpha = (byte)((color >> 24) & 0xFF);
            byte red = (byte)((color  >> 16) & 0xFF);
            byte green = (byte)((color  >> 8) & 0xFF);
            byte blue = (byte)(color & 0xFF);

            return System.Drawing.Color.FromArgb(alpha, red, green, blue);
        }
        public static string GetNextSignalColor()
        {
            int idx = SignalColorIdx;

            SignalColorIdx++;
            if (SignalColorIdx >= ColorList.Count)
            {
                SignalColorIdx = 0;
            }
            return ColorList[idx];
        }

        public static string GetNextMarkerColor()
        {
            int idx = MarkerColorIdx;

            MarkerColorIdx++;
            if (MarkerColorIdx >= ColorList.Count)
            {
                MarkerColorIdx = 0;
            }
            return ColorList[idx];
        }
        public static void MarkerColorReset()
        {
            MarkerColorIdx = 0;
        }
        public static void SignalColorReset()
        {
            SignalColorIdx = 0;
        }
        public static string GetNextColor(ref int colorIdx)
        {
            int idx = colorIdx;

            colorIdx++;
            if (colorIdx >= ColorList.Count)
            {
                colorIdx = 0;
            }
            return ColorList[idx];
        }
    }

}
