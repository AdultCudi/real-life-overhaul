using System;
using System.Collections.Generic;

// Two faults reported on the Workshop (26 Oct 2026). A driving test whose car "was not moving at all": the school's drive waits for the
// examiner to be aboard and for time to run, and sat in the car for the whole of its two minutes when either did not happen; it now
// seats the examiner, says a paused game, frees a time lock left behind and mends a car that will not go, or calls the drive off with
// the fee returned (the rules are NavCheck's TestStallCheck). And employers that could not be found: the gig work's employers have
// their offices at help-wanted boards outside shops, which the map listed only by a row and the search not at all. What only the
// game's side can do is checked in its source.
internal static partial class Program
{
    private static readonly string[] DriveKeys =
    {
        "reallife_lic_test_car_stuck", "reallife_lic_test_paused"
    };

    private static readonly string[] MapKeys =
    {
        "reallife_career_button_map", "reallife_career_post_shown", "reallife_career_sec_boards", "reallife_career_boards_about", "reallife_career_boards_at",
        "reallife_career_boards_phone", "reallife_career_why_no_board", "reallife_career_why_board_busy", "reallife_map_row_hiring", "reallife_map_hiring_one",
        "reallife_map_hiring_many"
    };

    private static void DriveMap()
    {
        DriveStall();
        EmployersOnTheMap();
        DriveMapTexts();
    }

    private static void DriveStall()
    {
        string test = ModSource(@"Licences\Driving\PracticalTest.cs");
        string stall = ModSource(@"Licences\Driving\PracticalTest.Stall.cs");
        Check(MemberText(test, "public void Frame(float now)").Contains("\n            Watch();") && MemberText(test, "public bool Begin(").Contains("\n            WatchBegin();"),
            "drive fix: the watch begins with every lesson and test and looks once a frame while one is on");
        string watch = MemberText(stall, "private void Watch()");
        Check(watch.Contains("Time.unscaledTime") && watch.Contains("if (real < _nextWatch)") && watch.Contains("TestBoardRules.Frozen(Time.timeScale)")
              && watch.Contains("Mathf.Min(real - _lastWatch, WatchHitch)"),
            "drive fix: the watch runs on the real clock four times a second, a long frame is not held against the car, and a stopped game is told from a stuck car");
        Check(watch.IndexOf("WatchFrozen(real)", StringComparison.Ordinal) < watch.IndexOf("WatchStall(seconds)", StringComparison.Ordinal),
            "drive fix: nothing is counted against the car while time is stopped");
        Check(MemberText(stall, "private void WatchBoard()").Contains("TestBoardRules.SeatNow(true, _seatedFor, _aboard, true)")
              && MemberText(stall, "private void WatchBoard()").Contains("cast.Seat(_examiner);") && MemberText(stall, "private void WatchBoard()").Contains("_aboard = true;"),
            "drive fix: an examiner who has not reached the car by the rule's wait is seated, and the log says how far they were");
        string stuck = MemberText(stall, "private void WatchStall(float seconds)");
        Check(stuck.Contains("Abort(\"reallife_lic_test_car_stuck\", refund: true)") && stuck.Contains("StallStage.GiveUp") && stuck.Contains("MendStuck(vehicle, pedal)"),
            "drive fix: a car that will not go is mended and then the drive is called off with the fee returned");
        Check(stuck.Contains("Licences check (the car moves): PASS") && stuck.Contains("Meta.Dev.Verbose") && stall.Contains("Licences check (the car moves): FAIL"),
            "drive fix: the test writes 'Licences check (the car moves)' PASS (with the diagnostics on) or FAIL (always) to the log");
        string mend = MemberText(stall, "private void MendStuck(");
        Check(mend.Contains("body.isKinematic = false;") && mend.Contains("body.constraints = RigidbodyConstraints.None;") && mend.Contains("body.WakeUp();")
              && mend.Contains("vehicle.input.Handbrake = 0f;") && mend.Contains("engine.StartEngine();") && mend.Contains("maxFuel") && mend.Contains("body.position += Vector3.up * Lift;"),
            "drive fix: the mend frees a fixed body, a held handbrake, a stopped engine and an empty tank, and lifts a car nothing else explains");
        string frozen = MemberText(stall, "private void WatchFrozen(float real)");
        Check(frozen.Contains("TestBoardRules.LockLeft(speed.isTimeControlDisabled, CityMap.IsOpen") && frozen.Contains("speed.DisableTimeControl(false);") && frozen.Contains("speed.Reset();")
              && frozen.Contains("reallife_lic_test_paused") && frozen.Contains("TestBoardRules.FrozenTold"),
            "drive fix: a lock left on the time controls is let go and time put back as the player left it; a pause the player made is said, not undone");
        Check(stall.Contains("COST:") && !stall.Contains("FindObjectsByType") && !stall.Contains("GetComponentsIn") && !stall.Contains("Physics."),
            "drive fix: the watch's cost is said and it searches and casts nothing");
        Check(MemberText(test, "public void Abort(string reasonKey, bool refund)").Contains("Licences.Give(_feePaid"),
            "drive fix: calling a drive off returns the fee it was paid with");
    }

