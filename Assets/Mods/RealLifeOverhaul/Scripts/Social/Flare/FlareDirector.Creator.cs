using System;
using System.Globalization;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the player as a creator (FlareCityRules.Creator; the brief's fourth step): the city's businesses offer paid posts as the account
    /// grows, a sponsorship's posts come on their days, and from Influencer on a creator income by the month; each paid post is the player's post about
    /// the business that paid, heard by the residents, so the business gets real customers from it. Run with the residents' side, after its day.
    /// COST: nothing per frame; a roll and a walk over the offers once a day.
    /// </summary>
    public sealed partial class FlareDirector
    {
        // After the residents' day: an offer from a business, the posts due of the offers taken, a creator income when due.
        private void CreatorDay(PopulationState s, int day)
        {
            FlareOffer offer = FlareCityRules.Offer(_city, s, day, _file.Followers, (int)_file.Tier, _file.Reputation, FlareCityGame.PriceLevel);
            if (offer != null)
            {
                offer.Name = FlareCityGame.NameOf(offer.Key);
                OfferDm(offer, day);   // the business writes with its offer: the kit's inbox takes or turns it down
                Logger?.Info("Flare in the city: " + offer.Name + " offers " + offer.Fee / 100 + " a post for " + offer.Posts + " post(s), open until day " + offer.Until + ".");
                if (FlareRules.MayNotify(_file, day, Settings.SocialMediaNotices))
                {
                    Tell(NotificationType.Info, "reallife_flare_offer_note", _openBusinessTab, "business", offer.Name, "fee", FlareText.Money((int)(offer.Fee / 100)));
                }
            }
            for (int i = 0; i < _city.Offers.Count; i++)
            {
                if (FlareCityRules.Due(_city.Offers[i], day))
                {
                    DealPost(_city.Offers[i], day);
                }
            }
            if (!FlareCityRules.CreatorDue(_city, day, (int)_file.Tier))
            {
                return;
            }
            long views = 0;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                views += p.Author == FlareAuthor.Player && p.Day > day - FlareCityRules.CreatorDays && p.Day <= day ? Math.Max(0, p.Reach) : 0;
            }
            long cents = FlareCityRules.CreatorIncome(views, FlareCityGame.PriceLevel);
            _city.CreatorDay = day;
            if (cents > 0 && FlareCityGame.PayCreator(cents))
            {
                _city.CreatorPaid = cents;
                _city.CreatorTotal += cents;
                Logger?.Info("Flare in the city: a creator income of " + cents / 100 + " for " + views + " views of the player's posts in " + FlareCityRules.CreatorDays + " days.");
                Notice(NotificationType.Success, "reallife_flare_creator_note", _openFeed, "money", FlareText.Money((int)(cents / 100)), "views", FlareText.N((int)Math.Min(int.MaxValue, views)));
            }
        }

        /// <summary>The player takes an offer on the Business tab: its first post goes up now and is paid. False when it cannot be taken.</summary>
        public bool AcceptOffer(int id)
        {
            if (!CityOn || Save == null)
            {
                return false;
            }
            FlareOffer offer = null;
            for (int i = 0; i < _city.Offers.Count; i++)
            {
                offer = _city.Offers[i].Id == id ? _city.Offers[i] : offer;
            }
            if (!FlareCityRules.Accept(offer, Save.Day))
            {
                return false;
            }
            DealPost(offer, Save.Day);
            Changed();
            return true;
        }

        // An offer's post: the player's paid post about the business (a paid partnership), heard by their followers, measured, and paid.
        private void DealPost(FlareOffer offer, int day)
        {
            string place = PlaceOfKey(offer.Key);
            if (place == null)
            {
                offer.Next = -1;
                return;
            }
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = Save != null ? Save.Hour : 12, Minute = FlareGame.MinuteOfDay % 60, Author = FlareAuthor.Player, Source = FlareSource.Sponsored,
                Tone = FlareTone.Cheerful, Place = place, Name = offer.Name, Arg = FlareCityRules.DealArg, Quality = 1000, Topic = "ad",
                Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = Math.Max(1, FlareRules.Reach(_file.Followers, 1000, 1000))
            };
            p.Likes = (int)((long)p.Reach * FlareRules.LikeShare / 1000);
            _file.Posts.Add(p);
            FlareRules.Prune(_file);
            CityHear(CityPostOf(p, FlareCityState.PlayerAccount, 1), CityFunnel(FunnelKind.Deal, id, place, day));
            FlareCityRules.Posted(offer, day);
            bool paid = FlareCityGame.PayDeal(offer.Fee);
            Logger?.Info("Flare in the city: a paid post for " + offer.Name + " (" + offer.Done + " of " + offer.Posts + "), " + (paid ? "paid " + offer.Fee / 100 : "not paid") + ".");
        }

        /// <summary>The offers of paid posts as a screen; null while the residents' side does not run.</summary>
        public FlareCityView CityOffersView()
        {
            return CityOn && Save != null ? FlareCityViews.Offers(_city, Save.Day) : null;
        }

        /// <summary>An offer by its number, or null.</summary>
        public FlareOffer OfferOf(int id)
        {
            for (int i = 0; CityOn && i < _city.Offers.Count; i++)
            {
                if (_city.Offers[i].Id == id)
                {
                    return _city.Offers[i];
                }
            }
            return null;
        }

        /// <summary>Test: an offer of a paid post from a business of the city's now, whatever the account's tier; the notice says who and for how much.</summary>
        public void TestResidentsOffer()
        {
            if (!CityTestReady("an offer of a paid post"))
            {
                return;
            }
            Guarded(() =>
            {
                PopulationState s = Residents;
                for (int i = 0; i < _city.Offers.Count; i++)
                {
                    if (_city.Offers[i].Open(Save.Day))
                    {
                        _city.Offers[i].Until = Save.Day - 1;   // the open one lapses, so a new one can come
                    }
                }
                FlareOffer offer = null;
                for (int t = 0; t < 64 && offer == null; t++)
                {
                    int followers = Math.Max(_file.Followers, FlareRules.TierFrom[(int)FlareTier.Known]);
                    offer = FlareCityRules.Offer(_city, s, Save.Day * 64 + t, followers, (int)FlareTier.Celebrity, _file.Reputation, FlareCityGame.PriceLevel);
                }
                if (offer == null)
                {
                    Tell(NotificationType.Warning, "reallife_flare_city_test_offer_none", null);
                    Logger?.Info("Flare check (an offer of a paid post): no business of the city's could offer: not run.");
                    return;
                }
                offer.Day = Save.Day;
                offer.Until = Save.Day + FlareCityRules.OfferDays - 1;
                offer.Name = FlareCityGame.NameOf(offer.Key);
                Logger?.Info("Flare check (an offer of a paid post): " + offer.Name + " offers " + offer.Fee / 100 + " a post for " + offer.Posts + " post(s), open until day " + offer.Until
                             + ": PASS");
                Tell(NotificationType.Info, "reallife_flare_city_test_offer_done", _openBusinessTab, "business", offer.Name, "fee", FlareText.Money((int)(offer.Fee / 100)));
                Changed();
            });
        }

        // A business's place as Flare writes it from the population's key, or null.
        private static string PlaceOfKey(string key)
        {
            int cut = key != null ? key.LastIndexOf('|') : -1;
            return cut > 0 && int.TryParse(key.Substring(cut + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? Tech.PlaceRules.Key(key.Substring(0, cut), number) : null;
        }
    }
}
