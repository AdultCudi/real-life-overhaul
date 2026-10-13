using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The words of Flare. A post is kept as a source, a tone, a person and a seed (FlarePost); here it becomes words,
    /// when it is shown, from en.json's templates: the seed picks the variant, so a post always reads the same. Every
    /// text is a key; nothing here is kept. Called when the app shows something or a notification is made, never
    /// otherwise.
    /// </summary>
    public static class FlareText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly StringBuilder Words = new StringBuilder(256);

        public static readonly string[] TabKeys = { "reallife_flare_tab_feed", "reallife_flare_tab_post", "reallife_flare_tab_me", "reallife_flare_tab_business", "reallife_flare_tab_city" };
        public static readonly string[] TierKeys = { "reallife_flare_tier_0", "reallife_flare_tier_1", "reallife_flare_tier_2", "reallife_flare_tier_3", "reallife_flare_tier_4" };
        public static readonly string[] ToneKeys = { "reallife_flare_tone_0", "reallife_flare_tone_1", "reallife_flare_tone_2", "reallife_flare_tone_3" };
        public static readonly string[] OutingKeys =
        {
            "reallife_flare_outing_0", "reallife_flare_outing_1", "reallife_flare_outing_2", "reallife_flare_outing_3", "reallife_flare_outing_4",
            "reallife_flare_outing_5", "reallife_flare_outing_6", "reallife_flare_outing_7", "reallife_flare_outing_8", "reallife_flare_outing_9"
        };
        public const int PostVariants = 2, ToneVariants = 2, ReplyVariants = 3;

        // ---- every key written out in full, by FlareSource (the locale check finds each one in en.json)
        private static readonly string[] DraftKeys =
        {
            "reallife_flare_draft_photo", "reallife_flare_draft_photo", "reallife_flare_draft_selfie", "reallife_flare_draft_outing", "reallife_flare_draft_promotion",
            "reallife_flare_draft_opened", "reallife_flare_draft_record", "reallife_flare_draft_voted", "reallife_flare_draft_endorsed", "reallife_flare_draft_deed",
            "reallife_flare_draft_arrested", "reallife_flare_draft_photo", "reallife_flare_draft_scandal", "reallife_flare_draft_photo",
            "reallife_flare_draft_photo", "reallife_flare_draft_photo", "reallife_flare_draft_photo", "reallife_flare_draft_photo", "reallife_flare_draft_photo",
            "reallife_flare_draft_photo", "reallife_flare_draft_photo", "reallife_flare_draft_photo", "reallife_flare_draft_shares", "reallife_flare_draft_photo",
            "reallife_flare_draft_photo", "reallife_flare_draft_issue_safety", "reallife_flare_draft_campaign", "reallife_flare_draft_graduated"
        };
        private static readonly string[][] PostKeys =
        {
            new[] { "reallife_flare_post_photo_1", "reallife_flare_post_photo_2" },
            new[] { "reallife_flare_post_photo_1", "reallife_flare_post_photo_2" },
            new[] { "reallife_flare_post_selfie_1", "reallife_flare_post_selfie_2" },
            new[] { "reallife_flare_post_outing_1", "reallife_flare_post_outing_2" },
            new[] { "reallife_flare_post_promotion_1", "reallife_flare_post_promotion_2" },
            new[] { "reallife_flare_post_opened_1", "reallife_flare_post_opened_2" },
            new[] { "reallife_flare_post_record_1", "reallife_flare_post_record_2" },
            new[] { "reallife_flare_post_voted_1", "reallife_flare_post_voted_2" },
            new[] { "reallife_flare_post_endorsed_1", "reallife_flare_post_endorsed_2" },
            new[] { "reallife_flare_post_deed_1", "reallife_flare_post_deed_2" },
            new[] { "reallife_flare_post_arrested_1", "reallife_flare_post_arrested_2" },
            new[] { "reallife_flare_post_photo_1", "reallife_flare_post_photo_2" },
            new[] { "reallife_flare_post_scandal_1", "reallife_flare_post_scandal_2" },
            new[] { "reallife_flare_post_sponsored_1", "reallife_flare_post_sponsored_2" },
            null, null, null, null, null, null, null, null,   // the city's own accounts' sources: worded in Other
            new[] { "reallife_flare_post_shares_1", "reallife_flare_post_shares_2" },
            null, null,
            new[] { "reallife_flare_post_issue_safety_1", "reallife_flare_post_issue_safety_2" },
            new[] { "reallife_flare_post_campaign_1", "reallife_flare_post_campaign_2" },
            new[] { "reallife_flare_post_graduated_1", "reallife_flare_post_graduated_2" }
        };
        private static readonly string[] RoadsKeys = { "reallife_flare_post_issue_roads_1", "reallife_flare_post_issue_roads_2" };
        private static readonly string[][] ToneLineKeys =
        {
            new[] { "reallife_flare_tone_0_1", "reallife_flare_tone_0_2" },
            new[] { "reallife_flare_tone_1_1", "reallife_flare_tone_1_2" },
            new[] { "reallife_flare_tone_2_1", "reallife_flare_tone_2_2" },
            new[] { "reallife_flare_tone_3_1", "reallife_flare_tone_3_2" }
        };
        private static readonly string[][] ReplyKeys =
        {
            new[] { "reallife_flare_reply_plain_1", "reallife_flare_reply_plain_2", "reallife_flare_reply_plain_3" },
            new[] { "reallife_flare_reply_cheer_1", "reallife_flare_reply_cheer_2", "reallife_flare_reply_cheer_3" },
            new[] { "reallife_flare_reply_proud_1", "reallife_flare_reply_proud_2", "reallife_flare_reply_proud_3" },
            new[] { "reallife_flare_reply_cool_1", "reallife_flare_reply_cool_2", "reallife_flare_reply_cool_3" },
            new[] { "reallife_flare_reply_kind_1", "reallife_flare_reply_kind_2", "reallife_flare_reply_kind_3" },
            new[] { "reallife_flare_reply_love_1", "reallife_flare_reply_love_2", "reallife_flare_reply_love_3" },
            new[] { "reallife_flare_reply_funny_1", "reallife_flare_reply_funny_2", "reallife_flare_reply_funny_3" }
        };
        private static readonly string[][] LifeKeys =
        {
            new[] { "reallife_flare_life_work_0", "reallife_flare_life_work_1" },
            new[] { "reallife_flare_life_district_0", "reallife_flare_life_district_1" },
            new[] { "reallife_flare_life_sport_0", "reallife_flare_life_sport_1" },
            new[] { "reallife_flare_life_food_0", "reallife_flare_life_food_1" },
            new[] { "reallife_flare_life_night_0", "reallife_flare_life_night_1" },
            new[] { "reallife_flare_life_shops_0", "reallife_flare_life_shops_1" },
            new[] { "reallife_flare_life_plain_0", "reallife_flare_life_plain_1" }
        };

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>"Label: value" with the value in bold, as the game's white box writes a line.</summary>
        public static string Pair(string labelKey, string value)
        {
            return T(labelKey) + " <b>" + value + "</b>";
        }

        public static string N(int value)
        {
            return value.ToString("N0", Inv);
        }

        /// <summary>An amount of money as the game writes it.</summary>
        public static string Money(float amount)
        {
            return amount.ToShortCurrencyFormat();
        }

        public static string Signed(int value)
        {
            return (value > 0 ? "+" : string.Empty) + N(value);
        }

        public static string Tier(FlareTier tier)
        {
            return T(TierKeys[FlareRules.Clamp((int)tier, 0, TierKeys.Length - 1)]);
        }

        public static string Tone(int tone)
        {
            return T(ToneKeys[FlareRules.Clamp(tone, 0, ToneKeys.Length - 1)]);
        }

        public static string Outing(int kind)
        {
            return T(OutingKeys[FlareRules.Clamp(kind, 0, OutingKeys.Length - 1)]);
        }

        /// <summary>A day and an hour as the app shows when something was: "Day 12, 14:00".</summary>
        public static string When(int day, int hour)
        {
            return Fill("reallife_flare_when", "day", day.ToString(Inv), "time", FlareRules.Clamp(hour, 0, 23).ToString("00", Inv) + ":00");
        }

        public static string Clock(int minuteOfDay)
        {
            return (minuteOfDay / 60).ToString("00", Inv) + ":" + (minuteOfDay % 60).ToString("00", Inv);
        }

        /// <summary>How the city reads the account: one of five words.</summary>
        public static string Reputation(int reputation)
        {
            return T(reputation < 25 ? "reallife_flare_rep_1" : reputation < 45 ? "reallife_flare_rep_2" : reputation < 65 ? "reallife_flare_rep_3" : reputation < 85 ? "reallife_flare_rep_4" : "reallife_flare_rep_5");
        }

        /// <summary>What a draft is, in a line: "A selfie with Alex", "Record day at Copper Kettle".</summary>
        public static string Draft(FlareDraft d, string friend)
        {
            bool with = d.Person >= 0 && !string.IsNullOrEmpty(friend) && (d.Source == FlareSource.Selfie || d.Source == FlareSource.Photo);
            string key = with ? (d.Source == FlareSource.Selfie ? "reallife_flare_draft_selfie_with" : "reallife_flare_draft_photo_with")
                : d.Source == FlareSource.Issue && d.Arg == (int)FlareIssue.Roads ? "reallife_flare_draft_issue_roads" : Of(DraftKeys, (int)d.Source);
            return Fill(key, "name", d.Name, "friend", friend, "kind", Outing(d.Arg));
        }

        /// <summary>The name of whoever wrote a post: "You", or the friend's first name.</summary>
        public static string Author(FlarePost p, string friend)
        {
            return p.Author == FlareAuthor.Player ? T("reallife_flare_you") : friend ?? "?";
        }

        /// <summary>
        /// The words of a post: the player's, a template of its source with the tone's line after it and the tag; a
        /// friend's, a template of its topic with their job or part of town.
        /// </summary>
        public static string Post(FlarePost p, string tagged, string job, string district, int niche = 0)
        {
            Words.Length = 0;
            if (p.Author >= FlareAuthor.Influencer)
            {
                return Other(p, district, niche);
            }
            if (p.Author == FlareAuthor.Friend)
            {
                string[] topic = LifeKeys[FlareRules.Clamp(p.Arg / 10, 0, LifeKeys.Length - 1)];
                Words.Append(Fill(topic[p.Arg % 10 % topic.Length], "job", job, "district", district));
                return Words.ToString();
            }
            string[] post = p.Source == FlareSource.Issue && p.Arg == (int)FlareIssue.Roads ? RoadsKeys : PostKeys[FlareRules.Clamp((int)p.Source, 0, PostKeys.Length - 1)] ?? PostKeys[1];
            Words.Append(Fill(post[Pick(p.Seed, 1, post.Length) - 1], "name", p.Name, "kind", Outing(p.Arg)));
            if (p.Disclosed)
            {
                Words.Append(' ').Append(Fill("reallife_flare_disclosure", "name", p.Name));
            }
            if (!string.IsNullOrEmpty(tagged))
            {
                Words.Append(' ').Append(Fill("reallife_flare_with", "friend", tagged));
            }
            string[] tone = ToneLineKeys[FlareRules.Clamp((int)p.Tone, 0, ToneLineKeys.Length - 1)];
            Words.Append(' ').Append(T(tone[Pick(p.Seed, 2, tone.Length) - 1]));
            return Words.ToString();
        }

        public static readonly string[] NicheKeys =
        {
            "reallife_flare_niche_0", "reallife_flare_niche_1", "reallife_flare_niche_2", "reallife_flare_niche_3", "reallife_flare_niche_4", "reallife_flare_niche_5", "reallife_flare_niche_6"
        };
        public static readonly string[] StyleKeys = { "reallife_flare_style_0", "reallife_flare_style_1", "reallife_flare_style_2", "reallife_flare_style_3" };
        public static readonly string[] DealKeys = { "reallife_flare_deal_0", "reallife_flare_deal_1", "reallife_flare_deal_2", "reallife_flare_deal_3", "reallife_flare_deal_4", "reallife_flare_deal_5" };
        public static readonly string[] DealAboutKeys =
        {
            "reallife_flare_deal_about_0", "reallife_flare_deal_about_1", "reallife_flare_deal_about_2", "reallife_flare_deal_about_3", "reallife_flare_deal_about_4", "reallife_flare_deal_about_5"
        };
        private static readonly string[][] NichePostKeys =
        {
            new[] { "reallife_flare_inf_food_1", "reallife_flare_inf_food_2" },
            new[] { "reallife_flare_inf_fashion_1", "reallife_flare_inf_fashion_2" },
            new[] { "reallife_flare_inf_fitness_1", "reallife_flare_inf_fitness_2" },
            new[] { "reallife_flare_inf_night_1", "reallife_flare_inf_night_2" },
            new[] { "reallife_flare_inf_tech_1", "reallife_flare_inf_tech_2" },
            new[] { "reallife_flare_inf_news_1", "reallife_flare_inf_news_2" },
            new[] { "reallife_flare_inf_business_1", "reallife_flare_inf_business_2" }
        };
        private static readonly string[] VisitKeys = { "reallife_flare_inf_visit_1", "reallife_flare_inf_visit_2" };
        private static readonly string[] HiredKeys = { "reallife_flare_inf_hired_1", "reallife_flare_inf_hired_2" };
        private static readonly string[] RivalKeys = { "reallife_flare_inf_rival_1", "reallife_flare_inf_rival_2" };
        private static readonly string[] VoteKeys = { "reallife_flare_inf_vote_1", "reallife_flare_inf_vote_2" };
        private static readonly string[] CandidateKeys = { "reallife_flare_cand_1", "reallife_flare_cand_2" };
        private static readonly string[] SorryKeys = { "reallife_flare_inf_sorry_1", "reallife_flare_inf_sorry_2" };
        private static readonly string[] BrandKeys = { "reallife_flare_brand_1", "reallife_flare_brand_2" };
        private static readonly string[] CityNewsKeys = { "reallife_flare_city_accident", "reallife_flare_city_crime", "reallife_flare_city_fire", "reallife_flare_city_arrest", "reallife_flare_city_breakin" };

        public static string Niche(FlareNiche niche)
        {
            return T(Of(NicheKeys, (int)niche));
        }

        public static string Style(FlareStyle style)
        {
            return T(Of(StyleKeys, (int)style));
        }

        public static string Deal(int deal)
        {
            return T(Of(DealKeys, deal));
        }

        // An influencer's, a brand's or a candidate's post: a template by what it is, a variant by its seed.
        private static string Other(FlarePost p, string district, int niche)
        {
            string[] keys;
            switch (p.Source)
            {
                case FlareSource.Visit: keys = VisitKeys; break;
                case FlareSource.Hired:
                    if (p.Arg == 1)
                    {
                        return Fill("reallife_flare_inf_off", "name", p.Name);
                    }
                    Words.Append(Fill(HiredKeys[Pick(p.Seed, 1, HiredKeys.Length) - 1], "name", p.Name)).Append(' ').Append(T("reallife_flare_paid"));
                    return Words.ToString();
                case FlareSource.Rival: keys = RivalKeys; break;
                case FlareSource.Election: keys = p.Author == FlareAuthor.Candidate ? CandidateKeys : VoteKeys; break;
                case FlareSource.Apology: keys = SorryKeys; break;
                case FlareSource.BrandNews: keys = BrandKeys; break;
                case FlareSource.Results: return Fill(p.Arg == 1 ? "reallife_flare_results_beat" : "reallife_flare_results_missed", "name", p.Name);
                case FlareSource.Headline: return p.Name ?? string.Empty;
                case FlareSource.CityNews:
                    for (int bit = 0; bit < CityNewsKeys.Length; bit++)
                    {
                        if ((p.Arg & (1 << bit)) != 0)
                        {
                            return T(CityNewsKeys[bit]);
                        }
                    }
                    keys = NichePostKeys[(int)FlareNiche.LocalNews];
                    break;
                default:
                    keys = NichePostKeys[FlareRules.Clamp(niche, 0, NichePostKeys.Length - 1)];
                    break;
            }
            return Fill(keys[Pick(p.Seed, 1, keys.Length) - 1], "name", p.Name, "district", district);
        }

        /// <summary>A friend's reply to a post, in a kind picked by the rules and a variant by the post and the friend.</summary>
        public static string Reply(FlareRules.ReplyKind kind, FlarePost p, int person)
        {
            string[] keys = ReplyKeys[FlareRules.Clamp((int)kind, 0, ReplyKeys.Length - 1)];
            return T(keys[Pick(p.Seed + person * 7919, 3, keys.Length) - 1]);
        }

        /// <summary>The followers over the last days, oldest first: "120, 131, 140".</summary>
        public static string History(int[] history)
        {
            Words.Length = 0;
            for (int i = 0; i < history.Length; i++)
            {
                Words.Append(i > 0 ? ", " : string.Empty).Append(N(history[i]));
            }
            return Words.ToString();
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2);   // (a dictionary each: the game keeps a notification's data)
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        private static string Of(string[] keys, int at)
        {
            return keys[FlareRules.Clamp(at, 0, keys.Length - 1)];
        }

        // A variant from 1 to 'of', always the same for the same seed and slot.
        private static int Pick(int seed, int slot, int of)
        {
            return 1 + FlareRules.Roll(seed, slot, 11) % of;
        }
    }
}