    private static void EmployersOnTheMap()
    {
        string careers = ModSource(@"Careers\CareerApp.Map.cs");
        string jobs = ModSource(@"Careers\CareerApp.Jobs.cs");
        string app = ModSource(@"Careers\CareerApp.cs");
        string start = ModSource(@"Careers\CareerApp.Start.cs");
        Check(MemberText(jobs, "private void WriteOpening(").Contains("MapButton(buttons, () => _director.ShowPost(o));"),
            "employers fix: a vacancy's page has Show on map");
        Check(MemberText(app, "private void WriteApplication(").Contains("MapButton(box.ButtonRow(), () => _director.ShowPost(a.Post));"),
            "employers fix: an application's page has Show on map");
        Check(app.Contains("MapButton(buttons, _director.ShowWork);") && !app.Contains("common_setdestination"),
            "employers fix: the job held has Show on map where it had the game's Set destination (it now also closes the phone and says where)");
        Check(start.Contains("WriteBoards(box);") && careers.Contains("CareersBridges.Boards") && careers.Contains("_director.ShowBoard(board)")
              && careers.Contains("reallife_career_boards_phone"),
            "employers fix: the Professions tab lists each employer at a board with its address and Show on map, and says when the city has no board for one");
        string director = ModSource(@"Careers\CareersDirector.Map.cs");
        string place = MemberText(director, "private string ShowPlace(");
        Check(place.Contains("DeskGame.Route(street, number)") && place.Contains("CloseApps();") && place.Contains("reallife_career_post_shown")
              && director.Contains("internal string ShowBoard(BoardEmployer board)") && director.Contains("CloseApps();"),
            "employers fix: Show on map puts the game's destination on the building, closes the phone so the marker shows, and says where");
        string bridges = ModSource(@"Careers\CareersBridges.cs");
        string wiring = ModSource(@"Wiring\CareersLinks.cs");
        Check(bridges.Contains("public static Action<List<BoardEmployer>> Boards;") && bridges.Contains("Boards = null;") && bridges.Contains("ShowBoard = null;")
              && wiring.Contains("CareersBridges.Boards = BoardEmployers;") && wiring.Contains("CareersBridges.ShowBoard = ShowBoard;") && wiring.Contains("Premises.Length == 0"),
            "employers fix: the Career app learns the boards through two bridges, wired in Wiring and given back at Reset, and the building trades (no board) are not listed");

        string links = ModSource(@"World\MapLinks.cs");
        int offices = links.IndexOf("Id = \"job_offices\"", StringComparison.Ordinal);
        string officeLink = offices >= 0 ? links.Substring(offices, Math.Min(2400, links.Length - offices)) : string.Empty;
        Check(officeLink.Contains("Found = true") && officeLink.Contains("Alias = employer") && officeLink.Contains("AliasUnder = At(T(\"reallife_map_row_job_offices\")"),
            "employers fix: the Job offices row names each board by its employer in the map's search, the shop it stands at under it");
        Check(links.Contains("SpecialBuildings.Aliases(link.Id, _scratch);") && links.Contains("public bool Found;"),
            "employers fix: a kind's search words are made again when its places are, and taken away with them when its option goes");
        int hiring = links.IndexOf("Id = \"hiring\"", StringComparison.Ordinal);
        string hiringLink = hiring >= 0 ? links.Substring(hiring, Math.Min(900, links.Length - hiring)) : string.Empty;
        Check(hiringLink.Contains("reallife_map_row_hiring") && hiringLink.Contains("Settings.CareersJobs") && hiringLink.Contains("HiringStamp()") && hiringLink.Contains("TickedByDefault")
              && links.Contains("Careers.CareersDirector.Instance.Hiring(_posts)"),
            "employers fix: the city map has an Employers hiring row, its places read again only when the posts open today change, unticked when there are many");
        string stamp = MemberText(ModSource(@"Careers\CareersDirector.Map.cs"), "internal long HiringStamp()");
        Check(stamp.Contains("o.Open(day)") && !stamp.Contains("new ") && !stamp.Contains("Find") && !stamp.Contains("LINQ"),
            "employers fix: the stamp the map asks every three seconds reads the open posts and allocates nothing");

        string book = ModSource(@"Core\SpecialBuildings.cs");
        string names = ModSource(@"Core\SpecialNames.cs");
        Check(book.Contains("internal static void Aliases(string kind, List<MapPlace> places)") && book.Contains("_aliasStamp++;") && book.Contains("Aliased.Clear();")
              && book.Contains("unchecked((Book.Stamp * 31 + FindableTypes.Count) * 31 + _aliasStamp)") && book.Contains("SpecialNames.SearchAgain();"),
            "employers fix: the search words change the book's stamp, so the map's list is made again, and go with the city");
        Check(names.Contains("SpecialBuildings.AllAliases(_aliases);") && names.Contains("listed.Add(alias.Building);") && names.Contains("words.Add(alias.Word);"),
            "employers fix: the city map's search lists each alias with its building, so choosing it takes the map to the shop the board stands at");
    }

