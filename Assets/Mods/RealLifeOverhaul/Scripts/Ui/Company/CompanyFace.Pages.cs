using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        public const int StepLess = 7420, StepMore = 7421, StepType = 7422;   // a stepper's minus and plus, and its typed figure: the argument is the index of its first action

        private const float ListRow = 56f, ListGap = 4f, SwitchRow = 56f;

        // ------------------------------------------------------------------------------------------------- a row of a list or a card

        // A row: its face or tile, two lines, a figure at the right with a note under it, a chip, a meter. Returns nothing; the caller's node makes it a target.
        private static void RowFace(UiScene s, UiAt inside, float x, float y, float w, float h, string title, string sub, string figure, string note, string chip, UiRole chipTone,
            UiRole figureTone, UiIcon icon, UiRole tint, bool face, float hue, UiRole ring, int grade, bool chosen, bool dim)
        {
            float size = UiTokens.IconTile;
            float tx = x + UiTokens.S3, ty = y + (h - size) / 2f;
            if (face)
            {
                UiDraw.Portrait(s, inside, tx + UiTokens.S1, ty + UiTokens.S1, size - UiTokens.S2, title ?? string.Empty, hue, ring);
            }
            else
            {
                int t = UiDraw.Tile(s, inside, tx, ty, size, dim ? UiRole.Muted : tint, false);
                float g = UiTokens.S6;
                UiDraw.Icon(s, inside.In(t), tx + (size - g) / 2f, ty + (size - g) / 2f, g, icon == UiIcon.None ? UiIcon.Store : icon, dim ? UiRole.Muted : tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            float right = x + w - UiTokens.S3;
            float fw = 0f;
            if (!string.IsNullOrEmpty(figure) || !string.IsNullOrEmpty(note))
            {
                fw = Math.Min(UiDraw.SnapDown(w * 0.36f), UiDraw.Snap(Math.Max(UiTextRules.Width(figure ?? string.Empty, UiStyle.BodyStrong), UiTextRules.Width(note ?? string.Empty, UiStyle.Small)) + UiTokens.S1));
            }
            float lx = tx + size + UiTokens.S3, lw = Math.Max(0f, right - fw - (fw > 0f ? UiTokens.S3 : 0f) - lx);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            bool meter = grade >= 0;
            float chipW = string.IsNullOrEmpty(chip) ? 0f : Math.Min(UiDraw.SnapDown(lw * 0.5f), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(chip, UiStyle.SmallStrong) + UiTokens.S1));
            float block = th + (string.IsNullOrEmpty(sub) ? 0f : UiTokens.S1 + sh) + (meter ? UiTokens.S2 + UiTokens.S1 : 0f);
            float top = y + UiDraw.SnapDown((h - block) / 2f);
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(title ?? string.Empty, UiStyle.Title) + UiTokens.S1), lw - (chipW > 0f ? chipW + UiTokens.S3 : 0f));
            UiDraw.TextMiddle(s, inside, lx, top, th, nameW, UiStyle.Title, dim ? UiRole.Text2 : UiRole.Text, title ?? string.Empty, UiFlags.Truncate);
            if (chipW > 0f)
            {
                UiDraw.Chip(s, inside, lx + nameW + UiTokens.S3, top + UiDraw.SnapDown((th - UiTokens.Badge - UiTokens.S1) / 2f), chip, chipTone, chipW);
            }
            float ly = top + th;
            if (!string.IsNullOrEmpty(sub))
            {
                ly += UiTokens.S1;
                UiDraw.TextMiddle(s, inside, lx, ly, sh, lw, UiStyle.Small, chosen ? UiRole.Text : UiRole.Text2, sub, UiFlags.Truncate);
                ly += sh;
            }
            if (meter)
            {
                UiDraw.Meter(s, inside, lx, ly + UiTokens.S2, lw, UiTokens.S1, grade, UiRole.Accent);
            }
            if (fw > 0f)
            {
                bool both = !string.IsNullOrEmpty(figure) && !string.IsNullOrEmpty(note);
                float fh = both ? th + UiTokens.S1 + sh : string.IsNullOrEmpty(figure) ? sh : th;
                float fy = y + UiDraw.SnapDown((h - fh) / 2f);
                if (!string.IsNullOrEmpty(figure))
                {
                    UiDraw.TextMiddle(s, inside, right - fw, fy, th, fw, UiStyle.BodyStrong, figureTone == UiRole.None ? UiRole.Text : figureTone, figure, UiFlags.Right | UiFlags.Truncate);
                    fy += th + UiTokens.S1;
                }
                if (!string.IsNullOrEmpty(note))
                {
                    UiDraw.TextMiddle(s, inside, right - fw, fy, sh, fw, UiStyle.Small, chosen ? UiRole.Text : UiRole.Muted, note, UiFlags.Right | UiFlags.Truncate);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- the list of a page

        /// <summary>A left-hand list: its title and count, tools, filters, the rows in a scrolling list and a link at its foot. The row chosen is in the cyan at a fifth. Returns the list's index in the scene.</summary>
        internal static short ListPane(UiScene s, UiAt at, CompanyList list, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float width, float bottom, IUiKeys keys)
        {
            float cy = y;
            if (!string.IsNullOrEmpty(list.Title))
            {
                float rw = 0f;
                if (list.Tools != null && list.Tools.Length == 1)
                {
                    CompanyButton t = list.Tools[0];
                    rw = Math.Min(UiDraw.SnapDown(width * 0.5f), UiDraw.Snap(UiTextRules.Width(t.Label, UiStyle.BodyStrong) + UiTokens.S4));
                    TextLink(s, at, lay, ref t, x + width - rw, cy, UiTokens.Control, rw);
                }
                else if (!string.IsNullOrEmpty(list.Count))
                {
                    rw = Math.Min(UiDraw.SnapDown(width * 0.4f), UiDraw.Snap(UiTextRules.Width(list.Count, UiStyle.Body) + UiTokens.S1));
                    UiDraw.TextMiddle(s, at, x + width - rw, cy, UiTokens.Control, rw, UiStyle.Body, UiRole.Text2, list.Count, UiFlags.Right | UiFlags.Truncate);
                }
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.Control, width - rw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text, list.Title, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.Control;
            }
            if (list.Filters != null && list.Filters.Length > 1)
            {
                cy += UiDraw.Chips(s, at, x, cy, width, list.Filters, list.Filters.Length, Filter) + UiTokens.S3;
            }
            float footH = string.IsNullOrEmpty(list.FootLabel) ? 0f : UiTokens.Control + UiTokens.S2;
            int rows = list.Rows.Length;
            lay.RowIds = lay.RowIds.Length >= rows ? lay.RowIds : new string[rows];
            short result = -1;
            if (rows == 0)
            {
                if (!string.IsNullOrEmpty(list.Empty))
                {
                    Sentence(s, at, list.Empty, x, cy, width, UiRole.Text2, 4);
                }
            }
            else
            {
                const float step = ListRow + ListGap;
                float viewH = Math.Max(ListRow, (float)Math.Floor((bottom - footH - cy + ListGap) / step) * step - ListGap);
                float content = rows * step - ListGap;
                float offset = lay.Offset[CompanyLayout.SlotList];
                int reveal = lay.Reveal[CompanyLayout.SlotList];
                lay.Reveal[CompanyLayout.SlotList] = -1;
                if (reveal >= 0 && reveal < rows)
                {
                    offset = Math.Min(offset, reveal * step);
                    offset = Math.Max(offset, reveal * step + ListRow - viewH);
                }
                offset = Math.Max(0f, Math.Min((float)Math.Round(offset / step) * step, Math.Max(0f, content - viewH)));
                lay.Offset[CompanyLayout.SlotList] = offset;
                bool scrolls = content > viewH + 0.5f;
                float rw = scrolls ? width - UiTokens.S4 : width;
                short clip = s.AddClip(x, cy, width, Math.Min(viewH, content), offset);
                s.Clips[clip].ContentHeight = content;
                int first = Math.Min(rows - 1, (int)(offset / step));
                int last = Math.Min(rows - 1, (int)((offset + viewH - 0.5f) / step));
                short lst = s.AddList(rows, first, last - first + 1, clip, list.Chosen);
                var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
                for (int r = first; r <= last; r++)
                {
                    ref CompanyRow row = ref list.Rows[r];
                    float ry = r * step;
                    UiAt rowAt = inClip.Arriving(r - first);
                    if (row.Caption)
                    {
                        UiAppWindow.GroupCaption(s, rowAt, UiTokens.S3, ry + ListRow - UiAppWindow.GroupLabel, rw - UiTokens.S6, row.Title);
                        continue;
                    }
                    bool chosen = r == list.Chosen;
                    int face = UiDraw.Box(s, rowAt, 0f, ry, rw, ListRow, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
                    RowFace(s, rowAt.In(face), 0f, ry, rw, ListRow, row.Title, row.Sub, row.Figure, row.Note, row.Chip, row.ChipTone, row.FigureTone, row.Icon, row.Tint, row.Face, row.Hue, row.Ring, -1, chosen, row.Dim);
                    UiDraw.Node(s, rowAt, 0f, ry, rw, ListRow, Row, r, face, false, lst, r);
                    lay.RowIds[r] = row.Id;
                }
                UiDraw.ScrollThumb(s, at, x + width - UiTokens.S1, cy, Math.Min(viewH, content), offset, content);
                lay.List[CompanyLayout.SlotList] = lst;
                lay.Clip[CompanyLayout.SlotList] = clip;
                result = lst;
                cy += Math.Min(viewH, content);
            }
            if (footH > 0f)
            {
                CompanyButton foot = new CompanyButton { Label = list.FootLabel, Act = list.Foot };
                int index = lay.Add(foot);
                float fy = Math.Max(cy + UiTokens.S2, bottom - footH + UiTokens.S2);
                int face = UiDraw.Box(s, at, x, fy, width, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(face);
                UiDraw.Icon(s, inside, x + UiTokens.S3, fy + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, list.FootIcon, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
                float lx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S2, room = x + width - lx - UiTokens.S3 - UiTokens.IconSmall;
                string shown = UiTextRules.Cut(list.FootLabel, UiStyle.BodyStrong, room);
                float drawn = UiTextRules.Width(shown, UiStyle.BodyStrong);
                UiDraw.TextMiddle(s, inside, lx, fy, UiTokens.Control, Math.Min(UiDraw.Snap(drawn + UiTokens.S1), room), UiStyle.BodyStrong, UiRole.AccentText, shown, UiFlags.Truncate);
                UiDraw.Icon(s, inside, lx + drawn + UiDraw.LinkGap, fy + (UiTokens.Control - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText, UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
                UiDraw.Node(s, at, x, fy, width, UiTokens.Control, Act, index, face);
            }
            return result;
        }

        // ------------------------------------------------------------------------------------------------- a page of a list and a card

        /// <summary>A page of a list at the left and a card at the right (Marketing, Cover; Staff is the same with a summary over it).</summary>
        internal static void ListPage(UiScene s, UiAt at, CompanyPage page, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            if (page.PartOff)
            {
                OffPage(s, at, w, page.OffTitle, page.OffBody, x, y, cw, bottom, keys);
                return;
            }
            float gap = UiTokens.S6;
            float lw = UiDraw.SnapDown(Math.Min(cw * 0.34f, 420f));
            float cx = x + lw + gap, ccw = cw - lw - gap;
            short list = ListPane(s, at, page.List, v, w, lay, x, y, lw, bottom, keys);
            if (list >= 0)
            {
                UiDraw.JoinColumns(s, x, x + lw, x + cw, -1);
            }
            if (page.Card != null)
            {
                CardPage(s, at, page.Card, v, w, lay, cx, y, ccw, bottom, keys);
            }
            else if (page.Top != null)
            {
                CardPage(s, at, page.Top, v, w, lay, cx, y, ccw, bottom, keys);
            }
            else
            {
                UiDraw.EmptyState(s, at, cx, y, ccw, bottom - y, UiIcon.Info, w.T("reallife_co_pick_title", "Nothing picked"), string.IsNullOrEmpty(page.Lead) ? w.T("reallife_co_pick_body", "Pick a row at the left to see it here.") : page.Lead, null, -1, keys);
            }
        }

        private static void StaffPage(UiScene s, UiAt at, CompanyFacts f, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            ListPage(s, at, f.Staff, v, w, lay, x, y, cw, bottom, keys);
        }

        // ------------------------------------------------------------------------------------------------- the card

        private static float HeadHeight(CompanyCard c) => Math.Max(MarkSize, UiTokens.S12 * 2f);

        /// <summary>A card as a page: its head (a face or a tile, the title, what it is, its chips and facts), then its blocks in order. Returns the y under it.</summary>
        internal static float CardPage(UiScene s, UiAt at, CompanyCard c, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            float cy = y;
            if (c.Back != null && c.Back.Length > 0)
            {
                cy = Buttons(s, at, lay, v, c.Back, x, cy, cw, keys, true) + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(c.Title))
            {
                cy = CardHead(s, at, c, x, cy, cw);
            }
            else if (!string.IsNullOrEmpty(c.Empty))
            {
                UiDraw.EmptyState(s, at, x, cy, cw, bottom - cy, UiIcon.Info, w.T("reallife_co_pick_title", "Nothing picked"), c.Empty, null, -1, keys);
                return bottom;
            }
            for (int i = 0; i < c.Blocks.Length; i++)
            {
                if (cy >= bottom)
                {
                    break;
                }
                cy = Block(s, at, lay, v, w, c.Blocks[i], x, cy, cw, bottom, keys);
            }
            return cy;
        }

        // The head: a face in its ring or a glyph on a tile, the kicker over the title, what it is under it and its chips; up to four facts across the right.
        private static float CardHead(UiScene s, UiAt at, CompanyCard c, float x, float y, float cw)
        {
            float size = MarkSize;
            float ring = UiTokens.S1;
            if (c.Face)
            {
                UiDraw.Portrait(s, at, x + ring, y + ring, size - ring * 2f, c.Title, c.Hue, c.Ring);
            }
            else
            {
                int t = UiDraw.Tile(s, at, x, y, size, c.Tint, false);
                float g = UiTokens.S8;
                UiDraw.Icon(s, at.In(t), x + (size - g) / 2f, y + (size - g) / 2f, g, c.Icon, c.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            int facts = Math.Min(4, Math.Min(c.FactLabels.Length, c.FactValues.Length));
            float factW = 0f;
            for (int i = 0; i < facts; i++)
            {
                factW = Math.Max(factW, Math.Max(UiTextRules.Width(c.FactLabels[i], UiStyle.SectionHeader) * 1.18f, UiTextRules.Width(c.FactValues[i], UiStyle.Title)));
            }
            factW = facts > 0 ? UiDraw.Snap(factW + UiTokens.S4) : 0f;
            float factsTotal = Math.Min(factW * facts, UiDraw.SnapDown(cw * 0.5f));
            if (facts > 0)
            {
                factW = UiDraw.SnapDown(factsTotal / facts);
            }
            float tx = x + size + UiTokens.S4, tw = Math.Max(0f, cw - size - UiTokens.S4 - (facts > 0 ? factsTotal + UiTokens.S4 : 0f));
            float cy = y;
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            if (!string.IsNullOrEmpty(c.Kicker))
            {
                UiDraw.TextMiddle(s, at, tx, cy, kh, tw, UiStyle.SectionHeader, UiRole.Text2, c.Kicker, UiFlags.Label | UiFlags.Truncate);
                cy += kh + UiTokens.S1;
            }
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero));
            UiDraw.TextMiddle(s, at, tx, cy, nameH, tw, UiStyle.Hero, UiRole.Text, c.Title, UiFlags.Truncate);
            cy += nameH + UiTokens.S1;
            if (!string.IsNullOrEmpty(c.Sub))
            {
                UiDraw.TextMiddle(s, at, tx, cy, UiTokens.PairRow, tw, UiStyle.Body, UiRole.Text2, c.Sub, UiFlags.Truncate);
                cy += UiTokens.PairRow + UiTokens.S1;
            }
            float chipX = tx;
            for (int i = 0; i < c.Chips.Length; i++)
            {
                float cwid = UiDraw.Chip(s, at, chipX, cy, c.Chips[i], i < c.ChipTones.Length ? c.ChipTones[i] : UiRole.Muted, Math.Max(0f, tx + tw - chipX));
                chipX += cwid + UiTokens.S2;
            }
            if (c.Chips.Length > 0)
            {
                cy += UiTokens.Badge + UiTokens.S2;
            }
            if (facts > 0)
            {
                float fx = x + cw - factsTotal;
                float labelH = kh + UiTokens.S1;
                for (int i = 0; i < facts; i++)
                {
                    UiDraw.Text(s, at, fx + i * factW, y, factW - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, c.FactLabels[i], UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Text(s, at, fx + i * factW, y + labelH, factW - UiTokens.S3, UiStyle.Title, UiRole.Text, c.FactValues[i], UiFlags.Truncate);
                }
            }
            return UiDraw.Snap(Math.Max(y + size, cy)) + UiTokens.S5;
        }

        /// <summary>One block of a card at a place. Returns the y under it.</summary>
        internal static float Block(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyBlock b, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            switch (b.Kind)
            {
                case CompanyBlockKind.Section:
                {
                    CompanyButton? link = null;
                    if (b.Act.Do != CompanyDo.None && !string.IsNullOrEmpty(b.Value))
                    {
                        link = new CompanyButton { Label = b.Value, Act = b.Act };
                    }
                    return Section(s, at, lay, x, y + UiTokens.S2, cw, b.Text, link.HasValue ? null : b.Value, link);
                }
                case CompanyBlockKind.Row:
                    UiDraw.InfoRow(s, at, x, y, cw, b.Text, b.Value, false, -1, 0, b.Tone == UiRole.None ? UiRole.Text : b.Tone);
                    return y + UiTokens.PairRow + UiTokens.S1;
                case CompanyBlockKind.Link:
                {
                    int index = lay.Add(new CompanyButton { Label = b.Value, Act = b.Act });
                    UiDraw.InfoRow(s, at, x, y, cw, b.Text, b.Value, true, Act, index);
                    return y + UiTokens.PairRow + UiTokens.S1;
                }
                case CompanyBlockKind.Note:
                    return Sentence(s, at, b.Text, x, y, cw, b.Tone == UiRole.None ? UiRole.Text2 : b.Tone, 4);
                case CompanyBlockKind.Buttons:
                    return Buttons(s, at, lay, v, b.Buttons, x, y, cw, keys, true) + UiTokens.S3;
                case CompanyBlockKind.Switch:
                    return SwitchBlock(s, at, lay, b, x, y, cw);
                case CompanyBlockKind.Stepper:
                    return StepperBlock(s, at, lay, b, x, y, cw);
                case CompanyBlockKind.Field:
                    return FieldBlock(s, at, lay, v, b, x, y, cw);
                case CompanyBlockKind.Choice:
                    return ChoiceBlock(s, at, lay, b, x, y, cw);
                case CompanyBlockKind.Grades:
                {
                    float label = UiDraw.LabelColumn(cw, b.Labels);
                    for (int i = 0; i < b.Labels.Length && i < b.Numbers.Length; i++)
                    {
                        y += UiDraw.GradeBar(s, at, x, y, cw, b.Labels[i], b.Numbers[i], label) + UiTokens.S1;
                    }
                    return y + UiTokens.S2;
                }
                case CompanyBlockKind.Lines:
                {
                    int rows = b.Rows != null ? b.Rows.Length : 0;
                    int fit = (int)Math.Max(0f, Math.Floor((bottom - y) / DoneRow));
                    for (int i = 0; i < Math.Min(rows, fit); i++)
                    {
                        y += DoneLine(s, at, lay, ref b.Rows[i], x, y, cw, DoneRow);
                    }
                    if (rows == 0 && !string.IsNullOrEmpty(b.Text))
                    {
                        y = Sentence(s, at, b.Text, x, y, cw, UiRole.Muted, 2);
                    }
                    return y + UiTokens.S2;
                }
                case CompanyBlockKind.Chips:
                {
                    float cx = x;
                    for (int i = 0; b.Options != null && i < b.Options.Length; i++)
                    {
                        float chipW = UiDraw.Chip(s, at, cx, y, b.Options[i], b.Tone == UiRole.None ? UiRole.Muted : b.Tone, Math.Max(0f, x + cw - cx));
                        cx += chipW + UiTokens.S2;
                    }
                    return y + UiTokens.Badge + UiTokens.S3;
                }
                case CompanyBlockKind.Meter:
                {
                    Pair(s, at, x, y, UiTokens.PairRow, cw, UiStyle.Body, UiRole.Text2, b.Text, UiStyle.Body, UiRole.Text, b.Value);
                    UiDraw.Meter(s, at, x, y + UiTokens.PairRow + UiTokens.S1, cw, UiTokens.S1, Math.Max(0, b.Number), b.Tone == UiRole.None ? UiRole.Accent : b.Tone);
                    return y + UiTokens.PairRow + UiTokens.S3 + UiTokens.S2;
                }
                case CompanyBlockKind.Check:
                    return y + UiDraw.CheckLine(s, at, x, y, cw, b.Text, b.On) + UiTokens.S1;
                case CompanyBlockKind.Rows:
                    return ItemRows(s, at, lay, w, b, x, y, cw, bottom);
                default:
                    return y;
            }
        }

        // A switch with what it decides: the words and the line under them at the left, the switch at the right (its state in words, or who decides).
        private static float SwitchBlock(UiScene s, UiAt at, CompanyLayout lay, CompanyBlock b, float x, float y, float cw)
        {
            int index = lay.Add(new CompanyButton { Label = b.Text, Act = b.Act, Disabled = b.Disabled, Why = b.Why });
            float switchW = Math.Min(UiDraw.SnapDown(cw * 0.4f), UiDraw.Snap(UiTextRules.Width(b.Value ?? string.Empty, UiStyle.Body) + 44f + UiTokens.S3 * 2f));
            float tw = cw - switchW - UiTokens.S4;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((SwitchRow - th - (string.IsNullOrEmpty(b.Sub) ? 0f : sh)) / 2f);
            UiDraw.TextMiddle(s, at, x, top, th, tw, UiStyle.Title, b.Disabled ? UiRole.Text2 : UiRole.Text, b.Text ?? string.Empty, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(b.Sub))
            {
                UiDraw.TextMiddle(s, at, x, top + th, sh, tw, UiStyle.Small, UiRole.Text2, b.Sub, UiFlags.Truncate);
            }
            UiDraw.Switch(s, at, x + cw - switchW, y + (SwitchRow - UiTokens.Control) / 2f, switchW, b.On, b.Value, Act, index, b.Disabled);
            return y + SwitchRow;
        }

        // A stepper with what it sets: the words and their line at the left, minus, the typed figure and plus at the right.
        private static float StepperBlock(UiScene s, UiAt at, CompanyLayout lay, CompanyBlock b, float x, float y, float cw)
        {
            int first = lay.Add(new CompanyButton { Label = b.Text, Act = b.Act, Disabled = !b.CanLess || b.Disabled });
            lay.Add(new CompanyButton { Label = b.Text, Act = b.Act2, Disabled = !b.CanMore || b.Disabled });
            lay.Add(new CompanyButton { Label = b.Text, Act = b.Act3, Disabled = b.Disabled });
            float sw = Math.Min(UiDraw.SnapDown(cw * 0.42f), 220f);
            float tw = cw - sw - UiTokens.S4;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((SwitchRow - th - (string.IsNullOrEmpty(b.Sub) ? 0f : sh)) / 2f);
            UiDraw.TextMiddle(s, at, x, top, th, tw, UiStyle.Title, UiRole.Text, b.Text ?? string.Empty, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(b.Sub))
            {
                UiDraw.TextMiddle(s, at, x, top + th, sh, tw, UiStyle.Small, UiRole.Text2, b.Sub, UiFlags.Truncate);
            }
            UiDraw.Stepper(s, at, x + cw - sw, y + (SwitchRow - UiTokens.Control) / 2f, sw, b.Value, b.CanLess && !b.Disabled, b.CanMore && !b.Disabled, StepLess, StepType, StepMore, first, b.Disabled);
            return y + SwitchRow;
        }

        // A name to type: the words and their hint at the left, the kit's text field at the right. Typing goes through the host's field; the screen puts the typed words in the action.
        private static float FieldBlock(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyBlock b, float x, float y, float cw)
        {
            lay.Add(new CompanyButton { Label = b.Text, Disabled = true });
            lay.Add(new CompanyButton { Label = b.Text, Disabled = true });
            int first = lay.ActCount - 2;
            lay.Add(new CompanyButton { Label = b.Text, Act = b.Act, Disabled = b.Disabled });
            string id = b.Act.S + ":" + b.Act.A;
            bool typing = v.Typing != null && v.TypingKind == CompanyTyping.PlanName && v.TypingId == id;
            float fw = Math.Min(UiDraw.SnapDown(cw * 0.5f), 320f);
            float tw = cw - fw - UiTokens.S4;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((SwitchRow - th - (string.IsNullOrEmpty(b.Sub) ? 0f : sh)) / 2f);
            UiDraw.TextMiddle(s, at, x, top, th, tw, UiStyle.Title, UiRole.Text, b.Text ?? string.Empty, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(b.Sub))
            {
                UiDraw.TextMiddle(s, at, x, top + th, sh, tw, UiStyle.Small, UiRole.Text2, b.Sub, UiFlags.Truncate);
            }
            UiDraw.TextField(s, at, x + cw - fw, y + (SwitchRow - UiTokens.Control) / 2f, fw, typing ? v.Typing : b.Value, b.Text, StepType, first, b.Disabled, typing);
            return y + SwitchRow;
        }

        // A choice among a few: the words at the left, the options as chips at the right, the chosen one filled.
        private static float ChoiceBlock(UiScene s, UiAt at, CompanyLayout lay, CompanyBlock b, float x, float y, float cw)
        {
            string[] options = b.Options ?? new string[0];
            float chips = 0f;
            for (int i = 0; i < options.Length; i++)
            {
                chips += UiDraw.ButtonWidth(UiButtonKind.Chip, options[i], UiIcon.None, UiAction.Count, null) + (i > 0 ? UiTokens.S2 : 0f);
            }
            bool below = chips > cw * 0.62f;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float tw = below ? cw : cw - chips - UiTokens.S4;
            float top = y + UiDraw.SnapDown((SwitchRow - th - (string.IsNullOrEmpty(b.Sub) ? 0f : sh)) / 2f);
            UiDraw.TextMiddle(s, at, x, top, th, tw, UiStyle.Title, UiRole.Text, b.Text ?? string.Empty, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(b.Sub))
            {
                UiDraw.TextMiddle(s, at, x, top + th, sh, tw, UiStyle.Small, UiRole.Text2, b.Sub, UiFlags.Truncate);
            }
            float cx = below ? x : x + cw - chips, cy = below ? y + SwitchRow : y + Mid(SwitchRow, UiTokens.Chip);
            for (int i = 0; i < options.Length; i++)
            {
                CompanyAct act = b.Acts != null && i < b.Acts.Length ? b.Acts[i] : default(CompanyAct);
                int index = lay.Add(new CompanyButton { Label = options[i], Act = act, Disabled = b.Disabled });
                cx += ChoiceChip(s, at, cx, cy, options[i], i == b.Chosen, Opt, index, b.Disabled) + UiTokens.S2;
            }
            return below ? cy + UiTokens.Chip + UiTokens.S2 : y + SwitchRow;
        }

        // A list of rows in a card: each a row with its own press; the rows that do not fit are left to the page's wheel. Returns the y under them.
        private static float ItemRows(UiScene s, UiAt at, CompanyLayout lay, CompanyWords w, CompanyBlock b, float x, float y, float cw, float bottom)
        {
            CompanyItem[] items = b.Items ?? new CompanyItem[0];
            int fit = (int)Math.Max(0f, Math.Floor((bottom - y + ListGap) / (ListRow + ListGap)));
            for (int i = 0; i < Math.Min(items.Length, fit); i++)
            {
                ref CompanyItem it = ref items[i];
                bool press = it.Press && it.Act.Do != CompanyDo.None;
                int face = UiDraw.Box(s, at, x, y, cw, ListRow, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
                RowFace(s, at.In(face), x, y, cw, ListRow, it.Title, it.Sub, it.Figure, it.Note, it.Chip, it.ChipTone, it.FigureTone, it.Icon, it.Tint, it.Face, it.Hue, it.Ring, it.Grade, false, false);
                if (press)
                {
                    int index = lay.Add(new CompanyButton { Label = it.Title, Act = it.Act });
                    UiDraw.Node(s, at, x, y, cw, ListRow, Item, index, face);
                }
                y += ListRow + ListGap;
            }
            if (items.Length > fit && fit >= 0)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, cw, UiStyle.Small, UiRole.Muted, w.F("reallife_co_and_more", "and {count} more", "count", (items.Length - fit).ToString()), UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            if (items.Length == 0 && !string.IsNullOrEmpty(b.Text))
            {
                y = Sentence(s, at, b.Text, x, y, cw, UiRole.Muted, 2);
            }
            return y + UiTokens.S2;
        }
    }
}
