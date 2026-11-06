using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// A building handed over stands at once, and the opening day is not missed (Scripts/Uptown: StandRules, LifeDirector.Stand.cs, LifeGame.Stand.cs,
// DistrictBuildings.StandNow; Scripts/NewBuildings: NewBuildingsDirector.MakeNow, MadeGame.City.cs; Scripts/Civic: the ground given up;
// Scripts/Construction: OpeningRules, the evening and morning notices, the objectives line, the missed opening): when a finished building
// stands (now, waiting, or from the next load), the one declaration a load and a move in play both make, the record marked so the next
// load stands it and sets nothing up twice, then over the sources the one rule, nothing per frame, nothing sent, and the texts.
internal static partial class Program
{
    private static void StandNowChecks()
    {
        StandCallChecks();
        StandSpecChecks();
        StandLoadChecks();
        StandSourceChecks();
        OpeningChecks();
        StandTextChecks();
    }

    // A finished building in a district that lives, with the engine ready, the player far from it.
    private static StandAsk StandBase()
    {
        return new StandAsk { Lives = true, Ready = true, Recorded = true };
    }

    private static void StandCallChecks()
    {
        string why;
        StandAsk far = StandBase();
        Check(StandRules.Call(far, out why) == StandCall.Now && why == null, "stand now: a handed-over building whose ground nobody sees stands at once, with no black screen");
        StandAsk near = StandBase();
        near.InView = true;
        Check(StandRules.Call(near, out why) == StandCall.Wait && why == null, "stand now: one in the camera's picture (or with the city map open) waits, so nothing pops in front of the player");
        near.Black = true;
        Check(StandRules.Call(near, out why) == StandCall.Now, "stand now: the same one stands at the next black screen (a door's fade, a loading screen)");
        StandAsk on = StandBase();
        on.OnGround = true;
        bool onGround = StandRules.Call(on, out _) == StandCall.Wait;
        on.Black = true;
        on.InView = true;
        Check(onGround && StandRules.Call(on, out _) == StandCall.Wait, "stand now: a building never stands on the player: it waits while they are on its ground, in view or not, black or not");
        StandAsk session = StandBase();
        session.InSession = true;
        StandCall inSession = StandRules.Call(session, out string sessionWhy);
        Check(inSession == StandCall.Load && sessionWhy != null && sessionWhy.Contains("Going Public"), "stand now: in a Going Public session the building stands from the next load, on the host's own rules, nothing new on any machine");
        var asks = new List<StandAsk>();
        foreach (int which in new[] { 0, 1, 2, 3, 4, 5 })
        {
            StandAsk a = StandBase();
            switch (which)
            {
                case 0: a.Lives = false; break;
                case 1: a.Ready = false; break;
                case 2: a.Unread = true; break;
                case 3: a.Recorded = false; break;
                case 4: a.Standing = true; break;
                default: a.Settled = true; break;
            }
            asks.Add(a);
        }
        var whys = new HashSet<string>(StringComparer.Ordinal);
        bool loads = true, said = true;
        foreach (StandAsk a in asks)
        {
            StandCall call = StandRules.Call(a, out string w);
            loads &= call == StandCall.Load;
            said &= !string.IsNullOrEmpty(w) && w.IndexOf(" - ", StringComparison.Ordinal) < 0 && !w.Contains("!");
            whys.Add(w ?? string.Empty);
        }
        Check(loads && said && whys.Count == asks.Count, "stand now: a district not living, an engine not ready, a line unreadable, no record, a building there already and one that has stood at a load are each left alone, each with its own why");
        StandAsk blackAndFar = StandBase();
        blackAndFar.Black = true;
        Check(StandRules.Call(blackAndFar, out _) == StandCall.Now && StandRules.OnGroundMargin > 0f, "stand now: a black screen with the ground out of view stands it too");
    }

