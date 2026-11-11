using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The entry of the rivals seen around the city in the feature list, its keys written out whole as the options checks read them. Its Guide page's numbers are
    /// facts filled from <see cref="SeenCatalogue"/> and the social life's own rules when the page opens.
    /// </summary>
    public static class SeenGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("rivals_seen", "reallife_meta_title_rivals_seen")
                .Group("reallife_seen_header")
                .About("reallife_guide_rivals_seen_about").Help("reallife_guide_rivals_seen")
                .Owns(Settings.LivingRivalsSeenId)
                .Data(SeenStore.Key, SeenStore.TestKey)
                .Test("reallife_seen_test_btn_where", "reallife_seen_test_btn_here", "reallife_seen_test_btn_sighting", "reallife_seen_test_btn_back")
                .Fact("seen_world_most", () => SeenCatalogue.WorldMost.ToString(Inv))
                .Fact("seen_near", () => SeenCatalogue.NearMetres.ToString(Inv))
                .Fact("seen_car_hold", () => SeenCatalogue.CarHoldDays.ToString(Inv))
                .Fact("seen_day_most", () => SeenCatalogue.PostsMost.ToString(Inv))
                .Fact("seen_player_gap", () => SeenCatalogue.PlayerGapDays.ToString(Inv))
                .Fact("seen_known", () => (SeenRules.KnowsFrom / 10).ToString(Inv))
                .Fact("seen_gain", () => (SocialRules.Chat(true, true, false, true, true).Familiarity).ToString("0.#", Inv))
                .ResetData(() => SeenDirector.Instance?.Forget());
        }
    }

    /// <summary>The place of the rivals seen around the city on the options page.</summary>
    public static class SeenOptions
    {
        public static ModOptions AddSeen(this ModOptions options)
        {
            return options
                .AddHeader("reallife_seen_header")
                .AddToggle(Settings.LivingRivalsSeenId, "reallife_seen_enabled", Settings.LivingRivalsSeen, v => { Settings.LivingRivalsSeen = v; })
                .AddButton("reallife_seen_test_btn_where", () => SeenDirector.Instance?.TestWhere())
                .AddButton("reallife_seen_test_btn_here", () => SeenDirector.Instance?.TestHere())
                .AddButton("reallife_seen_test_btn_sighting", () => SeenDirector.Instance?.TestSighting())
                .AddButton("reallife_seen_test_btn_back", () => SeenDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
