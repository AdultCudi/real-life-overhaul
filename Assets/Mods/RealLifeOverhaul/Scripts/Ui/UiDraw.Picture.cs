namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Another mod's own app picture, placed as a glyph is: on a tile, letterboxed (UiKind.Picture, found through
    /// UiPictures.Find by its key). The phone names another mod's picture by a handle; its key is "phone:" and the handle,
    /// each key made once. Game-free.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>The key prefix of another mod's app picture, which the phone's game file answers in UiPictures.Find.</summary>
        public const string PhonePicture = "phone:";

        private const int PictureKeysMost = 64;   // handles with a key kept: more than the phone lists apps
        private static readonly string[] PictureKeys = new string[PictureKeysMost];

        /// <summary>The key of another mod's picture by its handle (from 1); made once a handle.</summary>
        public static string PictureKey(int handle)
        {
            if (handle <= 0)
            {
                return null;
            }
            if (handle >= PictureKeysMost)
            {
                return PhonePicture + handle.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return PictureKeys[handle] ?? (PictureKeys[handle] = PhonePicture + handle.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>The handle in a picture's key, or 0 when it is not another mod's app picture.</summary>
        public static int PictureHandle(string key)
        {
            if (key == null || !key.StartsWith(PhonePicture, System.StringComparison.Ordinal))
            {
                return 0;
            }
            int n = 0;
            for (int i = PhonePicture.Length; i < key.Length; i++)
            {
                char c = key[i];
                if (c < '0' || c > '9' || n > 100000)
                {
                    return 0;
                }
                n = n * 10 + (c - '0');
            }
            return n;
        }

        /// <summary>Another mod's picture by its handle in a square box, letterboxed as it was drawn; returns its item.</summary>
        public static int Picture(UiScene s, UiAt at, float x, float y, float size, int picture)
        {
            var item = new UiItem
            {
                Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x, Y = y, W = size, H = size, Text = PictureKey(picture), Value = 1f,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>
        /// A game's own picture by its key (a firm's logo, UiPictures) filling a face's tile a hair inside its edge, in the
        /// place of a face or a glyph; returns the tile.
        /// </summary>
        public static int PictureFace(UiScene s, UiAt at, float x, float y, float size, string key, bool chosen = false)
        {
            int tile = Tile(s, at, x, y, size, UiRole.None, chosen);
            UiAt inside = at.In(tile);
            var item = new UiItem
            {
                Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x + UiTokens.S1, Y = y + UiTokens.S1, W = size - UiTokens.S2, H = size - UiTokens.S2,
                Text = key, Value = 1f, Clip = inside.Clip, Node = -1, Layer = inside.Layer, Order = inside.Order, Parent = inside.Parent, Alpha = 1f
            };
            s.Add(ref item);
            return tile;
        }

        /// <summary>
        /// A picture on an icon's tile, sized as <see cref="IconButton"/> sizes its glyph, so another mod's button stands in a
        /// row of the kit's own as one of them. Returns the tile.
        /// </summary>
        public static int PictureTile(UiScene s, UiAt at, float x, float y, float size, int picture, bool chosen = false)
        {
            int face = Tile(s, at, x, y, size, UiRole.None, chosen);
            float g = Snap(size * 0.64f);
            Picture(s, at.In(face), x + (size - g) / 2f, y + (size - g) / 2f, g, picture);
            return face;
        }
    }
}
