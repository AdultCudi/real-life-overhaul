using System;

namespace RealLifeOverhaul
{
    /// <summary>What a job asks of whoever takes it. Shown on the phone before it is taken.</summary>
    [Flags]
    public enum JobNeed
    {
        None = 0,
        Car = 1,            // an enclosed motor vehicle of the player's own, being driven or standing nearby
        Reference = 2,      // a reference letter: level two in any job
        CleanRecord = 4,    // not stopped by the police on a job for two days, and not sacked from this one
        Outdoors = 8,       // taken on the street, not inside a building
        Site = 16,          // a building trade: taken at a works site's notice board (Scripts/BuildTrades), never on the Gig Line
        Office = 32         // a building trade worked at the architects' office on a floor: taken inside it, not on the street
    }

    /// <summary>One job: who can take it, what a unit of its work pays, its energy, how much a day and how experience counts.</summary>
    public sealed class JobDef
    {
        public readonly string Id;
        public readonly string NameKey;          // "Parcel courier"
        public readonly string PitchKey;         // what the voice on the phone says about it
        public readonly string TransactionKey;   // the name the pay is booked under
        public readonly JobNeed Needs;
        public readonly float UnitPay;           // per fare, parcel or flyer, before distance
        public readonly float PayPerMetre;       // on top, per metre of the trip (0 for work that does not travel)
        public readonly float UnitEnergy;        // energy spent per unit of work done
        public readonly float UnitSkill;         // points of the skill gained per unit
        public readonly string Skill;            // the game's own skill it trains, or null
        public readonly int DailyUnits;          // how many units there are to be had in a day
        public readonly int UnitsPerLevel;       // units of work for each level after the first
        public readonly int ShiftUnits;          // how many units one shift is, on average (0 = until the player stops)
        public readonly int UnitsSpread;         // ...and how many more or fewer a freelance offer may make it; the average stays
        public readonly bool ByHour;             // paid for being there (a stall, a watch, a cab): a rota shift of it pays the hours begun, never the whole shift for finishing early

        public JobDef(string id, string nameKey, string pitchKey, string transactionKey, JobNeed needs, float unitPay, float payPerMetre,
            float unitEnergy, float unitSkill, string skill, int dailyUnits, int unitsPerLevel, int shiftUnits, int unitsSpread = 0, bool byHour = false)
        {
            Id = id;
            NameKey = nameKey;
            PitchKey = pitchKey;
            TransactionKey = transactionKey;
            Needs = needs;
            UnitPay = unitPay;
            PayPerMetre = payPerMetre;
            UnitEnergy = unitEnergy;
            UnitSkill = unitSkill;
            Skill = skill;
            DailyUnits = dailyUnits;
            UnitsPerLevel = unitsPerLevel;
            ShiftUnits = shiftUnits;
            UnitsSpread = unitsSpread;
            ByHour = byHour;
        }
    }

    /// <summary>
    /// Every job and every number that balances one; game-free. A shift played well pays 26 to 43 an hour on foot and 60 to 75
    /// with a car, against the game's cashier at 25 (docs/features/jobs.md); offers vary around these figures, and a tired or
    /// new hand earns less of them (<see cref="GigPayRules"/>).
    /// </summary>
    public static class JobCatalogue
    {
        public const string Flyers = "flyers";
        public const string Courier = "courier";
        public const string Taxi = "taxi";
        public const string Cleaner = "cleaner";
        public const string Stocker = "stocker";
        public const string Vendor = "vendor";
        public const string Busker = "busker";
        public const string Dispatch = "dispatch";   // the two career-only kinds: never on the Gig Line, no record in JobState
        public const string Lead = "lead";

