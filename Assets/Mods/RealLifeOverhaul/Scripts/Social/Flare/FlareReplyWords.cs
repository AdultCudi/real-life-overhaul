namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The words of a bulk or a manager's answer to a review: for each set of businesses (food, fashion, fitness, nightlife, tech, shops in general, the
    /// professions) six ways to say thanks (the first three cheerful, the last three proud), five to say sorry and four for a review of three stars, each
    /// with the reviewer's name and the business's. The variant is rolled from the account's seed and the review, never the same twice running within a
    /// press, so a run of answers does not read like a stamp. Whole keys, so the catalogue checks each one is written. Game-free.
    /// </summary>
    public static class FlareReplyWords
    {
        public const int Sets = 7, ThanksPer = 6, SorryPer = 5, MixedPer = 4;
        public const int ProudFrom = 3;     // the thanks from this one on are in the proud voice

        public static readonly string[][] Thanks =
        {
            new[]
            {
                "reallife_flare_batch_thanks_food_1",
                "reallife_flare_batch_thanks_food_2",
                "reallife_flare_batch_thanks_food_3",
                "reallife_flare_batch_thanks_food_4",
                "reallife_flare_batch_thanks_food_5",
                "reallife_flare_batch_thanks_food_6"
            },
            new[]
            {
                "reallife_flare_batch_thanks_fashion_1",
                "reallife_flare_batch_thanks_fashion_2",
                "reallife_flare_batch_thanks_fashion_3",
                "reallife_flare_batch_thanks_fashion_4",
                "reallife_flare_batch_thanks_fashion_5",
                "reallife_flare_batch_thanks_fashion_6"
            },
            new[]
            {
                "reallife_flare_batch_thanks_fitness_1",
                "reallife_flare_batch_thanks_fitness_2",
                "reallife_flare_batch_thanks_fitness_3",
                "reallife_flare_batch_thanks_fitness_4",
                "reallife_flare_batch_thanks_fitness_5",
                "reallife_flare_batch_thanks_fitness_6"
            },
            new[]
            {
                "reallife_flare_batch_thanks_night_1",
                "reallife_flare_batch_thanks_night_2",
                "reallife_flare_batch_thanks_night_3",
                "reallife_flare_batch_thanks_night_4",
                "reallife_flare_batch_thanks_night_5",
                "reallife_flare_batch_thanks_night_6"
            },
            new[]
            {
                "reallife_flare_batch_thanks_tech_1",
                "reallife_flare_batch_thanks_tech_2",
                "reallife_flare_batch_thanks_tech_3",
                "reallife_flare_batch_thanks_tech_4",
                "reallife_flare_batch_thanks_tech_5",
                "reallife_flare_batch_thanks_tech_6"
            },
            new[]
            {
                "reallife_flare_batch_thanks_shop_1",
                "reallife_flare_batch_thanks_shop_2",
                "reallife_flare_batch_thanks_shop_3",
                "reallife_flare_batch_thanks_shop_4",
                "reallife_flare_batch_thanks_shop_5",
                "reallife_flare_batch_thanks_shop_6"
            },
            new[]
            {
                "reallife_flare_batch_thanks_pro_1",
                "reallife_flare_batch_thanks_pro_2",
                "reallife_flare_batch_thanks_pro_3",
                "reallife_flare_batch_thanks_pro_4",
                "reallife_flare_batch_thanks_pro_5",
                "reallife_flare_batch_thanks_pro_6"
            }
        };

        public static readonly string[][] Sorry =
        {
            new[]
            {
                "reallife_flare_batch_sorry_food_1",
                "reallife_flare_batch_sorry_food_2",
                "reallife_flare_batch_sorry_food_3",
                "reallife_flare_batch_sorry_food_4",
                "reallife_flare_batch_sorry_food_5"
            },
            new[]
            {
                "reallife_flare_batch_sorry_fashion_1",
                "reallife_flare_batch_sorry_fashion_2",
                "reallife_flare_batch_sorry_fashion_3",
                "reallife_flare_batch_sorry_fashion_4",
                "reallife_flare_batch_sorry_fashion_5"
            },
            new[]
            {
                "reallife_flare_batch_sorry_fitness_1",
                "reallife_flare_batch_sorry_fitness_2",
                "reallife_flare_batch_sorry_fitness_3",
                "reallife_flare_batch_sorry_fitness_4",
                "reallife_flare_batch_sorry_fitness_5"
            },
            new[]
            {
                "reallife_flare_batch_sorry_night_1",
                "reallife_flare_batch_sorry_night_2",
                "reallife_flare_batch_sorry_night_3",
                "reallife_flare_batch_sorry_night_4",
                "reallife_flare_batch_sorry_night_5"
            },
            new[]
            {
                "reallife_flare_batch_sorry_tech_1",
                "reallife_flare_batch_sorry_tech_2",
                "reallife_flare_batch_sorry_tech_3",
                "reallife_flare_batch_sorry_tech_4",
                "reallife_flare_batch_sorry_tech_5"
            },
            new[]
            {
                "reallife_flare_batch_sorry_shop_1",
                "reallife_flare_batch_sorry_shop_2",
                "reallife_flare_batch_sorry_shop_3",
                "reallife_flare_batch_sorry_shop_4",
                "reallife_flare_batch_sorry_shop_5"
            },
            new[]
            {
                "reallife_flare_batch_sorry_pro_1",
                "reallife_flare_batch_sorry_pro_2",
                "reallife_flare_batch_sorry_pro_3",
                "reallife_flare_batch_sorry_pro_4",
                "reallife_flare_batch_sorry_pro_5"
            }
        };

        public static readonly string[][] Mixed =
        {
            new[]
            {
                "reallife_flare_batch_mixed_food_1",
                "reallife_flare_batch_mixed_food_2",
                "reallife_flare_batch_mixed_food_3",
                "reallife_flare_batch_mixed_food_4"
            },
            new[]
            {
                "reallife_flare_batch_mixed_fashion_1",
                "reallife_flare_batch_mixed_fashion_2",
                "reallife_flare_batch_mixed_fashion_3",
                "reallife_flare_batch_mixed_fashion_4"
            },
            new[]
            {
                "reallife_flare_batch_mixed_fitness_1",
                "reallife_flare_batch_mixed_fitness_2",
                "reallife_flare_batch_mixed_fitness_3",
                "reallife_flare_batch_mixed_fitness_4"
            },
            new[]
            {
                "reallife_flare_batch_mixed_night_1",
                "reallife_flare_batch_mixed_night_2",
                "reallife_flare_batch_mixed_night_3",
                "reallife_flare_batch_mixed_night_4"
            },
            new[]
            {
                "reallife_flare_batch_mixed_tech_1",
                "reallife_flare_batch_mixed_tech_2",
                "reallife_flare_batch_mixed_tech_3",
                "reallife_flare_batch_mixed_tech_4"
            },
            new[]
            {
                "reallife_flare_batch_mixed_shop_1",
                "reallife_flare_batch_mixed_shop_2",
                "reallife_flare_batch_mixed_shop_3",
                "reallife_flare_batch_mixed_shop_4"
            },
            new[]
            {
                "reallife_flare_batch_mixed_pro_1",
                "reallife_flare_batch_mixed_pro_2",
                "reallife_flare_batch_mixed_pro_3",
                "reallife_flare_batch_mixed_pro_4"
            }
        };

        /// <summary>The set of words for a business of this niche: one of the seven; the local news niche has the shop's.</summary>
        public static int SetOf(FlareNiche niche)
        {
            switch (niche)
            {
                case FlareNiche.Food: return 0;
                case FlareNiche.Fashion: return 1;
                case FlareNiche.Fitness: return 2;
                case FlareNiche.Nightlife: return 3;
                case FlareNiche.Tech: return 4;
                case FlareNiche.Business: return 6;
                default: return 5;
            }
        }

        /// <summary>How many variants a way has.</summary>
        public static int Count(FlareBatchWay way)
        {
            return way == FlareBatchWay.Thanks ? ThanksPer : way == FlareBatchWay.Sorry ? SorryPer : MixedPer;
        }

        /// <summary>A variant for a review: rolled from the seed and the review's key on a stream of its own, moved on by one when it would repeat the one before (-1 for none).</summary>
        public static int Variant(FlareBatchWay way, int seed, int key, int previous)
        {
            int n = Count(way);
            int v = FlareRules.Roll(seed, key, Salt) % n;
            return v == previous && n > 1 ? (v + 1) % n : v;
        }

        private const int Salt = 1417;   // the stream of the batch answers' words: no other roll uses it

        /// <summary>The key of one variant for a business's set.</summary>
        public static string Key(FlareBatchWay way, int set, int variant)
        {
            set = set < 0 ? 0 : set >= Sets ? Sets - 1 : set;
            string[][] table = way == FlareBatchWay.Thanks ? Thanks : way == FlareBatchWay.Sorry ? Sorry : Mixed;
            string[] row = table[set];
            return row[variant < 0 ? 0 : variant >= row.Length ? row.Length - 1 : variant];
        }

        /// <summary>The tone a variant is posted in: the last three thanks are proud, the other thanks cheerful, the rest honest.</summary>
        public static FlareTone Tone(FlareBatchWay way, int variant)
        {
            return way != FlareBatchWay.Thanks ? FlareTone.Honest : variant >= ProudFrom ? FlareTone.Proud : FlareTone.Cheerful;
        }
    }
}
