using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A player was hired as a draughtsperson at Vale and Hudson Architects and could not find where to clock on. The practice is on a floor of one of the mod's
// buildings and nothing said which: a clock on away from it said only "go up to its floor", its welcome gave no address, the destination marker of "Show me
// the office" was taken off at the next second (it was kept for a help-wanted board, and the building trades have none), the pages of "Show me the office"
// for the building trades named texts that did not exist, the city map had no pin for the practice and the Career app listed only the employers at boards.
// Every employer's place of work is now named where the player is sent to it, kept on the game's destination guider and pinned on the city map. The rules
// are checked here; what only the game's side can do is checked in its source.
internal static partial class Program
{
    // Every refusal a clock on can meet that has nothing to do with where the player stands.
    private static readonly string[] NotPlaceRefusals =
    {
        "reallife_career_no_off", "reallife_career_no_employer", "reallife_career_no_shift_today", "reallife_career_too_early", "reallife_career_too_late",
        "reallife_job_no_busy", "reallife_job_no_now", "reallife_job_no_client", "reallife_job_no_energy", "reallife_job_no_off", "reallife_job_no_daily",
        "reallife_trades_off", "reallife_job_on_foot", "reallife_trades_not_here", "reallife_career_cart_no_money", "reallife_career_at_depot", string.Empty, null
    };

    // Each new or changed text with the names in curly brackets it holds, sorted.
    private static readonly (string Key, string Names)[] WorkPlaceTexts =
    {
        ("reallife_trades_no_office_at", "{address}"), ("reallife_trades_no_office_none", ""), ("reallife_trades_no_site_at", "{address}"),
        ("reallife_trades_no_site_none", ""), ("reallife_career_office_page_at", "{address},{employer}"), ("reallife_career_office_shown_at", "{address},{employer}"),
        ("reallife_career_office_busy_at", "{address},{employer}"), ("reallife_career_architects_welcome_at", "{address},{rota}"),
        ("reallife_career_text_signed_at", "{address},{employer},{rota},{title}"), ("reallife_career_text_signed_board", "{address},{employer},{place},{rota},{title}"),
        ("reallife_career_where_practice", "{address}"), ("reallife_career_where_sites", "{address}"), ("reallife_career_where_board", "{address},{place}"),
        ("reallife_career_workplace_floor", "{address},{floor}"), ("reallife_career_sec_works", ""), ("reallife_career_works_about", ""),
        ("reallife_career_works_practice", "{address},{employer}"), ("reallife_career_works_sites", "{address},{employer}"), ("reallife_career_works_none", "{employer}"),
        ("reallife_career_why_works_busy", ""), ("reallife_career_reminder_at", "{address},{employer},{time},{title}"),
        ("reallife_career_text_reminder_at", "{address},{time},{title}")
    };

    private static void WorkPlace()
    {
        WorkPlaceRules();
        WorkPlaceMarker();
        WorkPlaceSources();
        WorkPlaceWords();
    }

