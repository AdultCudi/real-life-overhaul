using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One worked shift of the recent shifts: the day, the hours, the score that day and the pay.</summary>
    public sealed class CareerShift { public string Day, Hours, Pay; public int Score; }

    /// <summary>
    /// My job laid out as the mockup draws it: the profile, the next shift as a hero, the ladder as a track, what the next rung needs as check
    /// lines, the performance of twelve weeks as a chart with the colleague's line beside it, and at the right the standing, this week, the recent
    /// shifts and the people on the shift. Every part is the kit's own. Game-free.
    /// </summary>
    public static class CareerMyJob
    {
        private const float LeftShare = 0.62f;

        public static void Lay(UiScene s, UiAt at, CareerJob j, UiAppModel m, UtilitiesWords w, List<int> head, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            var top = new List<UiActionModel>(3);
            var more = new List<UiActionModel>(6);
            for (int i = 0; i < m.Actions.Length; i++)
            {
                (head.Contains(i) ? top : more).Add(m.Actions[i]);
            }
            y = UiAppWindow.Profile(s, at, m.Profile, top.ToArray(), x, y, cw, keys);
            if (more.Count > 0)
            {
                float bx = x;
                for (int i = 0; i < more.Count; i++)
                {
                    UiActionModel a = more[i];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    UiDraw.Button(s, at, bx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                    bx += bw + UiTokens.S3;
                }
                y += UiTokens.Control + UiTokens.S4;
            }
            float lw = UiDraw.SnapDown(cw * LeftShare), rw = cw - lw - UiTokens.S6, rx = x + lw + UiTokens.S6;
            // left: the next shift, the ladder, what the next rung needs, the twelve weeks
            float ly = y;
            ly += UiAppWindow.Hero(s, at, m.Hero, x, ly, lw) + UiTokens.S4;
            ly += UiTrack.Lay(s, at, Track(j, w), x, ly, lw) + UiTokens.S4;
            if (j.Needs.Count > 0)
            {
                ly += UiDraw.SectionHeader(s, at, x, ly, lw, w.F("reallife_work_what_needs", "What {rung} needs", "rung", j.NextRung ?? string.Empty));
                for (int i = 0; i < j.Needs.Count && ly + UiTokens.Control < bottom - UiTokens.S16 * 2f; i++)
                {
                    ly += UiTokens.S1 + UiDraw.CheckLine(s, at, x, ly, lw, j.Needs[i].Words + (j.Needs[i].Have != null ? ", " + j.Needs[i].Have : string.Empty), j.Needs[i].Met);
                }
                ly += UiTokens.S3;
            }
            if (j.Mine.Length >= 2 && bottom - ly >= UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, Chart(j, w), x, ly, lw, bottom - ly);
            }
            // right: standing, this week, recent shifts, the people on the shift
            float ry = UiAppWindow.Attributes(s, at, m.AttributesTitle, m.AttributeLabels, m.AttributeValues, rx, y, rw);
            if (j.WeekHours != null || j.WeekPay != null)
            {
                ry += UiDraw.SectionHeader(s, at, rx, ry, rw, w.T("reallife_work_this_week", "This week"));
                if (j.WeekHours != null)
                {
                    ry += UiDraw.InfoRow(s, at, rx, ry, rw, w.T("reallife_work_week_hours", "Hours worked"), j.WeekHours) + UiTokens.S1;
                }
                if (j.WeekPay != null)
                {
                    ry += UiDraw.InfoRow(s, at, rx, ry, rw, w.T("reallife_work_week_pay", "Pay so far"), j.WeekPay) + UiTokens.S1;
                }
                ry += UiTokens.S3;
            }
            if (j.Shifts.Count > 0 && bottom - ry > UiTokens.S16 * 4f)
            {
                var rows = new UiAppRow[Math.Min(j.Shifts.Count, 4)];
                for (int i = 0; i < rows.Length; i++)
                {
                    CareerShift c = j.Shifts[j.Shifts.Count - 1 - i];
                    rows[i] = WorkBuilder.Row(UiIcon.Clock, UiRole.None, new[] { c.Day + " " + c.Hours, c.Score.ToString(), c.Pay }, null, UiRole.Ok, c.Score);
                }
                ry = UiAppWindow.Table(s, at, w.T("reallife_work_recent_shifts", "Recent shifts"), new[] { WorkBuilder.Col(w.T("reallife_work_col_shift", "Shift"), 0f, false, true),
                    WorkBuilder.Col(w.T("reallife_work_col_score", "Score"), 96f, true), WorkBuilder.Col(w.T("reallife_work_col_pay", "Pay"), 96f, true) }, 1, rows, UiAppWindow.MoreCommand,
                    rx, ry, rw, Math.Min(bottom - ry, UiTokens.S16 * 6f)) + UiTokens.S3;
            }
            if (m.People.Length > 0 && bottom - ry > UiTokens.S16 * 3f)
            {
                UiAppWindow.People(s, at, m.PeopleTitle, m.PeopleCount, m.People, null, m.Note, m.NoteTone, null, rx, ry, rw, bottom - ry);
            }
        }

        private static UiTrackModel Track(CareerJob j, UtilitiesWords w)
        {
            var t = new UiTrackModel { Title = w.F("reallife_work_ladder_at", "The ladder at {employer}", "employer", j.Employer), Steps = new UiTrackStep[j.Rungs.Count],
                StateWords = new[] { w.T("reallife_work_step_done", "Done"), w.T("reallife_work_rung_you", "You"), w.T("reallife_work_rung_next", "Next"),
                    w.T("reallife_work_step_locked", "Locked"), w.T("reallife_work_step_later", "Later") } };
            int firstAhead = -1;
            for (int i = 0; i < j.Rungs.Count && firstAhead < 0; i++)
            {
                firstAhead = j.Rungs[i].State == 2 ? i : -1;
            }
            for (int i = 0; i < t.Steps.Length; i++)
            {
                CareerRung r = j.Rungs[i];
                t.Steps[i] = new UiTrackStep { Icon = UiIcon.Briefcase, Label = r.Title, Note = r.Pay,
                    State = r.State == 0 ? UiStepState.Done : r.State == 1 ? UiStepState.Now : i == firstAhead ? UiStepState.Next : UiStepState.Later };
            }
            return t;
        }

        private static UiChartModel Chart(CareerJob j, UtilitiesWords w)
        {
            int n = j.Mine.Length;
            var points = new string[n];
            var mine = new float[n];
            for (int i = 0; i < n; i++)
            {
                points[i] = (i % 4 == 0 || i == n - 1) ? w.F("reallife_work_week_n", "Week {n}", "n", (i + 1).ToString()) : string.Empty;
                mine[i] = j.Mine[i];
            }
            var series = new List<UiSeries> { new UiSeries { Label = w.T("reallife_work_you_line", "You"), Values = mine, Tint = UiRole.Accent } };
            if (j.RivalName != null && j.Theirs.Length == n)
            {
                var theirs = new float[n];
                for (int i = 0; i < n; i++)
                {
                    theirs[i] = j.Theirs[i];
                }
                series.Add(new UiSeries { Label = j.RivalName, Values = theirs, Tint = UiRole.Warn });
            }
            return new UiChartModel { Kind = UiChartKind.Line, Title = w.T("reallife_work_performance_weeks", "Performance, 12 weeks"), Points = points, Series = series.ToArray(),
                Format = v => ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }
    }
}
