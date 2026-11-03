using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What manage cargo shows: the holder, its slots, its goods as a catalogue with the chosen stack's detail, and Sell all.</summary>
    public sealed class UiCargoModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Package;
        public string Paused;                                  // the game's time is paused: its words at the strip's right, else null
        public string Holder, HolderDetail;                    // what holds the goods (a box truck, a hand truck) and the game's line on it
        public string HolderPicture;
        public float HolderAspect;
        public UiIcon HolderGlyph = UiIcon.Truck;
        public string SlotsLabel, SlotsValue;                  // "Slots used", "6 of 8"
        public float SlotsShare;                               // 0 to 1
        public UiCatalogueModel Catalogue = new UiCatalogueModel();
        public string Worth;                                   // a line on what it all sells for, or null
        public string SellAll;
        public bool SellAllShown, SellAllEnabled;              // the game shows Sell all only when a stack sells
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// Manage cargo (the game's ManageCargoUi): a panel centred over the world, its strip, the holder with its picture and
    /// its slots as a meter, its goods as the catalogue grid (each stack's picture, its name and count, an unpaid stack's
    /// state) with the chosen stack's detail and the game's own actions for it, and at the foot what it all sells for with
    /// Sell all as the danger action, which asks the game's question. Commands: the catalogue's from
    /// <see cref="CatalogueCommand"/>, then <see cref="SellAllCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiCargo
    {
        public const int CatalogueCommand = 630;                                       // + UiCatalogue.Card, + UiCatalogue.Action
        public const int SellAllCommand = 632;
        public static readonly int[] Commands = { CatalogueCommand + UiCatalogue.Card, CatalogueCommand + UiCatalogue.Action, SellAllCommand };
        private const float MostW = 1240f, MostH = 760f;
        private const float HolderTile = UiTokens.S16;
        private const float MeterW = 200f;

        public static void Lay(UiScene s, UiCargoModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("manage_cargo", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float w = Math.Min(MostW, UiDraw.SnapDown(frame.Width - UiTokens.S8 * 2f));
            float h = Math.Min(MostH, UiDraw.SnapDown(frame.BandY - UiTokens.S8 * 2f));
            float x = UiDraw.SnapDown((frame.Width - w) / 2f), y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            Draw(s, m, x, y, w, h, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static void Draw(UiScene s, UiCargoModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, m.Icon, m.Title, true);
            if (!string.IsNullOrEmpty(m.Paused))
            {
                // Before the close button: the strip's right end is the close's.
                float pw = UiDraw.Snap(UiTextRules.Width(m.Paused, UiStyle.Small) + UiTokens.S1);
                float px = x + w - UiFrame.Strip - UiTokens.S3 - pw;
                UiDraw.Icon(s, on, px - UiTokens.Icon - UiTokens.S2, y + (UiFrame.Strip - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Pause, UiRole.Text2, UiFlags.Middle);
                UiDraw.TextMiddle(s, on, px, y, UiFrame.Strip, pw, UiStyle.Small, UiRole.Text2, m.Paused, UiFlags.Truncate);
            }
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            cy = Holder(s, on, m, ix, cy, iw);
            UiDraw.Box(s, on, x, cy, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            // The foot only while it holds something: what it all sells for, or Sell all; else the goods take the room.
            bool footed = m.SellAllShown || !string.IsNullOrEmpty(m.Worth);
            float foot = footed ? UiTokens.Control + UiTokens.S4 * 2f : 0f;
            float body = y + h - foot - cy - UiTokens.S4 * 2f;
            UiCatalogue.Lay(s, on, m.Catalogue, ix, cy + UiTokens.S4, iw, body, keys, out _, CatalogueCommand);
            if (footed)
            {
                Foot(s, on, m, x, y + h - foot, w, keys);
            }
        }

        // The holder's picture on its tile, its name over the game's line on it, and its slots as a meter with "6 of 8".
        private static float Holder(UiScene s, UiAt at, UiCargoModel m, float x, float y, float w)
        {
            float rh = HolderTile + UiTokens.S4 * 2f, ty = y + UiTokens.S4;
            int tile = UiDraw.Box(s, at, x, ty, HolderTile, HolderTile, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = m.HolderPicture, Aspect = m.HolderAspect, Icon = m.HolderGlyph };
            UiCatalogue.Picture(s, at.In(tile), ref card, x, ty, HolderTile, HolderTile, UiTokens.S8);
            float meter = string.IsNullOrEmpty(m.SlotsValue) ? 0f : MeterW + UiTokens.S4 + UiDraw.Snap(UiTextRules.Width(m.SlotsValue, UiStyle.BodyStrong) + UiTokens.S1);
            float tx = x + HolderTile + UiTokens.S4, tw = w - HolderTile - UiTokens.S4 - (meter > 0f ? meter + UiTokens.S6 : 0f);
            bool detail = !string.IsNullOrEmpty(m.HolderDetail);
            float block = UiTextRules.LineHeight(UiStyle.Title) + (detail ? UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small) : 0f), by = ty + (HolderTile - block) / 2f;
            UiDraw.Text(s, at, tx, by, tw, UiStyle.Title, UiRole.Text, m.Holder, UiFlags.Truncate | UiFlags.Middle);   // the pair centred on the tile
            if (detail)
            {
                UiDraw.Text(s, at, tx, by + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, m.HolderDetail, UiFlags.Truncate | UiFlags.Middle);
            }
            if (meter > 0f)
            {
                // The slots' label over the meter, its figure beside it: the meter fills as the holder fills, the cyan till full.
                float mx = x + w - meter, lh = UiTextRules.LineHeight(UiStyle.Small), mh = UiTokens.S2;
                float my = ty + (HolderTile - lh - UiTokens.S2 - mh) / 2f;
                UiDraw.Text(s, at, mx, my, MeterW, UiStyle.Small, UiRole.Text2, m.SlotsLabel, UiFlags.Truncate | UiFlags.Label | UiFlags.Middle);
                UiDraw.Meter(s, at, mx, my + lh + UiTokens.S2, MeterW, mh, (int)Math.Round(Math.Max(0f, Math.Min(1f, m.SlotsShare)) * 100f), m.SlotsShare >= 1f ? UiRole.Warn : UiRole.Accent);
                float vx = mx + MeterW + UiTokens.S4;
                UiDraw.TextMiddle(s, at, vx, ty, HolderTile, x + w - vx, UiStyle.BodyStrong, UiRole.Text, m.SlotsValue, UiFlags.Right | UiFlags.Truncate);
            }
            return y + rh;
        }

        // The foot: what it all sells for at the left, Sell all at the right in the danger's look, as the game's red button.
        private static void Foot(UiScene s, UiAt at, UiCargoModel m, float x, float y, float w, IUiKeys keys)
        {
            UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, by = y + UiTokens.S4;
            float bw = m.SellAllShown ? UiDraw.ButtonWidth(UiButtonKind.Destructive, m.SellAll, UiIcon.Coin, UiAction.Count, keys) : 0f;
            if (!string.IsNullOrEmpty(m.Worth))
            {
                UiDraw.TextMiddle(s, at, ix, by, UiTokens.Control, iw - bw - UiTokens.S4, UiStyle.Body, UiRole.Text2, m.Worth, UiFlags.Truncate);
            }
            if (m.SellAllShown)
            {
                UiDraw.Button(s, at, ix + iw - bw, by, bw, UiTokens.Control, UiButtonKind.Destructive, m.SellAll, UiIcon.Coin, UiAction.Count, keys, SellAllCommand, 0, !m.SellAllEnabled);
            }
        }
    }
}
