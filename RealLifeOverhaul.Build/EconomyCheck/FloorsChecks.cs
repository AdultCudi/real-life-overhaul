using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Floors;

// The floors of the mod's buildings (Scripts/Floors): how many floors a building holds, which place goes on which spare floor
// and when one building is taken for places that found none, the same on every machine and whatever order they came in; the
// floors on the game's four-button panel, three to a page with "more floors" beyond four; the loan of the game's (FloorLoan)
// driven through every way in and out, each thing borrowed given back exactly once; and over the sources the university's and
// the towers' floors on the core, the swaps in its Game file alone and acted on at every answer of the loan, nothing of it
// saved, nothing per frame, and the test and its words.
internal static partial class Program
{
    private static void FloorsCore()
    {
        FloorsCapacity();
        FloorsPaging();
        FloorsAssign();
        FloorsLoanPaths();
        FloorsSources();
        FloorsKept();
        FloorsTexts();
    }

    // ---------------------------------------------------------------- how many floors a building holds

    private static void FloorsCapacity()
    {
        // The university's building (5,971 square metres, a C office of 225 a storey) and the towers (K offices of 660).
        Check(FloorsRules.Capacity(5971, 225, 4) == FloorsRules.MostFloors, "floors: the university's building holds the most a building may (26 storeys by its area, " + FloorsRules.MostFloors + " at most)");
        Check(FloorsRules.Capacity(2517, 660, 4) == 4 && FloorsRules.Capacity(9939, 660, 4) == 12 && FloorsRules.Capacity(48567, 660, 4) == FloorsRules.MostFloors,
            "floors: a tower holds its area's storeys, never fewer than its own four companies (2,517 sqm: 4; 9,939: 12; 48,567: 12)");
        Check(FloorsRules.Capacity(0, 225, 0) == 1 && FloorsRules.Capacity(100, 0, 0) == 1 && FloorsRules.Capacity(-5, -5, 0) == 1,
            "floors: a building with no area, or a size the game does not say, holds one floor");
        Check(FloorsRules.Capacity(1000, 225, 12) == 12 && FloorsRules.Capacity(99999, 75, 14) == 14,
            "floors: a holder's own floors always fit, above the most a building is given otherwise (all twelve companies in one tower)");
        Check(FloorsRules.DoorFloor(4, 3) == 3 && FloorsRules.DoorFloor(4, 0) == 1 && FloorsRules.DoorFloor(4, 9) == 1 && FloorsRules.DoorFloor(0, 2) == 0,
            "floors: the game's own door takes the player to the floor wanted while there is one, else the first");
        Check(FloorsRules.Own(null, 0, "ba:buildingsize_c", 2) && FloorsRules.Own("ba:buildingsize_c", 2, "ba:buildingsize_c", 2)
              && !FloorsRules.Own("ba:buildingsize_d", 1, "ba:buildingsize_c", 2) && !FloorsRules.Own("ba:buildingsize_c", 1, "ba:buildingsize_c", 2),
            "floors: a floor asking no structure, or the building's own size and version, stands on the building's own; any other is borrowed");
        Check(FloorsRules.Key("ba:street_secondavenue", 26) == "ba:street_secondavenue#26" && FloorsRules.Key(null, 3) == "#3",
            "floors: a building's key is its street and number, as every machine names it");
    }

    // ---------------------------------------------------------------- the game's panel: four buttons, pages beyond

    private static void FloorsPaging()
    {
        int[] slots = new int[FloorsRules.Slots];
        FloorsRules.Page(4, 0, 0, slots);
        bool door = slots[0] == 1 && slots[1] == 2 && slots[2] == 3 && slots[3] == 4;
        FloorsRules.Page(4, 2, 0, slots);
        bool lift = slots[0] == 1 && slots[1] == 3 && slots[2] == 4 && slots[3] == 0;
        Check(door && lift && FloorsRules.Pages(4, 0, 4) == 1 && FloorsRules.Pages(4, 2, 4) == 1,
            "floors: four floors fit the door's four buttons, the lift on floor 2 offers the three others, one page each (the university and a tower as they were)");

        // Five floors at a door (the university with a place on its fifth): three a page and "more floors".
        FloorsRules.Page(5, 0, 0, slots);
        bool first = slots[0] == 1 && slots[1] == 2 && slots[2] == 3 && slots[3] == -1;
        FloorsRules.Page(5, 0, 1, slots);
        bool second = slots[0] == 4 && slots[1] == 5 && slots[2] == 0 && slots[3] == -1;
        Check(first && second && FloorsRules.Pages(5, 0, 4) == 2 && FloorsRules.NextPage(0, 5, 0, 4) == 1 && FloorsRules.NextPage(1, 5, 0, 4) == 0,
            "floors: five floors at a door are two pages, 1 2 3 and 4 5, \"more floors\" on the fourth button turning to the next and back to the first");
        FloorsRules.Page(6, 2, 0, slots);
        bool liftFirst = slots[0] == 1 && slots[1] == 3 && slots[2] == 4 && slots[3] == -1;
        FloorsRules.Page(6, 2, 1, slots);
        bool liftSecond = slots[0] == 5 && slots[1] == 6 && slots[2] == 0 && slots[3] == -1;
        Check(liftFirst && liftSecond && FloorsRules.Pages(6, 2, 4) == 2, "floors: the lift on floor 2 of six offers 1 3 4, then 5 6, never its own floor");

        bool every = true, never = true, wraps = true;
        for (int floors = 1; floors <= FloorsRules.MostFloors; floors++)
        {
            for (int skip = 0; skip <= floors; skip++)
            {
                var seen = new HashSet<int>();
                int pages = FloorsRules.Pages(floors, skip, 4);
                int page = 0;
                for (int p = 0; p < pages; p++)
                {
                    FloorsRules.Page(floors, skip, page, slots);
                    for (int s = 0; s < slots.Length; s++)
                    {
                        if (slots[s] > 0)
                        {
                            never &= slots[s] != skip && slots[s] <= floors;
                            seen.Add(slots[s]);
                        }
                    }
                    page = FloorsRules.NextPage(page, floors, skip, 4);
                }
                every &= seen.Count == floors - (skip >= 1 ? 1 : 0);
                wraps &= page == 0;
            }
        }
        Check(every && never && wraps, "floors: from one floor to twelve, at the door and from every floor's lift, every other floor is on a page, none twice a page, the page after the last is the first");
        FloorsRules.Page(0, 0, 0, slots);
        Check(slots[0] == 0 && slots[3] == 0 && FloorsRules.Pages(0, 0, 4) == 1 && FloorsRules.NextPage(3, 0, 0, 4) == 0, "floors: a building with no floors offers no button");
    }

