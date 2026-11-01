using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, trends (FlareCityRules.Trends; the brief's fifth step): a viral post of the player's about a business starts one, the
    /// city's own talk starts others in the residents' day, and a notice says what is trending and for how long; Explore lists them, a guest's
    /// from the host's part. COST: nothing per frame; a notice when a trend begins.
    /// </summary>
    public sealed partial class FlareDirector
    {
        // A viral post of the player's about a business: the trend it starts, if one may (FlareDirector.City, once the residents heard it).
        private void TrendFromPost(FlarePost p)
        {
            PopulationState s = Residents;
            int b = s != null && Save != null ? _cityPass.BusinessOfPlace(p.Place) : -1;
            FlareTrend t = b >= 0 ? FlareCityRules.TrendOf(_city, s, b, Save.Day, true) : null;
            if (t != null)
            {
                TrendNotice(t, s);
            }
        }

        // A trend begun: the notice (Flare's notifications option), and the line in the log.
        private void TrendNotice(FlareTrend t, PopulationState s)
        {
            string district = t.District >= 0 && t.District < s.Districts.Count ? s.Districts[t.District].Id : null;
            string trend = TrendWords(t.Kind, t.What, district);
            int days = FlareCityCatalogue.TrendDays + FlareCityCatalogue.TrendFadeDays;
            Logger?.Info("Flare in the city: a trend begins, " + trend + (t.Mine ? ", from the player's viral post" : ", from the city's own talk") + ", for " + days + " days.");
            Notice(NotificationType.Info, t.Mine ? "reallife_flare_city_trend_note_mine" : "reallife_flare_city_trend_note", () => Open(TabExplore), "trend", trend,
                "days", FlareText.N(days));
        }

        /// <summary>A trend in words: its kind of goods in the city, a district's going out, or a game genre.</summary>
        internal static string TrendWords(TrendKind kind, int what, string district)
        {
            string name = FlareText.T(kind == TrendKind.Genre ? Tech.StudioViews.GenreKey(what) : FlareCityViews.TrendKindKey(what));
            if (kind == TrendKind.Genre)
            {
                return FlareText.Fill("reallife_flare_city_trend_genre", "what", name);
            }
            return district != null ? FlareText.Fill("reallife_flare_city_trend_district", "what", name, "district", FlareGame.DistrictName(district))
                : FlareText.Fill("reallife_flare_city_trend_goods", "what", name);
        }

        /// <summary>
        /// Test: a trend begun now from the player's first business, as a viral post about it begins one (its district's going out for a place to
        /// go out, its goods for a shop; for anything else, night life in its district), and its first day played; the notice says which.
        /// </summary>
        public void TestResidentsTrend()
        {
            if (!CityTestReady("a trend"))
            {
                return;
            }
            Guarded(() =>
            {
                int b = CityTestBusiness(out string place, out string name);
                if (b < 0)
                {
                    return;
                }
                PopulationState s = Residents;
                int day = Save.Day;
                FlareTrend t = FlareCityRules.TrendOf(_city, s, b, day, true)
                               ?? FlareCityRules.StartTrend(_city, TrendKind.District, (int)Spend.NightLife, s.Businesses.District[b], day, true);
                if (t == null)
                {
                    Tell(NotificationType.Warning, "reallife_flare_city_test_trend_none", null);
                    Logger?.Info("Flare check (a trend): no trend could begin (" + _city.Trends.Count + " running, or this one already): not run.");
                    return;
                }
                FlareCityRules.TrendsDay(_city, s, day, _cityPass.PersonSlot);
                string trend = TrendWords(t.Kind, t.What, t.District >= 0 && t.District < s.Districts.Count ? s.Districts[t.District].Id : null);
                Changed();
                Tell(NotificationType.Info, "reallife_flare_city_test_trend_done", () => Open(TabExplore), "trend", trend, "moved", FlareText.N(t.Moved));
                Logger?.Info("Flare check (a trend): " + trend + " begun from " + name + " at " + place + ", " + t.Moved + " taken to it on its first day: PASS");
            });
        }

        /// <summary>What is trending as a screen: the host's own, or a guest's from the host's part; null while neither runs.</summary>
        public FlareCityView CityTrendsView()
        {
            if (Save == null)
            {
                return null;
            }
            if (CityOn)
            {
                return FlareCityViews.Trends(FlareCityRules.Seen(_city, Residents), Save.Day);
            }
            return CityFollowing ? FlareCityViews.Trends(_cityFollowed.Trends, Save.Day) : null;
        }
    }
}
