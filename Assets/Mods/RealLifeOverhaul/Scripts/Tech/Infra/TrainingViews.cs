using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// An AI model's training as its page shows it, all from the day's own rule (<see cref="InfraRules.ModelsDay"/>) with the player's AI racks
    /// at full work: at each level, what the day gives it on the racks and rented; the level it is set to; the highest it may be set to and the
    /// own compute that rests on; what an AI rack bought today gives. Game-free; made by <see cref="TrainingViews.Look"/> when a page is written.
    /// </summary>
    public sealed class TrainingLook
    {
        public readonly long[] Own = new long[InfraRules.TrainLevels.Length];      // at each level, its training a day on the player's own AI racks...
        public readonly long[] Rented = new long[InfraRules.TrainLevels.Length];   // ...and rented
        public int Level;      // the level it is set to, read safely
        public int Most;       // the highest it may be set to (InfraRules.TrainMost of Keep)
        public long Keep;      // the own AI compute a day its part past what it may rent may take without another model's (ModelCompute.Keep)
        public int PerRack;    // what an AI rack bought today gives a day

        /// <summary>What the day trains it on at a level: on the racks and rented, less than the level when the racks fall short.</summary>
        public long Pace(int level) => Own[level] + Rented[level];
    }

    /// <summary>
    /// What an AI model's training settings say, as data for the old page and the kit's alike: a level's words (its units a day, the
    /// part on the player's own AI racks and the part rented with its cost a day) and the line under the level, which says how high the
    /// model may train, what holds it there and what raises it (a higher level, more AI racks, what can be rented); and the cloud page's
    /// line on what the player's own models take of the AI racks before the cloud sells any. Every figure is the day's own rule's
    /// (<see cref="InfraRules.ModelsDay"/>), so the page says what the day does. Choosing the line is a rule like any other, so EconomyCheck
    /// runs it (AiTrainingChecks). Game-free. COST: the day's rule once for each level when a page is written, never per frame.
    /// </summary>
    public static class TrainingViews
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// A model's training as its page shows it: the product at <paramref name="index"/> of the player's products, with so much AI compute
        /// of the player's racks a day (<paramref name="racks"/>), reckoned by the day's own rule at each of the levels; <paramref name="work"/>
        /// is the rule's list, reused. A product that is not there reads as a model with no racks.
        /// </summary>
        public static TrainingLook Look(IList<Product> products, int index, long racks, int perRack, List<ModelCompute> work)
        {
            var t = new TrainingLook { PerRack = Math.Max(0, perRack) };
            bool found = products != null && index >= 0 && index < products.Count;
            t.Level = InfraRules.TrainLevel(found ? products[index].Train : 0);
            for (int level = 0; level < InfraRules.TrainLevels.Length; level++)
            {
                if (found)
                {
                    InfraRules.ModelsDay(products, racks, work, index, level);
                    t.Own[level] = work[index].Own;
                    t.Rented[level] = work[index].Rented;
                    t.Keep = work[index].Keep;
                }
                else
                {
                    t.Rented[level] = Math.Min(InfraRules.TrainLevels[level], InfraCatalogue.RentedMost);
                }
            }
            t.Most = InfraRules.TrainMost(t.Keep);
            return t;
        }

        /// <summary>
        /// A level's words: none, all on the player's own AI racks, part on them and part rented at the rent's cost a day, or all rented, as the
        /// day gives it them. A model rents no more than <see cref="InfraCatalogue.RentedMost"/> a day.
        /// </summary>
        public static ViewLine Level(TrainingLook t, int level, IStudioFormat fmt)
        {
            int at = InfraRules.TrainLevel(level);
            long units = InfraRules.TrainLevels[at], mine = t.Own[at], rented = t.Rented[at];
            string cost = fmt.Money(InfraRules.RentCost(rented));
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
        /// The line under the level it is set to: (a warning) the racks fall short of it, so it trains on less; (a warning) it still trains at a
        /// level set when there was more compute, ahead of a model after it that trains less; room to train faster, with what the day gives it on
        /// the racks at the most; the top of the levels; or, at the most, what the next level needs of the player's own AI racks and what an AI
        /// rack bought now gives (<see cref="TrainingLook.PerRack"/>).
        /// </summary>
        public static ViewLine Room(TrainingLook t)
        {
            int at = t.Level, most = t.Most;
            long rent = InfraCatalogue.RentedMost, pace = t.Pace(at);
            if (pace < InfraRules.TrainLevels[at])
            {
                return Say("reallife_tech_product_train_short", ViewTone.Warn, "own", Whole(t.Own[at]), "rent", Whole(rent), "pace", Whole(pace));
            }
            if (at > most)
            {
                return Say("reallife_tech_product_train_over", ViewTone.Warn);
            }
            if (at < most)
            {
                return t.Own[most] > 0 ? Say("reallife_tech_product_train_room", ViewTone.Plain, "most", Whole(InfraRules.TrainLevels[most]), "own", Whole(t.Own[most]), "rent", Whole(rent))
                    : Say("reallife_tech_product_train_rent_room", ViewTone.Plain, "most", Whole(InfraRules.TrainLevels[most]));
            }
            if (most + 1 >= InfraRules.TrainLevels.Length)
            {
                return Say("reallife_tech_product_train_peak", ViewTone.Plain);
            }
            long next = InfraRules.TrainLevels[most + 1], more = Math.Max(1L, next - rent - Math.Max(0L, t.Keep));   // its part past the rent must fit in what may be kept for it
            string per = Whole(t.PerRack);
            return t.Keep > 0 ? Say("reallife_tech_product_train_top", ViewTone.Plain, "next", Whole(next), "more", Whole(more), "per", per)
                : Say("reallife_tech_product_train_rent_top", ViewTone.Plain, "rent", Whole(rent), "next", Whole(next), "more", Whole(more), "per", per);
        }

        /// <summary>True when the line under the level points to the Infra tab, where AI racks are bought: at the most the compute allows, or past it.</summary>
        public static bool WantsRacks(TrainingLook t) => t.Level > t.Most || (t.Level == t.Most && t.Most + 1 < InfraRules.TrainLevels.Length);

        /// <summary>
        /// The cloud page's line on the AI racks: what the player's own models take of them a day before the cloud sells any (their training and
        /// their customers' use, from <paramref name="day"/>, the day's own rule on the racks' <paramref name="racks"/>) and the AI compute the cloud
        /// sold yesterday, so a fall in its sales has its reason beside it. Null with no AI racks or no model on them.
        /// </summary>
        public static ViewLine Racks(long racks, List<ModelCompute> day, int sold)
        {
            long train = 0, run = 0;
            for (int i = 0; day != null && i < day.Count; i++)
            {
                train += day[i].Own;
                run += day[i].RunOwn;
            }
            return racks <= 0 || train + run <= 0 ? null
                : Say("reallife_infra_cloud_gpu_models", ViewTone.Plain, "train", Whole(train), "run", Whole(run), "sold", Whole(Math.Max(0, sold)));
        }

        private static ViewLine Say(string key, ViewTone tone, params string[] pairs) => new ViewLine { Key = key, Pairs = pairs, Tone = tone };

        // A whole number as TechText.Whole writes it.
        private static string Whole(long n) => n.ToString("N0", Inv);
    }
}
