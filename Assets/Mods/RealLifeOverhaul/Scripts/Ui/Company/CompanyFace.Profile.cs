using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float ProfileWide = 760f;   // px of page from which a profile's blocks run in two columns where a break says so

        // The profile of the one chosen: their head with the page's buttons at its right, a band of how they are doing, the question waiting, then the page in two columns. A manager's
        // are the decisions they own (the word of who decides at the switch) beside their limits, log and crew; any other's are the blocks the source wrote, split at a break.
        private static void ProfilePage(UiScene s, UiAt at, CompanyProfile p, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float pw, float bottom, IUiKeys keys)
        {
            var pm = new UiProfileModel
            {
                Name = p.Name, Hue = p.Hue, Ring = p.Ring, Glyph = p.Glyph, GlyphTint = p.GlyphTint, Role = p.Role, RoleIcon = UiIcon.IdBadge, Status = p.Status, StatusTone = p.StatusTone,
                FactLabels = p.FactLabels, FactValues = p.FactValues
            };
            int first = s.ItemCount;
            float cy = UiAppWindow.Profile(s, at, pm, null, x, y, pw, keys);
            if (p.Buttons.Length > 0)
            {
                // What can be done with the person, at the head's right: the name's room ends before them.
                float most = UiDraw.SnapDown(pw * 0.55f), total = 0f;
                for (int i = 0; i < p.Buttons.Length; i++)
                {
                    total += Math.Min(most, UiDraw.ButtonWidth(p.Buttons[i].Kind, p.Buttons[i].Label, p.Buttons[i].Icon, p.Buttons[i].Key, keys)) + (i > 0 ? UiTokens.S3 : 0f);
                }
                Buttons(s, at, lay, v, p.Buttons, x + pw - most, y, most, keys);
                float edge = x + pw - total - UiTokens.S3;
                for (int i = first; i < s.ItemCount; i++)
                {
                    if (s.Items[i].Kind == UiKind.Text && s.Items[i].Style == UiStyle.Hero && s.Items[i].X + s.Items[i].W > edge)
                    {
                        s.Items[i].W = Math.Max(UiTokens.S8, edge - s.Items[i].X);
                    }
                }
            }
            cy -= UiTokens.S2;
            if (!string.IsNullOrEmpty(p.WaitingText))
            {
                cy += WaitingBanner(s, at, lay, v, p, x, cy, pw, keys) + UiTokens.S4;
            }
            if (p.GradeLabels.Length > 0)
            {
                cy += GradesBand(s, at, p, x, cy, pw) + UiTokens.S4;
            }
            if (p.Tasks.Length > 0 || p.Limits.Length > 0 || p.Log.Length > 0 || p.Crew.Length > 0)
            {
                ManagerColumns(s, at, p, v, w, lay, x, cy, pw, bottom, keys);
            }
            else
            {
                BlockColumns(s, at, lay, v, w, p.More, x, cy, pw, bottom, keys);
            }
        }

        // How they are doing, as a band under the head: its label, then the grades as bars two across, each with its figure.
        private static float GradesBand(UiScene s, UiAt at, CompanyProfile p, float x, float y, float pw)
        {
            int n = Math.Min(4, Math.Min(p.GradeLabels.Length, p.GradeValues.Length));
            float pad = UiTokens.S4, gap = UiTokens.S6;
            int cols = n > 1 && pw >= 480f ? 2 : 1, rows = (n + cols - 1) / cols;
            float colW = UiDraw.SnapDown((pw - pad * 2f - gap * (cols - 1)) / cols);
            var labels = new string[n];
            Array.Copy(p.GradeLabels, labels, n);
            float label = UiDraw.LabelColumn(colW, labels);
            float h = pad + UiTokens.PairRow + UiTokens.S1 + rows * (UiTokens.PairRow + UiTokens.S1) - UiTokens.S1 + pad;
            UiAt inside = Band(s, at, x, y, pw, h);
            UiDraw.TextMiddle(s, inside, x + pad, y + pad, UiTokens.PairRow, pw - pad * 2f, UiStyle.SectionHeader, UiRole.Text2, p.GradesTitle ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            for (int i = 0; i < n; i++)
            {
                float gx = x + pad + (i % cols) * (colW + gap), gy = y + pad + UiTokens.PairRow + UiTokens.S1 + (i / cols) * (UiTokens.PairRow + UiTokens.S1);
                UiDraw.GradeBar(s, inside, gx, gy, colW, p.GradeLabels[i], p.GradeValues[i], label);
            }
            return h;
        }

        // A manager's page below the band: the decisions they own at the left, each with its switch beside it, and at the right their limits, where questions go, their log and their crew.
        private static void ManagerColumns(UiScene s, UiAt at, CompanyProfile p, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float pw, float bottom, IUiKeys keys)
        {
            float gap = UiTokens.S6;
            // The decisions' column is as wide as its words and its switch ask, so that the word at a switch stands by the decision it belongs to.
            float need = 0f, who = 0f;
            for (int i = 0; i < p.Tasks.Length; i++)
            {
                need = Math.Max(need, Math.Max(UiTextRules.Width(p.Tasks[i].Title ?? string.Empty, UiStyle.Title), UiTextRules.Width(p.Tasks[i].Line ?? string.Empty, UiStyle.Small)));
                who = Math.Max(who, SwitchWidth(p.Tasks[i].Who));
            }
            float c1w = p.Tasks.Length == 0 ? 0f : Math.Min(UiDraw.SnapDown((pw - gap) * 0.56f), Math.Max(UiDraw.Snap(UiTokens.ControlSmall + UiTokens.S3 + need + UiTokens.S6 + who), 380f));
            if (c1w == 0f)
            {
                c1w = UiDraw.SnapDown((pw - gap) * 0.4f);
            }
            float c2w = pw - gap - c1w, c2x = x + c1w + gap;
            float ly = y;
            if (p.Tasks.Length > 0)
            {
                ly = Section(s, at, lay, x, ly, c1w, p.TasksTitle, p.TasksHint);
                for (int i = 0; i < p.Tasks.Length && ly + TaskRow <= bottom; i++)
                {
                    ly = TaskLine(s, at, lay, ref p.Tasks[i], x, ly, c1w);
                }
            }
            // The right column: limits and where questions go, the log, the crew.
            at = at.InRegion(RegionSide);
            float ry = y;
            if (p.Limits.Length > 0 || p.RouteOptions.Length > 0)
            {
                CompanyButton? all = string.IsNullOrEmpty(p.LimitsAll.Label) ? (CompanyButton?)null : p.LimitsAll;
                ry = Section(s, at, lay, c2x, ry, c2w, p.LimitsTitle, null, all);
                for (int i = 0; i < p.Limits.Length && ry + SwitchRow <= bottom; i++)
                {
                    CompanyLimit l = p.Limits[i];
                    var b = new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Stepper, Text = l.Title, Sub = l.Sub, Value = l.Value, CanLess = l.CanLess, CanMore = l.CanMore, Disabled = l.Disabled, Why = l.Why, Act = l.Less,
                        Act2 = l.More, Act3 = l.Typed
                    };
                    ry = Block(s, at, lay, v, w, b, c2x, ry, c2w, bottom, keys);
                }
                if (p.RouteOptions.Length > 0 && ry + SwitchRow <= bottom)
                {
                    var b = new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = p.RouteTitle, Sub = p.RouteSub, Options = p.RouteOptions, Chosen = p.RouteChosen, Acts = p.RouteActs };
                    ry = Block(s, at, lay, v, w, b, c2x, ry, c2w, bottom, keys);
                }
                ry += UiTokens.S3;
            }
            if (p.Log.Length > 0 && bottom - ry >= UiTokens.PairRow + DoneRow * 2f)
            {
                CompanyButton? all = string.IsNullOrEmpty(p.LogAll) ? (CompanyButton?)null : new CompanyButton { Label = p.LogAll, Act = p.LogOpen };
                ry = Section(s, at, lay, c2x, ry, c2w, p.LogTitle, null, all);
                int reserve = p.Crew.Length > 0 ? (int)(UiTokens.PairRow + ListRow * 2f + UiTokens.S4) : 0;
                int fit = (int)Math.Max(1f, Math.Floor((bottom - ry - reserve) / DoneRow));
                for (int i = 0; i < Math.Min(p.Log.Length, fit) && ry + DoneRow <= bottom - reserve; i++)
                {
                    ry += DoneLine(s, at, lay, ref p.Log[i], c2x, ry, c2w, DoneRow, bottom - reserve);
                }
                ry += UiTokens.S3;
            }
            if (p.Crew.Length > 0 && bottom - ry >= UiTokens.PairRow + ListRow)
            {
                ry = Section(s, at, lay, c2x, ry, c2w, p.CrewTitle, p.CrewCount);
                int fit = (int)Math.Max(1f, Math.Floor((bottom - ry + ListGap) / (ListRow + ListGap)));
                for (int i = 0; i < Math.Min(p.Crew.Length, fit); i++)
                {
                    ref CompanyItem it = ref p.Crew[i];
                    int face = UiDraw.Box(s, at, c2x, ry, c2w, ListRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                    RowFace(s, at.In(face), c2x, ry, c2w, ListRow, it.Title, it.Sub, it.Figure, it.Note, it.Chip, it.ChipTone, it.FigureTone, it.Icon, it.Tint, true, it.Hue, it.Ring, it.Grade,
                        false, false, it.FaceName);
                    if (it.Press && it.Act.Do != CompanyDo.None)
                    {
                        int index = lay.Add(new CompanyButton { Label = it.Title, Act = it.Act });
                        UiDraw.Node(s, at, c2x, ry, c2w, ListRow, Item, index, face);
                    }
                    ry += ListRow + ListGap;
                }
            }
            for (int i = 0; i < p.More.Length && ry < bottom; i++)
            {
                ry = Block(s, at, lay, v, w, p.More[i], c2x, ry, c2w, bottom, keys);
            }
            if (!string.IsNullOrEmpty(p.Note) && ry + UiTokens.S8 <= bottom)
            {
                Sentence(s, at, p.Note, c2x, ry, c2w, UiRole.Text2, 3, UiStyle.Small);
            }
        }

        // Any other page: the blocks the source wrote, in two columns from the break where the page is wide enough and in one where it is not.
        private static void BlockColumns(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyBlock[] blocks, float x, float y, float pw, float bottom, IUiKeys keys)
        {
            int split = -1;
            for (int i = 0; i < blocks.Length && split < 0; i++)
            {
                split = blocks[i].Kind == CompanyBlockKind.Break ? i : -1;
            }
            if (split >= 0 && pw >= ProfileWide)
            {
                float gap = UiTokens.S6, colW = Math.Min(CardMeasure, UiDraw.SnapDown((pw - gap) / 2f)), rx = x + colW + gap, ry = y, ly = y;
                for (int i = 0; i < split && ly < bottom; i++)
                {
                    ly = Block(s, at, lay, v, w, blocks[i], x, ly, colW, bottom, keys);
                }
                UiAt side = at.InRegion(RegionSide);
                for (int i = split + 1; i < blocks.Length && ry < bottom; i++)
                {
                    ry = Block(s, side, lay, v, w, blocks[i], rx, ry, colW, bottom, keys);
                }
                return;
            }
            float cy = y;
            for (int i = 0; i < blocks.Length && cy < bottom; i++)
            {
                if (blocks[i].Kind != CompanyBlockKind.Break)
                {
                    cy = Block(s, at, lay, v, w, blocks[i], x, cy, Math.Min(pw, CardMeasure), bottom, keys);
                }
            }
        }

        // A decision a manager owns: its glyph on a tile, what it is and its limit in a line, the one who decides now and the switch.
        private static float TaskLine(UiScene s, UiAt at, CompanyLayout lay, ref CompanyTask t, float x, float y, float w)
        {
            int index = lay.Add(new CompanyButton { Label = t.Title, Act = t.Act, Disabled = t.Disabled, Why = t.Why });
            float switchW = Math.Min(UiDraw.SnapDown(w * 0.4f), SwitchWidth(t.Who));
            float textW = w - switchW - UiTokens.S3;
            UiAt inside = at;
            float size = UiTokens.ControlSmall;
            int tile = UiDraw.Tile(s, inside, x, y + Mid(TaskRow, size), size, UiRole.None, false);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, inside.In(tile), x + (size - g) / 2f, y + Mid(TaskRow, size) + (size - g) / 2f, g, t.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + size + UiTokens.S3, tw = textW - size - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((TaskRow - th - sh) / 2f);
            UiDraw.TextMiddle(s, inside, tx, top, th, tw, UiStyle.Title, t.Disabled ? UiRole.Text2 : UiRole.Text, t.Title ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, tx, top + th, sh, tw, UiStyle.Small, UiRole.Text2, t.Line ?? string.Empty, UiFlags.Truncate);
            UiDraw.Switch(s, at, x + w - switchW, y + Mid(TaskRow, UiTokens.Control), switchW, t.On, t.Who, Act, index, t.Disabled);
            return y + TaskRow;
        }

        // The question waiting for the owner on a manager's page: what it is and the one button that answers it.
        private static float WaitingBanner(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyProfile p, float x, float y, float w, IUiKeys keys)
        {
            float pad = UiTokens.S4;
            CompanyButton b = p.Waiting;
            float bw = Math.Min(UiDraw.SnapDown(w * 0.4f), UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys));
            float tw = w - pad * 2f - bw - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float lh = string.IsNullOrEmpty(p.WaitingLine) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(p.WaitingLine, UiStyle.Small, tw, 3));
            float h = Math.Max(UiTokens.Control, th + (lh > 0f ? UiTokens.S1 + lh : 0f)) + pad * 2f;
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = at.In(box);
            UiDraw.TextMiddle(s, inside, x + pad, y + pad, th, tw, UiStyle.Title, UiRole.Text, p.WaitingText, UiFlags.Truncate);
            if (lh > 0f)
            {
                UiDraw.Text(s, inside, x + pad, y + pad + th + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, p.WaitingLine, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            Button(s, inside, lay, v, ref b, x + w - pad - bw, y + Mid(h, UiTokens.Control), bw, keys);
            return h;
        }
    }
}
