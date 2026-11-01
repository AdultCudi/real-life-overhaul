namespace RealLifeOverhaul.Ui
{
    /// <summary>A picture the kit does not draw (another mod's app icon), placed as a glyph is: on a tile, letterboxed. Game-free.</summary>
    public static partial class UiDraw
    {
        /// <summary>A picture by its handle (UiPictures) in a square box, letterboxed as it was drawn; returns its item.</summary>
        public static int Picture(UiScene s, UiAt at, float x, float y, float size, int picture)
        {
            var item = new UiItem
            {
                Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x, Y = y, W = size, H = size, Value = picture,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
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
