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

        static List<string> ColorList = new List<string> { "#D00000", "#0000A0", "#007f3F", "#7F007F", "#FF7F00", "#7F3F00", "#F0F000","#7F7F7F", "#FF007F",   "#000000" };

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
