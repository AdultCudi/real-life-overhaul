using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Utilities app on the kit: an app window with icon tabs across, a statement of the bills with the chosen account
    /// beside it, the internet's places and the plans each can be given, and the broadband company as a hero with its
    /// districts, plans, rivals and customers, or, before it is founded, the pitch to found it. Game-free; the model says
    /// what, this says where. Cost: a layout when the model changes.
    /// </summary>
    public static partial class UtilitiesFace
    {
        public const int AccountsBase = UiDataTable.First;   // its rows answer AccountsBase + UiDataTable.Row
        public const int PlacesBase = 600, PlansBase = 700;
        public const int HeroAction = 800, DistrictAct = 801, CardAction = 802, CardSwitch = 803, BuyAction = 806, BuySwitch = 807;
        public const int SheetClose = 810, SheetAnswer = 811, SheetPick = 812, SheetAction = 813, SheetLess = 814, SheetMore = 815;

        /// <summary>Every command the screen answers, for the checks: the tables' own ranges and the buttons.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int>
            {
                UiAppWindow.NavCommand, HeroAction, DistrictAct, CardAction, CardSwitch, BuyAction, BuySwitch, SheetClose, SheetAnswer, SheetPick, SheetAction, SheetLess,
                SheetMore, UiChart.First + UiChart.Point
            };
            foreach (int first in new[] { AccountsBase, PlacesBase, PlansBase })
            {
                for (int i = 0; i <= UiDataTable.MassAnswer; i++)
                {
                    list.Add(first + i);
                }
            }
            return list.ToArray();
        }

        private const float SideLeast = 360f, SideMost = 464f;   // px: the network page's side column
        private const float DistrictH = UiTokens.S12;            // px: a district's row
        private const float SideRow = UiTokens.S12;              // px: a plan's or a provider's row

        public static void Lay(UiScene s, UtilitiesFaceModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("utilities", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.Globe, m.Nav, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            if (!string.IsNullOrEmpty(m.OffTitle))
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Globe, m.OffTitle, m.OffBody, null, -1, keys);
            }
            else if (m.Tab == 0)
            {
                AccountsPage(s, at, m, x, y, w, bottom, keys);
            }
            else if (m.Tab == 1)
            {
                InternetPage(s, at, m, x, y, w, bottom, keys);
            }
            else
            {
                NetworkPage(s, at, m, x, y, w, bottom, keys);
            }
            if (m.Sheet != null)
            {
                Sheet(s, m.Sheet, frame, keys);
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        private static float Headline(UiScene s, UiAt at, string words, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(words))
            {
                return y;
            }
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w, UiStyle.BodyStrong, UiRole.Text2, words, UiFlags.Truncate);
            return y + UiTokens.Control;
        }

        private static short ClipOf(UiScene s, short list) => list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;

        // ------------------------------------------------------------------------------------------------- Accounts

        private static void AccountsPage(UiScene s, UiAt at, UtilitiesFaceModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Headline(s, at, m.Headline, x, y, w);
            float h = bottom - y;
            bool detail = m.AccountCard != null;
            float tw = detail ? UiDraw.SnapDown(w * 0.62f) : w;
            m.AccountsList = UiDataTable.Lay(s, at.InRegion(1), m.Accounts, x, y, tw, h, keys, AccountsBase);
            m.AccountsClip = ClipOf(s, m.AccountsList);
            if (detail)
            {
                float cx = x + tw + UiTokens.S4;
                Card(s, at.InRegion(2), m.AccountCard, cx, y, x + w - cx, h, CardSwitch, -1, -1, keys, true, true);
            }
        }

        // ------------------------------------------------------------------------------------------------- Internet

        private static void InternetPage(UiScene s, UiAt at, UtilitiesFaceModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Headline(s, at, m.Headline, x, y, w);
            float h = bottom - y;
            if (m.Plans == null)
            {
                m.PlacesList = UiDataTable.Lay(s, at.InRegion(1), m.Places, x, y, w, h, keys, PlacesBase);
                m.PlacesClip = ClipOf(s, m.PlacesList);
                return;
            }
            // The places and, under them, what the chosen one can do; the plans it can be given down the right, every one in sight.
            float lw = UiDraw.SnapDown(w * 0.5f);
            float cardH = m.BuyCard != null ? CardHeight(m.BuyCard, lw, keys, true) : 0f;
            float placesH = Math.Max(UiTokens.S16 * 3f, h - cardH - (cardH > 0f ? UiTokens.S3 : 0f));
            m.PlacesList = UiDataTable.Lay(s, at.InRegion(1), m.Places, x, y, lw, placesH, keys, PlacesBase);
            m.PlacesClip = ClipOf(s, m.PlacesList);
            if (m.BuyCard != null)
            {
                float cy = Math.Max(y + placesH + UiTokens.S3, bottom - cardH);
                Card(s, at.InRegion(3), m.BuyCard, x, cy, lw, bottom - cy, BuySwitch, -1, -1, keys, true, true);
            }
            float rx = x + lw + UiTokens.S4, rw = x + w - rx;
            UiDraw.TextMiddle(s, at, rx, y, UiTokens.Control, rw, UiStyle.SectionHeader, UiRole.Text, m.PlansTitle, UiFlags.Label | UiFlags.Truncate);
            m.PlansList = UiDataTable.Lay(s, at.InRegion(2), m.Plans, rx, y + UiTokens.Control, rw, h - UiTokens.Control, keys, PlansBase);
            m.PlansClip = ClipOf(s, m.PlansList);
        }

        // ------------------------------------------------------------------------------------------------- Your network

        private static void NetworkPage(UiScene s, UiAt at, UtilitiesFaceModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UtilitiesNetworkModel n = m.Network;
            float hh = Hero(s, at, n, x, y, w, keys);
            y += hh + UiTokens.S4;
            float sw = Math.Max(SideLeast, Math.Min(SideMost, UiDraw.SnapDown(w * 0.28f)));
            float lw = w - sw - UiTokens.S4;
            float sx = x + lw + UiTokens.S4;
            if (n.State == 2)
            {
                UiDraw.EmptyState(s, at, x, y, lw, bottom - y, UiIcon.HardDrives, n.EmptyTitle, n.EmptyBody, null, -1, keys);
            }
            else if (n.State == 0)
            {
                // Not founded: the districts in a compact table, and under it what founding gets.
                float rowH = UiTokens.S8 + UiTokens.S1;
                float tableH = UiDraw.Snap(UiTokens.S2 + UiTokens.Control * 2f + UiTokens.S1 + n.Districts.Length * rowH + UiTokens.S2);
                DistrictsCard(s, at.InRegion(1), n, x, y, lw, tableH, rowH, keys);
                float gy = y + tableH + UiTokens.S4;
                if (n.Gets != null && bottom - gy >= UiTokens.S16 * 2f)
                {
                    Card(s, at.InRegion(3), n.Gets, x, gy, lw, UiDraw.SnapDown(bottom - gy), -1, -1, -1, keys, true, true);
                }
            }
            else
            {
                DistrictsCard(s, at.InRegion(1), n, x, y, lw, bottom - y, DistrictH, keys);
            }
            SideColumn(s, at.InRegion(2), n, sx, y, sw, bottom, keys);
        }

        // The hero: the company's tile, its name and chips, its figures, and the buttons that run it.
        private static float Hero(UiScene s, UiAt at, UtilitiesNetworkModel n, float x, float y, float w, IUiKeys keys)
        {
            float tile = UiAppWindow.HeroTileSize, pad = UiTokens.S4;
            float tx = x + pad, textX = tx + tile + UiTokens.S5, right = x + w - pad;
            float bw = 0f;
            for (int i = 0; i < n.Actions.Length; i++)
            {
                bw = Math.Max(bw, UiDraw.ButtonWidth(n.Actions[i].Kind, n.Actions[i].Label, n.Actions[i].Icon, n.Actions[i].Key, keys));
            }
            float buttonsH = n.Actions.Length * UiTokens.Control + Math.Max(0, n.Actions.Length - 1) * UiTokens.S2;
            float figW = 0f;
            var colW = new float[n.FigureLabels.Length];
            for (int i = 0; i < colW.Length; i++)
            {
                colW[i] = UiDraw.Snap(Math.Max(UiTextRules.Width(n.FigureLabels[i], UiStyle.SectionHeader), UiTextRules.Width(n.FigureValues[i], UiStyle.Number)) + UiTokens.S1);
                figW += colW[i] + (i > 0 ? UiTokens.S6 : 0f);
            }
            float buttonsX = n.Actions.Length > 0 ? right - bw : right;
            float textRight = buttonsX - (n.Actions.Length > 0 ? UiTokens.S6 : 0f);
            float figX = textRight - figW;
            bool stacked = figW > 0f && figX - UiTokens.S6 - textX < UiTokens.S16 * 5f;   // too narrow for the words and the figures side by side: the figures take a row of their own
            float textW = (stacked || figW <= 0f ? textRight : figX - UiTokens.S6) - textX;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)), chipH = UiTokens.Badge + UiTokens.S2;
            float lineH = string.IsNullOrEmpty(n.Line) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(n.Line, UiStyle.Body, textW, 3)) + UiTokens.S2;
            float block = nameH + UiTokens.S2 + lineH + chipH;
            float figH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number));
            float hh = UiDraw.Snap(pad * 2f + Math.Max(tile, Math.Max(block, buttonsH)) + (stacked ? UiTokens.S3 + figH : 0f));
            UiDraw.Box(s, at, x, y, w, hh, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float top = y + pad;
            int t = UiDraw.Tile(s, at, tx, top, tile, UiRole.None, false);
            float g = UiTokens.S10;
            UiDraw.Icon(s, at.In(t), tx + (tile - g) / 2f, top + (tile - g) / 2f, g, UiIcon.HardDrives, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float ty = top;
            UiDraw.Text(s, at, textX, ty, textW, UiStyle.Hero, UiRole.Text, n.Name, UiFlags.Truncate);
            ty += nameH + UiTokens.S2;
            if (lineH > 0f)
            {
                UiDraw.Text(s, at, textX, ty, textW, UiStyle.Body, UiRole.Text2, n.Line, UiFlags.Wrap | UiFlags.Label, 3);
                ty += lineH;
            }
            float cx = textX;
            for (int i = 0; i < n.Chips.Length; i++)
            {
                float cw = ChipWidth(n.Chips[i]);
                if (cx + cw > textX + textW)
                {
                    break;
                }
                UiDraw.Chip(s, at, cx, ty, n.Chips[i], n.ChipTones[i], cw);
                cx += cw + UiTokens.S2;
            }
            float fx = stacked ? textX : figX, fy = stacked ? top + Math.Max(tile, Math.Max(block, buttonsH)) + UiTokens.S3 : top;
            for (int i = 0; i < colW.Length && figW > 0f; i++)
            {
                UiDraw.Text(s, at, fx, fy, colW[i], UiStyle.SectionHeader, UiRole.Text2, n.FigureLabels[i], UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, fx, fy + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, colW[i], UiStyle.Number,
                    i < n.FigureInks.Length && n.FigureInks[i] != UiRole.None ? n.FigureInks[i] : UiRole.Text, n.FigureValues[i], UiFlags.Truncate);
                fx += colW[i] + UiTokens.S6;
            }
            float by = top;
            for (int i = 0; i < n.Actions.Length; i++)
            {
                ref UiActionModel a = ref n.Actions[i];
                UiDraw.Button(s, at, buttonsX, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                by += UiTokens.Control + UiTokens.S2;
            }
            return hh;
        }

        // The districts as a table: a colour and the name, the homes the network reaches, the customers, the take-up as a bar, the state, and what to do.
        private static void DistrictsCard(UiScene s, UiAt at, UtilitiesNetworkModel n, float x, float y, float w, float h, float rowH, IUiKeys keys)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float pad = UiTokens.S4, iw = w - pad * 2f, ix = x + pad;
            UiDraw.TextMiddle(s, on, ix, y + UiTokens.S2, UiTokens.Control, iw, UiStyle.SectionHeader, UiRole.Text, n.DistrictsTitle, UiFlags.Label | UiFlags.Truncate);
            float homesW = Math.Max(UiTokens.S16 * 1.5f, UiDraw.SnapDown(iw * 0.11f)), custW = homesW;
            float takeW = Math.Max(UiTokens.S16 * 3f, UiDraw.SnapDown(iw * 0.26f)), stateW = Math.Max(UiTokens.S16 * 1.75f, UiDraw.SnapDown(iw * 0.12f)), actW = UiTokens.S16 * 1.5f;
            float nameW = iw - homesW - custW - takeW - stateW - actW - UiTokens.S4 * 5f;
            if (nameW < UiTokens.S16 * 2.5f)
            {
                takeW = Math.Max(UiTokens.S16 * 2f, takeW - (UiTokens.S16 * 2.5f - nameW));
                nameW = iw - homesW - custW - takeW - stateW - actW - UiTokens.S4 * 5f;
            }
            float c1 = ix, c2 = c1 + nameW + UiTokens.S4, c3 = c2 + homesW + UiTokens.S4, c4 = c3 + custW + UiTokens.S4, c5 = c4 + takeW + UiTokens.S4, c6 = c5 + stateW + UiTokens.S4;
            float hy = y + UiTokens.S2 + UiTokens.Control, headH = UiTokens.Control;
            Head(s, on, n.Heads[0], c1, hy, headH, nameW, false);
            Head(s, on, n.Heads[1], c2, hy, headH, homesW, true);
            Head(s, on, n.Heads[2], c3, hy, headH, custW, true);
            Head(s, on, n.Heads[3], c4, hy, headH, takeW, false);
            Head(s, on, n.Heads[4], c5, hy, headH, stateW, false);
            float ry = hy + headH;
            UiDraw.Box(s, on, x, ry, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            ry += UiTokens.S1;
            int rows = Math.Min(n.Districts.Length, (int)Math.Floor((y + h - ry - (string.IsNullOrEmpty(n.TotalName) ? 0f : rowH)) / rowH));
            for (int i = 0; i < rows; i++)
            {
                DistrictRow(s, on, n.Districts[i], c1, c2, c3, c4, c5, c6, nameW, homesW, custW, takeW, stateW, actW, ry, rowH, keys);
                ry += rowH;
            }
            if (!string.IsNullOrEmpty(n.TotalName))
            {
                UiDraw.Box(s, on, x, ry, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                UiDraw.TextMiddle(s, on, c1, ry, rowH, nameW, UiStyle.BodyStrong, UiRole.Text, n.TotalName, UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, on, c2, ry, rowH, homesW, UiStyle.BodyStrong, UiRole.Text, n.TotalHomes, UiFlags.Right | UiFlags.Truncate);
                UiDraw.TextMiddle(s, on, c3, ry, rowH, custW, UiStyle.BodyStrong, UiRole.Text, n.TotalCustomers, UiFlags.Right | UiFlags.Truncate);
                UiDraw.TextMiddle(s, on, c4, ry, rowH, takeW, UiStyle.BodyStrong, UiRole.Text, n.TotalTakeUp, UiFlags.Right | UiFlags.Truncate);
            }
        }

        private static void Head(UiScene s, UiAt at, string words, float x, float y, float h, float w, bool right)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            float lh = UiTextRules.WrappedHeight(words, UiStyle.SectionHeader, w, 2);
            UiDraw.Text(s, at, x, y + (h - lh) / 2f, w, UiStyle.SectionHeader, UiRole.Muted, words, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle | (right ? UiFlags.Right : UiFlags.None), 2);
        }

        private static void DistrictRow(UiScene s, UiAt at, UtilitiesDistrictRow d, float c1, float c2, float c3, float c4, float c5, float c6, float nameW, float homesW, float custW,
            float takeW, float stateW, float actW, float y, float rowH, IUiKeys keys)
        {
            UiRole hue = (UiRole)((int)UiRole.Chart1 + Math.Max(0, Math.Min(7, d.Hue)));
            float dot = UiTokens.Dot;
            UiDraw.Box(s, at, c1, y + (rowH - dot) / 2f, dot, dot, hue, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
            UiDraw.TextMiddle(s, at, c1 + dot + UiTokens.S3, y, rowH, nameW - dot - UiTokens.S3, UiStyle.Title, UiRole.Text, d.Name, UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, c2, y, rowH, homesW, UiStyle.Body, UiRole.Text2, d.Homes, UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, c3, y, rowH, custW, UiStyle.Body, UiRole.Text, d.Customers, UiFlags.Right | UiFlags.Truncate);
            if (d.Bar && d.TakeUp >= 0)
            {
                float pw = UiDraw.Snap(UiTextRules.Width("100%", UiStyle.Body) + UiTokens.S1);
                float bar = UiDraw.SnapDown(takeW - pw - UiTokens.S3);
                UiDraw.Meter(s, at, c4, y + (rowH - UiTokens.S2) / 2f, bar, UiTokens.S2, d.TakeUp, hue);
                UiDraw.TextMiddle(s, at, c4 + takeW - pw, y, rowH, pw, UiStyle.Body, UiRole.Text, d.TakeUpWords, UiFlags.Right | UiFlags.Truncate);
            }
            else
            {
                UiDraw.TextMiddle(s, at, c4, y, rowH, takeW, UiStyle.Body, UiRole.Text2, d.TakeUpWords, UiFlags.Truncate);
            }
            float chip = Math.Min(UiDraw.SnapDown(stateW), ChipWidth(d.State));
            UiDraw.Chip(s, at, c5, y + UiDraw.SnapDown((rowH - UiTokens.Badge - UiTokens.S1) / 2f), d.State, d.StateTone, chip);
            if (!string.IsNullOrEmpty(d.Action))
            {
                float bw = Math.Min(actW, UiDraw.ButtonWidth(UiButtonKind.Secondary, d.Action, UiIcon.None, UiAction.Count, keys));
                UiDraw.Button(s, at, c6 + actW - bw, y + (rowH - UiTokens.ControlSmall) / 2f, bw, UiTokens.ControlSmall, UiButtonKind.Secondary, d.Action, UiIcon.None, UiAction.Count, keys,
                    DistrictAct, d.ActionDistrict);
            }
        }

        // The plans and the other providers, then the customers' line, or what founding gets.
        private static void SideColumn(UiScene s, UiAt at, UtilitiesNetworkModel n, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float head = UiTokens.S2 + UiTokens.Control, tail = UiTokens.S2;
            float rowH = SideRow;
            float plansH = n.Plans.Length > 0 ? head + n.Plans.Length * rowH + tail : 0f;
            float provH = n.Providers.Length > 0 ? head + n.Providers.Length * rowH + tail : 0f;
            float rest = bottom - y - plansH - provH - UiTokens.S4 * ((plansH > 0f ? 1 : 0) + (provH > 0f ? 1 : 0));
            if (rest < UiTokens.S16 * 3f && provH > 0f)
            {
                rowH = UiTokens.S10;   // tighter rows give the last card the room it needs
                plansH = n.Plans.Length > 0 ? head + n.Plans.Length * rowH + tail : 0f;
                provH = head + n.Providers.Length * rowH + tail;
                rest = bottom - y - plansH - provH - UiTokens.S4 * ((plansH > 0f ? 1 : 0) + 1);
            }
            if (plansH > 0f)
            {
                SideCard(s, at, n.PlansTitle, n.PlansLink, n.Plans, x, y, w, plansH, rowH, false, keys);
                y += plansH + UiTokens.S4;
            }
            if (provH > 0f)
            {
                SideCard(s, at, n.ProvidersTitle, null, n.Providers, x, y, w, provH, rowH, true, keys);
                y += provH + UiTokens.S4;
            }
            float h = bottom - y;
            if (n.Chart != null && h >= UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, n.Chart, x, y, w, UiDraw.SnapDown(h));
            }
        }

        private static void SideCard(UiScene s, UiAt at, string title, string link, UtilitiesSideRow[] rows, float x, float y, float w, float h, float rowH, bool avatars, IUiKeys keys)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float pad = UiTokens.S4, ix = x + pad, iw = w - pad * 2f;
            float lw = 0f;
            if (!string.IsNullOrEmpty(link))
            {
                lw = Math.Min(iw / 2f, UiDraw.ButtonWidth(UiButtonKind.Quiet, link, UiIcon.None, UiAction.Count, keys));
                UiDraw.Button(s, on, ix + iw - lw, y + UiTokens.S2, lw, UiTokens.Control, UiButtonKind.Quiet, link, UiIcon.None, UiAction.Count, keys, HeroAction, 1);
            }
            UiDraw.TextMiddle(s, on, ix, y + UiTokens.S2, UiTokens.Control, iw - lw - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            float ry = y + UiTokens.S2 + UiTokens.Control;
            float face = UiTokens.ControlSmall;
            for (int i = 0; i < rows.Length; i++)
            {
                UtilitiesSideRow r = rows[i];
                float fy = ry + (rowH - face) / 2f;
                if (avatars)
                {
                    UiDraw.Avatar(s, on, ix, fy, face, r.Name, 200f + i * 67f);
                }
                else
                {
                    int tile = UiDraw.Tile(s, on, ix, fy, face, UiRole.None, false);
                    UiDraw.Icon(s, on.In(tile), ix + (face - UiTokens.Icon) / 2f, fy + (face - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                }
                float priceW = Math.Max(UiDraw.Snap(UiTextRules.Width(r.Price, UiStyle.BodyStrong)), UiDraw.Snap(UiTextRules.Width(r.Per, UiStyle.Small))) + UiTokens.S1;
                priceW = Math.Min(priceW, UiDraw.SnapDown(iw * 0.34f));
                float tx = ix + face + UiTokens.S3, tw = iw - face - UiTokens.S3 - priceW - UiTokens.S3;
                float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small), ph = UiTextRules.LineHeight(UiStyle.BodyStrong);
                float top = ry + (rowH - lh - sh) / 2f, ptop = ry + (rowH - ph - sh) / 2f;
                UiDraw.Text(s, on, tx, top, tw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, on, tx, top + lh, tw, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, on, ix + iw - priceW, ptop, priceW, UiStyle.BodyStrong, UiRole.Text, r.Price, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, on, ix + iw - priceW, ptop + ph, priceW, UiStyle.Small, UiRole.Text2, r.Per, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
                ry += rowH;
            }
        }
    }
}
