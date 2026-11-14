using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Flare;

// "Hold meeting" at the owner's desk (Scripts/Sessions: a player's report of 13 Nov: no word of where the desk is or how to have one, a long
// wait after the press, Next session ("[P]") doing nothing and the panel stuck at "5 of 16"). What it holds:
//   the sessions table: every kind of desk session, where it is held, what it needs (its premise), what the panel does without it;
//   every kind reachable and finishable from every state of what a business can lack (a premise, the furniture, the day), over every
//     business type: the series the panel draws, the page it opens on, a skip that always moves on, a page that can always be left;
//   a save that was stuck (a kind held on a day that has not come, a manager's hand-over with nobody to take it) comes loose;
//   the panel's pages are drawn again by the game's panel after every change (it reads a page only when it opens), the click answers at
//     once, the walk to the chair is never waited on for ever, the desk is looked for again when the rota changes, and each step logs a line;
//   the texts hold no placeholder nothing fills, and the new ones read as the style asks.
internal static partial class Program
{
    // The business types a kind is held at, as flags.
    private const int HmSoftware = 1, HmGame = 2, HmConsult = 4, HmSecurity = 8, HmLab = 16, HmAgency = 32, HmAll = 63, HmNotGame = 61;
    private static readonly string[] HmTypeNames = { "software studio", "game studio", "IT consultancy", "cybersecurity firm", "AI lab", "web agency or design studio" };

    private sealed class HmRow
    {
        public string Id, Premise;
        public int Types, Desks;
        public string[] Why;
        public bool DataCentre, Flare;   // listed only while the player has a data centre / Flare in the city runs
    }

    private static HmRow Hm(string id, int types, int desks, string premise, params string[] why) => new HmRow { Id = id, Types = types, Desks = desks, Premise = premise, Why = why };

