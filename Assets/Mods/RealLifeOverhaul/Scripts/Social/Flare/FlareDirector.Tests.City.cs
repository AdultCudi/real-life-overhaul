using System;
using System.Globalization;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's director, the Tests page's tools for Flare in the city. Each plays the real thing, or says in a notice why it cannot.</summary>
    public sealed partial class FlareDirector
    {
        // The residents' side running with its day's indexes made; otherwise a notice says why not.
        private bool CityTestReady(string test)
        {
            if (!TestReady(test))
            {
                return false;
            }
            if (!CityOn)
            {
                Tell(NotificationType.Warning, "reallife_flare_city_test_off", null);
                string why = !CityWanted ? "its options are off" : Residents == null ? "the population does not run here" : "starting";
                Logger?.Info("Flare check (" + test + "): the residents' side is not running (" + why + "): not run.");
                return false;
            }
            if (_cityPass.Busy || _city.Indexed < 0)
            {
                Tell(NotificationType.Info, "reallife_flare_city_test_wait", null);
                return false;
            }
            return true;
        }

        // The player's first business the population counts, or -1 with a notice.
        private int CityTestBusiness(out string place, out string name)
        {
            place = name = null;
            var shops = FlareGame.Businesses();
            for (int i = 0; i < shops.Count; i++)
            {
                int b = _cityPass.BusinessOfPlace(FlareGame.Place(shops[i]));
                if (b >= 0 && b < _city.BusinessCount)
                {
                    place = FlareGame.Place(shops[i]);
                    name = FlareGame.Name(shops[i]);
                    return b;
                }
            }
            Tell(NotificationType.Warning, "reallife_flare_city_test_no_business", null);
            return -1;
        }

        /// <summary>Test: the residents' day played now, whole (it otherwise follows the population's day a slice a frame).</summary>
        public void TestResidentsDay()
        {
            if (!CityTestReady("the residents' day"))
            {
                return;
            }
            Guarded(() =>
            {
                PopulationState s = Residents;
                FillCityInputs(s);
                _cityPass.Begin(_city, s, FlareBridges.Ask(FlareBridges.Pools, null), s.Day);
                _cityPass.RunAll();
                CityDayDone();
                CityCheck(_cityPass);
                Tell(NotificationType.Info, "reallife_flare_city_test_day_done", null, "followers", FlareText.N(_city.CityFollowers), "reviews", FlareText.N(_cityPass.Reviewed),
                    "ads", FlareText.N(_cityPass.Advertised));
            });
        }

        /// <summary>Test: a post about the player's first business heard by the residents now, as a real one is (its pull moves); the counts in a notice.</summary>
        public void TestResidentsPost()
        {
            if (!CityTestReady("a post heard by the residents"))
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
                int before = _city.Attention[b];
                var post = new CityPost
                {
                    Id = 9000000 + Save.Day, Account = FlareCityState.PlayerAccount, Business = b, Kind = s.Businesses.Kind[b], Sign = 1, Quality = 1000, Fit = FlareRules.GoodFit,
                    District = s.Businesses.District[b], Seed = FlareRules.Roll(_file.Seed, Save.Day, 870)
                };
                HeardReport r = FlareCityRules.Hear(_city, s, post, Save.Day, _cityPass.AccountBiz, _cityPass.Scratch, null);
                _city.Attention[b] = FlareCityRules.AttentionOf(_city, s, b, _cityPass.Weights);
                bool pass = r.Seen > 0 && (r.Interested == 0 || _city.Attention[b] >= before);
                Logger?.Info("Flare check (a post heard by the residents): " + name + ": seen by " + r.Seen + " of " + _city.CityFollowers + " followers in the city, " + r.Interested + " interested, "
                             + r.Shared + " shared it, " + r.Discovered + " came across it; its pull " + before + " to " + _city.Attention[b] + " thousandths: " + (pass ? "PASS" : "FAIL"));
                Tell(NotificationType.Info, "reallife_flare_city_test_post_done", null, "business", name, "seen", FlareText.N(r.Seen), "interested", FlareText.N(r.Interested),
                    "pull", FlareText.Signed(_city.Attention[b] / 10) + "%");
                Changed();
            });
        }

        /// <summary>Test: a small ad for the player's first business shown now to its district's residents who care for it, free; the counts in a notice.</summary>
        public void TestResidentsAd()
        {
            if (!CityTestReady("an ad"))
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
                FlareAd ad = AdOf(place, 0, 0, 0);
                int target = FlareCityRules.TargetSize(_city, s, ad, s.Day);
                ad.Id = 9000000 + Save.Day;
                HeardReport r = FlareCityRules.Deliver(_city, s, ad, b, s.Businesses.Kind[b], _cityPass.BusinessAccount[b], Save.Day, _cityPass.AccountBiz, _cityPass.Scratch, null);
                _city.Attention[b] = FlareCityRules.AttentionOf(_city, s, b, _cityPass.Weights);
                bool pass = r.Seen == Math.Min(ad.Reach, target);
                Logger?.Info("Flare check (an ad): " + name + ": shown to " + r.Seen + " of " + target + " residents in its target (" + ad.Reach + " bought), " + r.Interested + " interested, "
                             + r.Followed + " followed: " + (pass ? "PASS" : "FAIL"));
                Tell(NotificationType.Info, "reallife_flare_city_test_ad_done", null, "business", name, "seen", FlareText.N(r.Seen), "interested", FlareText.N(r.Interested));
                Changed();
            });
        }

        /// <summary>Test: the player's first business's page written to the log as the interface kit will draw it; its headline in a notice.</summary>
        public void TestResidentsPage()
        {
            if (!CityTestReady("a business's page"))
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
                FlareCityView v = CityBusinessView(place, name);
                var line = new System.Text.StringBuilder(256);
                line.Append("Flare check (a business's page): ").Append(name).Append(':');
                foreach (FlareViewFigure f in v.Hero)
                {
                    line.Append(' ').Append(f.Label.Substring(f.Label.LastIndexOf('_') + 1)).Append(' ').Append(f.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
                }
                line.Append(' ').Append(v.Rows.Count).Append(" review(s)");
                foreach (FlareViewFigure f in v.Details)
                {
                    line.Append("; ").Append(f.Label.Substring(f.Label.LastIndexOf('_') + 1)).Append(' ').Append(f.Value.ToString(CultureInfo.InvariantCulture));
                }
                line.Append(v.Primary != null ? "; action " + v.Primary : string.Empty).Append(": ").Append(v.Hero.Count == 3 && v.Primary != null ? "PASS" : "FAIL");
                Logger?.Info(line.ToString());
                Tell(NotificationType.Info, "reallife_flare_city_test_page_done", null, "business", name, "followers", FlareText.N((int)v.Hero[0].Value),
                    "rating", v.Hero[1].Value > 0 ? (v.Hero[1].Value / 10f).ToString("0.0", CultureInfo.InvariantCulture) : FlareText.T("reallife_flare_city_no_rating"));
            });
        }
    }
}
