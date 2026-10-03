using UnityEngine;

namespace Tailwind.Art
{
    /// <summary>
    /// Shapes drawn into textures at startup, so the game runs with zero art files.
    /// Swap these for real sprites later without touching gameplay code.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite _square;
        private static Sprite _circle;
        private static Sprite _glider;
        private static Sprite _envelope;
        private static Sprite _skyGradient;
        private static Sprite _softBand;

        /// <summary>1×1 unit white square; scale and tint it.</summary>
        public static Sprite Square => _square != null ? _square : (_square = Make(Fill(4, 4, (x, y) => true), 4f, new Vector2(0.5f, 0.5f)));

        /// <summary>1-unit-wide anti-aliased circle.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(Disc(64), 64f, new Vector2(0.5f, 0.5f)));

        /// <summary>Paper glider pointing right, 1 × 0.5 units.</summary>
        public static Sprite Glider => _glider != null ? _glider : (_glider = Make(DrawGlider(128, 64), 128f, new Vector2(0.45f, 0.5f)));

        /// <summary>Envelope, 0.6 × 0.42 units.</summary>
        public static Sprite Envelope => _envelope != null ? _envelope : (_envelope = Make(DrawEnvelope(64, 44), 106f, new Vector2(0.5f, 0.5f)));

        /// <summary>Vertical dusk gradient, 1 × 1 unit, meant to be stretched over the screen.</summary>
        public static Sprite SkyGradient => _skyGradient != null ? _skyGradient : (_skyGradient = Make(Gradient(256, Palette.SkyBottom, Palette.SkyTop), 256f, new Vector2(0.5f, 0.5f), 1));

        /// <summary>1×1 unit white band whose top and bottom edges fade out; used for wind streams.</summary>
        public static Sprite SoftBand => _softBand != null ? _softBand : (_softBand = Make(Band(32), 32f, new Vector2(0.5f, 0.5f)));

        private delegate bool Mask(int x, int y);

        private static Sprite Make(Texture2D texture, float pixelsPerUnit, Vector2 pivot, int width = -1)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var rect = new Rect(0, 0, width > 0 ? width : texture.width, texture.height);
            float ppu = width > 0 ? texture.height : pixelsPerUnit;
            return Sprite.Create(texture, rect, pivot, ppu);
        }

        private static Texture2D Fill(int w, int h, Mask mask)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = mask(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D Disc(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f); // 1-pixel soft edge
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D DrawGlider(int w, int h)
        {
            // Two triangles: the upper wing (bright) and the folded keel (shaded), nose at the right.
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            var nose = new Vector2(w - 2, h * 0.55f);
            var tailTop = new Vector2(2, h - 2);
            var tailMid = new Vector2(w * 0.18f, h * 0.5f);
            var tailBottom = new Vector2(w * 0.12f, 4);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (InTriangle(p, nose, tailTop, tailMid))
                    {
                        c = new Color32(255, 255, 255, 255);
                    }
                    else if (InTriangle(p, nose, tailMid, tailBottom))
                    {
                        c = new Color32(205, 214, 235, 255);
                    }

                    pixels[y * w + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D DrawEnvelope(int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            Color32 paper = Palette.Letter;
            Color32 fold = new Color32(222, 205, 180, 255);
            Color32 seal = Palette.LetterSeal;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool border = x < 2 || y < 2 || x >= w - 2 || y >= h - 2;
                    // V-shaped flap lines from the top corners to the centre.
                    float vy = h - 1 - Mathf.Abs(x - (w - 1) * 0.5f) * (h * 0.9f / (w * 0.5f));
                    bool flap = Mathf.Abs(y - vy) < 1.2f && y > h * 0.3f;
                    bool sealDot = (x - w * 0.5f) * (x - w * 0.5f) + (y - h * 0.42f) * (y - h * 0.42f) < 22f;
                    pixels[y * w + x] = sealDot ? seal : (border || flap) ? fold : paper;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D Gradient(int h, Color bottom, Color top)
        {
            var texture = new Texture2D(1, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                texture.SetPixel(0, y, Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / (h - 1f))));
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D Band(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                // Opaque in the middle, fading to clear over the outer third on each side.
                float edge = Mathf.Min(y + 0.5f, size - y - 0.5f) / (size / 3f);
                byte a = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge)) * 255f);
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        /// <summary>Creates a tinted sprite object under <paramref name="parent"/>.</summary>
        public static SpriteRenderer Spawn(string name, Transform parent, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
