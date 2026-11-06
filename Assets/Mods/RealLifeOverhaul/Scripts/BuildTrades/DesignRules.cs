using System;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>What may stand in a cell of a floor plan.</summary>
    public enum DesignRoom
    {
        Empty = 0,
        Stairs = 1,
        Lift = 2,
        Lobby = 3,    // on the ground floor, at the front: under a cell of the typical floor, not in it
        Studio = 4,   // one cell
        TwoBed = 5,   // two cells side by side
        Family = 6    // three cells side by side
    }

    /// <summary>
    /// A design's result, the shape agreed with the construction company (Scripts/Construction reads Total, CostPermille,
    /// Storeys and Units): the floor plan's four scores, the facade's satisfaction and the frame's, each 0 to 100, -1 for a
    /// part not designed. Its line is "d1|fit|light|cost|flow|facade|structure|total|storeys|units|costpermille".
    /// </summary>
    public struct DesignScore
    {
        public int Fit, Light, Cost, Flow;
        public int Facade;          // -1: not designed
        public int Structure;       // -1: not engineered; 0: the floors would not hold
        public int Total;
        public int Storeys, Units;
        public int CostPermille;    // thousandths of the brief's budget the design comes to

        public const string Head = "d1";

        public int Plan => (int)Math.Round(Fit * 0.35 + Light * 0.25 + Flow * 0.2 + Cost * 0.2);

        public string Write()
        {
            return Head + "|" + Fit + "|" + Light + "|" + Cost + "|" + Flow + "|" + Facade + "|" + Structure + "|" + Total + "|" + Storeys + "|" + Units + "|" + CostPermille;
        }

        /// <summary>A line written by <see cref="Write"/>; false (and nothing) for anything else.</summary>
        public static bool Read(string line, out DesignScore score)
        {
            score = default;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }
            string[] f = line.Split('|');
            if (f.Length < 11 || f[0] != Head)
            {
                return false;
            }
            int[] v = new int[10];
            for (int i = 0; i < 10; i++)
            {
                if (!int.TryParse(f[i + 1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v[i]))
                {
                    return false;
                }
            }
            score.Fit = v[0];
            score.Light = v[1];
            score.Cost = v[2];
            score.Flow = v[3];
            score.Facade = v[4];
            score.Structure = v[5];
            score.Total = v[6];
            score.Storeys = v[7];
            score.Units = v[8];
            score.CostPermille = v[9];
            return score.Fit >= 0 && score.Fit <= 100 && score.Total >= 0 && score.Total <= 100 && score.Storeys >= 0 && score.Units >= 0 && score.CostPermille >= 0;
        }
    }

    /// <summary>A client's brief: who they are, how high, the flats wanted on each floor, what it may cost, and their taste in a facade.</summary>
    public sealed class DesignBrief
    {
        public int Client;             // 0 starter flats, 1 family homes, 2 mixed, 3 luxury (reallife_trades_client_N)
        public int Storeys;
        public int Studios, TwoBeds, Families;
        public bool Lift;
        public bool Simple;            // a draughtsperson's sheet: one floor, the stairs drawn already, no lift or lobby
        public int Budget;             // the plan's cost it may come to, in cost units
        public int FacadeBudget;       // ...and the facade's
        public int Loves = -1, Hates = -1;   // a facade option (element * 4 + option) the client loves and one they hate
        public string Commission;      // the construction company's project it is for, or null for the office's own

        public int Flats => Studios + TwoBeds + Families;
        public int FlatCells => Studios + TwoBeds * 2 + Families * 3;
    }

    /// <summary>
    /// The architecture mini-game, game-free (EconomyCheck runs it): a floor plan of three rows of four cells (the front, the
    /// middle, the back) where the stairs, a lift, the lobby and the client's flats are placed, scored on fit, light, cost
    /// and flow; a facade of four elements chosen within the client's budget, scored by their satisfaction; and a frame of
    /// columns on thirteen lines with a slab, scored by whether it holds and what it costs against the cheapest that would.
    /// </summary>
    public static class DesignRules
    {
        public const int Cols = 4, Rows = 3, Cells = Cols * Rows;   // a typical floor: 4.5 m bays, 18 m by 13.5 m
        public const int Front = 0, Middle = 1, Back = 2;

        // Cost units, at the start's prices (a unit is about a thousand of money: Scripts/Construction prices the whole).
        public const int StudioCost = 10, TwoBedCost = 17, FamilyCost = 24, StairsCost = 8, LiftCost = 22, LobbyCost = 12, CorridorCost = 3;

        // ---- the brief ----

        // Flats a typical floor of each client wants: studios, two-beds, families (eight cells at most: two lit rows).
        private static readonly int[,] Templates = { { 4, 2, 0 }, { 2, 0, 2 }, { 1, 3, 0 }, { 0, 2, 1 } };

        /// <summary>A brief drawn from a seed (the office's own work); a draughtsperson's is one floor of three flats with the stairs drawn.</summary>
        public static DesignBrief Draw(uint seed, bool simple)
        {
            var dice = new TradeDice(seed);
            var brief = new DesignBrief { Client = dice.Range(0, 4), Simple = simple };
            if (simple)
            {
                brief.Storeys = 1;
                brief.Studios = dice.Range(1, 3);
                brief.TwoBeds = 3 - brief.Studios;
                brief.Families = 0;
                brief.Lift = false;
            }
            else
            {
                brief.Storeys = dice.Range(3, 7);
                brief.Studios = Templates[brief.Client, 0];
                brief.TwoBeds = Templates[brief.Client, 1];
                brief.Families = Templates[brief.Client, 2];
                brief.Lift = brief.Client == 3 || brief.Storeys >= 4;
            }
            brief.Budget = (int)Math.Round(Reference(brief) * dice.Range(1.0f, 1.12f));
            brief.FacadeBudget = (int)Math.Round(FacadeReference(brief) * dice.Range(0.9f, 1.15f));
            brief.Loves = dice.Range(0, Elements * Options);
            do
            {
                brief.Hates = dice.Range(0, Elements * Options);
            }
            while (brief.Hates / Options == brief.Loves / Options);
            return brief;
        }

        /// <summary>
        /// A brief for a construction company's project (Scripts/Construction): its storeys and flats; the client by what the
        /// budget allows a flat. The same project gives the same brief on every machine.
        /// </summary>
        public static DesignBrief ForProject(string projectId, int storeys, int units, int budget)
        {
            uint seed = Hash(projectId);
            var dice = new TradeDice(seed);
            int floors = Math.Max(2, Math.Min(10, storeys));
            int perFloor = Math.Max(2, Math.Min(6, (int)Math.Ceiling(Math.Max(1, units) / (double)floors)));
            float perFlat = budget / (float)Math.Max(1, units);
            int client = perFlat > 400000f ? 3 : perFloor >= 5 ? 0 : perFloor <= 3 ? 1 : 2;
            var brief = new DesignBrief { Client = client, Storeys = floors, Commission = projectId };
            brief.Studios = Templates[client, 0];
            brief.TwoBeds = Templates[client, 1];
            brief.Families = Templates[client, 2];
            brief.Lift = client == 3 || floors >= 4;
            brief.Budget = (int)Math.Round(Reference(brief) * dice.Range(1.0f, 1.08f));
            brief.FacadeBudget = (int)Math.Round(FacadeReference(brief) * dice.Range(0.9f, 1.1f));
            brief.Loves = dice.Range(0, Elements * Options);
            do
            {
                brief.Hates = dice.Range(0, Elements * Options);
            }
            while (brief.Hates / Options == brief.Loves / Options);
            return brief;
        }

        /// <summary>A text's hash that is the same in every build and on every machine (not string.GetHashCode).</summary>
        public static uint Hash(string text)
        {
            uint h = 2166136261u;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                h = (h ^ text[i]) * 16777619u;
            }
            return h;
        }

        /// <summary>What the brief's flats come to on a good plan: every flat beside the stairs, no corridor, a floor a storey.</summary>
        public static int Reference(DesignBrief b)
        {
            int floor = b.Studios * StudioCost + b.TwoBeds * TwoBedCost + b.Families * FamilyCost + StairsCost + (b.Lift ? LiftCost : 0);
            return floor * Math.Max(1, b.Storeys) + (b.Simple ? 0 : LobbyCost);
        }

        // ---- the plan ----

        /// <summary>The rooms to place, in order: the stairs (drawn already on a draughtsperson's sheet), the lift, the lobby, then the flats from the largest.</summary>
        public static int Order(DesignBrief b, DesignRoom[] into)
        {
            int n = 0;
            if (!b.Simple)
            {
                into[n++] = DesignRoom.Stairs;
                if (b.Lift)
                {
                    into[n++] = DesignRoom.Lift;
                }
                into[n++] = DesignRoom.Lobby;
            }
            for (int i = 0; i < b.Families && n < into.Length; i++)
            {
                into[n++] = DesignRoom.Family;
            }
            for (int i = 0; i < b.TwoBeds && n < into.Length; i++)
            {
                into[n++] = DesignRoom.TwoBed;
            }
            for (int i = 0; i < b.Studios && n < into.Length; i++)
            {
                into[n++] = DesignRoom.Studio;
            }
            return n;
        }

        public const int SimpleStairs = Middle * Cols + 1;   // where the stairs stand on a draughtsperson's sheet

        public static int Length(DesignRoom room) => room == DesignRoom.Family ? 3 : room == DesignRoom.TwoBed ? 2 : 1;

        public static bool Flat(DesignRoom room) => room == DesignRoom.Studio || room == DesignRoom.TwoBed || room == DesignRoom.Family;

        /// <summary>
        /// The places a room can go now, each its first cell (a flat runs to the right of it in the same row); the lobby's are the
        /// four front cells of the ground floor, whatever stands above them. Returns how many, into <paramref name="places"/>.
        /// </summary>
        public static int Places(DesignRoom room, DesignRoom[] cells, int[] places)
        {
            int n = 0;
            if (room == DesignRoom.Lobby)
            {
                for (int c = 0; c < Cols; c++)
                {
                    places[n++] = Front * Cols + c;
                }
                return n;
            }
            int length = Length(room);
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c + length <= Cols; c++)
                {
                    bool free = true;
                    for (int k = 0; k < length && free; k++)
                    {
                        free = cells[r * Cols + c + k] == DesignRoom.Empty;
                    }
                    if (free && n < places.Length)
                    {
                        places[n++] = r * Cols + c;
                    }
                }
            }
            return n;
        }

        /// <summary>Puts a room at a place <see cref="Places"/> gave: its cells written, the lobby's front cell kept apart.</summary>
        public static void Put(DesignRoom room, int at, DesignRoom[] cells, ref int lobby)
        {
            if (room == DesignRoom.Lobby)
            {
                lobby = at;
                return;
            }
            int length = Length(room);
            for (int k = 0; k < length; k++)
            {
                cells[at + k] = room;
            }
        }

        /// <summary>Takes a room back off the plan from its first cell.</summary>
        public static void Take(DesignRoom room, int at, DesignRoom[] cells, ref int lobby)
        {
            if (room == DesignRoom.Lobby)
            {
                lobby = -1;
                return;
            }
            int length = Length(room);
            for (int k = 0; k < length && at + k < cells.Length; k++)
            {
                cells[at + k] = DesignRoom.Empty;
            }
        }

        /// <summary>Daylight in a cell: the front and the back rows have windows; the middle row none.</summary>
        public static bool Lit(int cell) => cell / Cols != Middle;

        /// <summary>Steps from a cell to the nearest core (the stairs or the lift); a large number with none.</summary>
        public static int ToCore(int cell, DesignRoom[] cells)
        {
            int best = 99;
            for (int i = 0; i < Cells; i++)
            {
                if (cells[i] == DesignRoom.Stairs || cells[i] == DesignRoom.Lift)
                {
                    int d = Math.Abs(i / Cols - cell / Cols) + Math.Abs(i % Cols - cell % Cols);
                    best = d < best ? d : best;
                }
            }
            return best;
        }

        /// <summary>How easy a flat is to reach by its steps to a core: beside it 1, two steps 0.7, three 0.45, further 0.2.</summary>
        public static float Reach(int steps) => steps <= 1 ? 1f : steps == 2 ? 0.7f : steps == 3 ? 0.45f : 0.2f;

        /// <summary>
        /// The plan's four scores, its storeys, flats and cost against the brief. <paramref name="starts"/> are each placed
        /// room's first cell by the order (-1 left out); the cells say what stands where.
        /// </summary>
        public static DesignScore Score(DesignBrief b, DesignRoom[] order, int count, int[] starts, DesignRoom[] cells, int lobby)
        {
            var s = new DesignScore { Facade = -1, Structure = -1, Storeys = Math.Max(1, b.Storeys) };
            int wanted = b.FlatCells, placedCells = 0, flats = 0, litCells = 0;
            float reach = 0f;
            int corridor = 0, cost = 0;
            bool stairs = b.Simple;
            for (int i = 0; i < count; i++)
            {
                DesignRoom room = order[i];
                int at = starts[i];
                if (at < 0)
                {
                    continue;
                }
                if (room == DesignRoom.Stairs)
                {
                    stairs = true;
                    cost += StairsCost;
                }
                else if (room == DesignRoom.Lift)
                {
                    cost += LiftCost;
                }
                else if (Flat(room))
                {
                    int length = Length(room);
                    int steps = 99;
                    for (int k = 0; k < length; k++)
                    {
                        litCells += Lit(at + k) ? 1 : 0;
                        int d = ToCore(at + k, cells);
                        steps = d < steps ? d : steps;
                    }
                    placedCells += length;
                    flats++;
                    reach += Reach(steps);
                    corridor += steps > 1 && steps < 99 ? steps - 1 : 0;
                    cost += room == DesignRoom.Studio ? StudioCost : room == DesignRoom.TwoBed ? TwoBedCost : FamilyCost;
                }
            }
            if (b.Simple)
            {
                cost += StairsCost;
            }
            cost += corridor * CorridorCost;
            int total = cost * s.Storeys + (!b.Simple && lobby >= 0 ? LobbyCost : 0);
            s.Fit = wanted > 0 ? (int)Math.Round(100.0 * placedCells / wanted) : 100;
            if (!stairs)
            {
                s.Fit = Math.Min(s.Fit, 30);   // no way up: it is not a building
            }
            s.Light = placedCells > 0 ? (int)Math.Round(100.0 * litCells / placedCells) : 0;
            float flatFlow = flats > 0 ? reach / flats : 0f;
            float lobbyFlow = b.Simple ? 1f : lobby < 0 ? 0f : Reach(ToCore(lobby, cells));   // the lobby under the floor's cell: its steps to a core above
            s.Flow = (int)Math.Round(100f * (b.Simple ? flatFlow : 0.8f * flatFlow + 0.2f * lobbyFlow));
            s.CostPermille = b.Budget > 0 ? (int)Math.Round(1000.0 * total / b.Budget) : 1000;
            s.Cost = CostScore(s.CostPermille);
            // The typical floor's flats on every storey above the ground, and the ground's less the one the lobby takes.
            s.Units = b.Simple ? flats : flats * (s.Storeys - 1) + Math.Max(0, flats - (lobby >= 0 ? 1 : 0));
            s.Total = s.Plan;
            return s;
        }

        /// <summary>The cost score by thousandths of the budget: 100 within it, 4 points off for each per cent over.</summary>
        public static int CostScore(int permille)
        {
            if (permille <= 1000)
            {
                return 100;
            }
            int score = 100 - (permille - 1000) * 4 / 10;
            return score < 0 ? 0 : score;
        }

        // ---- the facade ----

        public const int Elements = 4, Options = 4;   // walls, windows, the roof line, the entrance; four choices each

        // Each option's cost a storey, in cost units (walls) or once (the rest), and each client's taste for it, -2 to 2.
        private static readonly int[,] OptionCost =
        {
            { 6, 5, 10, 8 },    // walls: brick, render, stone, metal panels
            { 4, 4, 6, 3 },     // windows: sash, ribbon, full height, small
            { 3, 5, 9, 2 },     // the roof line: parapet, cornice, a set-back penthouse, plain
            { 2, 4, 7, 1 }      // the entrance: a canopy, a stone surround, a portico, plain
        };

        private static readonly int[,,] Taste =
        {
            // starter flats: cheap and modern
            { { 1, 1, -1, 2 }, { 0, 2, 1, 0 }, { 2, 0, -1, 1 }, { 2, 0, -1, 1 } },
            // family homes: traditional
            { { 2, 0, 1, -2 }, { 2, -1, 0, 0 }, { 0, 2, -1, 0 }, { 1, 2, 0, -1 } },
            // mixed: a bit of both
            { { 2, 1, 0, 0 }, { 1, 1, 1, -1 }, { 1, 1, 0, 0 }, { 2, 1, 0, -1 } },
            // luxury: stone and glass
            { { 0, -1, 2, 1 }, { 0, 1, 2, -2 }, { -1, 1, 2, -2 }, { -1, 1, 2, -2 } }
        };

        /// <summary>An option's cost for a building of so many storeys: the walls by the storey, the rest once.</summary>
        public static int FacadeCost(int element, int option, int storeys)
        {
            int cost = OptionCost[Clamp(element, 0, Elements - 1), Clamp(option, 0, Options - 1)];
            return element == 0 ? cost * Math.Max(1, storeys) : cost;
        }

        /// <summary>What a facade of the middle choices comes to: the budget is drawn round it.</summary>
        public static int FacadeReference(DesignBrief b)
        {
            int total = 0;
            for (int e = 0; e < Elements; e++)
            {
                total += FacadeCost(e, e == 0 ? 0 : 1, b.Storeys);
            }
            return total;
        }

        /// <summary>The client's taste for an option, with what they said they love and hate.</summary>
        public static int TasteOf(DesignBrief b, int element, int option)
        {
            int at = element * Options + option;
            int taste = Taste[Clamp(b.Client, 0, 3), Clamp(element, 0, Elements - 1), Clamp(option, 0, Options - 1)];
            if (at == b.Loves)
            {
                taste += 2;
            }
            if (at == b.Hates)
            {
                taste -= 3;
            }
            return taste;
        }

        /// <summary>
        /// The client's satisfaction with a facade, 0 to 100: their taste for the four choices from the worst there could be to
        /// the best, less a fifth of a point for each thousandth over the facade's budget. What the meter shows.
        /// </summary>
        public static int Satisfaction(DesignBrief b, int[] choice, out int cost)
        {
            int taste = 0, best = 0, worst = 0;
            cost = 0;
            for (int e = 0; e < Elements; e++)
            {
                int most = int.MinValue, least = int.MaxValue;
                for (int o = 0; o < Options; o++)
                {
                    int t = TasteOf(b, e, o);
                    most = t > most ? t : most;
                    least = t < least ? t : least;
                }
                best += most;
                worst += least;
                taste += TasteOf(b, e, Clamp(choice[e], 0, Options - 1));
                cost += FacadeCost(e, choice[e], b.Storeys);
            }
            float share = best > worst ? (taste - worst) / (float)(best - worst) : 1f;
            int score = (int)Math.Round(100f * share);
            if (b.FacadeBudget > 0 && cost > b.FacadeBudget)
            {
                int over = (int)Math.Round(1000.0 * (cost - b.FacadeBudget) / b.FacadeBudget);
                score -= over / 5;
            }
            return score < 0 ? 0 : score > 100 ? 100 : score;
        }

        /// <summary>The best satisfaction any facade could have for the brief (all 256 tried): a facade is paid against it.</summary>
        public static int BestSatisfaction(DesignBrief b)
        {
            int[] choice = new int[Elements];
            int best = 0;
            for (int code = 0; code < 256; code++)
            {
                choice[0] = code & 3;
                choice[1] = (code >> 2) & 3;
                choice[2] = (code >> 4) & 3;
                choice[3] = (code >> 6) & 3;
                int s = Satisfaction(b, choice, out _);
                best = s > best ? s : best;
            }
            return best;
        }

        // ---- the frame ----

        public const int Lines = 13;                 // column lines across the 18 m front, every 1.5 m; the two ends always stand
        public const float LineStep = 1.5f;          // metres
        public const int Slabs = 3;                  // 0 a thin slab, 1 a thick one, 2 a post-tensioned one
        private static readonly int[] SlabSpan = { 4, 6, 8 };     // steps of 1.5 m a slab spans: 6, 9 and 12 m
        private static readonly int[] SlabCost = { 10, 14, 19 };  // cost units a floor
        public const int ColumnCost = 3;             // cost units a column a storey
        public const int InRoom = 12;                // points off for a column standing in the middle of a bay (a room), not on its wall

        public static int SpanSteps(int slab) => SlabSpan[Clamp(slab, 0, Slabs - 1)];

        /// <summary>True if a line is on a wall between bays (every third), where a column stands in nobody's way.</summary>
        public static bool OnWall(int line) => line % 3 == 0;

        /// <summary>The longest gap between columns, in steps of 1.5 m; the ends always stand.</summary>
        public static int LongestSpan(bool[] columns)
        {
            int longest = 0, last = 0;
            for (int i = 1; i < Lines; i++)
            {
                if (i == Lines - 1 || columns[i])
                {
                    longest = Math.Max(longest, i - last);
                    last = i;
                }
            }
            return longest;
        }

        /// <summary>A frame's cost: its columns (the two ends with the rest) and its slab, every storey.</summary>
        public static int FrameCost(bool[] columns, int slab, int storeys)
        {
            int count = 2;
            for (int i = 1; i < Lines - 1; i++)
            {
                count += columns[i] ? 1 : 0;
            }
            return (count * ColumnCost + SlabCost[Clamp(slab, 0, Slabs - 1)]) * Math.Max(1, storeys);
        }

        /// <summary>True if every span is within what the slab carries.</summary>
        public static bool Holds(bool[] columns, int slab) => LongestSpan(columns) <= SpanSteps(slab);

        /// <summary>The cheapest frame that holds with no column in a room, tried every way (2 to the 11 by three slabs).</summary>
        public static int CheapestFrame(int storeys)
        {
            var columns = new bool[Lines];
            int best = int.MaxValue;
            for (int code = 0; code < 1 << (Lines - 2); code++)
            {
                bool clear = true;
                for (int i = 1; i < Lines - 1; i++)
                {
                    columns[i] = (code & (1 << (i - 1))) != 0;
                    clear &= !columns[i] || OnWall(i);
                }
                if (!clear)
                {
                    continue;
                }
                for (int slab = 0; slab < Slabs; slab++)
                {
                    if (Holds(columns, slab))
                    {
                        int cost = FrameCost(columns, slab, storeys);
                        best = cost < best ? cost : best;
                    }
                }
            }
            return best;
        }

        /// <summary>The frame's score: 0 if it would not hold, else 100 at the cheapest that would, less by its cost over that and for each column in a room.</summary>
        public static int FrameScore(bool[] columns, int slab, int storeys, int cheapest)
        {
            if (!Holds(columns, slab))
            {
                return 0;
            }
            int cost = FrameCost(columns, slab, storeys);
            int score = cheapest > 0 ? (int)Math.Round(100.0 * cheapest / cost) : 100;
            for (int i = 1; i < Lines - 1; i++)
            {
                score -= columns[i] && !OnWall(i) ? InRoom : 0;
            }
            return score < 0 ? 0 : score > 100 ? 100 : score;
        }

        // ---- the whole ----

        /// <summary>The design's total: the plan 60 %, the facade 25 %, the frame 15 %, over the parts done.</summary>
        public static int Total(in DesignScore s)
        {
            double sum = s.Plan * 0.6, weight = 0.6;
            if (s.Facade >= 0)
            {
                sum += s.Facade * 0.25;
                weight += 0.25;
            }
            if (s.Structure >= 0)
            {
                sum += s.Structure * 0.15;
                weight += 0.15;
            }
            int total = (int)Math.Round(sum / weight);
            return total < 0 ? 0 : total > 100 ? 100 : total;
        }

        /// <summary>The whole design's cost against the whole budget once its facade is chosen: the plan's share and the facade's together.</summary>
        public static int CostWith(DesignBrief b, int planPermille, int facadeCost)
        {
            long plan = (long)planPermille * b.Budget / 1000;
            long budget = (long)b.Budget + b.FacadeBudget;
            return budget > 0 ? (int)Math.Round(1000.0 * (plan + facadeCost) / budget) : 1000;
        }

        /// <summary>A stage's quality against what it could be, 0 to 1, for its pay.</summary>
        public static float Quality(int score, int best) => best <= 0 ? 1f : TradeRules.Clamp01(score / (float)best);

        private static int Clamp(int v, int low, int high) => v < low ? low : v > high ? high : v;
    }
}
