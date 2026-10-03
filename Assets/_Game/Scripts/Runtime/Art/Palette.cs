using UnityEngine;

namespace Tailwind.Art
{
    /// <summary>Dusk over Gióng Town. Every colour in the game comes from here.</summary>
    public static class Palette
    {
        public static readonly Color SkyTop = Hex(0x2B1E4A);
        public static readonly Color SkyBottom = Hex(0xF28C5B);
        public static readonly Color Sun = Hex(0xFFD08A);
        public static readonly Color FarBuildings = Hex(0x4A3466);
        public static readonly Color NearBuildings = Hex(0x2A1C40);
        public static readonly Color Window = Hex(0xFFC867);
        public static readonly Color Ground = Hex(0x1B1229);
        public static readonly Color Brick = Hex(0x7A4A3C);
        public static readonly Color BrickCap = Hex(0x4B2C25);
        public static readonly Color LanternRope = Hex(0x1E1430);
        public static readonly Color Lantern = Hex(0xF6C453);
        public static readonly Color Stream = new Color(0.86f, 0.97f, 1f, 0.38f);
        public static readonly Color StreamStreak = new Color(0.85f, 0.98f, 1f, 0.75f);
        public static readonly Color Letter = Hex(0xFFF4E0);
        public static readonly Color LetterSeal = Hex(0xE2574C);
        public static readonly Color Glider = Color.white;
        public static readonly Color Accent = Hex(0xFFC867);
        public static readonly Color Ink = Hex(0x14101E);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