    // ---------------------------------------------------------------- spare floors, the same on every machine

    private static void FloorsAssign()
    {
        // The city as it is: three towers of four companies (towers first), the university's four floors.
        var hosts = new List<FloorHostRow>
        {
            new FloorHostRow { Key = "ba:street_tenthstreet#12", Rank = FloorsRules.RankTowers, Used = 4, Capacity = FloorsRules.Capacity(2517, 660, 4) },
            new FloorHostRow { Key = "ba:street_sixthstreet#6", Rank = FloorsRules.RankTowers, Used = 4, Capacity = FloorsRules.Capacity(9939, 660, 4) },
            new FloorHostRow { Key = "ba:street_secondavenue#26", Rank = FloorsRules.RankCampus, Used = 4, Capacity = FloorsRules.Capacity(5971, 225, 4) },
        };
        var guests = new List<FloorGuestRow> { new FloorGuestRow { Id = "cityhall", Floors = 2 }, new FloorGuestRow { Id = "jobcentre", Floors = 1 }, new FloorGuestRow { Id = "licensing", Floors = 1 } };
        int[] hostOf = new int[8], first = new int[8];
        int placed = FloorsRules.Assign(hosts, guests, hostOf, first);
        Check(placed == 3 && hostOf[0] == 1 && first[0] == 5 && hostOf[1] == 1 && first[1] == 7 && hostOf[2] == 1 && first[2] == 8,
            "floors: new places take the spare floors of a tower first, above its four companies, in the places' order by id (City Hall 5 and 6, the job centre 7, licensing 8)");
        Check(FloorsRules.ClaimsFor(guests, hostOf) == 0, "floors: with room in a building the mod holds, no building of the city's is taken");

        // The towers full: the university's building next.
        hosts[1] = new FloorHostRow { Key = hosts[1].Key, Rank = FloorsRules.RankTowers, Used = 4, Capacity = 4 };
        FloorsRules.Assign(hosts, guests, hostOf, first);
        Check(hostOf[0] == 2 && first[0] == 5 && hostOf[1] == 2 && first[1] == 7 && hostOf[2] == 2 && first[2] == 8,
            "floors: the towers full, the university's building has room above its four floors of classrooms");

        // Determinism: every order of the buildings and the places gives the same floors.
        var rnd = new Random(20261023);
        bool same = true;
        for (int round = 0; round < 300; round++)
        {
            var h = new List<FloorHostRow>(hosts);
            var g = new List<FloorGuestRow>(guests);
            Shuffle(h, rnd);
            Shuffle(g, rnd);
            int[] ho = new int[8], fo = new int[8];
            FloorsRules.Assign(h, g, ho, fo);
            for (int i = 0; i < g.Count; i++)
            {
                int was = guests.FindIndex(x => x.Id == g[i].Id);
                same &= ho[i] >= 0 && h[ho[i]].Key == hosts[hostOf[was]].Key && fo[i] == first[was];
            }
        }
        Check(same, "floors: in 300 orders of the buildings and the places every place gets the same building and floor (every machine of a Going Public session alike)");

        // Release: a place let go frees its floors; the places after it by id move down; a holder's own floors never move.
        var two = new List<FloorGuestRow> { guests[1], guests[2] };
        FloorsRules.Assign(hosts, two, hostOf, first);
        Check(hostOf[0] == 2 && first[0] == 5 && hostOf[1] == 2 && first[1] == 6, "floors: City Hall let go, the job centre and licensing take floors 5 and 6 of the same building");
        bool ownKept = true;
        for (int i = 0; i < hosts.Count; i++)
        {
            ownKept &= first[0] > hosts[i].Used || hostOf[0] != i;
        }
        Check(ownKept, "floors: a place is never given a holder's own floor");

        // No room: one building for all of them, never one a place; a place larger than a building never gets one.
        var full = new List<FloorHostRow> { new FloorHostRow { Key = "a#1", Rank = 0, Used = 4, Capacity = 4 }, new FloorHostRow { Key = "b#2", Rank = 1, Used = 4, Capacity = 4 } };
        var many = new List<FloorGuestRow>();
        for (int i = 0; i < 7; i++)
        {
            many.Add(new FloorGuestRow { Id = "place" + i, Floors = 1 });
        }
        int none = FloorsRules.Assign(full, many, hostOf, first);
        Check(none == 0 && FloorsRules.ClaimsFor(many, hostOf) == 1, "floors: seven places with no spare floor anywhere take one building between them, not seven");
        var huge = new List<FloorGuestRow> { new FloorGuestRow { Id = "huge", Floors = FloorsRules.MostFloors + 1 } };
        FloorsRules.Assign(full, huge, hostOf, first);
        Check(hostOf[0] == -1 && FloorsRules.ClaimsFor(huge, hostOf) == 0, "floors: a place taller than a building is given none, and no building is taken for it");
        var crowd = new List<FloorGuestRow>();
        for (int i = 0; i < 60; i++)
        {
            crowd.Add(new FloorGuestRow { Id = "p" + i.ToString("00"), Floors = 1 });
        }
        int[] bigOf = new int[64], bigFirst = new int[64];
        FloorsRules.Assign(full, crowd, bigOf, bigFirst);
        Check(FloorsRules.ClaimsFor(crowd, bigOf) == FloorsRules.MostClaims, "floors: never more than " + FloorsRules.MostClaims + " buildings taken for places, however many ask");
        full.Add(new FloorHostRow { Key = "c#3", Rank = FloorsRules.RankClaimed, Used = 0, Capacity = FloorsRules.MostFloors });
        int fit = FloorsRules.Assign(full, many, hostOf, first);
        bool together = fit == 7;
        for (int i = 0; i < many.Count; i++)
        {
            together &= hostOf[i] == 2 && first[i] == i + 1;
        }
        Check(together && !FloorsRules.ClaimIdle(2, hostOf, many.Count) && FloorsRules.ClaimIdle(0, hostOf, many.Count),
            "floors: the building taken holds the seven places on floors 1 to 7 by id; it is in use, the full ones hold none of them");
        Check(FloorsRules.Assign(new List<FloorHostRow>(), many, hostOf, first) == 0 && FloorsRules.Assign(full, new List<FloorGuestRow>(), hostOf, first) == 0,
            "floors: with no building or no place, nothing is given");
    }

