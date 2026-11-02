namespace RealLifeOverhaul
{
    /// <summary>
    /// When a modelled prop cannot be drawn as modelled and what is said then (game-free, checked offline in NavCheck): a bundle's atlas that
    /// is not a real picture, a lit shader the game's build or the player's graphics settings do not give, a piece missing from the bundles.
    /// A prop drawn white is a material whose texture or shader did not arrive; the mod then draws the plain shapes in their own colours,
    /// and says in one line which piece, which material, why, and what the player can do about it.
    /// </summary>
    public static class PropFitRules
    {
        public const int SmallestAtlas = 64;   // pixels: a real atlas is 2048 or 4096 wide; a 1 x 1 or 4 x 4 is a placeholder, drawn white

        /// <summary>What is wrong with the material a bundle's props are drawn with, or null if nothing: checked once, when it is first made.</summary>
        public static string Problem(int atlasWidth, int atlasHeight, bool shaderFound, bool shaderSupported, bool takesColourMap)
        {
            if (!shaderFound)
            {
                return "the game's lit shader (HDRP/Lit) was not found";
            }
            if (!shaderSupported)
            {
                return "the game's lit shader is not supported on this graphics setting";
            }
            if (!takesColourMap)
            {
                return "the game's lit shader has no base colour map";
            }
            if (atlasWidth < SmallestAtlas || atlasHeight < SmallestAtlas)
            {
                return "the texture atlas is " + atlasWidth + " x " + atlasHeight + ", a placeholder and not a picture";
            }
            return null;
        }

        /// <summary>What the player can check, said once with the problem; <paramref name="reasonIsBundle"/> is true when the bundle did not load at all.</summary>
        public static string Cause(bool reasonIsBundle)
        {
            return reasonIsBundle
                ? "Likely cause: a second copy of the mod in ModsLocal, a Workshop download that did not finish, "
                  + "or a game build the mod was not made for (the first lines of Player.log name the build)"
                : "Likely cause: a game build or graphics setting the mod was not made for, or a Workshop download that did not finish (verify it in Steam)";
        }

        /// <summary>The one line that names a piece drawn plain: the piece, its material, the reason and the cause.</summary>
        public static string Line(string piece, string material, string reason, bool reasonIsBundle)
        {
            return "Props check (" + piece + "): drawn with plain shapes in their own colours, material '" + material + "': " + reason + ". " + Cause(reasonIsBundle) + ".";
        }
    }

    /// <summary>
    /// What a bus stop is made of and what that changes (game-free, checked offline in NavCheck): the sign and the shelter are each the bundle's
    /// model, plain shapes, or, with no lit shader at all, nothing that can be drawn. The click on a stop never depends on that: its pointer is a box
    /// of its own, so a stop that is plain or bare is still a stop.
    /// </summary>
    public static class BusStopLookRules
    {
        public enum Look { Model, Plain, Bare }

        /// <summary>How a piece of a stop is drawn: the model if the bundle gave it, else plain shapes if the game has a lit shader, else nothing.</summary>
        public static Look Of(bool modelled, bool shapesAvailable)
        {
            return modelled ? Look.Model : shapesAvailable ? Look.Plain : Look.Bare;
        }

        /// <summary>Whether the stop's pointer is made: it needs the game's interactive layer and nothing of how the stop is drawn.</summary>
        public static bool Clickable(Look look, bool layerFound)
        {
            return layerFound;
        }

        /// <summary>Whether the pointer outlines the stop when pointed at: only what has pieces to outline.</summary>
        public static bool Outlined(Look look, int renderers)
        {
            return look != Look.Bare && renderers > 0;
        }

        /// <summary>
        /// Whether a dressing built plain should be made again as modelled: the bundle came in after it was built (the props load in the
        /// background) and has the piece. Asked once a time a stop is put out, never per frame.
        /// </summary>
        public static bool Remodel(Look look, bool bundleReady, bool bundleHasPiece)
        {
            return look != Look.Model && bundleReady && bundleHasPiece;
        }
    }
}