    private static BuildingsLot StandLot(string id, float facing = 0f)
    {
        return new BuildingsLot
        {
            Id = id, Street = "reallife:street_uptown_1st", Number = 12, Block = 104, X = 120f, Z = 340f, Facing = facing, Width = 20, Depth = 30, Class = BuildingsClass.Warehouse, Version = 1, Spaces = 1
        };
    }

    private static void StandSpecChecks()
    {
        BuildingsLot lot = StandLot("uptown_1_04_0004");
        var f = new FinishedBuilding { Lot = lot.Id, Size = "ba:buildingsize_h", Version = 1, Type = "ba:buildingtype_warehouse", Design = "Factory4 (Warehouse)", Storeys = 0, Owner = FinishOwner.Player, Price = 1669767 };
        MadeSpec a = StandRules.Spec(lot, f, 31, null), b = StandRules.Spec(lot, f, 31, null);
        bool same = a.Id == b.Id && a.Street == b.Street && a.Number == b.Number && a.Size == b.Size && a.Version == b.Version && a.Type == b.Type && a.Design == b.Design
                    && a.Floors == b.Floors && a.DesignArea == b.DesignArea && a.Traffic == b.Traffic && a.Lots.Length == 1 && b.Lots.Length == 1
                    && a.Lots[0].X == b.Lots[0].X && a.Lots[0].Z == b.Lots[0].Z && a.Lots[0].Width == b.Lots[0].Width && a.Lots[0].Depth == b.Lots[0].Depth;
        Check(same, "stand now: the declaration of a finished building is the same every time it is made from its record, as on every machine");
        Check(a.Id == lot.Id && a.Street == lot.Street && a.Number == lot.Number && a.Size == f.Size && a.Version == f.Version && a.Type == f.Type && a.District == DistrictSplit.IdOfColumn(lot.Block % 100)
              && a.Design == "Factory4 (Warehouse)" && a.Life && a.Others == null && a.Preferred == 0 && a.Traffic == 31 && !a.Whole && a.Service == null,
            "stand now: it is declared at its own address, in the structure and design the construction game gave it, filed under its lot's district as the game's own, with no other structure to fall back on");
        Check(a.DesignArea && a.Floors == 1 && a.Lots[0].X == lot.X && a.Lots[0].Z == lot.Z && a.Lots[0].Width == lot.Width && a.Lots[0].Depth == lot.Depth && a.Lots[0].Facing == lot.Facing,
            "stand now: storeys 0 is the floor area of the building its design copies, on the lot's own ground");
        f.Storeys = 4;
        MadeSpec storeys = StandRules.Spec(lot, f, 31, null);
        Check(!storeys.DesignArea && storeys.Floors == 4, "stand now: four storeys of floor area are four, not the design's own");
        f.Design = string.Empty;
        Check(StandRules.Spec(lot, f, 31, null).Design == null, "stand now: no design recorded leaves the pick to the copy engine, the same on every machine");
        // The load's path for a lot built from the first day (structure given apart) and this one are the same function.
        var structure = new BuildingsStructure { Size = f.Size, Version = f.Version, Type = f.Type };
        MadeSpec apart = StandRules.Spec(lot, structure, null, f.Design, f.Storeys, 31, null);
        storeys = StandRules.Spec(lot, f, 31, null);
        Check(apart.Size == storeys.Size && apart.Version == storeys.Version && apart.Type == storeys.Type && apart.Floors == storeys.Floors && apart.DesignArea == storeys.DesignArea
              && apart.Design == storeys.Design, "stand now: a record's declaration and one made from its parts are one function");
        // A building over several lots stands on the rectangle round them all.
        BuildingsLot second = StandLot("uptown_1_04_0005");
        second.X = 140f;
        var span = new List<BuildingsLot> { lot, second };
        Check(BuildingsRules.SpanGround(span, out float sx, out float sz, out float sw, out float sd), "stand now: a span's ground is worked out");
        BuildingsLot ground = lot;
        ground.X = sx;
        ground.Z = sz;
        ground.Width = (int)Math.Floor(sw);
        ground.Depth = (int)Math.Floor(sd);
        MadeSpec spanned = StandRules.Spec(ground, f, 31, null);
        Check(spanned.Id == lot.Id && spanned.Lots[0].X == sx && spanned.Lots[0].Width == ground.Width && spanned.Lots[0].Width > lot.Width, "stand now: a building over several lots keeps its own lot's address and stands on the rectangle round them all");
        // The ground on the world's axes, for the camera and the player's feet.
        StandRules.Rect(lot, out float x1, out float z1, out float x2, out float z2);
        bool avenue = BuildingsRules.FrontsAvenue(lot.Facing);
        float w = avenue ? lot.Width : lot.Depth, d = avenue ? lot.Depth : lot.Width;
        Check(Math.Abs((x2 - x1) - w) < 0.001f && Math.Abs((z2 - z1) - d) < 0.001f && Math.Abs((x1 + x2) * 0.5f - lot.X) < 0.001f && Math.Abs((z1 + z2) * 0.5f - lot.Z) < 0.001f,
            "stand now: the ground a building takes is read on the world's axes, round the lot's middle");
        BuildingsLot turned = StandLot("uptown_1_04_0006", 90f);
        StandRules.Rect(turned, out float tx1, out float tz1, out float tx2, out float tz2);
        Check(Math.Abs((tx2 - tx1) - turned.Depth) < 0.001f && Math.Abs((tz2 - tz1) - turned.Width) < 0.001f, "stand now: a lot on a street that runs the other way has its sides the other way");
        Check(StandRules.OnIt(x1, z1, x2, z2, lot.X, lot.Z) && StandRules.OnIt(x1, z1, x2, z2, x2 + 1.5f, z2 + 1.5f) && !StandRules.OnIt(x1, z1, x2, z2, x2 + 5f, lot.Z) && !StandRules.OnIt(x1, z1, x2, z2, lot.X, z1 - 5f),
            "stand now: the player is on the ground in its middle and a metre or two round it, and off it beyond that");
    }