    // The rules: which employer works where, which refusals send the player to a place, and which text each place gets.
    private static void WorkPlaceRules()
    {
        int boards = 0, sites = 0, practices = 0;
        bool consistent = true;
        foreach (Employer e in CareerCatalogue.All)
        {
            consistent &= (e.Place == EmployerPlace.Board) == (e.Premises.Length > 0) && (e.Place == EmployerPlace.Practice) == (e.Id == CareerCatalogue.Architects);
            boards += e.Place == EmployerPlace.Board ? 1 : 0;
            sites += e.Place == EmployerPlace.Sites ? 1 : 0;
            practices += e.Place == EmployerPlace.Practice ? 1 : 0;
        }
        Check(consistent && sites == 2 && practices == 1 && boards == CareerCatalogue.All.Length - 3 && CareerCatalogue.Find(CareerCatalogue.Builders).Place == EmployerPlace.Sites
              && CareerCatalogue.Find(CareerCatalogue.Inspection).Place == EmployerPlace.Sites && CareerCatalogue.Find(CareerCatalogue.Architects).Place == EmployerPlace.Practice,
            "work place: every employer has a place of work: a board for the six at boards, the works sites for the builder and building control, the practice for the architects");

        string noOffice = CareerOfficeRules.NoOfficeKey, noSite = CareerOfficeRules.NoSiteKey, noIndoors = CareerOfficeRules.NoIndoorsKey;
        Check(CareerOfficeRules.PlaceRefusal(noOffice, false) == noOffice && CareerOfficeRules.PlaceRefusal(noOffice, true) == noOffice
              && CareerOfficeRules.PlaceRefusal(noSite, false) == noSite && CareerOfficeRules.PlaceRefusal(noIndoors, true) == noOffice
              && CareerOfficeRules.PlaceRefusal(noIndoors, false) == null,
            "work place: the practice's desk, a works site, and an office trade indoors anywhere but the practice are refusals that send the player to a place; a street job indoors is asked to call from the street");
        bool others = true;
        foreach (string key in NotPlaceRefusals)
        {
            others &= CareerOfficeRules.PlaceRefusal(key, false) == null && CareerOfficeRules.PlaceRefusal(key, true) == null;
        }
        Check(others, "work place: no other refusal of a clock on (the hour, a job on, the option off, no client, no energy, no money for the cart) is turned into a place");

        bool twins = true, names = true;
        foreach (string refusal in new[] { noOffice, noSite })
        {
            twins &= CareerOfficeRules.PlaceKey(refusal, OfficeWhere.Works) == refusal + "_at" && CareerOfficeRules.PlaceKey(refusal, OfficeWhere.Phone) == refusal + "_none"
                     && CareerOfficeRules.PlaceKey(refusal, OfficeWhere.Board) == refusal;
            names &= CareerOfficeRules.NamesPlace(refusal + "_at") && !CareerOfficeRules.NamesPlace(refusal + "_none") && !CareerOfficeRules.NamesPlace(refusal);
        }
        names &= CareerOfficeRules.NamesPlace(CareerOfficeRules.AtDepotKey + "_at") && CareerOfficeRules.NamesPlace(CareerOfficeRules.AtDepotKey + "_board")
                 && !CareerOfficeRules.NamesPlace(CareerOfficeRules.AtDepotKey) && !CareerOfficeRules.NamesPlace("reallife_career_too_early") && !CareerOfficeRules.NamesPlace(null);
        Check(twins, "work place: a place refusal reads its _at text where the work's place stands (its address named), its _none text where the city has none, so nobody is sent to a place that is not there");
        Check(names, "work place: only a refusal that names the address or the board offers the way (Show me the office); the _none text and the plain one do not");

        // Each employer whose first rank is worked at a place: the refusal it gets away from it is a place refusal, and both its texts are written.
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        bool chain = en != null, boardsFree = true, welcomes = en != null;
        foreach (Employer e in CareerCatalogue.All)
        {
            JobDef def = JobCatalogue.Find(e.Ranks[0].JobId ?? e.Job);
            bool office = def != null && (def.Needs & JobNeed.Office) != 0, site = def != null && (def.Needs & JobNeed.Site) != 0;
            if (e.Place == EmployerPlace.Board)
            {
                boardsFree &= def != null && !office && !site;   // a gig runs where the player stands: nothing sends them anywhere
            }
            else
            {
                string refusal = office ? noOffice : noSite;
                string place = CareerOfficeRules.PlaceRefusal(refusal, office);
                chain &= (e.Place == EmployerPlace.Practice) == office && (e.Place == EmployerPlace.Sites) == site && place == refusal;
                chain &= en != null && en.ContainsKey(CareerOfficeRules.PlaceKey(place, OfficeWhere.Works)) && en.ContainsKey(CareerOfficeRules.PlaceKey(place, OfficeWhere.Phone));
            }
            foreach (OfficeWhere where in e.Place == EmployerPlace.Board ? new[] { OfficeWhere.Board, OfficeWhere.Phone } : new[] { OfficeWhere.Works, OfficeWhere.Phone })
            {
                string welcome = CareerOfficeRules.WelcomeKey(e.Welcome, e.Place, where);
                welcomes &= en != null && en.ContainsKey(welcome) && (welcome == e.Welcome + "_at") == (e.Place == EmployerPlace.Practice && where == OfficeWhere.Works);
                string says = CareerOfficeRules.WhereKey(e.Place, where);
                welcomes &= (says == null) == (where == OfficeWhere.Phone) && (says == null || (en != null && en.ContainsKey(says)));
                string listed = CareerOfficeRules.WorksListKey(e.Place, where);
                welcomes &= listed != null && en != null && en.ContainsKey(listed);
            }
        }
        Check(chain, "work place: the first rank of the building trades is worked at a site or the practice, its refusal is a place refusal, and the text for a place that stands and for one that does not are both written");
        Check(boardsFree, "work place: the six employers at boards have gig work that is done where the player stands, so no place refusal is ever theirs");
        Check(welcomes, "work place: the practice's welcome names its address where it stands, every other welcome is the plain one, and every line for where the work is (the status page, the Career app's list) is written for each place");
        Check(CareerOfficeRules.WhereKey(EmployerPlace.Board, OfficeWhere.Board) != CareerOfficeRules.WhereKey(EmployerPlace.Practice, OfficeWhere.Works)
              && CareerOfficeRules.WhereKey(EmployerPlace.Practice, OfficeWhere.Works) != CareerOfficeRules.WhereKey(EmployerPlace.Sites, OfficeWhere.Works)
              && CareerOfficeRules.WorksListKey(EmployerPlace.Practice, OfficeWhere.Works) != CareerOfficeRules.WorksListKey(EmployerPlace.Sites, OfficeWhere.Works),
            "work place: the practice, a works site and a board each have words of their own");
    }

