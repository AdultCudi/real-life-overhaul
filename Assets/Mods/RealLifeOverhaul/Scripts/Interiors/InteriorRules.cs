using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>The kinds of placed item the decor score counts, from the game's own item tags and types; the order is the tab's.</summary>
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

    /// <summary>Why a business sells nothing just now, so its interior brings nobody extra (a shortfall still costs). Not saved.</summary>
    public enum SaleStop
    {
        None = 0,
        Closed = 1,      // closed by its switch, or not open at all today
        Empty = 2,       // nothing to sell: no product on its shelves, no service, no entrance
        NoStaff = 3,     // open, and nobody on shift at a station that needs somebody
        Prices = 4       // every one of its last customers turned its prices down
    }

    /// <summary>One kind of placed item that counts: how many are placed and their points (tenths, before the category's cap).</summary>
    public struct InteriorItem
    {
        public string Name;
        public DecorCategory Category;
        public int Copies;
        public int Points;
    }

    /// <summary>Today's customers of a business by where they came from, as the game counts them (InteriorRules.Split).</summary>
    public struct CustomerSplit
    {
        public int Total;       // every customer of today's list, those served and those to come
        public int Game;        // the street's traffic and the game's own draw
        public int Marketing;   // the business's marketing
        public int Interiors;   // its interior and decor, an office's clients booked for it included; negative when short
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
    /// The parts of the feature that apply today and how strong each is (<see cref="InteriorRules.Parts"/>): thousandths
    /// (1000: the slider at 100%), or for the decor the per mille of customers it brings at a score of 100; 0 is off.
    /// </summary>
    public struct InteriorParts
    {
        public int StandardScale;   // the district's standard: its customers either way, and its complaints
        public int DecorMille;      // customers decor brings at a decor score of 100, per mille (100: 10%)
        public int PriceScale;      // what a nicer place lets a shop charge: the price borne over the standard, and the basket
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

        // Read from the game
        public int Interior;                    // the game's own interior design score, 0 to 100 (walls and floors)
        public int Sqm;                         // floor area, square metres
        public int Clean = 100;                 // the game's own cleanliness, 0 to 100
        public int Income = InteriorRules.IncomeMiddle;   // the district's income level, 60 to 140 (100 is the city's middle)
        public int GameMinimum;                 // the game's own minimum interior score for the district (Midtown asks for one)
        public readonly int[] Points = new int[InteriorRules.Categories];   // tenths of a point, before each category's cap
        public readonly int[] Items = new int[InteriorRules.Categories];    // items placed, by category
        public int Kinds;                       // different kinds of decor item (furniture and equipment not counted)
        public bool RestCorner;                 // a seat, a table and something from a kitchen: somewhere for staff to take a break
        public bool Fitting;                    // still in its first days: the district does not hold it to its standard yet
        public int StandardFrom;                // and the day from which it does
        public SaleStop Stop;                   // why it sells nothing just now (looked at hourly), or None
        public readonly List<InteriorItem> Placed = new List<InteriorItem>(16);   // every kind of placed item that counts

        // Worked out
        public int Target;                      // the points a place of this size needs for full marks, tenths
        public readonly int[] Capped = new int[InteriorRules.Categories];   // each category's points after its cap, tenths
        public int DecorRaw, Decor, InteriorNow, Overall, Standard;
        public int Wear;                        // thousandths, 0 fresh to 1000 worn out (the file's)
        public int StandardFootfall, DecorFootfall, Footfall, Bears, Basket, BrandPoints, StandingPoints, Comfort, OfficeEsteem, OfficeBid;
        public int OfficeBoost;                 // thousandths more clients an office is booked, an hour ahead
        public int ComplaintChance;             // per mille today

        // Phasing in and the bound, set by the director before Work (the defaults are the rules in full)
        public int Ramp = 1000;                 // thousandths of the effects on customers and takings that apply today
        public int PhaseFrom;                   // the day it began to come in
        public int IssueDip;                    // business pressure's own change of the shop's customers now, per cent
        public int StandardFull, DecorFull;     // the two parts of footfall in full, before phasing in and giving way
        public int Bound;                       // the most the shortfall may cost today, per cent (6 at the default strength)
    }

    /// <summary>What the feature keeps for one business between days: its finishes' wear and when it last made news. Saved.</summary>
    public sealed class InteriorFile
    {
        public string Place = string.Empty;
        public int Wear;               // thousandths
        public int LastInterior = -1;  // the game's interior score when last read: a redecoration shows as a rise
        public int LastDay;            // the last day it was read
        public int StoryDay = -1000;   // the last day it was in the City Ledger or a Flare draft
        public int RefreshDay = -1;    // the last day its finishes were refreshed
        public int FirstDay = Unset;   // the first day it was read (its fitting-out days count from it)

        public const int Unset = int.MinValue;
        public const int LongAgo = -1000;   // a business already trading when the feature first read it
    }

    /// <summary>
    /// The rules of interiors and decor, game-free and checked by EconomyCheck (InteriorChecks): the decor score from the
    /// placed items, the target by floor area, wear and dirt, the district's standard, the strengths, the bound on what it
    /// may cost a business (<see cref="GiveWay"/>), the phasing in (<see cref="Ramp"/>) and every effect with its cap. The
    /// Guide prints its figures from here; the rules in full are in docs/features/interiors.md.
    /// </summary>
    public static partial class InteriorRules
    {
        public const int Categories = 7;
        public static readonly string[] Ids = { "art", "greenery", "lighting", "audio", "floor", "furniture", "equipment" };

        // Decor
        public static readonly int[] ItemPoints = { 10, 10, 8, 12, 10, 4, 2 };          // tenths of a point, an item kind's first copy
        public static readonly int[] CapShare = { 300, 300, 300, 150, 200, 150, 100 };  // thousandths of the target a category may fill
        public const int RepeatShare = 600;           // thousandths each further copy of a kind counts of the one before
        public const int RepeatMost = 5;              // copies of a kind counted at most
        public const int TargetLeast = 12;            // points a place of up to TargetFromSqm needs
        public const int TargetFromSqm = 24;          // square metres
        public const int TargetSqmPerPoint = 12;      // one point more for each this many square metres
        public const int TargetMost = 90;             // points
        public const int VarietyFrom = 3;             // different kinds of decor item before variety counts
        public const int VarietyMost = 8;             // points variety adds at most

        // Wear and dirt
        public const int WearDay = 3;                 // thousandths a day
        public const int WearDirtMost = 4;            // thousandths more a day on a floor as dirty as it gets
        public const int WearMost = 1000;             // thousandths: worn out
        public const int WearPenaltyMost = 200;       // thousandths a score loses at full wear
        public const int DirtFrom = 70;               // cleanliness under this costs the scores
        public const int DirtPenaltyMost = 150;       // thousandths they lose at nothing clean
        public const int RedecorateRise = 10;         // the game's interior score rising this much is a redecoration
        public const int RedecorateShare = 500;       // thousandths of the wear a redecoration keeps
        public const int RefreshPerSqm = 2;           // dollars a square metre to refresh the finishes
        public const int RefreshLeast = 150, RefreshMost = 5000;   // dollars
        public const int WornFrom = 600;              // thousandths of wear an inspector notices
        public const int WornInspection = 5;          // points off the cleanliness they judge
        public const int InteriorShare = 600;         // thousandths of the overall figure that is the interior score

        // The district's standard
        public const int IncomeLeast = 60, IncomeMiddle = 100, IncomeMost = 140;
        public const int StandardLeast = 15, StandardMost = 70;

        // Effects (at the default strength)
        public const int ShortPointsPerPercent = 4, ShortFootfallMost = 6;   // the shortfall's most is the bound
        public const int OverPointsPerPercent = 8, OverFootfallMost = 3;
        public const int FootfallLeast = -12, FootfallMost = 24;      // per cent: the shortfall at double; decor and the standard over it at double, kept to 24
        public const int BearsOverMost = 30;                           // thousandths of the price over the standard; under it, nothing less
        public const int DecorMille = 100;                             // per mille more customers at a decor score of 100 (10%)
        public const int ComplaintFrom = 10, ComplaintPerPoint = 4, ComplaintMost = 120;   // per mille a day
        public const int ComplaintFromStrength = 1000;                 // under this strength no complaint: the bound is under a complaint's 6%
        public const int RampDays = 7;                                 // days over which every effect on customers and takings comes in
        public const int BasketFullAt = 85;                            // the overall figure at which the basket is at its district's most
        public const int BasketMostPoor = 100, BasketMostRich = 60;    // per mille of an order's products, where the standard is lowest and highest
        public const int BasketOfficeMost = 1;                         // products more an office's client takes at most
        public const int BrandMiddle = 50, BrandPointsPer = 15, BrandMost = 3;
        public const int StandingMiddle = 50, StandingPointsPer = 20, StandingMost = 2;
        public const int ComfortMost = 5, ComfortMoralePerPoint = 8;
        public const int PlantsForTwo = 3;
        public const int OfficeOne = 60, OfficeTwo = 80;
        public const int BidFrom = 50, BidMost = 20;                   // thousandths of a bid's weight
        public const int StrikingFrom = 85;           // an overall figure that makes news
        public const int ShabbyWear = 800;            // thousandths of wear that does
        public const int ShabbyShort = 25;            // points under the standard that do too
        public const int StoryRestDays = 60;          // a business is in the news for its interior once in this many days at most
        public const int StoryChance = 100;           // per mille a day, once it is newsworthy
        public const int GraceDays = 14;              // a new business's days to fit out before its district holds it to the standard
        public const int RecentOrders = 8;            // the last completed orders looked at for prices turned down, and no fewer
        public const int OfficeBoostMost = 200;       // thousandths more clients at an overall score of 100 and the strongest decor

        // The strengths: the options' four sliders, per cent of the figures above.
        public const int StrengthLeast = 0, StrengthMost = 200, StrengthDefault = 100;

        // A saved choice of the older drop-downs as its slider figure, in the drop-down's order (Core/Settings).
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

        /// <summary>The parts that apply, from the four sliders and the other authors' mods, which take over their parts.</summary>
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

        /// <summary>The wear after a redecoration: the game's interior score up by <see cref="RedecorateRise"/> or more since last read.</summary>
        public static int Redecorated(int wear, int before, int now)
        {
            return before >= 0 && now - before >= RedecorateRise ? wear * RedecorateShare / 1000 : wear;
        }

        /// <summary>
        /// Why a refresh of the finishes cannot be done, as the notice's locale key, or null when it can: wear off in the options
        /// first (nothing wears), then a business Interiors keeps no record of (not read yet, or its file list full).
        /// </summary>
        public static string RefreshRefused(int wearScale, bool known, bool filed)
        {
            if (wearScale <= 0)
            {
                return "reallife_interior_wear_off";
            }
            return known && filed ? null : "reallife_interior_refresh_unknown";
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

        /// <summary>The standard a district expects, from its income level: 15 at 60 up to 70 at 140, never under the game's own minimum there.</summary>
        public static int Standard(int income, int gameMinimum)
        {
            int i = Clamp(income, IncomeLeast, IncomeMost);
            int standard = StandardLeast + (i - IncomeLeast) * (StandardMost - StandardLeast) / (IncomeMost - IncomeLeast);
            return Clamp(Math.Max(standard, Clamp(gameMinimum, 0, 100)), 0, 100);
        }
    }
}