        // The building trades (Scripts/BuildTrades): taken at a works site or the architects' office, never on the Gig Line.
        public const string Labourer = "labourer";
        public const string Mason = "mason";
        public const string Scaffolder = "scaffolder";
        public const string Painter = "painter";
        public const string Roofer = "roofer";
        public const string Electrician = "electrician";
        public const string Plumber = "plumber";
        public const string Crane = "crane_operator";
        public const string Inspector = "inspector";
        public const string Surveyor = "surveyor";
        public const string Security = "site_security";
        public const string Architect = "architect";
        public const string Draughting = "draughtsperson";
        public const string Engineer = "engineer";
        public const string Estimator = "estimator";

        public const int DailyShifts = 2;            // shifts of one gig a day: a client does not want the same hand three times, and the Gig Line says how many are left
        public const int MaxLevel = 4;
        public const float LevelBonus = 0.06f;       // pay per level above the first: a spammed gig reaches the top level in two days, so it is a smaller step now
        public const int ReferenceLevel = 2;         // the level in any job that earns a reference letter
        public const int StrikesToSack = 3;          // failed shifts in a job before it lets the player go
        public const int SackedDays = 3;             // days before it takes them back
        public const int CleanDays = 2;              // days after a police stop on a job before "clean record" holds again
        public const float MinEnergy = 15f;          // nobody is given work when this tired
        public const float PoliceStopCut = 0.25f;    // share of a unit's pay lost for being pulled over while on it
        public const float CrashCut = 0.15f;         // ...and for each crash

        private const string CustomerService = "ba:skill_customerservice";
        private const string DeliveryDriver = "ba:skill_deliverydriver";
        private const string Cleaning = "ba:skill_cleaning";

        /// <summary>
        /// In the order the phone offers them: the first needs nothing, each later one something the earlier ones give. Each kind's
        /// unit pay is set by what an hour of it asks (effort, care, skill) against the game's cashier at 25 an hour: the idle ones pay
        /// about that, the involved ones half as much again, the ones that need a car, a licence and a reference twice and more. The
        /// day's amount is <see cref="DailyShifts"/> shifts of each kind (the Gig Line says how many are left).
        /// </summary>
        public static readonly JobDef[] All =
        {
            // 24 flyers a bundle, about four minutes of walking up to people: 4.4 a flyer is 106 a bundle, 26 an hour: the cashier's rate for easy, outdoor work.
            new JobDef(Flyers, "reallife_job_flyers_name", "reallife_job_flyers_pitch", "reallife_transaction_job_flyers",
                JobNeed.Outdoors, 4.4f, 0f, 0.35f, 0.01f, CustomerService, 24 * DailyShifts, 40, 24, 6),
            // A client's floor cleaned to 100 %, about four minutes with the walk in: eight units of 18 are 144 at 100 %, 36 an hour: heavier, and judged on the result.
            new JobDef(Cleaner, "reallife_job_cleaner_name", "reallife_job_cleaner_pitch", "reallife_transaction_job_cleaner",
                JobNeed.Outdoors, 18f, 0f, 0.9f, 0.03f, Cleaning, 8 * DailyShifts, 30, 8, 2),
            // Six boxes from the kerb to a shop's shelves, about four minutes: 29 a box is 174, 43 an hour: the hardest work on foot, carrying and then the right shelf.
            new JobDef(Stocker, "reallife_job_stocker_name", "reallife_job_stocker_pitch", "reallife_transaction_job_stocker",
                JobNeed.Outdoors, 29f, 0f, 1.3f, 0.02f, CustomerService, 6 * DailyShifts, 20, 6, 2),
            // Twelve customers at a pavement cart, about five minutes: 13 each is 156, 39 an hour, and tips for quick and right. Paid for standing at it.
            new JobDef(Vendor, "reallife_job_vendor_name", "reallife_job_vendor_pitch", "reallife_transaction_job_vendor",
                JobNeed.Outdoors, 13f, 0f, 0.6f, 0.04f, CustomerService, 12 * DailyShifts, 30, 12, 3, byHour: true),
            // Tips only: a song is 40 seconds and brings 8 to 33 by how busy the street is, the hour and how it was played; 26 an hour on an ordinary street.
            new JobDef(Busker, "reallife_job_busker_name", "reallife_job_busker_pitch", "reallife_transaction_job_busker",
                JobNeed.Outdoors, 13f, 0f, 0.7f, 0f, null, 8 * DailyShifts, 20, 8, 2),
            // Five parcels a round, about four minutes with a car: 38 a door plus the distance, about 240 a round, 60 an hour (a car and a driver's skill; the van job pays 100).
            new JobDef(Courier, "reallife_job_courier_name", "reallife_job_courier_pitch", "reallife_transaction_job_courier",
                JobNeed.Outdoors | JobNeed.Car, 38f, 0.065f, 1.6f, 0.04f, DeliveryDriver, 5 * DailyShifts, 15, 5, 2),
            // A 600 m fare: 16 + 84 = 100 and a tip of up to 30 %, about 1.1 times what a taxi charges the player (90): the best an hour, for a
            // licence, a reference and a clean record. Paid for being on the road.
            new JobDef(Taxi, "reallife_job_taxi_name", "reallife_job_taxi_pitch", "reallife_transaction_job_taxi",
                JobNeed.Outdoors | JobNeed.Car | JobNeed.Reference | JobNeed.CleanRecord, 16f, 0.14f, 2.2f, 0.05f, CustomerService, GigOffers.TaxiShift * DailyShifts, 12, 0, byHour: true)
        };