    private static List<HmRow> HmTable()
    {
        const string d = "reallife_tech_desk_", g = d + "gtm_", s = "reallife_studio_session_";
        string[] ids = SectorRules.DeskIds;
        var t = new List<HmRow>
        {
            Hm(MeetingRules.Id, HmAll, 2, "professionals working at the firm (and the professionals option on)", "reallife_session_meeting_nobody", "reallife_session_meeting_no_people_part"),
            Hm(ids[SectorRules.DeskSprint], HmSoftware, 2, "a product of the firm being built or on sale", d + "why_no_product"),
            Hm(ids[SectorRules.DeskOutage], HmSoftware, 2, "a service of the firm went down lately and a product is on sale", d + "why_no_outage"),
            Hm(ids[SectorRules.DeskBigCustomer], HmSoftware, 2, "a product with enough business customers, no big ask lately", d + "why_no_big"),
            Hm(ids[SectorRules.DeskTraining], HmLab, 2, "an AI model in training, its safety checks done", d + "why_no_model", d + "why_held"),
            Hm(ids[SectorRules.DeskLaunch], HmLab, 2, "a model ready to launch or with a new generation", d + "why_no_launch"),
            Hm(ids[SectorRules.DeskPitch], HmConsult, 2, "an offer made to the firm or an open tender it takes", d + "why_no_pitch"),
            Hm(ids[SectorRules.DeskScope], HmConsult, 2, "a project far enough along for the client to ask for more", d + "why_no_scope"),
            Hm(ids[SectorRules.DeskIncident], HmSecurity, 2, "an incident under way for a client", d + "why_no_incident"),
            Hm(ids[SectorRules.DeskCapacity], HmAll, 1, "a data centre of the player's", d + "why_no_centre"),
            Hm(ids[SectorRules.DeskPower], HmAll, 1, "a data centre and a power price spike waiting", d + "why_no_centre", d + "why_no_spike"),
            Hm(ids[SectorRules.DeskFailover], HmAll, 1, "a data centre that went down lately", d + "why_no_centre", d + "why_no_failover"),
            Hm(ids[SectorRules.DeskClientPitch], HmAgency, 2, "an offer made to the firm", d + "why_no_offer"),
            Hm(ids[SectorRules.DeskRevision], HmAgency, 2, "a project far enough along for changes", d + "why_no_revision"),
            Hm(ids[SectorRules.DeskPortfolio], HmAgency, 2, "a finished project to show", d + "why_no_portfolio"),
            Hm(StudioRules.SessionIds[StudioRules.SessionPitch], HmGame, 2, "a concept drafted, and room for another product", s + "no_draft", s + "full"),
            Hm(StudioRules.SessionIds[StudioRules.SessionMilestone], HmGame, 2, "a game waiting at a milestone", s + "no_milestone"),
            Hm(StudioRules.SessionIds[StudioRules.SessionPlaytest], HmGame, 2, "a game far enough along to playtest", s + "no_build"),
            Hm(StudioRules.SessionIds[StudioRules.SessionPostLaunch], HmGame, 2, "a game of the studio on sale", s + "no_live"),
            Hm("tech_gtm_discovery", HmNotGame, 1, "the firm founded and at its interview stage", g + "why_none", g + "why_stage"),
            Hm("tech_gtm_build", HmNotGame, 1, "the firm at its build stage with a product building or live", g + "why_none", g + "why_stage", g + "why_product"),
            Hm("tech_gtm_users", HmNotGame, 1, "the firm at its early users stage with a product on sale", g + "why_none", g + "why_stage", g + "why_live"),
            Hm("tech_gtm_launch", HmNotGame, 1, "the firm at its launch stage", g + "why_none", g + "why_stage"),
            Hm("tech_gtm_price", HmNotGame, 1, "the firm at its pricing stage with a product on sale", g + "why_none", g + "why_stage", g + "why_live"),
            Hm("tech_board", HmAll, 1, "the board has a question about money taken from the firm", d + "board_why_none"),
            Hm("tech_bakeoff", HmNotGame, 1, "a head to head open for the firm", d + "bake_why_none"),
            Hm("tech_exit_offer", HmAll, 1, "an offer to buy the firm waits", d + "exit_why_none"),
            Hm("tech_exit_road", HmAll, 1, "the bankers are engaged and the roadshow is next", d + "exit_why_none"),
            Hm(FlareCityRules.SessionId, HmAll, 1, "Flare in the city runs and this week's plan is not made", "reallife_flare_session_off", "reallife_flare_session_held"),
        };
        foreach (HmRow r in t)
        {
            r.DataCentre = r.Id == ids[SectorRules.DeskCapacity] || r.Id == ids[SectorRules.DeskPower] || r.Id == ids[SectorRules.DeskFailover];
            r.Flare = r.Id == FlareCityRules.SessionId;
        }
        return t;
    }

    private static void HoldMeeting(bool print)
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        List<HmRow> table = HmTable();
        HmTableChecks(table, print);
        HmSeries(table);
        HmSave();
        HmSources();
        HmTexts(table);
        SessionCard();
        Console.WriteLine("  Hold meeting: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the table

    private static void HmTableChecks(List<HmRow> table, bool print)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        bool unique = true, whole = true;
        foreach (HmRow r in table)
        {
            unique &= ids.Add(r.Id);
            whole &= !string.IsNullOrEmpty(r.Id) && r.Why.Length > 0 && r.Types != 0 && r.Desks >= 1;
        }
        Check(unique && whole, "hold meeting: every kind of desk session has an id of its own, a premise, a reason it says without it and the furniture it needs (" + table.Count + " kinds)");

        // every registered kind is a row: the registry's sources are the wiring's, and each id of theirs is in the table
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        int sites = 0;
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            sites += Regex.Matches(System.IO.File.ReadAllText(file), @"new SessionDef\s*\r?\n?\s*\{").Count;
        }
        Check(sites == 8, "hold meeting: eight places in the mod make a kind of session (the meeting, the other firms', the studio's, going to market, exits, the board, the bake off, Flare's plan): found " + sites + "; a new one needs its row in the table");
        string wiring = System.IO.Path.Combine(scripts, "Wiring");
        string all = string.Concat(System.IO.Directory.GetFiles(wiring, "*.cs").Select(System.IO.File.ReadAllText));
        bool idsKnown = all.Contains("\"tech_gtm_discovery\"") && all.Contains("\"tech_gtm_price\"") && all.Contains("\"tech_board\"") && all.Contains("\"tech_bakeoff\"")
                        && all.Contains("\"tech_exit_offer\"") && all.Contains("\"tech_exit_road\"");
        foreach (HmRow r in table)
        {
            idsKnown &= ids.Contains(r.Id);
        }
        Check(idsKnown, "hold meeting: the ids the wiring registers are the table's");

