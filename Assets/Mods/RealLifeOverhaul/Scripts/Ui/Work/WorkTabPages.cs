using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A button of a row's detail: what it is called, how it is drawn and the app's own action number behind it.</summary>
    public sealed class WorkAct
    {
        public int Kind;
        public int Arg;
        public string Label;
        public UiButtonKind Style = UiButtonKind.Secondary;
        public UiIcon Icon = UiIcon.Check;
        public string Why;          // a reason it cannot be done now; the button is drawn disabled and the reason said
        public bool Opens;          // it opens a page or a place and changes nothing else
    }

    /// <summary>A person under a page: a face, what they are and a record out of 100.</summary>
    public sealed class WorkPerson
    {
        public string Name, Role;
        public string Label;        // what the record is of ("Craft")
        public int Score = -1;
        public string Label2;
        public int Score2 = -1;
        public UiIcon Icon = UiIcon.Person;
        public WorkAct Open;        // pressing them opens their page; null for none
    }

    /// <summary>One row of a tab's table and the detail it shows when chosen: a sentence, check lines and the buttons.</summary>
    public sealed class WorkRow
    {
        public int Id;
        public UiIcon Icon = UiIcon.Briefcase;
        public UiRole Tint = UiRole.None;
        public string[] Cells = new string[0];
        public string Status;
        public UiRole Tone = UiRole.Ok;
        public int Grade = -1;
        public string Detail;                                  // what the lead says when this row is chosen
        public readonly List<WorkAct> Acts = new List<WorkAct>(4);
        public readonly List<WorkPerson> People = new List<WorkPerson>(4);
        public string PeopleTitle;
        public readonly List<KeyValuePair<string, string>> Facts = new List<KeyValuePair<string, string>>(6);   // label and value pairs the catalogue's detail lists
        public readonly List<SplitNeed> Needs = new List<SplitNeed>(6);   // requirements shown as check lines in the detail
        public string NeedsTitle;
        public int Group = -1;                                 // a filter chip it belongs to (by the tab's chips); -1: every one
    }

    /// <summary>One tab of an app: its profile or lead, figures, attributes, its table and the people under it. Everything a page is made of,
    /// game-free.</summary>
    public sealed class WorkTab
    {
        public string Lead;                                    // a sentence, when the chosen row has none
        public UiProfileModel Profile;
        public UiHeroModel Hero;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public string AttrTitle;
        public string[] AttrLabels = new string[0];
        public int[] AttrValues = new int[0];
        public string TableTitle;
        public UiColumn[] Columns = new UiColumn[0];
        public int GradeColumn = -1;
        public readonly List<WorkRow> Rows = new List<WorkRow>(24);
        public string[] Chips = new string[0];                // filter chips above the table, each its own group
        public readonly List<WorkAct> Acts = new List<WorkAct>(3);   // buttons of the page itself (a new ad, a search)
        public string PeopleTitle, PeopleCount;
        public readonly List<WorkPerson> People = new List<WorkPerson>(8);
        public string Note;
        public UiRole NoteTone = UiRole.Warn;
        public UiChartModel Chart;                             // a line over the tab's table (the average by quarter), laid by WorkResults
        public bool Grid;                                      // laid as a catalogue of cards with the chosen one's detail beside them
        public string GridKicker;                              // what the chosen thing is, over its detail
        public bool Sub;                                       // a sub page (a person, a haggle): Back steps out of it
    }

    /// <summary>An app of the work apps in facts: its sections and a tab for each. Game-free.</summary>
    public sealed class WorkApp
    {
        public bool Ready;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public WorkTab[] Tabs = new WorkTab[0];
        public string OffTitle, OffBody;
    }

    /// <summary>
    /// The page every tab of Campus, Open Roles and the Marketplace stands on (Career's own pages are CareerPages): the app window's blocks
    /// from a tab's facts, the chosen row's detail as the lead and its buttons, its people under the table, and filter chips for the groups.
    /// One builder, so each app's sources only say what is true, never where it goes. Game-free.
    /// </summary>
    public static class WorkTabPages
    {
        public static WorkPage Build(WorkApp app, WorkView v, UtilitiesWords w)
        {
            var b = new WorkBuilder(w);
            if (!app.Ready || app.Tabs.Length == 0)
            {
                b.About(UiIcon.Briefcase, null, w.T("reallife_work_nothing_here", "Nothing here yet"), app.OffBody ?? w.T("reallife_work_off",
                    "Load a save to see this app."));
                return b.Done();
            }
            WorkTab t = app.Tabs[Math.Max(0, Math.Min(app.Tabs.Length - 1, v.Tab))];
            b.Page.Back = t.Sub;
            UiAppModel m = b.M;
            m.Profile = t.Profile;
            m.Hero = t.Hero;
            m.Figures = t.Figures;
            m.AttributesTitle = t.AttrTitle;
            m.AttributeLabels = t.AttrLabels;
            m.AttributeValues = t.AttrValues;
            var shown = new List<WorkRow>(t.Rows.Count);
            for (int i = 0; i < t.Rows.Count; i++)
            {
                if (v.Filter == 0 || t.Rows[i].Group < 0 || t.Rows[i].Group + 1 == v.Filter)
                {
                    shown.Add(t.Rows[i]);
                }
            }
            int at = v.Row >= 0 && v.Row < shown.Count ? v.Row : shown.Count > 0 && t.Profile == null ? 0 : -1;
            WorkRow chosen = at >= 0 ? shown[at] : null;
            if (t.Profile == null && t.Hero == null)
            {
                if (chosen != null && shown.Count > 0)
                {
                    b.Page.Detail = Detail(chosen);
                }
                else if (!string.IsNullOrEmpty(t.Lead) && shown.Count == 0 && t.AttrLabels.Length == 0 && t.Figures.Length == 0)
                {
                    b.About(UiIcon.Search, null, w.T("reallife_work_nothing_here", "Nothing here yet"), t.Lead);
                }
            }
            if (chosen == null && shown.Count > 0)
            {
                b.Lead(t.Lead);
            }
            for (int i = 0; i < t.Chips.Length; i++)
            {
                b.Chip(t.Chips[i], v.Filter == i, WorkBuilder.Do(WorkKinds.Filter, i, null, true));
            }
            if (chosen != null)
            {
                AddActs(b, chosen.Acts, chosen.Id);
            }
            AddActs(b, t.Acts, 0);
            if (t.Grid && shown.Count > 0)
            {
                int gi = at >= 0 ? at : 0;
                UiCatalogueModel cat = WorkGrid.Model(shown, gi, w, t.GridKicker, out WorkDo[] gridActs);
                b.Page.GridActs = gridActs;
                b.Page.Model.Hero = null;
                b.Page.Model.Next = null;
                b.Page.Body = (s, ats, x, y, cw, bottom, keys) => WorkGrid.Lay(s, ats, cat, b.M, x, y, cw, bottom, keys);
                b.Page.Chosen = gi;
                return b.Done();
            }
            if (shown.Count > 0)
            {
                var rows = new UiAppRow[shown.Count];
                for (int i = 0; i < rows.Length; i++)
                {
                    WorkRow r = shown[i];
                    rows[i] = WorkBuilder.Row(r.Icon, r.Tint, r.Cells, r.Status, r.Tone, r.Grade);
                }
                b.Table(null, t.Columns, t.GradeColumn, rows, new WorkDo[rows.Length], at);
                if (b.Page.Detail != null)
                {
                    SplitDetail detail = b.Page.Detail;
                    b.Page.Body = (s, ats, x, y, cw, bottom, keys) => WorkSplit.Lay(s, ats, b.M, detail, at, x, y, cw, bottom, keys);
                }
            }
            List<WorkPerson> people = chosen != null && chosen.People.Count > 0 ? chosen.People : t.People;
            if (people.Count > 0 && people.TrueForAll(x => x.Score2 < 0))
            {
                // people with a single figure are a short table (name, what they are, the figure): the people block always draws two bars
                var more = new UiAppRow[people.Count];
                for (int i = 0; i < more.Length; i++)
                {
                    WorkPerson p = people[i];
                    more[i] = WorkBuilder.Row(p.Icon, UiRole.None, new[] { p.Name, p.Role, p.Score >= 0 ? p.Score.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-" });
                }
                b.M.MoreTitle = chosen != null && chosen.People.Count > 0 && chosen.PeopleTitle != null ? chosen.PeopleTitle : t.PeopleTitle;
                b.M.MoreColumns = new[] { WorkBuilder.Col(people[0].Label ?? string.Empty, 0f, false, true), WorkBuilder.Col(string.Empty, 184f), WorkBuilder.Col(string.Empty, 96f, true) };
                b.M.MoreRows = more;
                b.Page.MoreRows = new WorkDo[more.Length];
            }
            else if (people.Count > 0)
            {
                var list = new UiPersonRow[people.Count];
                var opens = new WorkDo[people.Count];
                for (int i = 0; i < list.Length; i++)
                {
                    WorkPerson p = people[i];
                    list[i] = new UiPersonRow { Name = p.Name, Hue = UiColour.HueOf(p.Name ?? string.Empty), RoleIcon = p.Icon, Role = p.Role, FirstLabel = p.Label, First = p.Score,
                        SecondLabel = p.Label2, Second = p.Score2 };
                    opens[i] = p.Open != null ? WorkBuilder.Do(p.Open.Kind, p.Open.Arg, p.Open.Why, p.Open.Opens) : default(WorkDo);
                }
                b.People(chosen != null && chosen.People.Count > 0 && chosen.PeopleTitle != null ? chosen.PeopleTitle : t.PeopleTitle, t.PeopleCount, list, opens);
                m.Note = t.Note;
                m.NoteTone = t.NoteTone;
            }
            if (t.Chart != null && t.Profile != null)
            {
                UiChartModel chart = t.Chart;
                b.Page.Body = (s, ats, x, y, cw, bottom, keys) => WorkResults.Lay(s, ats, b.M, chart, x, y, cw, bottom, keys);
            }
            return b.Done();
        }

        private static SplitDetail Detail(WorkRow r)
        {
            var d = new SplitDetail { Icon = r.Icon, Title = r.Cells.Length > 0 ? r.Cells[0] : string.Empty, Status = r.Status, Tone = r.Tone,
                Sentence = r.Detail, PeopleTitle = r.PeopleTitle,
                NeedsTitle = r.NeedsTitle };
            d.Facts.AddRange(r.Facts);
            d.Needs.AddRange(r.Needs);
            if (r.People.Count > 0 && r.People.TrueForAll(x => x.Score2 >= 0))
            {
                d.People = new UiPersonRow[r.People.Count];
                for (int i = 0; i < d.People.Length; i++)
                {
                    WorkPerson p = r.People[i];
                    d.People[i] = new UiPersonRow { Name = p.Name, Hue = UiColour.HueOf(p.Name ?? string.Empty), RoleIcon = p.Icon, Role = p.Role, FirstLabel = p.Label, First = p.Score,
                        SecondLabel = p.Label2, Second = p.Score2 };
                }
            }
            return d;
        }

        private static void AddActs(WorkBuilder b, List<WorkAct> acts, int rowId)
        {
            for (int i = 0; i < acts.Count; i++)
            {
                WorkAct a = acts[i];
                b.Button(a.Label, a.Style, a.Icon, i == 0 && a.Style == UiButtonKind.Primary ? UiAction.Alt : i <= 1
                    && a.Style == UiButtonKind.Secondary ? UiAction.Option : UiAction.Count,
                    WorkBuilder.Do(a.Kind, a.Arg != 0 ? a.Arg : rowId, a.Why, a.Opens));
            }
        }
    }

    /// <summary>The action numbers every work app shares; each app adds its own from 100 up.</summary>
    public static class WorkKinds
    {
        public const int Filter = 1, GoTab = 2, Locate = 3, Back = 4;
    }
}
