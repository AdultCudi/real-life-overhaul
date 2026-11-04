using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float GridRow = 56f, HourRow = 32f;

        // The rota: places and their plans at the left, the chosen place's plan form over its week by person and day, and the chosen day hour by hour at the right.
        // The day, the planner's advice and a plan's needs are sub pages of the tab.
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
            float needsH = p != null && p.NeedWhen.Length > 0 ? UiTokens.PairRow + UiTokens.S1 + Math.Min(p.NeedWhen.Length, 5) * (UiTokens.PairRow + UiTokens.S1) + UiTokens.S4 : 0f;
            ListPane(s, at.InRegion(RegionList), r.Places, v, w, lay, x, y, lw, bottom - needsH, keys);
            if (needsH > 0f)
            {
                NeedsBlock(s, at.InRegion(RegionList), lay, p, w, x, bottom - needsH + UiTokens.S3, lw);
            }
            float mx = x + lw + gap, mw = cw - lw - gap;
            UiDraw.JoinColumns(s, x, x + lw, x + cw, -1);
            if (p == null)
            {
                UiDraw.EmptyState(s, at, mx, y, mw, bottom - y, UiIcon.Calendar, w.T("reallife_rota_pick_title", "No place picked"), w.T("reallife_rota_pick_body",
                    "Pick a place at the left to plan its week."), null, -1, keys);
                return;
            }
            int sub = v.Sub[CompanyView.Rota];
            if (!string.IsNullOrEmpty(p.Refused))
            {
                UiDraw.EmptyState(s, at, mx, y, mw, bottom - y, UiIcon.Calendar, p.Name ?? string.Empty, p.Refused, null, -1, keys);
                return;
            }
            if (sub == CompanyView.SubEdit && r.Edit != null)
            {
                CardPage(s, at.InRegion(RegionMain), r.Edit, v, w, lay, mx, y, mw, bottom, keys);
                return;
            }
            float cy = PlanForm(s, at.InRegion(RegionMain), lay, v, w, p, sub, mx, y, mw, keys);
            if (sub == CompanyView.SubDay && p.Board != null)
            {
                UiBoard.Lay(s, at, p.Board, mx, cy, mw, bottom - cy, keys);
                return;
            }
            if (sub == CompanyView.SubAdvice)
            {
                Advice(s, at, lay, v, w, p, mx, cy, mw, bottom, keys);
                return;
            }
            float hw = UiDraw.SnapDown(Math.Min(mw * 0.26f, 320f)), gw = mw - hw - gap;
            WeekGrid(s, at.InRegion(RegionMain), lay, v, w, p, mx, cy, gw, bottom, keys);
            HourBars(s, at.InRegion(RegionSide), lay, w, p, mx + gw + gap, cy, hw, bottom);
        }

        // The plan in view, in three rows: which view of the place (the week, the day, the planner's advice) at the left and how the planner fills it (its mode) at the right; the
        // plan's form (its name, how it repeats, the most hours each person may be given, overtime); and the buttons along the right. Returns the y under them.
        private static float PlanForm(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, int sub, float x, float y, float w2, IUiKeys keys)
        {
            float cy = y;
            string[] views = { w.T("reallife_rota_view_week", "Week"), w.T("reallife_rota_view_day", "Day"), w.T("reallife_rota_view_advice", "Advice") };
            float vx = x;
            for (int i = 0; i < views.Length; i++)
            {
                int idx = lay.Add(new CompanyButton { Label = views[i], Act = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Rota, i) });
                vx += ChoiceChip(s, at, vx, cy, views[i], i == sub, Act, idx) + UiTokens.S2;
            }
            float modes = 0f;
            for (int i = 0; i < p.ModeOptions.Length; i++)
            {
                modes += UiDraw.ButtonWidth(UiButtonKind.Chip, p.ModeOptions[i], UiIcon.None, UiAction.Count, null) + (i > 0 ? UiTokens.S2 : 0f);
            }
            float mx = x + w2 - modes;
            for (int i = 0; i < p.ModeOptions.Length && mx >= vx; i++)
            {
                CompanyAct act = i < p.ModeActs.Length ? p.ModeActs[i] : default(CompanyAct);
                int idx = lay.Add(new CompanyButton { Label = p.ModeOptions[i], Act = act });
                mx += ChoiceChip(s, at, mx, cy, p.ModeOptions[i], i == p.Mode, Opt, idx) + UiTokens.S2;
            }
            cy += UiTokens.Chip + UiTokens.S4;
            // The form: each field a label over its control. The day's board has no use for it, and the page keeps its targets to the board's.
            float fx = x;
            float rowH = UiTokens.Control;
            if (sub == CompanyView.SubDay)
            {
                goto Buttons;
            }
            string planName = p.PlanNames.Length > 0 && p.PlanChosen >= 0 && p.PlanChosen < p.PlanNames.Length ? p.PlanNames[p.PlanChosen] : p.Name;
            float nameW = Math.Min(UiDraw.SnapDown(w2 * 0.22f), UiDraw.Snap(UiTextRules.Width(planName ?? string.Empty, UiStyle.Title) + UiTokens.S4));
            nameW = Math.Max(nameW, UiDraw.Snap(UiTextRules.Width(w.T("reallife_rota_plan", "Plan"), UiStyle.SectionHeader) * 1.2f + UiTokens.S2));
            UiDraw.TextMiddle(s, at, fx, cy, UiTokens.PairRow, nameW, UiStyle.SectionHeader, UiRole.Muted, w.T("reallife_rota_plan", "Plan"), UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, fx, cy + UiTokens.PairRow, rowH, nameW, UiStyle.Title, UiRole.Text, planName ?? string.Empty, UiFlags.Truncate);
            fx += nameW + UiTokens.S5;
            if (p.RepeatOptions.Length > 0)
            {
                float rw = 0f;
                for (int i = 0; i < p.RepeatOptions.Length; i++)
                {
                    rw += UiDraw.ButtonWidth(UiButtonKind.Chip, p.RepeatOptions[i], UiIcon.None, UiAction.Count, null) + (i > 0 ? UiTokens.S2 : 0f);
                }
                string repeats = w.T("reallife_rota_repeats", "Repeats");
                UiDraw.TextMiddle(s, at, fx, cy, UiTokens.PairRow, Math.Max(rw, UiDraw.Snap(UiTextRules.Width(repeats, UiStyle.SectionHeader) * 1.2f)), UiStyle.SectionHeader, UiRole.Muted, repeats,
                    UiFlags.Label | UiFlags.Truncate);
                float ox = fx;
                for (int i = 0; i < p.RepeatOptions.Length; i++)
                {
                    CompanyAct act = i < p.RepeatActs.Length ? p.RepeatActs[i] : default(CompanyAct);
                    int idx = lay.Add(new CompanyButton { Label = p.RepeatOptions[i], Act = act });
                    ox += ChoiceChip(s, at, ox, cy + UiTokens.PairRow + Mid(rowH, UiTokens.Chip), p.RepeatOptions[i], i == p.Repeat, Opt, idx) + UiTokens.S2;
                }
                fx += rw + UiTokens.S5;
            }
            if (!string.IsNullOrEmpty(p.HoursValue))
            {
                string hoursLabel = w.T("reallife_rota_most_hours", "Most hours each");
                float sw = Math.Max(168f, UiDraw.Snap(UiTextRules.Width(hoursLabel, UiStyle.SectionHeader) * 1.2f + UiTokens.S1));
                UiDraw.TextMiddle(s, at, fx, cy, UiTokens.PairRow, sw, UiStyle.SectionHeader, UiRole.Muted, hoursLabel, UiFlags.Label | UiFlags.Truncate);
                int first = lay.Add(new CompanyButton { Label = p.HoursValue, Act = p.HoursLessAct, Disabled = !p.HoursLess });
                lay.Add(new CompanyButton { Label = p.HoursValue, Act = p.HoursMoreAct, Disabled = !p.HoursMore });
                lay.Add(new CompanyButton { Label = p.HoursValue, Act = p.HoursTypeAct });
                UiDraw.Stepper(s, at, fx, cy + UiTokens.PairRow, sw, p.HoursValue, p.HoursLess, p.HoursMore, StepLess, StepType, StepMore, first, false);
                fx += sw + UiTokens.S5;
                string overtime = w.T("reallife_rota_overtime", "Overtime");
                float ow = Math.Max(UiDraw.Snap(UiTextRules.Width(overtime, UiStyle.SectionHeader) * 1.2f + UiTokens.S1), SwitchWidth(p.Overtime ? w.T("reallife_co_on", "On")
                    : w.T("reallife_co_off", "Off")));
                UiDraw.TextMiddle(s, at, fx, cy, UiTokens.PairRow, ow, UiStyle.SectionHeader, UiRole.Muted, overtime, UiFlags.Label | UiFlags.Truncate);
                int oi = lay.Add(new CompanyButton { Label = overtime, Act = p.OvertimeAct });
                UiDraw.Switch(s, at, fx, cy + UiTokens.PairRow, ow, p.Overtime, p.Overtime ? w.T("reallife_co_on", "On") : w.T("reallife_co_off", "Off"), Act, oi, false);
            }
            cy += UiTokens.PairRow + rowH + UiTokens.S4;
        Buttons:
            // The buttons along the right: edit, copy, back to the last rota, and fill the gaps (the main one, F).
            CompanyButton[] buttons = { p.Edit, p.Copy, p.Back, p.Fill };
            float total = 0f;
            for (int i = 0; i < buttons.Length; i++)
            {
                total += string.IsNullOrEmpty(buttons[i].Label) ? 0f : UiDraw.ButtonWidth(buttons[i].Kind, buttons[i].Label, buttons[i].Icon, buttons[i].Key, keys) + UiTokens.S3;
            }
            float bx = x + Math.Max(0f, w2 - total + UiTokens.S3);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (string.IsNullOrEmpty(buttons[i].Label))
                {
                    continue;
                }
                CompanyButton b = buttons[i];
                float bw = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                Button(s, at, lay, v, ref b, bx, cy, bw, keys);
                bx += bw + UiTokens.S3;
            }
            return cy + UiTokens.Control + UiTokens.S5;
        }

        // The week: each person a row, each day a chip of their hours or "off", their hours at the right, the day's hours under; then the sums.
        private static void WeekGrid(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, float x, float y, float width, float bottom, IUiKeys keys)
        {
            float cy = y;
            string title = w.T("reallife_rota_the_week", "The week");
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, width * 0.4f, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(p.Weekly))
            {
                UiDraw.TextMiddle(s, at, x + width * 0.4f, cy, UiTokens.PairRow, width * 0.6f, UiStyle.Small, UiRole.AccentText, p.Weekly, UiFlags.Right | UiFlags.Truncate);
            }
            cy += UiTokens.PairRow + UiTokens.S1;
            float personW = UiDraw.SnapDown(Math.Min(width * 0.26f, 220f)), hoursW = 64f;
            float dayW = UiDraw.SnapDown((width - personW - hoursW) / 7f);
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.Chip, personW, UiStyle.SectionHeader, UiRole.Muted, w.T("reallife_rota_person", "Person"), UiFlags.Label | UiFlags.Truncate);
            for (int d = 0; d < 7; d++)
            {
                string name = d < p.DayNames.Length ? p.DayNames[d] : string.Empty;
                float dx = x + personW + d * dayW;
                int face = UiDraw.Box(s, at, dx + UiTokens.S1, cy, dayW - UiTokens.S2, UiTokens.Chip, d == p.Day ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, d == p.Day
                    ? UiFlags.Selected : UiFlags.None);
                UiDraw.TextMiddle(s, at.In(face), dx + UiTokens.S1, cy, UiTokens.Chip, dayW - UiTokens.S2, UiStyle.SectionHeader, d == p.Day ? UiRole.OnAccent : UiRole.Muted, name,
                    UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                UiDraw.Node(s, at, dx + UiTokens.S1, cy, dayW - UiTokens.S2, UiTokens.Chip, DayHead, d, face);
            }
            UiDraw.TextMiddle(s, at, x + personW + dayW * 7f, cy, UiTokens.Chip, hoursW, UiStyle.SectionHeader, UiRole.Muted, w.T("reallife_rota_hours", "Hours"),
                UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            cy += UiTokens.Chip + UiTokens.S2;
            float footer = UiTokens.PairRow + UiTokens.S2 + UiTokens.Control * 2f + UiTokens.S4;
            int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - footer) / GridRow));
            int shown = Math.Min(p.People.Length, fit);
            for (int i = 0; i < shown; i++)
            {
                ref CompanyRotaPerson pr = ref p.People[i];
                UiDraw.Portrait(s, at, x + UiTokens.S1, cy + (GridRow - UiTokens.AvatarRow) / 2f + UiTokens.S1, UiTokens.AvatarRow - UiTokens.S2, pr.Name, pr.Hue, pr.Ring);
                float tx = x + UiTokens.AvatarRow + UiTokens.S2, tw = personW - UiTokens.AvatarRow - UiTokens.S2;
                float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                float top = cy + UiDraw.SnapDown((GridRow - th - sh) / 2f);
                UiDraw.TextMiddle(s, at, tx, top, th, tw, UiStyle.Title, UiRole.Text, pr.Name ?? string.Empty, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, tx, top + th, sh, tw, UiStyle.Small, UiRole.Text2, pr.Role ?? string.Empty, UiFlags.Truncate);
                for (int d = 0; d < 7; d++)
                {
                    string shift = pr.Shifts != null && d < pr.Shifts.Length ? pr.Shifts[d] : null;
                    float dx = x + personW + d * dayW + UiTokens.S1, dw = dayW - UiTokens.S2, dh = UiTokens.ControlSmall;
                    float dy = cy + Mid(GridRow, dh);
                    bool warn = pr.Tones != null && d < pr.Tones.Length && pr.Tones[d] != UiRole.None;
                    if (string.IsNullOrEmpty(shift))
                    {
                        UiDraw.TextMiddle(s, at, dx, dy, dh, dw, UiStyle.Small, UiRole.Muted, w.T("reallife_rota_day_off", "off"), UiFlags.Centre | UiFlags.Truncate);
                        continue;
                    }
                    int face = UiDraw.Box(s, at, dx, dy, dw, dh, warn ? UiRole.WarnSoft : d == p.Day ? UiRole.RowSel : UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
                    UiDraw.TextMiddle(s, at.In(face), dx, dy, dh, dw, UiStyle.SmallStrong, warn ? UiRole.WarnText : UiRole.Text, shift, UiFlags.Centre | UiFlags.Truncate);
                }
                UiDraw.TextMiddle(s, at, x + personW + dayW * 7f, cy, GridRow, hoursW, UiStyle.BodyStrong, pr.HoursTone == UiRole.None ? UiRole.Text : pr.HoursTone, pr.Hours ?? string.Empty,
                    UiFlags.Right | UiFlags.Truncate);
                cy += GridRow;
            }
            if (shown < p.People.Length)
            {
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, width, UiStyle.Small, UiRole.Muted, w.F("reallife_co_and_more", "and {count} more", "count", (p.People.Length - shown).ToString()),
                    UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            // The day's hours, and the total.
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, personW, UiStyle.Body, UiRole.Text2, w.T("reallife_rota_staff_hours", "Staff hours"), UiFlags.Label | UiFlags.Truncate);
            for (int d = 0; d < 7 && d < p.DayTotals.Length; d++)
            {
                UiDraw.TextMiddle(s, at, x + personW + d * dayW, cy, UiTokens.PairRow, dayW, UiStyle.BodyStrong, UiRole.Text, p.DayTotals[d] ?? string.Empty, UiFlags.Centre | UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, at, x + personW + dayW * 7f, cy, UiTokens.PairRow, hoursW, UiStyle.BodyStrong, UiRole.Text, p.TotalHours ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
            cy += UiTokens.PairRow + UiTokens.S4;
            // The sums under it, each its label over its figure.
            int n = Math.Min(p.SummaryLabels.Length, p.SummaryValues.Length);
            if (n > 0)
            {
                float slot = UiDraw.SnapDown(width / n);
                for (int i = 0; i < n; i++)
                {
                    UiDraw.Text(s, at, x + i * slot, cy, slot - UiTokens.S3, UiStyle.Small, UiRole.Text2, p.SummaryLabels[i], UiFlags.Label | UiFlags.Truncate);
                    UiRole tone = i < p.SummaryTones.Length && p.SummaryTones[i] != UiRole.None ? p.SummaryTones[i] : UiRole.Text;
                    UiDraw.Text(s, at, x + i * slot, cy + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1, slot - UiTokens.S3, UiStyle.Title, tone, p.SummaryValues[i],
                        UiFlags.Truncate);
                }
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(p.Ahead) && cy + UiTokens.PairRow * 2f <= bottom)
            {
                Sentence(s, at, p.Ahead, x, cy, width, UiRole.AccentText, 2, UiStyle.Small);
            }
        }

        // The chosen day hour by hour: a bar of who is on shift against a tick where the planner needs them, short hours in red.
        private static void HourBars(UiScene s, UiAt at, CompanyLayout lay, CompanyWords w, CompanyRotaPlace p, float x, float y, float width, float bottom)
        {
            float cy = y;
            string dayTitle = p.DayTitle ?? string.Empty;
            float titleW = UiDraw.Snap(UiTextRules.Width(dayTitle, UiStyle.SectionHeader) * 1.2f + UiTokens.S1);
            if (titleW > width)
            {
                dayTitle = p.DayShort ?? dayTitle;   // the day alone where the whole will not fit
                titleW = Math.Min(width, UiDraw.Snap(UiTextRules.Width(dayTitle, UiStyle.SectionHeader) * 1.2f + UiTokens.S1));
            }
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, titleW, UiStyle.SectionHeader, UiRole.Text2, dayTitle, UiFlags.Label | UiFlags.Truncate);
            float noteW = width - titleW - UiTokens.S3;
            if (!string.IsNullOrEmpty(p.DayNote) && noteW >= UiDraw.Snap(UiTextRules.Width(p.DayNote, UiStyle.Small) + UiTokens.S1))
            {
                UiDraw.TextMiddle(s, at, x + width - noteW, cy, UiTokens.PairRow, noteW, UiStyle.Small, UiRole.AccentText, p.DayNote, UiFlags.Right | UiFlags.Truncate);
            }
            cy += UiTokens.PairRow + UiTokens.S1;
            int n = Math.Min(p.HourLabels.Length, Math.Min(p.OnShift.Length, p.Needed.Length));
            int most = 1;
            for (int i = 0; i < n; i++)
            {
                most = Math.Max(most, Math.Max(p.OnShift[i], p.Needed[i]));
            }
            float labelW = 32f, figW = 52f;
            float trackX = x + labelW + UiTokens.S2, trackW = width - labelW - figW - UiTokens.S4;
            int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - UiTokens.PairRow * 2f) / HourRow));
            for (int i = 0; i < Math.Min(n, fit); i++)
            {
                bool shortHere = p.OnShift[i] < p.Needed[i];
                UiDraw.TextMiddle(s, at, x, cy, HourRow, labelW, UiStyle.Small, UiRole.Muted, p.HourLabels[i], UiFlags.Truncate);
                float bh = UiTokens.S3;
                float by = cy + Mid(HourRow, bh);
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
                UiDraw.TextMiddle(s, at, x + width - figW, cy, HourRow, figW, UiStyle.SmallStrong, shortHere ? UiRole.DangerText : UiRole.Text, p.OnShift[i] + "/" + p.Needed[i],
                    UiFlags.Right | UiFlags.Truncate);
                cy += HourRow;
            }
            if (p.Legend.Length > 0)
            {
                cy += UiTokens.S2;
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
        }

        // The place's needs by block of hours: when, and how many on shift; Edit opens the plan's page.
        private static void NeedsBlock(UiScene s, UiAt at, CompanyLayout lay, CompanyRotaPlace p, CompanyWords w, float x, float y, float width)
        {
            CompanyButton? edit = p.Edit.Act.Do != CompanyDo.None ? new CompanyButton { Label = w.T("reallife_rota_edit", "Edit"), Act = p.Edit.Act } : (CompanyButton?)null;
            float cy = Section(s, at, lay, x, y, width, p.NeedsTitle, null, edit);
            for (int i = 0; i < p.NeedWhen.Length && i < 5; i++)
            {
                UiDraw.InfoRow(s, at, x, cy, width, p.NeedWhen[i], i < p.NeedWho.Length ? p.NeedWho[i] : string.Empty);
                cy += UiTokens.PairRow + UiTokens.S1;
            }
        }

        // The planner's ladder: each plan with what it comes to and the button that uses it, the advised one marked; and what stands beside it.
        private static void Advice(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyRotaPlace p, float x, float y, float width, float bottom, IUiKeys keys)
        {
            float cy = Section(s, at, lay, x, y, width, p.AdviceTitle);
            if (!string.IsNullOrEmpty(p.AdviceNote))
            {
                cy = Sentence(s, at, p.AdviceNote, x, cy, width, UiRole.Text2, 3);
            }
            float rowH = 72f;
            for (int i = 0; i < p.Rungs.Length && cy + rowH <= bottom; i++)
            {
                ref CompanyRung r = ref p.Rungs[i];
                int face = UiDraw.Box(s, at, x, cy, width, rowH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                UiAt inside = at.In(face);
                CompanyButton use = r.Use;
                float bw = string.IsNullOrEmpty(use.Label) ? 0f : Math.Min(UiDraw.SnapDown(width * 0.3f), UiDraw.ButtonWidth(use.Kind, use.Label, use.Icon, use.Key, keys));
                float tw = width - UiTokens.S5 * 2f - bw - UiTokens.S3 - UiDraw.Snap(UiTextRules.Width(r.Hours ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S4);
                float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                float top = cy + UiDraw.SnapDown((rowH - th - sh * (string.IsNullOrEmpty(r.Gain) ? 1f : 2f)) / 2f);
                float advisedW = r.Advised ? UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(w.T("reallife_rota_advised", "Advised"), UiStyle.SmallStrong) + UiTokens.S1) : 0f;
                float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name ?? string.Empty, UiStyle.Title) + UiTokens.S1), Math.Max(0f, tw - (advisedW > 0f ? advisedW + UiTokens.S3 : 0f)));
                UiDraw.TextMiddle(s, inside, x + UiTokens.S4, top, th, nameW, UiStyle.Title, UiRole.Text, r.Name ?? string.Empty, UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, x + UiTokens.S4, top + th, sh, tw, UiStyle.Small, UiRole.Text2, r.Line ?? string.Empty, UiFlags.Truncate);
                if (!string.IsNullOrEmpty(r.Gain))
                {
                    UiDraw.TextMiddle(s, inside, x + UiTokens.S4, top + th + sh, sh, tw, UiStyle.Small, UiRole.OkText, r.Gain, UiFlags.Truncate);
                }
                float fx = x + width - UiTokens.S4 - bw - UiTokens.S3 - UiDraw.Snap(UiTextRules.Width(r.Hours ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S4);
                UiDraw.TextMiddle(s, inside, fx, top, th, UiDraw.Snap(UiTextRules.Width(r.Hours ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S4), UiStyle.BodyStrong, UiRole.Text, r.Hours
                    ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, fx, top + th, sh, UiDraw.Snap(UiTextRules.Width(r.Hours ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S4), UiStyle.Small, UiRole.Muted, r.Net
                    ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                if (bw > 0f)
                {
                    Button(s, inside, lay, v, ref use, x + width - UiTokens.S4 - bw, cy + (rowH - UiTokens.Control) / 2f, bw, keys);
                }
                if (r.Advised)
                {
                    UiDraw.Chip(s, inside, x + UiTokens.S4 + nameW + UiTokens.S3, top + UiDraw.SnapDown((th - UiTokens.Badge - UiTokens.S1) / 2f),
                        w.T("reallife_rota_advised", "Advised"), UiRole.Ok, advisedW);
                }
                cy += rowH + UiTokens.S2;
            }
            for (int i = 0; i < p.Beside.Length && cy < bottom; i++)
            {
                cy = Block(s, at, lay, v, w, p.Beside[i], x, cy, width, bottom, keys);
            }
        }
    }
}