        // the types: the table's flags against the wiring's own lists
        string board = ModSource(@"Wiring\Wiring.BoardLinks.cs"), bake = ModSource(@"Wiring\Wiring.BakeLinks.cs"), found = ModSource(@"Wiring\Wiring.FoundLinks.cs");
        Check(board.Contains("TechTypes.GameStudio") && !bake.Contains("TechTypes.GameStudio") && !found.Contains("TechTypes.GameStudio"),
            "hold meeting: the board's question is held at the game studio too, the bake off and going to market are not (as the table says)");

        // what a player is shown: the kinds a business type holds when everything is on (the report's \"5 of 16\" was one such list)
        for (int t = 0; t < HmTypeNames.Length; t++)
        {
            int n = table.Count(r => (r.Types & (1 << t)) != 0);
            if (print)
            {
                Console.WriteLine("    " + HmTypeNames[t] + ": " + n + " kinds when a data centre and Flare in the city are on");
            }
            Check(n >= 8 && n <= 17, "hold meeting: a " + HmTypeNames[t] + " holds " + n + " kinds when every part is on (the report's player saw 16)");
        }
        if (print)
        {
            Console.WriteLine("    id | held at | needs | without it");
            foreach (HmRow r in table)
            {
                var at = new List<string>();
                for (int t = 0; t < HmTypeNames.Length; t++)
                {
                    if ((r.Types & (1 << t)) != 0)
                    {
                        at.Add(HmTypeNames[t]);
                    }
                }
                Console.WriteLine("    " + r.Id + " | " + (r.Types == HmAll ? "every tech firm type" : string.Join(", ", at)) + (r.DataCentre ? " (listed while the player has a data centre)" : r.Flare ? " (listed while Flare in the city runs)" : string.Empty)
                                  + " | " + r.Desks + " computer workstation(s) with a chair, one free of the rota | " + r.Premise + " | not drawn while another kind can be held, else drawn with: " + string.Join(" / ", r.Why));
            }
        }

