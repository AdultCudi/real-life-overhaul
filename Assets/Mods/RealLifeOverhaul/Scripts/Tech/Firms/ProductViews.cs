using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What a product's page says about its next feature, its model's training and why it has no customers, as data: each line is a
    /// text's key with the names and values that fill it, chosen here once for the old pages and the kit's alike (TechText writes
    /// them in the game's words). Choosing the line is a rule like any other, so EconomyCheck's TechPointsChecks run it, not only the
    /// figures under it. A withdrawn product, which nothing builds and nothing trains, says none of it. Game-free.
    /// COST: a few sums when a page is written, never per frame.
    /// </summary>
    public static class ProductViews
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The line about a product's next feature: what it costs in points still to put in or, once the work is in and a model's
        /// training is all it waits for, that it waits. Never a negative count of points (the work goes on building while a model
        /// trains, <see cref="FirmsRules.Wait"/>). <paramref name="markets"/> is the tech markets' wording, which names no rival of
        /// the product's own. Null for a withdrawn product, whose next feature will never be built.
        /// </summary>
        public static ViewLine NextFeature(Product p, bool markets)
        {
            FeatureWait wait = FirmsRules.Wait(p);
            if (wait.Closed)
            {
                return null;
            }
            string features = p.Features.ToString(Inv);
            if (markets)
            {
                return wait.Trains ? Say("reallife_tech_mkt_product_build_wait", ViewTone.Plain, "features", features, "ahead", Whole(wait.Ahead))
                    : Say("reallife_tech_mkt_product_build", ViewTone.Plain, "features", features, "next", Whole(wait.Points));
            }
            string rival = FirmsCatalogue.RivalName(FirmsCatalogue.RivalStock(p.K, p.S)), theirs = p.RivalFeatures.ToString(Inv);
            return wait.Trains ? Say("reallife_tech_product_build_wait", ViewTone.Plain, "features", features, "rival", rival, "theirs", theirs, "ahead", Whole(wait.Ahead))
                : Say("reallife_tech_product_build", ViewTone.Plain, "features", features, "rival", rival, "theirs", theirs, "next", Whole(wait.Points));
        }

        /// <summary>
        /// An AI model's training in words while any is still to come: what training is, and the days it takes at the model's daily
        /// level, or (a warning) that the level is none, so none comes. Null when nothing is short: any other product, a model whose
        /// training is in, and a withdrawn model, which the cloud does not train.
        /// </summary>
        public static ViewLine TrainingWait(Product p)
        {
            FeatureWait wait = FirmsRules.Wait(p);
            if (p.K != ProductKind.Model || wait.Closed || wait.Training <= 0)
            {
                return null;
            }
            if (wait.Days < 0)
            {
                return Say("reallife_tech_product_training_off", ViewTone.Warn);
            }
            int level = InfraRules.TrainLevels[TalentCatalogue.Clamp(p.Train, 0, InfraRules.TrainLevels.Length - 1)];
            return Say("reallife_tech_product_training_days", ViewTone.Plain, "units", level.ToString(Inv), "days", Whole(wait.Days));
        }

        /// <summary>
        /// Why a product that is not on sale yet has no customers, and what puts it on sale. Null for a product on sale, a withdrawn
        /// one and a game, whose page (the studio's) says it.
        /// </summary>
        public static ViewLine OffSale(Product p)
        {
            if (p.Stage != 0 || p.K == ProductKind.Game)
            {
                return null;
            }
            return FirmsRules.CanLaunch(p) ? Say("reallife_tech_product_off_sale_ready", ViewTone.Plain)
                : Say("reallife_tech_product_off_sale_short", ViewTone.Plain, "launch", FirmsCatalogue.LaunchFeatures.ToString(Inv), "features", p.Features.ToString(Inv));
        }

        private static ViewLine Say(string key, ViewTone tone, params string[] pairs) => new ViewLine { Key = key, Pairs = pairs, Tone = tone };

        // A whole number as TechText.Whole writes it.
        private static string Whole(long n) => n.ToString("N0", Inv);
    }
}
