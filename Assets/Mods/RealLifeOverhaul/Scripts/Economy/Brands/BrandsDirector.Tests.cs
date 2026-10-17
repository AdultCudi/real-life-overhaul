using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The director's part for the test tools: each puts one thing in place and opens the app on it. The state before
    /// the first test is kept for "put brands back"; money moved after a test is real.
    /// </summary>
    public sealed partial class BrandsDirector
    {
        private const int TestRec = 8500;       // hundredths: the made brand's recognition in its districts...
        private const int TestCity = 4000;      // ...and city-wide
        private const int TestCapital = 100000; // the test franchisee's capital

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyBrands || _broken || !_running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // The state as it is before a test tool first changes it, kept once to be put back.
        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = BrandStore.Write(_state);
            }
        }

        private void TestDone(string what, int tab, string pick)
        {
            Logger?.Warn("Brands test: " + what + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_brand_test_done", null, 7f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
            Open(tab, pick);
        }

        private void TestGuard(string test, Action work)
        {
            try
            {
                Reread();
                work();
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
            }
        }

        private Brand TestBrand(string test)
        {
            if (_state.Brands.Count > 0)
            {
                return _state.Brands[0];
            }
            TestRefusals.TellKey(Logger, test, "reallife_brand_test_no_brand");
            return null;
        }

        /// <summary>Makes the best free shop a brand (or takes the first), joins every shop of its type, and puts it on offer with high recognition.</summary>
        public void TestMake()
        {
            if (!TestReady("Brands make"))
            {
                return;
            }
            TestGuard("make", () =>
            {
                BrandShop best = null;
                for (int i = 0; i < _shops.Count; i++)
                {
                    BrandShop shop = _shops[i];
                    if (shop.File == null && shop.Chain == null && (best == null || Yesterday(shop) > Yesterday(best)))
                    {
                        best = shop;
                    }
                }
                Brand b = _state.Brands.Count > 0 && (best == null || _state.Brands.Count >= BrandCatalogue.BrandsMost) ? _state.Brands[0] : null;
                if (b == null && best == null)
                {
                    TestRefusals.TellKey(Logger, "Brands make", "reallife_brand_test_no_business");
                    return;
                }
                BeforeTest();
                if (b == null)
                {
                    b = BrandRules.Create(_state, best.Name, best.Type, best.Place, best.District, Save.Day);
                    if (b == null)
                    {
                        TestRefusals.TellKey(Logger, "Brands make", "reallife_brand_test_no_business");
                        return;
                    }
                }
                int joined = 0;
                for (int i = 0; i < _shops.Count; i++)
                {
                    BrandShop shop = _shops[i];
                    joined += shop.File == null && shop.Chain == null && BrandRules.Join(_state, b, shop.Place, shop.Type, shop.District, Save.Day) ? 1 : 0;
                }
                Reread();
                for (int i = 0; i < b.Districts.Count; i++)
                {
                    b.Districts[i].Rec = Math.Max(b.Districts[i].Rec, TestRec);
                }
                b.City = Math.Max(b.City, TestCity);
                b.Offered = true;
                TestDone("'" + b.Name + "' (" + b.Type + ") with " + BrandRules.Members(_state, b, null) + " member shop(s), " + joined + " of them joined now; recognition " + TestRec
                         + " hundredths in its " + b.Districts.Count + " district(s) and " + b.City + " city-wide; on offer as a franchise (the day's rules take it off offer again if it has under "
                         + BrandCatalogue.OfferShops + " member shops with standards of " + BrandCatalogue.OfferStandards + ")", TabBrands, "brand:" + b.Id);
            });
        }

        private static int Yesterday(BrandShop shop)
        {
            try
            {
                return shop.Reg != null ? UnityEngine.Mathf.RoundToInt(BusinessGame.DailyRevenue(shop.Reg)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Somebody applies to the first brand, on offer or not.</summary>
        public void TestApplicant()
        {
            if (!TestReady("Brands applicant"))
            {
                return;
            }
            TestGuard("applicant", () =>
            {
                Brand b = TestBrand("Brands applicant");
                if (b == null)
                {
                    return;
                }
                BeforeTest();
                if (_state.Applicants.Count >= BrandCatalogue.ApplicantsMost)
                {
                    _state.Applicants.RemoveAt(0);
                }
                BrandsGame.Districts(_districts);
                Applicant a = FranchiseRules.Generate(_state.Seed, _state.NextId++, b.Id, Save.Day, _districts);
                _state.Applicants.Add(a);
                TestDone(a.Name + " applies to '" + b.Name + "': " + a.Temper + ", capital " + a.Capital + ", " + a.Experience + " year(s), ability " + a.Ability + ", wants " + a.District
                         + ", waits until day " + a.Until, TabApplicants, "app:" + a.Id);
            });
        }

        /// <summary>A careful franchisee signs the middle terms with the first brand in the next district with somewhere to open; no fee is paid.</summary>
        public void TestSign()
        {
            if (!TestReady("Brands sign"))
            {
                return;
            }
            TestGuard("sign", () =>
            {
                Brand b = TestBrand("Brands sign");
                if (b == null)
                {
                    return;
                }
                if (_state.Franchises.Count >= BrandCatalogue.FranchisesMost)
                {
                    TestRefusals.TellKey(Logger, "Brands sign", "reallife_brand_answer_full");
                    return;
                }
                BrandsGame.Districts(_districts);
                BrandShop flagship = ShopAt(b.Flagship);
                int home = Math.Max(0, _districts.IndexOf(flagship != null ? flagship.District : null));
                for (int step = 1; step <= _districts.Count; step++)
                {
                    string district = _districts[(home + step) % _districts.Count];
                    BrandsGame.Sites(district, b.Type, _sites);
                    _sitesFor = 0;
                    var existing = new Franchise();
                    bool canConvert = BrandsGame.Convert(existing, _state, district, b.Type);
                    if (_sites.Count == 0 && !canConvert)
                    {
                        continue;
                    }
                    BeforeTest();
                    var a = new Applicant
                    {
                        Id = _state.NextId++, BrandId = b.Id, Name = FranchiseRules.Generate(_state.Seed, _state.NextId, b.Id, Save.Day, _districts).Name, Temper = Temper.Careful,
                        Capital = TestCapital, Experience = 5, ShopsRun = 1, Ability = 65, District = district, Until = Save.Day + BrandCatalogue.ApplicantDays
                    };
                    _state.Applicants.Add(a);
                    Franchise f = FranchiseRules.Sign(_state, b, a, 1, 1, 1, false, 0, Index, Save.Day, BrandsGame.DaysPerYear);
                    Place(f, b, _sites.Count > 0 ? _sites[0] : null, canConvert ? existing : null, Save.Day);
                    _sites.Clear();
                    TestDone(f.Name + " signed with '" + b.Name + "' in " + district + " (no entry fee paid): royalty " + f.RoyaltyBp + " and levy " + f.LevyBp + " ten-thousandths until day "
                             + f.Until + "; the shop is " + f.Hold + " at " + f.Place + (f.Hold != Hold.Books ? " as the game's '" + f.GameName + "'" : string.Empty) + ", " + f.Base
                             + " customers on an ordinary day; the royalty of the days to come is real", TabAgreements, "fr:" + f.Id);
                    return;
                }
                TestRefusals.TellKey(Logger, "Brands sign", "reallife_brand_test_no_site");
            });
        }

        /// <summary>The first franchised shop has one ordinary day's takings left and asks for help.</summary>
        public void TestTrouble()
        {
            if (!TestReady("Brands trouble"))
            {
                return;
            }
            TestGuard("trouble", () =>
            {
                Franchise f = _state.Franchises.Count > 0 ? _state.Franchises[0] : null;
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b == null)
                {
                    TestRefusals.TellKey(Logger, "Brands trouble", "reallife_brand_test_no_franchise");
                    return;
                }
                BeforeTest();
                int ordinary = Math.Max(1, FranchiseRules.Ordinary(f.Base, b.Ticket));
                if (f.Ask != Ask.None && f.Suit == 0)
                {
                    FranchiseRules.Drop(b, f, Save.Day);
                }
                f.Health = ordinary;
                f.Trouble = true;
                if (f.Ask == Ask.None)
                {
                    f.Ask = Ask.Help;
                    f.AskUntil = Save.Day + BrandCatalogue.AskDays;
                    f.AskAmount = ordinary * BrandCatalogue.HelpDays;
                }
                TestDone(f.Name + " has " + f.Health + " in hand (an ordinary day takes " + ordinary + ") and asks for " + f.AskAmount + " by day " + f.AskUntil, TabAgreements, "fr:" + f.Id);
            });
        }

        /// <summary>A failed inspection is heard of at the first member shop; no inspection happens and no fine is charged.</summary>
        public void TestScandal()
        {
            if (!TestReady("Brands scandal"))
            {
                return;
            }
            TestGuard("scandal", () =>
            {
                Member m = _state.Members.Count > 0 ? _state.Members[0] : null;
                if (m == null)
                {
                    TestRefusals.TellKey(Logger, "Brands scandal", "reallife_brand_test_no_brand");
                    return;
                }
                BeforeTest();
                Heard(m.Place, Scandal.Inspection);
                TestDone("a failed inspection heard of at " + ShopName(m.Place) + " (no inspection, no fine)", TabBrands, "brand:" + m.BrandId);
            });
        }

        /// <summary>Every brand's own clock runs a year on today's figures; the game's day, the franchises and the money stand still.</summary>
        public void TestYear()
        {
            if (!TestReady("Brands year"))
            {
                return;
            }
            TestGuard("year", () =>
            {
                if (TestBrand("Brands year") == null)
                {
                    return;
                }
                BeforeTest();
                int days = BrandsGame.DaysPerYear;
                string told = string.Empty;
                for (int k = 0; k < _state.Brands.Count; k++)
                {
                    Brand b = _state.Brands[k];
                    int before = BrandRules.Best(b), city = b.City;
                    for (int d = 0; d < days; d++)
                    {
                        BrandRules.Day(_state, b);
                    }
                    told += (k > 0 ? "; " : string.Empty) + "'" + b.Name + "' best district " + before + " to " + BrandRules.Best(b) + ", city-wide " + city + " to " + b.City
                            + " hundredths, standards " + b.Standards;
                }
                TestDone(days + " day(s) of every brand's own clock on today's figures (the game's day has not moved, nothing was paid): " + told, TabBrands, "brand:" + _state.Brands[0].Id);
            });
        }

        /// <summary>Puts back the state from before the first test; a business a test franchise opened is closed by the game's closure, one it converted keeps trading.</summary>
        public void TestBack()
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, "Brands back", TestRefusal.NotLoaded);
                return;
            }
            if (!_running || _broken)
            {
                TestRefusals.Tell(Logger, "Brands back", TestRefusal.FeatureOff);
                return;
            }
            try
            {
                int version;
                BrandsState back = _beforeTests != null ? BrandStore.Read(_beforeTests, out version) : null;
                _beforeTests = null;
                if (back == null)
                {
                    TestRefusals.TellKey(Logger, "Brands back", "reallife_brand_test_nothing_back");
                    return;
                }
                int closed = 0;
                for (int i = 0; i < _state.Franchises.Count; i++)
                {
                    Franchise f = _state.Franchises[i];
                    Brand b = _state.Brand(f.BrandId);
                    if ((f.Hold == Hold.Opened || f.Hold == Hold.Converted) && back.Franchise(f.Id) == null && b != null)
                    {
                        BrandsGame.Close(f, b.Type);   // an opened shop is closed, a converted one given its own start day again
                        closed += f.Hold == Hold.Opened ? 1 : 0;
                    }
                    if (f.Suit != 0 && back.Franchise(f.Id) == null && BrandsBridges.WithdrawClaim != null)
                    {
                        BrandsBridges.WithdrawClaim(f.Suit);
                    }
                }
                back.LastDay = Save.Day;
                _state = back;
                _sitesFor = 0;
                Reread();
                BrandsGame.Remark();
                TestDone("the state from before the tests is back (" + _state.Brands.Count + " brand(s), " + _state.Members.Count + " member shop(s), " + _state.Franchises.Count
                         + " franchised shop(s)); " + closed + " business(es) a test opened were closed by the game's own closure; money that moved since stays moved", TabBrands, null);
            }
            catch (Exception e)
            {
                Fault("a test (back)", e);
            }
        }
    }
}
