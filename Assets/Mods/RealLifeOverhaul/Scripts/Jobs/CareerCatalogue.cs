using System;

namespace RealLifeOverhaul
{
    /// <summary>What a shift at a rank is made of.</summary>
    public enum CareerWork
    {
        Gig,        // the employer's own gig job (JobCatalogue), with the rank's changes
        Dispatch,   // the planning desk: parcels on to routes against the clock (DispatchJob)
        Lead        // in charge of the others: staff to check on, incidents to decide (LeadJob)
    }

    /// <summary>What a rank changes about the work itself.</summary>
    [Flags]
    public enum CareerPerk
    {
        None = 0,
        CompanyVan = 1,     // the firm's van, free of the day's hire
        Premium = 2,        // needs a Luxury-kind car; passengers are harder to please and tip far better
        OwnCart = 4,        // the cart is rented by the day and the takings are kept
        Shorter = 8         // a cab shift is a set number of fares, not until the driver stops
    }

    /// <summary>One rung of an employer's ladder. Game-free: every number that balances a career is in this file.</summary>
    public sealed class CareerRank
    {
        public readonly string TitleKey;
        public readonly CareerWork Work;
        public readonly float Hourly;       // basic wage per game hour of a rota shift, on top of what the work itself pays
        public readonly float Piece;        // factor on what the work itself pays (piece pay)
        public readonly float TipFactor;    // factor on tips
        public readonly int ExtraUnits;     // units added to a shift (a bigger round)
        public readonly CareerPerk Perks;

        public CareerRank(string titleKey, CareerWork work, float hourly, float piece = 1f, float tipFactor = 1f, int extraUnits = 0,
            CareerPerk perks = CareerPerk.None)
        {
            TitleKey = titleKey;
            Work = work;
            Hourly = hourly;
            Piece = piece;
            TipFactor = tipFactor;
            ExtraUnits = extraUnits;
            Perks = perks;
        }

        public bool Has(CareerPerk perk) => (Perks & perk) != 0;
    }

    /// <summary>A rota on offer: which days of the week (bit 0 = Monday) and the hour a shift starts.</summary>
    public sealed class CareerRota
    {
        public readonly string NameKey;
        public readonly int Days;
        public readonly int Hour;

        public CareerRota(string nameKey, int days, int hour)
        {
            NameKey = nameKey;
            Days = days;
            Hour = hour;
        }
    }

    /// <summary>
    /// One employer: its name, where it is found (which kind of building its depot or office is taken to be
    /// in), its boss, the gig it grows out of, and its ladder.
    /// </summary>
    public sealed class Employer
    {
        public readonly string Id;
        public readonly string NameKey;
        public readonly string BossKey;          // the boss's name
        public readonly string Pitch;            // the boss's own lines, in their own manner: what the work is, with {title}, {wage}, {status}
        public readonly string Welcome;          // ...on being taken on, with {rota}
        public readonly string Review;           // ...how they open a review
        public readonly string Notice;           // ...on being given notice, with {shifts}
        public readonly string Walkout;          // ...on being walked out on
        public readonly string Job;              // the gig it grows out of (JobCatalogue id)
        public readonly string Related;          // a gig whose experience also counts here, or null
        public readonly int References;          // good references needed to be taken on
        public readonly int Seed;                // the boss's face: the same in every game
        public readonly bool BossFemale;
        public readonly string LeadSkill;        // the game's skill a lead or desk shift trains, or null
        public readonly string[] Colleagues;     // names of the two people seen on shifts
        public readonly string[] Premises;       // business types its depot may be, best first (matched as a substring of the game's type name)
        public readonly CareerRank[] Ranks;

        /// <param name="keys">Locale keys, each written out in full where the employer is listed: name, boss, the two colleagues, then the boss's five lines.</param>
        public Employer(string id, string[] keys, string job, string related, int references, int seed, bool bossFemale, string leadSkill,
            string[] premises, params CareerRank[] ranks)
        {
            Id = id;
            NameKey = keys[0];
            BossKey = keys[1];
            Pitch = keys[4];
            Welcome = keys[5];
            Review = keys[6];
            Notice = keys[7];
            Walkout = keys[8];
            Job = job;
            Related = related;
            References = references;
            Seed = seed;
            BossFemale = bossFemale;
            LeadSkill = leadSkill;
            Colleagues = new[] { keys[2], keys[3] };
            Premises = premises;
            Ranks = ranks;
        }
    }