    // The destination marker of Show me the office: the building trades have no board, so what keeps it up is the place where it stands.
    private static void WorkPlaceMarker()
    {
        const float Now = 100f, Until = 1000f;
        float farSqr = 60f * 60f;
        // The case that failed: a works site or the practice (no board, index -1) with time left and the player far off took its marker off at the next second.
        bool kept = true;
        for (int count = 0; count <= 8; count++)
        {
            kept &= !CareerOfficeRules.MarkOff(Now, Until, CareerOfficeRules.BoardGone(-1, count), farSqr, CareerOfficeRules.MarkReach(OfficeWhere.Works));
        }
        Check(kept, "work place: the marker on the practice's door or a works site stays up while time is left and the player is far, whatever the city's count of boards");
        float works = CareerOfficeRules.MarkReach(OfficeWhere.Works), board = CareerOfficeRules.MarkReach(OfficeWhere.Board);
        Check(works == CareerOfficeRules.WorksReach && board == CareerOfficeRules.BoardReach && works < board && works > 0f,
            "work place: a door or a site is come right up to (the marker comes off nearer than for a board, which the player is only to see)");
        // Every combination of a board index, a count of boards, time, a distance and a reach agrees with the plain statement of the rule.
        int combinations = 0;
        bool agrees = true;
        foreach (int index in new[] { -1, 0, 2, 5 })
        {
            foreach (int count in new[] { 0, 1, 3, 6 })
            {
                foreach (float now in new[] { Now, Until - 1f, Until, Until + 50f })
                {
                    foreach (float metres in new[] { 0f, 7.9f, 8.1f, 24.9f, 25.1f, 60f })
                    {
                        foreach (OfficeWhere where in new[] { OfficeWhere.Works, OfficeWhere.Board })
                        {
                            combinations++;
                            float reach = CareerOfficeRules.MarkReach(where);
                            bool want = now >= Until || (index >= 0 && index >= count) || metres < reach;
                            agrees &= CareerOfficeRules.MarkOff(now, Until, CareerOfficeRules.BoardGone(index, count), metres * metres, reach) == want;
                        }
                    }
                }
            }
        }
        Check(agrees && combinations == 4 * 4 * 4 * 6 * 2, "work place: the marker comes off when its time is up, its board is past the count or the player is within reach of its place, and in no other case (" + combinations + " combinations)");
        Check(!CareerOfficeRules.BoardGone(-1, 0) && !CareerOfficeRules.BoardGone(-1, 9) && CareerOfficeRules.BoardGone(3, 3) && !CareerOfficeRules.BoardGone(2, 3),
            "work place: an employer with no board never has one gone; a board past the count has");
    }