    private static void StandLoadChecks()
    {
        // The record a building stood in play was set up as it stood: the next load stands the same building from the same record
        // and does not set it up twice (no second deed, no second letting); one that was not stood now is set up at its first load.
        var state = new LifeState();
        var inPlay = new FinishedBuilding { Lot = "uptown_1_04_0004", Size = "ba:buildingsize_h", Version = 1, Type = "ba:buildingtype_warehouse", Design = "Factory4 (Warehouse)", Owner = FinishOwner.Player, Price = 1669767, Day = 45 };
        var waits = new FinishedBuilding { Lot = "uptown_1_05_0002", Size = "ba:buildingsize_c", Version = 1, Type = "ba:buildingtype_retail", Owner = FinishOwner.City, Day = 45 };
        state.Finished.Add(inPlay);
        state.Finished.Add(waits);
        bool dueBefore = StandRules.FirstLoadDue(inPlay) && StandRules.FirstLoadDue(waits);
        StandRules.SetUp(inPlay);   // as it stood in play
        bool dueAfter = !StandRules.FirstLoadDue(inPlay) && StandRules.FirstLoadDue(waits);
        LifeState back = LifeStore.Read(LifeStore.Write(state), out _);
        bool kept = back != null && back.Finished.Count == 2 && !StandRules.FirstLoadDue(back.On("uptown_1_04_0004")) && StandRules.FirstLoadDue(back.On("uptown_1_05_0002"));
        Check(dueBefore && dueAfter && kept, "stand now: a building stood in play is marked set up and the mark is in the save, so the next load sets nothing up twice; one not stood yet is set up at its first load");
        // The next load declares each record once, from the same function that declared it in play.
        MadeSpec now = StandRules.Spec(StandLot("uptown_1_04_0004"), inPlay, 31, null), load = StandRules.Spec(StandLot("uptown_1_04_0004"), back.On("uptown_1_04_0004"), 31, null);
        Check(now.Id == load.Id && now.Size == load.Size && now.Version == load.Version && now.Type == load.Type && now.Design == load.Design && now.Floors == load.Floors && now.DesignArea == load.DesignArea
              && now.Lots[0].Width == load.Lots[0].Width && now.Lots[0].Depth == load.Lots[0].Depth,
            "stand now: the building the next load declares from the saved record is the building that stood in play: same address, structure, design, floors and ground");
        Check(!StandRules.FirstLoadDue(null), "stand now: no record, nothing to set up");
        StandRules.SetUp(null);   // a missing record is no fault
    }

