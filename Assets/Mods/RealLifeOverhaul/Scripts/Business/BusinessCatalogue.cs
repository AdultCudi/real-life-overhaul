namespace RealLifeOverhaul
{
    /// <summary>Which options-page switch an issue belongs to.</summary>
    public enum PressureGroup
    {
        Rivals,
        Racket,
        Inspections,
        Staff,
        Suppliers,
        Customers,
        Audit,
        Opportunities
    }

    /// <summary>
    /// One kind of thing that can happen to a business: a small story with a start, choices, a
    /// deadline and an end. Game-free; what each does to the game is in BusinessIssues.
    /// </summary>
    public sealed class IssueDef
    {
        public readonly string Id;
        public readonly PressureGroup Group;
        public readonly bool Positive;     // an opportunity: the only kind left with "easy business" on
        public readonly int Weight;        // share of the deal
        public readonly int DecideDays;    // days the player has to choose before the default happens
        public readonly int RestDays;      // days before this kind can come up again, anywhere

        public readonly string NameKey;    // what it is called on the Business desk

        public IssueDef(string id, string nameKey, PressureGroup group, bool positive, int weight, int decideDays, int restDays)
        {
            Id = id;
            NameKey = nameKey;
            Group = group;
            Positive = positive;
            Weight = weight;
            DecideDays = decideDays;
            RestDays = restDays;
        }

    }

    /// <summary>
    /// Every kind of business pressure and every number that bounds it. Game-free, like the job,
    /// event and crime catalogues: nothing that balances a pressure is anywhere else.
    ///
    /// The rules that hold for all of them:
    ///  - never on a business the mod has known for less than <see cref="NewBusinessDays"/> days, and
    ///    nothing at all in the first <see cref="NewSaveDays"/> days after the mod first sees a save;
    ///  - one open issue per business, <see cref="MostOpen"/> overall; after one ends, that business is
    ///    left alone for <see cref="BusinessRestDays"/> days;
    ///  - at most one new issue a day, rolled once, in the morning;
    ///  - every charge is a share of what that business itself took a day (the game's order history, last
    ///    seven days) between a floor and a ceiling; one the player did not choose (a fine, a repair, an
    ///    adjustment) is also never more than <see cref="CashShare"/> of the cash
    ///    in hand, never forced: a charge that cannot be paid falls back to the issue's other outcome;
    ///  - footfall changes go through the game's promotion figure, between <see cref="MostDip"/> and
    ///    <see cref="MostBoost"/> percent of customers, one per business, each with an end day;
    ///  - a closure is the game's own "temporarily closed" switch for <see cref="MostClosedHours"/>
    ///    hours at most, reopened by the mod (and by the player at any time).
    /// </summary>
    public static class BusinessCatalogue
    {
        public const string Undercut = "undercut";
        public const string Poach = "poach";
        public const string Smear = "smear";
        public const string Racket = "racket";
        public const string Health = "health";
        public const string Fire = "fire";
        public const string Licence = "licence";
        public const string Strike = "strike";
        public const string StaffTheft = "stafftheft";
        public const string Argument = "argument";
        public const string SupplierLate = "supplierlate";
        public const string SupplierPrice = "supplierprice";
        public const string Complaint = "complaint";
        public const string Praise = "praise";
        public const string Audit = "audit";
        public const string BulkOrder = "bulkorder";
        public const string Feature = "feature";
        public const string Award = "award";

        // ---- the rules for all
        public const int NewSaveDays = 7;
        public const int NewBusinessDays = 7;
        public const int BusinessRestDays = 3;
        public const int MostOpen = 2;
        public const int RollHour = 9;
        public const float RollBase = 0.08f;          // chance a day that something comes up, at frequency 1...
        public const float RollPerStep = 0.025f;      // ...and more for each step of the frequency option (0.305 at 10)
        public const float CashShare = 0.06f;         // no single charge is more than this share of the cash in hand
        public const int MostDip = -12;               // percent of customers
        public const int MostBoost = 10;
        public const int AmbientMostDip = -25;        // what the city's economy may add to that, in all (district, cycle and prices: Economy/City)...
        public const int AmbientMostBoost = 15;       // ...kept apart from an issue's own change, which keeps its own bounds
        public const int MostClosedHours = 12;
        public const float SatisfactionFloor = 20f;   // an employee's satisfaction is never pushed below this (the game makes them resign under 10)

        // ---- rivals
        public const int UndercutDip = -10;
        public const int UndercutDays = 5;
        public const float UndercutMarketingShare = 0.6f;    // of a day's income: floor 80, ceiling 1200
        public const float UndercutPriceShare = 0.3f;        // floor 40, ceiling 600
        public const int UndercutQualityCleanliness = 80;
        public const int UndercutQualitySatisfaction = 60;
        public const int PoachDays = 3;
        public const int PoachRaise = 12;                    // percent the rival offers: what matching it costs
        public const int SmearDip = -6;
        public const int SmearDays = 4;
        public const float SmearReplyShare = 0.4f;           // floor 60, ceiling 800
        public const float SmearLawyerShare = 0.25f;         // floor 50, ceiling 500
        public const float SmearLawyerOdds = 0.55f;

        // ---- protection racket
        public const float RacketWeeklyShare = 0.28f;        // of a day's income, a week (4 % of the week): floor 60, ceiling 600
        public const float RacketCreep = 1.1f;               // what the collector asks grows by this each week paid...
        public const float RacketCreepMost = 1.5f;           // ...up to this much of the first demand
        public const float RacketMinDaily = 150f;            // a business making less than this a day is not worth their while
        public const float RacketRetaliationOdds = 0.7f;     // before security: multiplied by 1 - level / 150
        public const float RacketRepairShare = 0.5f;         // floor 80, ceiling 900
        public const int RacketClosedHours = 4;
        public const int RacketInvestigationDays = 3;
        public const float RacketPoliceOdds = 0.35f;         // plus half the security level, in percent, to 0.85 at most
        public const float RacketPoliceMost = 0.85f;
        public const int RacketSecurityLevel = 50;           // the game's security level at which the collector thinks better of it
        public const int RacketSecurityDays = 3;
        public const int RacketRestDays = 14;                // after they are refused or back off
        public const int RacketStingHour = 14;

        // ---- inspections
        public const int InspectionHour = 10;
        public const int HealthPass = 75;                    // the game's cleanliness figure, 0 to 100
        public const int HealthFail = 50;
        public const int HealthShut = 30;
        public const float HealthFineShare = 0.5f;           // floor 100, ceiling 1500
        public const float HealthCleanShare = 0.2f;          // a contract clean before the visit: floor 40, ceiling 400
        public const int HealthClosedHours = 6;
        public const int FirePass = 55;                      // cleanliness (clutter)...
        public const int FireFacility = 40;                  // ...and the game's facility score
        public const float FireFineShare = 0.4f;             // floor 80, ceiling 1000
        public const float FireCheckShare = 0.25f;           // a contractor's check beforehand: floor 50, ceiling 500
        public const int FireClosedHours = 4;
        public const int PassBoost = 4;                      // percent of customers for a clean pass...
        public const int PassBoostDays = 3;
        public const float LicenceShare = 0.5f;              // floor 60, ceiling 900
        public const float LicenceLate = 1.5f;
        public const int LicenceClosedHours = 6;

        // ---- staff
        public const float StrikeGap = 0.12f;                // average shortfall against the game's own wage formula that starts it
        public const float StrikeRaiseTo = 0.95f;            // agreeing brings each underpaid wage to this share of the formula...
        public const float StrikeRaiseMost = 1.15f;          // ...but never by more than this at once
        public const int StrikeBonusHours = 8;               // a one-off bonus of this many hours' wages each
        public const float StrikeWalkoutOdds = 0.5f;
        public const int StrikeClosedHours = 8;
        public const float StaffTheftShare = 0.15f;          // floor 20, ceiling 300: booked as theft in the business's books, no cash moves
        public const int StaffTheftMinSecurity = 10;         // the game's security level: there has to be a camera to catch them on
        public const float StaffTheftRepayOdds = 0.3f;
        public const float ArgumentLunchShare = 0.05f;       // floor 20, ceiling 120

        // ---- suppliers
        public const float ExpressShare = 0.15f;             // of the delivery's own price: floor 20, ceiling 400
        public const float SurchargeShare = 0.08f;           // floor 15, ceiling 500
        public const float NegotiateOdds = 0.3f;             // plus half the player's negotiation skill, in percent, to 0.8 at most

        // ---- customers
        public const int ComplaintBelow = 50;                // the game's overall satisfaction
        public const int ComplaintDip = -6;
        public const int ComplaintDays = 3;
        public const float ComplaintVoucherShare = 0.15f;    // floor 20, ceiling 300
        public const int PraiseAbove = 70;
        public const int PraiseBoost = 6;
        public const int PraiseDays = 3;
        public const int PraiseSharedDays = 5;

        // ---- tax audit
        public const float AuditAccountantShare = 0.07f;     // of a week's sales: floor 100, ceiling 800 (1 % of the week)
        public const float AuditAdjustShare = 0.14f;         // floor 100, ceiling 1500 (2 % of the week)
        public const float AuditRefundShare = 0.07f;         // ceiling 500

        // ---- opportunities
        public const float BulkShare = 0.4f;                 // floor 120, ceiling 2000
        public const int BulkHour = 12;
        public const int FeatureBoost = 8;
        public const int FeatureDays = 3;
        public const int AwardSatisfaction = 75;
        public const int AwardCleanliness = 70;
        public const float AwardPrizeShare = 0.3f;           // floor 100, ceiling 1000
        public const int AwardBoost = 5;
        public const int AwardDays = 4;

        public static readonly IssueDef[] All =
        {
            new IssueDef(Undercut, "reallife_biz_undercut_name", PressureGroup.Rivals, false, 10, 3, 14),
            new IssueDef(Poach, "reallife_biz_poach_name", PressureGroup.Rivals, false, 6, PoachDays, 21),
            new IssueDef(Smear, "reallife_biz_smear_name", PressureGroup.Rivals, false, 6, 2, 16),
            new IssueDef(Racket, "reallife_biz_racket_name", PressureGroup.Racket, false, 7, 3, RacketRestDays),
            new IssueDef(Health, "reallife_biz_health_name", PressureGroup.Inspections, false, 9, 1, 14),
            new IssueDef(Fire, "reallife_biz_fire_name", PressureGroup.Inspections, false, 6, 1, 20),
            new IssueDef(Licence, "reallife_biz_licence_name", PressureGroup.Inspections, false, 5, 3, 30),
            new IssueDef(Strike, "reallife_biz_strike_name", PressureGroup.Staff, false, 7, 3, 16),
            new IssueDef(StaffTheft, "reallife_biz_stafftheft_name", PressureGroup.Staff, false, 6, 2, 14),
            new IssueDef(Argument, "reallife_biz_argument_name", PressureGroup.Staff, false, 7, 2, 10),
            new IssueDef(SupplierLate, "reallife_biz_supplierlate_name", PressureGroup.Suppliers, false, 7, 1, 10),
            new IssueDef(SupplierPrice, "reallife_biz_supplierprice_name", PressureGroup.Suppliers, false, 6, 2, 14),
            new IssueDef(Complaint, "reallife_biz_complaint_name", PressureGroup.Customers, false, 8, 2, 10),
            new IssueDef(Praise, "reallife_biz_praise_name", PressureGroup.Customers, true, 8, 2, 10),
            new IssueDef(Audit, "reallife_biz_audit_name", PressureGroup.Audit, false, 4, 3, 30),
            new IssueDef(BulkOrder, "reallife_biz_bulkorder_name", PressureGroup.Opportunities, true, 9, 1, 8),
            new IssueDef(Feature, "reallife_biz_feature_name", PressureGroup.Opportunities, true, 7, 2, 12),
            new IssueDef(Award, "reallife_biz_award_name", PressureGroup.Opportunities, true, 4, 2, 30)
        };

        public static IssueDef Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The chance that a morning brings something, for a frequency option of 1 to 10.</summary>
        public static float RollChance(int frequency)
        {
            int steps = frequency < 1 ? 0 : frequency > 10 ? 9 : frequency - 1;
            return RollBase + RollPerStep * steps;
        }

        /// <summary>
        /// A charge: a share of a day's income between a floor and a ceiling, and never more than
        /// <see cref="CashShare"/> of the cash in hand (which may take it under the floor, down to nothing).
        /// </summary>
        public static int Fee(float dailyIncome, float share, int floor, int ceiling, float cash)
        {
            float fee = dailyIncome * share;
            fee = fee < floor ? floor : fee > ceiling ? ceiling : fee;
            fee = Prices.At(fee);   // the game's income and the bounds are at the start's prices: the fee is at today's
            float most = cash > 0f ? cash * CashShare : 0f;
            if (fee > most)
            {
                fee = most;
            }
            return fee < 0f ? 0 : (int)fee;
        }

        /// <summary>A footfall change kept inside what any pressure may do.</summary>
        public static int ClampFootfall(int percent)
        {
            return percent < MostDip ? MostDip : percent > MostBoost ? MostBoost : percent;
        }

        /// <summary>
        /// The promotion figure (0 to 100) that gives a business so many percent more or fewer customers than
        /// its own figure does, by the game's formula: customers go with base + 0.75 x promotion / 100.
        /// </summary>
        public static int PromotionFor(int own, int percent, float baseMultiplier)
        {
            return PromotionFor(own, percent, 0, baseMultiplier);
        }

        /// <summary>What the city's economy asks for, kept inside what it may do.</summary>
        public static int ClampAmbient(int percent)
        {
            return percent < AmbientMostDip ? AmbientMostDip : percent > AmbientMostBoost ? AmbientMostBoost : percent;
        }

        /// <summary>
        /// The promotion figure the mod writes for a business: its own where nothing changes its footfall, otherwise
        /// <see cref="PromotionFor(int, int, int, float)"/>. The one function behind every write of that figure
        /// (BusinessGame.Footfall), and behind the staff rota planner's forecast of a day on which the parts will be
        /// different, so that the forecast and what is written cannot drift apart.
        /// </summary>
        public static int Written(int own, int percent, int ambient, float baseMultiplier)
        {
            return percent != 0 || ambient != 0 ? PromotionFor(own, percent, ambient, baseMultiplier) : own;
        }

        /// <summary>The same with the city's economy on top: an issue's change and the ambient one are each kept to their own bounds and added.</summary>
        public static int PromotionFor(int own, int percent, int ambient, float baseMultiplier)
        {
            float multiplier = baseMultiplier + 0.75f * own / 100f;
            float wanted = multiplier * (1f + (ClampFootfall(percent) + ClampAmbient(ambient)) / 100f);
            float promotion = (wanted - baseMultiplier) / 0.75f * 100f;
            int rounded = (int)(promotion + 0.5f);
            return rounded < 0 ? 0 : rounded > 100 ? 100 : rounded;
        }
    }
}
