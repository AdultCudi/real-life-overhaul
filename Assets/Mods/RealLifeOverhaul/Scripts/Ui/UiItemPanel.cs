using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A reading of a vehicle's: its glyph and name, how full its meter is and in what tone, and its figure.</summary>
    public struct UiReading
    {
        public UiIcon Icon;
        public string Label;       // "Fuel"
        public float Share;        // 0 to 1; below 0 for a reading with no meter (the speed, the parking)
        public UiRole Tone;        // the meter's and the figure's tone: None for the words' own
        public string Value;       // "64%", "57 km/h"
    }

    /// <summary>An action of the item panel: the game's button, its words, the game's key for it, and how it stands.</summary>
    public struct UiItemAction
    {
        public string Label;
        public UiGameKey Key;      // the game reads this key itself; the cap only names it
        public bool Main;          // the panel's first action: on the cyan
        public bool Danger;        // it parts the player from the thing (Sell, Discard): its words in the danger's red
        public bool Disabled;
    }

    /// <summary>What the item panel shows: the thing in hand or the vehicle, its readings, its cargo and the game's buttons.</summary>
    public sealed class UiItemPanelModel
    {
        public string Title;                                   // "Vehicle", "Item", or the mini game's name
        public UiIcon Icon = UiIcon.Package;
        public string Name, Status;                            // its name, and its state with the street it is on
        public string Picture;                                 // the thing's own picture (UiPictures), or null for the glyph
        public float Aspect;
        public UiIcon Glyph = UiIcon.Package;
        public string Owner;                                   // "Yours", "Not yours", or null
        public UiRole OwnerTone = UiRole.Ok;
        public string ReadingsLabel;
        public UiReading[] Readings = new UiReading[0];
        public UiInfoRow[] Facts = new UiInfoRow[0];           // the parking zone, the unpaid total, what the game's panel adds
        public string CargoLabel, CargoValue;
        public UiCatalogueCard[] Cargo = new UiCatalogueCard[0];   // a tile each stack: its picture, its count as the figure
        public int CargoSlots;                                 // the slots there are: said as "1 of 10", the empty ones never drawn
        public UiItemAction[] Actions = new UiItemAction[0];
        public string More;                                    // another mod's button in the game's panel: opens the game's own view; null for none
        public bool Hidden;                                    // the game has the panel faded out (a menu over the world): nothing is drawn
        public float AvoidX, AvoidY, AvoidW, AvoidH;           // px of the layout: the mod's minimap card, kept clear of; 0 wide for none
    }

    /// <summary>
    /// The item panel (the game's ItemPanelUI): a compact info panel docked at the bottom left above the status bar for the
    /// thing in hand or the vehicle: its strip, its picture with its name, state and whose it is, a vehicle's readings
    /// (fuel, condition, the mod's speed and what the car needs) as meters with their glyphs, its cargo as a tile a stack
    /// under how many of its slots are used, and the game's actions with the game's own key caps. It is as wide as what it
    /// holds needs (narrower with no readings) and keeps clear of the mod's minimap card. The HUD takes no key: the game answers its keys, the
    /// pointer presses the rest. Commands from <see cref="ActionCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiItemPanel
    {
        public const int ActionCommand = 620, CargoCommand = 621, MoreCommand = 622;
        public static readonly int[] Commands = { ActionCommand, CargoCommand, MoreCommand };
        public const float Width = UiInfoPanel.Width;   // px: with a vehicle's readings
        public const float Narrow = 360f;               // px: a thing in hand, its name, its cargo and its buttons

        /// <summary>The panel's width: the info panel's with readings or facts to show, narrower for a thing in hand.</summary>
        public static float WidthOf(UiItemPanelModel m) => m.Readings.Length > 0 || m.Facts.Length > 0 ? Width : Narrow;
        private const float HeadH = UiTokens.S16 + UiTokens.S4;
        private const float PicW = UiTokens.S16 + UiTokens.S6, PicH = UiTokens.S12;
        private const float ReadingH = UiTokens.PairRow + UiTokens.S1;
        private const float LabelW = 96f, ValueW = 112f;
        private const float CargoTile = UiTokens.S12 + UiTokens.S2;   // px: a stack's tile, its picture big enough to tell
        private const int CargoAcross = 8;   // tiles a row at most; fewer, wider ones when a count needs the room

        // Tiles a row: as many as stand at least as wide as they are high, fewer where the widest count needs the room
        // ("$3,009"), never under four.
        private static int Across(UiItemPanelModel m)
        {
            float need = 0f;
            for (int i = 0; i < m.Cargo.Length; i++)
            {
                if (!string.IsNullOrEmpty(m.Cargo[i].Figure))
                {
                    need = Math.Max(need, UiDraw.Snap(UiTextRules.Width(m.Cargo[i].Figure, UiStyle.SmallStrong) + UiTokens.S2) + UiTokens.S1 * 2f);
                }
            }
            float iw = WidthOf(m) - UiTokens.S4 * 2f;
            int fit = (int)((iw + UiTokens.S2) / (Math.Max(need, CargoTile) + UiTokens.S2));
            return Math.Max(4, Math.Min(CargoAcross, fit));
        }

        public static float Height(UiItemPanelModel m)
        {
            if (m.Hidden)
            {
                return 0f;
            }
            float h = UiFrame.Strip + HeadH;
            if (m.Readings.Length > 0 || m.Facts.Length > 0)
            {
                h += UiTokens.S2 * 2f + (m.Readings.Length > 0 ? UiTokens.PairRow + m.Readings.Length * ReadingH : 0f) + m.Facts.Length * UiTokens.PairRow + UiTokens.S1;
            }
            if (Math.Max(m.CargoSlots, m.Cargo.Length) > 0)
            {
                h += CargoHeight(m) + UiTokens.S1;
            }
            int actions = m.Actions.Length + (string.IsNullOrEmpty(m.More) ? 0 : 1);
            if (actions > 0)
            {
                int rows = ActionRows(m, WidthOf(m) - UiTokens.S4 * 2f);
                h += UiTokens.S3 + rows * UiTokens.Control + (rows - 1) * UiTokens.S2 + UiTokens.S3;
            }
            else
            {
                h += UiTokens.S2;
            }
            return UiDraw.Snap(h);
        }

        /// <summary>The panel over the world, docked above the status bar at the left; no bar of its own, as the HUD draws it.</summary>
        public static void Lay(UiScene s, UiItemPanelModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("item_panel", frame.Width, frame.Height, dark);
            s.Pointer = true;   // the HUD takes no key: the game reads its own, the pointer presses the panel
            s.Floats = true;
            if (m.Hidden)
            {
                return;
            }
            float h = Height(m), w = WidthOf(m);
            float x = UiTokens.S4, y = UiDraw.SnapDown(frame.BandY - UiTokens.S4 - h);
            if (m.AvoidW > 0f && m.AvoidH > 0f && UiHudRules.Overlaps(x, y, w, h, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH))
            {
                // The minimap's card stands there: beside it at the same foot, or over it where the screen is too narrow.
                float beside = UiDraw.Snap(m.AvoidX + m.AvoidW + UiTokens.S2);
                if (beside + w <= frame.Width - UiTokens.S4)
                {
                    x = beside;
                }
                else
                {
                    y = Math.Max(UiTokens.S4, UiDraw.SnapDown(m.AvoidY - UiTokens.S2 - h));
                }
            }
            Draw(s, m, x, y);
        }

        public static float Draw(UiScene s, UiItemPanelModel m, float x, float y)
        {
            float h = Height(m), w = WidthOf(m);
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, m.Icon, m.Title, false);
            cy = Head(s, on, m, x, cy, w);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (m.Readings.Length > 0 || m.Facts.Length > 0)
            {
                float rh = UiTokens.S2 * 2f + (m.Readings.Length > 0 ? UiTokens.PairRow + m.Readings.Length * ReadingH : 0f) + m.Facts.Length * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, w, rh);
                float ry = cy + UiTokens.S2;
                if (m.Readings.Length > 0)
                {
                    ry += UiDraw.SectionRow(s, on, ix, ry, iw, m.ReadingsLabel, null, false, -1, 0);
                    for (int i = 0; i < m.Readings.Length; i++)
                    {
                        Reading(s, on, ref m.Readings[i], ix, ry, iw);
                        ry += ReadingH;
                    }
                }
                for (int i = 0; i < m.Facts.Length; i++)
                {
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, m.Facts[i].Label, m.Facts[i].Value, false, -1, 0, m.Facts[i].Ink == UiRole.None ? UiRole.Text : m.Facts[i].Ink);
                }
                cy += rh + UiTokens.S1;
            }
            if (Math.Max(m.CargoSlots, m.Cargo.Length) > 0)
            {
                cy = Cargo(s, on, m, x, cy, w);
            }
            Actions(s, on, m, ix, cy + UiTokens.S3, iw);
            return y + h;
        }

        // The thing's picture on a tile, its name over its state, and whose it is as a chip at the right.
        private static float Head(UiScene s, UiAt at, UiItemPanelModel m, float x, float y, float w)
        {
            float ix = x + UiTokens.S4, py = y + (HeadH - PicH) / 2f;
            int tile = UiDraw.Box(s, at, ix, py, PicW, PicH, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = m.Picture, Aspect = m.Aspect, Icon = m.Glyph };
            UiCatalogue.Picture(s, at.In(tile), ref card, ix, py, PicW, PicH, UiTokens.S6);
            float tx = ix + PicW + UiTokens.S3, right = x + w - UiTokens.S4;
            if (!string.IsNullOrEmpty(m.Owner))
            {
                float cw = UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(m.Owner, UiStyle.SmallStrong) + UiTokens.S1);
                UiDraw.Chip(s, at, right - cw, y + (HeadH - UiTokens.Badge - UiTokens.S1) / 2f, m.Owner, m.OwnerTone, cw);
                right -= cw + UiTokens.S3;
            }
            bool status = !string.IsNullOrEmpty(m.Status);
            float block = UiTextRules.LineHeight(UiStyle.Title) + (status ? UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small) : 0f), ny = y + (HeadH - block) / 2f;
            UiDraw.Text(s, at, tx, ny, right - tx, UiStyle.Title, UiRole.Text, m.Name, UiFlags.Truncate | UiFlags.Middle);   // the pair centred on the head
            if (status)
            {
                UiDraw.Text(s, at, tx, ny + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, right - tx, UiStyle.Small, UiRole.Text2, m.Status, UiFlags.Truncate | UiFlags.Middle);
            }
            return y + HeadH;
        }

        // A reading: its glyph, its name, its meter in its tone (none for a figure alone) and its figure at the right.
        private static void Reading(UiScene s, UiAt at, ref UiReading r, float x, float y, float w)
        {
            float h = UiTokens.PairRow;
            UiDraw.Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tone == UiRole.None ? UiRole.Text2 : r.Tone, UiFlags.Middle);
            float lx = x + UiTokens.Icon + UiTokens.S2;
            UiDraw.TextMiddle(s, at, lx, y, h, LabelW, UiStyle.Body, UiRole.Text2, r.Label, UiFlags.Truncate | UiFlags.Label);
            float vx = x + w - ValueW;
            UiRole ink = r.Tone == UiRole.Danger ? UiRole.DangerText : r.Tone == UiRole.Warn ? UiRole.WarnText : UiRole.Text;
            UiDraw.TextMiddle(s, at, vx, y, h, ValueW, UiStyle.BodyStrong, ink, r.Value, UiFlags.Right | UiFlags.Truncate);
            if (r.Share >= 0f)
            {
                float mx = lx + LabelW + UiTokens.S2, mw = vx - UiTokens.S3 - mx, mh = UiTokens.S2;
                UiDraw.Meter(s, at, mx, y + (h - mh) / 2f, mw, mh, (int)Math.Round(Math.Max(0f, Math.Min(1f, r.Share)) * 100f), r.Tone == UiRole.None ? UiRole.Accent : r.Tone);
            }
        }

        // The cargo's band: its line ("1 of 10") and a row of tiles for the stacks held; the empty slots are said, not drawn.
        private static float CargoHeight(UiItemPanelModel m)
        {
            int stacks = m.Cargo.Length, across = Across(m), rows = (stacks + across - 1) / across;
            return UiTokens.S2 * 2f + UiTokens.PairRow + (stacks > 0 ? UiTokens.S2 + rows * CargoTile + (rows - 1) * UiTokens.S2 : 0f);
        }

        // The cargo: a tile a stack, its picture and its count; a stack's tile takes it out.
        private static float Cargo(UiScene s, UiAt at, UiItemPanelModel m, float x, float y, float w)
        {
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            int slots = m.Cargo.Length, across = Across(m);
            float ch = CargoHeight(m);
            UiDraw.Band(s, at, x, y, w, ch);
            float ry = y + UiTokens.S2;
            ry += UiDraw.SectionRow(s, at, ix, ry, iw, m.CargoLabel, m.CargoValue, false, -1, 0);
            ry += UiTokens.S2;
            float tw = UiDraw.SnapDown((iw - UiTokens.S2 * (across - 1)) / across);
            int first = s.NodeCount;
            for (int i = 0; i < slots; i++)
            {
                float tx = ix + (i % across) * (tw + UiTokens.S2), ty = ry + (i / across) * (CargoTile + UiTokens.S2);
                ref UiCatalogueCard c = ref m.Cargo[i];
                int face = UiDraw.Box(s, at, tx, ty, tw, CargoTile, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(face);
                UiCatalogue.Picture(s, inside, ref c, tx, ty, tw, CargoTile, UiTokens.S8);
                if (!string.IsNullOrEmpty(c.Figure))
                {
                    float fw = Math.Min(tw - UiTokens.S1 * 2f, UiDraw.Snap(UiTextRules.Width(c.Figure, UiStyle.SmallStrong) + UiTokens.S2));
                    float fh = UiTokens.S4 + UiTokens.S1;
                    // The count sits over the picture's corner on purpose, as the phone's badges sit over their tiles.
                    int badge = UiDraw.Box(s, inside, tx + tw - UiTokens.S1 - fw, ty + CargoTile - UiTokens.S1 - fh, fw, fh, UiRole.Panel, UiRole.None, UiTokens.RadiusSmall, UiFlags.Overflow);
                    UiDraw.TextMiddle(s, inside.In(badge), tx + tw - UiTokens.S1 - fw, ty + CargoTile - UiTokens.S1 - fh, fh, fw, UiStyle.SmallStrong, c.State != null ? UiRole.WarnText : UiRole.Text,
                        c.Figure, UiFlags.Centre | UiFlags.Truncate | UiFlags.Overflow);
                }
                UiDraw.Node(s, at, tx, ty, tw, CargoTile, CargoCommand, i, face, c.Disabled);
            }
            if (s.NodeCount > first)
            {
                s.Nodes[first].Left = first;
                s.Nodes[s.NodeCount - 1].Right = s.NodeCount - 1;
            }
            return y + ch + UiTokens.S1;
        }

        // How many rows the buttons take: two a row while both their words fit half the width, else one takes the row.
        private static int ActionRows(UiItemPanelModel m, float w)
        {
            int count = m.Actions.Length + (string.IsNullOrEmpty(m.More) ? 0 : 1), rows = 0;
            float bw = UiDraw.SnapDown((w - UiTokens.S2) / 2f);
            for (int i = 0; i < count; rows++)
            {
                i += i + 1 < count && FitsHalf(m, i, bw) && FitsHalf(m, i + 1, bw) ? 2 : 1;
            }
            return rows;
        }

        // Whether a button's words fit a half-width button beside its key cap.
        private static bool FitsHalf(UiItemPanelModel m, int i, float bw)
        {
            if (i >= m.Actions.Length)
            {
                return UiTextRules.Fits(m.More, UiStyle.Body, bw - UiTokens.S3 * 2f);
            }
            float kw = UiDraw.GameKeyWidth(m.Actions[i].Key);
            return UiTextRules.Fits(m.Actions[i].Label, UiStyle.Body, bw - UiTokens.S2 - (kw > 0f ? kw + UiTokens.S2 : UiTokens.S1) - UiTokens.S3);
        }

        // The game's buttons, two a row while both fit, in the game's order: the game's key on its cap, then the words; the
        // first on the cyan, a parting one in red. A button whose words need more takes the row.
        private static void Actions(UiScene s, UiAt at, UiItemPanelModel m, float x, float y, float w)
        {
            int count = m.Actions.Length + (string.IsNullOrEmpty(m.More) ? 0 : 1);
            float bw = UiDraw.SnapDown((w - UiTokens.S2) / 2f);
            int row = 0;
            for (int i = 0; i < count; row++)
            {
                bool pair = i + 1 < count && FitsHalf(m, i, bw) && FitsHalf(m, i + 1, bw);
                float by = y + row * (UiTokens.Control + UiTokens.S2);
                ActionButton(s, at, m, i, x, by, pair ? bw : w);
                if (pair)
                {
                    ActionButton(s, at, m, i + 1, x + bw + UiTokens.S2, by, bw);
                }
                i += pair ? 2 : 1;
            }
        }

        private static void ActionButton(UiScene s, UiAt at, UiItemPanelModel m, int i, float bx, float by, float aw)
        {
            if (i >= m.Actions.Length)
            {
                int moreFace = UiDraw.Box(s, at, bx, by, aw, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.TextMiddle(s, at.In(moreFace), bx + UiTokens.S3, by, UiTokens.Control, aw - UiTokens.S3 * 2f, UiStyle.Body, UiRole.AccentText, m.More, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Node(s, at, bx, by, aw, UiTokens.Control, MoreCommand, 0, moreFace);
                return;
            }
            ref UiItemAction a = ref m.Actions[i];
            bool main = a.Main && !a.Disabled;
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, bx, by, aw, UiTokens.Control, main ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, a.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float kw = UiDraw.GameKeyWidth(a.Key), kx = bx + UiTokens.S2;
            float tx = kx + (kw > 0f ? kw + UiTokens.S2 : UiTokens.S1);
            UiRole ink = main ? UiRole.OnAccent : a.Danger ? UiRole.DangerText : UiRole.Text;
            UiDraw.TextMiddle(s, inside, tx, by, UiTokens.Control, bx + aw - UiTokens.S3 - tx, UiStyle.Body, ink, a.Label, UiFlags.Label | UiFlags.Truncate);
            int node = UiDraw.Node(s, at, bx, by, aw, UiTokens.Control, ActionCommand, i, face, a.Disabled);
            UiDraw.GameKey(s, inside, kx, by + (UiTokens.Control - UiTokens.KeyCap) / 2f, a.Key, main, a.Disabled);
            if (a.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }
    }
}