    /// <summary>
    /// Employers, ranks, rotas and every figure of the career ladder. Game-free, like the job, event and
    /// crime catalogues.
    ///
    /// Balance. One game hour is one minute of play; a rota shift is one run of the work, about four game
    /// hours. The game pays a cashier 25 an hour and its own staff 12 to 18 at no skill; its van job comes
    /// to about 100 an hour. Freelance gigs (JobCatalogue) come to 10 to 18 an hour on foot and 45 to 80
    /// with a car. An employer adds a basic hourly wage on top of the piece pay, for the hours of the shift
    /// (four at most), so that a rota shift is worth about a fifth to a third more than the same work
    /// freelance, and the desk and lead ranks are salaried:
    ///
    ///   rank                         basic/h   work pays                      about, per game hour
    ///   Leafleter / Cleaner            4         as the gig                     14 to 19
    ///   Stocker / Vendor               5         as the gig                     21 to 24
    ///   Courier                        6         as the gig                     50 to 66
    ///   Senior courier                 9         7 parcels, firm's van, +10 %   65 to 80
    ///   Dispatcher                    20         14 a plan x quality, 3 plans   27 to 31 (no car, no risk)
    ///   Depot supervisor              30         12 a good call, 4 calls        36 to 42
    ///   Crew / team / shift lead      18 to 20   10 a good call                 24 to 30
    ///   Assistant manager             26         12 a good call                 32 to 38
    ///   Cab driver                     5         4 fares, as the gig            55 to 85
    ///   Premium driver                 8         fares +25 %, tips x2           80 to 120 (needs a Luxury car)
    ///   Owner-operator                 0         fares +45 %, tips x2           90 to 130
    ///   Own cart                       0         takings 11 a customer less 3.5 stock, 60 a day for the cart: 30 to 45
    ///
    /// All of it stays under the van job except the two top cab ranks, which need a car costing tens of
    /// thousands. Tax follows the game: it takes no tax from the player's wages (its one employer job pays
    /// the hourly rate in full), so the payslip's tax line reads zero and says so.
    /// Once the player runs businesses, employers pay less (<see cref="OwnerFactor"/>): work stays possible
    /// but is not a better use of an hour than a shop.
    /// </summary>
    public static class CareerCatalogue
    {
        public const string Courier = "courier";
        public const string Cab = "cab";
        public const string Cleaning = "cleaning";
        public const string Retail = "retail";
        public const string Food = "food";
        public const string Promo = "promo";

        public const int ShiftHours = 4;            // the most hours of basic wage one shift pays
        public const int EarlyHours = 2;            // a shift may be clocked on to this long before its hour
        public const int OnTimeHours = 1;           // ...and counts as on time until this long after it
        public const int LateHours = 3;             // after this it is a no-show
        public const int ReminderHours = 3;         // the phone reminds this long before
        public const int MarksToDismiss = 3;        // late arrivals and no-shows before the employer lets the player go
        public const int ShiftsPerReview = 3;       // rota shifts between performance reviews
        public const int NoticeShifts = 2;          // rota shifts to work out after giving notice
        public const int NoticeDays = 7;            // ...or this many days, whichever comes first
        public const int BanDays = 7;               // days an employer will not hear from someone it dismissed or who walked out
        public const int MaxRaises = 3;             // raises at one rank
        public const float RaiseStep = 1f;          // basic wage per hour added by a raise
        public const int HistoryMost = 8;           // lines of employment history kept
        public const int CartRent = 60;             // a day's rent of the cart, own-cart licence
        public const float CartTakings = 11f;       // what a customer pays at the player's own cart
        public const float CartStock = 3.5f;        // ...and what the drink cost
        public const int CabShiftFares = 4;         // fares in an employed cab shift
        public const int GigFee = 45;               // a venue's fee for a booked set, on top of the hat
        public const float GigTipFactor = 1.3f;
        public const int GigNeedsLevel = 2;         // busker level at which the agency books sets
        public const float PromoteScore = 70f;      // review score for a promotion
        public const float RaiseScore = 55f;        // ...for a raise
        public const float WarnScore = 35f;         // ...below which the review is a warning (a mark)
        public const float PremiumHarsh = 0.7f;     // a premium passenger minds this share of what an ordinary one puts up with