    private static void StandSourceChecks()
    {
        string wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs"), director = ModSource(@"Uptown\LifeDirector.cs"), stand = ModSource(@"Uptown\LifeDirector.Stand.cs");
        string game = ModSource(@"Uptown\LifeGame.Stand.cs"), city = ModSource(@"NewBuildings\MadeGame.City.cs"), builder = ModSource(@"NewBuildings\NewBuildingsDirector.cs");
        string cleared = ModSource(@"Uptown\LifeDirector.Cleared.cs"), rules = ModSource(@"Uptown\StandRules.cs"), districts = ModSource(@"Uptown\DistrictBuildings.cs");
        Check(wiring.Length > 0 && director.Length > 0 && stand.Length > 0 && game.Length > 0 && city.Length > 0 && builder.Length > 0 && cleared.Length > 0 && rules.Length > 0,
            "stand now sources: the rules, the district's part, the game's part, the city's list and the wiring are there");
        // One rule for every finished building the construction game hands over: the project's, a rival's development, the city's redevelopment.
        string finish = MethodText(wiring, "private static bool Finish("), finishOver = MethodText(wiring, "private static bool FinishOver("), standOne = MethodText(wiring, "private static bool Stand(");
        Check(finish.Contains("DistrictBuildings.Finish(") && finish.Contains("&& Stand(lot)") && finishOver.Contains("DistrictBuildings.FinishOver(") && finishOver.Contains("&& Stand(lot)")
              && standOne.Contains("DistrictBuildings.StandNow(lot, out _)") && standOne.Contains("return true;"),
            "stand now sources: every building the construction game hands to the district is stood by the one call, and handed over all the same when it cannot be");
        Check(wiring.Contains("DistrictBuildings.GroundGiven = GroundGiven;") && wiring.Contains("DistrictBuildings.GroundGiven = null;") && wiring.Contains("SiteBuilders.CopyDrop = ")
              && ModSource(@"Civic\CivicDirector.cs").Contains("_looks?.DropStood();") && ModSource(@"Civic\CivicDirector.cs").Contains("_sites?.ReleaseOn(x1, z1, x2, z2);"),
            "stand now sources: the site's props and the look over its ground come down in the same moment, and the wiring is taken off with the rest");
        // The load and the move in play make the same declaration and set up the same way.
        string declare = MethodText(director, "private static void Declare(");
        Check(declare.Contains("MadeSpec kept = FinishedSpec(lot, f);") && declare.Contains("MadeBuildings.Declare(kept);") && stand.Contains("BuildingsLot ground = GroundOf(lot, f);") && stand.Contains("StandRules.Spec(ground, f, TrafficOf(lot.Id)")
              && ModSource(@"Uptown\LifeGame.Buildings.cs").Contains("return StandRules.Spec(lot, structure, others, design, storeys, traffic, wanted);"),
            "stand now sources: the load declares a finished building from the same function a move in play does");
        Check(MethodText(director, "private static bool FirstLife(").Contains("if (!StandRules.FirstLoadDue(f))") && MethodText(director, "private static bool FirstLife(").Contains("StandRules.SetUp(f);")
              && stand.Contains("StandRules.SetUp(f);") && stand.Contains("LifeGame.Settle(made, f.Owner, f.Price, SaveGameManager.Current)"),
            "stand now sources: the deed or the letting is written once, in play or at the first load, by the same call");
        // The same make as at a load: the copy engine's Make, then the district's lists, the carving, the fillers, the city's list.
        Check(builder.Contains("Make(_specs[i])") && builder.Contains("if (!Make(spec, true)") && stand.Contains("director.MakeNow(spec, out why)") && stand.Contains("FrontageRules.Plan(Plan,")
              && stand.Contains("LifeGame.Enter(made);") && stand.Contains("LifeGame.Cut(made);") && stand.Contains("LifeGame.StandFillers(StandFills);") && stand.Contains("MadeGame.JoinCity(made, out string joinWhy)"),
            "stand now sources: the building is made in play by the copy engine's own Make, arranged by the streets' own rules, then cut, filled and listed as at a load");
        // The city's list is joined only when the game's closest-building job can be made again, else it is not touched.
        Check(city.IndexOf("GameContract.ClosestReset.Method == null", StringComparison.Ordinal) > 0
              && city.IndexOf("GameContract.ClosestReset.Method == null", StringComparison.Ordinal) < city.IndexOf("GameDlc.Join(city, c, address);", StringComparison.Ordinal)
              && city.IndexOf("GameDlc.Join(city, c, address);", StringComparison.Ordinal) < city.IndexOf("ResetClosest();", StringComparison.Ordinal),
            "stand now sources: the city's list grows only when the closest-building job can be made again from it (its arrays are as long as the list they were made from)");
        // Nothing per frame, nothing sent, no Harmony: a one-off at the handover, once a second while one waits, at a black screen.
        Check(!Regex.IsMatch(stand + game + city + rules, @"void Update\(|void LateUpdate\(|void FixedUpdate\(|FindObjectsOfType|FindObjectsByType|FindObjectOfType|using System\.Linq|\.Where\(|\.Select\(|Harmony"),
            "stand now sources: nothing per frame, no search of the scene, no LINQ, no Harmony");
        Check(!Regex.IsMatch(stand + game + city + rules + districts, @"GoingPublic\.Send|Network|Rpc|SharedCity"), "stand now sources: nothing is sent: a building stood in play is this machine's own, and a session leaves it to the load");
        Check(stand.Contains("InSession = GoingPublic.InSession") && stand.Contains("ScenePrewarm.Again(") && MethodText(director, "private void OnUpdate(").Contains("StandPending(false);"),
            "stand now sources: a Going Public session is asked, a waiting building stands at a black screen or once a second while nobody sees it");
        // A building knocked down in this session lets go its address and ground, so one finished on its lot is made in play too.
        Check(cleared.Contains("NewBuildingsDirector.Instance?.Retire(made);") && cleared.Contains("MadeBuildings.Withdraw(made.Spec.Id);") && builder.Contains("CopyGame.ReleaseMade(old.Spec.Id);")
              && stand.Contains("director.Retire(knocked);"), "stand now sources: a building knocked down lets go its address and its ground, so a new one finished there stands in play");
        Check(MethodText(director, "private static void Begin(").Contains("Waiting.Clear();") && MethodText(director, "private void OnDestroy(").Contains("Waiting.Clear();"),
            "stand now sources: nothing waits across a load or the city going");
        Check(ModSource(@"Core\GameContract.cs").Contains("\"_cityBuildingControllersDictionary\"") && ModSource(@"Core\GameContract.cs").Contains("\"Buildings.ClosestBuildingFromPlayer\", \"OnDestroy\""),
            "stand now sources: the game's two private pieces the city's list needs are lines of the contract");
        Check(ModSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("DistrictBuildings.StandNow") && !ModSource(@"Landmarks\LandmarksDirector.Works.cs").Contains("StandNow"),
            "stand now sources: the construction game's finished buildings stand in play; the landmarks stand as they did (from the load)");
    }

