using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The feature's entry in the mod's feature list, and the figures its Guide page prints: every {name} in its text is
    /// filled from the rules here, so the page can never say anything RLO Texts does not do. Called once, when the feature
    /// list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class TextsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("texts", "reallife_meta_title_texts")
                .Group("reallife_texts_header")
                .About("reallife_guide_texts_about").Help("reallife_guide_texts")
                .Owns(Settings.TextsId, Settings.TextsDeviceId, Settings.TextsThreadsId, Settings.TextsRepliesId, Settings.TextsPostsId, Settings.TextsPeopleId,
                    Settings.TextsOfficialId, Settings.TextsRivalsId, Settings.TextsLedgerId, Settings.TextsCityId)
                .Data(TextsStore.SaveKey, TextMemoryStore.SaveKey, TextsCityStore.SaveKey)
                .Test("reallife_texts_test_sample")
                .Test("reallife_texts_test_shown", TextsDirector.TestShown)
                .Test("reallife_texts_test_staff", TextsDirector.TestStaff)
                .Test("reallife_texts_test_life", TextsDirector.TestLife)
                .Test("reallife_texts_test_gossip", TextsDirector.TestGossip)
                .Fact("texts_timeout", () => TextServer.TimeOut.ToString("0", Inv))
                .Fact("texts_per_minute", () => TextQueue.PerMinute.ToString(Inv))
                .Fact("texts_slow", () => TextSpeed.FullSlow.ToString("0", Inv))
                .Fact("texts_ahead", () => TextSpeed.AheadSlow.ToString("0", Inv))
                .Fact("texts_memory_lines", () => TextMemoryRules.CardMost.ToString(Inv))
                .Fact("texts_memory_people", () => TextMemoryRules.PeopleMost.ToString(Inv))
                .Fact("texts_memory_facts", () => TextMemoryRules.FactsMost.ToString(Inv))
                .Fact("texts_big_share", () => TextsCityRules.BigShare.ToString(Inv))
                .Fact("texts_large_slow", () => TextSpeed.LargeSlow.ToString("0.0", Inv))
                .Cleanup(() => TextsDirector.Words.Clear())
                .ResetData(() =>
                {
                    TextsDirector.Words.Store.Clear();
                    TextsDirector.ForgetAll();
                });
        }
    }

    /// <summary>RLO Texts' place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class TextsOptions
    {
        /// <summary>The "RLO Texts" block of the options page: the switch, where it runs, the threads, each use, the status and the sample.</summary>
        public static ModOptions AddTexts(this ModOptions options)
        {
            return options
                .AddHeader("reallife_texts_header")
                .AddToggle(Settings.TextsId, "reallife_texts_enabled", Settings.Texts, v => Settings.Texts = v)
                .AddDropdown(Settings.TextsDeviceId, "reallife_texts_opt_device",
                    new[] { "reallife_texts_device_0", "reallife_texts_device_1", "reallife_texts_device_2" }, Settings.TextsDevice, v => Settings.TextsDevice = v)
                .AddDropdown(Settings.TextsThreadsId, "reallife_texts_opt_threads",
                    new[] { "reallife_texts_threads_0", "reallife_texts_threads_2", "reallife_texts_threads_3", "reallife_texts_threads_4" }, Settings.TextsThreads,
                    v => Settings.TextsThreads = v)
                .AddToggle(Settings.TextsRepliesId, "reallife_texts_opt_replies", Settings.TextsReplies, v => Settings.TextsReplies = v)
                .AddToggle(Settings.TextsPostsId, "reallife_texts_opt_posts", Settings.TextsPosts, v => Settings.TextsPosts = v)
                .AddToggle(Settings.TextsPeopleId, "reallife_texts_opt_people", Settings.TextsPeople, v => Settings.TextsPeople = v)
                .AddToggle(Settings.TextsOfficialId, "reallife_texts_opt_official", Settings.TextsOfficial, v => Settings.TextsOfficial = v)
                .AddToggle(Settings.TextsRivalsId, "reallife_texts_opt_rivals", Settings.TextsRivals, v => Settings.TextsRivals = v)
                .AddToggle(Settings.TextsLedgerId, "reallife_texts_opt_ledger", Settings.TextsLedger, v => Settings.TextsLedger = v)
                .AddToggle(Settings.TextsCityId, "reallife_texts_opt_city", Settings.TextsCity, v => Settings.TextsCity = v)
                .AddButton("reallife_texts_status", TextsDirector.ShowStatus)
                .AddButton("reallife_texts_test_sample", TextsDirector.TestSample)
                .AddSplitter();
        }
    }
}