        private const string Logistics = "ba:skill_logisticsmanager";
        private const string Hr = "ba:skill_hrmanager";

        public static readonly CareerRota[] Rotas =
        {
            new CareerRota("reallife_career_rota_mwf", 1 | 4 | 16, 8),        // Monday, Wednesday, Friday mornings
            new CareerRota("reallife_career_rota_tts", 2 | 8 | 32, 13),       // Tuesday, Thursday, Saturday afternoons
            new CareerRota("reallife_career_rota_weekend", 32 | 64, 10)       // weekends
        };

        public static readonly Employer[] All =
        {
            new Employer(Promo, new[]
                {
                    "reallife_career_promo_name", "reallife_career_promo_boss", "reallife_career_promo_mate_1", "reallife_career_promo_mate_2",
                    "reallife_career_promo_pitch", "reallife_career_promo_welcome", "reallife_career_promo_review", "reallife_career_promo_notice",
                    "reallife_career_promo_walkout"
                }, JobCatalogue.Flyers, JobCatalogue.Vendor, 0, 4101, true, Hr, new[] { "marketingagency", "graphicdesigner", "eventplanning" },
                new CareerRank("reallife_career_promo_rank_1", CareerWork.Gig, 4f),
                new CareerRank("reallife_career_promo_rank_2", CareerWork.Lead, 18f)),
            new Employer(Cleaning, new[]
                {
                    "reallife_career_cleaning_name", "reallife_career_cleaning_boss", "reallife_career_cleaning_mate_1", "reallife_career_cleaning_mate_2",
                    "reallife_career_cleaning_pitch", "reallife_career_cleaning_welcome", "reallife_career_cleaning_review", "reallife_career_cleaning_notice",
                    "reallife_career_cleaning_walkout"
                }, JobCatalogue.Cleaner, JobCatalogue.Stocker, 0, 4202, true, Hr, new[] { "interiorinstallation", "movingservice", "hospital" },
                new CareerRank("reallife_career_cleaning_rank_1", CareerWork.Gig, 4f),
                new CareerRank("reallife_career_cleaning_rank_2", CareerWork.Lead, 18f)),
            new Employer(Food, new[]
                {
                    "reallife_career_food_name", "reallife_career_food_boss", "reallife_career_food_mate_1", "reallife_career_food_mate_2",
                    "reallife_career_food_pitch", "reallife_career_food_welcome", "reallife_career_food_review", "reallife_career_food_notice",
                    "reallife_career_food_walkout"
                }, JobCatalogue.Vendor, JobCatalogue.Flyers, 0, 4303, false, null, new[] { "fastfood", "coffeeshop", "wholesale" },
                new CareerRank("reallife_career_food_rank_1", CareerWork.Gig, 5f),
                new CareerRank("reallife_career_food_rank_2", CareerWork.Gig, 0f, 1f, 1.2f, 0, CareerPerk.OwnCart)),
            new Employer(Retail, new[]
                {
                    "reallife_career_retail_name", "reallife_career_retail_boss", "reallife_career_retail_mate_1", "reallife_career_retail_mate_2",
                    "reallife_career_retail_pitch", "reallife_career_retail_welcome", "reallife_career_retail_review", "reallife_career_retail_notice",
                    "reallife_career_retail_walkout"
                }, JobCatalogue.Stocker, JobCatalogue.Cleaner, 1, 4404, false, Hr, new[] { "supermarket", "wholesale", "officesupply" },
                new CareerRank("reallife_career_retail_rank_1", CareerWork.Gig, 5f),
                new CareerRank("reallife_career_retail_rank_2", CareerWork.Lead, 20f),
                new CareerRank("reallife_career_retail_rank_3", CareerWork.Lead, 26f, 1.2f)),
            new Employer(Courier, new[]
                {
                    "reallife_career_courier_name", "reallife_career_courier_boss", "reallife_career_courier_mate_1", "reallife_career_courier_mate_2",
                    "reallife_career_courier_pitch", "reallife_career_courier_welcome", "reallife_career_courier_review", "reallife_career_courier_notice",
                    "reallife_career_courier_walkout"
                }, JobCatalogue.Courier, JobCatalogue.Taxi, 1, 4505, true, Logistics, new[] { "importexport", "truckgarage", "warehouse" },
                new CareerRank("reallife_career_courier_rank_1", CareerWork.Gig, 6f),
                new CareerRank("reallife_career_courier_rank_2", CareerWork.Gig, 9f, 1.1f, 1f, 2, CareerPerk.CompanyVan),
                new CareerRank("reallife_career_courier_rank_3", CareerWork.Dispatch, 20f),
                new CareerRank("reallife_career_courier_rank_4", CareerWork.Lead, 30f, 1.2f)),
            new Employer(Cab, new[]
                {
                    "reallife_career_cab_name", "reallife_career_cab_boss", "reallife_career_cab_mate_1", "reallife_career_cab_mate_2",
                    "reallife_career_cab_pitch", "reallife_career_cab_welcome", "reallife_career_cab_review", "reallife_career_cab_notice",
                    "reallife_career_cab_walkout"
                }, JobCatalogue.Taxi, JobCatalogue.Courier, 2, 4606, false, null, new[] { "cardealership", "gasstation", "truckgarage" },
                new CareerRank("reallife_career_cab_rank_1", CareerWork.Gig, 5f, 1f, 1f, 0, CareerPerk.Shorter),
                new CareerRank("reallife_career_cab_rank_2", CareerWork.Gig, 8f, 1.25f, 2f, 0, CareerPerk.Shorter | CareerPerk.Premium),
                new CareerRank("reallife_career_cab_rank_3", CareerWork.Gig, 0f, 1.45f, 2f, 0, CareerPerk.Shorter | CareerPerk.Premium))
        };

