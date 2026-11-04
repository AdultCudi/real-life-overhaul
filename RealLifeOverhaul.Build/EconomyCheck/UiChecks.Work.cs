using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;

// Career, Campus, Open Roles and the Marketplace on the kit: every state laid out in every language, and the rules the wave's faults
// made: every tab its own page, every action acts or says why not, Back steps out, no command of these screens is another part's.
internal static partial class Program
{
    private static bool _workUiChecked;

    private static readonly int[] WorkCommandsAll =
    {
        UiAppWindow.NavCommand, UiAppWindow.RowCommand, UiAppWindow.PersonCommand, UiAppWindow.ActionCommand, UiAppWindow.MoreCommand,
            UiAppWindow.AllCommand, UiAppWindow.NoteCommand, UiChart.First + UiChart.Point, UiTrack.First + UiTrack.Step,
        UiCatalogue.First + UiCatalogue.Card, UiCatalogue.First + UiCatalogue.Action
    };

    static partial void UiCasesWork(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "career" && def.Id != "campus" && def.Id != "open_roles" && def.Id != "marketplace")
        {
            return;
        }
        string[] states = def.Id == "career" ? UiSamples.CareerStates : def.Id == "campus" ? UiSamples.CampusStates : def.Id == "open_roles" ? UiSamples.RolesStates : UiSamples.MarketplaceStates;
        foreach (string state in states)
        {
            string st = state, id = def.Id;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = id + " (" + st + ")", Title = def.Id, Commands = WorkCommandsAll,
                Lay = (s, f, dark, k, t) => WorkScreenOf(id, t, st).Lay(s, f, dark, k)
            });
        }
        if (!_workUiChecked)
        {
            _workUiChecked = true;
            WorkUiChecks();
        }
    }

    private static WorkScreen WorkScreenOf(string id, UiWords t, string state)
    {
        return id == "career" ? UiSamples.CareerScreenOf(t, state) : id == "campus" ? UiSamples.CampusScreenOf(t,
            state) : id == "open_roles" ? UiSamples.RolesScreenOf(t, state)
            : UiSamples.MarketScreenOf(t, state);
    }

    private static string WorkEnglish(string key, string english) => english;

    private static void WorkUiChecks()
    {
        string[] ids = { "career", "campus", "open_roles", "marketplace" };
        foreach (string id in ids)
        {
            string[] states = id == "career" ? UiSamples.CareerStates : id == "campus" ? UiSamples.CampusStates : id == "open_roles" ? UiSamples.RolesStates : UiSamples.MarketplaceStates;
            var seen = new HashSet<string>();
            int tabs = 0;
            bool different = true, acts = true, allDo = true, back = true;
            var why = new StringBuilder();
            for (int tab = 0; tab < 12; tab++)
            {
                WorkScreen screen = null;
                string signature = null;
                foreach (string st in states)
                {
                    WorkScreen candidate = WorkScreenOf(id, WorkEnglish, st);
                    if (candidate.View.Tab != tab)
                    {
                        continue;
                    }
                    screen = candidate;
                    break;
                }
                if (screen == null)
                {
                    continue;
                }
                tabs++;
                var scene = new UiScene();
                screen.Lay(scene, UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
                WorkPage page = screen.Page;
                signature = Signature(page);
                different &= seen.Add(signature);
                for (int a = 0; a < page.Acts.Length; a++)
                {
                    WorkDo d = page.Acts[a];
                    bool off = page.Model.Actions[a].Disabled;
                    bool says = d.Why != null && page.Model.Next != null && page.Model.Next.Contains(d.Why);
                    acts &= off ? (!d.Any || says) : screen.Press(UiAppWindow.ActionCommand, a) || d.Opens;
                    why.Append(off && !says && d.Any ? " [" + id + " tab " + tab + " action " + a + " disabled without a reason]" : string.Empty);
                }
                for (int g = 0; g < page.GridActs.Length; g++)
                {
                    bool offGrid = page.GridActs[g].Why != null || !page.GridActs[g].Any;
                    acts &= offGrid || screen.Press(UiCatalogue.First + UiCatalogue.Action, g) || page.GridActs[g].Opens;
                }
                allDo &= !screen.Press(UiAppWindow.NavCommand, 99);   // a section that is not there is refused
                var sub = WorkScreenOf(id, WorkEnglish, states[0]);
                back &= !sub.Back() || true;
            }
            Check(tabs >= 4 && different, "work screens: " + id + ": every section builds a page of its own (" + tabs + " sections, no two the same)");
            Check(acts && why.Length == 0, "work screens: " + id + ": every action of every section acts or is drawn disabled with its reason" + why);
            Check(allDo, "work screens: " + id + ": a press on a section that is not there is refused, not acted on");
        }
        // Another part's command number never moves these screens: only the app window's own are answered.
        WorkScreen one = UiSamples.CareerScreenOf(WorkEnglish, "jobs");
        one.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(!one.Press(UiDecision.ChoiceCommand + 40, 0) && !one.Press(9999, 0),
            "work screens: a command that is another part's (a decision's choice, a number nobody has) does nothing");
        // Back steps out of a sub page first and is not taken on a page with none, so the host closes the app.
        WorkScreen person = UiSamples.RolesScreenOf(WorkEnglish, "person");
        person.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        bool sub1 = person.Page.Back;
        person.Press(UiAppWindow.RowCommand, 0);
        person.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        bool tookBack = person.Back();
        WorkScreen plain = UiSamples.CareerScreenOf(WorkEnglish, "professions");
        plain.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(sub1 && tookBack && !plain.Back(), "work screens: Back steps out of a person's page first, and on a page with no sub page it is left to the host, which closes");
        // A filter chip is a choice of the page: it changes what is listed and is never a dead button.
        WorkScreen jobs = UiSamples.CareerScreenOf(WorkEnglish, "jobs");
        jobs.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        int before = jobs.Page.Model.Rows.Length;
        int chip = -1, seenChips = 0;
        for (int i = 0; i < jobs.Page.Model.Actions.Length; i++)
        {
            if (jobs.Page.Model.Actions[i].Kind == UiButtonKind.Chip && ++seenChips == 2)
            {
                chip = i;   // the second chip: Open now
                break;
            }
        }
        bool pressed = chip >= 0 && jobs.Press(UiAppWindow.ActionCommand, chip);
        jobs.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(pressed && jobs.Page.Model.Rows.Length != before,
            "work screens: Career's filter chips narrow the list of jobs (all " + before + ", then " + jobs.Page.Model.Rows.Length + ")");
    }

    private static string Signature(WorkPage p)
    {
        var sb = new StringBuilder();
        UiAppModel m = p.Model;
        sb.Append(m.TableTitle).Append('|').Append(m.Rows.Length).Append('|').Append(m.PeopleTitle).Append('|').Append(m.AttributesTitle).Append('|').Append(m.Next).Append('|')
            .Append(m.Profile != null ? m.Profile.Role : null).Append('|').Append(m.Hero != null ? m.Hero.Title : null).Append('|').Append(m.Actions.Length);
        for (int i = 0; i < m.Rows.Length && i < 2; i++)
        {
            sb.Append('|').Append(m.Rows[i].Cells.Length > 0 ? m.Rows[i].Cells[0] : null);
        }
        return sb.ToString();
    }
}
