using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The tech companies' towers (Tech/Sites/HqTowers.cs): which of the city's office buildings house the twelve companies, a
// floor each, the same on every machine; never a building taken, never one twice; the buildings the companies held one
// each are back on the market (Core/SpecialNameBook.cs refuses only the towers); the floors offered at a door and the lift;
// and the player's desk kept on the floor of the company they work for, wherever its tower moves.
internal static partial class Program
{
    private static readonly string[] Firms = { "Codeharbour Software", "Skyvault Systems", "Abacus Lane Software", "Shopfront Online", "Pixel Harbour Games", "Tallyrow Analytics",
        "Signpost Search", "Circuitry Works", "Quartzline Semiconductors", "Ledgerline Payments", "Signalhill Telecom", "Fibreway Networks" };

    private static readonly string[] TowerNames = { "Harbourgate Tower", "Meridian Exchange", "Lanternhill House", "Northbank Tower", "Copperfield Court", "Westwater House" };

    private static List<HqCandidate> City(int count, int seed)
    {
        var all = new List<HqCandidate>();
        var rnd = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            all.Add(new HqCandidate { Street = "Street " + (i % 7), Number = 10 + i, Sqm = 300 + rnd.Next(4000) });
        }
        return all;
    }

    private static void Towers()
    {
        int wanted = HqTowers.Towers(Firms.Length);
        Check(wanted == 3 && HqTowers.PerTower == 4, "towers: twelve companies make three towers of four floors (" + wanted + " of " + HqTowers.PerTower + ")");
        Check(HqTowers.Towers(0) == 0 && HqTowers.Towers(1) == 1 && HqTowers.Towers(4) == 1 && HqTowers.Towers(5) == 2, "towers: one tower for up to four companies, one more for each four after");

        // ---------------------------------------------------------------- the picks
        List<HqCandidate> all = City(50, 20261014);
        int[] pick = new int[wanted], again = new int[wanted];
        int have = HqTowers.Pick(all, wanted, null, pick);
        HqTowers.Pick(all, wanted, null, again);
        Check(have == 3 && Same(pick, again) && Distinct(pick) && Array.IndexOf(pick, -1) < 0, "towers: the same city gives the same three towers every time, never one building twice");
        var shuffled = new List<HqCandidate>(all);
        shuffled.Reverse();
        int[] fromShuffled = new int[wanted];
        HqTowers.Pick(shuffled, wanted, null, fromShuffled);
        bool sameBuildings = true;
        for (int t = 0; t < wanted; t++)
        {
            sameBuildings &= HqTowers.Key(all[pick[t]].Street, all[pick[t]].Number) == HqTowers.Key(shuffled[fromShuffled[t]].Street, shuffled[fromShuffled[t]].Number);
        }
        Check(sameBuildings, "towers: the order the game lists its buildings in does not change the towers (every machine of a session alike)");
        var sizes = new List<int>();
        foreach (HqCandidate c in all)
        {
            sizes.Add(c.Sqm);
        }
        sizes.Sort();
        bool largest = true;
        foreach (int i in pick)
        {
            largest &= all[i].Sqm >= sizes[sizes.Count - wanted];
        }
        Check(largest, "towers: with every office empty, the towers are the city's three largest office buildings");

        // ---------------------------------------------------------------- never a building taken; a let moves only its tower
        var rnd = new Random(20261014);
        bool neverTaken = true, nobodyMoves = true, onlyItsOwn = true, near = true;
        for (int round = 0; round < 200; round++)
        {
            List<HqCandidate> city = City(20 + rnd.Next(40), round);
            var let = new bool[city.Count];
            int free = 0;
            for (int i = 0; i < let.Length; i++)
            {
                let[i] = rnd.Next(3) == 0;
                free += let[i] ? 0 : 1;
            }
            int[] chosen = new int[wanted];
            int got = HqTowers.Pick(city, wanted, i => let[i], chosen);
            near &= got == Math.Min(wanted, free);
            foreach (int i in chosen)
            {
                neverTaken &= i < 0 || !let[i];
            }
            int spare = -1;
            for (int i = 0; i < let.Length && spare < 0; i++)
            {
                spare = !let[i] && Array.IndexOf(chosen, i) < 0 ? i : -1;
            }
            int[] later = new int[wanted];
            if (spare >= 0)
            {
                let[spare] = true;
                HqTowers.Pick(city, wanted, i => let[i], later);
                nobodyMoves &= Same(chosen, later);
                let[spare] = false;
            }
            for (int t = 0; t < wanted; t++)
            {
                if (chosen[t] < 0)
                {
                    continue;
                }
                int lost = chosen[t];
                let[lost] = true;
                HqTowers.Pick(city, wanted, i => let[i], later);
                for (int u = 0; u < wanted; u++)
                {
                    onlyItsOwn &= u == t ? later[u] != lost : later[u] == chosen[u];
                }
                let[lost] = false;
            }
        }
        Check(neverTaken, "towers: in 200 cities a building a player rents or owns, a rival's, another feature's, is never a tower");
        Check(near, "towers: every tower has a building while there are enough free, however they fall");
        Check(nobodyMoves, "towers: a building let that is no tower moves no tower");
        Check(onlyItsOwn, "towers: a tower whose building is let moves, and no other tower does");

        // ---------------------------------------------------------------- the floors
        int[] towerOf = new int[Firms.Length], floorOf = new int[Firms.Length];
        HqTowers.Floors(Firms.Length, pick, towerOf, floorOf);
        bool everyone = true, plain = true;
        var places = new HashSet<string>();
        for (int k = 0; k < Firms.Length; k++)
        {
            everyone &= towerOf[k] >= 0 && floorOf[k] >= 1 && places.Add(towerOf[k] + "/" + floorOf[k]);
            plain &= towerOf[k] == k / HqTowers.PerTower && floorOf[k] == k % HqTowers.PerTower + 1;
        }
        Check(everyone, "towers: every company has a floor, and no two share one");
        Check(plain && HqTowers.FloorsOf(0, towerOf, Firms.Length) == 4 && HqTowers.FloorsOf(2, towerOf, Firms.Length) == 4,
            "towers: four floors to a tower, the companies in order (Skyvault Systems on floor 2 of the first)");
        int[] two = { 5, -1, 9 };
        HqTowers.Floors(Firms.Length, two, towerOf, floorOf);
        bool spread = true;
        places.Clear();
        for (int k = 0; k < Firms.Length; k++)
        {
            spread &= (towerOf[k] == 0 || towerOf[k] == 2) && floorOf[k] >= 1 && floorOf[k] <= 6 && places.Add(towerOf[k] + "/" + floorOf[k]);
        }
        Check(spread && HqTowers.FloorsOf(0, towerOf, Firms.Length) == 6 && HqTowers.FloorsOf(2, towerOf, Firms.Length) == 6,
            "towers: with two buildings free, six floors to each, and every company still has one");
        int[] one = { -1, 3, -1 };
        HqTowers.Floors(Firms.Length, one, towerOf, floorOf);
        Check(HqTowers.FloorsOf(1, towerOf, Firms.Length) == 12 && floorOf[11] == 12, "towers: with one building free, all twelve on its twelve floors");
        int[] none = { -1, -1, -1 };
        HqTowers.Floors(Firms.Length, none, towerOf, floorOf);
        Check(Array.TrueForAll(towerOf, t => t == -1) && Array.TrueForAll(floorOf, f => f == 0), "towers: with no office building free, no company has a floor (they keep to the Tech app)");
        int[] few = new int[wanted];
        Check(HqTowers.Pick(City(2, 3), wanted, null, few) == 2 && few[2] == -1, "towers: a city of two empty offices has two towers");
        Check(HqTowers.Pick(new List<HqCandidate>(), wanted, null, few) == 0 && few[0] == -1, "towers: a city of none has none");

        // ---------------------------------------------------------------- the door and the lift
        Check(HqTowers.DoorFloor(4, 3) == 3 && HqTowers.DoorFloor(4, 0) == 1 && HqTowers.DoorFloor(4, 9) == 1 && HqTowers.DoorFloor(0, 2) == 0,
            "towers: the game's own door takes the player to their employer's floor, else the first");
        int[] slots = new int[4];
        HqTowers.Page(4, 0, 0, slots);
        bool door = slots[0] == 1 && slots[1] == 2 && slots[2] == 3 && slots[3] == 4;
        HqTowers.Page(4, 2, 0, slots);
        bool lift = slots[0] == 1 && slots[1] == 3 && slots[2] == 4 && slots[3] == 0;
        Check(door && lift, "towers: the door offers the four floors, the lift on floor 2 the three others");
        var seen = new HashSet<int>();
        for (int page = 0; page < 4; page++)
        {
            HqTowers.Page(12, 0, page, slots);
            for (int s = 0; s < 3; s++)
            {
                seen.Add(slots[s]);
            }
            door &= slots[3] == -1;
        }
        Check(door && seen.Count == 12 && !seen.Contains(0), "towers: twelve floors on one building are offered three to a page with \"more floors\", every one reached");

        // ---------------------------------------------------------------- off the market: the towers only
        var book = new SpecialNameBook();
        var tenants = new string[4];
        for (int t = 0; t < wanted; t++)
        {
            Array.Copy(Firms, t * 4, tenants, 0, 4);
            book.Set("techhq_tower_" + t, all[pick[t]].Street, all[pick[t]].Number, SpecialKind.Other, TowerNames[t], "reallife_hq_tower_card_type", () => true, literal: true, tenants: tenants);
        }
        int refused = 0;
        foreach (HqCandidate c in all)
        {
            refused += book.Refuses(c.Street, c.Number, false, false) ? 1 : 0;
        }
        Check(refused == wanted, "towers: the mod holds " + refused + " office buildings for the twelve companies, where it held twelve: the rest are to let");
        SpecialEntry first = book.At(all[pick[0]].Street, all[pick[0]].Number);
        Check(first != null && first.Tenants != null && first.Tenants.Length == 4 && first.Tenants[1] == "Skyvault Systems" && !ReferenceEquals(first.Tenants, tenants),
            "towers: a tower names its companies by floor, for the search and its sign (a copy of its own)");
        Check(!book.Refuses(all[pick[0]].Street, all[pick[0]].Number, true, false), "towers: a tower's building the player has come to rent is theirs, not refused (the tower moves)");

        // ---------------------------------------------------------------- the desk on the employer's floor
        string place = "Street%203~13";
        string mine = HqTowers.FloorPlace(place, "Skyvault Systems"), theirs = HqTowers.FloorPlace(place, "Codeharbour Software");
        Check(mine != theirs && mine == HqTowers.FloorPlace(place, "Skyvault Systems") && HqTowers.FloorPlace(place, null) == place && HqTowers.FloorPlace(null, "Skyvault Systems") == null,
            "towers: each company's floor of a tower has a place of its own; the player's desk is on the employer's floor alone");
        // The employer's tower moves (its building let): the job's address follows the company (Careers), and the desk with it.
        var moving = new bool[all.Count];
        moving[pick[0]] = true;
        int[] after = new int[wanted];
        HqTowers.Pick(all, wanted, i => moving[i], after);
        HqTowers.Floors(Firms.Length, after, towerOf, floorOf);
        HqCandidate now = all[after[towerOf[1]]];
        string jobPlace = HqTowers.FloorPlace(now.Street + "~" + now.Number, Firms[1]);
        string floorPlace = HqTowers.FloorPlace(now.Street + "~" + now.Number, Firms[towerOf[1] * 4 + floorOf[1] - 1]);
        Check(after[0] != pick[0] && jobPlace == floorPlace && jobPlace != HqTowers.FloorPlace(all[pick[0]].Street + "~" + all[pick[0]].Number, Firms[1]),
            "towers: a job at a company whose tower moved has its desk on that company's floor of the new tower, not in the old building");

        float r, gr, b;
        HqColours.Of(1, false, out r, out gr, out b);
        Check(HqColours.Count == Firms.Length && b > r && b > gr, "sites: a colour for each company (Skyvault Systems is blue)");
    }
}
