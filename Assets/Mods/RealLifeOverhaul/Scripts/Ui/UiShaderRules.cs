namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Which shader the interface's text is drawn with, and what its check says, game-free. The font bundle (reallife_ui) is
    /// built on Windows, and a material in a bundle brings its shader along compiled for that build alone: on a Mac, where
    /// the game draws with Metal, it does not draw and every letter was a pink block. So a face never keeps the shader it
    /// came with: when the bundle is read each face's material is given a text shader the running game has, and a face is
    /// written with only while its shader draws on this graphics device; otherwise the game's font stands in.
    /// Checked offline (EconomyCheck, UiShaderChecks). Cost: a few comparisons when the fonts are read; nothing a frame.
    /// </summary>
    public static class UiShaderRules
    {
        public const string Bundle = "reallife" + "_ui";   // split, so the locale check does not take the bundle's name for a key
        public const string Text = "TextMeshPro/Distance Field";               // what the faces were made for, and the game's interface font's own
        public const string TextSmall = "TextMeshPro/Mobile/Distance Field";   // TextMeshPro's lighter cut: plain letters come out the same, and the kit uses no outline, glow or bevel

        /// <summary>The places a text shader of the game's is looked for, in the order they are asked (<see cref="Pick"/>).</summary>
        public const int FromDefaultFont = 0, FromGameFont = 1, ByName = 2, ByNameSmall = 3, Places = 4;

        /// <summary>True for a shader that draws the kit's letters as they were made: one of TextMeshPro's two distance field shaders for an interface.</summary>
        public static bool ForText(string name) => name == Text || name == TextSmall;

        /// <summary>
        /// The place whose shader the faces are given: the first that offers a text shader which draws on this graphics
        /// device. TextMeshPro's default font comes first because the game holds it for the whole session, so its shader
        /// never goes with a scene (in Build 3682 it is NotoSans-Regular SDF, drawn with the game's own copy of
        /// <see cref="Text"/>); then the game's interface font, then the game's shaders by name, where the engine may
        /// hand back the bundle's own copy. -1 for none: the faces are then left out and the game's font stands in.
        /// </summary>
        public static int Pick(string[] names, bool[] draws)
        {
            for (int i = 0; names != null && draws != null && i < names.Length && i < draws.Length; i++)
            {
                if (draws[i] && ForText(names[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Where the shader in use came from, for the check's line.</summary>
        public static string From(int place, string font)
        {
            switch (place)
            {
                case FromDefaultFont: return "from TextMeshPro's default font in the game, " + (font ?? "?");
                case FromGameFont: return "from the game's interface font, " + (font ?? "?");
                case ByName:
                case ByNameSmall: return "found in the game by name";
                default: return "none found";
            }
        }

        /// <summary>
        /// The check's one line for a bundle: how many materials and shaders it brought, how many of those shaders do not
        /// draw on this graphics device, how many were replaced by the game's own, and the materials left on a shader that
        /// does not draw (<paramref name="left"/> names them; none is a pass). A player's Player.log proves or disproves
        /// the fix with it, so it also says what the text is drawn with, the graphics device, the system and the game.
        /// </summary>
        public static string Line(string bundle, int materials, int shaders, int unsupported, int repointed, int leftCount, string left, string with, string device, string card,
            string system, int build)
        {
            bool pass = leftCount == 0;
            return "Shaders check (" + bundle + "): " + materials + " materials, " + shaders + " shaders, " + unsupported + " not supported on " + device + ", " + repointed
                   + " repointed to the game's own, " + leftCount + " left: "
                   + (pass ? "PASS" : "FAIL (" + left + ": no text shader of the game's that draws here was found, so the game's font stands in for these faces)")
                   + ". Text shader in use: " + with + ". Graphics: " + device + " (" + card + "). System: " + system + ". Game: Build " + build + ".";
        }
    }
}
