namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A thin ring that fills toward a goal from the top, clockwise: its track the quiet line all round, its run in the
    /// cyan to the share. The time skip veil draws one round its clock. Game-free. Cost: two items.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>The ring in a square of <paramref name="size"/> px at (x, y), filled to <paramref name="share"/> (0 to 1).</summary>
        public static void Ring(UiScene s, UiAt at, float x, float y, float size, float share, UiRole tone = UiRole.Accent)
        {
            share = share < 0f ? 0f : share > 1f ? 1f : share;
            // A slice with a radius is drawn on the thin ring's sprite (UiView.Slots), the track first so the run lies over it.
            var track = new UiItem
            {
                Kind = UiKind.Slice, Fill = UiRole.Track, X = x, Y = y, W = size, H = size, Value = 1f, Points = new[] { 0f, 1f }, Radius = 1f, Alpha = 1f,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Flags = UiFlags.Decor
            };
            s.Add(ref track);
            if (share <= 0f)
            {
                return;
            }
            var run = new UiItem
            {
                Kind = UiKind.Slice, Fill = tone == UiRole.None ? UiRole.Accent : tone, X = x, Y = y, W = size, H = size, Value = share, Points = new[] { 0f, share },
                Radius = 1f, Alpha = 1f, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Flags = UiFlags.Decor
            };
            s.Add(ref run);
        }
    }
}
