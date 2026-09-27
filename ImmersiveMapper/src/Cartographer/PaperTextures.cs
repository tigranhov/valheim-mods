using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Textures made in code, so the mod ships no asset files: the parchment, and a pen strip whose lower half is grainy
    /// charcoal and upper half smooth ink. Strokes are tinted by vertex colour.
    /// </summary>
    internal static class PaperTextures
    {
        public static readonly Color PaperColor = new Color(0.86f, 0.78f, 0.62f);

        private static Texture2D _paper;
        private static Texture2D _pens;

        public static Texture2D Paper => _paper != null ? _paper : _paper = MakePaper(512, 366);

        public static Texture2D Pens => _pens != null ? _pens : _pens = MakePens(128, 64);

        /// <summary>The v range of each pen in <see cref="Pens"/>, kept off the seam so bilinear filtering doesn't mix them.</summary>
        public static Vector2 PenRows(Pen pen)
        {
            return pen == Pen.Ink ? new Vector2(0.53f, 0.97f) : new Vector2(0.03f, 0.47f);
        }

        private static Texture2D MakePaper(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "IM_Parchment", wrapMode = TextureWrapMode.Clamp };
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

                    // Darker, browner edges, like handled parchment.
                    float edgeX = Mathf.Min(x, width - 1 - x) / (width * 0.5f);
                    float edgeY = Mathf.Min(y, height - 1 - y) / (height * 0.5f);
                    float edge = 1f - Mathf.Clamp01(Mathf.Min(edgeX, edgeY) * 5f);
                    edge *= edge;

                    Color color = PaperColor * shade;
                    color = Color.Lerp(color, new Color(0.55f, 0.4f, 0.24f), edge * 0.55f);
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakePens(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "IM_Pens",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            int half = height / 2;
            for (int y = 0; y < height; y++)
            {
                bool ink = y >= half;
                float across = ((ink ? y - half : y) + 0.5f) / half;
                // 1 in the middle of the stroke, 0 at its edges.
                float middle = 1f - Mathf.Abs(across * 2f - 1f);
                for (int x = 0; x < width; x++)
                {
                    float alpha;
                    if (ink)
                    {
                        alpha = Mathf.Clamp01(middle * 4f) * 0.92f;
                    }
                    else
                    {
                        float tooth = Mathf.PerlinNoise(x * 0.31f, y * 0.9f);
                        float drag = Mathf.PerlinNoise(x * 0.05f + 17f, y * 0.2f);
                        alpha = Mathf.Clamp01(middle * 2.2f - 0.25f) * Mathf.Lerp(0.35f, 0.95f, tooth * 0.7f + drag * 0.3f);
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