        public static Employer Find(string id)
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

        /// <summary>True on a rota's day: day 1 of a game is a Monday, as the game counts.</summary>
        public static bool RotaDay(int days, int day)
        {
            return day > 0 && (days & (1 << ((day - 1) % 7))) != 0;
        }

        /// <summary>The next day on or after a day that a rota works; -1 for an empty rota.</summary>
        public static int NextRotaDay(int days, int from)
        {
            for (int i = 0; i < 7; i++)
            {
                if (RotaDay(days, from + i))
                {
                    return from + i;
                }
            }
            return -1;
        }

        /// <summary>
        /// What employers pay someone who runs businesses of their own, as a factor: nothing off with none,
        /// then 15 % less for each, never under 55 %.
        /// </summary>
        public static float OwnerFactor(int businesses)
        {
            float factor = 1f - 0.15f * (businesses < 0 ? 0 : businesses);
            return factor < 0.55f ? 0.55f : factor;
        }

        /// <summary>
        /// Experience brought along: rota shifts credited towards the first review for the level already
        /// reached in the employer's own gig (two each above the first) and in a related one (one each).
        /// Never the whole review.
        /// </summary>
        public static int CarryOver(int ownLevel, int relatedLevel)
        {
            int credit = (ownLevel > 1 ? (ownLevel - 1) * 2 : 0) + (relatedLevel > 1 ? relatedLevel - 1 : 0);
            return credit > ShiftsPerReview - 1 ? ShiftsPerReview - 1 : credit;
        }

        /// <summary>
        /// A review's score, 0 to 100, from what really happened since the last one: mostly turning up on
        /// time, then complaints (failed shifts), crashes and police stops on the job, with a little for tips.
        /// </summary>
        public static float ReviewScore(int onTime, int late, int noShow, int complaints, int crashes, int stops, int tips, int shifts)
        {
            int due = onTime + late + noShow;
            float punctual = due > 0 ? (onTime + late * 0.4f) / due : 1f;
            float score = 20f + 60f * punctual;
            score -= complaints * 12f;
            score -= crashes * 6f;
            score -= stops * 10f;
            if (shifts > 0)
            {
                float perShift = tips / (float)shifts;
                score += perShift >= 20f ? 20f : perShift;   // a point a dollar of tips a shift, twenty at most
            }
            else
            {
                score -= 10f;
            }
            return score < 0f ? 0f : score > 100f ? 100f : score;
        }
    }
}
