using System;
using System.Collections.Generic;

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

    public enum CounterAnswer { Accepted, HalfWay, Refused }

    /// <summary>
    /// How an offer is made, countered and taken up. Game-free: the director reads the business's figures from the
    /// game and hands them in. Every chance is a roll from the save's seed and what it is about.
    /// </summary>
    public static class OfferRules
    {
        public const int YoungTarget = 300;   // the daily profit expected of a business that has none yet

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

        /// <summary>The share an investor asks when they come of their own accord: a quarter, a half or three quarters of the way up their range, by temperament.</summary>
        public static int InboundShare(Investor investor)
        {
            int k = (int)investor.Kind;
            return VenturesCatalogue.Lerp(VenturesCatalogue.ShareLeastBp[k], VenturesCatalogue.ShareMostBp[k], 0.25 + 0.25 * (int)investor.Temper);
        }

        /// <summary>
        /// What a share of a business is worth to an investor: the share of so many days of its recent profit, within
        /// their largest sum (the share shrinks to fit) and the room left on the business. False if that is under
        /// their smallest sum.
        /// </summary>
        public static bool Terms(VenturesFile file, Investor investor, string place, float recentProfit, int valueDays, int wantShareBp, int day, out int amount, out int shareBp)
        {
            int deals;
            int room = VenturesCatalogue.ShareCapBp - file.ShareOn(place, day, out deals);
            shareBp = Math.Min(wantShareBp, room);
            amount = VenturesCatalogue.Share(recentProfit * valueDays, shareBp);
            if (amount > investor.ChequeMost)
            {
                shareBp = (int)(((long)shareBp * investor.ChequeMost + amount - 1) / amount);   // (rounded up: the share is never worth less than the sum)
                amount = investor.ChequeMost;
            }
            amount = amount / 100 * 100;
            return shareBp > 0 && amount >= investor.ChequeLeast;
        }

        /// <summary>An offer for a business the player runs, added to the file; null if there is no room for one or the figures do not carry the investor's smallest sum.</summary>
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

        /// <summary>
        /// The one counter an offer allows: more money for the same share, or the same money for a smaller share. The
        /// investor says yes, meets the player half way or holds to the first terms, by temperament and by how many
        /// of the business's last days made a profit. The offer is changed in place.
        /// </summary>
        public static CounterAnswer Counter(VenturesFile file, Offer offer, Investor investor, in BusinessFigures b, bool moreMoney)
        {
            if (offer.Countered)
            {
                return CounterAnswer.Refused;
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
            int tenths = answer == CounterAnswer.Accepted ? step * 10 : step * 5;   // (half way: half the step, in tenths of a per cent)
            if (moreMoney)
            {
                offer.Amount = (int)Math.Min(investor.ChequeMost, (long)offer.Amount * (1000 + tenths) / 1000 / 100 * 100);
            }
            else
            {
                offer.ShareBp = Math.Max(1, (int)((long)offer.ShareBp * (1000 - tenths) / 1000));
            }
            return answer;
        }

        /// <summary>
        /// An offer taken up. For a business that is running: a new deal, or more money and share on the deal the same
        /// investor already has there (a second round). The caller pays the money in. Null for a business not yet open:
        /// see <see cref="Promise"/>.
        /// </summary>
        public static Deal Accept(VenturesFile file, Offer offer, in BusinessFigures b, int day)
        {
            if (offer.ForNew)
            {
                return null;
            }
            file.Offers.Remove(offer);
            Deal deal = null;
            for (int i = 0; i < file.Deals.Count; i++)
            {
                if (file.Deals[i].Place == offer.Place && file.Deals[i].InvestorId == offer.InvestorId && file.Deals[i].Stage != DealStage.Called)
                {
                    deal = file.Deals[i];
                }
            }
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

        /// <summary>An offer for a business not yet open, taken up: the money waits for one of that kind to be opened ('had': how many of that kind the player runs already).</summary>
        public static Pledge Promise(VenturesFile file, Offer offer, int day, int had = 0)
        {
            if (!offer.ForNew || file.Pledges.Count >= VenturesCatalogue.PledgesMost)
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

        /// <summary>The business a promise waited for has been opened: the deal starts on it. The caller pays the money in.</summary>
        public static Deal Open(VenturesFile file, Pledge pledge, string place, string business, int day)
        {
            file.Pledges.Remove(pledge);
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

        /// <summary>A target: the recent daily profit grown by so much, by a day. A business with no profit yet is asked for a first small one.</summary>
        public static void SetTarget(Deal deal, float recentProfit, int growthPct, int days, int day)
        {
            deal.BaseProfit = Math.Max(0, (int)Math.Round(recentProfit));
            deal.TargetProfit = Math.Max(YoungTarget, (int)((long)deal.BaseProfit * (100 + growthPct) / 100));
            deal.TargetDay = day + days;
        }
    }

    public enum DealNews
    {
        Paid, Short, TargetMet, Warned, Demanded, DemandDefault, PromiseKept, PromiseBroken, WentCold, Called, CallPaid, CallMissed, CallDone, Claimed, BusinessGone, Settled, BoardEnded,
        PaidBack
    }

    public struct DealEvent
    {
        public DealNews What;
        public int DealId;
        public int InvestorId;
        public int Amount;
        public string Business;
    }

    /// <summary>
    /// A deal's day: the investor's share of yesterday's profit (never of a loss), the target when its day comes,
    /// and what follows a miss: a warning, then a demand, then, for a demanding investor on a level that allows it,
    /// their money called back; anybody else loses interest (and, if they have not had their money back, will not back
    /// the founder again). An investor who has had their money back and sees a target met sets no more targets.
    /// Game-free: money moves through the wallet it is given, and what happened is added to a list for the director
    /// to say.
    /// </summary>
    public static class DealRules
    {
        /// <summary>One deal's day. True if the deal has ended and was taken off the file.</summary>
        public static bool RunDay(VenturesFile file, Deal deal, in BusinessFigures b, int day, int level, IWallet wallet, List<DealEvent> said, Func<int, int> fileClaim)
        {
            Investor investor = file.Investor(deal.InvestorId);
            Temperament temper = investor != null ? investor.Temper : Temperament.Even;
            string name = investor != null ? investor.Name : string.Empty;
            level = VenturesCatalogue.Level(level);
            if (deal.Stage == DealStage.Called)
            {
                return CallDay(file, deal, wallet, said, fileClaim, name);
            }
            if (!b.Exists)
            {
                // The business has been sold or closed: the investor is owed what they put in, less what they have been paid.
                int owed = Math.Max(0, deal.Amount - deal.PaidOut);
                if (owed <= 0)
                {
                    Say(said, DealNews.Settled, deal, 0);
                    file.Deals.Remove(deal);
                    return true;
                }
                StartCall(deal, owed, level);
                Say(said, DealNews.BusinessGone, deal, owed);
                return false;
            }

            int due = (b.HasYesterday ? VenturesCatalogue.Share(b.ProfitYesterday, deal.ShareNow(day - 1)) : 0) + deal.Arrears;
            if (due > 0)
            {
                int paid = Math.Min(due, wallet.Cash);
                if (paid > 0 && !wallet.Take(paid, VenturesMoney.Share, name))
                {
                    paid = 0;
                }
                deal.Arrears = due - paid;
                deal.PaidOut += paid;
                file.PaidOutTotal += paid;
                if (paid > 0)
                {
                    Say(said, DealNews.Paid, deal, paid);
                }
                if (deal.Arrears > 0)
                {
                    Mood(deal, VenturesCatalogue.MoodArrears);
                    Say(said, DealNews.Short, deal, deal.Arrears);
                }
            }
            if (deal.BoardUntil > 0 && day >= deal.BoardUntil)
            {
                deal.BoardUntil = 0;
                Say(said, DealNews.BoardEnded, deal, 0);
            }
            if (deal.Stage == DealStage.Demand && day >= deal.DemandDay)
            {
                Answer(deal, investor, day, investor == null || investor.Kind != InvestorKind.Firm, b);
                Say(said, DealNews.DemandDefault, deal, 0);
            }
            if (deal.CostDay > 0 && day >= deal.CostDay)
            {
                deal.CostDay = 0;
                if (b.AvgCost14 <= deal.CostTarget)
                {
                    Mood(deal, VenturesCatalogue.MoodKeptPromise);
                    Say(said, DealNews.PromiseKept, deal, deal.CostTarget);
                }
                else
                {
                    Say(said, DealNews.PromiseBroken, deal, deal.CostTarget);
                    Miss(file, deal, investor, day, level, said);
                }
            }
            if (deal.Stage == DealStage.Running && deal.TargetDay > 0 && day >= deal.TargetDay)
            {
                if (b.AvgProfit7 >= deal.TargetProfit)
                {
                    deal.Misses = 0;
                    deal.TargetsMet++;
                    deal.FollowOn = true;
                    file.TargetsMet++;
                    file.MoveReputation(VenturesCatalogue.ReputationMet);
                    Mood(deal, VenturesCatalogue.MoodMet);
                    Say(said, DealNews.TargetMet, deal, deal.TargetProfit);
                    if (deal.PaidOut >= deal.Amount)
                    {
                        // They have had their money back and the business does what they asked: no more targets, only their share.
                        deal.Stage = DealStage.Quiet;
                        deal.TargetDay = 0;
                        Say(said, DealNews.PaidBack, deal, deal.PaidOut);
                    }
                    else
                    {
                        OfferRules.SetTarget(deal, b.AvgProfit7, OfferRules.Growth(temper, level), VenturesCatalogue.TargetDays[(int)temper], day);
                    }
                }
                else
                {
                    Miss(file, deal, investor, day, level, said);
                }
            }
            return false;
        }

        // A target missed (or a promise broken): a warning, then a demand, then the money called back or the investor gone cold.
        // An investor who goes cold before they have had their money back will not back this founder again, and the others hear of it.
        private static void Miss(VenturesFile file, Deal deal, Investor investor, int day, int level, List<DealEvent> said)
        {
            Temperament temper = investor != null ? investor.Temper : Temperament.Even;
            deal.Misses++;
            Mood(deal, VenturesCatalogue.MoodMissed);
            if (deal.Misses == 1)
            {
                deal.TargetDay = day + VenturesCatalogue.TargetDays[(int)temper];
                Say(said, DealNews.Warned, deal, deal.TargetProfit);
            }
            else if (deal.Misses == 2)
            {
                deal.Stage = DealStage.Demand;
                deal.DemandDay = day + VenturesCatalogue.DemandDays;
                deal.TargetDay = 0;
                Say(said, DealNews.Demanded, deal, 0);
            }
            else if (Callable(temper, level, deal.Misses))
            {
                StartCall(deal, CallSum(deal), level);
                Say(said, DealNews.Called, deal, deal.CallOwed);
            }
            else
            {
                deal.Stage = DealStage.Cold;
                deal.TargetDay = 0;
                deal.CostDay = 0;
                bool unpaid = deal.PaidOut < deal.Amount;
                if (unpaid && investor != null)
                {
                    investor.ClosedUntil = day + VenturesCatalogue.ColdClosedDays;
                    file.MoveReputation(VenturesCatalogue.ReputationCold);
                }
                Say(said, DealNews.WentCold, deal, unpaid ? 1 : 0);
            }
        }

        /// <summary>True if an investor of this temperament calls their money back after so many misses at this level. Only the demanding do, and nobody on Relaxed.</summary>
        public static bool Callable(Temperament temper, int level, int misses)
        {
            int needed = VenturesCatalogue.MissesToCall[VenturesCatalogue.Level(level)];
            return temper == Temperament.Demanding && needed > 0 && misses >= needed;
        }

        /// <summary>What an investor calls back: what they put in, less half of what they have been paid, and a quarter of what they put in at least.</summary>
        public static int CallSum(Deal deal)
        {
            return Math.Max(deal.Amount * VenturesCatalogue.CallLeastPct / 100, deal.Amount - (int)((long)deal.PaidOut * VenturesCatalogue.CallKeptPct / 100));
        }

        /// <summary>The deal becomes a debt paid by the day; the share of the profit stops.</summary>
        public static void StartCall(Deal deal, int owed, int level)
        {
            deal.Stage = DealStage.Called;
            deal.CallOwed = owed;
            deal.CallPerDay = Math.Max(1, (owed + VenturesCatalogue.CallDays[VenturesCatalogue.Level(level)] - 1) / VenturesCatalogue.CallDays[VenturesCatalogue.Level(level)]);
            deal.CallMisses = 0;
            deal.Arrears = 0;
            deal.BoardUntil = 0;
            deal.CostDay = 0;
            deal.TargetDay = 0;
            deal.FollowOn = false;
        }

        private static bool CallDay(VenturesFile file, Deal deal, IWallet wallet, List<DealEvent> said, Func<int, int> fileClaim, string name)
        {
            int pay = Math.Min(deal.CallPerDay, deal.CallOwed);
            if (pay > 0 && wallet.Cash >= pay && wallet.Take(pay, VenturesMoney.Returned, name))
            {
                deal.CallOwed -= pay;
                deal.CallMisses = 0;
                Say(said, DealNews.CallPaid, deal, pay);
            }
            else if (pay > 0)
            {
                deal.CallMisses++;
                Say(said, DealNews.CallMissed, deal, pay);
            }
            if (deal.CallOwed <= 0)
            {
                Say(said, DealNews.CallDone, deal, 0);
                file.Deals.Remove(deal);
                return true;
            }
            if (deal.CallMisses >= VenturesCatalogue.CallMissesMost && deal.Claim == 0)
            {
                int number = fileClaim != null ? fileClaim(deal.CallOwed) : 0;
                if (number > 0)
                {
                    // The courts have the debt now: it is judged and collected as every civil claim is.
                    deal.Claim = number;
                    file.MoveReputation(VenturesCatalogue.ReputationClaim);
                    Say(said, DealNews.Claimed, deal, deal.CallOwed);
                    file.Deals.Remove(deal);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The answer to a demand: a seat on the board (a larger share for a while; a fund already has one and does not
        /// take this), or a promise to cut the business's daily costs, tested later. A new target period starts.
        /// </summary>
        public static bool Answer(Deal deal, Investor investor, int day, bool board, in BusinessFigures b)
        {
            if (deal.Stage != DealStage.Demand || (board && investor != null && investor.Kind == InvestorKind.Firm))
            {
                return false;
            }
            deal.Stage = DealStage.Running;
            if (board)
            {
                deal.BoardUntil = day + VenturesCatalogue.BoardDays;
            }
            else
            {
                deal.CostTarget = Math.Max(0, (int)Math.Floor(b.AvgCost14 * (100 - VenturesCatalogue.CostCutPct) / 100.0));
                deal.CostDay = day + VenturesCatalogue.CostCutDays;
            }
            deal.TargetDay = day + VenturesCatalogue.TargetDays[investor != null ? (int)investor.Temper : (int)Temperament.Even];
            return true;
        }

        /// <summary>The price to buy an investor out today: what is still called if their money is called, else the greater of their share of so many days of recent profit and a little over what they put in.</summary>
        public static int BuyOutPrice(Deal deal, in BusinessFigures b)
        {
            if (deal.Stage == DealStage.Called)
            {
                return deal.CallOwed;
            }
            return Math.Max(VenturesCatalogue.Share(b.AvgProfit14 * VenturesCatalogue.BuyOutDays, deal.ShareBp), (int)((long)deal.Amount * VenturesCatalogue.BuyOutFloorPct / 100)) + deal.Arrears;
        }

        /// <summary>Buys an investor out at today's price. False, and nothing changed, if the cash is not there.</summary>
        public static bool BuyOut(VenturesFile file, Deal deal, in BusinessFigures b, IWallet wallet)
        {
            int price = BuyOutPrice(deal, b);
            Investor investor = file.Investor(deal.InvestorId);
            if (price > 0 && (wallet.Cash < price || !wallet.Take(price, deal.Stage == DealStage.Called ? VenturesMoney.Returned : VenturesMoney.BuyOut, investor != null ? investor.Name : string.Empty)))
            {
                return false;
            }
            if (deal.Stage != DealStage.Called)
            {
                file.MoveReputation(VenturesCatalogue.ReputationBoughtOut);
            }
            file.Deals.Remove(deal);
            return true;
        }

        private static void Mood(Deal deal, int by)
        {
            deal.Mood = Math.Max(0, Math.Min(100, deal.Mood + by));
        }

        private static void Say(List<DealEvent> said, DealNews what, Deal deal, int amount)
        {
            if (said != null)
            {
                said.Add(new DealEvent { What = what, DealId = deal.Id, InvestorId = deal.InvestorId, Amount = amount, Business = deal.Business });
            }
        }
    }
}