    private static void OpeningChecks()
    {
        Check(OpeningRules.Notice(46, 45, OpeningRules.EveHour) == OpeningNotice.Eve && OpeningRules.Notice(46, 45, OpeningRules.EveHour - 1) == OpeningNotice.None
              && OpeningRules.Notice(46, 45, OpeningRules.EveHour + 1) == OpeningNotice.None && OpeningRules.Notice(46, 44, OpeningRules.EveHour) == OpeningNotice.None,
            "opening: a notice the evening before, at six, and no other evening");
        Check(OpeningRules.Notice(46, 46, OpeningRules.MorningHour) == OpeningNotice.Morning && OpeningRules.Notice(46, 46, OpeningRules.MorningHour + 1) == OpeningNotice.None
              && OpeningRules.Notice(46, 47, OpeningRules.MorningHour) == OpeningNotice.None && OpeningRules.Notice(0, 0, OpeningRules.MorningHour) == OpeningNotice.None && OpeningRules.Notice(0, -1, OpeningRules.EveHour) == OpeningNotice.None,
            "opening: a notice on the morning of the day, two hours before the ribbon may be cut, and none for a building with no opening day");
        Check(OpeningRules.MorningHour == OpeningRules.From - 2 && OpeningRules.From == 10 && OpeningRules.To == 18, "opening: the ribbon is cut between ten and six, as it was");
        Check(!OpeningRules.Over(46, 46, 17) && OpeningRules.Over(46, 46, 18) && OpeningRules.Over(46, 47, 0) && !OpeningRules.Over(46, 45, 23) && !OpeningRules.Over(0, 50, 20),
            "opening: it is held without the player once six has struck on its day, or its day has gone");
        Check(OpeningRules.Standing(true) == 3 && OpeningRules.Standing(false) == 1 && OpeningRules.Lost() == 2, "opening: what it gives is as it was: standing 3 with the ribbon, 1 without, so a missed one costs 2");
        // The objectives line is urgent from the handover.
        var s = new BuildState();
        var handed = new ProjectFile { Id = 2, Kind = ProjectKind.Plot, Stage = ProjectStage.Finished, Number = 2, Handed = 45, Opening = 46, Storeys = 4, DueDay = 36, StageSeen = 2 };
        s.Projects.Add(handed);
        var f = new FakeTrack { Today = 45, Now = 14 };
        List<TrackLine> lines = TrackOf(s, f);
        Check(lines.Count == 1 && lines[0].Need == TrackNeed.Urgent && lines[0].Key == "reallife_bld_track_opening" && lines[0].Day == 46, "opening: the objectives show the opening as urgent from the day of the handover");
        f.Today = 46;
        lines = TrackOf(s, f);
        Check(lines.Count == 1 && lines[0].Need == TrackNeed.Urgent && lines[0].Key == "reallife_bld_track_opening_today", "opening: and on the day itself");
        handed.Opened = true;
        Check(TrackOf(s, f).Count == 0, "opening: once it has opened, the line is gone");
        // The director.
        string day = ModSource(@"Construction\ConstructionDirector.Day.cs"), actions = ModSource(@"Construction\ConstructionDirector.Actions.cs"), tests = ModSource(@"Construction\ConstructionDirector.Tests.cs");
        string hour = MethodText(day, "private void OpeningHour(");
        Check(hour.Contains("OpeningRules.Notice(p.Opening, day, hour)") && hour.Contains("\"reallife_bld_notice_opening_eve\"") && hour.Contains("\"reallife_bld_notice_opening\"")
              && hour.Contains("p.Opening != day + 1") && hour.Contains("OpeningRules.Over(p.Opening, day, hour)"), "opening sources: each hour owes the evening's and the morning's notice and holds the opening once its hours are over");
        string open = MethodText(actions, "private void Open(ProjectFile p, bool ribbon)");
        Check(open.Contains("Standing(OpeningRules.Standing(ribbon), false);") && open.Contains("if (!ribbon)") && open.Contains("OpeningMissed(p);") && open.Contains("if (p.Opened)"),
            "opening sources: an opening held without the player says so, once (an opened building returns first), and its standing is the rules'");
        string missed = MethodText(actions, "private void OpeningMissed(");
        Check(missed.Contains("\"reallife_bld_notice_opening_missed\"") && missed.Contains("{ \"got\"") && missed.Contains("{ \"full\"") && missed.Contains("NotificationType.Warning"), "opening sources: the missed opening is a warning with what was gained and what the ribbon would have given");
        Check(ModSource(@"Construction\ConstructionDirector.App.cs").Contains("OpeningFrom = OpeningRules.From, OpeningTo = OpeningRules.To") && tests.Contains("public void TestOpeningMissed()")
              && ModSource(@"Construction\ConstructionGuide.cs").Contains("reallife_bld_test_opening_missed") && ModSource(@"TestRun\RunPlan.cs").Contains("reallife_bld_test_opening_missed")
              && ModSource(@"TestRun\RunList.cs").Contains("reallife_bld_test_opening_missed"), "opening sources: the hours are the rules', and the missed opening is a test, a button and a check of the run");
        Check(!Regex.IsMatch(ModSource(@"Construction\OpeningRules.cs"), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|void Update\("), "opening sources: OpeningRules is game-free and runs nothing a frame");
    }

    private static void StandTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        bool keys = true, notices = true, holders = true, mended = true;
        var fix = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "a finished building now stands the moment it is handed over" }, { "pt", "um prédio concluído agora aparece no momento da entrega" }, { "zh-cn", "竣工的建筑现在在交付的那一刻就立起来" },
            { "de", "Ein fertiges Gebäude steht jetzt in dem Moment" }, { "fr", "un bâtiment terminé se dresse désormais à l'instant" }, { "es", "un edificio terminado ahora se levanta en el momento" },
            { "ru", "готовое здание теперь появляется в тот момент" }, { "nl", "een afgebouwd gebouw staat nu op het moment" }, { "tr", "bitmiş bir bina artık teslim edildiği anda" }
        };
        bool fixes = true;
        for (int l = 0; l < words.Length; l++)
        {
            if (words[l] == null)
            {
                continue;
            }
            if (l < ModLanguages.Required)
            {
                foreach (string key in new[] { "reallife_bld_notice_opening_eve", "reallife_bld_notice_opening_missed", "reallife_bld_yours_now", "reallife_bld_test_opening_missed" })
                {
                    keys &= words[l].TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v);
                }
                holders &= words[l].TryGetValue("reallife_bld_notice_opening_eve", out string eveText) && eveText.Contains("{name}")
                           && words[l].TryGetValue("reallife_bld_notice_opening_missed", out string missText) && missText.Contains("{name}") && missText.Contains("{got}") && missText.Contains("{full}");
            }
            notices &= !words[l].TryGetValue("reallife_bld_notice_opening_eve", out string eve) || (eve.Length <= 240 && !eve.Contains("!") && !eve.Contains("\u2014") && !eve.Contains("\u2013"));
            notices &= !words[l].TryGetValue("reallife_bld_notice_opening_missed", out string miss) || (miss.Length <= 240 && !miss.Contains("!") && !miss.Contains("\u2014") && !miss.Contains("\u2013"));
            // The line in What's new, in any row (the release turns the next row into the version's): once, in every language that has the row.
            if (fix.TryGetValue(codes[l], out string phrase))
            {
                int count = 0;
                foreach (KeyValuePair<string, string> row in words[l])
                {
                    if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal))
                    {
                        count += Regex.Matches(row.Value, Regex.Escape(phrase)).Count;
                    }
                }
                fixes &= count == 1;
            }
        }
        mended &= words.Length > 0 && words[0] != null && words[0].TryGetValue("reallife_guide_construction", out string guide) && !guide.Contains("yours from the next load")
                  && words[0].TryGetValue("reallife_bld_yours_now", out string yours) && !yours.Contains("next load");
        Check(keys && holders, "stand now texts: the evening's notice, the missed opening's, the building that stands and the test are in the required languages, each with its words to fill");
        Check(notices, "stand now texts: each notice is at most 240 characters, with no exclamation mark and no dash");
        Check(fixes, "stand now texts: the fix line is in What's new once, in all nine languages");
        Check(mended, "stand now texts: the Guide no longer says a finished building is yours from the next load");
    }
}
