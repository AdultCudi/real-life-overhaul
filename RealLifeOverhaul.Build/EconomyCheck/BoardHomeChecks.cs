using System.Collections.Generic;

// A help-wanted board for each employer's own kind of building, and a board that a parked car sent to a spare building going back to the
// first one (Jobs/BoardHomeRules.cs, JobBoards.cs, CareerOfficeRules.Extend). A player's Brightline Promotions office stood in the Hamptons, a
// building of a kind that was none of its own, and stayed there.
internal static partial class Program
{
    private static string[][] RealPremises()
    {
        RealLifeOverhaul.Employer[] all = RealLifeOverhaul.CareerCatalogue.All;
        var premises = new string[all.Length][];
        for (int e = 0; e < all.Length; e++)
        {
            premises[e] = all[e].Premises;
        }
        return premises;
    }

    private static int EmployerOf(string id)
    {
        RealLifeOverhaul.Employer[] all = RealLifeOverhaul.CareerCatalogue.All;
        for (int e = 0; e < all.Length; e++)
        {
            if (all[e].Id == id)
            {
                return e;
            }
        }
        return -1;
    }

    private static void BoardHomeChecks()
    {
        string[][] premises = RealPremises();
        int promo = EmployerOf(RealLifeOverhaul.CareerCatalogue.Promo);

        // A city whose list shows fifteen kinds of service building, the marketing agency's last: the first six kinds were what the boards
        // stood at, so Brightline's own kind got none and its office was whichever board was left over.
        var city = new List<string>();
        foreach (string k in new[] { "furniturestore", "recruitmentagency", "casino", "school", "bank", "appliancestore", "importexport", "hospital", "gasstation", "supermarket",
                     "cardealership", "officesupplystore", "wholesalestore", "interiorinstallationfirm", "marketingagency" })
        {
            city.Add("ba:businesstype_" + k);
        }
        var chosen = new List<string>();
        RealLifeOverhaul.BoardHomeRules.Kinds(premises, city, 6, chosen);
        Check(chosen.Count == 6 && chosen.Contains("ba:businesstype_marketingagency"),
            "board home: Brightline's own kind of building, the marketing agency, gets a board though the city's list shows it last of fifteen");
        bool each = true;
        for (int e = 0; e < premises.Length; e++)
        {
            bool has = premises[e].Length == 0;
            for (int p = 0; p < premises[e].Length && !has; p++)
            {
                for (int k = 0; k < chosen.Count && !has; k++)
                {
                    has = RealLifeOverhaul.BoardHomeRules.Matches(chosen[k], premises[e][p]);
                }
            }
            each &= has;
        }
        Check(each, "board home: every employer with premises has a board of one of its own kinds whenever the city has one");
        var again = new List<string>();
        RealLifeOverhaul.BoardHomeRules.Kinds(premises, city, 6, again);
        Check(string.Join(",", chosen) == string.Join(",", again),
            "board home: the kinds chosen are the same every time for one city's list, and what a street holds for a second does not enter into it");
        var few = new List<string> { "ba:businesstype_furniturestore", "ba:businesstype_casino" };
        RealLifeOverhaul.BoardHomeRules.Kinds(premises, few, 6, chosen);
        Check(chosen.Count == 2 && chosen[0] == "ba:businesstype_furniturestore",
            "board home: a city with none of an employer's kinds gives it a board of the kinds it has, in the city's own order, and never more boards than kinds");

        // The office mapping a save is left with: Brightline holds a board of a kind that is none of its own (a furniture store, far away) and its own
        // kind's board comes up late. The office goes to it.
        string[] kinds = { "ba:businesstype_furniturestore", "ba:businesstype_bank", "ba:businesstype_hospital", "ba:businesstype_marketingagency" };
        var depot = new int[premises.Length];
        for (int e = 0; e < depot.Length; e++)
        {
            depot[e] = -1;
        }
        depot[promo] = 0;
        RealLifeOverhaul.CareerOfficeRules.Extend(premises, kinds, 3, depot);
        Check(depot[promo] == 3, "board home: an office left over at a building of none of the employer's kinds moves to the employer's own kind's board when that is up");
        Check(RealLifeOverhaul.CareerOfficeRules.Leftover(premises[promo], kinds, 0) && !RealLifeOverhaul.CareerOfficeRules.Leftover(premises[promo], kinds, 3),
            "board home: a board is a leftover for an employer only when it is the kind of none of its premises");
        int cleaners = EmployerOf(RealLifeOverhaul.CareerCatalogue.Cleaning);
        var settled = new int[premises.Length];
        for (int e = 0; e < settled.Length; e++)
        {
            settled[e] = -1;
        }
        settled[cleaners] = 2;   // the cleaners at the hospital, their own kind
        settled[promo] = 3;
        string[] more = { kinds[0], kinds[1], kinds[2], kinds[3], "ba:businesstype_eventplanning" };
        RealLifeOverhaul.CareerOfficeRules.Extend(premises, more, 4, settled);
        Check(settled[cleaners] == 2 && settled[promo] == 3, "board home: an office at the employer's own kind never moves for a later board");

        // A temporary block: the first building of a kind failed three asks while a car stood at its door, so the board stands at the second.
        // That board is asked at the first again, and goes back the first time the first can take it.
        Check(RealLifeOverhaul.BoardHomeRules.Mends(1, 0, true) && RealLifeOverhaul.BoardHomeRules.Mends(1, 99, true)
              && RealLifeOverhaul.BoardHomeRules.Mends(1, 0, false) && RealLifeOverhaul.BoardHomeRules.Mends(3, 5, false),
            "board home: a board at a spare building is asked at a better one again, near the player always and far from him a few times");
        Check(!RealLifeOverhaul.BoardHomeRules.Mends(0, 0, true) && !RealLifeOverhaul.BoardHomeRules.Mends(0, 0, false)
              && !RealLifeOverhaul.BoardHomeRules.Mends(1, RealLifeOverhaul.BoardHomeRules.FarAsks, false),
            "board home: a board at its home is never asked elsewhere, and a better building far from the player is given up for now once its asks have failed");
        Check(!RealLifeOverhaul.BoardHomeRules.MayMove(40f, 900f) && !RealLifeOverhaul.BoardHomeRules.MayMove(900f, 60f) && RealLifeOverhaul.BoardHomeRules.MayMove(900f, 900f),
            "board home: a board is moved only when the player is far from where it stands and from where it will stand");

        // The timeline of a car that parks at the first building and drives off: asks at load, three failed, the spare takes the board, later asks.
        int rank = 0;
        bool[] holds = { false, true };   // the first building's pavement is blocked, the second's is clear
        for (int ask = 1; ask <= 3 && rank == 0; ask++)
        {
            if (!holds[0] && ask >= 3)
            {
                rank = 1;   // JobBoards.Search: Tries failed asks, then the next building of the kind
            }
        }
        Check(rank == 1, "board home: three failed asks put the board at the second building, for now");
        holds[0] = true;   // the car has gone
        for (int c = 0; c < rank; c++)
        {
            if (RealLifeOverhaul.BoardHomeRules.Mends(rank, 0, false) && holds[c] && RealLifeOverhaul.BoardHomeRules.MayMove(300f, 300f))
            {
                rank = c;
                break;
            }
        }
        Check(rank == 0, "board home: once the first building can take a board it goes back there, whatever blocked it before");

        string boards = ModSource(@"Jobs\JobBoards.cs");
        string search = MemberText(boards, "private void Search()");
        string mend = MemberText(boards, "private void Mend(Vector3 player)");
        string tick = MemberText(boards, "public void Tick(Vector3 player)");
        Check(search.Contains("BoardHomeRules.Kinds(premises, _seen, Most, _chosen);") && search.Contains("if (_next < buildings.Length)")
              && search.Contains("Place(list[_spare], kind, _spare)"),
            "board home: nothing is put up before the whole list has been seen, the kinds are each employer's own first, and a spare building's board keeps its rank");
        Check(tick.Contains("Mend(player);") && mend.Contains("now < _mendAt") && mend.Contains("_mendAt = now + RetryEvery;") && mend.Contains("BoardHomeRules.Mends(")
              && mend.Contains("BoardHomeRules.MayMove(") && mend.Contains("Move(board, home, c, at, stand, outward);"),
            "board home: a spare board is asked at its better buildings from the one-second tick, one board in RetryEvery seconds, and moved only out of sight");
        Check(MemberText(boards, "private void Retry(Vector3 player)").Contains("Place(building, name, at)"),
            "board home: a board put up late at a spare building keeps its rank too and is mended the same way");
        string director = ModSource(@"Jobs\CareerDirector.cs");
        Check(MemberText(director, "private void OfficeMark(Vector3 player, JobBoards boards)").Contains("boards.Moves != _officeMoves")
              && ModSource(@"World\MapLinks.cs").Contains("long stamp = jobs.HelpBoards.Stamp;"),
            "board home: a destination marker on a board that moved comes off, and the city map's rows are drawn again");
    }
}
