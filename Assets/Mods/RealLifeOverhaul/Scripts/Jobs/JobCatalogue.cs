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
        Outdoors = 8        // taken on the street, not inside a building
    }

    /// <summary>
    /// One job: who can take it, what a unit of its work (a fare, a parcel, a flyer) pays, what
    /// it costs in energy, how much of it there is in a day and how experience in it is counted.
    /// Game-free, like the event and crime catalogues: every number that balances a job is here
    /// and nowhere else.
    ///
    /// Balanced against what the game itself pays, read from its data files by
    /// JobAssets/scripts/extract_wages.py (one game hour is one minute of play):
    ///  - the one employer job, cashier at El Gato Food Market: 25 an hour, 8 to 16, six days;
    ///  - what the game pays its own staff at no skill: cleaning 12 an hour, customer service
    ///    16, delivery driver 18 (times 1 + skill^1.05 / 100);
    ///  - the delivery van job: 100 a drop (150 at one depot), three drops, about three game
    ///    hours with the van to bring back: roughly 100 an hour, plus tips of about 5 a drop;
    ///  - food delivery: 25 + 0.05 a metre within 500 m, so 25 to 50 a delivery, tips of about 3;
    ///  - a taxi charges the player 0.15 a metre.
    /// The jobs here need no diploma, building, stock or fixed hours, so the ones on foot pay
    /// about what the game pays unskilled staff, a little under the cashier's 25 until a few
    /// levels in: flyers about 10 to 15 an hour, cleaner 14, shelf stocker 18, vendor 16 plus
    /// tips, busker 8 to 20 by where and when. The ones that need a car pay more, and less than
    /// the van job for the same time: courier 45 to 60, ride-hail 50 to 80 once it is earned.
    /// </summary>
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
        public readonly int ShiftUnits;          // how many units one shift is (0 = until the player stops)

        public JobDef(string id, string nameKey, string pitchKey, string transactionKey, JobNeed needs, float unitPay, float payPerMetre,
            float unitEnergy, float unitSkill, string skill, int dailyUnits, int unitsPerLevel, int shiftUnits)
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
        }
    }

    public static class JobCatalogue
    {
        public const string Flyers = "flyers";
        public const string Courier = "courier";
        public const string Taxi = "taxi";
        public const string Cleaner = "cleaner";
        public const string Stocker = "stocker";
        public const string Vendor = "vendor";
        public const string Busker = "busker";
        public const string Dispatch = "dispatch";   // the two career-only kinds of shift: never on the Gig Line, no record in JobState
        public const string Lead = "lead";

        public const int MaxLevel = 4;
        public const float LevelBonus = 0.08f;       // pay per level above the first
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

        /// <summary>In the order the phone offers them: the first needs nothing, each later one something the earlier ones give.</summary>
        public static readonly JobDef[] All =
        {
            // 24 flyers a bundle, about four minutes of walking up to people: about 60 for a bundle with a fair share taken.
            new JobDef(Flyers, "reallife_job_flyers_name", "reallife_job_flyers_pitch", "reallife_transaction_job_flyers",
                JobNeed.Outdoors, 2.5f, 0f, 0.35f, 0.01f, CustomerService, 48, 40, 24),
            // Eight patches of a client's floor, about four minutes with the walk in: 7 a patch is 56, cleaning's 12 an hour and a little over.
            new JobDef(Cleaner, "reallife_job_cleaner_name", "reallife_job_cleaner_pitch", "reallife_transaction_job_cleaner",
                JobNeed.Outdoors, 7f, 0f, 0.9f, 0.03f, Cleaning, 24, 30, 8),
            // Six boxes from the kerb to a shop's shelves, about four minutes: 12 a box is 72, 18 an hour.
            new JobDef(Stocker, "reallife_job_stocker_name", "reallife_job_stocker_pitch", "reallife_transaction_job_stocker",
                JobNeed.Outdoors, 12f, 0f, 1.3f, 0.02f, CustomerService, 18, 20, 6),
            // Twelve customers at a pavement cart, about five minutes: 6.5 each is 78, customer service's 16 an hour, and tips for quick and right.
            new JobDef(Vendor, "reallife_job_vendor_name", "reallife_job_vendor_pitch", "reallife_transaction_job_vendor",
                JobNeed.Outdoors, 6.5f, 0f, 0.6f, 0.04f, CustomerService, 36, 30, 12),
            // Tips only: a song is 40 seconds and brings 4 to 16 by how busy the street is, the hour and how it was played.
            new JobDef(Busker, "reallife_job_busker_name", "reallife_job_busker_pitch", "reallife_transaction_job_busker",
                JobNeed.Outdoors, 6f, 0f, 0.7f, 0f, null, 16, 20, 8),
            // Five parcels a round, about four minutes with a car: 18 a door plus the distance, about 110 to 130 a round.
            new JobDef(Courier, "reallife_job_courier_name", "reallife_job_courier_pitch", "reallife_transaction_job_courier",
                JobNeed.Outdoors | JobNeed.Car, 18f, 0.03f, 1.6f, 0.04f, DeliveryDriver, 15, 15, 5),
            // A fare of 600 m: 8 + 42 = 50, with a tip of up to 30 % for a smooth, quick ride. A third of what a taxi charges the player.
            new JobDef(Taxi, "reallife_job_taxi_name", "reallife_job_taxi_pitch", "reallife_transaction_job_taxi",
                JobNeed.Outdoors | JobNeed.Car | JobNeed.Reference | JobNeed.CleanRecord, 8f, 0.07f, 2.2f, 0.05f, CustomerService, 14, 12, 0)
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
            return null;
        }

        /// <summary>
        /// Shifts only an employer gives (Jobs/Career*): a unit is a plan made at the dispatch desk (14 for a perfect
        /// one, less by how far from the best it was) or a call made well by the one in charge (12). Most of their pay
        /// is the rank's hourly salary. Not offered on the phone and not counted in JobState's levels.
        /// </summary>
        public static readonly JobDef[] Career =
        {
            new JobDef(Dispatch, "reallife_job_dispatch_name", "reallife_job_dispatch_name", "reallife_transaction_job_dispatch",
                JobNeed.Outdoors, 14f, 0f, 0.8f, 0.06f, "ba:skill_logisticsmanager", 999, 0, 3),
            new JobDef(Lead, "reallife_job_lead_name", "reallife_job_lead_name", "reallife_transaction_job_lead",
                JobNeed.Outdoors, 12f, 0f, 0.7f, 0.05f, "ba:skill_hrmanager", 999, 0, 4)
        };

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
    }
}
