using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the mod's asset bundles may bring that only one system draws, game-free. The bundles are built on Windows
    /// (there is no macOS build of them), and a bundle is the same file on every system but for one thing: a shader
    /// inside it is compiled for the graphics API of the system it was built for, so a material that brings one draws
    /// pink on a Mac. So the bundles hold meshes, textures, clips, fonts and text, and what they draw with is the running
    /// game's own shader, found by name or taken from a material of the game's. Each loader hands what it read to
    /// <see cref="BundleShaders"/>, which gives a material that did come in a bundle the game's own shader of its name,
    /// and one line a bundle says what was found. StandardCheck reads the shipped files themselves and fails a bundle
    /// that holds a shader the game has not got. Cost: nothing a frame.
    /// </summary>
    public static class BundleShaderRules
    {
        /// <summary>
        /// The bundles the mod ships, by their files' names under Bundles: each is read by one loader, which has it checked.
        /// (Each name is written in two parts, so the locale check does not take a bundle's name for a text's key.)
        /// </summary>
        public static readonly string[] Bundles =
        {
            "reallife" + "_ui", "reallife" + "_anim", "reallife" + "_character", "reallife" + "_props", "reallife" + "_props_2", "reallife" + "_props_site"
        };

        // A shader a material may come with, and the game's shader that gives the same look where the game has not got the first.
        private static readonly string[] Fallbacks =
        {
            "TextMeshPro/Distance Field", "TextMeshPro/Mobile/Distance Field"   // plain letters come out the same: the kit uses no outline, glow or bevel
        };

        /// <summary>The game's shader that stands in for one it may lack, or null: only a pair written down here is ever swapped.</summary>
        public static string Fallback(string shader)
        {
            for (int i = 0; i + 1 < Fallbacks.Length; i += 2)
            {
                if (Fallbacks[i] == shader)
                {
                    return Fallbacks[i + 1];
                }
            }
            return null;
        }

        /// <summary>True for a name in a bundle's list that is a material or a shader by its ending: such an asset brings what one system alone draws.</summary>
        public static bool BringsShader(string assetName)
        {
            string name = assetName ?? string.Empty;
            return name.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".shadervariants", StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith(".compute", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The check's one line for a bundle: its materials and their shaders, how many of those shaders do not draw on this
        /// graphics device, how many were replaced by the game's own, the materials left on one that does not draw
        /// (<paramref name="left"/> names them), then what the bundle's own pieces are drawn with, the graphics device, the
        /// system and the game. A pass is none left and <paramref name="drawn"/>: the game's shader the pieces are drawn
        /// with is there and draws (true for a bundle that needs none). The same words as the font bundle's line
        /// (Ui/UiShaderRules.Line).
        /// </summary>
        public static string Line(string bundle, int materials, int shaders, int unsupported, int repointed, int leftCount, string left, bool drawn, string with, string device,
            string card, string system, int build)
        {
            string verdict = leftCount > 0 ? "FAIL (" + left + ": the game has no shader of that name that draws here, so what wears these is not drawn as modelled)"
                : drawn ? "PASS" : "FAIL (the game's shader for the bundle's pieces is not there to draw them)";
            return "Shaders check (" + bundle + "): " + materials + " materials, " + shaders + " shaders, " + unsupported + " not supported on " + device + ", " + repointed
                   + " repointed to the game's own, " + leftCount + " left: " + verdict + ". " + with + ". Graphics: " + device + " (" + card + "). System: " + system
                   + ". Game: Build " + build + ".";
        }

        /// <summary>True when a bundle's line is a pass: no material left on a shader that does not draw, and the pieces' own shader there.</summary>
        public static bool Passes(int leftCount, bool drawn)
        {
            return leftCount == 0 && drawn;
        }

        /// <summary>What a bundle's pieces are drawn with, for its line: the game's shader the mod makes their material from, and whether it draws here.</summary>
        public static string With(string shader, bool found, bool draws, int named)
        {
            string list = named == 0 ? "Its list names no material or shader" : "Its list names " + named + " material(s) or shader(s) that were not read";
            if (shader == null)
            {
                return list + "; nothing of it is drawn with a material of its own";
            }
            return list + "; its pieces are drawn with the game's '" + shader + "', which " + (!found ? "was not found" : draws ? "draws here" : "does not draw here");
        }
    }
}
