using System;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// The kinds of placed item the decor score counts, from the game's own item tags and item types (Interiors/InteriorGame
    /// sorts each item). Saved nowhere; the order is the tab's.
    /// </summary>
    public enum DecorCategory
    {
        Art = 0,         // wall art, mirrors and ornaments: tag decoration or type Decoration (seasonal ones too)
        Greenery = 1,    // tag plant
        Lighting = 2,    // tag light or type LightSource
        Audio = 3,       // tag loudspeaker or type RadioSource
        Floor = 4,       // rugs and floor decor: tag flatonfloor or type FlatDecoration
        Furniture = 5,   // seats, tables, desks and beds: counted up to a small share only
        Equipment = 6    // shelves, tills, workstations, machines: counted up to a smaller share still
    }

    /// <summary>What sort of place the business is, for what its interior can do.</summary>
    public enum InteriorKind
    {
        None = 0,        // not one the feature looks at (a warehouse, a factory, an empty unit)
        Shop = 1,        // customers come in: footfall, prices, the basket
        Office = 2       // a headquarters or an office: staff, hiring and client wins only
    }

    /// <summary>One business as the feature saw it this morning: what was read, and every figure worked out from it. Not saved.</summary>
    public sealed class InteriorShop
    {
        public string Place = string.Empty;
        public string Name = string.Empty;
        public string District = string.Empty;
        public InteriorKind Kind;
        public bool Brand;                      // trades under one of the player's brands, or is a firm of the tech sector (filled by the links)

        // read from the game
        public int Interior;                    // the game's own interior design score, 0 to 100 (walls and floors)
        public int Sqm;                         // floor area
        public int Clean = 100;                 // the game's own cleanliness, 0 to 100
        public int Income = InteriorRules.IncomeMiddle;   // the district's income level, 60 to 140 (100 is the city's middle)
        public int GameMinimum;                 // the game's own minimum interior score for the district (Midtown asks for one)
        public readonly int[] Points = new int[InteriorRules.Categories];   // tenths of a point, before each category's cap
        public readonly int[] Items = new int[InteriorRules.Categories];    // items placed, by category
        public int Kinds;                       // different kinds of decor item (furniture and equipment not counted)
        public bool RestCorner;                 // a seat, a table and something from a kitchen: somewhere for staff to take a break

        // worked out
        public int Target;                      // the points a place of this size needs for full marks, tenths
        public readonly int[] Capped = new int[InteriorRules.Categories];   // each category's points after its cap, tenths
        public int DecorRaw, Decor, InteriorNow, Overall, Standard;
        public int Wear;                        // thousandths, 0 fresh to 1000 worn out (the file's)
        public int StandardFootfall, DecorFootfall, Footfall, Bears, Basket, BrandPoints, StandingPoints, Comfort, OfficeEsteem, OfficeBid;
        public int ComplaintChance;             // per mille today
    }

    /// <summary>What the feature keeps for one business between days: its finishes' wear and when it last made news. Saved.</summary>
    public sealed class InteriorFile
    {
        public string Place = string.Empty;
        public int Wear;               // thousandths
        public int LastInterior = -1;  // the game's interior score when last read (a redecoration is seen by it rising)
        public int LastDay;            // the last day it was read
        public int StoryDay = -1000;   // the last day it was in the City Ledger or a Flare draft
        public int RefreshDay = -1;    // the last day its finishes were refreshed
    }

    /// <summary>
    /// The rules of "interiors and decor matter", game-free and checked by EconomyCheck (InteriorChecks): the decor
    /// score from the placed items, the target by floor area, wear and dirt, the district's standard, and every effect
    /// with its cap. Every figure the Guide prints comes from here.
    ///
    /// THE SCORES
    ///   Interior   the game's own interior design score (walls and floors, InteriorScoreCalculator), 0 to 100.
    ///   Decor      the placed items, sorted by the game's own tags and types into seven categories. An item kind's
    ///              first copy counts its category's points, each further copy of the same kind 60% of the one
    ///              before, five copies at most. Each category counts up to its share of the target: art, greenery
    ///              and lighting 30% each, floor decor 20%, audio 15%, furniture 15%, equipment 10%; so no single
    ///              kind of thing fills it, and variety is needed for full marks. The target grows with the floor
    ///              area: 12 points for a small shop, 1 more for each 12 m2 over 24, 90 at most. Each different kind
    ///              of decor item over three adds a point, 8 at most.
    ///   Wear       the finishes age: 3 thousandths a day, up to 4 more on a dirty floor, half as much while the
    ///              cleaning service looks after the place. Refreshing them sets it back to nothing; redecorating
    ///              (the game's interior score rising by 10 or more) halves it.
    ///   Both scores lose up to 20% at full wear and up to 15% for dirt (cleanliness under 70).
    ///   Overall    six tenths interior, four tenths decor.
    ///
    /// THE DISTRICT'S STANDARD: 15 in the poorest district (income 60) up to 70 in the richest (140), rising with its
    /// income level, and never under the game's own minimum for it (Midtown's).
    ///
    /// THE EFFECTS (each inside its cap; the footfall parts then inside business pressure's own -25% to +15% for
    /// everything the mod adds):
    ///   standard     short of it: 1% fewer customers for each 4 points, 8% at most, and a complaint more likely
    ///                (4 per mille a day for each point short from 10 short, 120 at most); over it: 1% more for each
    ///                8 points, 3% at most. Never no customers.
    ///   decor        up to 5% more customers at a decor score of 100.
    ///   footfall     the two together, -8% to +8%.
    ///   prices       over the standard the district bears 1 thousandth more of the price for each point, 3% at
    ///                most; under it 1 thousandth less for each point, 2% at most.
    ///   basket       over the standard, a chance of 2 per mille a point (6% at most) that a customer takes one more
    ///                product the shop sells and has in stock.
    ///   brand        a shop of a brand counts up to 3 points more or less in its brand's standards.
    ///   standing     a shop's figure in the customers' reputation moves up to 2 points either way.
    ///   staff        comfort 0 to 5: plants (1, or 2 with three or more), good light (1), a rest corner (2):
    ///                the calm level contented staff settle at goes up by the comfort, and a professional's morale
    ///                heads 8 thousandths higher for each point.
    ///   offices      the best office of the player's at 60 or over adds 1 to the esteem candidates on the jobs
    ///                board feel, 2 at 80; a firm's office over 50 weighs its bids up to 2% better.
    /// </summary>
    public static class InteriorRules
    {
        public const int Categories = 7;
        public static readonly string[] Ids = { "art", "greenery", "lighting", "audio", "floor", "furniture", "equipment" };

        // ---- decor
        public static readonly int[] ItemPoints = { 10, 10, 8, 12, 10, 4, 2 };          // tenths of a point, an item kind's first copy
        public static readonly int[] CapShare = { 300, 300, 300, 150, 200, 150, 100 };  // thousandths of the target a category may fill
        public const int RepeatShare = 600;           // thousandths each further copy of a kind counts of the one before
        public const int RepeatMost = 5;              // copies of a kind counted at most
        public const int TargetLeast = 12;            // points a place of up to TargetFromSqm needs
        public const int TargetFromSqm = 24;
        public const int TargetSqmPerPoint = 12;      // one point more for each this many square metres
        public const int TargetMost = 90;
        public const int VarietyFrom = 3;             // different kinds of decor item before variety counts
        public const int VarietyMost = 8;             // points variety adds at most

        // ---- wear and dirt
        public const int WearDay = 3;                 // thousandths a day
        public const int WearDirtMost = 4;            // thousandths more a day on a floor as dirty as it gets
        public const int WearMost = 1000;
        public const int WearPenaltyMost = 200;       // thousandths a score loses at full wear
        public const int DirtFrom = 70;               // cleanliness under this costs the scores...
        public const int DirtPenaltyMost = 150;       // ...up to this many thousandths at nothing clean
        public const int RedecorateRise = 10;         // the game's interior score rising this much is a redecoration...
        public const int RedecorateShare = 500;       // ...which keeps this share of the wear
        public const int RefreshPerSqm = 2;           // dollars a square metre to refresh the finishes...
        public const int RefreshLeast = 150, RefreshMost = 5000;   // ...within these
        public const int WornFrom = 600;              // wear an inspector notices...
        public const int WornInspection = 5;          // ...as this many points off the cleanliness they judge
        public const int InteriorShare = 600;         // thousandths of the overall figure that is the interior score

        // ---- the district's standard
        public const int IncomeLeast = 60, IncomeMiddle = 100, IncomeMost = 140;
        public const int StandardLeast = 15, StandardMost = 70;

        // ---- effects
        public const int ShortPointsPerPercent = 4, ShortFootfallMost = 8;
        public const int OverPointsPerPercent = 8, OverFootfallMost = 3;
        public const int DecorFootfallMost = 5;
        public const int FootfallLeast = -8, FootfallMost = 8;
        public const int BearsOverMost = 30, BearsUnderMost = 20;      // thousandths of the price
        public const int ComplaintFrom = 10, ComplaintPerPoint = 4, ComplaintMost = 120;   // per mille a day
        public const int BasketPerPoint = 2, BasketMost = 60;          // per mille a customer
        public const int BrandMiddle = 50, BrandPointsPer = 15, BrandMost = 3;
        public const int StandingMiddle = 50, StandingPointsPer = 20, StandingMost = 2;
        public const int ComfortMost = 5, ComfortMoralePerPoint = 8;
        public const int PlantsForTwo = 3;
        public const int OfficeOne = 60, OfficeTwo = 80;
        public const int BidFrom = 50, BidMost = 20;                   // thousandths of a bid's weight
        public const int StrikingFrom = 85;           // an overall figure that makes news...
        public const int ShabbyWear = 800;            // ...and wear that does, or an overall figure this far under the standard
        public const int ShabbyShort = 25;
        public const int StoryRestDays = 60;          // a business is in the news for its interior once in this many days at most
        public const int StoryChance = 100;           // per mille a day, once it is newsworthy

        public static int Clamp(int v, int least, int most)
        {
            return v < least ? least : v > most ? most : v;
        }

        // ---------------------------------------------------------------- decor

        /// <summary>Tenths of a point for so many copies of one item kind of a category: each copy 60% of the one before, five at most.</summary>
        public static int KindPoints(DecorCategory category, int copies)
        {
            int first = ItemPoints[(int)category] * 1000;
            long sum = 0, step = first;
            for (int i = 0; i < Math.Min(copies, RepeatMost); i++)
            {
                sum += step;
                step = step * RepeatShare / 1000;
            }
            return (int)((sum + 500) / 1000);
        }

        /// <summary>The points, tenths, a place of so many square metres needs for a decor score of 100.</summary>
        public static int Target(int sqm)
        {
            int points = TargetLeast + Math.Max(0, sqm - TargetFromSqm) / TargetSqmPerPoint;
            return Clamp(points, TargetLeast, TargetMost) * 10;
        }

        /// <summary>A category's points after its cap, tenths.</summary>
        public static int Capped(DecorCategory category, int points, int target)
        {
            return Clamp(points, 0, (int)((long)target * CapShare[(int)category] / 1000));
        }

        /// <summary>The points variety adds: one for each different kind of decor item over three, eight at most.</summary>
        public static int Variety(int kinds)
        {
            return Clamp(kinds - VarietyFrom, 0, VarietyMost);
        }

        /// <summary>The decor score, 0 to 100, from the categories' points after their caps, the target and the kinds of item.</summary>
        public static int DecorScore(int[] capped, int target, int kinds)
        {
            long sum = 0;
            for (int i = 0; i < Categories && capped != null && i < capped.Length; i++)
            {
                sum += Math.Max(0, capped[i]);
            }
            int score = target > 0 ? (int)((sum * 100 + target / 2) / target) : 0;
            return Clamp(score + (sum > 0 ? Variety(kinds) : 0), 0, 100);
        }

        // ---------------------------------------------------------------- wear and dirt

        /// <summary>Thousandths of wear a day adds: the finishes' age, more on a dirty floor, half while the cleaning service looks after it.</summary>
        public static int WearToday(int clean, bool cleaned)
        {
            int wear = WearDay + (100 - Clamp(clean, 0, 100)) * WearDirtMost / 100;
            return cleaned ? (wear + 1) / 2 : wear;
        }

        /// <summary>The wear after a day, never over the most.</summary>
        public static int Age(int wear, int clean, bool cleaned)
        {
            return Clamp(wear + WearToday(clean, cleaned), 0, WearMost);
        }

        /// <summary>The wear after a redecoration is seen (the game's interior score rose by <see cref="RedecorateRise"/> or more since it was last read).</summary>
        public static int Redecorated(int wear, int before, int now)
        {
            return before >= 0 && now - before >= RedecorateRise ? wear * RedecorateShare / 1000 : wear;
        }

        /// <summary>What a refresh of the finishes costs, whole dollars, by floor area, before the price level.</summary>
        public static int RefreshCost(int sqm)
        {
            return Clamp(Math.Max(0, sqm) * RefreshPerSqm, RefreshLeast, RefreshMost);
        }

        /// <summary>A score after wear and dirt: up to 20% off at full wear, up to 15% off for cleanliness under 70.</summary>
        public static int Effective(int score, int wear, int clean)
        {
            int off = Clamp(wear, 0, WearMost) * WearPenaltyMost / WearMost;
            int c = Clamp(clean, 0, 100);
            if (c < DirtFrom)
            {
                off += (DirtFrom - c) * DirtPenaltyMost / DirtFrom;
            }
            return Clamp((int)(((long)Clamp(score, 0, 100) * (1000 - off) + 500) / 1000), 0, 100);
        }

        /// <summary>The overall figure: six tenths interior, four tenths decor.</summary>
        public static int Overall(int interior, int decor)
        {
            return Clamp((Clamp(interior, 0, 100) * InteriorShare + Clamp(decor, 0, 100) * (1000 - InteriorShare) + 500) / 1000, 0, 100);
        }

        /// <summary>The cleanliness an inspector judges: a worn place looks dirtier than it is.</summary>
        public static int InspectedClean(int clean, int wear)
        {
            return wear >= WornFrom ? Math.Max(0, clean - WornInspection) : clean;
        }

        // ---------------------------------------------------------------- the district

        /// <summary>The standard a district expects, from its income level: 15 at 60 up to 70 at 140, never under the game's own minimum there.</summary>
        public static int Standard(int income, int gameMinimum)
        {
            int i = Clamp(income, IncomeLeast, IncomeMost);
            int standard = StandardLeast + (i - IncomeLeast) * (StandardMost - StandardLeast) / (IncomeMost - IncomeLeast);
            return Clamp(Math.Max(standard, Clamp(gameMinimum, 0, 100)), 0, 100);
        }

        // ---------------------------------------------------------------- effects

        /// <summary>The standard's part of footfall, per cent: short of it 1% for each 4 points (8 at most), over it 1% for each 8 (3 at most).</summary>
        public static int StandardFootfall(int overall, int standard)
        {
            int gap = overall - standard;
            if (gap < 0)
            {
                return -Math.Min(ShortFootfallMost, (-gap + ShortPointsPerPercent - 1) / ShortPointsPerPercent);
            }
            return Math.Min(OverFootfallMost, gap / OverPointsPerPercent);
        }

        /// <summary>The decor's part of footfall, per cent: up to 5 at a decor score of 100.</summary>
        public static int DecorFootfall(int decor)
        {
            return Clamp(decor, 0, 100) * DecorFootfallMost / 100;
        }

        /// <summary>The interior's footfall, per cent: the parts that are on, together, inside -8 to +8.</summary>
        public static int Footfall(int standardPart, int decorPart)
        {
            return Clamp(standardPart + decorPart, FootfallLeast, FootfallMost);
        }

        /// <summary>Thousandths more (or less) of the price the district bears at this business: 1 a point over the standard (30 at most), 1 a point under (20 at most).</summary>
        public static int Bears(int overall, int standard)
        {
            int gap = overall - standard;
            return gap >= 0 ? Math.Min(BearsOverMost, gap) : -Math.Min(BearsUnderMost, -gap);
        }

        /// <summary>A tolerance with the interior's part on it, thousandths.</summary>
        public static int Tolerance(int tolerance, int bears)
        {
            return (int)((long)tolerance * (1000 + Clamp(bears, -BearsUnderMost, BearsOverMost)) / 1000);
        }

        /// <summary>The chance a complaint about the place starts today, per mille: nothing until 10 short of the standard, then 4 a point, 120 at most.</summary>
        public static int ComplaintChance(int overall, int standard)
        {
            int under = standard - overall;
            return under < ComplaintFrom ? 0 : Math.Min(ComplaintMost, under * ComplaintPerPoint);
        }

        /// <summary>The chance a customer takes one more product, per mille: 2 a point over the standard, 60 at most.</summary>
        public static int BasketChance(int overall, int standard)
        {
            return Clamp((overall - standard) * BasketPerPoint, 0, BasketMost);
        }

        /// <summary>Points a shop of a brand counts more or less in its brand's standards: one for each 15 over or under 50, three at most.</summary>
        public static int BrandPoints(int overall)
        {
            return Clamp((overall - BrandMiddle) / BrandPointsPer, -BrandMost, BrandMost);
        }

        /// <summary>Points a shop's figure in the customers' reputation moves: one for each 20 over or under 50, two at most.</summary>
        public static int StandingPoints(int overall)
        {
            return Clamp((overall - StandingMiddle) / StandingPointsPer, -StandingMost, StandingMost);
        }

        /// <summary>Staff comfort, 0 to 5: plants (one, two with three or more), good light (the lighting at half its share or more), a rest corner (two).</summary>
        public static int Comfort(int plants, int lightingCapped, int target, bool restCorner)
        {
            int comfort = plants >= PlantsForTwo ? 2 : plants > 0 ? 1 : 0;
            if (target > 0 && lightingCapped * 2 >= (int)((long)target * CapShare[(int)DecorCategory.Lighting] / 1000))
            {
                comfort++;
            }
            if (restCorner)
            {
                comfort += 2;
            }
            return Clamp(comfort, 0, ComfortMost);
        }

        /// <summary>The level staff paid fairly settle at, with the comfort of their workplace on it.</summary>
        public static int Calm(int calm, int comfort)
        {
            return calm + Clamp(comfort, 0, ComfortMost);
        }

        /// <summary>Thousandths a professional's morale heads higher for the comfort of their workplace.</summary>
        public static int Morale(int comfort)
        {
            return Clamp(comfort, 0, ComfortMost) * ComfortMoralePerPoint;
        }

        /// <summary>What the best office adds to the esteem candidates feel: 1 at 60, 2 at 80.</summary>
        public static int OfficeEsteem(int bestOffice)
        {
            return bestOffice >= OfficeTwo ? 2 : bestOffice >= OfficeOne ? 1 : 0;
        }

        /// <summary>Thousandths a firm's bids weigh better for its office: nothing to 50, then up to 20 at 100.</summary>
        public static int OfficeBid(int overall)
        {
            return Clamp((overall - BidFrom) * BidMost / (100 - BidFrom), 0, BidMost);
        }

        /// <summary>A bid's weighed figure with the office's part: lower is better.</summary>
        public static long Weighed(long bid, int reputation, int office)
        {
            return bid * (1500 - reputation - Clamp(office, 0, BidMost)) / 1000;
        }

        /// <summary>1 for a striking interior, -1 for a shabby one, 0 for neither.</summary>
        public static int Newsworthy(int overall, int standard, int wear, bool standardOn)
        {
            if (overall >= StrikingFrom && wear < WornFrom)
            {
                return 1;
            }
            if (wear >= ShabbyWear || (standardOn && standard - overall >= ShabbyShort))
            {
                return -1;
            }
            return 0;
        }

        /// <summary>True if a newsworthy business may be in the news today: rested since the last time, and the day's draw under the chance.</summary>
        public static bool Story(int lastStory, int day, int draw)
        {
            return day - lastStory >= StoryRestDays && draw < StoryChance;
        }

        // ---------------------------------------------------------------- living with the two other mods

        /// <summary>The district standard's part (its footfall and its complaints): its switch on, and not while the other author's interiors mod is.</summary>
        public static bool StandardOn(bool option, bool interiorsModLoaded)
        {
            return option && !interiorsModLoaded;
        }

        /// <summary>The basket: its switch on, and not while the other author's interiors mod is (it adds to baskets itself).</summary>
        public static bool BasketOn(bool option, bool interiorsModLoaded)
        {
            return option && !interiorsModLoaded;
        }

        /// <summary>The decor's footfall: its switch on, and not while the other author's ambience mod is (it adds customers for decor itself).</summary>
        public static bool DecorFootfallOn(bool option, bool ambienceModLoaded)
        {
            return option && !ambienceModLoaded;
        }

        // ---------------------------------------------------------------- one business, worked out

        /// <summary>
        /// Every figure of a business from what was read and its wear, with the parts that are on. The kind decides what
        /// applies: an office gets no customers' parts, a place of neither kind nothing at all.
        /// </summary>
        public static void Work(InteriorShop s, int wear, bool standardOn, bool decorOn, bool pricesOn, bool basketOn, bool standingOn, bool staffOn)
        {
            s.Wear = Clamp(wear, 0, WearMost);
            s.Target = Target(s.Sqm);
            for (int i = 0; i < Categories; i++)
            {
                s.Capped[i] = Capped((DecorCategory)i, s.Points[i], s.Target);
            }
            s.DecorRaw = DecorScore(s.Capped, s.Target, s.Kinds);
            s.Decor = Effective(s.DecorRaw, s.Wear, s.Clean);
            s.InteriorNow = Effective(s.Interior, s.Wear, s.Clean);
            s.Overall = Overall(s.InteriorNow, s.Decor);
            s.Standard = Standard(s.Income, s.GameMinimum);
            bool shop = s.Kind == InteriorKind.Shop;
            s.StandardFootfall = shop && standardOn ? StandardFootfall(s.Overall, s.Standard) : 0;
            s.DecorFootfall = shop && decorOn ? DecorFootfall(s.Decor) : 0;
            s.Footfall = Footfall(s.StandardFootfall, s.DecorFootfall);
            s.Bears = shop && pricesOn ? Bears(s.Overall, s.Standard) : 0;
            s.ComplaintChance = shop && standardOn ? ComplaintChance(s.Overall, s.Standard) : 0;
            s.Basket = shop && basketOn ? BasketChance(s.Overall, s.Standard) : 0;
            s.BrandPoints = shop && standingOn ? BrandPoints(s.Overall) : 0;
            s.StandingPoints = shop && standingOn ? StandingPoints(s.Overall) : 0;
            s.Comfort = s.Kind != InteriorKind.None && staffOn
                ? Comfort(s.Items[(int)DecorCategory.Greenery], s.Capped[(int)DecorCategory.Lighting], s.Target, s.RestCorner) : 0;
            s.OfficeEsteem = s.Kind == InteriorKind.Office && staffOn ? OfficeEsteem(s.Overall) : 0;
            s.OfficeBid = s.Kind != InteriorKind.None && staffOn ? OfficeBid(s.Overall) : 0;
        }

        /// <summary>The category to add to next: the one furthest under its cap, decor before furniture and equipment; -1 when every one is full.</summary>
        public static int Next(InteriorShop s)
        {
            int best = -1, room = 0;
            for (int i = 0; i < (int)DecorCategory.Furniture; i++)
            {
                int cap = (int)((long)s.Target * CapShare[i] / 1000);
                int left = cap - s.Capped[i];
                if (left > room)
                {
                    room = left;
                    best = i;
                }
            }
            return best;
        }
    }
}
