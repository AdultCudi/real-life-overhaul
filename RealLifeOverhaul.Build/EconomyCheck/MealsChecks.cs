using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Meals;

// Meals and music (Scripts/Meals): which seat a meal is taken to and that the walk keeps to the room, how long it is
// eaten, when dancing lifts the mood and how often; then, over the sources, that the food's effect stays the game's own
// (and home cooking's), that the game is touched only through the feature's Game, Body and Button files, that every way
// out gives back what was borrowed, that nothing is saved, sent or done to the clock, and the texts in every language.
internal static partial class Program
{
    private static void Meals()
    {
        MealSeatChecks();
        MealFloorChecks();
        MealTimeChecks();
        DanceRuleChecks();
        MealSourceChecks();
        MealTextChecks();
        Takeout();   // takeout and plates: TakeoutChecks.cs
    }

    private static SeatOption MealSeatAt(float flat, float path, bool table, bool free = true, float up = 0f)
    {
        return new SeatOption { Flat = flat, Path = path, Table = table, Free = free, Up = up };
    }

    private static void MealSeatChecks()
    {
        // Worth sitting down to: a meal, never a sip.
        Check(!MealsRules.Wanted(0f) && !MealsRules.Wanted(7.99f) && MealsRules.Wanted(8f) && MealsRules.Wanted(100f), "meals: a rise of 8 or more is a meal worth sitting down to, less is not");

        // The same room: a walk about as long as the straight line; round a wall through a door is another room.
        Check(MealsRules.SameSpace(4f, 4f) && MealsRules.SameSpace(4f, 7.5f) && !MealsRules.SameSpace(4f, 7.6f), "meals: a walk up to one and a half times the line plus 1.5 m keeps to the room");
        Check(!MealsRules.SameSpace(4f, -1f) && MealsRules.SameSpace(0f, 0f) && MealsRules.SameSpace(0f, 1.5f) && !MealsRules.SameSpace(0f, 1.6f), "meals: no way there is never the room; a seat at the feet is");

        // What may be used at all.
        Check(MealsRules.Usable(MealSeatAt(3f, 3f, true)), "meals: a free seat in reach on the same floor is usable");
        Check(!MealsRules.Usable(MealSeatAt(3f, 3f, true, false)), "meals: a seat somebody uses is not");
        Check(!MealsRules.Usable(MealSeatAt(9.1f, 9.1f, true)) && MealsRules.Usable(MealSeatAt(9f, 9f, true)), "meals: the farthest seat is 9 m away");
        Check(!MealsRules.Usable(MealSeatAt(3f, 3f, true, true, 1.1f)) && !MealsRules.Usable(MealSeatAt(3f, 3f, true, true, -1.1f)) && MealsRules.Usable(MealSeatAt(3f, 3f, true, true, -1f)),
            "meals: a seat on another floor or a stage, more than a metre up or down, is not");
        Check(!MealsRules.Usable(MealSeatAt(3f, -1f, true)), "meals: a seat with no way there is not");

        // The pick: a table before any other seat, then the shortest walk, then the earlier.
        SeatOption[] seats = { MealSeatAt(2f, 2f, false), MealSeatAt(5f, 6f, true), MealSeatAt(4f, 4.5f, true), MealSeatAt(1f, 1f, false, false) };
        Check(MealsRules.Pick(seats, 4) == 2, "meals: the nearer of two table seats is picked over a nearer sofa: " + MealsRules.Pick(seats, 4));
        Check(MealsRules.Pick(seats, 1) == 0, "meals: with only a sofa, the sofa");
        Check(MealsRules.Pick(seats, 0) == -1 && MealsRules.Pick(new SeatOption[0], 3) == -1, "meals: no seats, no pick");
        Check(MealsRules.Pick(seats, 99) == 2, "meals: a count past the array's end reads only what is there");
        SeatOption[] equal = { MealSeatAt(3f, 3f, true), MealSeatAt(3f, 3f, true) };
        Check(MealsRules.Pick(equal, 2) == 0, "meals: of two equal seats, the first");
        SeatOption[] none = { MealSeatAt(3f, 30f, true), MealSeatAt(12f, 12f, false), MealSeatAt(2f, 2f, true, false) };
        Check(MealsRules.Pick(none, 3) == -1, "meals: a seat in another room, one too far and one taken: none, the meal is eaten where the player stands");
        SeatOption[] walled = { MealSeatAt(2f, 9f, true), MealSeatAt(6f, 6.5f, false) };
        Check(MealsRules.Pick(walled, 2) == 1, "meals: a table behind a wall loses to a chair in the room");
        bool same = true;
        for (int i = 0; i < 50; i++)
        {
            same &= MealsRules.Pick(seats, 4) == 2 && MealsRules.Pick(walled, 2) == 1;
        }
        Check(same, "meals: the pick is the same every time for the same seats");
        Check(MealsRules.MostLooked == 8 && MealsRules.MostLooked > 0, "meals: the walk is measured for the eight nearest seats at most");

        // A table top in front of a chair.
        Check(!MealsRules.TableTop(0.5f) && MealsRules.TableTop(0.55f) && MealsRules.TableTop(0.75f) && MealsRules.TableTop(1.05f) && !MealsRules.TableTop(1.1f),
            "meals: a table top is 0.55 to 1.05 m above the seat's floor (a dining table, a counter), never the floor or a shelf");

        // Timing: the pick runs once a meal over eight seats; far within a millisecond.
        double ms = PickTime(seats);
        Check(ms < 200.0, "meals: 200,000 picks over four seats in under 200 ms: " + ms.ToString("0.0") + " ms");
    }

