using System;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>The transaction keys of Ventures' money (each has a text and a "_label" in en.json).</summary>
    public static class VenturesMoney
    {
        public const string Invested = "reallife_transaction_venture_invested";
        public const string Share = "reallife_transaction_venture_share";
        public const string BuyOut = "reallife_transaction_venture_buy_out";
        public const string Returned = "reallife_transaction_venture_returned";
        public const string SharesBought = "reallife_transaction_shares_bought";
        public const string SharesSold = "reallife_transaction_shares_sold";
        public const string Commission = "reallife_transaction_shares_commission";
        public const string Dividend = "reallife_transaction_shares_dividend";
    }

    /// <summary>An investor's answer to a counter-offer.</summary>
    public enum CounterAnswer { Accepted, HalfWay, Refused }

    /// <summary>How an offer is made, countered and taken up; game-free, every chance a roll seeded by what it is about.</summary>
    public static class OfferRules
    {
        /// <summary>The daily profit expected of a business that has none yet, at today's prices.</summary>
        public static int YoungTarget => Prices.At(300);

        /// <summary>True for a business an investor would come to: a profit on enough of its last days, and enough of it.</summary>
        public static bool Qualifies(in BusinessFigures b)
        {
            return b.Exists && b.ProfitableDays >= VenturesCatalogue.ProfitDaysNeeded && b.AvgProfit7 >= VenturesCatalogue.OfferProfitFloor;
        }

        /// <summary>The chance, in per cent, that a business that qualifies gets an offer at one weekly look.</summary>
        public static int Chance(bool liked, int phase)
        {
            int chance = VenturesCatalogue.OfferChancePct + (liked ? VenturesCatalogue.LikedBonusPct : 0);
            return phase < 0 ? chance * VenturesCatalogue.SlowdownChancePct / 100 : phase > 0 ? chance * VenturesCatalogue.BoomChancePct / 100 : chance;
        }

        /// <summary>The growth an investor expects at a level, in per cent of the recent profit; 1 at least.</summary>
        public static int Growth(Temperament temper, int level)
        {
            return Math.Max(1, VenturesCatalogue.TargetGrowthPct[(int)temper] * VenturesCatalogue.GrowthScalePct[VenturesCatalogue.Level(level)] / 100);
        }

        /// <summary>The share an investor asks when they come of their own accord: a quarter, a half or three quarters up their range, by temperament.</summary>
        public static int InboundShare(Investor investor)
        {
            int k = (int)investor.Kind;
            return VenturesCatalogue.Lerp(VenturesCatalogue.ShareLeastBp[k], VenturesCatalogue.ShareMostBp[k], 0.25 + 0.25 * (int)investor.Temper);
        }

        /// <summary>What a share is worth to an investor, within their largest sum and the room left; false under their smallest sum.</summary>
        public static bool Terms(VenturesFile file, Investor investor, string place, float recentProfit, int valueDays, int wantShareBp, int day, out int amount, out int shareBp)
        {
            int deals;
            int room = VenturesCatalogue.ShareCapBp - file.ShareOn(place, day, out deals);
            shareBp = Math.Min(wantShareBp, room);
            amount = VenturesCatalogue.Share(recentProfit * valueDays, shareBp);
            int most = investor.Most;
            if (amount > most)
            {
                shareBp = (int)(((long)shareBp * most + amount - 1) / amount);   // rounded up: never worth less than the sum
                amount = most;
            }
            amount = amount / 100 * 100;
            return shareBp > 0 && amount >= investor.Least;
        }

        /// <summary>An offer for a business the player runs, added to the file; null if there is no room or the figures do not carry the investor's smallest sum.</summary>
        public static Offer Make(VenturesFile file, Investor investor, string place, string business, in BusinessFigures b, int day, int level, int source)
        {
            int deals;
            file.ShareOn(place, day, out deals);
            bool followOn = source == OfferSource.FollowOn;
            if (file.Offers.Count >= VenturesCatalogue.OffersMost || file.HasOffer(place) || (!followOn && (file.Deals.Count >= VenturesCatalogue.DealsMost
                || deals >= VenturesCatalogue.DealsPerBusiness || file.HasDeal(place, investor.Id))))
            {
                return null;
            }
            int amount, share;
            if (!Terms(file, investor, place, b.AvgProfit7, followOn ? VenturesCatalogue.FollowOnValueDays : VenturesCatalogue.OfferValueDays, InboundShare(investor), day, out amount, out share))
            {
                return null;
            }
            return Add(file, investor, place, business, amount, share, day, level, source);
        }

        /// <summary>Terms already worked out (a pitch's decision), put on the table.</summary>
        public static Offer Add(VenturesFile file, Investor investor, string place, string business, int amount, int shareBp, int day, int level, int source)
        {
            var offer = new Offer
            {
                Id = file.NextId++, InvestorId = investor.Id, Source = source, Place = place ?? string.Empty, Business = business ?? string.Empty,
                Amount = amount, ShareBp = shareBp, TargetPct = Growth(investor.Temper, level), TargetDays = VenturesCatalogue.TargetDays[(int)investor.Temper],
                MadeDay = day, LapseDay = day + VenturesCatalogue.OfferLapseDays
            };
            file.Offers.Add(offer);
            file.Unseen++;
            return offer;
        }

        /// <summary>The one counter an offer allows, more money or a smaller share, answered by temperament and profitable days; changes the offer.</summary>
        public static CounterAnswer Counter(VenturesFile file, Offer offer, Investor investor, in BusinessFigures b, bool moreMoney)
        {
            if (offer.Countered || (moreMoney && !MoreMoney(offer, investor)))
            {
                return CounterAnswer.Refused;   // at their largest sum already: nothing to ask for, and the one counter is kept
            }
            offer.Countered = true;
            int t = (int)investor.Temper;
            int quality = offer.ForNew ? 0 : Math.Max(0, b.ProfitableDays - VenturesCatalogue.ProfitDaysNeeded) * VenturesCatalogue.CounterQualityPct;
            int yes = VenturesCatalogue.CounterAcceptPct[t] + quality;
            int roll = VenturesRandom.Percent(file.Seed, offer.Id * 2 + (moreMoney ? 1 : 0), 7001);
            CounterAnswer answer = roll < yes ? CounterAnswer.Accepted : roll < yes + VenturesCatalogue.CounterHalfPct[t] ? CounterAnswer.HalfWay : CounterAnswer.Refused;
            if (answer == CounterAnswer.Refused)
            {
                return answer;
            }
            int step = moreMoney ? VenturesCatalogue.CounterMoneyPct : VenturesCatalogue.CounterSharePct;
            int tenths = answer == CounterAnswer.Accepted ? step * 10 : step * 5;   // tenths of a per cent; half way is half the step
            if (moreMoney)
            {
                offer.Amount = (int)Math.Min(investor.Most, (long)offer.Amount * (1000 + tenths) / 1000 / 100 * 100);
            }
            else
            {
                offer.ShareBp = Math.Max(1, (int)((long)offer.ShareBp * (1000 - tenths) / 1000));
            }
            return answer;
        }

        /// <summary>True while more money can be asked of an investor: the offer is under their largest sum.</summary>
        public static bool MoreMoney(Offer offer, Investor investor)
        {
            return offer.Amount < investor.Most;
        }

        /// <summary>True while one more deal fits the file: its deals and the promises waiting to become deals stay within DealsMost.</summary>
        public static bool DealRoom(VenturesFile file)
        {
            return file.Deals.Count + file.Pledges.Count < VenturesCatalogue.DealsMost;
        }

        /// <summary>
        /// Whether an offer for a running business can still be taken up as things stand: the share within the business's
        /// room, and a second round on a deal there is or a new deal within the caps.
        /// </summary>
        public static bool Fits(VenturesFile file, Offer offer, int day)
        {
            int deals;
            if (file.ShareOn(offer.Place, day, out deals) + offer.ShareBp > VenturesCatalogue.ShareCapBp)
            {
                return false;
            }
            return Round(file, offer) != null || (DealRoom(file) && deals < VenturesCatalogue.DealsPerBusiness);
        }

        // The investor's deal on the business that a second round adds to; null for a new deal.
        private static Deal Round(VenturesFile file, Offer offer)
        {
            Deal deal = null;
            for (int i = 0; i < file.Deals.Count; i++)
            {
                if (file.Deals[i].Place == offer.Place && file.Deals[i].InvestorId == offer.InvestorId && file.Deals[i].Stage != DealStage.Called)
                {
                    deal = file.Deals[i];
                }
            }
            return deal;
        }

        /// <summary>An offer for a running business taken up, as a new deal or a second round; the caller pays the money in. Null if not open, or there is no room.</summary>
        public static Deal Accept(VenturesFile file, Offer offer, in BusinessFigures b, int day)
        {
            if (offer.ForNew || !Fits(file, offer, day))
            {
                return null;
            }
            file.Offers.Remove(offer);
            Deal deal = Round(file, offer);
            if (deal == null)
            {
                deal = new Deal
                {
                    Id = file.NextId++, InvestorId = offer.InvestorId, Place = offer.Place, Business = offer.Business, StartDay = day,
                    Mood = VenturesCatalogue.MoodStart, Test = offer.Test
                };
                file.Deals.Add(deal);
            }
            deal.Amount += offer.Amount;
            deal.ShareBp += offer.ShareBp;
            deal.Stage = DealStage.Running;
            deal.Misses = 0;
            deal.FollowOn = false;
            SetTarget(deal, b.AvgProfit7, offer.TargetPct, offer.TargetDays, day);
            file.InvestedTotal += offer.Amount;
            return deal;
        }

        /// <summary>An offer for a business not yet open taken up: the money waits for one of that kind ('had': how many the player runs now).</summary>
        public static Pledge Promise(VenturesFile file, Offer offer, int day, int had = 0)
        {
            if (!offer.ForNew || file.Pledges.Count >= VenturesCatalogue.PledgesMost || !DealRoom(file))
            {
                return null;
            }
            file.Offers.Remove(offer);
            var pledge = new Pledge
            {
                Id = file.NextId++, InvestorId = offer.InvestorId, TypeName = offer.TypeName, Budget = offer.Budget, Amount = offer.Amount, ShareBp = offer.ShareBp,
                TargetPct = offer.TargetPct, TargetDays = offer.TargetDays, MadeDay = day, OpenBy = day + VenturesCatalogue.PledgeDays, Had = had, Test = offer.Test
            };
            file.Pledges.Add(pledge);
            return pledge;
        }

        /// <summary>
        /// The business a promise waited for has been opened: the deal starts on it, and that business is no other promise's
        /// new one. The caller pays the money in. Null, the promise kept, while the file has no room for another deal.
        /// </summary>
        public static Deal Open(VenturesFile file, Pledge pledge, string place, string business, int day)
        {
            if (file.Deals.Count >= VenturesCatalogue.DealsMost)
            {
                return null;
            }
            file.Pledges.Remove(pledge);
            for (int i = 0; i < file.Pledges.Count; i++)
            {
                file.Pledges[i].Had += file.Pledges[i].TypeName == pledge.TypeName ? 1 : 0;
            }
            var deal = new Deal
            {
                Id = file.NextId++, InvestorId = pledge.InvestorId, Place = place, Business = business ?? string.Empty, Amount = pledge.Amount, ShareBp = pledge.ShareBp,
                StartDay = day, Mood = VenturesCatalogue.MoodStart, Test = pledge.Test
            };
            SetTarget(deal, 0f, pledge.TargetPct, pledge.TargetDays, day);
            file.Deals.Add(deal);
            file.InvestedTotal += pledge.Amount;
            return deal;
        }

        /// <summary>A target: the recent daily profit grown by so much, by a day; a business with no profit yet is asked for a first small one.</summary>
        public static void SetTarget(Deal deal, float recentProfit, int growthPct, int days, int day)
        {
            deal.BaseProfit = Math.Max(0, (int)Math.Round(recentProfit));
            deal.TargetProfit = Math.Max(YoungTarget, (int)((long)deal.BaseProfit * (100 + growthPct) / 100));
            deal.TargetDay = day + days;
        }
    }
}
