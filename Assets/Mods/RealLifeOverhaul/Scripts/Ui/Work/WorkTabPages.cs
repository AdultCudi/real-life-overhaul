using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The page every tab of Campus, Open Roles and the Marketplace stands on (Career's My job is CareerMyJob): a tab's facts laid out in the shape it
    /// takes (a squad page of a table and a detail, a catalogue of cards, a diary, a transcript, skills, children, a form, a reader), each its own
    /// layout, with its buttons, chips and rows named by what they do. One builder, so each app's sources only say what is true, never where it goes.
    /// Game-free.
    /// </summary>
    public static class WorkTabPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static WorkPage Build(WorkApp app, WorkView v, UtilitiesWords w)
        {
            var b = new WorkBuilder(w);
            if (!app.Ready || app.Tabs.Length == 0)
            {
                string title = app.OffTitle ?? w.T("reallife_work_off_title", "Nothing to show yet");
                string body = app.OffBody ?? w.T("reallife_work_off", "Load a save to see this app.");
                b.Page.Body = (s, at, x, y, cw, bottom, keys) => UiDraw.EmptyState(s, at, x, y, cw, bottom - y, UiIcon.Briefcase, title, body, null, -1, keys);
                return b.Done();
            }
            WorkTab t = app.Tabs[Math.Max(0, Math.Min(app.Tabs.Length - 1, v.Tab))];
            b.Page.Back = t.Sub;
            switch (t.Shape)
            {
                case WorkShape.Cards: return Cards(b, t, v, w);
                case WorkShape.Diary: return Diary(b, t, v, w);
                case WorkShape.Statement: return Statement(b, t, w);
                case WorkShape.Skills: return Skills(b, t, w);
                case WorkShape.Profiles: return Kids(b, t, w);
                case WorkShape.Form: return Form(b, t, v, w);
                case WorkShape.Reader: return Reader(b, t, w);
                case WorkShape.Haggle: return Haggle(b, t, w);
                default: return Table(b, t, v, w);
            }
        }

        // ------------------------------------------------------------------------------------------------- the squad page

        private static WorkPage Table(WorkBuilder b, WorkTab t, WorkView v, UtilitiesWords w)
        {
            List<WorkRow> shown = Shown(t, v, out int asideRole);
            int at = v.Row >= 0 && v.Row < shown.Count ? v.Row : shown.Count > 0 ? 0 : -1;
            WorkRow chosen = at >= 0 ? shown[at] : null;
            var m = new WorkTableModel { Stats = t.Stats.Count > 0 ? t.Stats : null, SearchHint = t.Search, Query = v.Query, Aside = t.Aside,
                AsideChosen = t.Aside != null ? asideRole : -1, UnderLead = t.NoteLead,
                UnderText = t.NoteText };
            m.Chips = Chips(b, t, v, w, out m.ChosenChip);
            m.Below = BelowOf(t, shown);
            UiDataTableModel tm = m.Table;
            tm.Columns = t.Columns;
            tm.RowCount = shown.Count;
            tm.Rows = new UiDataRow[shown.Count];
            tm.Selected = at;
            tm.Offset = v.Offset;
            tm.Reveal = v.Reveal;
            tm.EmptyIcon = t.EmptyIcon;
            tm.EmptyTitle = t.EmptyTitle;
            tm.EmptyBody = t.EmptyBody;
            tm.EmptyAction = t.EmptyActLabel;
            var opens = new WorkDo[shown.Count];
            var locates = new WorkDo[shown.Count];
            for (int i = 0; i < shown.Count; i++)
            {
                tm.Rows[i] = DataRow(t, shown[i]);
                if (shown[i].Open != null)
                {
                    opens[i] = WorkBuilder.Do(shown[i].Open.Kind, shown[i].Open.Arg != 0 ? shown[i].Open.Arg : shown[i].Id, shown[i].Open.Why, true);
                }
                if (shown[i].Locate != null)
                {
                    locates[i] = WorkBuilder.Do(shown[i].Locate.Kind, shown[i].Locate.Arg != 0 ? shown[i].Locate.Arg : shown[i].Id, shown[i].Locate.Why, true);
                }
            }
            if (t.Aside != null)
            {
                b.Page.Asides = new WorkDo[t.Aside.Rows.Count];
            }
            var panelActs = new List<UiActionModel>(4);
            if (chosen != null)
            {
                for (int i = 0; i < chosen.Acts.Count; i++)
                {
                    panelActs.Add(b.ModelAt(b.Act(chosen.Acts[i], chosen.Id, i)));
                }
                m.Detail = chosen;
            }
            var pageActs = new List<UiActionModel>(2);
            for (int i = 0; i < t.Acts.Count; i++)
            {
                pageActs.Add(b.ModelAt(b.Act(t.Acts[i], 0, chosen == null ? i : i + 2)));
            }
            m.Amounts = t.Amounts.ToArray();
            b.Page.Amounts = m.Amounts;
            b.Page.Rows = opens;
            b.Page.Locates = locates;
            b.Page.Chosen = at;
            b.Page.Table = tm;
            b.Page.Typing = !string.IsNullOrEmpty(t.Search);
            if (t.EmptyAct != null)
            {
                b.Page.Empty = WorkBuilder.Do(t.EmptyAct.Kind, t.EmptyAct.Arg, t.EmptyAct.Why, true);
            }
            WorkPage page = b.Done();
            m.Actions = panelActs.ToArray();
            m.PageActs = pageActs.ToArray();
            m.Why = page.Why;
            page.Body = (s, ats, x, y, cw, bottom, keys) => WorkTable.Lay(s, ats, m, x, y, cw, bottom, keys);
            return page;
        }

        /// <summary>The row a table or a catalogue shows its detail for under this view, or null: what a source steps a choice of its own for.</summary>
        public static WorkRow ChosenRow(WorkTab t, WorkView v)
        {
            List<WorkRow> shown = Shown(t, v, out int _);
            int at = v.Row >= 0 && v.Row < shown.Count ? v.Row : shown.Count > 0 ? 0 : -1;
            return at >= 0 ? shown[at] : null;
        }

        // The bands under a catalogue or a table speak of the rows it shows: made again from the rows a chip or a search leaves, not from the whole list.
        private static List<WorkFormBand> BelowOf(WorkTab t, List<WorkRow> shown)
        {
            if (t.Bands == null || shown.Count == t.Rows.Count)
            {
                return t.Below;
            }
            var bands = new List<WorkFormBand>(3);
            t.Bands(bands, shown);
            return bands;
        }

        // The rows the chips, the search and the chosen role of an aside leave.
        private static List<WorkRow> Shown(WorkTab t, WorkView v, out int asideRole)
        {
            asideRole = t.Aside != null ? Math.Max(0, Math.Min(Math.Max(0, t.Aside.Rows.Count - 1), v.Aside)) : -1;
            int group = t.Aside != null && t.Aside.Rows.Count > 0 ? t.Aside.Rows[asideRole].Id : int.MinValue;
            var shown = new List<WorkRow>(t.Rows.Count);
            string query = v.Query ?? string.Empty;
            for (int i = 0; i < t.Rows.Count; i++)
            {
                WorkRow r = t.Rows[i];
                if (t.Aside != null && r.Group != group)
                {
                    continue;
                }
                if (v.Filter != 0 && !Counts(t, r, v.Filter))
                {
                    continue;
                }
                if (query.Length > 0 && !Matches(r, query))
                {
                    continue;
                }
                shown.Add(r);
            }
            return shown;
        }

        private static bool Counts(WorkTab t, WorkRow r, int chip)
        {
            return t.ChipsByMask ? (r.Mask >> (chip - 1) & 1) != 0 : r.Group == chip - 1;
        }

        private static bool Matches(WorkRow r, string query)
        {
            return Has(r.Name, query) || Has(r.Sub, query) || Has(r.Seller, query) || Has(r.Person, query) || Has(r.Line, query);
        }

        private static bool Has(string text, string query) => text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        // "All 24", "Open now 17": the counts are the rows each chip holds, so a chip never promises more than it shows.
        private static UiActionModel[] Chips(WorkBuilder b, WorkTab t, WorkView v, UtilitiesWords w, out int chosen)
        {
            chosen = Math.Max(0, Math.Min(t.Chips.Length, v.Filter));
            if (t.Chips.Length == 0)
            {
                return new UiActionModel[0];
            }
            var models = new UiActionModel[t.Chips.Length + 1];
            int[] counts = new int[t.Chips.Length + 1];
            int group = t.Aside != null && t.Aside.Rows.Count > 0 ? t.Aside.Rows[Math.Max(0, Math.Min(t.Aside.Rows.Count - 1, v.Aside))].Id : int.MinValue;
            for (int i = 0; i < t.Rows.Count; i++)
            {
                if (t.Aside != null && t.Rows[i].Group != group)
                {
                    continue;
                }
                counts[0]++;
                for (int c = 1; c <= t.Chips.Length; c++)
                {
                    counts[c] += Counts(t, t.Rows[i], c) ? 1 : 0;
                }
            }
            for (int c = 0; c <= t.Chips.Length; c++)
            {
                string name = (c == 0 ? t.AllWord ?? w.T("reallife_work_filter_all_word", "All") : t.Chips[c - 1]) + " " + counts[c].ToString(Inv);
                b.Chip(name, v.Filter == c, WorkBuilder.Do(WorkKinds.Filter, c, null, true));
                models[c] = b.ModelAt(b.Count - 1);
            }
            return models;
        }

        private static UiDataRow DataRow(WorkTab t, WorkRow r)
        {
            int n = t.Columns.Length;
            var row = new UiDataRow { Cells = new string[n], Values = new int[n], Tones = new UiRole[n], Sub = r.Sub, Hue = UiColour.HueOf(r.Person ?? r.Name ?? string.Empty), Ring = r.Ring,
                Pictures = new string[n] };
            for (int c = 0; c < n; c++)
            {
                row.Values[c] = c < r.Values.Length ? r.Values[c] : -1;
                row.Tones[c] = c < r.Tones.Length ? r.Tones[c] : UiRole.None;
                row.Cells[c] = c < r.Cells.Length ? r.Cells[c] : null;
                UiCellKind kind = t.Columns[c].Kind;
                if (c == 0)
                {
                    row.Cells[0] = kind == UiCellKind.Person && !string.IsNullOrEmpty(r.Person) ? r.Person : r.Name;
                    if (kind == UiCellKind.Tile)
                    {
                        row.Values[0] = (int)r.Icon;
                        row.Tones[0] = r.Tint;
                        row.Pictures[0] = r.Picture;
                    }
                }
                if (kind == UiCellKind.Action)
                {
                    row.Values[c] = r.Locate != null ? (int)UiIcon.Locate : 0;
                }
                if (kind == UiCellKind.Chip && string.IsNullOrEmpty(row.Cells[c]))
                {
                    row.Cells[c] = r.Status;
                    row.Tones[c] = r.Tone;
                }
            }
            return row;
        }

        // ------------------------------------------------------------------------------------------------- the catalogue

        // The rows by their own figure, the lowest first, each in its place among those before it: two rows of one figure keep
        // the order they came in, which List.Sort does not promise. A catalogue holds a few dozen rows, sorted on a press.
        private static void ByOrder(List<WorkRow> rows)
        {
            for (int i = 1; i < rows.Count; i++)
            {
                WorkRow row = rows[i];
                int j = i - 1;
                while (j >= 0 && rows[j].Order > row.Order)
                {
                    rows[j + 1] = rows[j];
                    j--;
                }
                rows[j + 1] = row;
            }
        }

        private static WorkPage Cards(WorkBuilder b, WorkTab t, WorkView v, UtilitiesWords w)
        {
            List<WorkRow> shown = Shown(t, v, out int _);
            // The sort choice's other order: by each row's own figure (a listing's asking price), the lowest first, the tab's own order between two alike.
            bool byOrder = v.Sort == 1 && t.SortByOrder != null;
            if (byOrder)
            {
                ByOrder(shown);
            }
            int at = v.Row >= 0 && v.Row < shown.Count ? v.Row : shown.Count > 0 ? 0 : -1;
            WorkRow chosen = at >= 0 ? shown[at] : null;
            var m = new WorkCardsModel { SearchHint = t.Search, Query = v.Query, Cards = shown.ToArray(), Chosen = at, Offset = v.Offset, Reveal = v.Reveal, EmptyTitle = t.EmptyTitle,
                EmptyBody = t.EmptyBody, EmptyIcon = t.EmptyIcon, SortLabel = byOrder ? t.SortByOrder : t.SortWord, Sorts = t.SortByOrder != null };
            m.Chips = Chips(b, t, v, w, out m.ChosenChip);
            m.Below = BelowOf(t, shown);
            var panelActs = new List<UiActionModel>(4);
            if (chosen != null)
            {
                for (int i = 0; i < chosen.Acts.Count; i++)
                {
                    panelActs.Add(b.ModelAt(b.Act(chosen.Acts[i], chosen.Id, i)));
                }
                m.Detail = chosen;
            }
            m.Amounts = t.Amounts.ToArray();
            b.Page.Amounts = m.Amounts;
            b.Page.Rows = new WorkDo[shown.Count];
            b.Page.Chosen = at;
            b.Page.Cards = m;
            b.Page.Typing = !string.IsNullOrEmpty(t.Search);
            WorkPage page = b.Done();
            m.Actions = panelActs.ToArray();
            m.Why = page.Why;
            page.Body = (s, ats, x, y, cw, bottom, keys) => WorkCards.Lay(s, ats, m, x, y, cw, bottom, keys);
            return page;
        }

        // ------------------------------------------------------------------------------------------------- Studying

        private static WorkPage Diary(WorkBuilder b, WorkTab t, WorkView v, UtilitiesWords w)
        {
            var cardActs = new int[Math.Max(4, t.Rows.Count * 4)];
            for (int i = 0; i < cardActs.Length; i++)
            {
                cardActs[i] = -1;
            }
            for (int i = 0; i < t.Rows.Count && i < 4; i++)
            {
                for (int k = 0; k < t.Rows[i].Acts.Count && k < 4; k++)
                {
                    cardActs[i * 4 + k] = b.Act(t.Rows[i].Acts[k], t.Rows[i].Id, 2);
                }
            }
            var pageActs = new List<UiActionModel>(2);
            for (int i = 0; i < t.Acts.Count; i++)
            {
                pageActs.Add(b.ModelAt(b.Act(t.Acts[i], 0, i)));
            }
            b.Page.Diary = t.Diary != null ? t.Diary.Month : null;
            WorkPage page = b.Done();
            UiActionModel[] actions = page.Model.Actions;
            page.Body = (s, ats, x, y, cw, bottom, keys) =>
            {
                float cy = Buttons(s, ats, pageActs, x, y, cw, keys);
                if (t.Rows.Count > 0)
                {
                    WorkStudy.Diary(s, ats, t, actions, cardActs, x, cy, cw, bottom, keys);
                }
                else if (t.Diary != null)
                {
                    UiDiary.Lay(s, ats, t.Diary.Month, x, cy, cw, bottom - cy, keys);
                }
            };
            return page;
        }

        // A row of the page's own buttons at its right; the y under it, or the same y with none.
        private static float Buttons(UiScene s, UiAt at, List<UiActionModel> list, float x, float y, float w, IUiKeys keys)
        {
            if (list.Count == 0)
            {
                return y;
            }
            float right = x + w;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                UiActionModel a = list[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            return y + UiTokens.Control + UiTokens.S4;
        }

        // ------------------------------------------------------------------------------------------------- Results, Skills, Family

        private static WorkPage Statement(WorkBuilder b, WorkTab t, UtilitiesWords w)
        {
            WorkStatement m = t.Statement;
            WorkPage page = b.Done();
            page.Statement = m;
            page.Body = (s, ats, x, y, cw, bottom, keys) => WorkStudy.Statement(s, ats, m, x, y, cw, bottom, keys);
            return page;
        }

        private static WorkPage Skills(WorkBuilder b, WorkTab t, UtilitiesWords w)
        {
            WorkPage page = b.Done();
            page.Body = (s, ats, x, y, cw, bottom, keys) => WorkStudy.Skills(s, ats, t, x, y, cw, bottom, keys);
            return page;
        }

        private static WorkPage Kids(WorkBuilder b, WorkTab t, UtilitiesWords w)
        {
            var kidActs = new int[Math.Max(4, t.Kids.Count * 4)];
            for (int i = 0; i < kidActs.Length; i++)
            {
                kidActs[i] = -1;
            }
            for (int i = 0; i < t.Kids.Count; i++)
            {
                for (int k = 0; k < t.Kids[i].Acts.Count && k < 4; k++)
                {
                    kidActs[i * 4 + k] = b.Act(t.Kids[i].Acts[k], i, k);
                }
            }
            WorkFormModel form = t.Form;
            WireForm(b, form);
            WorkAmount[] amounts = t.Amounts.ToArray();
            b.Page.Amounts = amounts;
            WorkPage page = b.Done();
            UiActionModel[] actions = page.Model.Actions;
            page.Body = (s, ats, x, y, cw, bottom, keys) =>
            {
                float end = y;
                if (t.Kids.Count > 0)
                {
                    end = WorkStudy.Kids(s, ats, t, actions, kidActs, x, y, cw, bottom, keys) + UiTokens.S4;
                }
                else
                {
                    float eh = Math.Min(bottom - y, UiTokens.S16 * 3f);
                    UiDraw.EmptyState(s, ats, x, y, cw, eh, UiIcon.Heart, t.EmptyTitle, t.EmptyBody, null, -1, keys);
                    end = y + eh + UiTokens.S4;
                }
                if (form != null)
                {
                    WorkForm.Lay(s, ats, form, actions, amounts, x, end, cw, bottom, keys);
                }
            };
            return page;
        }

        // ------------------------------------------------------------------------------------------------- forms, readers

        private static void WireForm(WorkBuilder b, WorkFormModel form)
        {
            if (form == null)
            {
                return;
            }
            WireBands(b, form.Left);
            WireBands(b, form.Right);
        }

        private static void WireBands(WorkBuilder b, List<WorkFormBand> bands)
        {
            for (int i = 0; i < bands.Count; i++)
            {
                WorkFormBand band = bands[i];
                for (int k = 0; k < band.Rows.Count; k++)
                {
                    WorkFormRow r = band.Rows[k];
                    if (r.Act != null)
                    {
                        r.Why = r.Act.Why;   // said in the row, beside the button that cannot be pressed
                        r.ActAt = b.Button(r.Act.Label, r.Act.Style, r.Act.Icon, UiAction.Count, WorkBuilder.Do(r.Act.Kind, r.Act.Arg, r.Act.Why, r.Act.Opens));
                        for (int m = 0; m < r.More.Count; m++)
                        {
                            b.Button(r.More[m].Label, r.More[m].Style, r.More[m].Icon, UiAction.Count, WorkBuilder.Do(r.More[m].Kind, r.More[m].Arg, r.More[m].Why, r.More[m].Opens));
                        }
                        if (r.Act2 != null)
                        {
                            b.Button(r.Act2.Label, r.Act2.Style, r.Act2.Icon, UiAction.Count, WorkBuilder.Do(r.Act2.Kind, r.Act2.Arg, r.Act2.Why, r.Act2.Opens));   // the next place after the first
                        }
                    }
                }
                if (band.Foot != null)
                {
                    band.FootWhy = band.Foot.Why;
                    band.FootAt = b.Button(band.Foot.Label, band.Foot.Style, band.Foot.Icon, UiAction.Count, WorkBuilder.Do(band.Foot.Kind, band.Foot.Arg, band.Foot.Why, band.Foot.Opens));
                }
            }
        }

        private static WorkPage Form(WorkBuilder b, WorkTab t, WorkView v, UtilitiesWords w)
        {
            WorkFormModel form = t.Form ?? new WorkFormModel();
            var pageActs = new List<UiActionModel>(3);
            for (int i = 0; i < t.Acts.Count; i++)
            {
                pageActs.Add(b.ModelAt(b.Act(t.Acts[i], 0, i)));
            }
            WireForm(b, form);
            WorkAmount[] amounts = t.Amounts.ToArray();
            b.Page.Amounts = amounts;
            WorkPage page = b.Done();
            UiActionModel[] actions = page.Model.Actions;
            page.Body = (s, ats, x, y, cw, bottom, keys) =>
            {
                float top = y, afterTabs = y;
                if (t.PersonTabs.Length > 0)
                {
                    afterTabs = PageTabs(s, ats, t.PersonTabs, v.PageTab, x, top);   // the tabs and the page's buttons stand in one row
                }
                float cy = Math.Max(afterTabs, Buttons(s, ats, pageActs, x, top, cw, keys));
                WorkForm.Lay(s, ats, form, actions, amounts, x, cy, cw, bottom, keys);
            };
            return page;
        }

        // The person page's tabs: File, Career and Contract, as chips over the page they show.
        private static float PageTabs(UiScene s, UiAt at, string[] names, int shown, float x, float y)
        {
            float cx = x, h = UiTokens.ControlSmall + UiTokens.S1;
            for (int i = 0; i < names.Length; i++)
            {
                var a = new UiActionModel { Label = names[i], Kind = UiButtonKind.Chip, Command = WorkCmd.PageTab, Arg = i };
                cx += WorkTable.Chip(s, at, a, i == shown, cx, y, h) + UiTokens.S2;
            }
            return y + h + UiTokens.S3;
        }

        private static WorkPage Haggle(WorkBuilder b, WorkTab t, UtilitiesWords w)
        {
            WorkRow r = t.Haggle;
            var choices = new List<UiActionModel>(5);
            for (int i = 0; i < r.Acts.Count; i++)
            {
                WorkAct a = r.Acts[i];
                int at = b.Button(a.Label, a.Style, a.Icon, a.Choice ? (UiAction)((int)UiAction.Pick1 + Math.Min(8, choices.Count)) : UiAction.Count,
                    WorkBuilder.Do(a.Kind, a.Arg != 0 ? a.Arg : r.Id, a.Why, a.Opens));
                choices.Add(b.ModelAt(at));
            }
            WorkAmount[] amounts = t.Amounts.ToArray();
            b.Page.Amounts = amounts;
            WorkPage page = b.Done();
            string why = page.Why;
            page.Body = (s, ats, x, y, cw, bottom, keys) => WorkHaggle.Lay(s, ats, r, r.Acts, choices.ToArray(), amounts, why, x, y, cw, bottom, keys);
            return page;
        }

        private static WorkPage Reader(WorkBuilder b, WorkTab t, UtilitiesWords w)
        {
            UiReaderModel m = t.Reader;
            var pageActs = new List<UiActionModel>(2);
            for (int i = 0; i < t.Acts.Count; i++)
            {
                pageActs.Add(b.ModelAt(b.Act(t.Acts[i], 0, i)));
            }
            b.Page.Reader = m;
            WorkPage page = b.Done();
            string why = page.Why;
            page.Body = (s, ats, x, y, cw, bottom, keys) =>
            {
                float foot = pageActs.Count > 0 ? UiTokens.Control + UiTokens.S3 : 0f;
                UiReader.Lay(s, ats, m, x, y, cw, bottom - y - foot, keys);
                if (pageActs.Count > 0)
                {
                    float right = x + cw, by = bottom - UiTokens.Control;
                    if (why != null)
                    {
                        UiDraw.TextMiddle(s, ats, x, by, UiTokens.Control, cw * 0.5f, UiStyle.Small, UiRole.WarnText, why, UiFlags.Truncate);
                    }
                    for (int i = pageActs.Count - 1; i >= 0; i--)
                    {
                        UiActionModel a = pageActs[i];
                        float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                        right -= bw;
                        UiDraw.Button(s, ats, right, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                        right -= UiTokens.S3;
                    }
                }
            };
            return page;
        }
    }

    /// <summary>The action numbers every work app shares, taken by the screen itself; each app's own start at <see cref="AppsFirst"/>.</summary>
    public static class WorkKinds
    {
        public const int Filter = 1, GoTab = 2, Back = 3;
        public const int AppsFirst = 100;
    }
}