    // A house with an upper floor (a player's report: nobody sat down after cooking): the building lists the seats upstairs
    // among those below, and the search kept the first it came to and measured the eight nearest, all of them upstairs and
    // never usable, so the table downstairs was never looked at. Only the player's own floor is kept now, the nearest of it
    // whatever order the list is in.
    private static void MealFloorChecks()
    {
        Check(MealsRules.SameFloor(0f) && MealsRules.SameFloor(1f) && MealsRules.SameFloor(-1f) && !MealsRules.SameFloor(1.01f) && !MealsRules.SameFloor(-1.01f)
              && !MealsRules.SameFloor(3.5f) && !MealsRules.SameFloor(-3.5f),
            "meals floors: a seat within a metre up or down is on the player's floor, one a storey up or down is not");
        bool sameRule = true;
        for (float up = -5f; up <= 5f; up += 0.25f)
        {
            sameRule &= MealsRules.Usable(MealSeatAt(3f, 3f, true, true, up)) == MealsRules.SameFloor(up);
        }
        Check(sameRule, "meals floors: a seat is usable on the player's floor by the same rule the shortlist keeps to");

        var kept = new SeatOption[4];
        Check(MealsRules.Slot(kept, 0, 4, 2f, 0f) == 0 && MealsRules.Slot(kept, 3, 4, 2f, 0f) == 3, "meals floors: while there is room, a seat takes the next place");
        Check(MealsRules.Slot(kept, 0, 4, 2f, 3.5f) == -1 && MealsRules.Slot(kept, 0, 4, 2f, -3.5f) == -1, "meals floors: a seat upstairs or downstairs takes no place");
        Check(MealsRules.Slot(kept, 0, 4, 9.01f, 0f) == -1 && MealsRules.Slot(kept, 0, 4, 9f, 0f) == 0, "meals floors: a seat beyond the farthest a meal is carried takes no place");
        Check(MealsRules.Slot(null, 0, 4, 2f, 0f) == -1 && MealsRules.Slot(kept, 0, 0, 2f, 0f) == -1, "meals floors: no shortlist, or no room in it, no place");
        kept[0].Flat = 3f;
        kept[1].Flat = 7f;
        kept[2].Flat = 5f;
        kept[3].Flat = 6f;
        Check(MealsRules.Slot(kept, 4, 4, 4f, 0f) == 1, "meals floors: a full shortlist gives a nearer seat the farthest one's place");
        Check(MealsRules.Slot(kept, 4, 4, 7f, 0f) == -1 && MealsRules.Slot(kept, 4, 4, 8f, 0f) == -1, "meals floors: a full shortlist takes no seat that is no nearer than all it keeps");
        Check(MealsRules.Slot(kept, 9, 99, 4f, 0f) == 1, "meals floors: a count or room past the shortlist's end reads only what is there");

        // The house of the report: twenty seats upstairs over the kitchen listed first, a sofa in the cellar, then the six
        // at the dining table on the player's floor, farther across the floor than every seat upstairs.
        var house = new List<SeatOption>();
        for (int i = 0; i < 20; i++)
        {
            house.Add(MealSeatAt(1f + i * 0.2f, -1f, false, true, 3.5f + (i % 3) * 0.3f));
        }
        house.Add(MealSeatAt(2f, -1f, false, true, -3.2f));
        for (int i = 0; i < 6; i++)
        {
            house.Add(MealSeatAt(6f + i * 0.3f, 6.5f + i * 0.3f, true, true, 0.45f));
        }
        Check(MealOldPick(house) < 0, "meals floors: the search as it was kept the first seats listed and found none to sit at (the report)");
        int pick = MealShortlistPick(house, out SeatOption[] measured, out int count);
        Check(count == 6 && measured.Length == 6 && pick == 0 && measured[0].Table && Near(measured[0].Flat, 6f) && MealsRules.SameFloor(measured[0].Up),
            "meals floors: now the six at the dining table are the shortlist and the nearest of them is the seat (" + count + " kept, pick " + pick + ")");

        // Whatever order the building lists its furniture in, the same seat.
        var random = new Random(20261023);
        bool same = true;
        for (int round = 0; round < 40; round++)
        {
            var shuffled = new List<SeatOption>(house);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            int p = MealShortlistPick(shuffled, out SeatOption[] m, out int n);
            same &= n == 6 && p >= 0 && Near(m[p].Flat, 6f);
        }
        Check(same, "meals floors: the same seat whatever order the building lists its furniture in");

        // A big room on one floor: of forty seats the nearest twenty-four are kept, listed nearest last.
        var hall = new List<SeatOption>();
        for (int i = 39; i >= 0; i--)
        {
            hall.Add(MealSeatAt(0.5f + i * 0.2f, 0.5f + i * 0.2f, false));
        }
        MealShortlistPick(hall, out SeatOption[] near, out int hallCount);
        Check(hallCount == 24 && near.Length == MealsRules.MostLooked && Near(near[0].Flat, 0.5f) && Near(near[MealsRules.MostLooked - 1].Flat, 0.5f + (MealsRules.MostLooked - 1) * 0.2f),
            "meals floors: the shortlist keeps the nearest seats when there are more than it holds, and the walk is measured for the nearest eight");

        // Over the sources: both of the building's lists of seats go through the shortlist, and the line counts the floors left out.
        string game = MealsSource("MealsGame.cs");
        string indoors = MealCode(MemberText(game, "private static int Indoors(Vector3 from)"));
        string keep = MemberText(game, "private static int Keep(Transform place, Vector3 from, int count)");
        Check(Occurs(indoors, "Keep(place, from, count)") == 2 && Occurs(indoors, "Options[slot] = seat.Option;") == 2 && !indoors.Contains("count++")
              && !indoors.Contains("count < MostSeats"),
            "meals floors sources: the table seats and the other seats go through the shortlist, with no stop after the first seats listed");
        Check(keep.Contains("MealsRules.SameFloor(to.y)") && keep.Contains("_elsewhere++;") && keep.Contains("MealsRules.Slot(Options, count, MostSeats, Mathf.Sqrt(CitySpots.Flat(to)), to.y)"),
            "meals floors sources: a seat on another floor is counted as left out and takes no place; the others by their distance across the floor");
        string director = MealsSource("MealsDirector.cs");
        Check(Occurs(director, "out int looked, out int elsewhere") == 2 && Occurs(director, "Elsewhere(elsewhere)") == 2
              && MealsSource("MealsDirector.Tests.cs").Contains("out int looked, out int elsewhere"),
            "meals floors sources: a meal's line and the test say how many seats on another floor were left out");
    }

