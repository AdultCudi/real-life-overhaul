using System;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What an AI model's training settings say, as data for the old page and the kit's alike: a level's words (its units a day, the
    /// part on the player's own AI racks and the part rented with its cost a day) and the line under the level, which says how high the
    /// model may train, what holds it there and what raises it (a higher level, more AI racks, what can be rented). Choosing the line is
    /// a rule like any other, so EconomyCheck runs it (AiTrainingChecks). Game-free. COST: a few sums when a page is written, never per frame.
    /// </summary>
    public static class TrainingViews
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// A level's words with so much own AI compute for the model a day: none, all on its own racks, part on them and part rented at
        /// the rent's cost a day, or all rented. A model rents no more than <see cref="InfraCatalogue.RentedMost"/> a day.
        /// </summary>
        public static ViewLine Level(int level, long own, IStudioFormat fmt)
        {
            long units = InfraRules.TrainLevels[InfraRules.TrainLevel(level)];
            long mine = Math.Min(units, Math.Max(0L, own)), rented = Math.Min(units - mine, InfraCatalogue.RentedMost);
            string cost = fmt.Money(rented * InfraCatalogue.GpuUnitDay * InfraCatalogue.RentedGpuShare / 1000);
            if (units <= 0)
            {
                return Say("reallife_tech_product_train_none", ViewTone.Plain);
            }
            if (mine <= 0 && units <= InfraCatalogue.RentedMost)
            {
                return Say("reallife_tech_product_train", ViewTone.Plain, "units", Whole(units), "cost", cost);
            }
            return rented <= 0 ? Say("reallife_tech_product_train_own", ViewTone.Plain, "units", Whole(units))
                : Say("reallife_tech_product_train_split", ViewTone.Plain, "units", Whole(units), "own", Whole(mine), "rented", Whole(rented), "cost", cost);
        }

        /// <summary>
        /// The line under the level: room to train faster, the most the model's compute allows with what the next level needs of the
        /// player's own AI racks (<paramref name="perRack"/>: what an AI rack bought today gives), the top of the levels, or (a warning)
        /// a level set when there was more compute, which it now trains below.
        /// </summary>
        public static ViewLine Room(int level, long own, int perRack)
        {
            int at = InfraRules.TrainLevel(level), most = InfraRules.TrainMost(own);
            long mine = Math.Max(0L, own), rent = InfraCatalogue.RentedMost;
            if (at > most)
            {
                long pace = Math.Min(InfraRules.TrainLevels[at], mine + rent);
                return Say("reallife_tech_product_train_short", ViewTone.Warn, "own", Whole(mine), "rent", Whole(rent), "pace", Whole(pace));
            }
            if (at < most)
            {
                return mine > 0 ? Say("reallife_tech_product_train_room", ViewTone.Plain, "most", Whole(InfraRules.TrainLevels[most]), "own", Whole(mine), "rent", Whole(rent))
                    : Say("reallife_tech_product_train_rent_room", ViewTone.Plain, "most", Whole(InfraRules.TrainLevels[most]));
            }
            if (most + 1 >= InfraRules.TrainLevels.Length)
            {
                return Say("reallife_tech_product_train_peak", ViewTone.Plain);
            }
            long next = InfraRules.TrainLevels[most + 1], more = Math.Max(1L, next - rent - mine);
            string per = Whole(Math.Max(0, perRack));
            return mine > 0 ? Say("reallife_tech_product_train_top", ViewTone.Plain, "own", Whole(mine), "rent", Whole(rent), "next", Whole(next), "more", Whole(more), "per", per)
                : Say("reallife_tech_product_train_rent_top", ViewTone.Plain, "rent", Whole(rent), "next", Whole(next), "more", Whole(more), "per", per);
        }

        /// <summary>True when the line under the level points to the Infra tab, where AI racks are bought: at the most the compute allows, or below a level it no longer covers.</summary>
        public static bool WantsRacks(int level, long own)
        {
            int at = InfraRules.TrainLevel(level), most = InfraRules.TrainMost(own);
            return at > most || (at == most && most + 1 < InfraRules.TrainLevels.Length);
        }

        private static ViewLine Say(string key, ViewTone tone, params string[] pairs) => new ViewLine { Key = key, Pairs = pairs, Tone = tone };

        // A whole number as TechText.Whole writes it.
        private static string Whole(long n) => n.ToString("N0", Inv);
    }
}
