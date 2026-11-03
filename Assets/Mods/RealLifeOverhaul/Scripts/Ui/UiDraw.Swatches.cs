using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A row of colour swatches, as a game's colour grid: each colour a square on its slate tile, the chosen one framed in
    /// the cyan all round, the row wrapping when it is longer than the room. A colour is a picture the game resolves
    /// (UiPictures): the game's own swatch sprite, or one square of a colour the kit names by <see cref="SwatchKey"/>.
    /// Game-free. Cost: a box, a picture and a target a swatch, on a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>A swatch's side, its tile with its frame: a control's height, so a row of them is pressed as buttons are.</summary>
        public const float Swatch = UiTokens.Control;

        /// <summary>The key prefix of a square of one colour, which the game's side answers with a sprite of that colour.</summary>
        public const string SwatchPicture = "swatch:";

        private static readonly Dictionary<int, string> SwatchKeys = new Dictionary<int, string>(32);

        /// <summary>The picture key of one colour (0xRRGGBB); made once a colour.</summary>
        public static string SwatchKey(int rgb)
        {
            rgb &= 0xFFFFFF;
            if (!SwatchKeys.TryGetValue(rgb, out string key))
            {
                key = SwatchPicture + rgb.ToString("X6", CultureInfo.InvariantCulture);
                SwatchKeys[rgb] = key;
            }
            return key;
        }

        /// <summary>The colour a swatch key names (0xRRGGBB), or -1 when the key is no colour of the kit's.</summary>
        public static int SwatchColour(string key)
        {
            if (key == null || !key.StartsWith(SwatchPicture, StringComparison.Ordinal) || key.Length != SwatchPicture.Length + 6)
            {
                return -1;
            }
            return int.TryParse(key.Substring(SwatchPicture.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb) ? rgb : -1;
        }

        /// <summary>The height a row of <paramref name="count"/> swatches takes in a width.</summary>
        public static float SwatchesHeight(float w, int count)
        {
            int across = Math.Max(1, (int)((w + UiTokens.S2) / (Swatch + UiTokens.S2)));
            int rows = Math.Max(1, (count + across - 1) / across);
            return rows * Swatch + (rows - 1) * UiTokens.S2;
        }

        /// <summary>
        /// The swatches from <paramref name="x"/>, <paramref name="y"/>, wrapping in <paramref name="w"/>: each a target of
        /// <paramref name="command"/> with its index. Returns the height.
        /// </summary>
        public static float Swatches(UiScene s, UiAt at, float x, float y, float w, string[] pictures, int count, int chosen, int command, bool disabled = false)
        {
            count = Math.Min(count, pictures?.Length ?? 0);
            int across = Math.Max(1, (int)((w + UiTokens.S2) / (Swatch + UiTokens.S2)));
            int first = s.NodeCount;
            for (int i = 0; i < count; i++)
            {
                float sx = x + (i % across) * (Swatch + UiTokens.S2), sy = y + (i / across) * (Swatch + UiTokens.S2);
                bool on = i == chosen;
                // The chosen one's tile is the cyan, so its frame shows all round the colour: never a mark down one side.
                int face = Box(s, at, sx, sy, Swatch, Swatch, on ? UiRole.Accent : UiRole.Tile, UiRole.None, UiTokens.RadiusLarge,
                    (on ? UiFlags.Selected : UiFlags.None) | (disabled ? UiFlags.Disabled : UiFlags.None));
                float inset = UiTokens.S1, side = Swatch - inset * 2f;
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Text = pictures[i], Value = 1f, X = sx + inset, Y = sy + inset, W = side, H = side, Flags = UiFlags.Share | UiFlags.Middle,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = face, Alpha = 1f
                };
                s.Add(ref item);
                Node(s, at, sx, sy, Swatch, Swatch, command, i, face, disabled);
            }
            if (count > 0 && s.NodeCount > first)
            {
                s.Nodes[first].Left = first;   // the row's ends go nowhere sideways, so the walk stays in the row
                s.Nodes[s.NodeCount - 1].Right = s.NodeCount - 1;
            }
            return SwatchesHeight(w, count);
        }
    }
}