        public static JobDef Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            for (int i = 0; i < Career.Length; i++)
            {
                if (Career[i].Id == id)
                {
                    return Career[i];
                }
            }
            for (int i = 0; i < Trades.Length; i++)
            {
                if (Trades[i].Id == id)
                {
                    return Trades[i];
                }
            }
            return null;
        }

        /// <summary>Shifts only an employer gives (a dispatch plan, a lead's call); not on the phone and not in JobState's levels.</summary>
        public static readonly JobDef[] Career =
        {
            new JobDef(Dispatch, "reallife_job_dispatch_name", "reallife_job_dispatch_name", "reallife_transaction_job_dispatch",
                JobNeed.Outdoors, 42f, 0f, 0.8f, 0.06f, "ba:skill_logisticsmanager", 999, 0, 3),
            new JobDef(Lead, "reallife_job_lead_name", "reallife_job_lead_name", "reallife_transaction_job_lead",
                JobNeed.Outdoors, 36f, 0f, 0.7f, 0.05f, "ba:skill_hrmanager", 999, 0, 4)
        };

        /// <summary>
        /// The building trades, each a gig of its own with a level and a record (<see cref="Recorded"/>), taken at a works site
        /// or the architects' office. Four game hours a shift, as every gig: a trade that asks nothing pays about what a gig
        /// on foot does (44 to 51 an hour), one after a short course 70 to 80, a diploma's about 80, a degree's 90 to 120,
        /// as the professions' first rungs do (Careers/Work/ProCatalogue); the shift's mini-game moves it (Scripts/BuildTrades).
        /// </summary>
        public static readonly JobDef[] Trades =
        {
            // Eight loads dug, mixed or carried at 22: 176 a shift, 44 an hour. Open to anyone; it teaches masonry on the job.
            Trade(Labourer, JobNeed.Site, 22f, 1.2f, 16, 24, 8),
            // Six stretches of wall at 34: 204, 51 an hour, by how true they are laid. Open to anyone.
            Trade(Mason, JobNeed.Site, 34f, 1.1f, 12, 18, 6),
            Trade(Scaffolder, JobNeed.Site, 30f, 1.2f, 12, 18, 6),
            Trade(Painter, JobNeed.Site, 22f, 0.8f, 16, 24, 8),
            // A carpenter's diploma or joinery: 46 a run of tiles, 69 an hour.
            Trade(Roofer, JobNeed.Site, 46f, 1.1f, 12, 18, 6),
            // A diploma of the trade, or its skill: 52 and 50 a circuit or a run of pipe, about 78 an hour.
            Trade(Electrician, JobNeed.Site, 52f, 0.7f, 12, 18, 6),
            Trade(Plumber, JobNeed.Site, 50f, 0.8f, 12, 18, 6),
            // The crane ticket: eight loads set down at 40, 80 an hour.
            Trade(Crane, JobNeed.Site, 40f, 0.5f, 16, 24, 8),
            // A degree in building: eight items judged right at 55, 110 an hour; four corners at 110.
            Trade(Inspector, JobNeed.Site, 55f, 0.5f, 16, 24, 8),
            Trade(Surveyor, JobNeed.Site, 110f, 0.7f, 8, 12, 4),
            // The security licence: six rounds of the site at 46, 69 an hour, after its working hours.
            Trade(Security, JobNeed.Site, 46f, 0.6f, 12, 18, 6, byHour: true),
            // At the architects' office: a stage of a design at 160 (two or three to a commission, 80 to 120 an hour),
            // a draughtsperson's sheet at 60 (45 an hour), an engineer's frame at 220 (110), an estimator's tender at 120 (90).
            Trade(Architect, JobNeed.Office, 160f, 0.6f, 6, 6, 3),
            Trade(Draughting, JobNeed.Office, 60f, 0.5f, 9, 9, 3),
            Trade(Engineer, JobNeed.Office, 220f, 0.5f, 4, 6, 2),
            Trade(Estimator, JobNeed.Office, 120f, 0.5f, 6, 6, 3)
        };