    // The game's side, read in its source.
    private static void WorkPlaceSources()
    {
        string director = ModSource(@"Jobs\CareerDirector.cs");
        string dialog = ModSource(@"Jobs\CareerDialog.cs");
        string employment = ModSource(@"Jobs\CareerDirector.Employment.cs");
        string trades = ModSource(@"BuildTrades\TradesDirector.cs");
        string site = ModSource(@"BuildTrades\SiteWork.cs");
        string links = ModSource(@"World\MapLinks.cs");
        string wiring = ModSource(@"Wiring\CareersLinks.cs");
        string app = ModSource(@"Careers\CareerApp.Map.cs");
        string map = ModSource(@"Careers\CareersDirector.Map.cs");
        string rules = ModSource(@"Jobs\CareerOfficeRules.cs");
        string mark = MemberText(director, "private void OfficeMark(Vector3 player, JobBoards boards)");
        Check(mark.Contains("CareerOfficeRules.MarkOff(Time.unscaledTime, _officeUntil, CareerOfficeRules.BoardGone(_officeBoard, boards.Count), FlatSqr(_officeAt - player), _officeReach)")
              && !mark.Contains("_officeBoard < 0") && !mark.Contains("boards.Front("),
            "work place: the marker comes off by the rule, from where it stands, and an employer with no board is not taken for one whose board has gone");
        string show = MemberText(director, "public bool ShowOffice(int employer)");
        Check(show.Contains("_officeAt = place;") && show.Contains("_officeReach = CareerOfficeRules.MarkReach(WhereOf(employer));") && show.Contains("_officeMark.Show("),
            "work place: Show me the office keeps where the marker stands and how near the player must come, for a board, the practice's door and a works site alike");
        string clock = MethodText(director, "public bool ClockOn(out string reasonKey)");
        string at = MemberText(director, "private string AtPlace(string reasonKey, Employer employer, CareerRank rank)");
        string test = MemberText(director, "public bool TestShift(Employer employer, CareerRank rank, out string reasonKey)");
        Check(clock.Contains("reasonKey = AtPlace(reasonKey, CareerState.Employer, CareerState.CurrentRank);") && test.Contains("reasonKey = AtPlace(reasonKey, employer, rank);")
              && at.Contains("CareerOfficeRules.PlaceRefusal(reasonKey,") && at.Contains("(def.Needs & JobNeed.Office) != 0")
              && at.Contains("CareerOfficeRules.PlaceKey(place, WhereOf(CareerCatalogue.IndexOf(employer.Id)))") && at.Contains("Settings.Trades"),
            "work place: a clock on refused away from where the work is done gets the text for the place that stands (or the one that does not), or the trades switched off; so does the test shift");
        Check(ModSource(@"Jobs\JobDirector.Tests.cs").Contains("_career.OfficeData(CareerCatalogue.IndexOf(employer.Id))"),
            "work place: Options > Tests, Work a shift at my rank, shows a player's words for a place, so the fix is tried without waiting for a rota day");
        string data = MemberText(director, "public Dictionary<string, string> OfficeData(int employer)");
        Check(data.Contains("OfficeAddress(employer)") && data.Contains("OfficePlace(employer)") && data.Contains("CareerClock.Span(CareerState.RotaHour)") && director.Contains("JobDef def = RotaJob(employer, rank);"),
            "work place: the address, the shop and the shift's time fill a refusal's text wherever it is shown, and the job of a rank is read in one place");
        string clockOn = MemberText(dialog, "private static DialogEntry ClockOn()");
        Check(clockOn.Contains("CareerOfficeRules.NamesPlace(reason)") && clockOn.Contains("\"reallife_career_office_show\".Localize()") && clockOn.Contains("OnConfirm = G(() => ShowOffice(employer))")
              && clockOn.Contains("address = career.OfficeAddress(CareerState.EmployerIndex)"),
            "work place: the Work contact's refusal that names a place says where, and offers Show me the office on its button");
        string where = MemberText(dialog, "private static string WhereText(CareerDirector career, Employer employer)");
        Check(where.Contains("CareerOfficeRules.WhereKey(employer.Place, career.WhereOf(index))") && where.Contains("career.OfficeData(index)") && dialog.Contains("string where = WhereText(career, employer);")
              && dialog.Contains("CareerOfficeRules.WelcomeKey(employer.Welcome, employer.Place, career.WhereOf(at))"),
            "work place: the Work contact's status page says where the work is, and the practice's welcome names its address");
        Check(employment.Contains("OfficeKey(\"reallife_career_text_signed\", employer, letter)") && !employment.Contains("CareerPhone.Text(\"reallife_career_text_signed\", data)"),
            "work place: the text message sent at signing on says where the work is (the _at and _board twins)");
        string depot = MemberText(trades, "public bool PracticeWhere(out FloorWhere where)");
        string pin = MemberText(trades, "public bool PracticePin(");
        string stamp = MethodText(trades, "public long PracticeStamp");
        Check(depot.Contains("Settings.Trades && Settings.TradesOffice && FloorsDirector.Where(_office, out where)") && trades.Contains("address = Floored(where);")
              && trades.Contains("\"reallife_career_workplace_floor\"") && pin.Contains("where.Door") && pin.Contains("where.Building") && pin.Contains("Floored(where)"),
            "work place: the practice's address is its building's with the floor, from the one place the floors core is asked");
        Check(stamp.Length > 0 && !Regex.IsMatch(stamp, @"\bnew\b") && trades.Contains("director.Career.OfficeData(CareerState.EmployerIndex)") && site.Contains("director.Career.OfficeData(CareerState.EmployerIndex)"),
            "work place: the practice's stamp allocates nothing, and the practice's panel and the site notice fill a refusal's address");
        int offices = links.IndexOf("Id = \"job_offices\"", StringComparison.Ordinal);
        string row = offices >= 0 ? links.Substring(offices, Math.Min(3000, links.Length - offices)) : string.Empty;
        string place = MemberText(links, "private static void PracticePlace(List<MapPlace> places)");
        Check(row.Contains("trades.PracticeStamp") && row.Contains("PracticePlace(places);") && place.Contains("PracticePin(out Vector3 door") && place.Contains("Lit = building")
              && place.Contains("Alias = employer") && place.Contains("AliasUnder = At(T(\"reallife_map_row_job_offices\"), address)"),
            "work place: the city map's Job offices row pins the practice, lights its building, finds it by the employer's name, and is made again when the practice's floor changes");
        Check(wiring.Contains("employer.Place != EmployerPlace.Board") && !wiring.Contains("employer.Premises.Length == 0") && wiring.Contains("\"reallife_career_why_works_busy\"")
              && app.Contains("WriteWorks(box);") && app.Contains("\"reallife_career_sec_works\"") && app.Contains("site.Line") && MemberText(map, "internal string ShowBoard(BoardEmployer board)").Contains("\"reallife_career_office_shown_at\"")
              && MemberText(map, "internal string ShowBoard(BoardEmployer board)").Contains("\"reallife_career_office_shown_board\""),
            "work place: the Career app lists the building trades' three with the practice's address and the nearest site's, each with Show on map, and the notice names the place");
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bTime\.") && rules.Contains("public static bool MarkOff(") && rules.Contains("public static string PlaceKey("), "work place: the rules stay game-free");
        string careers = SafeRead(@"..\..\..\..\..\docs\features\careers.md"), tradesDoc = SafeRead(@"..\..\..\..\..\docs\features\trades.md");
        Check(careers.Contains("### Finding the place of work") && careers.Contains("CareerOfficeRules.MarkOff") && tradesDoc.Contains("careers.md#finding-the-place-of-work"),
            "work place: the careers and trades pages say how the place of work is found");
    }

    // A file under the repository root, read from the checker's folder; empty if it is not there.
    private static string SafeRead(string relative)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, relative));
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
    }

    // The words: each text is there with the names it needs, in the mod's style, and short enough where a notice may carry it.
    private static void WorkPlaceWords()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        Check(en != null, "work place: en.json is read");
        if (en == null)
        {
            return;
        }
        int bad = 0;
        foreach ((string Key, string Names) text in WorkPlaceTexts)
        {
            bool ok = en.TryGetValue(text.Key, out string words) && !string.IsNullOrWhiteSpace(words) && NamesOf(words) == text.Names && words.IndexOf('!') < 0 && words.IndexOf('—') < 0
                      && words.IndexOf('–') < 0 && !words.Contains(" - ") && !words.Contains("--") && !words.Contains("  ") && IsPlainAscii(words);
            if (!ok)
            {
                bad++;
                Check(false, "work place: " + text.Key + " is in en.json with " + (text.Names.Length > 0 ? text.Names : "no names") + " in curly brackets, in the style (no dash, no exclamation mark)");
            }
        }
        Check(bad == 0, "work place: the " + WorkPlaceTexts.Length + " new or changed texts are in en.json with the names they need, in the style");
        foreach (string code in new[] { "pt", "zh-cn" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            int wrong = 0;
            foreach ((string Key, string Names) text in WorkPlaceTexts)
            {
                // a language not yet translated is held by the locale check; one that has the text keeps the English names
                wrong += own != null && own.TryGetValue(text.Key, out string words) && NamesOf(words) != text.Names ? 1 : 0;
            }
            Check(wrong == 0, "work place: the texts that " + code + " has for these keys keep the names of the English ones");
        }
        // Twins: the text for a place that stands holds what the plain one holds, and the address or the board besides.
        string Said(string key) => en.TryGetValue(key, out string said) ? said : string.Empty;
        Check(NamesOf(Said("reallife_career_text_signed")) == "{employer},{rota},{title}",
            "work place: the plain text sent at signing on holds the employer, the title and the rota the twins hold");
        Check(Said("reallife_trades_no_office_at").Contains("practice") && Said("reallife_trades_no_office_at").Contains("floor") && Said("reallife_trades_no_site_at").Contains("nearest")
              && Said("reallife_trades_no_office_none").Contains("no floor") && Said("reallife_trades_no_site_none").Contains("no works site"),
            "work place: the refusals say which place, and the _none ones say there is none");
        string reminder = Said("reallife_career_reminder_at") + " " + Said("reallife_career_text_reminder_at");
        Check(reminder.Contains("once you are there") && !reminder.Contains("from your phone (Work) or at the office") && !reminder.Contains("by calling this number, or at the office"),
            "work place: the reminders for a works place do not promise a clock on from the phone away from the place");
        Check(Said("reallife_career_workplace_floor").Replace("{address}", "12 Fifth Avenue").Replace("{floor}", "7") == "12 Fifth Avenue, floor 7",
            "work place: the practice's address reads as the building's with its floor");
        bool shortEnough = true;
        foreach (string key in new[] { "reallife_trades_no_office_at", "reallife_trades_no_site_at", "reallife_career_reminder_at", "reallife_career_office_shown_at", "reallife_career_why_works_busy" })
        {
            string filled = Regex.Replace(Said(key), @"\{(\w+)\}", m => m.Groups[1].Value == "address" ? "1234 Hudson Boulevard North, floor 12" : new string('x', 30));
            shortEnough &= filled.Length > 0 && filled.Length <= 240;
        }
        Check(shortEnough, "work place: a text a notice may carry stays within 240 characters with a long address");
        // What's new: the fix, with its mark, in some row.
        const string line = "A new job's workplace is now easy to find";
        string marked = FixMarks["en"] + line;   // the mark sits right before the sentence, not merely somewhere earlier in its row
        bool told = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            told |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                    && row.Value.IndexOf(marked, StringComparison.Ordinal) >= 0;
        }
        Check(told, "work place: What's new tells of the fix in some row, with the fix mark");
    }

    // Plain ASCII: the mod's English has no curly quote, no accent and no symbol that the style check would flag.
    private static bool IsPlainAscii(string text)
    {
        foreach (char c in text)
        {
            if (c > 126)
            {
                return false;
            }
        }
        return true;
    }
}
