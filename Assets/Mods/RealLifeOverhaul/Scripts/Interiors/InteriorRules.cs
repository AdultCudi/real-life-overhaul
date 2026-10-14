using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Why a business sells nothing just now, so that its interior brings it nobody extra (a shortfall of the standard
    /// still costs it, as it costs the customers who do come). Shown in the Interior tab; not saved.
    /// </summary>
    public enum SaleStop
    {
        None = 0,
        Closed = 1,      // closed by its switch, or not open at all today
        Empty = 2,       // nothing to sell: no product on its shelves, no service, no entrance
        NoStaff = 3,     // open, and nobody on shift at a station that needs somebody
        Prices = 4       // every one of its last customers turned its prices down
    }

    /// <summary>One kind of placed item that counts: its name, its category, how many are placed and the points they make (tenths, before the category's cap).</summary>
    public struct InteriorItem
    {
        public string Name;
        public DecorCategory Category;
        public int Copies;
        public int Points;
    }

    /// <summary>
    /// Today's customers of a business by where they came from, as the game counts them (InteriorRules.Split): its own
    /// street traffic, its marketing, its interior and decor, the rest of this mod, and another author's mod.
    /// </summary>
    public struct CustomerSplit
    {
        public int Total;       // every customer of today's list, those served and those to come
        public int Game;        // the street's traffic and the game's own draw
        public int Marketing;   // the business's marketing
        public int Interiors;   // its interior and decor (an office's clients booked for it included); less than nothing when it falls short
        public int Others;      // the rest of this mod: the city's economy, the company, brands, Flare, Standing and events
        public int OtherMod;    // Meaningful Ambience's, on top of the game's draw
    }

    /// <summary>What sort of place the business is, for what its interior can do.</summary>
    public enum InteriorKind
    {
        None = 0,        // not one the feature looks at (a warehouse, a factory, an empty unit)
        Shop = 1,        // customers come in: footfall, prices, the basket
        Office = 2       // a headquarters or an office: staff, hiring and client wins only
    }

    /// <summary>
    /// The parts of the feature that apply today and how strong each is, from the options and the two other authors'
    /// mods (<see cref="InteriorRules.Parts"/>). A strength is thousandths (1000: the figures as the rules give them, the
    /// slider at 100%) or, for the decor, the per mille of customers it brings at a decor score of 100; nothing is off.
    /// </summary>
    public struct InteriorParts
    {
        public int StandardScale;   // the district's standard: its customers either way, and its complaints
        public int DecorMille;      // customers decor brings at a decor score of 100, per mille (100: 10%)
        public int PriceScale;      // what a nicer place lets a shop charge: the price the district bears over the standard, and the basket's share
        public int WearScale;       // how fast the finishes wear
        public bool Basket;         // the basket's own switch
        public bool Standing;       // brands, Standing and the news
        public bool Staff;          // staff comfort, the jobs board and bids
    }

    /// <summary>One business as the feature saw it this morning: what was read, and every figure worked out from it. Not saved.</summary>
    public sealed class InteriorShop
    {
        public string Place = string.Empty;
        public string Name = string.Empty;
        public string District = string.Empty;
        public InteriorKind Kind;

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
        public bool Fitting;                    // still in its first days: the district does not hold it to its standard yet
        public int StandardFrom;                // ...and the day from which it does
        public SaleStop Stop;                   // why it sells nothing just now (looked at once a game hour), or None
        public readonly List<InteriorItem> Placed = new List<InteriorItem>(16);   // every kind of placed item that counts

        // worked out
        public int Target;                      // the points a place of this size needs for full marks, tenths
        public readonly int[] Capped = new int[InteriorRules.Categories];   // each category's points after its cap, tenths
        public int DecorRaw, Decor, InteriorNow, Overall, Standard;
        public int Wear;                        // thousandths, 0 fresh to 1000 worn out (the file's)
        public int StandardFootfall, DecorFootfall, Footfall, Bears, Basket, BrandPoints, StandingPoints, Comfort, OfficeEsteem, OfficeBid;
        public int OfficeBoost;                 // thousandths more clients an office is booked, an hour ahead
        public int ComplaintChance;             // per mille today

        // phasing in and the bound (set by the director before Work; these defaults are the rules in full)
        public int Ramp = 1000;                 // thousandths of every effect on customers and takings that apply today (InteriorRules.Ramp)
        public int PhaseFrom;                   // the day it began to come in
        public int IssueDip;                    // business pressure's own change of the shop's customers now, per cent (a complaint's -6)
        public int StandardFull, DecorFull;     // the two parts of footfall in full, before phasing in and giving way
        public int Bound;                       // the most the shortfall may cost today, per cent (6 at the default strength)
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
        public int FirstDay = Unset;   // the first day it was read (its fitting-out days count from it)

        public const int Unset = int.MinValue;
        public const int LongAgo = -1000;   // a business already trading when the feature first read it
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
    /// income level, and never under the game's own minimum for it (Midtown's). A business gets its first 14 days to fit
    /// out: falling short of the standard costs it nothing then (customers, complaints), being over it counts.
    ///
    /// THE STRENGTHS: four sliders of the options, each from 0% (off) to 200% (double) of the figures below, 100% by
    /// default: the district standard (its customers either way and its complaints), the decor's customers (and an
    /// office's clients), what a nicer interior lets a shop charge (the price the district bears and the basket), and
    /// wear.
    ///
    /// THE BOUND, so that nothing of the feature's can cut an established business's takings sharply: at the defaults
    /// the interior can cost it at most 6% of its customers, and nothing of its prices or its baskets, which only ever
    /// gain. A complaint it brings (business pressure's own, 6% for 3 days) counts inside the same 6%: while any dip of
    /// business pressure's costs the shop customers, the shortfall gives way so that the two together never take more
    /// than the bound (<see cref="GiveWay"/>); and under the default strength, where the bound is smaller than a
    /// complaint, the interior brings no complaints at all. 6% because it is what one complaint already costs, under a
    /// normal day's swing in the game's own hourly figures, and well under what good decor brings back (10%). At double
    /// strength the bound is 12%.
    ///
    /// PHASED IN: every effect on customers and takings comes in over <see cref="RampDays"/> days (a seventh more each
    /// day) from the day these rules first read a business: for a save that meets them, from that day; for a business
    /// opened afterwards, from its own first day (<see cref="Ramp"/>). Nothing changes a business's takings overnight.
    ///
    /// THE EFFECTS, each inside its cap, at the default strengths:
    ///   standard     short of it: 1% fewer customers for each 4 points short (rounded down), 6% at most, and a
    ///                complaint more likely (4 per mille a day for each point short from 10 short, 120 at most); over
    ///                it: 1% more for each 8 points, 3% at most. Never no customers.
    ///   decor        10% more customers at a decor score of 100 (20% at double).
    ///   footfall     the two together, -12% to +24% at the strongest: a part of business pressure's footfall of its
    ///                own (BusinessCatalogue.ClampInterior), beside what the rest of the mod adds. While the business
    ///                sells nothing (closed, nothing on its shelves, nobody on shift, its last 8 customers all turning
    ///                its prices down) it brings nobody extra and adds nothing to a basket; a shortfall still costs.
    ///   prices       over the standard the district bears 1 thousandth more of the price for each point, 3% at
    ///                most; under it nothing less.
    ///   basket       over the standard, each customer takes more of what the shop sells and has in stock: their
    ///                order's products (never the door's entrance fee) times a share that rises from nothing at the
    ///                standard to the district's most at an overall figure of 85: 10% where the standard is lowest,
    ///                6% where it is highest. The whole part for certain, the rest as a chance; an office's client
    ///                one at most. As Meaningful Interiors does; the prices' strength sizes it.
    ///   brand        a shop of a brand counts up to 3 points more or less in its brand's standards.
    ///   standing     a shop's figure in the customers' reputation moves up to 2 points either way.
    ///   staff        comfort 0 to 5: plants (1, or 2 with three or more), good light (1), a rest corner (2):
    ///                the calm level contented staff settle at goes up by the comfort, and a professional's morale
    ///                heads 8 thousandths higher for each point.
    ///   offices      the best office of the player's at 60 or over adds 1 to the esteem candidates on the jobs
    ///                board feel, 2 at 80; a firm's office over 50 weighs its bids up to 2% better. An office that takes
    ///                clients is booked more of them an hour ahead: the decor's strength at its overall score (10% at
    ///                100 by default, 20% at double), never more than its staffed workstations left free.
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

        // ---- effects (at the default strength)
        public const int ShortPointsPerPercent = 4, ShortFootfallMost = 6;   // the shortfall's most is the bound: see the class summary
        public const int OverPointsPerPercent = 8, OverFootfallMost = 3;
        public const int FootfallLeast = -12, FootfallMost = 24;      // the standard short of it at double; the decor and the standard over it at double, kept to 24
        public const int BearsOverMost = 30;                           // thousandths of the price over the standard; under it, nothing less
        public const int DecorMille = 100;                             // per mille more customers at a decor score of 100 (10%)
        public const int ComplaintFrom = 10, ComplaintPerPoint = 4, ComplaintMost = 120;   // per mille a day
        public const int ComplaintFromStrength = 1000;                 // the standard's strength under which the interior brings no complaint (its bound is then under a complaint's 6%)
        public const int RampDays = 7;                                 // days over which every effect on customers and takings comes in
        public const int BasketFullAt = 85;                            // the overall figure at which the basket is at its district's most...
        public const int BasketMostPoor = 100, BasketMostRich = 60;    // ...per mille of an order's products: where the standard is lowest, where it is highest
        public const int BasketOfficeMost = 1;                         // products more an office's client takes at most
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
        public const int GraceDays = 14;              // a new business's days to fit out before its district holds it to the standard
        public const int RecentOrders = 8;            // the last completed orders looked at for prices turned down, and no fewer
        public const int OfficeBoostMost = 200;       // thousandths more clients at an overall score of 100 and the decor's strongest choice

        // ---- the strengths: the options' four sliders, per cent of the figures above
        public const int StrengthLeast = 0, StrengthMost = 200, StrengthDefault = 100;

        // The drop-downs the sliders replaced (0.8.2 to 0.9.10): each choice's slider figure, in the order of its
        // drop-down, so that a saved choice is kept (Core/Settings). The reward over the standard had a drop-down of its
        // own; it is now the standard's strength (its customers) and the prices' (the basket).
        public static readonly int[] StandardWas = { 0, 50, 100, 150 };          // Off, Gentle, Standard, Strict
        public static readonly int[] DecorWas = { 0, 50, 100, 150, 200 };        // Off, 5%, 10%, 15%, 20%
        public static readonly int[] PricesWas = { 0, 50, 75, 100, 125 };        // Off, Low, Conservative, Standard, Generous
        public static readonly int[] WearWas = { 0, 50, 100, 150 };              // Off, Slow, Standard, Fast

        public static int Clamp(int v, int least, int most)
        {
            return v < least ? least : v > most ? most : v;
        }

        /// <summary>A slider's per cent as a strength in thousandths, kept inside 0% to 200%.</summary>
        public static int Strength(int percent)
        {
            return Clamp(percent, StrengthLeast, StrengthMost) * 10;
        }

        /// <summary>A drop-down's saved choice as the slider's per cent that replaced it, kept inside its choices.</summary>
        public static int Was(int[] choices, int index)
        {
            return choices[Clamp(index, 0, choices.Length - 1)];
        }

        /// <summary>A figure at a strength, thousandths, rounded half away from nothing.</summary>
        public static int Scale(int value, int mille)
        {
            long v = (long)value * Math.Max(0, mille);
            return (int)(v >= 0 ? (v + 500) / 1000 : (v - 500) / 1000);
        }

        /// <summary>
        /// The parts that apply, from the options' four sliders (per cent, 0 to 200) and the two other authors' mods: with
        /// Meaningful Interiors on, the district standard (both ways) and the basket are left to it; with Meaningful
        /// Ambience on, the customers decor brings. The rest keeps its strength.
        /// </summary>
        public static InteriorParts Parts(int standard, int decor, int prices, int wear, bool basket, bool standing, bool staff, bool interiorsMod, bool ambienceMod)
        {
            var p = new InteriorParts
            {
                StandardScale = interiorsMod ? 0 : Strength(standard),
                DecorMille = ambienceMod ? 0 : Strength(decor) * DecorMille / 1000,
                PriceScale = Strength(prices),
                WearScale = Strength(wear),
                Standing = standing,
                Staff = staff
            };
            p.Basket = basket && !interiorsMod && p.PriceScale > 0;
            return p;
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
            return WearToday(clean, cleaned, 1000);
        }

        /// <summary>The same at a strength of the wear option, thousandths (Slow is half, Off nothing).</summary>
        public static int WearToday(int clean, bool cleaned, int scale)
        {
            int wear = WearDay + (100 - Clamp(clean, 0, 100)) * WearDirtMost / 100;
            return Scale(cleaned ? (wear + 1) / 2 : wear, scale);
        }

        /// <summary>The wear after a day, never over the most.</summary>
        public static int Age(int wear, int clean, bool cleaned)
        {
            return Age(wear, clean, cleaned, 1000);
        }

        /// <summary>The same at a strength of the wear option.</summary>
        public static int Age(int wear, int clean, bool cleaned, int scale)
        {
            return Clamp(wear + WearToday(clean, cleaned, scale), 0, WearMost);
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

        /// <summary>The standard's part of footfall, per cent: short of it 1% for each 4 points (rounded down, 6 at most), over it 1% for each 8 (3 at most).</summary>
        public static int StandardFootfall(int overall, int standard)
        {
            return StandardFootfall(overall, standard, 1000);
        }

        /// <summary>The same at the standard's strength, thousandths (both ways: 12% at most short of it at double).</summary>
        public static int StandardFootfall(int overall, int standard, int scale)
        {
            int gap = overall - standard;
            if (gap < 0)
            {
                return -Scale(Math.Min(ShortFootfallMost, -gap / ShortPointsPerPercent), scale);
            }
            return Scale(Math.Min(OverFootfallMost, gap / OverPointsPerPercent), scale);
        }

        /// <summary>The most falling short of the standard may cost at a strength, per cent: 6 at the default, 12 at double, nothing off.</summary>
        public static int Bound(int scale)
        {
            return Scale(ShortFootfallMost, scale);
        }

        /// <summary>The decor's part of footfall, per cent: so many per mille at a decor score of 100 (100, 10%, by default; 200 at double), rounded down.</summary>
        public static int DecorFootfall(int decor, int mille)
        {
            return Clamp(decor, 0, 100) * Clamp(mille, 0, Strength(StrengthMost) * DecorMille / 1000) / 1000;
        }

        /// <summary>The interior's footfall, per cent: the parts that are on, together, inside -12 to +24.</summary>
        public static int Footfall(int standardPart, int decorPart)
        {
            return Clamp(standardPart + decorPart, FootfallLeast, FootfallMost);
        }

        /// <summary>
        /// The shortfall's part beside a dip of business pressure's own at the shop (a complaint's -6% and the like, per
        /// cent): the two together never take more than the bound, unless the dip alone does (then the shortfall takes
        /// nothing). A part that gains, or no dip, is left as it is.
        /// </summary>
        public static int GiveWay(int standardPart, int issuePercent, int bound)
        {
            if (standardPart >= 0)
            {
                return standardPart;
            }
            int room = Math.Min(0, -Math.Max(0, bound) - Math.Min(0, issuePercent));
            return Math.Max(standardPart, room);
        }

        /// <summary>
        /// Thousandths of the effects on customers and takings that apply on a day, for a business they began to come in
        /// for on <paramref name="from"/>: a seventh on that day, a seventh more each day, in full on the seventh.
        /// In full for a business with no such day.
        /// </summary>
        public static int Ramp(int from, int day)
        {
            if (from == InteriorFile.Unset || from == InteriorFile.LongAgo)
            {
                return 1000;
            }
            return Clamp((int)(((long)day - from + 1) * 1000 / RampDays), 0, 1000);
        }

        /// <summary>The first day an effect applies in full, for a business it began to come in for on <paramref name="from"/>.</summary>
        public static int FullFrom(int from)
        {
            return from + RampDays - 1;
        }

        /// <summary>Thousandths more of the price the district bears at this business: 1 a point over the standard, 30 at most; under it nothing less.</summary>
        public static int Bears(int overall, int standard)
        {
            return Bears(overall, standard, 1000);
        }

        /// <summary>The same at the strength of the prices option, thousandths.</summary>
        public static int Bears(int overall, int standard, int scale)
        {
            int gap = overall - standard;
            return gap > 0 ? Scale(Math.Min(BearsOverMost, gap), scale) : 0;
        }

        /// <summary>The chance a complaint about the place starts today, per mille: nothing until 10 short of the standard, then 4 a point, 120 at most.</summary>
        public static int ComplaintChance(int overall, int standard)
        {
            return ComplaintChance(overall, standard, 1000);
        }

        /// <summary>The same at the strength of the standard, thousandths; none at all under the default strength, whose bound is smaller than a complaint.</summary>
        public static int ComplaintChance(int overall, int standard, int scale)
        {
            int under = standard - overall;
            return under < ComplaintFrom || scale < ComplaintFromStrength ? 0 : Scale(Math.Min(ComplaintMost, under * ComplaintPerPoint), scale);
        }

        /// <summary>The most the basket adds in a district, per mille of an order's products: 100 where the standard is lowest (15), 60 where it is highest (70).</summary>
        public static int BasketMost(int standard)
        {
            int s = Clamp(standard, StandardLeast, StandardMost);
            return BasketMostPoor - (s - StandardLeast) * (BasketMostPoor - BasketMostRich) / (StandardMost - StandardLeast);
        }

        /// <summary>
        /// The products more a customer takes, per mille of the products in their order: nothing at or under the standard,
        /// rising in step with the way from the standard to an overall figure of 85, where it is the district's most.
        /// </summary>
        public static int BasketShare(int overall, int standard)
        {
            return BasketShare(overall, standard, 1000);
        }

        /// <summary>The same at the strength of the prices option, thousandths.</summary>
        public static int BasketShare(int overall, int standard, int scale)
        {
            int gap = Clamp(overall, 0, 100) - standard;
            if (gap <= 0)
            {
                return 0;
            }
            int most = BasketMost(standard), span = BasketFullAt - standard;
            return Scale(span <= 0 ? most : most * Math.Min(gap, span) / span, scale);
        }

        /// <summary>
        /// The products one customer takes more: the products in their order (never the door's entrance fee) times the
        /// share, the whole part for certain and one more with the rest as its chance (draw 0 to 999). An office's client
        /// one at most.
        /// </summary>
        public static int BasketExtra(int products, int share, int draw, bool office)
        {
            long total = (long)Math.Max(0, products) * Clamp(share, 0, 1000);
            int n = (int)(total / 1000);
            if (draw < total % 1000)
            {
                n++;
            }
            return office ? Math.Min(n, BasketOfficeMost) : n;
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

        /// <summary>True while a business first read on <paramref name="firstDay"/> is still fitting out: the standard's shortfall does not count yet.</summary>
        public static bool Fitting(int firstDay, int day)
        {
            return firstDay != InteriorFile.Unset && day - firstDay < GraceDays;
        }

        // ---------------------------------------------------------------- no customers wasted

        /// <summary>
        /// Why a business sells nothing now, from the first that holds: closed, nothing to sell, nobody on shift, every one
        /// of its last <see cref="RecentOrders"/> customers (and no fewer) turning its prices down. None when it can sell:
        /// once something is on the shelves, somebody on shift, or one customer of the last few bought, it starts again.
        /// </summary>
        public static SaleStop Stop(bool closed, bool stocked, bool staffed, int recent, int refused)
        {
            if (closed)
            {
                return SaleStop.Closed;
            }
            if (!stocked)
            {
                return SaleStop.Empty;
            }
            if (!staffed)
            {
                return SaleStop.NoStaff;
            }
            return recent >= RecentOrders && refused >= recent ? SaleStop.Prices : SaleStop.None;
        }

        // ---------------------------------------------------------------- where today's customers came from

        /// <summary>
        /// Today's customers by where they came from. The game draws a business's customers by its promotion figure: a
        /// base, and three quarters of the figure in hundredths (CustomerEntriesCalculator.GetPromotionMultiplier). The
        /// figure is its street's traffic and its marketing (BusinessHelper.UpdatePromotion: <paramref name="own"/>), and
        /// this mod writes it again with its parts on top (<paramref name="written"/>): the interior's per cent, and the
        /// rest's (an issue's change and the ambient sum). <paramref name="drawn"/> customers of the game's own draw are
        /// shared out by those parts of the multiplier (a part can be less than nothing), what the written figure lost at
        /// its top of 100 shared by the parts' sizes; <paramref name="booked"/> (an office's clients booked by the
        /// interior) and <paramref name="otherMod"/> (another mod's, over the game's draw) are added as they are. The
        /// shares add up to the total exactly.
        /// </summary>
        public static CustomerSplit Split(int drawn, int booked, int otherMod, int baseMille, int traffic, int own, int written, int interiorPercent, int otherPercent)
        {
            var split = new CustomerSplit { Total = Math.Max(0, drawn) + Math.Max(0, booked) + Math.Max(0, otherMod), OtherMod = Math.Max(0, otherMod) };
            drawn = Math.Max(0, drawn);
            own = Clamp(own, 0, 100);
            written = Clamp(written, 0, 100);
            long baseTen = 10L * Math.Max(0, baseMille);
            long game = baseTen + 75L * Clamp(traffic, 0, own);
            long ownM = baseTen + 75L * own;
            long all = baseTen + 75L * written;
            long change = all - ownM;
            long interiorRaw = ownM * interiorPercent / 100, otherRaw = ownM * otherPercent / 100;
            long interior = interiorRaw + otherRaw != 0 && Math.Sign(interiorRaw + otherRaw) == Math.Sign(change)
                ? change * interiorRaw / (interiorRaw + otherRaw) : interiorRaw;
            long others = change - interior;
            long marketing = ownM - game;
            if (all <= 0 || drawn == 0)
            {
                split.Game = drawn;
            }
            else
            {
                split.Marketing = (int)Rounded(drawn * marketing, all);
                split.Interiors = (int)Rounded(drawn * interior, all);
                split.Others = (int)Rounded(drawn * others, all);
                split.Game = drawn - split.Marketing - split.Interiors - split.Others;
            }
            split.Interiors += Math.Max(0, booked);
            return split;
        }

        private static long Rounded(long value, long by)
        {
            return value >= 0 ? (value + by / 2) / by : -((-value + by / 2) / by);
        }

        // ---------------------------------------------------------------- an office's clients

        /// <summary>Thousandths more clients an office gets: the decor's per mille (100, 10%, by default; 200 at double) at its overall score.</summary>
        public static int OfficeBoost(int overall, int decorMille)
        {
            return Clamp(Clamp(overall, 0, 100) * Clamp(decorMille, 0, OfficeBoostMost) / 100, 0, OfficeBoostMost);
        }

        /// <summary>
        /// How many clients more an office is booked for the coming hour: its boost on the clients the game has booked it
        /// (thousandths, the fraction carried to the next hour), never more than its staffed workstations left free then.
        /// What could not be booked for want of a free station is not carried.
        /// </summary>
        public static int OfficeClients(int gameClients, int free, int boostMille, ref int carryMille)
        {
            long total = (long)Math.Max(0, gameClients) * Clamp(boostMille, 0, OfficeBoostMost) + Clamp(carryMille, 0, 999);
            int n = (int)(total / 1000);
            carryMille = (int)(total - (long)n * 1000);
            if (n > free)
            {
                n = Math.Max(0, free);
                carryMille = 0;
            }
            return n;
        }

        // ---------------------------------------------------------------- one business, worked out

        /// <summary>
        /// Every figure of a business from what was read and its wear, with the parts that are on. The kind decides what
        /// applies: an office gets no customers' parts, a place of neither kind nothing at all. Its effects on customers
        /// and takings are those of its day of phasing in (<see cref="InteriorShop.Ramp"/>), and its shortfall gives way
        /// to a dip of business pressure's at it (<see cref="InteriorShop.IssueDip"/>).
        /// </summary>
        public static void Work(InteriorShop s, int wear, InteriorParts p)
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
            s.Ramp = Clamp(s.Ramp, 0, 1000);
            s.Bound = shop ? Bound(p.StandardScale) : 0;
            s.StandardFull = shop ? StandardFootfall(s.Overall, s.Standard, p.StandardScale) : 0;
            if (s.Fitting)
            {
                s.StandardFull = Math.Max(0, s.StandardFull);   // fitting out: over the standard counts, short of it not yet
            }
            s.DecorFull = shop ? DecorFootfall(s.Decor, p.DecorMille) : 0;
            if (s.Stop != SaleStop.None)
            {
                s.StandardFull = Math.Min(0, s.StandardFull);   // selling nothing: nobody extra, the shortfall's cost stays
                s.DecorFull = 0;
            }
            Combine(s);
            s.Bears = shop ? Scale(Bears(s.Overall, s.Standard, p.PriceScale), s.Ramp) : 0;
            s.ComplaintChance = shop && !s.Fitting && s.Ramp >= 1000 ? ComplaintChance(s.Overall, s.Standard, p.StandardScale) : 0;
            s.Basket = shop && p.Basket && s.Stop == SaleStop.None ? Scale(BasketShare(s.Overall, s.Standard, p.PriceScale), s.Ramp) : 0;
            s.BrandPoints = shop && p.Standing ? BrandPoints(s.Overall) : 0;
            s.StandingPoints = shop && p.Standing ? StandingPoints(s.Overall) : 0;
            s.Comfort = s.Kind != InteriorKind.None && p.Staff
                ? Comfort(s.Items[(int)DecorCategory.Greenery], s.Capped[(int)DecorCategory.Lighting], s.Target, s.RestCorner) : 0;
            s.OfficeEsteem = s.Kind == InteriorKind.Office && p.Staff ? OfficeEsteem(s.Overall) : 0;
            s.OfficeBoost = s.Kind == InteriorKind.Office && s.Stop == SaleStop.None ? Scale(OfficeBoost(s.Overall, p.DecorMille), s.Ramp) : 0;
            s.OfficeBid = s.Kind != InteriorKind.None && p.Staff ? OfficeBid(s.Overall) : 0;
        }

        /// <summary>
        /// The footfall of a worked business from its two parts in full, its day of phasing in and business pressure's
        /// dip at it now: what the director asks again when that dip changes (a complaint starts or ends), without
        /// reading the business again.
        /// </summary>
        public static void Combine(InteriorShop s)
        {
            s.StandardFootfall = GiveWay(Scale(s.StandardFull, s.Ramp), s.IssueDip, s.Bound);
            s.DecorFootfall = Scale(s.DecorFull, s.Ramp);
            s.Footfall = Footfall(s.StandardFootfall, s.DecorFootfall);
        }

        /// <summary>What the interior does to a shop's takings today, thousandths either way: its customers and its customers' baskets together (prices are not changed, only what the district bears).</summary>
        public static int Takings(int footfall, int basket)
        {
            long t = (100L + Clamp(footfall, FootfallLeast, FootfallMost)) * (1000L + Clamp(basket, 0, 1000)) / 100 - 1000;
            return (int)t;
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