    private static void Shuffle<T>(List<T> list, Random rnd)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            T t = list[i];
            list[i] = list[j];
            list[j] = t;
        }
    }

    // ---------------------------------------------------------------- the loan, through every way in and out

    // Counts what the loan answered along a path: each borrow given back once, each dirt list put back once, each visit ended.
    private sealed class LoanTally
    {
        public int Borrows, GiveBacks, DirtBack, Arrives, Leaves, Shadows, ShadowsBack, TakeOuts, Refused;

        public FloorDo Add(FloorDo d)
        {
            Borrows += (d & FloorDo.Borrow) != 0 ? 1 : 0;
            GiveBacks += (d & FloorDo.GiveBack) != 0 ? 1 : 0;
            DirtBack += (d & FloorDo.PutBackDirt) != 0 ? 1 : 0;
            Arrives += (d & FloorDo.Arrive) != 0 ? 1 : 0;
            Leaves += (d & FloorDo.Leave) != 0 ? 1 : 0;
            Shadows += (d & FloorDo.Shadow) != 0 ? 1 : 0;
            ShadowsBack += (d & FloorDo.PutBackRegistration) != 0 ? 1 : 0;
            TakeOuts += (d & FloorDo.TakeOut) != 0 ? 1 : 0;
            Refused += (d & FloorDo.Refused) != 0 ? 1 : 0;
            return d;
        }
    }

    private const string Uni = "ba:street_secondavenue#26", Next = "ba:street_secondavenue#24", Hospital = "ba:street_firstavenue#2";

    private static void FloorsLoanPaths()
    {
        // Each way in and out as the game's events come (BuildingManager.EnterBuildingCoroutine: the exit first from a building
        // the player is in, the load, onEnterBuilding; ExitFromBuildingCoroutine: onExitBuilding), with the loan's answers counted.
        LoanPath("going in by a floor's button at the door, and out by the game's door", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 2, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            Check(l.OnFloor && l.VisitFloor == 2 && l.VisitBorrowed && l.Shadowed && !l.Borrowed, "floors path: in by the door: on floor 2, the size back the moment the game loaded it, the place's registration on");
            t.Add(l.Exited(Uni));
        });
        LoanPath("the game's own door seen by the watch", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.Exited(Uni));
        });
        LoanPath("the game's own door into a tower (the building's own structure, no watch)", 1, 0, 0, (l, t) =>
        {
            t.Add(l.Entered("tower#1", false));
            t.Add(l.Found("tower#1", 3, false));
            Check(l.OnFloor && l.VisitFloor == 3 && !l.VisitBorrowed, "floors path: a tower's own door: on the employer's floor at once, nothing borrowed");
            t.Add(l.Exited("tower#1"));
        });
        LoanPath("the watch saw the game going in, but into the building next door", 0, 0, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            t.Add(l.Entered(Next, false));
            Check(!l.OnFloor && !l.Pending, "floors path: another building entered: no floor, nothing asked any more");
        });
        LoanPath("the game refused to go in, or failed to", 0, 0, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            t.Add(l.EnteringOver());
        });
        LoanPath("the game took longer than 15 s to go in", 0, 0, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            FloorDo early = t.Add(l.Timeout(FloorLoan.MostSeconds - 1f));
            Check(early == FloorDo.None && l.Borrowed, "floors path: under 15 s the borrowed structure is waited for");
            t.Add(l.Timeout(FloorLoan.MostSeconds));
            t.Add(l.Entered(Uni, true));
            Check(!l.OnFloor, "floors path: given up on, the late going in brings no floor (the player is found inside, and taken to one again)");
        });
        LoanPath("the lift between floors on the structure on show", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.Moved(3));
            t.Add(l.Moved(4));
            Check(l.VisitFloor == 4 && !l.Borrowed && !l.Pending, "floors path: the lift on the same structure: floor 4, nothing borrowed or asked");
            t.Add(l.Exited(Uni));
        });
        LoanPath("the lift to a floor on another structure (through the game's door)", 2, 2, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 4, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.Ask(Uni, 5, true, true, 1f));
            Check(l.Reentering && l.Borrowed, "floors path: to floor 5 on another structure: the building read as it for the game's going in again");
            FloorDo exitFirst = t.Add(l.Exited(Uni));
            Check((exitFirst & FloorDo.Leave) != 0 && l.Pending && l.Borrowed, "floors path: the game's exit first ends floor 4, and floor 5 stays asked with its structure borrowed");
            t.Add(l.Entered(Uni, true));
            Check(l.OnFloor && l.VisitFloor == 5 && !l.Borrowed, "floors path: on floor 5, the building's own size back again");
            t.Add(l.Exited(Uni));
        });
        LoanPath("the lift from another structure to the building's own", 2, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 4, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.Ask(Uni, 6, false, true, 1f));
            t.Add(l.Exited(Uni));
            t.Add(l.Entered(Uni, false));
            Check(l.OnFloor && l.VisitFloor == 6 && !l.VisitBorrowed && !l.Shadowed, "floors path: on a floor of the building's own structure: its own registration");
            t.Add(l.Exited(Uni));
        });
        LoanPath("saving on a floor", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 2, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            Check(!l.Borrowed, "floors path: at a save on a floor no building reads as another structure (what the game writes holds nothing borrowed)");
            t.Add(l.Exited(Uni));
        });
        LoanPath("loading a save made on a floor of another structure", 1, 1, 0, (l, t) =>
        {
            FloorDo load = t.Add(l.Entered(Uni, false));   // the game's own load puts the player in the building's own structure
            Check(load == FloorDo.None && !l.OnFloor, "floors path: the load itself brings no floor");
            t.Add(l.Ask(Uni, 1, true, false, 0f));   // found inside on no floor: the way back, through the game's door
            FloorDo exitFirst = t.Add(l.Exited(Uni));
            Check(exitFirst == FloorDo.None && l.Pending, "floors path: the game's exit first, on no floor, ends nothing and keeps the floor asked");
            t.Add(l.Entered(Uni, true));
            t.Add(l.Exited(Uni));
        });
        LoanPath("loading a save made on a floor of a tower (the building's own structure)", 1, 0, 0, (l, t) =>
        {
            t.Add(l.Found("tower#1", 1, false));
            t.Add(l.Exited("tower#1"));
        });
        LoanPath("sleep, fast travel or a taxi from a floor (the game takes the player out)", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 3, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.Exited(Uni));
        });
        LoanPath("fainting on a floor: to hospital", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 3, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.Exited(Uni));
            t.Add(l.Entered(Hospital, false));
            Check(!l.OnFloor, "floors path: in hospital, on no floor");
        });
        LoanPath("the option switched off on a floor of another structure", 1, 1, 1, (l, t) =>
        {
            t.Add(l.Ask(Uni, 2, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            FloorDo gone = t.Add(l.Gone(Uni));
            Check((gone & (FloorDo.Leave | FloorDo.PutBackRegistration | FloorDo.TakeOut)) == (FloorDo.Leave | FloorDo.PutBackRegistration | FloorDo.TakeOut),
                "floors path: switched off on a floor of another structure: the floor ends, the building's registration is back, the player is taken out");
            FloorDo out1 = t.Add(l.Exited(Uni));
            Check(out1 == FloorDo.None, "floors path: the walk out ends nothing more");
        });
        LoanPath("the option switched off while the game goes in", 0, 0, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 2, true, false, 0f));
            t.Add(l.Gone(Uni));
            t.Add(l.Entered(Uni, false));
            Check(!l.OnFloor, "floors path: switched off going in: the game loads the building's own structure and no floor comes of it");
        });
        LoanPath("the option switched off on a floor of the building's own structure", 1, 0, 0, (l, t) =>
        {
            t.Add(l.Found("tower#1", 2, false));
            FloorDo gone = t.Add(l.Gone("tower#1"));
            Check((gone & FloorDo.Leave) != 0 && (gone & FloorDo.TakeOut) == 0 && (gone & FloorDo.PutBackRegistration) == 0,
                "floors path: switched off on a tower's floor: the floor ends and the player stays in the building, as without the towers");
        });
        LoanPath("the scene unloading while the game goes in", 0, 0, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            t.Add(l.CityGone());
        });
        LoanPath("the scene unloading on a floor", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            t.Add(l.Entered(Uni, true));
            t.Add(l.CityGone());
        });
        LoanPath("a second going in asked while one is under way", 1, 1, 0, (l, t) =>
        {
            t.Add(l.Ask(Uni, 1, true, false, 0f));
            FloorDo again = t.Add(l.Ask(Uni, 2, true, false, 0f));
            Check(again == FloorDo.Refused && l.PendingFloor == 1, "floors path: a second going in is refused while one is under way, the first kept");
            t.Add(l.Entered(Uni, true));
            t.Add(l.Exited(Uni));
        });

        var loan = new FloorLoan();
        Check(loan.Ask(Uni, 2, true, true, 0f) == FloorDo.Refused && !loan.Borrowed, "floors: a way through the game's door from a floor is refused on no floor, nothing borrowed");
        Check(loan.Moved(3) == FloorDo.Refused && loan.Found(Uni, 0, false) == FloorDo.Refused && loan.Ask(null, 1, true, false, 0f) == FloorDo.Refused,
            "floors: a lift on no floor, a floor 0 and a building with no key are refused");
    }

    // One path: fresh loan, the steps, then each borrow given back once, each dirt list put back once, the visits ended, the
    // registrations of the places' own ended (by the game's exit or put back), and nothing held at the end.
    private static void LoanPath(string what, int arrives, int shadows, int takeOuts, Action<FloorLoan, LoanTally> steps)
    {
        var loan = new FloorLoan();
        var tally = new LoanTally();
        steps(loan, tally);
        Check(tally.Borrows == tally.GiveBacks && tally.DirtBack == tally.Borrows, "floors path (" + what + "): every borrowed structure given back once, with its dirt spots ("
                                                                                + tally.Borrows + " borrowed, " + tally.GiveBacks + " given back, " + tally.DirtBack + " dirt lists back)");
        Check(tally.Arrives == arrives && tally.Leaves == arrives, "floors path (" + what + "): " + arrives + " floor(s) arrived on and as many left (" + tally.Arrives + " and " + tally.Leaves + ")");
        Check(tally.Shadows == shadows && tally.ShadowsBack <= tally.Shadows, "floors path (" + what + "): " + shadows + " registration(s) of the place's own, each ended (" + tally.Shadows + ")");
        Check(tally.TakeOuts == takeOuts, "floors path (" + what + "): the player taken out " + takeOuts + " time(s) (" + tally.TakeOuts + ")");
        Check(!loan.Borrowed && !loan.Pending && !loan.OnFloor && !loan.Shadowed, "floors path (" + what + "): nothing borrowed, asked or held at the end");
    }

    // ---------------------------------------------------------------- the sources

    private static void FloorsSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string game = Read(@"Floors\FloorsGame.cs") + Read(@"Floors\FloorsGame.Layout.cs");
        string director = Read(@"Floors\FloorsDirector.cs") + Read(@"Floors\FloorsDirector.Book.cs") + Read(@"Floors\FloorsDirector.Entering.cs")
                          + Read(@"Floors\FloorsDirector.Lift.cs") + Read(@"Floors\FloorsDirector.Panels.cs") + Read(@"Floors\FloorsDirector.Tests.cs");
        string campus = Read(@"Careers\CampusInterior.cs") + Read(@"Careers\CampusInterior.Entering.cs") + Read(@"Careers\CampusInterior.Floor.cs") + Read(@"Careers\CampusInterior.Panels.cs");
        string towers = Read(@"Tech\Sites\TowerFloors.cs"), sites = Read(@"Tech\Sites\TechSites.cs") + Read(@"Tech\Sites\TechSites.Places.cs"), hq = Read(@"Tech\Sites\Headquarters.cs");
        Check(game.Length > 0 && director.Length > 0 && campus.Length > 0 && towers.Length > 0 && sites.Length > 0 && hq.Length > 0,
            "floors sources: the core, the university's floors and the towers' are read (" + scripts + ")");

        // The university's floors and the towers' are on the core.
        Check(campus.Contains("FloorsDirector.Hold(_host)") && campus.Contains("FloorsDirector.Let(_host,") && campus.Contains("FloorsDirector.Enter(_host,")
              && campus.Contains("FloorsDirector.Lift(floor,") && campus.Contains("class CampusHost : FloorHost") && campus.Contains("class CampusFloors : FloorPlace")
              && campus.Contains("Structure = new FloorStructure(SizeD, SchoolVersion)") && campus.Contains("Layout = new FloorLayout(CareersGame.SchoolType, SchoolLayout)"),
            "floors sources: the university hands its building to the core with its floors on the school's D1 and its layout, goes in and takes the lift through it");
        Check(towers.Contains("FloorsDirector.Hold(host)") && towers.Contains("FloorsDirector.Let(") && towers.Contains("class TowerHost : FloorHost")
              && towers.Contains("class CompanyFloor : FloorPlace") && towers.Contains("HqTowers.DoorFloor(Tower.Floors.Count, EmployerFloor(Tower))"),
            "floors sources: each tower is a building held by the core, a company a floor, the game's own door to the employer's floor");
        Check(sites.Contains("internal void FloorArrived()") && sites.Contains("internal void FloorLeft()") && sites.Contains("internal bool FloorReady()")
              && !sites.Contains("IEnumerator Lift(") && !sites.Contains("UiFader"),
            "floors sources: the towers' lift is the core's; TechSites lays a company's floor on its hooks");

        // The swaps are the core's Game file's alone: no other source of the university's, the towers' or the core's writes them.
        string others = campus + towers + sites + hq + director;
        Check(Regex.IsMatch(game, @"asset\.BuildingSize = size;") && Regex.IsMatch(game, @"asset\.BuildingVersion = version;")
              && !Regex.IsMatch(others, @"\.BuildingSize\s*=[^=]") && !Regex.IsMatch(others, @"\.BuildingVersion\s*=[^=]"),
            "floors sources: a building's size and version are written in Floors/FloorsGame.ReadAs alone");
        Check(Regex.IsMatch(game, @"m\.buildingRegistration = value;") && !Regex.IsMatch(others, @"\.buildingRegistration\s*=[^=]"),
            "floors sources: the manager's registration is swapped in Floors/FloorsGame alone");
        Check(Regex.IsMatch(game, @"\.alwaysOpen = ") && !Regex.IsMatch(others, @"\.alwaysOpen\s*=[^=]") && !others.Contains("EnterBuilding(") && !others.Contains("navmeshAgent.Warp(")
              && !Regex.IsMatch(game + director, @"\.BuildingType\s*=[^=]"),
            "floors sources: the doors' flags, the game's going in and the warp are Floors/FloorsGame's alone; nothing of the floors changes a building's type");

        // Every answer of the loan that can give back or end a visit is acted on.
        int calls = 0, acted = 0;
        var lacking = new List<string>();
        foreach (Match m in Regex.Matches(director, @"Loan\.(Entered|EnteringOver|Timeout|Gone|CityGone|Exited|Found)\("))
        {
            calls++;
            int line = director.LastIndexOf('\n', m.Index) + 1;
            int end = director.IndexOf('\n', m.Index);
            string statement = director.Substring(line, (end < 0 ? director.Length : end) - line);
            string after = director.Substring(m.Index, Math.Min(900, director.Length - m.Index));
            bool done = statement.Contains("Act(Loan.") || statement.Contains("& FloorDo.GiveBack") || (statement.Contains("FloorDo d =") && after.Contains("(d & FloorDo."))
                        || (statement.Contains("FloorDo d = Loan.CityGone()") && after.Contains("FloorDo.GiveBack"));
            acted += done ? 1 : 0;
            if (!done)
            {
                lacking.Add(statement.Trim());
            }
        }
        Check(calls >= 9 && acted == calls, "floors sources: every answer of the loan is acted on (" + acted + " of " + calls + (lacking.Count > 0 ? "; not: " + string.Join(" | ", lacking) : string.Empty) + ")");
        string give = MethodText(director, "private static void GiveBackNow(string why)");
        Check(give.Contains("FloorsGame.ReadAs(_asset, _ownSize, _ownVersion)") && give.Contains("FloorsGame.PutDirt(_dirtOf, _keptDirt)") && give.Contains("the building's own size put back ("),
            "floors sources: giving back puts the building's own size, version and dirt spots back, with its self-check line");
        string enter = MethodText(director, "private string EnterNow(FloorHost host, int floor, string how, bool reenter)");
        string seen = MethodText(director, "private void GameWayIn(FloorHost host)");
        Check(ComesBefore(enter, "Borrow(host, place,", "StartCoroutine(Guard.Routine(Settle()))") && ComesBefore(seen, "Borrow(host, place,", "StartCoroutine(Guard.Routine(Settle()))")
              && enter.Contains("GiveBackNow(\"the game's going in failed\")"),
            "floors sources: every borrow is followed by the settle's look (the going in over, or 15 s), and a failed call gives back at once");
        Check(director.Contains("EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen)") && director.Contains("EventHold.Keep(ref GlobalEvents.onExitBuilding, _onExit, ref _exitSeen)")
              && director.Contains("Everything(\"the city went\", true)") && director.Contains("Everything(\"the floors stood down\", false)") && director.Contains("SaveHook.Keep(_onSave)"),
            "floors sources: the game's enter and exit events are kept on, the city going and a stand-down give everything back, the save is looked at");
        string act = MethodText(director, "private static void Act(FloorDo d, string why)");
        Check(act.Contains("bool going = FloorsGame.CityGoing;") && act.Contains("(d & FloorDo.TakeOut) != 0 && v != null && !going") && act.Contains("FloorsGame.ManagerRegistration = FloorsGame.RegistrationOf(v.Host.Building)"),
            "floors sources: a release puts the building's registration back and takes the player out of a borrowed structure, but never while the city goes");

        // Nothing of it saved, nothing a frame.
        string floors = game + director + Read(@"Floors\FloorsRules.cs") + Read(@"Floors\FloorLoan.cs") + Read(@"Floors\FloorPlace.cs") + Read(@"Floors\FloorHost.cs") + Read(@"Floors\FloorVisit.cs");
        Check(!floors.Contains("modData") && !floors.Contains("BuildingRegistrations.Add") && !floors.Contains("SaveLines") && !floors.Contains("Store."),
            "floors sources: the floors write nothing into the save: no line of the mod's, no registration in the save's list");
        Check(!Regex.IsMatch(floors, @"void (Update|LateUpdate|FixedUpdate)\(") && Occurrences(director, "yield return null;") >= 1
              && MethodText(director, "private static IEnumerator WatchClick()").Contains("while (_watching)"),
            "floors sources: no Update; the one loop a frame is the watch at a door, while it is wanted");
        Check(!Regex.IsMatch(director, @"InstanceBehavior<|PlayerHelper\.|BuildingHelper\.|UiFader|BuildingManager\.IsInsideBuilding|PlacementSystem\."),
            "floors sources: the director asks the game nothing itself; Floors/FloorsGame does");

        // The kept self-check lines of the university, and the test's line.
        Check(campus.Contains("Check = \"Careers check (university way in)\"") && campus.Contains("LayCheck = \"Careers check (university floor)\"")
              && director.Contains(".Check + \": \" + why + \": the building read as \"") && director.Contains(".LayCheck + \": frame \"")
              && campus.Contains("\"Careers check (university door)\", \"Careers check (university lift)\"") && towers.Contains("\"Tech sites check (tower door)\", \"Tech sites check (tower lift)\""),
            "floors sources: the university's and the towers' self-check lines are written as before, by the core with their names");
        Check(director.Contains("\"Floors check: floor \" + v.Floor + \" of \" + v.Address + \" on structure \" + v.Structure + \", the building's own size and version put back on the way out")
              && director.Contains("FloorsGame.OtherStructure(own, ownVersion, SchoolSize, SchoolVersion)"),
            "floors sources: the way out of a floor on another structure says its check line; the test's structure is neither D1 nor the building's own");
        string list = Read(@"Meta\FeatureList.cs");
        Check(list.Contains(".Test(\"reallife_floors_test_place\", Floors.FloorsDirector.TestPlace)"), "floors sources: Options > Tests has the floors' test");
        string root = Read(@"Core\RealLifeOverhaulMod.cs");
        Check(root.Contains("Guard.Add<Floors.FloorsDirector>(_root)") && root.Contains("Floors.FloorsDirector.Logger = context.Logger"), "floors sources: the floors' director is made with the city");
    }

    // ---------------------------------------------------------------- what the university's and the towers' floors kept

    // Over the sources: each behaviour the university's and the towers' floors had before the core, as their hooks give it now.
    private static void FloorsKept()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string campus = Read(@"Careers\CampusInterior.cs") + Read(@"Careers\CampusInterior.Entering.cs") + Read(@"Careers\CampusInterior.Floor.cs");
        string towers = Read(@"Tech\Sites\TowerFloors.cs"), sites = Read(@"Tech\Sites\TechSites.cs");
        string panels = Read(@"Floors\FloorsDirector.Panels.cs"), lift = Read(@"Floors\FloorsDirector.Lift.cs"), main = Read(@"Floors\FloorsDirector.cs");

        // The university: its panel at the door while closed with its floors greyed, the class on now in blue, the lift hidden while
        // a class is sat, floor 1 after a load, its registration of its own with the school's type, layout and hours.
        Check(campus.Contains("public override bool ShowsDoor(bool open) => true;") && Occurrences(campus, "public override bool Open() => _u.OpenNow();") == 2
              && panels.Contains("panel.Button(slot, key, args, press[slot], !door || place.Open(), marked || slot == 0);"),
            "floors kept: the university's door panel stands while it is closed, its floors greyed at the door (never at the lift), the first or the marked floor in blue");
        string button = MethodText(campus, "public override void Button(int floor, int part, out string key, out object args, out bool marked)");
        Check(button.Contains("UniversityBusinessNow(out int classCourse)") && button.Contains("\"reallife_campus_floor_class\" : \"reallife_campus_floor\"") && button.Contains("marked = here;"),
            "floors kept: the floor of the class, exam or ceremony on now says its course and is marked, the others say their floor");
        Check(campus.Contains("public override bool Busy() => _u._director.SittingNow;") && campus.Contains("public override int DefaultFloor(bool afterLoad) => afterLoad ? 1 : _u.DoorFloor();")
              && campus.Contains("CareersText.T(\"reallife_campus_lift_here\", \"floor\", floor.ToString())"),
            "floors kept: the university's lift is not offered while a class is sat and says the floor the player is on; found inside after a load, floor 1; by the game's door, the class's floor");
        string shadow = MethodText(campus, "private static BuildingRegistration Shadow(BuildingRegistration real, CampusBuilding campus)");
        Check(campus.Contains("return campus != null ? Shadow(real, campus) : null;") && shadow.Contains("businessTypeName = CareersGame.SchoolType") && shadow.Contains("Layout = SchoolLayout")
              && shadow.Contains("AvailableForRent = false") && shadow.Contains("RentedByPlayer = false") && shadow.Contains("CampusRules.OpenDay(d)") && shadow.Contains("r.dirtSpots = real.dirtSpots;"),
            "floors kept: on a floor the manager's registration is the university's own: the school's type and layout, its hours, nobody's to rent, the structure's dirt spots");
        string laid = MethodText(campus, "private void Laid(FloorVisit v, FloorLaid laid)");
        Check(ComesBefore(laid, "Rooms(Manager);", "Label();") && laid.Contains("\"reallife_campus_note_floor\"") && laid.Contains("ToRoom(v, course, begin)")
              && MethodText(campus, "private IEnumerator ToRoom(FloorVisit v, int course, bool begin)").Contains("yield return null;")
              && campus.Contains("LayLast = SchoolDoor;") && campus.Contains("StructureWords = \"the school's structure\";"),
            "floors kept: the school's layout laid with its classroom doors last, then the rooms found and named, the floor's courses said, and a frame later on to the classroom asked for");
        Check(MethodText(campus, "private void Shown(FloorVisit v)").Contains("GoToRoom(course, begin)") && MethodText(campus, "private void Moved(FloorVisit v, int fromFloor)").Contains("Label();")
              && MethodText(campus, "private void OnFloor(FloorVisit v)").Contains("manager.isOpen = open;"),
            "floors kept: the lift gives the rooms the floor's courses under the dark screen and goes on to the classroom once light; the classrooms open and shut with the university");

        // The towers: the panel only while a tower stands empty, the employer's floor marked and the game's door to it, the
        // player turned as they came in, ten frames between two companies' furniture, the lobby laid again under the dark.
        Check(towers.Contains("public override bool ShowsDoor(bool open) => open && Tower.Floors.Count > 0;") && towers.Contains("public override bool Open() => Settings.TechSiteHq && Headquarters.Open(Tower);")
              && towers.Contains("mine ? \"reallife_hq_floor_yours\" : \"reallife_hq_floor\"") && towers.Contains("marked = mine;"),
            "floors kept: a tower's panel stands only while it stands empty, a floor a company, the player's employer's marked");
        Check(towers.Contains("FacesIn = true;") && towers.Contains("ClearFrames = LiftClearFrames;") && towers.Contains("private const int LiftClearFrames = 10;")
              && towers.Contains("_floors._sites.FloorArrived();") && towers.Contains("public override void Leave(FloorVisit visit) => _floors._sites.FloorLeft();")
              && towers.Contains("public override bool Ready(FloorVisit visit) => _floors._sites.FloorReady();"),
            "floors kept: a tower's lift turns the player as they came in, waits ten frames for one company's furniture to go, and stays dark until the next company's floor stands");
        string arrived = MethodText(sites, "internal void FloorArrived()");
        Check(arrived.Contains("Floors.FloorsDirector.Lifting") && ComesBefore(arrived, "_laying = true;", "Inside(Save.Day);") && arrived.Contains("_laying = false;")
              && sites.Contains("if (key == null || (Floors.FloorsDirector.Lifting && !_laying))"),
            "floors kept: by the lift a company's lobby and floor are laid at once behind the dark screen; by the door, by the look just after it");

        // The core's panels, reaches and lift are the old ones.
        Check(main.Contains("private const float DoorReach = 3.2f;") && main.Contains("private const float LiftReach = 2.6f;") && main.Contains("private const float WatchReach = 12f;")
              && main.Contains("private const float LiftFade = 0.25f;") && main.Contains("private const float LiftWaitMost = 6f;")
              && panels.Contains("host.DoorPanel.Show(host.Door, at, 2.2f,") && panels.Contains("host.LiftPanel.Show(v.Spawn, at, 2.1f,"),
            "floors kept: the panels show within the university's and the towers' reaches (3.2 m at a door, 2.6 m at the lift), as high, the lift a quarter-second fade each way, six seconds at most dark");
        string liftSame = MethodText(lift, "private IEnumerator LiftSame(FloorVisit v, int floor)");
        Check(ComesBefore(liftSame, "from.Leave(v);", "FloorsGame.Warp(v.Spawn, v.Turn, to.FacesIn);") && ComesBefore(liftSame, "FloorsGame.Warp(", "from.ClearFrames")
              && ComesBefore(liftSame, "from.ClearFrames", "to.Arrive(v);") && ComesBefore(liftSame, "to.Arrive(v);", "ReadyNow(v)") && ComesBefore(liftSame, "FloorsGame.UnFade(LiftFade)", "to.Shown(v);"),
            "floors kept: the lift: the floor's furniture down, the player where they came in, the cuts let go, the next floor's furniture up, light again once it stands, then the floor's words");
        Check(MethodText(panels, "private static void DoorPressed(FloorHost host, int slot)").Contains("DoorPanel(host, at);   // the next page at once, on the press")
              && MethodText(panels, "private static void LiftPressed(FloorHost host, int slot)").Contains("LiftPanel(v, at);   // the next page at once, on the press"),
            "floors: \"More floors\" turns the page on its press, at the door and the lift");
    }

    // ---------------------------------------------------------------- the words

    private static void FloorsTexts()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("floors", langs, codes))
        {
            return;
        }
        var need = new Dictionary<string, string[]>
        {
            { "reallife_floors_floor", new[] { "{floor}", "{place}" } },
            { "reallife_floors_lift_here", new[] { "{floor}", "{place}" } },
            { "reallife_floors_host", new string[0] },
            { "reallife_floors_host_card_type", new string[0] },
            { "reallife_floors_host_door", new[] { "{count}" } },
            { "reallife_floors_test_place", new string[0] },
            { "reallife_floors_test_name", new string[0] },
            { "reallife_floors_test_done", new[] { "{floor}", "{address}" } },
            { "reallife_floors_test_none", new string[0] },
            { "reallife_floors_test_gone", new string[0] },
            { "reallife_campus_floor_more", new string[0] }
        };
        var missing = new List<string>();
        foreach (KeyValuePair<string, string[]> k in need)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                bool ok = langs[l].TryGetValue(k.Key, out string words) && !string.IsNullOrWhiteSpace(words);
                for (int p = 0; ok && p < k.Value.Length; p++)
                {
                    ok = words.Contains(k.Value[p]);
                }
                if (!ok)
                {
                    missing.Add(k.Key + " (" + codes[l] + ")");
                }
            }
        }
        Check(missing.Count == 0, "floors: every word of the floors is in every language with the figures it is filled with (" + need.Count + " keys" + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : string.Empty) + ")");
        bool short1 = true;
        foreach (string key in new[] { "reallife_floors_test_done", "reallife_floors_test_none", "reallife_floors_test_gone" })
        {
            short1 &= langs[0].TryGetValue(key, out string words) && words.Length <= 240;
        }
        Check(short1, "floors: the test's notices are a notification's length");
    }
}
