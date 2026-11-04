using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's photo picker: the whole Gallery to choose a post's photo from. Its title, its search words, its albums as chips and the
    /// photos as the Gallery's own album (a grid under a heading a day). The screen fills it. Game-free.
    /// </summary>
    public sealed class FlarePickerModel
    {
        public string Title, Search, SearchHint, Cancel;
        public string[] Albums = new string[0];       // each album's name with its count
        public int Album;
        public readonly UiAlbumModel Photos = new UiAlbumModel();
    }

    public static partial class FlareFace
    {
        public const float PickerWidth = 1040f, PickerHeight = 760f;

        // The picker over the window: a card with its title and close, the search, the album chips, the grid, Cancel. Layer 1, as the sheets.
        private static void PhotoSheet(UiScene s, FlareFaceModel m, float wx, float wy, float ww, float wh, IUiKeys keys)
        {
            FlarePickerModel k = m.Picker;
            UiDraw.Scrim(s, wx, wy, ww, wh, 1);
            float cw = Math.Min(PickerWidth, UiDraw.SnapDown(ww - UiTokens.S16 * 2f));
            float ch = Math.Min(PickerHeight, UiDraw.SnapDown(wh - UiTokens.S8 * 2f));
            float cx = wx + UiDraw.SnapDown((ww - cw) / 2f), cy = wy + UiDraw.SnapDown((wh - ch) / 2f);
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 15 };
            int card = UiDraw.Box(s, at, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            at = at.In(card);
            float x = cx + UiTokens.S6, w = cw - UiTokens.S6 * 2f, y = cy + UiTokens.S6;
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w - UiTokens.Control - UiTokens.S2, UiStyle.Number, UiRole.Text, k.Title, UiFlags.Truncate);
            int close = UiDraw.Box(s, at, x + w - UiTokens.Control, y, UiTokens.Control, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.Icon(s, at.In(close), x + w - UiTokens.Control + (UiTokens.Control - UiTokens.Icon) / 2f, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Close,
                UiRole.Text2, UiFlags.Middle | UiFlags.Centre);
            UiDraw.Node(s, at, x + w - UiTokens.Control, y, UiTokens.Control, UiTokens.Control, SheetCancelCommand, 0, close);
            y += UiTokens.Control + UiTokens.S3;
            TextField(s, at, x, y, w, k.Search, k.SearchHint, PickerField, UiIcon.Search);
            y += UiTokens.Control + UiTokens.S3;
            // The albums as chips, wrapped to the width.
            float px = x;
            for (int i = 0; i < k.Albums.Length; i++)
            {
                float pw = Math.Min(w, PillWidth(k.Albums[i]));
                if (px + pw > x + w && px > x)
                {
                    px = x;
                    y += UiTokens.ControlSmall + UiTokens.S2;
                }
                Pill(s, at, px, y, pw, k.Albums[i], i == k.Album ? PillKind.Chosen : PillKind.Line, PickerAlbumCommand, i);
                px += pw + UiTokens.S2;
            }
            y += UiTokens.ControlSmall + UiTokens.S3;
            float foot = UiTokens.Control + UiTokens.S3;
            float gh = Math.Max(UiTokens.S16 * 2f, cy + ch - UiTokens.S6 - foot - y);
            UiAlbum.Grid(s, at, k.Photos, x, y, w - UiTokens.S4, gh, PickerPhotoCommand, true);
            float qw = Math.Min(w / 2f, ButtonWidth(k.Cancel));
            float qx = x + w - qw, qy = cy + ch - UiTokens.S6 - UiTokens.Control;
            int cancel = UiDraw.Box(s, at, qx, qy, qw, UiTokens.Control, UiRole.None, UiRole.Line, UiTokens.Control / 2f, UiFlags.Circle);
            UiDraw.TextMiddle(s, at.In(cancel), qx, qy, UiTokens.Control, qw, UiStyle.Button, UiRole.Text, k.Cancel, UiFlags.Centre | UiFlags.Label);
            UiDraw.Node(s, at, qx, qy, qw, UiTokens.Control, SheetCancelCommand, 1, cancel);
        }
    }
}
