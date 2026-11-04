using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float MarkSize = 72f, BadgeMark = 24f, WaitingRow = 56f, DoneRow = 32f, TableRow = 40f, PersonRow = 56f;

        // ------------------------------------------------------------------------------------------------- the overview

        private static void OverviewPage(UiScene s, UiAt at, CompanyFacts f, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            CompanyOverviewFacts o = f.Overview;
            if (v.Sub[CompanyView.Overview] == CompanyView.SubBusiness && f.Business != null)
            {
                CardPage(s, at, f.Business, v, w, lay, x, y, cw, bottom, keys);
                return;
            }
            if (o.PartOff)
            {
                OffPage(s, at, w, null, null, x, y, cw, bottom, keys);
                return;
            }
            if (o.Shop != null)
            {
                OneShop(s, at, f, o, v, w, lay, x, y, cw, bottom, keys);
                return;
            }
            float head = Head(s, at.InRegion(RegionMain), f, o, v, w, lay, x, y, cw, keys);
            float top = y + head + UiTokens.S5;
            float gap = UiTokens.S6;
            float lw = UiDraw.SnapDown((cw - gap) * 0.56f), rw = cw - gap - lw;
            // With nothing waiting for the owner the left column is short, and the managers today stand under what was done for them, so that the two columns end near each other.
            bool managersLeft = o.Lead == null && o.Waiting.Length == 0 && o.Managers.Length > 0;
            Left(s, at.InRegion(RegionMain), f, o, v, w, lay, x, top, lw, bottom, keys, managersLeft);
            Right(s, at.InRegion(RegionSide), f, o, v, w, lay, x + lw + gap, top, rw, bottom, keys, managersLeft);
        }

        // A company with one business: what waits for the owner, as cards, and the next step at the left; the business as a panel at the right, in place of a table of one row.
        private static void OneShop(UiScene s, UiAt at, CompanyFacts f, CompanyOverviewFacts o, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom,
            IUiKeys keys)
        {
            UiAt main = at.InRegion(RegionMain);
            float head = Head(s, main, f, o, v, w, lay, x, y, cw, keys);
            float top = y + head + UiTokens.S5, gap = UiTokens.S6;
            float lw = UiDraw.SnapDown((cw - gap) * 0.5f), rw = cw - gap - lw, cy = top;
            if (o.Lead != null)
            {
                cy += LeadCard(s, main, o.Lead, v, w, lay, x, cy, lw, keys) + UiTokens.S5;
            }
            if (o.Waiting.Length > 0)
            {
                cy = Section(s, main, lay, x, cy, lw, o.Lead != null ? w.T("reallife_co_also_waiting", "Also waiting") : w.T("reallife_co_waiting_for_you", "Waiting for you"), o.WaitingCount);
                for (int i = 0; i < o.Waiting.Length && cy < bottom - UiTokens.Control; i++)
                {
                    cy += WaitingCard(s, main, lay, v, ref o.Waiting[i], x, cy, lw, keys) + UiTokens.S3;
                }
                cy += UiTokens.S2;
            }
            if (o.Next.Name != null && cy < bottom - UiTokens.Control)
            {
                cy = Section(s, main, lay, x, cy, lw, w.T("reallife_co_next_step", "Next step"));
                cy += WaitingCard(s, main, lay, v, ref o.Next, x, cy, lw, keys) + UiTokens.S4;
            }
            if (o.Done.Length > 0 && bottom - cy >= UiTokens.PairRow + UiTokens.S1 + DoneRow)
            {
                CompanyButton log = new CompanyButton { Label = w.T("reallife_co_managers_log", "Managers' log"), Act = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers) };
                cy = Section(s, main, lay, x, cy, lw, w.T("reallife_co_done_today", "Done for you today"), null, log);
                int fit = (int)Math.Max(1f, Math.Floor((bottom - cy) / DoneRow));
                for (int i = 0; i < Math.Min(o.Done.Length, fit) && cy + DoneRow <= bottom; i++)
                {
                    cy += DoneLine(s, main, lay, ref o.Done[i], x, cy, lw, DoneRow, bottom);
                }
            }
            // What the business's panel holds past its break (the licences and the cover held) stands at the left under what waits, where the column would end short.
            CompanyBlock[] beside = AfterBreak(o.Shop.Blocks, out CompanyBlock[] before);
            if (beside.Length > 0)
            {
                cy += UiTokens.S2;
                for (int i = 0; i < beside.Length && cy < bottom; i++)
                {
                    cy = Block(s, main, lay, v, w, beside[i], x, cy, lw, bottom, keys);
                }
            }
            float rx = x + lw + gap;
            float toolsH = o.Tools != null && o.Tools.Length > 0 ? UiTokens.Control + UiTokens.S4 : 0f;
            CompanyCard panel = beside.Length > 0 ? WithBlocks(o.Shop, before) : o.Shop;
            CardPage(s, at.InRegion(RegionSide), panel, v, w, lay, rx, top, rw, bottom - toolsH, keys);
            ToolsRow(s, at.InRegion(RegionSide), lay, v, o, rx, rw, bottom, keys);
        }

        // The blocks of a card before its first break, and those after it.
        private static CompanyBlock[] AfterBreak(CompanyBlock[] blocks, out CompanyBlock[] before)
        {
            int at = -1;
            for (int i = 0; i < blocks.Length && at < 0; i++)
            {
                at = blocks[i].Kind == CompanyBlockKind.Break ? i : -1;
            }
            before = at < 0 ? blocks : new CompanyBlock[at];
            if (at < 0)
            {
                return new CompanyBlock[0];
            }
            Array.Copy(blocks, before, at);
            var after = new CompanyBlock[blocks.Length - at - 1];
            Array.Copy(blocks, at + 1, after, 0, after.Length);
            return after;
        }

        private static CompanyCard WithBlocks(CompanyCard c, CompanyBlock[] blocks) => new CompanyCard
        {
            Kicker = c.Kicker, Title = c.Title, Sub = c.Sub, Icon = c.Icon, Tint = c.Tint, Face = c.Face, FaceName = c.FaceName, Hue = c.Hue, Ring = c.Ring, Chips = c.Chips, ChipTones = c.ChipTones,
            FactLabels = c.FactLabels, FactValues = c.FactValues, Blocks = blocks, Back = c.Back, Empty = c.Empty
        };

        /// <summary>
        /// Moves the sections with these titles (each with the block after it) to the end of a card's blocks, behind a break: the one-business panel keeps its figures, campaign and actions at
        /// the right and puts the licences and the cover at the left, under what waits.
        /// </summary>
        internal static void AsideSections(System.Collections.Generic.List<CompanyBlock> blocks, params string[] titles)
        {
            var moved = new System.Collections.Generic.List<CompanyBlock>(4);
            for (int i = blocks.Count - 2; i >= 0; i--)
            {
                if (blocks[i].Kind == CompanyBlockKind.Section && Array.IndexOf(titles, blocks[i].Text) >= 0)
                {
                    moved.Insert(0, blocks[i + 1]);
                    moved.Insert(0, blocks[i]);
                    blocks.RemoveRange(i, 2);
                }
            }
            if (moved.Count > 0)
            {
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
                blocks.AddRange(moved);
            }
        }

        // One thing that waits for the owner, as a card of its own: who or what, the line under it and the button that opens it. A page of few things is made of cards, not rows.
        private static float WaitingCard(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, ref CompanyWaiting row, float x, float y, float w, IUiKeys keys)
        {
            float pad = UiTokens.S4, size = UiTokens.AvatarRow;
            CompanyButton b = new CompanyButton { Label = row.OpenLabel, Act = row.Open, Kind = UiButtonKind.Secondary, Icon = UiIcon.Next };
            float bw = Math.Min(UiDraw.SnapDown(w * 0.38f), UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys));
            float tx = x + pad + size + UiTokens.S3, tw = Math.Max(0f, w - pad * 2f - size - UiTokens.S3 - bw - UiTokens.S3);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), lh = string.IsNullOrEmpty(row.Line) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(row.Line, UiStyle.Small, tw, 3));
            float h = Math.Max(size, th + (lh > 0f ? UiTokens.S1 + lh : 0f)) + pad * 2f;
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = at.In(box);
            if (row.Icon != UiIcon.None)
            {
                int tile = UiDraw.Tile(s, inside, x + pad, y + Mid(h, size), size, UiRole.None, false);
                float g = UiTokens.S6;
                UiDraw.Icon(s, inside.In(tile), x + pad + (size - g) / 2f, y + Mid(h, size) + (size - g) / 2f, g, row.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                UiDraw.Portrait(s, inside, x + pad, y + Mid(h, size), size, row.Name, row.Hue, row.Ring);
            }
            float top = y + UiDraw.SnapDown((h - th - (lh > 0f ? UiTokens.S1 + lh : 0f)) / 2f);
            UiDraw.TextMiddle(s, inside, tx, top, th, tw, UiStyle.Title, UiRole.Text, row.Name ?? string.Empty, UiFlags.Truncate);
            if (lh > 0f)
            {
                UiDraw.Text(s, inside, tx, top + th + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, row.Line, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            Button(s, inside, lay, v, ref b, x + w - pad - bw, y + Mid(h, UiTokens.Control), bw, keys);
            return h;
        }

        // The company's head: its mark, name and what it owns as badges, and the day's result as the key figure with its spark.
        private static float Head(UiScene s, UiAt at, CompanyFacts f, CompanyOverviewFacts o, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, IUiKeys keys)
        {
            Mark(s, at, x, y, MarkSize, f.Mark, UiStyle.Hero);
            float rw = Math.Min(UiDraw.SnapDown(cw * 0.3f), 360f);
            float tx = x + MarkSize + UiTokens.S4, tw = Math.Max(0f, cw - MarkSize - UiTokens.S4 - rw - UiTokens.S6);
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S1;
            UiDraw.TextMiddle(s, at, tx, y, nameH, tw, UiStyle.Hero, UiRole.Text, f.Name ?? string.Empty, UiFlags.Truncate);
            float ly = y + nameH;
            UiDraw.TextMiddle(s, at, tx, ly, UiTokens.PairRow, tw, UiStyle.Body, UiRole.Text2, o.Line ?? string.Empty, UiFlags.Truncate);
            float by = ly + UiTokens.PairRow + UiTokens.S2;
            float bx = tx, bottom = by + UiTokens.Chip;
            for (int i = 0; i < o.Businesses.Length; i++)
            {
                ref CompanyBusiness b = ref o.Businesses[i];
                float nw = UiDraw.Snap(UiTextRules.Width(b.Name ?? string.Empty, UiStyle.SmallStrong) + UiTokens.S1);
                float bw = UiTokens.S2 + BadgeMark - UiTokens.S2 + UiTokens.S2 + nw + UiTokens.S3;
                bw = Math.Min(bw, UiDraw.SnapDown(tw * 0.5f));
                if (bx + bw > tx + tw)
                {
                    if (bx == tx)
                    {
                        break;
                    }
                    int rest = o.Businesses.Length - i;
                    UiDraw.TextMiddle(s, at, bx, by, UiTokens.Chip, Math.Max(0f, tx + tw - bx), UiStyle.Small, UiRole.Muted, "+" + rest, UiFlags.Truncate);
                    break;
                }
                int face = UiDraw.Box(s, at, bx, by, bw, UiTokens.Chip, UiRole.Pill, UiRole.None, UiTokens.Chip / 2f, UiFlags.Circle);
                UiAt inside = at.In(face);
                float mk = UiTokens.Chip - UiTokens.S1;
                Mark(s, inside, bx + UiTokens.S1 / 2f, by + UiTokens.S1 / 2f, mk, b.Mark, UiStyle.Badge);
                UiDraw.TextMiddle(s, inside, bx + mk + UiTokens.S2, by, UiTokens.Chip, bw - mk - UiTokens.S2 - UiTokens.S3, UiStyle.SmallStrong, UiRole.Text, b.Name ?? string.Empty, UiFlags.Truncate);
                UiDraw.Node(s, at, bx, by, bw, UiTokens.Chip, Badge, i, face);
                bx += bw + UiTokens.S2;
            }
            float height = Math.Max(MarkSize, bottom - y);
            // The day's result: its label, the figure in its tone, and the line of the days that exist beside it.
            float rx = x + cw - rw;
            UiDraw.TextMiddle(s, at, rx, y, UiTokens.PairRow, rw, UiStyle.SectionHeader, UiRole.Muted, o.ResultLabel ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            float vy = y + UiTokens.PairRow + UiTokens.S1;
            bool has = !string.IsNullOrEmpty(o.Result);
            float figH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)) + UiTokens.S1;
            string shown = has ? o.Result : w.T("reallife_co_not_yet", "Not yet");
            float fw = Math.Min(UiDraw.Snap(UiTextRules.Width(shown, UiStyle.Number) + UiTokens.S2), UiDraw.SnapDown(rw * 0.55f));
            UiDraw.TextMiddle(s, at, rx, vy, figH, fw, UiStyle.Number, has ? ToneInk(o.ResultTone) : UiRole.Text2, shown, UiFlags.Truncate);
            if (has && UiDraw.Drawn(o.Spark))
            {
                UiDraw.Spark(s, at, rx + fw + UiTokens.S4, vy, rw - fw - UiTokens.S4, figH, o.Spark, o.ResultTone < 0 ? UiRole.Danger : UiRole.Ok);
            }
            else if (!string.IsNullOrEmpty(o.ResultNote))
            {
                UiDraw.TextMiddle(s, at, rx + fw + UiTokens.S3, vy, figH, Math.Max(0f, rw - fw - UiTokens.S3), UiStyle.Small, UiRole.Muted, o.ResultNote, UiFlags.Truncate);
            }
            return height;
        }

        // The left column: what needs the owner (the Lead, then the rest that waits) and what was done for them today.
        private static void Left(UiScene s, UiAt at, CompanyFacts f, CompanyOverviewFacts o, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float lw, float bottom, IUiKeys keys,
            bool managersLeft = false)
        {
            float cy = y;
            bool anything = o.Lead != null || o.Waiting.Length > 0;
            if (o.Lead != null)
            {
                cy += LeadCard(s, at, o.Lead, v, w, lay, x, cy, lw, keys) + UiTokens.S5;
            }
            else if (!anything)
            {
                float h = UiTokens.IconTile + UiTokens.S4 * 2f;
                UiAt band = Band(s, at, x, cy, lw, h);
                UiDraw.Icon(s, band, x + UiTokens.S4, cy + UiTokens.S4, UiTokens.IconTile, UiIcon.Check, UiRole.None, UiFlags.Middle, true);
                float tx = x + UiTokens.S4 + UiTokens.IconTile + UiTokens.S4, tw = lw - UiTokens.S4 * 2f - UiTokens.IconTile - UiTokens.S4;
                UiDraw.TextMiddle(s, band, tx, cy + UiTokens.S4, UiTokens.IconTile, tw, UiStyle.Title, UiRole.Text, w.T("reallife_co_quiet_title", "Nothing is waiting for you"),
                    UiFlags.Label | UiFlags.Truncate);
                cy += h + UiTokens.S5;
            }
            int doneWant = o.Done.Length;
            // What waits for the owner has the room first; what was done for them comes after it, as many rows as are left, from a header and one row.
            float doneNeed = (doneWant > 0 || !string.IsNullOrEmpty(o.DoneNote) ? UiTokens.PairRow + UiTokens.S1 + DoneRow : 0f);
            if (o.Waiting.Length > 0)
            {
                cy = Section(s, at, lay, x, cy, lw, w.T("reallife_co_also_waiting", "Also waiting"), o.WaitingCount);
                int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - doneNeed - UiTokens.S5) / WaitingRow));
                int rows = Math.Min(o.Waiting.Length, fit);
                for (int i = 0; i < rows; i++)
                {
                    cy += WaitingLine(s, at, lay, ref o.Waiting[i], x, cy, lw, keys);
                }
                if (rows < o.Waiting.Length && cy + UiTokens.PairRow <= bottom)
                {
                    // What waits and has no room is counted, so that the list never says fewer than it holds; it comes up as the ones above are answered.
                    UiDraw.TextMiddle(s, at, x + UiTokens.AvatarRow + UiTokens.S3, cy, UiTokens.PairRow, lw - UiTokens.AvatarRow - UiTokens.S3, UiStyle.Small, UiRole.Muted,
                        w.F("reallife_co_and_more", "and {count} more", "count", (o.Waiting.Length - rows).ToString()), UiFlags.Truncate);
                    cy += UiTokens.PairRow;
                }
                cy += UiTokens.S4;
            }
            if (doneNeed > 0f && bottom - cy >= UiTokens.PairRow + UiTokens.S1 + DoneRow)
            {
                CompanyButton log = new CompanyButton { Label = w.T("reallife_co_managers_log", "Managers' log"), Act = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers) };
                cy = Section(s, at, lay, x, cy, lw, w.T("reallife_co_done_today", "Done for you today"), null, log);
                if (doneWant == 0)
                {
                    cy = Sentence(s, at, o.DoneNote, x, cy, lw, UiRole.Muted, 2);
                }
                else
                {
                    float keep = managersLeft ? UiTokens.S4 + UiTokens.PairRow + UiTokens.S1 + PersonRow * Math.Min(3, o.Managers.Length) : 0f;   // the room the managers keep under the rows
                    int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - keep) / DoneRow));
                    int rows = Math.Min(doneWant, fit);
                    for (int i = 0; i < rows && cy + DoneRow <= bottom - keep; i++)
                    {
                        cy += DoneLine(s, at, lay, ref o.Done[i], x, cy, lw, DoneRow, bottom - keep);
                    }
                }
            }
            if (managersLeft && bottom - cy >= UiTokens.S4 + UiTokens.PairRow + PersonRow)
            {
                ManagersToday(s, at, lay, o, w, x, cy + UiTokens.S4, lw, bottom, bottom);
            }
        }

        // The managers today: who, where, and what they decided and asked; or, with none, the sentence and the link to Managers. Draws nothing below the limit.
        private static float ManagersToday(UiScene s, UiAt at, CompanyLayout lay, CompanyOverviewFacts o, CompanyWords w, float x, float y, float rw, float bottom, float limit)
        {
            float cy = y;
            CompanyButton all = new CompanyButton { Label = w.F("reallife_co_all_count", "All {count}", "count", o.Managers.Length.ToString()), Act = CompanyAct.Of(CompanyDo.Go, null,
                CompanyView.Managers) };
            cy = Section(s, at, lay, x, cy, rw, w.T("reallife_co_managers_today", "Managers today"), null, o.Managers.Length > 0 ? all : (CompanyButton?)null);
            if (o.Managers.Length == 0)
            {
                CompanyButton open = new CompanyButton { Label = w.T("reallife_co_open_managers", "Open Managers"), Act = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers) };
                cy = Sentence(s, at, w.T("reallife_co_no_managers", "No manager yet. A manager decides what you hand over, and asks you what is past their limit."), x, cy, rw, UiRole.Text2, 3);
                if (bottom - cy >= UiTokens.Control)
                {
                    cy += LinkRow(s, at, lay, ref open, x, cy, rw);
                }
                return cy;
            }
            int rows = (int)Math.Max(1f, Math.Floor((limit - cy) / PersonRow));
            if (rows < o.Managers.Length && rows > 1 && (limit - cy) - rows * PersonRow < UiTokens.PairRow)
            {
                rows--;   // the room for the count of the rest
            }
            for (int i = 0; i < Math.Min(o.Managers.Length, rows); i++)
            {
                cy += ManagerLine(s, at, lay, ref o.Managers[i], x, cy, rw);
            }
            if (rows < o.Managers.Length && limit - cy >= UiTokens.PairRow)
            {
                // The managers that have no room are counted, so that "All N" never disagrees with the rows.
                UiDraw.TextMiddle(s, at, x + UiTokens.AvatarRow + UiTokens.S3, cy, UiTokens.PairRow, rw - UiTokens.AvatarRow - UiTokens.S3, UiStyle.Small, UiRole.Muted,
                    w.F("reallife_co_and_more", "and {count} more", "count", (o.Managers.Length - rows).ToString()), UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            return cy;
        }

        // A row of "also waiting": the one who asks in their ring, who and what, and the link that opens it.
        private static float WaitingLine(UiScene s, UiAt at, CompanyLayout lay, ref CompanyWaiting row, float x, float y, float w, IUiKeys keys)
        {
            int index = lay.Add(new CompanyButton { Label = row.OpenLabel, Act = row.Open, Kind = UiButtonKind.Quiet });
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, WaitingRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float size = UiTokens.AvatarRow;
            if (row.Icon != UiIcon.None)
            {
                int tile = UiDraw.Tile(s, inside, x, y + (WaitingRow - size) / 2f, size, UiRole.None, false);
                float g = UiTokens.S6;
                UiDraw.Icon(s, inside.In(tile), x + (size - g) / 2f, y + (WaitingRow - g) / 2f, g, row.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                UiDraw.Portrait(s, inside, x, y + (WaitingRow - size) / 2f, size, row.Name, row.Hue, row.Ring);
            }
            float openW = Math.Min(UiDraw.SnapDown(w * 0.2f), UiDraw.Snap(UiTextRules.Width(row.OpenLabel ?? string.Empty, UiStyle.BodyStrong) + UiTokens.IconSmall + UiTokens.S3));
            float tx = x + size + UiTokens.S3, tw = w - size - UiTokens.S3 - openW - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((WaitingRow - th - sh) / 2f);
            UiDraw.TextMiddle(s, inside, tx, top, th, tw, UiStyle.Title, UiRole.Text, row.Name ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, tx, top + th, sh, tw, UiStyle.Small, UiRole.Text2, row.Line ?? string.Empty, UiFlags.Truncate);
            string open = UiTextRules.Cut(row.OpenLabel ?? string.Empty, UiStyle.BodyStrong, openW - UiTokens.IconSmall - UiTokens.S1);
            float ow = UiTextRules.Width(open, UiStyle.BodyStrong);
            UiDraw.TextMiddle(s, inside, x + w - UiTokens.IconSmall - UiDraw.LinkGap - ow - UiTokens.S1, y, WaitingRow, ow + UiTokens.S1, UiStyle.BodyStrong, UiRole.AccentText, open, UiFlags.Right);
            UiDraw.Icon(s, inside, x + w - UiTokens.IconSmall, y + (WaitingRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText,
                UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, WaitingRow, Act, index, face);
            return WaitingRow;
        }

        // A row of "done for you": when, what the manager did, what it cost or saved.
        internal static float DoneLine(UiScene s, UiAt at, CompanyLayout lay, ref CompanyDone row, float x, float y, float w, float h, float limit = -1f)
        {
            // The words have the width left by the time and the money; one that does not fit one line takes two where the column has the room (limit: the y it may not pass).
            float whenW = Math.Min(UiDraw.SnapDown(w * 0.2f), UiDraw.Snap(UiTextRules.Width("Day 000 00:00", UiStyle.Small) + UiTokens.S2));
            float mw = string.IsNullOrEmpty(row.Money) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.25f), UiDraw.Snap(UiTextRules.Width(row.Money, UiStyle.BodyStrong) + UiTokens.S1));
            float textW = w - whenW - UiTokens.S2 - mw - UiTokens.S3;
            string text = row.Text ?? string.Empty;
            float oneLine = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)), wrapped = 0f;
            if (limit > 0f && UiTextRules.Width(text, UiStyle.Body) > textW)
            {
                float two = UiDraw.Snap(UiTextRules.WrappedHeight(text, UiStyle.Body, textW, 2));
                if (two > oneLine + 1f && y + Math.Max(h, two + UiTokens.S3) <= limit)
                {
                    wrapped = two;
                    h = Math.Max(h, two + UiTokens.S3);
                }
            }
            bool press = row.Open.Do != CompanyDo.None;
            int face = -1;
            UiAt inside = at;
            if (press)
            {
                int index = lay.Add(new CompanyButton { Label = row.Text, Act = row.Open, Kind = UiButtonKind.Quiet });
                face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                inside = at.In(face);
                UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, Act, index, face);
            }
            UiDraw.TextMiddle(s, inside, x, y, h, whenW, UiStyle.Small, UiRole.Muted, row.When ?? string.Empty, UiFlags.Truncate);
            if (wrapped > 0f)
            {
                UiDraw.Text(s, inside, x + whenW + UiTokens.S2, y + UiDraw.SnapDown((h - wrapped) / 2f), textW, UiStyle.Body, UiRole.Text, text, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
            else
            {
                UiDraw.TextMiddle(s, inside, x + whenW + UiTokens.S2, y, h, textW, UiStyle.Body, UiRole.Text, text, UiFlags.Truncate);
            }
            if (mw > 0f)
            {
                UiDraw.TextMiddle(s, inside, x + w - mw, y, h, mw, UiStyle.BodyStrong, ToneInk(row.Tone), row.Money, UiFlags.Right | UiFlags.Truncate);
            }
            return h;
        }

        // The right column: the day by business, where the costs went, the managers today.
        private static void Right(UiScene s, UiAt at, CompanyFacts f, CompanyOverviewFacts o, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float rw, float bottom, IUiKeys keys,
            bool managersLeft = false)
        {
            float cy = y;
            // Columns: the business, then three figures as wide as their words. The section's own label heads the first, so no label stands over another.
            float money = 0f;
            for (int i = 0; i < o.Businesses.Length; i++)
            {
                money = Math.Max(money, Math.Max(UiTextRules.Width(o.Businesses[i].Takings ?? string.Empty, UiStyle.Body), Math.Max(UiTextRules.Width(o.Businesses[i].Costs ?? string.Empty,
                    UiStyle.Body),
                    UiTextRules.Width(o.Businesses[i].Result ?? string.Empty, UiStyle.BodyStrong))));
            }
            money = Math.Max(money, Math.Max(UiTextRules.Width(o.TotalTakings ?? string.Empty, UiStyle.BodyStrong), Math.Max(UiTextRules.Width(o.TotalCosts ?? string.Empty, UiStyle.BodyStrong),
                UiTextRules.Width(o.TotalResult ?? string.Empty, UiStyle.BodyStrong))));
            string c1 = w.T("reallife_co_col_takings", "Takings"), c2 = w.T("reallife_co_col_costs", "Costs"), c3 = w.T("reallife_co_col_result", "Result");
            float heads = Math.Max(UiTextRules.Width(c1, UiStyle.SectionHeader), Math.Max(UiTextRules.Width(c2, UiStyle.SectionHeader), UiTextRules.Width(c3, UiStyle.SectionHeader))) * 1.2f;
            float colW = UiDraw.Snap(Math.Max(money, heads) + UiTokens.S3);
            float nameW = rw - colW * 3f - UiTokens.S3;
            if (nameW < rw * 0.3f)
            {
                colW = UiDraw.SnapDown((rw * 0.7f - UiTokens.S3) / 3f);
                nameW = rw - colW * 3f - UiTokens.S3;
            }
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, nameW, UiStyle.SectionHeader, UiRole.Text2, w.T("reallife_co_the_day", "The day"), UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + nameW + UiTokens.S3, cy, UiTokens.PairRow, colW, UiStyle.SectionHeader, UiRole.Muted, c1, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + nameW + UiTokens.S3 + colW, cy, UiTokens.PairRow, colW, UiStyle.SectionHeader, UiRole.Muted, c2, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + nameW + UiTokens.S3 + colW * 2f, cy, UiTokens.PairRow, colW, UiStyle.SectionHeader, UiRole.Muted, c3, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            cy += UiTokens.PairRow + UiTokens.S1;
            if (o.Districts != null && o.Districts.Options.Length > 2)
            {
                cy = FilterBars(s, at, new[] { o.Districts }, o.DistrictFilter, x, cy, rw);   // the old table's district filter
            }
            float below = UiTokens.PairRow * 3f + UiTokens.S6 * 2f + (o.CostLabels.Length + 1) * UiTokens.PairRow + (managersLeft ? 0f : o.Managers.Length > 0
                ? PersonRow * Math.Min(3, o.Managers.Length) : UiTokens.Control);
            int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - TableRow - below) / TableRow));
            int shown = Math.Min(o.Businesses.Length, fit);
            for (int i = 0; i < shown; i++)
            {
                ref CompanyBusiness b = ref o.Businesses[i];
                int face = UiDraw.Box(s, at, x - UiTokens.S2, cy, rw + UiTokens.S4, TableRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(face);
                Mark(s, inside, x, cy + (TableRow - BadgeMark) / 2f, BadgeMark, b.Mark, UiStyle.Badge);
                UiDraw.TextMiddle(s, inside, x + BadgeMark + UiTokens.S3, cy, TableRow, nameW - BadgeMark - UiTokens.S3, UiStyle.Body, UiRole.Text, b.Name ?? string.Empty, UiFlags.Truncate);
                string nobooks = w.T("reallife_co_no_books", "no books");
                if (b.HasBooks)
                {
                    UiDraw.TextMiddle(s, inside, x + nameW + UiTokens.S3, cy, TableRow, colW, UiStyle.Body, UiRole.Text, b.Takings ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, inside, x + nameW + UiTokens.S3 + colW, cy, TableRow, colW, UiStyle.Body, UiRole.Text, b.Costs ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, inside, x + nameW + UiTokens.S3 + colW * 2f, cy, TableRow, colW, UiStyle.BodyStrong, ToneInk(b.ResultTone), b.Result ?? string.Empty,
                        UiFlags.Right | UiFlags.Truncate);
                }
                else
                {
                    UiDraw.TextMiddle(s, inside, x + nameW + UiTokens.S3, cy, TableRow, colW * 3f, UiStyle.Body, UiRole.Muted, nobooks, UiFlags.Right | UiFlags.Truncate);
                }
                UiDraw.Node(s, at, x - UiTokens.S2, cy, rw + UiTokens.S4, TableRow, Badge, i, face);
                cy += TableRow;
            }
            if (shown < o.Businesses.Length)
            {
                UiDraw.TextMiddle(s, at, x + BadgeMark + UiTokens.S3, cy, UiTokens.PairRow, rw, UiStyle.Small, UiRole.Muted, w.F("reallife_co_and_more", "and {count} more", "count",
                    (o.Businesses.Length - shown).ToString()), UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            if (!string.IsNullOrEmpty(o.TotalResult))
            {
                Pair(s, at, x, cy, TableRow, nameW, UiStyle.BodyStrong, UiRole.Text, string.IsNullOrEmpty(o.TotalLabel) ? w.T("reallife_co_all_businesses", "All businesses") : o.TotalLabel,
                    UiStyle.Body, UiRole.Text, null);
                UiDraw.TextMiddle(s, at, x + nameW + UiTokens.S3, cy, TableRow, colW, UiStyle.BodyStrong, UiRole.Text, o.TotalTakings ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + nameW + UiTokens.S3 + colW, cy, TableRow, colW, UiStyle.BodyStrong, UiRole.Text, o.TotalCosts ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + nameW + UiTokens.S3 + colW * 2f, cy, TableRow, colW, UiStyle.BodyStrong, ToneInk(o.TotalTone), o.TotalResult, UiFlags.Right | UiFlags.Truncate);
                cy += TableRow;
            }
            cy += UiTokens.S4;
            // Where the costs went.
            if (o.CostLabels.Length > 0 && bottom - cy >= UiTokens.PairRow * 3f)
            {
                cy = Section(s, at, lay, x, cy, rw, w.T("reallife_co_costs_went", "Where the costs went"));
                for (int i = 0; i < o.CostLabels.Length && cy + UiTokens.PairRow <= bottom; i++)
                {
                    cy += UiDraw.InfoRow(s, at, x, cy, rw, o.CostLabels[i], o.CostValues[i]) + UiTokens.S1;
                }
                if (!string.IsNullOrEmpty(o.CostTotal) && cy + UiTokens.PairRow <= bottom)
                {
                    Pair(s, at, x, cy, UiTokens.PairRow, rw, UiStyle.BodyStrong, UiRole.Text, w.T("reallife_co_costs_total", "Costs"), UiStyle.BodyStrong, UiRole.Text, o.CostTotal);
                    cy += UiTokens.PairRow + UiTokens.S1;
                }
                cy += UiTokens.S4;
            }
            // The managers today.
            float toolsH = o.Tools != null && o.Tools.Length > 0 ? UiTokens.Control + UiTokens.S4 : 0f;   // the tools row has the foot of the column
            if (!managersLeft && bottom - toolsH - cy >= UiTokens.PairRow + PersonRow)
            {
                cy = ManagersToday(s, at, lay, o, w, x, cy, rw, bottom, bottom - toolsH - (string.IsNullOrEmpty(o.Note) ? 0f : UiTokens.S12));
            }
            if (!string.IsNullOrEmpty(o.Note) && bottom - toolsH - cy >= UiTokens.S10)
            {
                cy = Sentence(s, at, o.Note, x, cy + UiTokens.S2, rw, UiRole.Text2, 3, UiStyle.Small);
            }
            // The tools stand under what the column holds; they take the foot of the column only when the column is full.
            ToolsRow(s, at, lay, v, o, x, rw, Math.Min(bottom, cy + UiTokens.S6 + UiTokens.Control), keys);
        }

        private static float ManagerLine(UiScene s, UiAt at, CompanyLayout lay, ref CompanyManagerToday m, float x, float y, float w)
        {
            int index = lay.Add(new CompanyButton { Label = m.Name, Act = m.Open, Kind = UiButtonKind.Quiet });
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, PersonRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float size = UiTokens.AvatarRow - UiTokens.S1 * 2f;
            UiDraw.Portrait(s, inside, x, y + (PersonRow - size) / 2f, size, m.Name, m.Hue, m.Ring);
            float fw = Math.Min(UiDraw.SnapDown(w * 0.45f), UiDraw.Snap(UiTextRules.Width(m.Figures ?? string.Empty, UiStyle.Small) + UiTokens.S1));
            float tx = x + size + UiTokens.S3, tw = w - size - UiTokens.S3 - fw - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((PersonRow - th - sh) / 2f);
            UiDraw.TextMiddle(s, inside, tx, top, th, tw, UiStyle.Title, UiRole.Text, m.Name ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, tx, top + th, sh, tw, UiStyle.Small, UiRole.Text2, m.Place ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, x + w - fw, y, PersonRow, fw, UiStyle.Small, UiRole.Text2, m.Figures ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, PersonRow, Act, index, face);
            return PersonRow;
        }

        // What else the overview has to hand: cleaning, the player's own bank, the company's line in Stocks, each only where it is there.
        private static void ToolsRow(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyOverviewFacts o, float x, float rw, float bottom, IUiKeys keys)
        {
            if (o.Tools == null || o.Tools.Length == 0)
            {
                return;
            }
            Buttons(s, at, lay, v, o.Tools, x, bottom - UiTokens.Control, rw, keys, true);
        }
    }
}
