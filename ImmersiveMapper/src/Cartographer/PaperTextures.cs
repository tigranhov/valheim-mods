using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Textures made in code, so the mod ships no asset files: a parchment sheet with handled edges, an edgeless parchment
    /// that repeats under a master map, and a pen strip with three rows (grainy charcoal, smooth ink, soft wash).
    /// Strokes are tinted by vertex colour.
    /// </summary>
    internal static class PaperTextures
    {
        public static readonly Color PaperColor = new Color(0.86f, 0.78f, 0.62f);

        /// <summary>Canvas units covered by one repeat of <see cref="PaperTile"/>.</summary>
        public const float TileUnits = 1.2f;

        private const int PenRow = 32;

        private static Texture2D _paper;
        private static Texture2D _tile;
        private static Texture2D _pens;

        public static Texture2D Paper => _paper != null ? _paper : _paper = MakePaper(512, 366, true);

        public static Texture2D PaperTile => _tile != null ? _tile : _tile = MakePaper(512, 512, false);

        public static Texture2D Pens => _pens != null ? _pens : _pens = MakePens(128);

        /// <summary>The v range of a pen's row in <see cref="Pens"/>, kept off the seams so bilinear filtering doesn't mix rows.</summary>
        public static Vector2 PenRows(Pen pen)
        {
            int row = pen == Pen.Wash ? 2 : pen == Pen.Charcoal ? 0 : 1;
            float margin = 0.03f / 3f;
            return new Vector2(row / 3f + margin, (row + 1) / 3f - margin);
        }

        /// <summary>A v in the middle of the wash row, where it's most opaque: fills use it.</summary>
        public static float FillRow => 2.5f / 3f;

        private static Texture2D MakePaper(int width, int height, bool edges)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = edges ? "IM_Parchment" : "IM_ParchmentTile",
                // Mirrored repeats hide the seams of noise that doesn't wrap.
                wrapMode = edges ? TextureWrapMode.Clamp : TextureWrapMode.Mirror,
            };
            var pixels = new Color[width * height];
            var random = new System.Random(7);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float blotches = Mathf.PerlinNoise(x * 0.012f, y * 0.012f);
                    float fibres = Mathf.PerlinNoise(x * 0.09f + 40f, y * 0.35f + 11f);
                    float grain = (float)random.NextDouble();
                    float shade = 0.9f + 0.08f * blotches + 0.04f * fibres + 0.025f * grain;
                    Color color = PaperColor * shade;
                    if (edges)
                    {
                        // Darker, browner edges, like handled parchment.
                        float edgeX = Mathf.Min(x, width - 1 - x) / (width * 0.5f);
                        float edgeY = Mathf.Min(y, height - 1 - y) / (height * 0.5f);
                        float edge = 1f - Mathf.Clamp01(Mathf.Min(edgeX, edgeY) * 5f);
                        color = Color.Lerp(color, new Color(0.55f, 0.4f, 0.24f), edge * edge * 0.55f);
                    }
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakePens(int width)
        {
            int height = PenRow * 3;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "IM_Pens",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int row = y / PenRow;
                float across = (y % PenRow + 0.5f) / PenRow;
                // 1 in the middle of the stroke, 0 at its edges.
                float middle = 1f - Mathf.Abs(across * 2f - 1f);
                for (int x = 0; x < width; x++)
                {
                    float alpha;
                    if (row == 0)
                    {
                        float tooth = Mathf.PerlinNoise(x * 0.31f, y * 0.9f);
                        float drag = Mathf.PerlinNoise(x * 0.05f + 17f, y * 0.2f);
                        // Dense enough to read as a solid line, with a little tooth left at the edges.
                        alpha = Mathf.Clamp01(middle * 2.6f - 0.15f) * Mathf.Lerp(0.72f, 1f, tooth * 0.7f + drag * 0.3f);
                    }
                    else if (row == 1)
                    {
                        alpha = Mathf.Clamp01(middle * 4f);
                    }
                    else
                    {
                        float mottle = Mathf.PerlinNoise(x * 0.08f + 5f, y * 0.25f + 3f);
                        float soft = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(middle * 1.6f));
                        alpha = soft * Mathf.Lerp(0.88f, 1f, mottle);
                    }
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