    // The shortlist as MealsGame.Indoors keeps it (MealsRules.Slot, in the order listed), then its nearest MostLooked
    // measured and picked as MealsGame.Find does.
    private static int MealShortlistPick(List<SeatOption> listed, out SeatOption[] measured, out int count)
    {
        const int most = 24;
        var shortlist = new SeatOption[most];
        count = 0;
        foreach (SeatOption seat in listed)
        {
            int slot = MealsRules.Slot(shortlist, count, most, seat.Flat, seat.Up);
            if (slot < 0)
            {
                continue;
            }
            count += slot == count ? 1 : 0;
            shortlist[slot] = seat;
        }
        var nearest = new List<SeatOption>(shortlist).GetRange(0, count);
        nearest.Sort((a, b) => a.Flat.CompareTo(b.Flat));
        measured = nearest.GetRange(0, Math.Min(count, MealsRules.MostLooked)).ToArray();
        return MealsRules.Pick(measured, measured.Length);
    }

    // The search before the fix: the first twenty-four seats in the order listed, whatever their floor, then the eight nearest.
    private static int MealOldPick(List<SeatOption> listed)
    {
        var first = listed.GetRange(0, Math.Min(24, listed.Count));
        first.Sort((a, b) => a.Flat.CompareTo(b.Flat));
        SeatOption[] measured = first.GetRange(0, Math.Min(first.Count, MealsRules.MostLooked)).ToArray();
        return MealsRules.Pick(measured, measured.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PickTime(SeatOption[] seats)
    {
        var watch = Stopwatch.StartNew();
        int sum = 0;
        for (int i = 0; i < 200000; i++)
        {
            sum += MealsRules.Pick(seats, 4);
        }
        watch.Stop();
        return sum == 400000 ? watch.Elapsed.TotalMilliseconds : double.MaxValue;
    }

    private static void MealTimeChecks()
    {
        Check(Near(MealsRules.EatSeconds(0f), 8f) && Near(MealsRules.EatSeconds(-5f), 8f), "meals: the shortest meal is 8 s, and a meal that filled nothing is not shorter");
        Check(Near(MealsRules.EatSeconds(40f), 13f), "meals: a meal that fills 40 is eaten for 13 s: " + MealsRules.EatSeconds(40f));
        Check(Near(MealsRules.EatSeconds(56f), 15f) && Near(MealsRules.EatSeconds(100f), 15f), "meals: the longest meal is 15 s, however big");
        bool rising = true;
        for (int fill = 0; fill < 100; fill++)
        {
            rising &= MealsRules.EatSeconds(fill + 1) >= MealsRules.EatSeconds(fill);
        }
        Check(rising, "meals: a bigger meal is never eaten faster");
        Check(MealsRules.WaitMost > 0f && MealsRules.WaitMost <= 6f && MealsRules.WalkMost >= 10f && MealsRules.WalkMost <= 30f,
            "meals: the game's own bite is waited for a few seconds at most, a walk given up after 20 s");
    }

    private static void DanceRuleChecks()
    {
        Check(!MealsRules.Lifts(14f, 1000f, -1f) && MealsRules.Lifts(15f, 1000f, -1f), "dance: the mood lifts after 15 game minutes of dancing, not before");
        Check(!MealsRules.Lifts(30f, 1000f, 1000f - 60f), "dance: not again an hour after the last lift");
        Check(!MealsRules.Lifts(30f, 1000f, 1000f - 479f) && MealsRules.Lifts(30f, 1000f, 1000f - 480f), "dance: again from 8 game hours after the last lift");
        Check(MealsRules.Lifts(30f, 100f, 900f), "dance: a clock behind the last lift (another save) does not hold the lift back for good");
        bool never = true;
        bool inRange = true;
        for (int last = -1; last < MealsRules.DanceKinds; last++)
        {
            for (int r = 0; r <= 100; r++)
            {
                int next = MealsRules.NextDance(last, r / 100f);
                inRange &= next >= 0 && next < MealsRules.DanceKinds;
                never &= next != last;
            }
        }
        Check(inRange && never, "dance: the next dance is one of the game's nine, never the one danced last");
        Check(MealsRules.NextDance(3, -1f) == 4 && MealsRules.NextDance(8, 0f) == 0 && MealsRules.NextDance(0, 2f) == 8, "dance: a roll outside 0 to 1 still gives one of the nine");
        var seen = new HashSet<int>();
        for (int r = 0; r < 100; r++)
        {
            seen.Add(MealsRules.NextDance(4, r / 100f));
        }
        Check(seen.Count == MealsRules.DanceKinds - 1, "dance: from one dance every other dance can come next: " + seen.Count);
    }

    // A source without its comments, for the checks that look for what the code calls.
    private static string MealCode(string source)
    {
        return Regex.Replace(source, @"//[^
]*", string.Empty);
    }

    private static string MealsSource(string name)
    {
        return ModSource(@"Meals\" + name);
    }

    private static void MealSourceChecks()
    {
        string[] names = { "MealsDirector.cs", "MealsDirector.Dance.cs", "MealsDirector.Tests.cs", "MealsDirector.Takeout.cs", "MealsGame.cs", "MealBody.cs", "DanceButton.cs", "TakeoutButton.cs",
                           "MealsBridges.cs", "MealsGuide.cs", "MealsRules.cs", "MealsText.cs" };   // TakeoutGame.cs, the one file that eats from the bag, is checked in TakeoutChecks.cs
        var code = new Dictionary<string, string>();
        foreach (string name in names)
        {
            code[name] = MealsSource(name);
            Check(code[name].Length > 0, "meals sources: " + name + " is there");
        }
        string all = MealCode(string.Join("\n", code.Values));   // the code, without its comments

        // The food's effect is the game's own (or home cooking's): nothing here fills, pays, gives energy or a second food event.
        Check(!Regex.IsMatch(all, @"ChangeHunger|SetCurrentHunger|ChangeMoney|GenerateEnergy|EnergyGeneratedFromConsumables|ReduceFromCargo|GameEvent\.Invoke|TryToConsume"),
            "meals sources: nothing of the food's effect is done here: it happened once, in the game or home cooking, before any of this");
        // Only the Game, Body and Button files touch the game; nothing is saved, sent or done to the clock.
        foreach (string name in new[] { "MealsDirector.cs", "MealsDirector.Dance.cs", "MealsDirector.Tests.cs", "MealsDirector.Takeout.cs", "MealsBridges.cs", "MealsRules.cs", "MealsText.cs", "MealsGuide.cs" })
        {
            Check(!Regex.IsMatch(MealCode(code[name]), @"\bInstanceBehavior\b|\bPlayerDances\b|\bSitOnChair\b|\bSetGoal\b|\bNotifications\.|\bLoudSpeakersManager\b|\bPhysics\.|\bNavMesh\.|\ballItemControllers\b"),
                "meals sources: only MealsGame, MealBody and DanceButton touch the game (" + name + ")");
        }
        Check(!Regex.IsMatch(all, @"\bmodData\b|\bPlayerPrefs\b|\bMPClient\b|\bMPServer\b|\bSharedCity\b|\bSend\("), "meals sources: nothing is saved and nothing sent");
        Check(!Regex.IsMatch(all, @"TimeMachine|timeScale|ForceTimeMachine|SetGameSpeed|ChangeTimeSpeed|\.Pause\("), "meals sources: the clock is never stopped, skipped or sped (Going Public's shared clock)");
        Check(!Regex.IsMatch(MealCode(code["MealsRules.cs"]), @"\bUnityEngine\b|\bRandom\b|DateTime|TickCount"), "meals sources: the rules are game-free and take their roll from the caller");
        Check(!Regex.IsMatch(all, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|OverlapSphere\(|RaycastAll\(|Raycast\(") && Occurs(all, "new NavMeshPath()") == 1
              && code["MealsGame.cs"].Contains("_path = new NavMeshPath();   // made once"),
            "meals sources: no whole-scene search, no physics query that allocates, the walking path made once");
        string game = code["MealsGame.cs"];
        Check(game.Contains("manager.allItemControllers") && game.Contains("Seating.Bench(from, StreetReach)") && game.Contains("Physics.RaycastNonAlloc(") && game.Contains("GetCornersNonAlloc(Corners)"),
            "meals sources: seats come from the building's own furniture list or one bench query, the table top from one ray that keeps no array");
        Check(game.Contains("public static bool OwnPlace()") && MemberText(game, "public static bool OwnPlace()").Contains("GoingPublic.Mine(reg)") && MemberText(game, "public static bool OwnPlace()").Contains("reg.RentedByPlayer"),
            "meals sources: dancing is offered only in a place of the player's own, never another player's");
        Check(MemberText(game, "public static void Reserve(MealSeat seat, bool keep)").Contains("seat.Spot.occupied = keep;"), "meals sources: a table's own seat is kept for the player as the game's customers keep theirs, and given back");

        // The director: idle, one comparison a frame; every way out goes through End, which gives everything back.
        string director = code["MealsDirector.cs"];
        string onUpdate = MemberText(director, "private void OnUpdate()");
        Check(onUpdate.Contains("if (now < _nextLook)") && onUpdate.IndexOf("if (now < _nextLook)", StringComparison.Ordinal) < onUpdate.IndexOf("Meal();", StringComparison.Ordinal),
            "meals sources: idle, the frame costs one time comparison");
        foreach (string method in new[] { "private void OnUpdate()", "private void Meal()" })
        {
            string body = MemberText(director, method);
            Check(body.Length > 0 && !Regex.IsMatch(body, @"\bnew\b|""\s*\+|\+\s*""|\$""|ToString\("), "meals sources: " + method + " allocates nothing");
        }
        string end = MemberText(director, "public void End(bool eaten, string why, bool stopWalk)");
        Check(InOrder(end, "if (_step == Step.None)", "_step = Step.None;", "MealsGame.Unlisten(", "MealsGame.StopWalk(player);", "MealsGame.ShowDisplay(seat, false, ref _shown);", "_body.Release();",
                "MealsGame.Bench(seat, false, _benchBefore);", "MealsGame.Reserve(seat, false);"),
            "meals sources: End takes the listeners off, stops the walk, puts the tray away, stands the player up and gives the seat and the bench back");
        Check(MemberText(director, "private void OnSave()").Contains("End(") && MemberText(director, "private void OnDisable()").Contains("End(") && MemberText(director, "private void OnDestroy()").Contains("End(")
              && MemberText(director, "private void Slow(float now)").Contains("End(false, \"a load\""),
            "meals sources: a save, a load, the component stopping and the city unloading each end the meal");
        string meal = MemberText(director, "private void Meal()");
        Check(meal.Contains("if (!Wanted)") && meal.Contains("MealsGame.Inside != _inside") && meal.Contains("MealsGame.ScreenUp()") && meal.Contains("MealsGame.MovePressed()") && meal.Contains("Time.time > _until"),
            "meals sources: the option off, a door or a journey, a screen (a bed, a time skip), moving off and a walk that never arrives each end it");
        Check(MemberText(director, "private void Moved()").Contains("End(") && MemberText(director, "private void Blocked()").Contains("End("), "meals sources: walking elsewhere, or clicking away while seated, ends it");
        Check(MemberText(director, "private void Update()").Contains("End(false, \"a fault\", true);"), "meals sources: a fault gives everything back before the guard stops the feature");
        Check(director.Contains("Meals check (meal): ") && code["MealsDirector.Dance.cs"].Contains("Meals check (dance): ") && code["DanceButton.cs"].Contains("Meals check (dance): "),
            "meals sources: a self-check line for each meal and each dance, with Developer diagnostics on");
        string body2 = code["MealBody.cs"];
        string release = MemberText(body2, "public void Release()");
        Check(InOrder(release, "StopEating();", "StopOver();", "_hold.Detach(null);", "_props.Return(_plate);", "_seat.Stand();"), "meals sources: the body's release stops the eating, empties the hands, shelves the plate and stands the player up");
        Check(!Regex.IsMatch(body2 + game, @"appearance|SetFatness|blendShape|Hair|\.material\b|SkinnedMeshRenderer"), "meals sources: the character's body, face and hair are never touched, only moved by clips");

        // The dance: the game's own dances; it ends on every way.
        string dance = code["MealsDirector.Dance.cs"];
        string slow = MemberText(dance, "private void DanceSlow(PlayerController player)");
        Check(slow.Contains("!MealsGame.Dancing(player)") && slow.Contains("!Settings.MealsDance") && slow.Contains("MealsGame.Building(), _danceIn") && slow.Contains("!MealsGame.MusicPlaying()"),
            "meals sources: the dance ends when the game ends it (moving), the option goes off, the player leaves or the music stops");
        Check(InOrder(slow, "MealsRules.Lifts(", "_lastLift = now;", "ModState.AddModifier(ModState.MoodUp);") && Occurs(dance, "ModState.AddModifier(") == 1, "meals sources: the lift is the mod's own mood, once, and capped by the rules");
        string start = MemberText(game, "public static void StartDance(PlayerController player, int dance)");
        Check(InOrder(start, "PlayerDances.StopDancing();", "player.ResetNavigation();", "player.Character.SetDance((DanceType)dance);") && game.Contains("PlayerDances.StopDancing();"),
            "meals sources: the dances are the game's own clips, started on the player's animator and stopped by the game's own call");
        Check(!game.Contains("PlayerDances.StartDancing") && !Regex.IsMatch(game + dance, @"EnergyHelper\.(AddEnergySpender|SpentEnergyOnce)|ChangeEnergy|HappinessHelper|TemporalHappinessBoost"),
            "meals sources: dancing to the music spends no energy and lifts nothing but the mod's mood (never the game's dancing, its energy spender or its nightclub boost)");

        // Home cooking: a seat asked for before the food, the food given once whatever the answer.
        string cook = ModSource(@"Home\CookActivity.cs");
        string end2 = MemberText(cook, "protected override void End(PlayerController player, bool completed, int minutes)");
        Check(InOrder(end2, "HomeBridges.ServedAtTable != null && HomeBridges.ServedAtTable(fill)", "if (!atTable)", "Gesture(player, AnimationType.ConsumeFood);", "ChangeHunger(fill);") && Occurs(end2, "ChangeHunger(") == 1,
            "meals sources: a home-cooked dish asks for a seat first, leaves out the cooker's bite when it goes to the table, and fills once either way");
        string links = ModSource(@"Wiring\Wiring.MealsLinks.cs");
        string wiring = ModSource(@"Wiring\Wiring.cs");
        Check(links.Contains("HomeBridges.ServedAtTable = ServedAtTable;") && links.Contains("HomeBridges.ServedAtTable = null;") && wiring.Contains("MealsLinks.Connect") && wiring.Contains("MealsLinks.Disconnect"),
            "meals sources: home cooking reaches meals only through its bridge, filled in and taken off by Wiring");

        // The options, the page and the feature list.
        string settings = ModSource(@"Core\Settings.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        foreach (string id in new[] { "Seated", "Cooked", "Dance", "Takeout" })
        {
            Check(settings.Contains("Meals" + id + "Id = \"meals_" + id.ToLowerInvariant() + "\"") && settings.Contains("Meals" + id + " = Bool(modId, Meals" + id + "Id, Meals" + id + ")")
                  && settings.Contains("public static bool Meals" + id + " = true;") && tree.Contains("\"meals_" + id.ToLowerInvariant() + "\""),
                "meals sources: the option meals_" + id.ToLowerInvariant() + " is saved, read, on unless switched off and on the options page");
        }
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Meals.MealsGuide.Register();") && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("Guard.Add<Meals.MealsDirector>(_root)"),
            "meals sources: the feature is registered and its director started");
    }

    private static void MealTextChecks()
    {
        string text = MealsSource("MealsText.cs");
        var keys = new List<string>();
        foreach (Match m in Regex.Matches(text, "\"(reallife_[a-z_]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        keys.AddRange(new[] { "reallife_opt_sec_meals", "reallife_opt_tip_meals_seated", "reallife_opt_tip_meals_cooked", "reallife_opt_tip_meals_dance", "reallife_opt_tip_meals_takeout" });
        Check(keys.Count >= 20, "meals texts: its keys are found: " + keys.Count);
        string[] notices = { MealsText.CannotDance, MealsText.Lifted, MealsText.NoSeat, MealsText.NoMusic };
        string[] facts = { "{meals_reach}", "{meals_least}", "{meals_most}", "{meals_dance_minutes}", "{meals_dance_hours}", "{meals_takeout_most}" };
        string guide = MealsSource("MealsGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "meals texts: the Guide's " + fact + " is filled from the rules");
        }
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "meals texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                Check(words.TryGetValue(key, out string value) && value.Length > 0, "meals texts: " + key + " is in " + code + ".json");
            }
            foreach (string key in notices)
            {
                Check(words.TryGetValue(key, out string value) && value.Length <= 240, "meals texts: the notice " + key + " is at most 240 characters in " + code);
            }
            foreach (string key in keys)
            {
                if (words.TryGetValue(key, out string value))
                {
                    Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "meals texts: " + key + " has no exclamation mark or dash in " + code);
                }
            }
            if (words.TryGetValue(MealsText.Guide, out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "meals texts: the Guide page has " + fact + " in " + code);
                }
            }
        }
        // Dancing only lifts the mood: the Guide says it costs no energy, in each language the page is written in.
        var noEnergy = new Dictionary<string, string> { { "en", "Dancing costs no energy" }, { "pt", "Dançar não gasta energia" }, { "zh-cn", "跳舞不消耗能量" } };
        foreach (KeyValuePair<string, string> said in noEnergy)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, said.Key + ".json"));
            Check(words != null && words.TryGetValue(MealsText.Guide, out string page) && page.Contains(said.Value) && !page.Contains("spends energy") && !page.Contains("gasta energia como")
                  && !page.Contains("一样消耗能量"),
                "meals texts: the Guide page says dancing to the music costs no energy, in " + said.Key);
        }
        // What's new in all nine, in any row: the next row until a release numbers it into its own.
        var lines = new Dictionary<string, string>
        {
            { "en", "Meals and music: when you eat and a free seat is near" }, { "pt", "Refeições e música: quando você come" }, { "zh-cn", "用餐与音乐：" },
            { "de", "Essen und Musik: Wenn du isst" }, { "fr", "Repas et musique : quand vous mangez" }, { "es", "Comidas y música: cuando comes" },
            { "ru", "Еда и музыка: когда вы едите" }, { "nl", "Eten en muziek: als je eet" }, { "tr", "Yemek ve müzik: yemek yediğinizde" },
        };
        foreach (KeyValuePair<string, string> line in lines)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, line.Key + ".json"));
            Check(AnyChangesRow(words, line.Value), "meals texts: What's new says so in " + line.Key);
        }
    }
}