        private static JobDef Trade(string id, JobNeed needs, float unitPay, float unitEnergy, int dailyUnits, int unitsPerLevel, int shiftUnits, bool byHour = false)
        {
            const string mod = "reallife_";
            return new JobDef(id, mod + "trades_" + id + "_name", mod + "trades_" + id + "_about", mod + "transaction_trades_" + id,
                (needs & JobNeed.Office) != 0 ? needs : needs | JobNeed.Outdoors, unitPay, 0f, unitEnergy, 0f, null, dailyUnits, unitsPerLevel, shiftUnits, 0, byHour);
        }

        /// <summary>True for one of the building trades (<see cref="Trades"/>).</summary>
        public static bool IsTrade(JobDef def) => def != null && (def.Needs & (JobNeed.Site | JobNeed.Office)) != 0;

        /// <summary>Every job that keeps a record in JobState (experience, strikes, the day's amount): the Gig Line's, then the trades.</summary>
        public static readonly JobDef[] Recorded = Join(All, Trades);

        private static JobDef[] Join(JobDef[] first, JobDef[] then)
        {
            var all = new JobDef[first.Length + then.Length];
            first.CopyTo(all, 0);
            then.CopyTo(all, first.Length);
            return all;
        }

        /// <summary>A job's place in <see cref="Recorded"/>, or -1 for one with no record (the two career-only kinds).</summary>
        public static int RecordOf(string id)
        {
            for (int i = 0; i < Recorded.Length; i++)
            {
                if (Recorded[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
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

        /// <summary>The level reached after so many units of work: 1 to <see cref="MaxLevel"/>.</summary>
        public static int LevelFor(JobDef def, int unitsDone)
        {
            int level = 1 + (def.UnitsPerLevel > 0 ? unitsDone / def.UnitsPerLevel : 0);
            return level > MaxLevel ? MaxLevel : level;
        }

        /// <summary>What a level multiplies pay by.</summary>
        public static float PayFactor(int level)
        {
            return 1f + LevelBonus * (level - 1);
        }

        /// <summary>
        /// Whether officers are about to move a street worker on: the police on and their presence above nothing. The busker's
        /// move-on and the cart's warning for calling out both ask this, so the Guide says one thing of them.
        /// </summary>
        public static bool OfficersAbout(bool police, int presence)
        {
            return police && presence > 0;
        }
    }
}
