using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>What a deal's day brought, for the director to say.</summary>
    public enum DealNews
    {
        Paid, Short, TargetMet, Warned, Demanded, DemandDefault, PromiseKept, PromiseBroken, WentCold, Called, CallPaid, CallMissed, CallDone, Claimed, BusinessGone, Settled, BoardEnded,
        PaidBack
    }

    /// <summary>One thing a deal's day brought, with the sum it is about.</summary>
    public struct DealEvent
    {
        public DealNews What;
        public int DealId;
        public int InvestorId;
        public int Amount;
        public string Business;
    }

    /// <summary>A deal's day, game-free: the share of yesterday's profit, the target, and after misses a warning, a demand, a call or a cold investor.</summary>
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
                // Sold or closed: what they put in, less what they have been paid, is owed.
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
                    file.MoveReputation(VenturesCatalogue.ReputationMet, deal.Test);
                    Mood(deal, VenturesCatalogue.MoodMet);
                    Say(said, DealNews.TargetMet, deal, deal.TargetProfit);
                    if (deal.PaidOut >= deal.Amount)
                    {
                        // Paid back and the business does what they asked: only their share from now on.
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

        // A target missed or a promise broken. Gone cold before being paid back, an investor never backs the founder again.
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
                    file.MoveReputation(VenturesCatalogue.ReputationCold, deal.Test);
                }
                Say(said, DealNews.WentCold, deal, unpaid ? 1 : 0);
            }
        }

        /// <summary>True if an investor calls their money back after so many misses at this level: only the demanding do, and nobody on Relaxed.</summary>
        public static bool Callable(Temperament temper, int level, int misses)
        {
            int needed = VenturesCatalogue.MissesToCall[VenturesCatalogue.Level(level)];
            return temper == Temperament.Demanding && needed > 0 && misses >= needed;
        }

        /// <summary>What an investor calls back: what they put in less half of what they have been paid, and a quarter of it at least.</summary>
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
                    // The courts have the debt now, judged and collected as every civil claim is.
                    deal.Claim = number;
                    file.MoveReputation(VenturesCatalogue.ReputationClaim, deal.Test);
                    Say(said, DealNews.Claimed, deal, deal.CallOwed);
                    file.Deals.Remove(deal);
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for a called sum missed on as many days as take it to court, with no court that took it: the justice system is off.</summary>
        public static bool NoCourt(Deal deal)
        {
            return deal.Stage == DealStage.Called && deal.Claim == 0 && deal.CallMisses >= VenturesCatalogue.CallMissesMost;
        }

        /// <summary>Answers a demand with a board seat (a fund has one already) or a promise to cut costs; false if there is no demand.</summary>
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

        /// <summary>
        /// Today's buy-out price: what is owed if called, what the next day calls if the business is gone (what they put in
        /// less what they were paid), else the greater of their share of recent profit and a little over their sum.
        /// </summary>
        public static int BuyOutPrice(Deal deal, in BusinessFigures b)
        {
            if (deal.Stage == DealStage.Called)
            {
                return deal.CallOwed;
            }
            if (!b.Exists)
            {
                return Math.Max(0, deal.Amount - deal.PaidOut);
            }
            return Math.Max(VenturesCatalogue.Share(b.AvgProfit14 * VenturesCatalogue.BuyOutDays, deal.ShareBp), (int)((long)deal.Amount * VenturesCatalogue.BuyOutFloorPct / 100)) + deal.Arrears;
        }

        /// <summary>Buys an investor out at today's price. False, and nothing changed, if the cash is not there.</summary>
        public static bool BuyOut(VenturesFile file, Deal deal, in BusinessFigures b, IWallet wallet)
        {
            int price = BuyOutPrice(deal, b);
            Investor investor = file.Investor(deal.InvestorId);
            string why = deal.Stage == DealStage.Called ? VenturesMoney.Returned : VenturesMoney.BuyOut;
            if (price > 0 && (wallet.Cash < price || !wallet.Take(price, why, investor != null ? investor.Name : string.Empty)))
            {
                return false;
            }
            if (deal.Stage != DealStage.Called)
            {
                file.MoveReputation(VenturesCatalogue.ReputationBoughtOut, deal.Test);
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
