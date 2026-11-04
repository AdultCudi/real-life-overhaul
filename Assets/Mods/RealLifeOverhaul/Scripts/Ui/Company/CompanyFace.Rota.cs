using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float GridRowLeast = 56f, GridRowMost = 72f, GridRowSmall = 112f, HourRowLeast = 32f, HourRowMost = 48f;

        // The rota: at the left the places with their plans under each and, under them, what the chosen plan needs and what the week comes to; at the right the chosen place's
        // week (a header, the planner's mode and the plan's settings in a band, then the people by day beside the hours of the day), its day on the board, the planner's advice
        // (and each rung's week to read), or the plan's own page. A small shop lays its week out across the page.
        private static void RotaPage(UiScene s, UiAt at, CompanyFacts f, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            CompanyRotaFacts r = f.Rota;
            if (r.PartOff)
            {
                OffPage(s, at, w, r.OffTitle, r.OffBody, x, y, cw, bottom, keys);
                return;
            }
            float gap = UiTokens.S6;
            float lw = UiDraw.SnapDown(Math.Min(cw * 0.25f, 360f));
            CompanyRotaPlace p = r.Place;
            bool refused = p != null && (!string.IsNullOrEmpty(p.Refused) || p.RefusedCard != null);
            bool editing = p != null && !refused && v.Sub[CompanyView.Rota] == CompanyView.SubEdit && r.Edit != null;
            RotaLeft(s, at, lay, v, w, r, refused ? null : p, editing, x, y, lw, bottom, keys);
            RotaRight(s, at, lay, v, w, r, p, x + lw + gap, y, cw - lw - gap, bottom, keys);
            UiDraw.JoinColumns(s, x, x + lw, x + cw, -1);
        }

        // ------------------------------------------------------------------------------------------------- the left column

        // The places, each with its plans under it, then (for a place with a rota) what the plan in view needs by time, what the week comes to and the plan that holds next.
        private static void RotaLeft(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaFacts r, CompanyRotaPlace p, bool editing, float x, float y, float lw,
            float bottom, IUiKeys keys)
        {
            UiAt atL = at.InRegion(RegionList);
            float row = UiTokens.PairRow + UiTokens.S1;
            bool needs = p != null && !editing && p.NeedWhen.Length > 0;
            int sums = p != null ? Math.Min(p.SummaryLabels.Length, p.SummaryValues.Length) : 0;
            bool ahead = p != null && !string.IsNullOrEmpty(p.Ahead);
            float aheadH = ahead ? UiDraw.Snap(UiTextRules.WrappedHeight(p.Ahead, UiStyle.Small, lw - UiTokens.S3 * 2f, 4)) + UiTokens.S3 * 2f : 0f;
            float reserve = (needs ? UiTokens.S3 + row + Math.Min(p.NeedWhen.Length, 5) * row : 0f) + (sums > 0 ? UiTokens.S3 + row + sums * row : 0f) + (ahead ? UiTokens.S3 + aheadH : 0f);
            float paneBottom = Math.Max(y + UiTokens.Control + ListRow, bottom - reserve);
            ListPane(s, atL, r.Places, v, w, lay, x, y, lw, paneBottom, keys);
            // Where the list ended: its rows to the end of them, or as far as the room allows.
            float step = ListRow + ListGap, top = y + (string.IsNullOrEmpty(r.Places.Title) ? 0f : UiTokens.Control);
            int rows = r.Places.Rows.Length;
            float viewH = Math.Max(ListRow, (float)Math.Floor((paneBottom - top + ListGap) / step) * step - ListGap);
            float cy = rows == 0 ? top : top + Math.Min(viewH, rows * step - ListGap);
            if (needs)
            {
                cy = NeedsBlock(s, atL, lay, p, w, x, cy + UiTokens.S3, lw);
            }
            if (sums > 0)
            {
                cy = Section(s, atL, lay, x, cy + UiTokens.S3, lw, p.SummaryTitle);
                for (int i = 0; i < sums; i++)
                {
                    UiRole tone = i < p.SummaryTones.Length && p.SummaryTones[i] != UiRole.None ? p.SummaryTones[i] : UiRole.Text;
                    UiDraw.InfoRow(s, atL, x, cy, lw, p.SummaryLabels[i], p.SummaryValues[i], false, -1, 0, tone);
                    cy += row;
                }
            }
            if (ahead)
            {
                cy += UiTokens.S3;
                UiAt inside = Band(s, atL, x, cy, lw, aheadH);
                Sentence(s, inside, p.Ahead, x + UiTokens.S3, cy + UiTokens.S3, lw - UiTokens.S3 * 2f, UiRole.AccentText, 4, UiStyle.Small);
            }
        }

        // The place's needs by block of hours: when, and how many on shift; Edit opens the plan's page. Returns the y under it.
        private static float NeedsBlock(UiScene s, UiAt at, CompanyLayout lay, CompanyRotaPlace p, CompanyWords w, float x, float y, float width)
        {
            CompanyButton? edit = p.Edit.Act.Do != CompanyDo.None ? new CompanyButton { Label = w.T("reallife_rota_edit", "Edit"), Act = p.Edit.Act } : (CompanyButton?)null;
            float cy = Section(s, at, lay, x, y, width, p.NeedsTitle, null, edit);
            for (int i = 0; i < p.NeedWhen.Length && i < 5; i++)
            {
                UiDraw.InfoRow(s, at, x, cy, width, p.NeedWhen[i], i < p.NeedWho.Length ? p.NeedWho[i] : string.Empty);
                cy += UiTokens.PairRow + UiTokens.S1;
            }
            return cy;
        }

        // ------------------------------------------------------------------------------------------------- the right column

        private static void RotaRight(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaFacts r, CompanyRotaPlace p, float mx, float y, float mw, float bottom,
            IUiKeys keys)
        {
            UiAt atM = at.InRegion(RegionMain);
            if (p == null)
            {
                UiDraw.EmptyState(s, at, mx, y, mw, bottom - y, UiIcon.Calendar, w.T("reallife_rota_pick_title", "No place picked"), w.T("reallife_rota_pick_body",
                    "Pick a place at the left to plan its week."), null, -1, keys);
                return;
            }
            if (p.RefusedCard != null)
            {
                CardPage(s, atM, p.RefusedCard, v, w, lay, mx, y, mw, bottom, keys);
                return;
            }
            if (!string.IsNullOrEmpty(p.Refused))
            {
                UiDraw.EmptyState(s, at, mx, y, mw, bottom - y, UiIcon.Calendar, p.Name ?? string.Empty, p.Refused, null, -1, keys);
                return;
            }
            int sub = v.Sub[CompanyView.Rota];
            if (sub == CompanyView.SubEdit && r.Edit != null)
            {
                float ey = CardPage(s, atM, r.Edit, v, w, lay, mx, y, mw, bottom, keys);
                if (r.Floor != null && r.Floor.Crew.Length > 0 && bottom - ey >= 192f)
                {
                    FloorChart(s, at, r.Floor, mx, ey + UiTokens.S3, mw, bottom - ey - UiTokens.S3);
                }
                return;
            }
            if (sub == CompanyView.SubAdvice && p.Look != null)
            {
                LookPage(s, atM, lay, v, w, p, mx, y, mw, bottom, keys);
                return;
            }
            float cy = RotaHeader(s, atM, lay, v, w, p, sub, mx, y, mw, keys);
            if (sub == CompanyView.SubDay && p.Board != null)
            {
                DayPage(s, at, p, mx, cy, mw, bottom, keys);
                return;
            }
            cy = RotaBand(s, atM, lay, w, p, sub != CompanyView.SubDay && sub != CompanyView.SubAdvice, mx, cy, mw);
            if (sub == CompanyView.SubAdvice)
            {
                Advice(s, at, lay, v, w, p, mx, cy, mw, bottom, keys);
                return;
            }
            WeekPage(s, at, lay, v, w, p, mx, cy, mw, bottom);
        }

        // The header: which view of the place (the week, the day, the planner's advice) at the left; at the right the way back to the old rota where there is one, and the one
        // main button, Fill the rota. Returns the y under it.
        private static float RotaHeader(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, int sub, float x, float y, float width, IUiKeys keys)
        {
            string[] views = { w.T("reallife_rota_view_week", "Week"), w.T("reallife_rota_view_day", "Day"), w.T("reallife_rota_view_advice", "Advice") };
            float vx = x, chipY = y + Mid(UiTokens.Control, UiTokens.Chip);
            for (int i = 0; i < views.Length; i++)
            {
                int idx = lay.Add(new CompanyButton { Label = views[i], Act = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Rota, i) });
                vx += ChoiceChip(s, at, vx, chipY, views[i], i == sub, Act, idx) + UiTokens.S2;
            }
            float right = x + width;
            int lastChip = s.NodeCount - 1, firstButton = s.NodeCount;
            if (!string.IsNullOrEmpty(p.Fill.Label))
            {
                CompanyButton fill = p.Fill;
                float bw = UiDraw.ButtonWidth(fill.Kind, fill.Label, fill.Icon, fill.Key, keys);
                right -= bw;
                Button(s, at, lay, v, ref fill, right, y, bw, keys);
                right -= UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(p.Back.Label))
            {
                CompanyButton back = p.Back;
                float bw = UiDraw.ButtonWidth(back.Kind, back.Label, back.Icon, back.Key, keys);
                if (right - bw >= vx)
                {
                    Button(s, at, lay, v, ref back, right - bw, y, bw, keys);
                }
            }
            if (lastChip >= 0 && firstButton < s.NodeCount)
            {
                s.Nodes[lastChip].Right = firstButton;   // the day's tabs lie nearer than the buttons, so the last view chip is linked to the first button
            }
            return y + UiTokens.Control + UiTokens.S3;
        }

        // The planner's mode as one choice of short names, and (on the week) the plan's settings beside it: how it repeats, the most hours each person may be given and
        // overtime. The groups run on from the left and take a second line where a language's words are long. Returns the y under the band.
        private static float RotaBand(UiScene s, UiAt at, CompanyLayout lay, CompanyWords w, CompanyRotaPlace p, bool settings, float x, float y, float width)
        {
            string repeats = w.T("reallife_rota_repeats", "Repeats"), hoursLabel = w.T("reallife_rota_most_hours", "Most hours each"), overtime = w.T("reallife_rota_overtime", "Overtime");
            string on = w.T("reallife_co_on", "On"), off = w.T("reallife_co_off", "Off");
            // Each group's width: the wider of its label and its control.
            var wide = new float[4];
            wide[0] = p.ModeOptions.Length > 0 ? Math.Max(ChipsWidth(p.ModeOptions), LabelWidth(p.ModeTitle) + (string.IsNullOrEmpty(p.ModePicked) ? 0f : UiTokens.S3
                + UiDraw.Snap(UiTextRules.Width(p.ModePicked, UiStyle.Small) + UiTokens.S1))) : 0f;
            wide[1] = settings && p.RepeatOptions.Length > 0 ? Math.Max(ChipsWidth(p.RepeatOptions), LabelWidth(repeats)) : 0f;
            wide[2] = settings && !string.IsNullOrEmpty(p.HoursValue) ? Math.Max(168f, LabelWidth(hoursLabel)) : 0f;
            wide[3] = settings && !string.IsNullOrEmpty(p.HoursValue) ? Math.Max(LabelWidth(overtime), SwitchWidth(p.Overtime ? on : off)) : 0f;
            float pad = UiTokens.S3, inner = width - pad * 2f, gap = UiTokens.S5, rowH = UiTokens.PairRow + UiTokens.Control;
            var lineOf = new int[4];
            int lines = 1;
            float used = 0f;
            bool any = false;
            for (int g = 0; g < 4; g++)
            {
                if (wide[g] <= 0f)
                {
                    continue;
                }
                any = true;
                if (used > 0f && used + gap + wide[g] > inner)
                {
                    lines++;
                    used = 0f;
                }
                lineOf[g] = lines - 1;
                used += (used > 0f ? gap : 0f) + wide[g];
            }
            if (!any)
            {
                return y;
            }
            float height = pad * 2f + lines * rowH + (lines - 1) * UiTokens.S2;
            UiAt inside = Band(s, at, x, y, width, height);
            var runX = new float[lines];
            for (int g = 0; g < 4; g++)
            {
                if (wide[g] <= 0f)
                {
                    continue;
                }
                float gx = x + pad + runX[lineOf[g]], gy = y + pad + lineOf[g] * (rowH + UiTokens.S2);
                runX[lineOf[g]] += wide[g] + gap;
                float controlY = gy + UiTokens.PairRow;
                switch (g)
                {
                    case 0:
                        UiDraw.TextMiddle(s, inside, gx, gy, UiTokens.PairRow, LabelWidth(p.ModeTitle), UiStyle.SectionHeader, UiRole.Muted, p.ModeTitle ?? string.Empty,
                            UiFlags.Label | UiFlags.Truncate);
                        if (!string.IsNullOrEmpty(p.ModePicked))
                        {
                            float nx = gx + LabelWidth(p.ModeTitle) + UiTokens.S3;
                            UiDraw.TextMiddle(s, inside, nx, gy, UiTokens.PairRow, Math.Max(0f, gx + wide[0] - nx), UiStyle.Small, UiRole.Text2, p.ModePicked, UiFlags.Truncate);
                        }
                        ChoiceChips(s, inside, lay, p.ModeOptions, p.ModeActs, p.Mode, gx, controlY + Mid(UiTokens.Control, UiTokens.Chip));
                        break;
                    case 1:
                        UiDraw.TextMiddle(s, inside, gx, gy, UiTokens.PairRow, wide[1], UiStyle.SectionHeader, UiRole.Muted, repeats, UiFlags.Label | UiFlags.Truncate);
                        ChoiceChips(s, inside, lay, p.RepeatOptions, p.RepeatActs, p.Repeat, gx, controlY + Mid(UiTokens.Control, UiTokens.Chip));
                        break;
                    case 2:
                    {
                        UiDraw.TextMiddle(s, inside, gx, gy, UiTokens.PairRow, wide[2], UiStyle.SectionHeader, UiRole.Muted, hoursLabel, UiFlags.Label | UiFlags.Truncate);
                        int first = lay.Add(new CompanyButton { Label = p.HoursValue, Act = p.HoursLessAct, Disabled = !p.HoursLess });
                        lay.Add(new CompanyButton { Label = p.HoursValue, Act = p.HoursMoreAct, Disabled = !p.HoursMore });
                        lay.Add(new CompanyButton { Label = p.HoursValue, Act = p.HoursTypeAct });
                        UiDraw.Stepper(s, inside, gx, controlY, wide[2], p.HoursValue, p.HoursLess, p.HoursMore, StepLess, StepType, StepMore, first, false);
                        break;
                    }
                    default:
                    {
                        UiDraw.TextMiddle(s, inside, gx, gy, UiTokens.PairRow, wide[3], UiStyle.SectionHeader, UiRole.Muted, overtime, UiFlags.Label | UiFlags.Truncate);
                        int oi = lay.Add(new CompanyButton { Label = overtime, Act = p.OvertimeAct });
                        UiDraw.Switch(s, inside, gx, controlY, wide[3], p.Overtime, p.Overtime ? on : off, Act, oi, false);
                        break;
                    }
                }
            }
            return y + height + UiTokens.S4;
        }

        private static float LabelWidth(string label) => UiDraw.Snap(UiTextRules.Width(label ?? string.Empty, UiStyle.SectionHeader) * 1.2f + UiTokens.S1);

        private static float ChipsWidth(string[] options)
        {
            float total = 0f;
            for (int i = 0; i < options.Length; i++)
            {
                total += UiDraw.ButtonWidth(UiButtonKind.Chip, options[i], UiIcon.None, UiAction.Count, null) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return total;
        }

        // A choice among a few as a row of chips, the chosen one filled; each does its own action.
        private static void ChoiceChips(UiScene s, UiAt at, CompanyLayout lay, string[] options, CompanyAct[] acts, int chosen, float x, float y)
        {
            for (int i = 0; i < options.Length; i++)
            {
                int idx = lay.Add(new CompanyButton { Label = options[i], Act = i < acts.Length ? acts[i] : default(CompanyAct) });
                x += ChoiceChip(s, at, x, y, options[i], i == chosen, Opt, idx) + UiTokens.S2;
            }
        }

        // ------------------------------------------------------------------------------------------------- the week

        // The week: the people by day with the hours of the chosen day beside them; a shop of three people or fewer lays it out across the page with the hours of the day under it.
        private static void WeekPage(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, float x, float y, float width, float bottom)
        {
            UiAt atM = at.InRegion(RegionMain);
            string title = w.T("reallife_rota_the_week", "The week");
            if (p.People.Length > 0 && p.People.Length <= 3)
            {
                float chart = 240f;
                float rowH = Math.Max(GridRowLeast, Math.Min(GridRowSmall, UiDraw.SnapDown((bottom - y - 96f - chart) / p.People.Length)));
                float cy = WeekGrid(s, atM, lay, w, p.People, p.DayNames, p.Day, p.DayTotals, p.TotalHours, title, p.Weekly, true, x, y, width, bottom - chart, rowH);
                HourColumns(s, at.InRegion(RegionSide), p, x, cy + UiTokens.S4, width, bottom - cy - UiTokens.S4);
                return;
            }
            float gap = UiTokens.S6, hw = UiDraw.SnapDown(Math.Min(width * 0.26f, 320f)), gw = width - hw - gap;
            float each = UiDraw.SnapDown((bottom - y - 96f) / Math.Max(1, p.People.Length));
            WeekGrid(s, atM, lay, w, p.People, p.DayNames, p.Day, p.DayTotals, p.TotalHours, title, p.Weekly, true, x, y, gw, bottom, Math.Max(GridRowLeast, Math.Min(GridRowMost, each)));
            HourBars(s, at.InRegion(RegionSide), p, x + gw + gap, y, hw, bottom);
        }

        // The people by day: each person a row, each day a chip of their hours or "off", their hours at the right, the day's hours under. Returns the y under it.
        private static float WeekGrid(UiScene s, UiAt at, CompanyLayout lay, CompanyWords w, CompanyRotaPerson[] people, string[] dayNames, int day, string[] dayTotals, string totalHours,
            string title, string weekly, bool press, float x, float y, float width, float bottom, float rowH)
        {
            float cy = y;
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, width * 0.4f, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(weekly))
            {
                UiDraw.TextMiddle(s, at, x + width * 0.4f, cy, UiTokens.PairRow, width * 0.6f, UiStyle.Small, UiRole.AccentText, weekly, UiFlags.Right | UiFlags.Truncate);
            }
            cy += UiTokens.PairRow + UiTokens.S1;
            float personW = UiDraw.SnapDown(Math.Min(Math.Max(width * 0.26f, 188f), 220f)), hoursW = 64f;   // wide enough for a role to stand whole
            float dayW = UiDraw.SnapDown((width - personW - hoursW) / 7f);
            for (int d = 0; d < 7; d++)
            {
                string name = d < dayNames.Length ? dayNames[d] : string.Empty;
                float dx = x + personW + d * dayW;
                bool chosen = press && d == day;
                int face = UiDraw.Box(s, at, dx + UiTokens.S1, cy, dayW - UiTokens.S2, UiTokens.Chip, chosen ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen
                    ? UiFlags.Selected : UiFlags.None);
                UiDraw.TextMiddle(s, at.In(face), dx + UiTokens.S1, cy, UiTokens.Chip, dayW - UiTokens.S2, UiStyle.SectionHeader, chosen ? UiRole.OnAccent : UiRole.Muted, name,
                    UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                if (press)
                {
                    UiDraw.Node(s, at, dx + UiTokens.S1, cy, dayW - UiTokens.S2, UiTokens.Chip, DayHead, d, face);
                }
            }
            UiDraw.TextMiddle(s, at, x + personW + dayW * 7f, cy, UiTokens.Chip, hoursW, UiStyle.SectionHeader, UiRole.Muted, w.T("reallife_rota_hours", "Hours"),
                UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            cy += UiTokens.Chip + UiTokens.S2;
            float footer = UiTokens.PairRow + UiTokens.S4;
            int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - footer) / rowH));
            int shown = Math.Min(people.Length, fit);
            for (int i = 0; i < shown; i++)
            {
                ref CompanyRotaPerson pr = ref people[i];
                float size = UiTokens.AvatarRow - UiTokens.S2, faceY = cy + Mid(rowH, UiTokens.AvatarRow) + UiTokens.S1;
                if (pr.ToHire)
                {
                    int t = UiDraw.Tile(s, at, x + UiTokens.S1, faceY, size, UiRole.Muted, false);
                    UiDraw.Icon(s, at.In(t), x + UiTokens.S1 + (size - UiTokens.Icon) / 2f, faceY + (size - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Plus, UiRole.Muted,
                        UiFlags.Middle | UiFlags.Centre, true);
                }
                else
                {
                    UiDraw.Portrait(s, at, x + UiTokens.S1, faceY, size, pr.Name, pr.Hue, pr.Ring);
                }
                float tx = x + UiTokens.AvatarRow + UiTokens.S2, tw = personW - UiTokens.AvatarRow - UiTokens.S2;
                float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                float top = cy + UiDraw.SnapDown((rowH - th - sh) / 2f);
                UiDraw.TextMiddle(s, at, tx, top, th, tw, UiStyle.Title, UiRole.Text, pr.Name ?? string.Empty, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, tx, top + th, sh, tw, UiStyle.Small, UiRole.Text2, pr.Role ?? string.Empty, UiFlags.Truncate);
                float dh = rowH >= 88f ? UiTokens.Control : UiTokens.ControlSmall;
                for (int d = 0; d < 7; d++)
                {
                    string shift = pr.Shifts != null && d < pr.Shifts.Length ? pr.Shifts[d] : null;
                    float dx = x + personW + d * dayW + UiTokens.S1, dw = dayW - UiTokens.S2;
                    float dy = cy + Mid(rowH, dh);
                    bool warn = pr.Tones != null && d < pr.Tones.Length && pr.Tones[d] != UiRole.None;
                    if (string.IsNullOrEmpty(shift))
                    {
                        UiDraw.TextMiddle(s, at, dx, dy, dh, dw, UiStyle.Small, UiRole.Muted, w.T("reallife_rota_day_off", "off"), UiFlags.Centre | UiFlags.Truncate);
                        continue;
                    }
                    int cell = UiDraw.Box(s, at, dx, dy, dw, dh, warn ? UiRole.WarnSoft : press && d == day ? UiRole.RowSel : UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
                    UiDraw.TextMiddle(s, at.In(cell), dx, dy, dh, dw, UiStyle.SmallStrong, warn ? UiRole.WarnText : UiRole.Text, shift, UiFlags.Centre | UiFlags.Truncate);
                }
                UiDraw.TextMiddle(s, at, x + personW + dayW * 7f, cy, rowH, hoursW, UiStyle.BodyStrong, pr.HoursTone == UiRole.None ? UiRole.Text : pr.HoursTone, pr.Hours ?? string.Empty,
                    UiFlags.Right | UiFlags.Truncate);
                cy += rowH;
            }
            if (shown < people.Length)
            {
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, width, UiStyle.Small, UiRole.Muted, w.F("reallife_co_and_more", "and {count} more", "count", (people.Length - shown).ToString()),
                    UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            // The day's hours, and the total.
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, personW, UiStyle.Body, UiRole.Text2, w.T("reallife_rota_staff_hours", "Staff hours"), UiFlags.Label | UiFlags.Truncate);
            for (int d = 0; d < 7 && d < dayTotals.Length; d++)
            {
                UiDraw.TextMiddle(s, at, x + personW + d * dayW, cy, UiTokens.PairRow, dayW, UiStyle.BodyStrong, UiRole.Text, dayTotals[d] ?? string.Empty, UiFlags.Centre | UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, at, x + personW + dayW * 7f, cy, UiTokens.PairRow, hoursW, UiStyle.BodyStrong, UiRole.Text, totalHours ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            return cy + UiTokens.PairRow + UiTokens.S4;
        }

        // The chosen day hour by hour: a bar of who is on shift against a tick where the planner needs them, short hours in red. The rows grow with the room.
        private static void HourBars(UiScene s, UiAt at, CompanyRotaPlace p, float x, float y, float width, float bottom)
        {
            float cy = DayTitleRow(s, at, p, x, y, width);
            int n = Math.Min(p.HourLabels.Length, Math.Min(p.OnShift.Length, p.Needed.Length));
            int most = 1;
            for (int i = 0; i < n; i++)
            {
                most = Math.Max(most, Math.Max(p.OnShift[i], p.Needed[i]));
            }
            float labelW = 32f, figW = 52f;
            float trackX = x + labelW + UiTokens.S2, trackW = width - labelW - figW - UiTokens.S4;
            float hourRow = Math.Max(HourRowLeast, Math.Min(HourRowMost, UiDraw.SnapDown((bottom - cy - UiTokens.PairRow * 2f) / Math.Max(1, n))));
            int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - UiTokens.PairRow * 2f) / hourRow));
            for (int i = 0; i < Math.Min(n, fit); i++)
            {
                bool shortHere = p.OnShift[i] < p.Needed[i];
                UiDraw.TextMiddle(s, at, x, cy, hourRow, labelW, UiStyle.Small, UiRole.Muted, p.HourLabels[i], UiFlags.Truncate);
                float bh = UiTokens.S3;
                float by = cy + Mid(hourRow, bh);
                UiDraw.Box(s, at, trackX, by, trackW, bh, UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
                float fillW = Math.Max(0f, trackW * p.OnShift[i] / most);
                if (fillW > 0f)
                {
                    UiDraw.Box(s, at, trackX, by, fillW, bh, shortHere ? UiRole.Danger : UiRole.Accent, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share);
                }
                if (p.Needed[i] > 0)
                {
                    float tx = trackX + trackW * p.Needed[i] / most;
                    UiDraw.Box(s, at, tx - 1f, by - UiTokens.S1, 2f, bh + UiTokens.S2, UiRole.Text, UiRole.None, 0f, UiFlags.Decor | UiFlags.Share);
                }
                UiDraw.TextMiddle(s, at, x + width - figW, cy, hourRow, figW, UiStyle.SmallStrong, shortHere ? UiRole.DangerText : UiRole.Text, p.OnShift[i] + "/" + p.Needed[i],
                    UiFlags.Right | UiFlags.Truncate);
                cy += hourRow;
            }
            DayLegend(s, at, p, x, cy + UiTokens.S2, width, bottom);
        }

        // The day's title with its note at the right. Returns the y under it.
        private static float DayTitleRow(UiScene s, UiAt at, CompanyRotaPlace p, float x, float y, float width)
        {
            string dayTitle = p.DayTitle ?? string.Empty;
            float titleW = UiDraw.Snap(UiTextRules.Width(dayTitle, UiStyle.SectionHeader) * 1.2f + UiTokens.S1);
            if (titleW > width)
            {
                dayTitle = p.DayShort ?? dayTitle;   // the day alone where the whole will not fit
                titleW = Math.Min(width, UiDraw.Snap(UiTextRules.Width(dayTitle, UiStyle.SectionHeader) * 1.2f + UiTokens.S1));
            }
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, titleW, UiStyle.SectionHeader, UiRole.Text2, dayTitle, UiFlags.Label | UiFlags.Truncate);
            float noteW = width - titleW - UiTokens.S3;
            if (!string.IsNullOrEmpty(p.DayNote) && noteW >= UiDraw.Snap(UiTextRules.Width(p.DayNote, UiStyle.Small) + UiTokens.S1))
            {
                UiDraw.TextMiddle(s, at, x + width - noteW, y, UiTokens.PairRow, noteW, UiStyle.Small, UiRole.AccentText, p.DayNote, UiFlags.Right | UiFlags.Truncate);
            }
            return y + UiTokens.PairRow + UiTokens.S1;
        }

        // What the colours of the day's bars mean, on as many lines as the words need.
        private static void DayLegend(UiScene s, UiAt at, CompanyRotaPlace p, float x, float cy, float width, float bottom)
        {
            float lx = x;
            for (int i = 0; i < p.Legend.Length; i++)
            {
                float lw = UiDraw.Snap(UiTextRules.Width(p.Legend[i], UiStyle.Small) + UiTokens.S1);
                float need = UiTokens.StatusDot + UiTokens.S2 + lw;
                if (lx > x && lx + need > x + width)
                {
                    lx = x;   // the key goes on to a second line where the words of a language are long
                    cy += UiTokens.PairRow;
                }
                if (cy + UiTokens.PairRow > bottom)
                {
                    break;
                }
                UiRole dot = i == 0 ? UiRole.Accent : i == 1 ? UiRole.Danger : UiRole.Text;
                UiDraw.Box(s, at, lx, cy + (UiTokens.PairRow - UiTokens.StatusDot) / 2f, i == 2 ? 2f : UiTokens.StatusDot, UiTokens.StatusDot, dot, UiRole.None, UiTokens.RadiusSmall,
                    UiFlags.Decor);
                UiDraw.TextMiddle(s, at, lx + UiTokens.StatusDot + UiTokens.S2, cy, UiTokens.PairRow, Math.Min(lw, x + width - (lx + UiTokens.StatusDot + UiTokens.S2)), UiStyle.Small,
                    UiRole.Text2, p.Legend[i], UiFlags.Truncate);
                lx += need + UiTokens.S3;
            }
        }

        // The chosen day for a small shop, across the page: a column for each hour, the crew on shift against a tick where it is needed, short hours in red.
        private static void HourColumns(UiScene s, UiAt at, CompanyRotaPlace p, float x, float y, float width, float height)
        {
            float cy = DayTitleRow(s, at, p, x, y, width);
            int n = Math.Min(p.HourLabels.Length, Math.Min(p.OnShift.Length, p.Needed.Length));
            float legendH = p.Legend.Length > 0 ? UiTokens.PairRow + UiTokens.S2 : 0f;
            float room = UiDraw.SnapDown(Math.Max(0f, y + height - cy - legendH));
            if (n > 0 && room >= UiTokens.S16)
            {
                var labels = new string[n];
                Array.Copy(p.HourLabels, labels, n);
                Columns(s, at, x, cy, width, room, labels, p.OnShift, p.Needed);
            }
            DayLegend(s, at, p, x, cy + room + UiTokens.S2, width, y + height);
        }

        // Columns for hours: a figure over each, its bar from the foot, a tick where the need stands (null for none) and the hour's label under it.
        private static void Columns(UiScene s, UiAt at, float x, float y, float width, float height, string[] labels, int[] on, int[] need)
        {
            int n = labels.Length;
            if (n == 0)
            {
                return;
            }
            float slot = UiDraw.SnapDown(width / n), figH = UiTokens.PairRow, labelH = UiTokens.PairRow;
            float plotH = UiDraw.SnapDown(height - figH - labelH);
            if (slot < UiTokens.S6 || plotH < UiTokens.S8)
            {
                return;
            }
            int most = 1;
            for (int i = 0; i < n; i++)
            {
                most = Math.Max(most, Math.Max(on[i], need != null ? need[i] : 0));
            }
            float bw = UiDraw.SnapDown(Math.Max(UiTokens.S2, Math.Min(UiTokens.S12, slot / 2f)));
            float x0 = x + UiDraw.SnapDown((width - slot * n) / 2f);
            for (int i = 0; i < n; i++)
            {
                bool shortHere = need != null && on[i] < need[i];
                float sx = x0 + i * slot, cx = sx + UiDraw.SnapDown((slot - bw) / 2f), foot = y + figH + plotH;
                UiDraw.Box(s, at, cx, y + figH, bw, plotH, UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
                if (on[i] > 0)
                {
                    float bh = Math.Max(UiTokens.S1, plotH * on[i] / most);
                    UiDraw.Box(s, at, cx, foot - bh, bw, bh, shortHere ? UiRole.Danger : UiRole.Accent, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share | UiFlags.Decor);
                }
                if (need != null && need[i] > 0)
                {
                    UiDraw.Box(s, at, cx - UiTokens.S1, foot - plotH * need[i] / most - 1f, bw + UiTokens.S2, 2f, UiRole.Text, UiRole.None, 0f, UiFlags.Decor | UiFlags.Share);
                }
                UiDraw.TextMiddle(s, at, sx, y, figH, slot, UiStyle.SmallStrong, shortHere ? UiRole.DangerText : UiRole.Text, need != null ? on[i] + "/" + need[i] : on[i].ToString(),
                    UiFlags.Centre | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, sx, foot, labelH, slot, UiStyle.Small, UiRole.Muted, labels[i], UiFlags.Centre | UiFlags.Truncate);
            }
        }

        // The floor of crew a plan asks for, hour by hour, under its page.
        private static void FloorChart(UiScene s, UiAt at, CompanyFloor floor, float x, float y, float width, float height)
        {
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, width, UiStyle.SectionHeader, UiRole.Text2, floor.Title ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            int n = Math.Min(floor.Hours.Length, floor.Crew.Length);
            if (n > 0)
            {
                var labels = new string[n];
                Array.Copy(floor.Hours, labels, n);
                var crew = new int[n];
                Array.Copy(floor.Crew, crew, n);
                Columns(s, at, x, y + UiTokens.PairRow + UiTokens.S2, width, Math.Min(UiDraw.SnapDown(height - UiTokens.PairRow - UiTokens.S2), 280f), labels, crew, null);
            }
        }

        // ------------------------------------------------------------------------------------------------- the day

        // The chosen day on the board of lanes (the bars carry their hours only, the lane's head says whose), and the day's own figures under it.
        private static void DayPage(UiScene s, UiAt at, CompanyRotaPlace p, float x, float y, float width, float bottom, IUiKeys keys)
        {
            int figs = Math.Min(p.DayFigureLabels.Length, p.DayFigureValues.Length);
            float panel = UiTokens.S3 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float figH = figs > 0 ? UiTokens.S4 + panel : 0f;
            UiBoardModel m = p.Board;
            float strip = (m.CoverTitle != null ? UiBoard.CoverHeight(m) : 0f) + UiBoard.LegendHeight(m);
            float natural = UiTokens.Control + UiTokens.S4 + UiTokens.PairRow + UiTokens.S1 + Math.Max(1, m.Lanes.Length) * UiBoard.LaneHeight + strip;
            float boardH = Math.Min(bottom - y - figH, natural);
            UiBoard.Lay(s, at, m, x, y, width, boardH, keys);
            if (figs > 0)
            {
                float fy = y + boardH + UiTokens.S4;
                UiAt inside = Band(s, at, x, fy, width, panel);
                FiguresBlock(s, inside, new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = p.DayFigureLabels, Values = p.DayFigureValues }, x + UiTokens.S4, fy + UiTokens.S3,
                    width - UiTokens.S4 * 2f);
            }
        }

        // ------------------------------------------------------------------------------------------------- the planner's advice

        // The planner's ladder: each plan with what it comes to, a Look at its week and the button that uses it, the advised one marked; then your people against the advised
        // plan, and what stands beside it.
        private static void Advice(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, float x, float y, float width, float bottom, IUiKeys keys)
        {
            UiAt atM = at.InRegion(RegionMain);
            float cy = Section(s, atM, lay, x, y, width, p.AdviceTitle);
            if (!string.IsNullOrEmpty(p.AdviceNote))
            {
                cy = Sentence(s, atM, p.AdviceNote, x, cy, width, UiRole.Text2, 2);
            }
            float rowH = 72f;
            for (int i = 0; i < p.Rungs.Length && cy + rowH <= bottom; i++)
            {
                RungRow(s, atM, lay, v, w, ref p.Rungs[i], x, cy, width, rowH, keys);
                cy += rowH + UiTokens.S2;
            }
            float besideH = 0f;
            for (int i = 0; i < p.Beside.Length; i++)
            {
                besideH += BesideHeight(p.Beside[i]);
            }
            if (p.Folk.Length > 0 && cy + UiTokens.PairRow + ListRow <= bottom)
            {
                cy = Section(s, atM, lay, x, cy + UiTokens.S2, width, p.FolkTitle);
                int room = (int)Math.Max(1f, Math.Floor((bottom - cy - besideH - (p.Folk.Length > 1 ? UiTokens.PairRow : 0f)) / (ListRow + ListGap))), shown = Math.Min(p.Folk.Length, room);
                for (int i = 0; i < shown && cy + ListRow <= bottom; i++)
                {
                    FolkRow(s, atM, ref p.Folk[i], x, cy, width);
                    cy += ListRow + ListGap;
                }
                if (shown < p.Folk.Length && cy + UiTokens.PairRow <= bottom)
                {
                    UiDraw.TextMiddle(s, atM, x, cy, UiTokens.PairRow, width, UiStyle.Small, UiRole.Muted, w.F("reallife_co_and_more", "and {count} more", "count", (p.Folk.Length - shown).ToString()),
                        UiFlags.Truncate);
                    cy += UiTokens.PairRow;
                }
            }
            for (int i = 0; i < p.Beside.Length && cy < bottom; i++)
            {
                CompanyBlock b = p.Beside[i];
                if (b.Kind == CompanyBlockKind.Section && i + 1 < p.Beside.Length && cy + BesideHeight(b) + BesideHeight(p.Beside[i + 1]) > bottom)
                {
                    break;   // a heading is left out with the rows that would end past the window
                }
                if (b.Kind == CompanyBlockKind.Items && b.Items != null && cy + b.Items.Length * (ListRow + ListGap) > bottom)
                {
                    break;
                }
                cy = Block(s, atM, lay, v, w, b, x, cy, width, bottom, keys);
            }
        }

        // The room a block beside the people asks for: a heading, rows of the list, or a sentence of two lines.
        private static float BesideHeight(CompanyBlock b) => b.Kind == CompanyBlockKind.Section ? UiTokens.S2 + UiTokens.PairRow + UiTokens.S1
            : b.Kind == CompanyBlockKind.Items ? (b.Items != null ? b.Items.Length : 0) * (ListRow + ListGap) + UiTokens.S2
            : b.Kind == CompanyBlockKind.Note ? UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) * 2f + UiTokens.S2 : 0f;

        // A rung of the ladder: its name (the advised one marked), what it needs and gains, its hours and what it nets, and at the right a Look at its week and the button that
        // uses it, or a short word where it cannot be used yet.
        private static void RungRow(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, ref CompanyRung r, float x, float y, float width, float rowH, IUiKeys keys)
        {
            int face = UiDraw.Box(s, at, x, y, width, rowH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = at.In(face);
            float right = x + width - UiTokens.S4;
            string chip = r.Chip;
            if (!string.IsNullOrEmpty(chip))
            {
                float cwid = UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(chip, UiStyle.SmallStrong) + UiTokens.S1);
                right -= cwid;
                UiDraw.Chip(s, inside, right, y + UiDraw.SnapDown((rowH - UiTokens.Badge - UiTokens.S1) / 2f), chip, UiRole.Warn, cwid);
            }
            else if (!string.IsNullOrEmpty(r.Use.Label))
            {
                CompanyButton use = r.Use;
                float bw = Math.Min(UiDraw.SnapDown(width * 0.3f), UiDraw.ButtonWidth(use.Kind, use.Label, use.Icon, use.Key, keys));
                right -= bw;
                Button(s, inside, lay, v, ref use, right, y + (rowH - UiTokens.Control) / 2f, bw, keys);
            }
            if (!string.IsNullOrEmpty(r.Look.Label))
            {
                CompanyButton look = r.Look;
                float bw = Math.Min(UiDraw.SnapDown(width * 0.2f), UiDraw.ButtonWidth(look.Kind, look.Label, look.Icon, look.Key, keys));
                right -= UiTokens.S3 + bw;
                Button(s, inside, lay, v, ref look, right, y + (rowH - UiTokens.Control) / 2f, bw, keys);
            }
            float hoursW = UiDraw.Snap(Math.Max(UiTextRules.Width(r.Hours ?? string.Empty, UiStyle.BodyStrong), UiTextRules.Width(r.Net ?? string.Empty, UiStyle.Small)) + UiTokens.S1);
            hoursW = Math.Min(hoursW, UiDraw.SnapDown(width * 0.3f));
            float fx = right - UiTokens.S4 - hoursW;
            float tw = Math.Max(0f, fx - UiTokens.S4 - (x + UiTokens.S4));
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((rowH - th - sh * (string.IsNullOrEmpty(r.Gain) ? 1f : 2f)) / 2f);
            string advised = w.T("reallife_rota_advised", "Advised");
            float advisedW = r.Advised ? UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(advised, UiStyle.SmallStrong) + UiTokens.S1) : 0f;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name ?? string.Empty, UiStyle.Title) + UiTokens.S1), Math.Max(0f, tw - (advisedW > 0f ? advisedW + UiTokens.S3 : 0f)));
            UiDraw.TextMiddle(s, inside, x + UiTokens.S4, top, th, nameW, UiStyle.Title, UiRole.Text, r.Name ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, x + UiTokens.S4, top + th, sh, tw, UiStyle.Small, UiRole.Text2, r.Line ?? string.Empty, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(r.Gain))
            {
                UiDraw.TextMiddle(s, inside, x + UiTokens.S4, top + th + sh, sh, tw, UiStyle.Small, UiRole.OkText, r.Gain, UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, inside, fx, top, th, hoursW, UiStyle.BodyStrong, UiRole.Text, r.Hours ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, fx, top + th, sh, hoursW, UiStyle.Small, UiRole.Muted, r.Net ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            if (r.Advised)
            {
                UiDraw.Chip(s, inside, x + UiTokens.S4 + nameW + UiTokens.S3, top + UiDraw.SnapDown((th - UiTokens.Badge - UiTokens.S1) / 2f), advised, UiRole.Ok, advisedW);
            }
        }

        // One person against the advised plan: their portrait and what they ask for, the range they ask for as a bar with the plan's hours marked on it, and the hours at the right
        // with how far from the range they stand.
        private static void FolkRow(UiScene s, UiAt at, ref CompanyRotaFolk f, float x, float y, float width)
        {
            float size = UiTokens.IconTile;
            float ty = y + (ListRow - size) / 2f;
            UiDraw.Portrait(s, at, x + UiTokens.S3 + UiTokens.S1, ty + UiTokens.S1, size - UiTokens.S2, f.Name, f.Hue, f.Ring);
            float lx = x + UiTokens.S3 + size + UiTokens.S3;
            float figW = UiDraw.SnapDown(Math.Min(width * 0.2f, 160f));
            float barW = UiDraw.SnapDown(width * 0.28f);
            float textW = Math.Max(0f, width - (lx - x) - barW - figW - UiTokens.S3 * 3f);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((ListRow - th - sh) / 2f);
            UiDraw.TextMiddle(s, at, lx, top, th, textW, UiStyle.Title, UiRole.Text, f.Name ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, lx, top + th, sh, textW, UiStyle.Small, UiRole.Text2, string.IsNullOrEmpty(f.Asks) ? f.Role ?? string.Empty : f.Asks, UiFlags.Truncate);
            float bx = lx + textW + UiTokens.S3;
            RangeBar(s, at, ref f, bx, y + Mid(ListRow, UiTokens.S4), barW);
            float fx = x + width - UiTokens.S3 - figW;
            bool both = !string.IsNullOrEmpty(f.Note);
            float fh = both ? th + UiTokens.S1 + sh : th;
            float fy = y + UiDraw.SnapDown((ListRow - fh) / 2f);
            UiDraw.TextMiddle(s, at, fx, fy, th, figW, UiStyle.BodyStrong, UiRole.Text, f.Figure ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            if (both)
            {
                UiDraw.TextMiddle(s, at, fx, fy + th + UiTokens.S1, sh, figW, UiStyle.Small, f.Tone == UiRole.None ? UiRole.Text2 : f.Tone, f.Note, UiFlags.Right | UiFlags.Truncate);
            }
        }

        // The hours somebody asks for as a band on a track and the plan's hours as a tick on it, in green inside the band and red outside it.
        private static void RangeBar(UiScene s, UiAt at, ref CompanyRotaFolk f, float x, float y, float width)
        {
            float h = UiTokens.S4;
            UiDraw.Box(s, at, x, y, width, h, UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
            float scale = Math.Max(1f, Math.Max(Math.Max(f.Most, f.Least), f.Plan) * 1.25f);
            if (f.Least > 0 || f.Most > 0)
            {
                float from = width * f.Least / scale, to = f.Most > 0 ? width * f.Most / scale : width;
                UiDraw.Box(s, at, x + from, y, Math.Max(UiTokens.S1, to - from), h, UiRole.AccentSoft, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share | UiFlags.Decor);
            }
            float tick = x + Math.Min(width, width * f.Plan / scale);
            UiRole ink = f.Tone == UiRole.DangerText || f.Tone == UiRole.WarnText ? UiRole.Danger : UiRole.Ok;
            UiDraw.Box(s, at, tick - 2f, y - UiTokens.S1, 4f, h + UiTokens.S2, ink, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share | UiFlags.Decor);
        }

        // ------------------------------------------------------------------------------------------------- a rung's week, to read

        // One rung of the ladder read as a week, applying nothing: the way back, the one button that uses it, its name and what it comes to, who works when under it and what it
        // changes in the opening hours, and why.
        private static void LookPage(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, float x, float y, float width, float bottom, IUiKeys keys)
        {
            CompanyRotaLook l = p.Look;
            float cy = y;
            float right = x + width;
            if (!string.IsNullOrEmpty(l.Chip))
            {
                float cwid = UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(l.Chip, UiStyle.SmallStrong) + UiTokens.S1);
                UiDraw.Chip(s, at, right - cwid, cy + UiDraw.SnapDown((UiTokens.Control - UiTokens.Badge - UiTokens.S1) / 2f), l.Chip, UiRole.Warn, cwid);
            }
            else if (!string.IsNullOrEmpty(l.Use.Label))
            {
                CompanyButton use = l.Use;
                float bw = UiDraw.ButtonWidth(use.Kind, use.Label, use.Icon, use.Key, keys);
                Button(s, at, lay, v, ref use, right - bw, cy, bw, keys);
            }
            if (!string.IsNullOrEmpty(l.Back.Label))
            {
                CompanyButton back = l.Back;
                Button(s, at, lay, v, ref back, x, cy, Math.Min(UiDraw.SnapDown(width * 0.4f), UiDraw.ButtonWidth(back.Kind, back.Label, back.Icon, back.Key, keys)), keys);
            }
            cy += UiTokens.Control + UiTokens.S4;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero));
            string advised = w.T("reallife_rota_advised", "Advised");
            float advisedW = l.Advised ? UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(advised, UiStyle.SmallStrong) + UiTokens.S1) : 0f;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(l.Name ?? string.Empty, UiStyle.Hero) + UiTokens.S1), Math.Max(0f, width - (advisedW > 0f ? advisedW + UiTokens.S3 : 0f)));
            UiDraw.TextMiddle(s, at, x, cy, nameH, nameW, UiStyle.Hero, UiRole.Text, l.Name ?? string.Empty, UiFlags.Truncate);
            if (l.Advised)
            {
                UiDraw.Chip(s, at, x + nameW + UiTokens.S3, cy + UiDraw.SnapDown((nameH - UiTokens.Badge - UiTokens.S1) / 2f), advised, UiRole.Ok, advisedW);
            }
            cy += nameH + UiTokens.S1;
            cy = Sentence(s, at, l.Line, x, cy, width, UiRole.Text2, 1);
            cy = Sentence(s, at, l.Why, x, cy, width, UiRole.Text2, 2, UiStyle.Small);
            if (l.FigureLabels.Length > 0)
            {
                cy = FiguresBlock(s, at, new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = l.FigureLabels, Values = l.FigureValues }, x, cy + UiTokens.S1, width);
            }
            // The week at the left and, beside it, what the plan changes in the opening hours (the week alone across the page where it changes none).
            bool beside = l.Changes.Length > 0;
            float gap = UiTokens.S6, side = beside ? UiDraw.SnapDown(Math.Max(300f, Math.Min(380f, width * 0.34f))) : 0f, gw = width - (beside ? side + gap : 0f);
            float rowH = Math.Max(44f, Math.Min(GridRowMost, UiDraw.SnapDown((bottom - cy - 92f) / Math.Max(1, l.People.Length))));
            float end = WeekGrid(s, at, lay, w, l.People, p.DayNames, -1, l.DayTotals, l.TotalHours, w.T("reallife_rota_the_week", "The week"), null, false, x, cy, gw, bottom, rowH);
            if (!beside)
            {
                Sentence(s, at, l.ChangesNone, x, end, width, UiRole.Text2, 1);
                return;
            }
            float sx = x + gw + gap, sy = Section(s, at, lay, sx, cy, side, l.ChangesTitle);
            for (int i = 0; i < l.Changes.Length; i++)
            {
                float h = ChangeRow(s, at, ref l.Changes[i], sx, sy, side, bottom);
                if (h <= 0f)
                {
                    break;
                }
                sy += h;
            }
        }

        // A run of hours the plan opens or closes: a plus or a minus on a tile, when and what it is worth, and why in as many lines as it needs. Returns its height, or 0 where it
        // would end past the window.
        private static float ChangeRow(UiScene s, UiAt at, ref CompanyRotaChange c, float x, float y, float width, float bottom)
        {
            float tile = UiTokens.ControlSmall, textX = x + tile + UiTokens.S3, textW = width - tile - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float figW = UiDraw.Snap(UiTextRules.Width(c.Figure ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1);
            float subH = string.IsNullOrEmpty(c.Sub) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(c.Sub, UiStyle.Small, textW, 3));
            float h = Math.Max(tile, th + (subH > 0f ? UiTokens.S1 + subH : 0f));
            if (y + h > bottom)
            {
                return 0f;
            }
            UiRole tone = c.Opens ? UiRole.Ok : UiRole.Warn;
            int t = UiDraw.Tile(s, at, x, y, tile, tone, false);
            UiDraw.Icon(s, at.In(t), x + (tile - UiTokens.Icon) / 2f, y + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, c.Opens ? UiIcon.Plus : UiIcon.Minus, tone, UiFlags.Middle | UiFlags.Centre,
                true);
            UiDraw.TextMiddle(s, at, textX, y, th, Math.Max(0f, textW - figW - UiTokens.S3), UiStyle.Title, UiRole.Text, c.Title ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + width - figW, y, th, figW, UiStyle.BodyStrong, c.Opens ? UiRole.OkText : UiRole.WarnText, c.Figure ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            if (subH > 0f)
            {
                UiDraw.Text(s, at, textX, y + th + UiTokens.S1, textW, UiStyle.Small, UiRole.Text2, c.Sub, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            return h + UiTokens.S4;
        }
    }
}
