using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// My job laid out as the mockup draws it: the profile with its buttons, the next shift as a hero, the ladder as a track, what the next rung needs
    /// as check lines, the performance of twelve weeks as a chart with the colleague's line beside it, and at the right the standing, this week, the
    /// recent shifts, the people on the shift and the working life. Every part is the kit's own. Game-free.
    /// </summary>
    public static class CareerMyJob
    {
        private const float LeftShare = 0.62f;

        public static void Lay(UiScene s, UiAt at, CareerJob j, UiAppModel m, UtilitiesWords w, List<UiActionModel> heads, List<UiActionModel> more, List<UiActionModel> leave,
            float x, float y, float cw, float bottom, IUiKeys keys)
        {
            // the profile's own buttons: leaving first and quiet, the main one last
            var ordered = new List<UiActionModel>(4);
            more.InsertRange(0, leave);   // leaving is a row of its own under the head: in a long language the head's buttons would stand over its status
            if (heads.Count > 1)
            {
                ordered.Add(heads[1]);
            }
            ordered.Add(heads[0]);
            y = UiAppWindow.Profile(s, at, m.Profile, ordered.ToArray(), x, y, cw, keys);
            if (more.Count > 0)
            {
                float bx = x;
                for (int i = 0; i < more.Count; i++)
                {
                    UiActionModel a = more[i];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, UiAction.Count, keys);
                    UiDraw.Button(s, at, bx, y - UiTokens.S3, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
                    bx += bw + UiTokens.S3;
                }
                y += UiTokens.Control + UiTokens.S2;
            }
            if (j.DeskFallback != null)
            {
                // No desk could be had: where it is, why not, and what stops a shift being begun from here.
                string text = j.DeskLine + " " + j.DeskFallback + (j.DeskWhy != null ? " " + j.DeskWhy : string.Empty);
                string lead = w.T("reallife_career_desk_title", "Your desk");
                WorkPanel.Note(s, at, UiIcon.Info, lead, text, x, y, cw);
                y += WorkPanel.NoteHeight(lead, text, cw) + UiTokens.S3;
            }
            float lw = UiDraw.SnapDown(cw * LeftShare), rw = cw - lw - UiTokens.S8, rx = x + lw + UiTokens.S8;
            // left: the next shift, the ladder, what the next rung needs, the twelve weeks
            float ly = y;
            ly += UiAppWindow.Hero(s, at, m.Hero, x, ly, lw) + UiTokens.S4;
            ly += UiTrack.Lay(s, at, Track(j, w), x, ly, lw) + UiTokens.S4;
            if (j.Needs.Count > 0)
            {
                ly += UiDraw.SectionHeader(s, at, x, ly, lw, w.F("reallife_work_what_needs", "What {rung} needs", "rung", j.NextRung ?? string.Empty));
                for (int i = 0; i < j.Needs.Count && ly + UiTokens.PairRow < bottom - UiTokens.S16 * 2f; i++)
                {
                    float have = string.IsNullOrEmpty(j.Needs[i].Have) ? 0f : Math.Min(lw * 0.35f, UiTextRules.Width(j.Needs[i].Have, UiStyle.Body) + UiTokens.S2);
                    UiDraw.CheckLine(s, at, x, ly, lw - have - UiTokens.S2, j.Needs[i].Words, j.Needs[i].Met);
                    if (have > 0f)
                    {
                        UiDraw.TextMiddle(s, at, x + lw - have, ly, UiTokens.PairRow, have, UiStyle.Body, UiRole.Text2, j.Needs[i].Have, UiFlags.Right | UiFlags.Truncate);
                    }
                    ly += UiTokens.PairRow + UiTokens.S1;
                }
                ly += UiTokens.S3;
            }
            // the chart's axis labels need their height: with the desk's note above it, which takes some of the column, it is drawn only where there is room for them
            if (j.Mine.Length >= 2 && bottom - ly >= UiTokens.S16 * (j.DeskFallback != null ? 3f : 2f))
            {
                UiChart.Lay(s, at, Chart(j, w), x, ly, lw, bottom - ly);
            }
            // right: standing, this week, recent shifts, the people on the shift, the working life
            float ry = UiAppWindow.Attributes(s, at, m.AttributesTitle, m.AttributeLabels, m.AttributeValues, rx, y, rw);
            ry += UiTokens.S3;
            float peersNeed = j.Peers.Count > 0 ? UiTokens.PairRow + UiTokens.S1 + j.Peers.Count * 48f + UiTokens.S3 : 0f;   // the people on the shift are all drawn: the rows above give way
            if (j.WeekHours != null || j.WeekPay != null)
            {
                ry += WorkPanel.SectionHead(s, at, rx, ry, rw, w.T("reallife_work_this_week", "This week"), null);
                if (j.WeekHours != null)
                {
                    ry += UiDraw.InfoRow(s, at, rx, ry, rw, w.T("reallife_work_week_hours", "Hours worked"), j.WeekHours) + UiTokens.S1;
                }
                if (j.WeekPay != null)
                {
                    ry += UiDraw.InfoRow(s, at, rx, ry, rw, w.T("reallife_work_week_pay", "Pay so far"), j.WeekPay) + UiTokens.S1;
                }
                if (j.PaidNote != null)
                {
                    ry += UiDraw.InfoRow(s, at, rx, ry, rw, w.T("reallife_work_paid_day", "Paid"), j.PaidNote) + UiTokens.S1;
                }
                ry += UiTokens.S3;
            }
            if (j.Shifts.Count > 0 && bottom - ry - peersNeed > UiTokens.S16 * 2f)
            {
                int most = Math.Min(Math.Min(j.Shifts.Count, 3), (int)((bottom - ry - peersNeed - UiTokens.S16 * 2f) / UiTokens.TableRow) + 1);
                var rows = new UiAppRow[Math.Max(1, most)];
                for (int i = 0; i < rows.Length; i++)
                {
                    CareerShift c = j.Shifts[j.Shifts.Count - 1 - i];
                    rows[i] = WorkBuilder.RowOf(UiIcon.Clock, UiRole.None, new[] { c.Day + ", " + c.Hours, c.Score.ToString(), c.Pay }, null, UiRole.Ok, c.Score);
                }
                ry = UiAppWindow.Table(s, at, w.T("reallife_work_recent_shifts", "Recent shifts"), new[] { Col(w.T("reallife_work_col_shift", "Shift"), 0f, false, true),
                    Col(w.T("reallife_work_col_score", "Score"), 96f, true, false), Col(w.T("reallife_work_col_pay", "Pay"), 96f, true, false) }, 1, rows, UiAppWindow.MoreCommand,
                    rx, ry, rw, Math.Min(bottom - ry - peersNeed, UiTokens.S16 * 6f)) + UiTokens.S3;
            }
            if (j.Peers.Count > 0 && bottom - ry > UiTokens.S16 * 2f)
            {
                ry += WorkPanel.SectionHead(s, at, rx, ry, rw, w.T("reallife_work_on_shift", "On shift with you"), j.Peers.Count.ToString());
                for (int i = 0; i < j.Peers.Count && ry + 48f <= bottom + 0.5f; i++)
                {
                    CareerPeer c = j.Peers[i];
                    ry += WorkPanel.PersonRow(s, at, new WorkPerson { Name = c.Name, Role = c.Role, Right = c.Score.ToString(), Ring = UiRole.Ok, Chip = c.Tag, ChipTone = c.Tone }, rx, ry, rw);
                }
                ry += UiTokens.S3;
            }
            if (j.Life.Count > 0 && bottom - ry > UiTokens.S16)
            {
                ry += UiDraw.SectionHeader(s, at, rx, ry, rw, w.T("reallife_work_working_life", "Working life"));
                for (int i = 0; i < j.Life.Count && ry + UiTokens.PairRow <= bottom; i++)
                {
                    ry += UiDraw.InfoRow(s, at, rx, ry, rw, j.Life[i].Key, j.Life[i].Value) + UiTokens.S1;
                }
            }
        }

        private static UiColumn Col(string title, float width, bool right, bool grow) => new UiColumn { Title = title, Width = width, Right = right, Grow = grow };

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
            UiIcon[] glyphs = { UiIcon.Person, UiIcon.IdBadge, UiIcon.Handshake, UiIcon.People, UiIcon.Presentation, UiIcon.Trophy };
            for (int i = 0; i < t.Steps.Length; i++)
            {
                CareerRung r = j.Rungs[i];
                t.Steps[i] = new UiTrackStep { Icon = r.State == 0 ? UiIcon.Check : glyphs[Math.Min(glyphs.Length - 1, i)], Label = r.Title, Note = r.Pay,
                    State = r.State == 0 ? UiStepState.Done : r.State == 1 ? UiStepState.Now : i == firstAhead ? UiStepState.Next : UiStepState.Later };
            }
            return t;
        }

        internal static UiChartModel Chart(CareerJob j, UtilitiesWords w)
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
            if (j.RivalName != null && j.Theirs.Length > 0)
            {
                // the weeks end together: the colleague's last n weeks, and before their record begins (they came with fewer weeks) their first
                var theirs = new float[n];
                for (int i = 0; i < n; i++)
                {
                    theirs[i] = j.Theirs[Math.Max(0, j.Theirs.Length - n + i)];
                }
                series.Add(new UiSeries { Label = j.RivalName, Values = theirs, Tint = UiRole.Warn });
            }
            return new UiChartModel { Kind = UiChartKind.Line, Title = w.T("reallife_work_performance_weeks", "Performance, 12 weeks"), Points = points, Series = series.ToArray(),
                Format = v => ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }
    }
}
