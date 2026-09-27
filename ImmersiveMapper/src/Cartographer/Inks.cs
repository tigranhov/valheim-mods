using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    internal enum BrushSize : byte
    {
        Fine = 0,
        Medium = 1,
        Broad = 2,
    }

    /// <summary>
    /// The colours a drawing can use, by stable index (saved in strokes), and the size of each tool. Sizes are shares
    /// of the view's height, so a zoomed-in brush draws a finer line on the canvas, like leaning closer to the paper.
    /// </summary>
    internal static class Inks
    {
        public readonly struct Swatch
        {
            public readonly string Name;
            public readonly Color32 Color;

            public Swatch(string name, byte r, byte g, byte b)
            {
                Name = name;
                Color = new Color32(r, g, b, 255);
            }
        }

        public const byte Black = 0;
        /// <summary>Swatches from here on are named after the biomes; the player decides where each one goes.</summary>
        public const byte FirstBiome = 4;

        public static readonly Swatch[] All =
        {
            new Swatch("Lampblack", 38, 30, 24),
            new Swatch("Red ochre", 150, 52, 30),
            new Swatch("Woad", 40, 66, 120),
            new Swatch("Verdigris", 58, 92, 44),
            new Swatch("Meadows", 128, 170, 70),
            new Swatch("Black Forest", 40, 80, 45),
            new Swatch("Swamp", 105, 95, 50),
            new Swatch("Mountain", 238, 238, 230),
            new Swatch("Plains", 205, 170, 70),
            new Swatch("Mistlands", 120, 105, 140),
            new Swatch("Ashlands", 110, 35, 25),
            new Swatch("Deep North", 180, 215, 235),
            new Swatch("Ocean", 55, 100, 160),
        };

        public static readonly Color32 GuideColor = new Color32(120, 95, 70, 115);

        public static Color32 Of(byte index)
        {
            return All[index < All.Length ? index : Black].Color;
        }

        public static float PenWidth(Pen pen, BrushSize size)
        {
            switch (pen)
            {
                case Pen.Ink:
                    return Pick(size, 0.0025f, 0.0045f, 0.008f);
                case Pen.Wash:
                    return Pick(size, 0.03f, 0.06f, 0.12f);
                default:
                    return Pick(size, 0.006f, 0.011f, 0.02f);
            }
        }

        public static float StampSize(BrushSize size)
        {
            return Pick(size, 0.045f, Sheet.DefaultStampSize, 0.1f);
        }

        public static float TextSize(BrushSize size)
        {
            return Pick(size, 0.025f, Sheet.DefaultNoteSize, 0.06f);
        }

        public static float EraserRadius(BrushSize size)
        {
            return Pick(size, 0.01f, 0.02f, 0.045f);
        }

        private static float Pick(BrushSize size, float fine, float medium, float broad)
        {
            return size == BrushSize.Fine ? fine : size == BrushSize.Broad ? broad : medium;
        }
    }
}