    private static void DriveMapTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("drive and employers fix", words, codes))
        {
            return;
        }
        var all = new List<string>(DriveKeys);
        all.AddRange(MapKeys);
        for (int l = 0; l < codes.Length && l < 3; l++)
        {
            bool every = true;
            foreach (string key in all)
            {
                every &= words[l].TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v) && v.IndexOf("  ", StringComparison.Ordinal) < 0 && v.IndexOf('!') < 0
                         && v.IndexOf('–') < 0 && v.IndexOf('—') < 0;
            }
            Check(every, "drive and employers fix: all " + all.Count + " new texts are in " + codes[l] + ", with no double space, exclamation mark or dash");
            bool lengths = true;
            foreach (string key in new[] { "reallife_lic_test_car_stuck", "reallife_lic_test_paused", "reallife_career_post_shown", "reallife_career_why_board_busy" })
            {
                lengths &= words[l].TryGetValue(key, out string v) && v.Length <= 240;
            }
            Check(lengths, "drive and employers fix: the notices are 240 characters or fewer in " + codes[l]);
            foreach (string key in new[] { "reallife_career_post_shown", "reallife_career_boards_at", "reallife_career_boards_phone", "reallife_map_hiring_one", "reallife_map_hiring_many" })
            {
                bool placeholders = words[l].TryGetValue(key, out string v) && v.Contains("{employer}")
                    && (key != "reallife_career_post_shown" && key != "reallife_career_boards_at" || v.Contains("{address}"))
                    && (key != "reallife_map_hiring_many" || v.Contains("{count}"));
                Check(placeholders, "drive and employers fix: " + key + " keeps its fields in " + codes[l]);
            }
        }
        Check(FixLineEverywhere("the driving school's car could be left standing", "o carro da autoescola podia ficar parado", "驾校的车有时停在原地",
                                "Das Auto der Fahrschule konnte stehen bleiben", "la voiture de l'école pouvait rester à l'arrêt", "el coche de la autoescuela podía quedarse parado",
                                "машина школы могла стоять на месте", "de auto van de rijschool kon stilstaan", "Sürücü kursunun arabası olduğu yerde kalabiliyor"),
            "drive fix: What's new has the driving school's car line, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("employers were hard to find", "os empregadores eram difíceis de achar", "雇主不容易找到", "Arbeitgeber waren schwer zu finden",
                                "les employeurs étaient difficiles à trouver", "los empleadores eran difíciles de encontrar", "работодателей было трудно найти",
                                "werkgevers waren moeilijk te vinden", "İşverenleri bulmak zordu"),
            "employers fix: What's new has the employers line, with its fix mark, in some row of every language");
        // The Guide says where an employer is found, in the three languages that carry the pages.
        string[] careers = { "Show on map", "Mostrar no mapa", "在地图上显示" };
        for (int l = 0; l < 3; l++)
        {
            Check(words[l].TryGetValue("reallife_guide_careers", out string page) && page.Contains(careers[l]) && page.Contains("Fairway")
                  && words[l].TryGetValue("reallife_guide_careers_jobs", out string jobs) && jobs.Contains(careers[l]),
                "employers fix: the Guide's careers and professional jobs pages say where an employer's office is and name Show on map in " + codes[l]);
        }
    }
}