        // utilities are no premise: no kind's own test reads power, water or the internet, so a firm with them cut still holds its sessions
        string[] tests =
        {
            @"Tech\TechDirector.SectorDesk.cs", @"Tech\TechDirector.FoundDesk.cs", @"Tech\TechDirector.StudioDesk.cs", @"Tech\TechDirector.Exits.cs", @"Tech\TechDirector.BakeOff.cs",
            @"Wiring\Wiring.SessionLinks.cs", @"Sessions\SessionDirector.Desk.cs", @"Sessions\SessionRules.cs"
        };
        var read = new StringBuilder();
        foreach (string f in tests)
        {
            read.Append(ModSource(f));
        }
        string joined = read.ToString();
        Check(joined.Length > 5000 && !Regex.IsMatch(joined, @"SupplyKeys|UtilitiesDirector|PowerCut|WaterCut|SupplyOn|isOpen|temporarilyClosed"),
            "hold meeting: power, water and the internet are no premise of any session (no kind's test or the desk's reads them), so a firm with them off still holds its sessions");
        string blockedBlock = MethodText(ModSource(@"Tech\TechDirector.SectorDesk.cs"), "internal string SectorBlocked(int kind, string place)");
        bool whyKeys = blockedBlock.Length > 0;
        foreach (HmRow r in table)
        {
            foreach (string why in r.Why)
            {
                whyKeys &= LocaleFile("en").ContainsKey(why);
            }
        }
        Check(whyKeys, "hold meeting: every reason a kind says without its premise is a text of the mod in English");
    }

    // ---------------------------------------------------------------- every state

    // Which kinds a business type holds in a state of the player's: a data centre or not, Flare in the city or not.
    private static List<HmRow> HmKinds(List<HmRow> table, int type, bool centre, bool flare)
    {
        return table.Where(r => (r.Types & (1 << type)) != 0 && (!r.DataCentre || centre) && (!r.Flare || flare)).ToList();
    }

    private static void HmSeries(List<HmRow> table)
    {
        long states = 0;
        bool seriesOk = true, firstOk = true, skipOk = true, buttonsOk = true, leaveOk = true, openOk = true, reachOk = true;
        string bad = "none";
        int[][] furniture = { new[] { 0, 0 }, new[] { 1, 1 }, new[] { 2, 0 }, new[] { 2, 1 }, new[] { 4, 2 } };   // workstations, those free of the rota
        var skipFor = new bool[24];   // the skip's walk round a series of so many pages, from each page: independent of the state, so once
        for (int count = 1; count < skipFor.Length; count++)
        {
            bool round = true;
            for (int at = 0; at < count; at++)
            {
                int p = at;
                var seen = new HashSet<int>();
                for (int press = 0; press < count; press++)
                {
                    int next = SessionRules.NextPage(p, count);
                    round &= next >= 0 && next < count && (count < 2 || next != p);
                    p = next;
                    seen.Add(p);
                }
                round &= p == at && seen.Count == count;   // round the series, every page once, back where it began
            }
            skipFor[count] = round;
        }
        for (int type = 0; type < HmTypeNames.Length; type++)
        {
            for (int world = 0; world < 4; world++)   // a data centre, Flare in the city
            {
                List<HmRow> kinds = HmKinds(table, type, (world & 1) != 0, (world & 2) != 0);
                int n = kinds.Count;
                long masks = 1L << Math.Min(n, 20);
                long step = n > 12 ? 7 : 1;   // every state of premises up to twelve kinds; beyond, a spread that still holds the extremes
                for (long m = 0; m < masks; m += step)
                {
                    long mask = m;
                    if (m + step >= masks)
                    {
                        mask = masks - 1;
                    }
                    var met = new bool[n];
                    for (int i = 0; i < n; i++)
                    {
                        met[i] = (mask >> i & 1) != 0;
                    }
                    for (int pattern = 0; pattern < 3; pattern++)
                    {
                        var due = new bool[n];
                        for (int i = 0; i < n; i++)
                        {
                            due[i] = pattern == 0 || (pattern == 1 ? met[i] : !met[i]);
                        }
                        foreach (int[] f in furniture)
                        {
                            states++;
                            var pick = new int[n];
                            int shown = SessionRules.Series(met, n, pick);
                            int metCount = met.Count(x => x);
                            bool series = shown == (metCount > 0 ? metCount : n) && shown >= 1;
                            for (int i = 0; i < shown; i++)
                            {
                                series &= metCount == 0 || met[pick[i]];
                                series &= i == 0 || pick[i] > pick[i - 1];
                            }
                            var can = new bool[shown];
                            var dueShown = new bool[shown];
                            for (int i = 0; i < shown; i++)
                            {
                                int k = pick[i];
                                SessionDef def = new SessionDef { DesksLeast = kinds[k].Desks, OwnDesk = true };
                                can[i] = met[k] && due[k] && SessionRules.Missing(def, f[0], f[1], out _) == SessionNeed.None;
                                dueShown[i] = due[k];
                            }
                            int first = SessionRules.FirstPage(can, dueShown, shown);
                            bool firstGood = first >= 0 && first < shown && (!can.Any(x => x) || can[first]) && (can.Any(x => x) || !dueShown.Any(x => x) || dueShown[first]);
                            bool skip = shown < skipFor.Length && skipFor[shown];
                            for (int at = 0; at < shown; at++)
                            {
                                SessionRules.PageButtons b = SessionRules.Buttons(can[at], shown, 0, false);
                                buttonsOk &= b.Cancel && b.Begin == can[at] && b.Next == (shown > 1);
                                leaveOk &= b.Cancel;   // a page can always be left
                            }
                            // every kind of the type is reachable: drawn in the series, or, when another can be held, one skip of a series that holds it
                            seriesOk &= series;
                            firstOk &= firstGood;
                            skipOk &= skip;
                            if (!(series && firstGood && skip) && bad == "none")
                            {
                                bad = HmTypeNames[type] + ", world " + world + ", mask " + mask + ", furniture " + f[0] + "/" + f[1] + ", pattern " + pattern;
                            }
                        }
                    }
                }
                // each kind alone met is drawn; none met is drawn with its reason
                for (int k = 0; k < n; k++)
                {
                    var met = new bool[n];
                    met[k] = true;
                    var pick = new int[n];
                    int shown = SessionRules.Series(met, n, pick);
                    reachOk &= shown == 1 && pick[0] == k;
                    var none = new bool[n];
                    int all = SessionRules.Series(none, n, new int[n]);
                    openOk &= all == n;
                }
            }
        }
        Check(seriesOk, "hold meeting: the panel draws the kinds whose premise is met, and every kind with its reason when none is, never an empty desk, over " + states + " states (" + bad + ")");
        Check(firstOk, "hold meeting: the panel opens on a kind that can be held when there is one, else on one that is due: never on a page that cannot be held while another can (" + bad + ")");
        Check(skipOk, "hold meeting: Next session always moves to another page, round the series, every page once and back to where it began, from every page of every state (" + bad + ")");
        Check(buttonsOk && leaveOk, "hold meeting: every page has Cancel; Begin only where the kind can be held; the skip wherever there is another page");
        Check(reachOk && openOk, "hold meeting: a kind whose premise is the only one met is the one drawn, and with no premise met every kind of the type is drawn, so each of the " + table.Count + " is reachable");

        // finishable: a held session is due again after its days and never sooner; one held on a day that has not come is due now
        bool days = true;
        string[] ids = SectorRules.DeskIds;
        int[] every = SectorRules.DeskEvery;
        for (int k = 0; k < ids.Length; k++)
        {
            days &= every[k] >= 1 && !SessionRules.Due(50, 50, every[k]) && SessionRules.Due(50, 50 + every[k], every[k]) && (every[k] == 1 || !SessionRules.Due(50, 50 + every[k] - 1, every[k]));
        }
        days &= SessionRules.Due(SessionRules.Never, 10, 7) && SessionRules.Due(900, 10, 7) && !SessionRules.Due(10, 10, 7) && SessionRules.DaysToDue(900, 10, 7) == 0;
        Check(days, "hold meeting: every kind is held and due again after its own days, never sooner; one never held, or held on a day that has not come, is due now");
    }

    // ---------------------------------------------------------------- a stuck save

    private static void HmSave()
    {
        // a save made on a later clock (a kind held on day 900, handed to a manager who is gone), loaded on day 100
        var file = new SessionFile { Seed = 77, Reminded = 900 };
        SessionRecord a = file.Get("5 first street", "firm_power");
        a.Held = 900;
        a.Delegated = true;
        a.Until = 950;
        SessionRecord b = file.Get("5 first street", "tech_team_meeting");
        b.Held = 95;
        var data = new Dictionary<string, string> { [SessionStore.Key] = SessionStore.Write(file) };
        SessionFile back = SessionStore.Load(data, out int version);
        SessionRecord ra = back?.Find("5 first street", "firm_power");
        Check(version >= 0 && ra != null && ra.Held == 900 && ra.Delegated, "hold meeting: a save with a kind held on a day to come, handed to a manager, reads back as it was");
        Check(ra != null && SessionRules.Due(ra.Held, 100, 1) && SessionRules.DaysToDue(ra.Held, 100, 1) == 0,
            "hold meeting: that kind is due on day 100: a held day that has not come no longer keeps it waiting (it waited for day 901)");
        Check(ra != null && !back.Effect("5 first street", "firm_power", 100, out _, out _), "hold meeting: and its effect, which began on a day to come, is not read on day 100");

        // the report's page: five kinds before the first that can be held, never held, none of them able. It used to open there.
        var met = new[] { false, false, false, false, false, true, true };
        var can = new[] { false, false, false, false, false, true, true };
        var due = new[] { true, true, true, true, true, true, true };
        var pick = new int[7];
        int shown = SessionRules.Series(met, 7, pick);
        Check(shown == 2 && pick[0] == 5 && pick[1] == 6 && SessionRules.FirstPage(new[] { true, true }, new[] { true, true }, shown) == 0,
            "hold meeting: a firm whose first five kinds lack their premise opens on the sixth, where it opened on the first never-held one, the dead page of 5 of 16");
        int old = SessionRules.FirstPage(can, due, 7);
        Check(old == 5, "hold meeting: asked of all seven at once the first page is the first that can be held, the sixth");
        int stepped = 0;
        for (int i = 0; i < 20; i++)
        {
            stepped = SessionRules.NextPage(stepped, 16);
        }
        Check(stepped == 4, "hold meeting: twenty skips of sixteen pages come to page 5 and never stand still");
    }

    // ---------------------------------------------------------------- the sources

    private static void HmSources()
    {
        string panel = ModSource(@"Sessions\SessionPanel.cs"), desk = ModSource(@"Sessions\SessionDirector.Desk.cs"), game = ModSource(@"Sessions\SessionGame.cs");
        string director = ModSource(@"Sessions\SessionDirector.cs"), day = ModSource(@"Sessions\SessionDirector.Day.cs"), deskGame = ModSource(@"Careers\DeskGame.cs");
        string kit = ModSource(@"Ui\Pages\SessionKit.cs");

        // the game's panel reads a page only when it opens or the time begins: every change of page draws it again
        string repage = MethodText(panel, "private void Repage()");
        string redraw = MethodText(game, "public static void Redraw(PlayerActivity.IPlayerActivity activity)");
        int barePage = Regex.Matches(panel, @"(?<![A-Za-z])Page\(\);").Count;
        int repages = Regex.Matches(panel, @"\bRepage\(\);").Count;
        Check(repage.Contains("Page();") && repage.Contains("SessionGame.Redraw(this);") && redraw.Contains("ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity)") && redraw.Contains("UpdateActivityDisplay()")
              && barePage == 2 && repages >= 5,
            "hold meeting sources: Next session, Hand to manager, Begin, a choice and a refused start each draw the game's panel again (the game reads a page only on opening): " + repages + " calls, the page built bare only in the constructor");
        Check(MethodText(panel, "private void Next()").Contains("SessionRules.NextPage(_at, _defs.Length)") && MethodText(panel, "private void Next()").Contains("skip: from session "),
            "hold meeting sources: the skip is the rule's, and says where it went in the log");

        // the panel opens at once; the desk's click walks nowhere first
        string click = MethodText(desk, "private void DeskClicked()");
        string open = MethodText(desk, "private bool OpenPanel(EmployeeStationController desk)");
        Check(click.Contains("OpenPanel(_desk)") && !click.Contains("WalkTo(") && !desk.Contains("WalkTo(desk, () => OpenPanel(desk))"),
            "hold meeting sources: a click on the desk opens the panel at once; the walk comes after the choices, once, to the chair (it walked to the desk first and opened on arrival)");
        Check(open.Contains("SessionRules.Series(met, n, pick)") && open.Contains("SessionRules.FirstPage(shownCan, shownDue, shown)") && open.Contains("new SessionPanel(this, desk, defs, first)"),
            "hold meeting sources: the panel is opened with the series of kinds whose premise is met, on the first page that can be held");
        Check(!MethodText(ModSource(@"Sessions\SessionDirector.Tests.cs"), "public void TestMeeting()").Contains("DeskGame.WalkTo(desk"),
            "hold meeting sources: the test opens the panel as the click does");

        // never a wait that cannot end
        string walk = MethodText(panel, "private IEnumerator Walk()");
        Check(walk.Contains("DeskGame.Reaches(_station)") && walk.Contains("reallife_session_note_no_way") && walk.Contains("while (_state == PlayerActivityState.MovingTowardsActivity && Time.unscaledTime < until)")
              && walk.Contains("End(false, true);") && panel.Contains("private const float WalkSeconds = 40f;") && deskGame.Contains("public static bool Reaches(EntityController station)"),
            "hold meeting sources: the walk to the chair is asked whether the game can find a way (its goal call says an error and never calls back), and is called off after forty seconds, nothing held");
        Check(game.Contains("public static bool PanelBusy => DeskGame.Busy;") && !game.Contains("!PlayerActivity.PlayerActivityUI.CanStartActivity()"),
            "hold meeting sources: the wait for the game's panel to let go reads the panel itself (CanStartActivity said yes with the panel open, and is another mod's to patch)");

        // the desk is looked for again when the rota changes, and let go with its marker
        Check(game.Contains("public static int Roster(BuildingRegistration reg)") && MethodText(desk, "private void DeskWatch()").Contains("roster == _roster") && MethodText(desk, "private void DeskWatch()").Contains("SessionGame.Roster(reg)"),
            "hold meeting sources: a person taken off the last free workstation in the rota makes it the owner's desk without the player leaving the office");
        string release = MethodText(desk, "private void DeskRelease(string why)");
        Check(release.Contains("_mark?.Hide();") && release.Contains("_toldDesk.Clear();") && release.Contains("SessionGame.Target = null;") && director.Contains("new GameUi.GuideMark(\"the owner's desk for desk sessions\").While(MarkWanted)"),
            "hold meeting sources: the marker on the desk goes with the visit, the panel, the option and a fault, and the game's own destination guider is the one used");

        // told where the desk is, or what to buy
        string told = MethodText(desk, "private void DeskTold()");
        Check(told.Contains("\"reallife_session_note_no_desk\"") && told.Contains("\"reallife_session_note_desk\"") && told.Contains("_mark?.Hide();") && told.Contains("_mark.Show(_desk.transform.position, _markName)")
              && MethodText(desk, "private void DeskClicked()").Contains("\"reallife_session_note_no_desk\""),
            "hold meeting sources: a player in an office where something can be held is told where the desk is (marked) or what to buy or free to have one");
        Check(MethodText(desk, "internal string Begins(SessionDef def, int[] choices, out int minutes)").Contains("\"reallife_session_refused_desks\""),
            "hold meeting sources: too few workstations is said as that, not as no desk of one's own");

        // a log line for each step
        string[] lines =
        {
            "the desk was clicked", "no desk at ", "the panel is open at ", "the panel was not opened: ", "held \" + def.Id", "refused at the end of ", "hand to manager", "Sessions check (desk): "
        };
        string everything = desk + panel + day + director;
        string missing = lines.FirstOrDefault(l => !everything.Contains(l)) ?? "none";
        Check(missing == "none" && panel.Contains("\"skip: from session \"") && panel.Contains("\"begin \" + Def.Id") && panel.Contains("\"refused to begin \"") && panel.Contains("\"page \" + (_at + 1) + \" of \"")
              && desk.Contains("Note(def.Id + \" stopped after \"") && day.Contains("Note(\"reminder on day \""),
            "hold meeting sources: each step writes a line to Player.log: pressed, desk found or not and why, page drawn, skipped, begun, held, stopped, refused (missing: " + missing + ")");

        // the keyboard: the page that cannot begin does not start on Cancel
        Check(kit.Contains("primary >= 0 ? primary : Math.Max(0, other)"), "hold meeting sources: on the kit's page that cannot begin, focus starts on the skip, never on Cancel");
    }

    // ---------------------------------------------------------------- the texts

    private static void HmTexts(List<HmRow> table)
    {
        Dictionary<string, string> en = LocaleFile("en");
        var prefix = new Regex(@"^reallife_(tech_desk_|session_|studio_(pitch|milestone|playtest|postlaunch|session)_|flare_session_)", RegexOptions.CultureInvariant);
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        foreach (string folder in new[] { "Sessions", "Tech", "Wiring", "Social", "Ui" })
        {
            foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(scripts, folder), "*.cs", System.IO.SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(System.IO.File.ReadAllText(file), "\"([a-z][a-z0-9_]*)\""))
                {
                    supplied.Add(m.Groups[1].Value);
                }
            }
        }
        var unfilled = new List<string>();
        var shown = new List<string>();
        int checkedKeys = 0;
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (!prefix.IsMatch(kv.Key))
            {
                continue;
            }
            checkedKeys++;
            foreach (Match m in Regex.Matches(kv.Value, @"\{([^{}]*)\}"))
            {
                string name = m.Groups[1].Value;
                bool known = Regex.IsMatch(name, @"^[a-z][a-z0-9_]*$") && (supplied.Contains(name) || Regex.IsMatch(name, @"^choice\d$"));
                if (!known)
                {
                    unfilled.Add(kv.Key + " {" + name + "}");
                }
            }
            bool plain = Regex.IsMatch(kv.Key, @"(_title|_about|_trade|_step\d?|_cta|_mark|_enabled|_button_[a-z]+|studio_session_[a-z_]+|flare_session_(off|held)|_why_[a-z_]+)$")
                         || (kv.Key.StartsWith("reallife_session_meeting_", StringComparison.Ordinal) && !kv.Key.Contains("_done_"))
                         || (en.ContainsKey(kv.Key + "_trade"));   // a choice's name sits beside its trade
            if (plain && kv.Value.Contains("{"))
            {
                shown.Add(kv.Key);
            }
        }
        Check(checkedKeys > 400 && unfilled.Count == 0, "hold meeting texts: no text of the feature holds a placeholder nothing fills, none a {P} for a key (" + checkedKeys + " texts; unfilled: " + (unfilled.Count == 0 ? "none" : string.Join(", ", unfilled)) + ")");
        Check(shown.Count == 0, "hold meeting texts: a title, a line of what a session is, a step, a choice and its trade, a button and a reason, which the panel shows as they are, hold no placeholder (" + (shown.Count == 0 ? "none" : string.Join(", ", shown)) + ")");
        bool anyKeyName = en.Where(kv => prefix.IsMatch(kv.Key)).Any(kv => Regex.IsMatch(kv.Value, @"\{[A-Z]\}"));
        Check(!anyKeyName, "hold meeting texts: no text of the feature names a key in braces ({P}); the key on a button is the game's [P] or the kit's cap, drawn from the binding");

        string[] mine =
        {
            "reallife_session_note_no_desk", "reallife_session_note_desk", "reallife_session_refused_desks", "reallife_session_note_no_way", "reallife_session_mark", "reallife_session_status_own_desk",
            "reallife_tech_mkt_next_desk"
        };
        bool style = true;
        string why = "none";
        foreach (string k in mine)
        {
            bool has = en.TryGetValue(k, out string v) && !string.IsNullOrEmpty(v);
            if (!has || v.Contains('!') || v.Contains('—') || v.Contains('–') || v.Contains(" - ") || v.Length > 240)
            {
                style = false;
                why = k;
            }
        }
        Check(style, "hold meeting texts: the new and changed texts are there, in plain words with no dash and no exclamation mark, a notice within 240 characters (" + why + ")");
        string noDesk = en.TryGetValue("reallife_session_note_no_desk", out string nd) ? nd : string.Empty;
        Check(noDesk.Contains("Computer Workstation") && noDesk.Contains("rota") && noDesk.Contains("{business}") && noDesk.Contains("Buy"),
            "hold meeting texts: the notice for no desk says what a desk is, where its rota is and what to buy");
        string noticeDesk = en.TryGetValue("reallife_session_note_desk", out string dd) ? dd : string.Empty;
        Check(noticeDesk.Contains("{session}") && noticeDesk.Contains("{business}") && noticeDesk.Contains("{count}") && noticeDesk.Contains("marked"), "hold meeting texts: the notice for a desk says which desk (marked), at which firm, and what waits");
        string guide = en.TryGetValue("reallife_guide_living", out string gl) ? gl : string.Empty;
        string[] facts = { "{session_remind}", "{session_manager_least}", "{meeting_every}", "{meeting_morale}", "{meeting_ease}", "{meeting_push}", "{meeting_cost}", "{meeting_short}", "{meeting_desks}" };
        Check(guide.Contains("opens at once") && guide.Contains("Rota tab") && guide.Contains("marker points") && facts.All(guide.Contains),
            "hold meeting texts: the Guide says where the desk is and how to have one, the panel opens at once, and keeps every figure it filled before");
        bool whatsNew = false;
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && kv.Key != "reallife_changes_title" && kv.Value.Contains("Fixed: holding a session at your desk."))
            {
                whatsNew = true;
            }
        }
        Check(whatsNew, "hold meeting texts: What's new has the fix's sentence, with its fix mark, in some reallife_changes_ row of English");
    }
}
