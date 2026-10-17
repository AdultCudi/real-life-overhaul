using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>What one day did at a franchised shop: the money that moves, and what the franchisee now asks.</summary>
    public struct FranchiseDay
    {
        public int Takings, Customers;
        public int Royalty, Levy, Support;
        public DayEvent Event;
    }

    /// <summary>
    /// Franchising by rule: applicants and terms, a franchised shop's day, what franchisees ask, how agreements end, and
    /// a shop of the player's under a city chain. Game-free; every chance is <see cref="BrandCatalogue.Dice"/> of the seed.
    /// </summary>
    public static class FranchiseRules
    {
        /// <summary>Whether somebody applies to a brand on offer today, more likely the better known it is city-wide.</summary>
        public static bool Arrives(int seed, Brand b, int day)
        {
            return b.Offered && BrandCatalogue.Dice(seed, b.Id * 31 + 7, day) < BrandCatalogue.ArriveBase + BrandCatalogue.ArrivePerPoint * (b.City / 100);
        }

        /// <summary>An applicant, wholly from the save's seed and their number, wanting one of the districts given.</summary>
        public static Applicant Generate(int seed, int id, int brandId, int day, IList<string> districts)
        {
            var a = new Applicant { Id = id, BrandId = brandId, Until = day + BrandCatalogue.ApplicantDays };
            a.Temper = (Temper)(BrandCatalogue.Dice(seed, id, 1) % 3);
            int t = (int)a.Temper;
            int capital = BrandCatalogue.CapitalLeast[t] + (int)((long)(BrandCatalogue.CapitalMost[t] - BrandCatalogue.CapitalLeast[t]) * BrandCatalogue.Dice(seed, id, 2) / 999);
            a.Capital = capital / 1000 * 1000;
            a.Experience = BrandCatalogue.Dice(seed, id, 3) * (BrandCatalogue.ExperienceMost + 1) / 1000;
            a.ShopsRun = Math.Min(5, a.Experience / 3 + BrandCatalogue.Dice(seed, id, 4) % 2);
            a.ShopsClosed = Math.Min(a.ShopsRun, BrandCatalogue.Dice(seed, id, 5) % (a.Temper == Temper.CornerCutting ? 3 : 2));
            a.Ability = BrandCatalogue.Clamp(BrandCatalogue.AbilityBase[t] + a.Experience * BrandCatalogue.AbilityPerYear - a.ShopsClosed * 6 + BrandCatalogue.Dice(seed, id, 6) % 11 - 5,
                5, 100);
            a.District = districts != null && districts.Count > 0 ? districts[BrandCatalogue.Dice(seed, id, 7) % districts.Count] ?? string.Empty : string.Empty;
            a.Name = BrandCatalogue.NameFirst[BrandCatalogue.Dice(seed, id, 8) % BrandCatalogue.NameFirst.Length] + " "
                     + BrandCatalogue.NameSecond[BrandCatalogue.Dice(seed, id, 9) % BrandCatalogue.NameSecond.Length] + " "
                     + BrandCatalogue.NameLast[BrandCatalogue.Dice(seed, id, 10) % BrandCatalogue.NameLast.Length];
            return a;
        }

        /// <summary>Applicants past their last day go. Returns how many.</summary>
        public static int Lapse(BrandsState s, int day)
        {
            int gone = 0;
            for (int i = s.Applicants.Count - 1; i >= 0; i--)
            {
                if (day > s.Applicants[i].Until)
                {
                    s.Applicants.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }

        public static int Tier(int tier)
        {
            return BrandCatalogue.Clamp(tier, 0, BrandCatalogue.Tiers - 1);
        }

        /// <summary>The entry fee of a tier at today's prices; less for a franchisee's second shop.</summary>
        public static int EntryFee(Applicant a, int tier, int index)
        {
            int fee = CompanyCatalogue.AtPrices(BrandCatalogue.EntryFee[Tier(tier)], index);
            return a != null && a.From != 0 ? CompanyCatalogue.Share(fee, BrandCatalogue.SecondShopShare) : fee;
        }

        public static int FitOut(int index)
        {
            return CompanyCatalogue.AtPrices(BrandCatalogue.FitOut, index);
        }

        /// <summary>How heavy terms are to an applicant: two a step of the fee and of the royalty, one a step of the levy, less for a district of their own.</summary>
        public static int Burden(int feeTier, int royaltyTier, int levyTier, bool exclusive)
        {
            return Math.Max(0, Tier(feeTier) * 2 + Tier(royaltyTier) * 2 + Tier(levyTier) - (exclusive ? BrandCatalogue.ExclusiveRelief : 0));
        }

        /// <summary>What an applicant will bear: more the better known the brand is where they want to trade, and more for the bolder tempers.</summary>
        public static int Bears(Temper temper, int rec)
        {
            return BrandCatalogue.BearBase + BrandCatalogue.Clamp(rec, 0, BrandCatalogue.RecMost) / BrandCatalogue.BearPerRec
                   + BrandCatalogue.BearTemper[BrandCatalogue.Clamp((int)temper, 0, 2)];
        }

        /// <summary>True if another franchisee holds the district alone, or these terms ask for it alone while another trades there; their own shops do not count.</summary>
        public static bool TerritoryTaken(BrandsState s, Brand b, Applicant a, bool exclusive)
        {
            Franchise own = a.From != 0 ? s.Franchise(a.From) : null;
            for (int i = 0; i < s.Franchises.Count; i++)
            {
                Franchise f = s.Franchises[i];
                if (f.BrandId != b.Id || !string.Equals(f.District, a.District, StringComparison.Ordinal) || (own != null && string.Equals(f.Name, own.Name, StringComparison.Ordinal)))
                {
                    continue;
                }
                if (f.Exclusive || exclusive)
                {
                    return true;
                }
            }
            return false;
        }

        public static Refusal Answer(BrandsState s, Brand b, Applicant a, int feeTier, int royaltyTier, int levyTier, bool exclusive, int index)
        {
            if (s.Franchises.Count >= BrandCatalogue.FranchisesMost)
            {
                return Refusal.Full;
            }
            if (TerritoryTaken(s, b, a, exclusive))
            {
                return Refusal.Territory;
            }
            if (a.Capital < EntryFee(a, feeTier, index) + FitOut(index))
            {
                return Refusal.NoCapital;
            }
            return Burden(feeTier, royaltyTier, levyTier, exclusive) > Bears(a.Temper, BrandRules.Effective(b, a.District)) ? Refusal.TooDear : Refusal.None;
        }

        /// <summary>
        /// The agreement is signed: the applicant becomes a franchisee with their capital less the fee and the fit-out,
        /// out of the first shop's cash for a second shop. The caller fills in the shop itself.
        /// </summary>
        public static Franchise Sign(BrandsState s, Brand b, Applicant a, int feeTier, int royaltyTier, int levyTier, bool exclusive, int lengthTier, int index, int day, int yearDays)
        {
            int fee = EntryFee(a, feeTier, index);
            var f = new Franchise
            {
                Id = s.NextId++, BrandId = b.Id, Name = a.Name, Temper = a.Temper, Ability = a.Ability, District = a.District, Signed = day,
                Term = BrandCatalogue.LengthYears[Tier(lengthTier)] * Math.Max(8, yearDays), EntryFee = fee, RoyaltyBp = BrandCatalogue.RoyaltyBp[Tier(royaltyTier)],
                LevyBp = BrandCatalogue.LevyBp[Tier(levyTier)], Exclusive = exclusive, Health = a.Capital - fee - FitOut(index), SeenDay = day
            };
            f.Until = day + f.Term;
            f.True = Target(f, day) * 100;
            Franchise first = a.From != 0 ? s.Franchise(a.From) : null;
            if (first != null)
            {
                first.Health -= a.Capital;
            }
            s.Franchises.Add(f);
            s.Applicants.Remove(a);
            BrandRules.District(b, f.District, true);
            return f;
        }

        /// <summary>An ordinary day's takings at a shop: the game's customers for it, at what a customer spends at the brand's member shops.</summary>
        public static int Ordinary(int customers, int ticket)
        {
            return (int)Math.Min(int.MaxValue / 8, ((long)Math.Max(0, customers) * Math.Max(0, ticket) + 50) / 100);
        }

        /// <summary>An ordinary day's takings by each factor in turn; never falls when any of the first seven rises, nor rises with more shops beside it.</summary>
        public static int Takings(int customers, int ticket, int rec, int income, CycleState cycle, int ability, int standards, int others, int seed, int day)
        {
            long t = Ordinary(customers, ticket);
            t = t * (BrandCatalogue.AbilityLeast + BrandCatalogue.AbilityPerPoint * BrandCatalogue.Clamp(ability, 0, 100)) / 1000;
            t = t * (BrandCatalogue.RecLeast + BrandCatalogue.RecPerPoint * (BrandCatalogue.Clamp(rec, 0, BrandCatalogue.RecMost) / 100)) / 1000;
            t = t * (BrandCatalogue.IncomeBase + BrandCatalogue.IncomePerPoint * BrandCatalogue.Clamp(income, 60, 140)) / 1000;
            t = t * BrandCatalogue.CycleTakings[BrandCatalogue.Clamp((int)cycle, 0, BrandCatalogue.CycleTakings.Length - 1)] / 1000;
            t = t * (BrandCatalogue.StandardsBase + BrandCatalogue.StandardsPerPoint * BrandCatalogue.Clamp(standards, 0, 100)) / 1000;
            t = t * (1000 - Math.Min(BrandCatalogue.CrowdMost, BrandCatalogue.CrowdEach * Math.Max(0, others))) / 1000;
            t = t * (1000 - BrandCatalogue.NoiseMost + BrandCatalogue.Dice(seed, day, 11) * (2 * BrandCatalogue.NoiseMost + 1) / 1000) / 1000;
            return (int)Math.Min(t, int.MaxValue / 8);
        }

        /// <summary>A share of takings in ten-thousandths, to the nearest whole unit.</summary>
        public static int Royalty(int takings, int bp)
        {
            return BrandCatalogue.Bp(Math.Max(0, takings), Math.Max(0, bp));
        }

        /// <summary>What supporting one franchised shop costs the brand in a day, at today's prices.</summary>
        public static int Support(int takings, int index)
        {
            return CompanyCatalogue.AtPrices(BrandCatalogue.SupportBase, index) + BrandCatalogue.Bp(Math.Max(0, takings), BrandCatalogue.SupportBp);
        }

        /// <summary>Where a franchisee's standards head, points: by temperament and ability, lifted by a warning or retraining, lower while short of cash.</summary>
        public static int Target(Franchise f, int day)
        {
            return BrandCatalogue.Clamp(BrandCatalogue.StandardsTarget[BrandCatalogue.Clamp((int)f.Temper, 0, 2)] + (f.Ability - 50) / BrandCatalogue.AbilityPull
                                        + (day < f.WarnedUntil ? BrandCatalogue.WarnedLift : 0) + (day < f.RetrainedUntil ? BrandCatalogue.RetrainedLift : 0)
                                        - (f.Trouble ? BrandCatalogue.TroubleDrop : 0), 0, 100);
        }

        /// <summary>True while the franchisee has something to complain of: the top royalty, a warning on file, or levy left unspent.</summary>
        public static bool Grievance(Brand b, Franchise f, int index)
        {
            return f.RoyaltyBp >= BrandCatalogue.RoyaltyBp[BrandCatalogue.Tiers - 1] || f.Warnings > 0 || b.Fund > CompanyCatalogue.AtPrices(BrandCatalogue.FundIdleMost, index);
        }

        /// <summary>What a franchisee claims back in a dispute: some ordinary days of royalty and levy.</summary>
        public static int DisputeClaim(Brand b, Franchise f)
        {
            int ordinary = Ordinary(f.Base, b.Ticket);
            return (Royalty(ordinary, f.RoyaltyBp) + Royalty(ordinary, f.LevyBp)) * BrandCatalogue.DisputeDays;
        }

        /// <summary>What a franchisee bids for the brand: days of what the franchises bring in after support, and a sum for each point of city-wide recognition.</summary>
        public static int BidPrice(Brand b, int index)
        {
            long price = (long)Math.Max(0, b.Net) * BrandCatalogue.BidDays + (long)CompanyCatalogue.AtPrices(BrandCatalogue.BidPerPoint, index) * (b.City / 100);
            return (int)Math.Min(price, int.MaxValue / 8);
        }

        /// <summary>
        /// One day at a franchised shop: its takings and what they pay, the franchisee's cash and standards, then at most
        /// one event (it closes, runs out, or the franchisee asks). An ask past its day is dropped first. The caller books the money.
        /// </summary>
        public static FranchiseDay Day(BrandsState s, Brand b, Franchise f, int day, int income, CycleState cycle, int index)
        {
            var r = new FranchiseDay();
            int seed = s.Seed ^ (f.Id * 7919);
            int ordinary = Ordinary(f.Base, b.Ticket);
            r.Takings = Takings(f.Base, b.Ticket, BrandRules.Effective(b, f.District), income, cycle, f.Ability, f.StandardsNow, BrandRules.Shops(s, b, f.District) - 1, seed, day);
            r.Customers = b.Ticket > 0 ? (int)((long)r.Takings * 100 / b.Ticket) : 0;
            bool waived = day < f.WaivedUntil;
            r.Royalty = waived ? 0 : Royalty(r.Takings, f.RoyaltyBp);
            r.Levy = waived ? 0 : Royalty(r.Takings, f.LevyBp);
            r.Support = Support(r.Takings, index);

            int fixedBp = BrandCatalogue.FixedBp - (f.Temper == Temper.CornerCutting ? BrandCatalogue.CutterSaving : 0);
            f.Health += BrandCatalogue.Bp(r.Takings, BrandCatalogue.MarginBp) - r.Royalty - r.Levy - BrandCatalogue.Bp(ordinary, fixedBp);
            f.Takings = r.Takings;
            f.Customers = r.Customers;
            f.Royalty = r.Royalty + r.Levy;
            f.Paid += r.Royalty + r.Levy;
            f.Trouble = f.Health < (long)ordinary * BrandCatalogue.TroubleDays;
            int noise = BrandCatalogue.Dice(seed, day, 12) * (2 * BrandCatalogue.DriftNoise + 1) / 1000 - BrandCatalogue.DriftNoise;
            f.True = BrandCatalogue.Clamp(f.True + BrandCatalogue.Bp(Target(f, day) * 100 - f.True, BrandCatalogue.DriftBp) + noise, 0, 10000);

            if (f.Health < -(long)ordinary * BrandCatalogue.FailDays)
            {
                r.Event = DayEvent.Failed;
                return r;
            }
            if (day >= f.Until)
            {
                r.Event = DayEvent.Lapsed;
                return r;
            }
            bool renewal = f.Until - day <= BrandCatalogue.RenewalNotice && !f.NoRenewal;
            if (f.Ask != Ask.None)
            {
                // A dispute before the court waits for the court; anything else gives way to the renewal when its time comes.
                if (f.Suit != 0 || f.Ask == Ask.Renewal || (day <= f.AskUntil && !renewal))
                {
                    return r;
                }
                Drop(b, f, day);
            }
            if (renewal)
            {
                Put(f, Ask.Renewal, f.Until, CompanyCatalogue.Share(f.EntryFee, BrandCatalogue.RenewalShare));
                r.Event = DayEvent.AsksRenewal;
                return r;
            }
            if (day < f.AskRest)
            {
                return r;
            }
            if (f.Trouble)
            {
                Put(f, Ask.Help, day + BrandCatalogue.AskDays, ordinary * BrandCatalogue.HelpDays);
                r.Event = DayEvent.AsksHelp;
                return r;
            }
            if (f.Temper != Temper.Careful && Grievance(b, f, index) && BrandCatalogue.Dice(seed, day, 13) < BrandCatalogue.DisputeChance)
            {
                Put(f, Ask.Dispute, day + BrandCatalogue.AskDays, DisputeClaim(b, f));
                r.Event = DayEvent.Disputes;
                return r;
            }
            if (day < b.AskRest)
            {
                return r;
            }
            if (f.Temper != Temper.CornerCutting && day - f.Signed >= BrandCatalogue.SecondAfterDays && f.Health >= (long)ordinary * BrandCatalogue.SecondCashDays
                && BrandCatalogue.Dice(seed, day, 14) < BrandCatalogue.SecondChance)
            {
                Put(f, Ask.SecondShop, day + BrandCatalogue.AskDays, 0);
                b.AskRest = day + BrandCatalogue.AskRestDays;
                r.Event = DayEvent.AsksSecond;
                return r;
            }
            if (f.Temper == Temper.Ambitious && b.Bid == 0 && day - f.Signed >= BrandCatalogue.BidAfterDays && f.Health >= (long)ordinary * BrandCatalogue.BidCashDays
                && BrandCatalogue.Dice(seed, day, 15) < BrandCatalogue.BidChance)
            {
                b.Bid = BidPrice(b, index);
                b.BidBy = f.Id;
                b.BidUntil = day + BrandCatalogue.AskDays;
                b.AskRest = day + BrandCatalogue.AskRestDays;
                Put(f, Ask.BuyBrand, b.BidUntil, b.Bid);
                r.Event = DayEvent.Bids;
            }
            return r;
        }

        private static void Put(Franchise f, Ask ask, int until, int amount)
        {
            f.Ask = ask;
            f.AskUntil = until;
            f.AskAmount = Math.Max(0, amount);
        }

        /// <summary>An ask is over, answered or not: nothing of its kind comes again for a while, and a bid of this franchisee's is withdrawn.</summary>
        public static void Drop(Brand b, Franchise f, int day)
        {
            if (f.Ask == Ask.BuyBrand && b != null && b.BidBy == f.Id)
            {
                b.Bid = b.BidBy = b.BidUntil = 0;
            }
            if (f.Ask == Ask.Renewal)
            {
                f.NoRenewal = true;   // asked once: the agreement runs out on its day
            }
            f.Ask = Ask.None;
            f.AskUntil = f.AskAmount = 0;
            f.AskRest = day + BrandCatalogue.AskRestDays;
        }

        /// <summary>Keeps the week's average of what the franchises bring in a day after support.</summary>
        public static void NoteNet(Brand b, int net)
        {
            int sum = b.Net * 6 + net;
            b.Net = sum >= 0 ? (sum + 3) / 7 : -((-sum + 3) / 7);
        }

        public static int Inspect(Franchise f, int day)
        {
            f.Seen = f.StandardsNow;
            f.SeenDay = day;
            return f.Seen;
        }

        public static bool InspectionDue(Franchise f, int day)
        {
            return day - f.SeenDay >= BrandCatalogue.InspectDueDays;
        }

        /// <summary>True while a fresh inspection found the shop under the brand's minimum. A brand with no minimum holds nobody to anything.</summary>
        public static bool Evidence(Brand b, Franchise f, int day)
        {
            return b.Minimum > 0 && f.Seen >= 0 && day - f.SeenDay <= BrandCatalogue.FreshDays && f.Seen < b.Minimum;
        }

        public static bool CanWarn(Brand b, Franchise f, int day)
        {
            return Evidence(b, f, day) && day >= f.WarnedUntil;
        }

        /// <summary>A written warning: the standards head higher for a while, and it is on file.</summary>
        public static void Warn(Franchise f, int day)
        {
            f.Warnings++;
            f.WarnedUntil = day + BrandCatalogue.WarnDays;
        }

        public static int RetrainFee(int index)
        {
            return CompanyCatalogue.AtPrices(BrandCatalogue.RetrainFee, index);
        }

        public static int InspectorFee(int index)
        {
            return CompanyCatalogue.AtPrices(BrandCatalogue.InspectorFee, index);
        }

        /// <summary>Retraining, paid by the brand: standards up at once and headed higher for a while, and a little ability for good.</summary>
        public static void Retrain(Franchise f, int day)
        {
            f.True = Math.Min(10000, f.True + BrandCatalogue.RetrainLift);
            f.RetrainedUntil = day + BrandCatalogue.RetrainDays;
            f.Ability = Math.Min(100, f.Ability + 3);
        }

        /// <summary>An agreement may be ended with cause after enough warnings, on a fresh inspection still under the minimum.</summary>
        public static bool HasCause(Brand b, Franchise f, int day)
        {
            return f.Warnings >= BrandCatalogue.CauseWarnings && Evidence(b, f, day);
        }

        /// <summary>What ending an agreement without cause costs: a share of an ordinary day's takings for each day left, capped, and never under a share of the entry fee.</summary>
        public static int Compensation(Brand b, Franchise f, int day)
        {
            int left = BrandCatalogue.Clamp(f.Until - day, 0, BrandCatalogue.CompensationDaysMost);
            return Math.Max(CompanyCatalogue.Share(f.EntryFee, BrandCatalogue.CompensationLeastShare), left * BrandCatalogue.Bp(Ordinary(f.Base, b.Ticket), BrandCatalogue.CompensationBp));
        }

        /// <summary>The brand ends the agreement; without cause the district and the city think less of it. Returns the recognition the district lost.</summary>
        public static int End(BrandsState s, Brand b, Franchise f, bool cause, int day)
        {
            Drop(b, f, day);
            s.Franchises.Remove(f);
            return cause ? 0 : BrandRules.Lose(b, f.District, BrandCatalogue.UnfairLoss, BrandCatalogue.UnfairCityLoss);
        }

        /// <summary>The franchisee has closed: the brand's name is on a shut shop. Returns the recognition the district lost.</summary>
        public static int Fail(BrandsState s, Brand b, Franchise f, int day)
        {
            Drop(b, f, day);
            s.Franchises.Remove(f);
            b.Failed++;
            return BrandRules.Lose(b, f.District, BrandCatalogue.FailureLoss, CompanyCatalogue.Share(BrandCatalogue.FailureLoss, BrandCatalogue.ScandalCityShare));
        }

        /// <summary>The agreement ran out and was not renewed: the shop leaves the brand with no hard feelings.</summary>
        public static void Lapse(BrandsState s, Brand b, Franchise f, int day)
        {
            Drop(b, f, day);
            s.Franchises.Remove(f);
        }

        /// <summary>Renewed for the same length again. The fee is the caller's to book.</summary>
        public static void Renew(Franchise f)
        {
            f.Until += Math.Max(1, f.Term);
            f.Ask = Ask.None;
            f.AskUntil = f.AskAmount = 0;
            f.NoRenewal = false;
        }

        /// <summary>A grant from the brand goes into the franchisee's cash.</summary>
        public static void Help(Brand b, Franchise f, int grant, int day)
        {
            f.Health += Math.Max(0, grant);
            Drop(b, f, day);
        }

        /// <summary>The other answer to help: the royalty and the levy are waived for a while.</summary>
        public static void Waive(Brand b, Franchise f, int day)
        {
            f.WaivedUntil = day + BrandCatalogue.WaiveDays;
            Drop(b, f, day);
        }

        /// <summary>A second shop agreed: the franchisee joins the applicants with half their cash. Null if the applicants are full, and the ask stands.</summary>
        public static Applicant Second(BrandsState s, Brand b, Franchise f, int day)
        {
            if (s.Applicants.Count >= BrandCatalogue.ApplicantsMost)
            {
                return null;
            }
            var a = new Applicant
            {
                Id = s.NextId++, BrandId = f.BrandId, Name = f.Name, Temper = f.Temper, Ability = f.Ability, Capital = Math.Max(0, f.Health / 2), District = f.District,
                Until = day + BrandCatalogue.ApplicantDays, From = f.Id, ShopsRun = 1
            };
            s.Applicants.Add(a);
            Drop(b, f, day);
            return a;
        }

        /// <summary>A dispute settled by giving way: the royalty goes down a step, or the levy if the royalty is at its lowest. False if there is nothing left to give.</summary>
        public static bool Settle(Brand b, Franchise f, int day)
        {
            bool gave = false;
            for (int i = BrandCatalogue.Tiers - 1; i > 0 && !gave; i--)
            {
                if (f.RoyaltyBp >= BrandCatalogue.RoyaltyBp[i])
                {
                    f.RoyaltyBp = BrandCatalogue.RoyaltyBp[i - 1];
                    gave = true;
                }
            }
            for (int i = BrandCatalogue.Tiers - 1; i > 0 && !gave; i--)
            {
                if (f.LevyBp >= BrandCatalogue.LevyBp[i])
                {
                    f.LevyBp = BrandCatalogue.LevyBp[i - 1];
                    gave = true;
                }
            }
            if (gave)
            {
                f.Suit = 0;
                Drop(b, f, day);
            }
            return gave;
        }

        /// <summary>A dispute turned down or dealt with by the court: it is over, and the franchisee's heart is less in it.</summary>
        public static void Stand(Brand b, Franchise f, int day)
        {
            f.Suit = 0;
            f.True = Math.Max(0, f.True - 500);   // five points of standards
            Drop(b, f, day);
        }

        public static int Chain(int chain)
        {
            return BrandCatalogue.Clamp(chain, 0, BrandCatalogue.Chains - 1);
        }

        public static int ChainFee(int chain, int index)
        {
            return CompanyCatalogue.AtPrices(BrandCatalogue.ChainFee[Chain(chain)], index);
        }

        /// <summary>The customers a chain's name brings from the first day, per cent: its recognition is the same everywhere.</summary>
        public static int ChainFootfall(int chain)
        {
            return BrandRules.RecFootfall(BrandCatalogue.ChainRec[Chain(chain)], 100);
        }

        /// <summary>Outside what the chain asks: standards under its minimum, or prices (thousandths of the market's; 0 for none to judge by) outside its range.</summary>
        public static bool ChainBreach(int chain, int standards, int prices)
        {
            chain = Chain(chain);
            return standards < BrandCatalogue.ChainStandards[chain] || (prices > 0 && (prices < BrandCatalogue.ChainPriceLeast[chain] || prices > BrandCatalogue.ChainPriceMost[chain]));
        }

        /// <summary>Joined today for a year, which renews itself.</summary>
        public static Taken Take(BrandsState s, string place, int chain, int day, int yearDays)
        {
            if (string.IsNullOrEmpty(place) || s.TakenAt(place) != null || s.Member(place) != null || s.Taken.Count >= BrandCatalogue.TakenMost)
            {
                return null;
            }
            var t = new Taken { Place = place, Chain = Chain(chain), From = day, Until = day + Math.Max(8, yearDays) };
            s.Taken.Add(t);
            return t;
        }

        /// <summary>The day's royalty on the shop's takings, kept in the week's average.</summary>
        public static int ChainRoyalty(Taken t, int takings)
        {
            int royalty = Royalty(takings, BrandCatalogue.ChainRoyaltyBp[Chain(t.Chain)]);
            t.Average = (t.Average * 6 + royalty + 3) / 7;
            t.Paid += royalty;
            return royalty;
        }

        /// <summary>A day in or out of what the chain asks: 1 the day it writes, 2 the day it fines, 3 from the day it ends the agreement, else 0.</summary>
        public static int ChainStep(Taken t, bool breach, int day, int yearDays)
        {
            if (day >= t.Until)
            {
                t.Until += Math.Max(8, yearDays);
            }
            if (!breach)
            {
                t.Breach = 0;
                return 0;
            }
            t.Breach++;
            return t.Breach == BrandCatalogue.ChainWarnDays ? 1 : t.Breach == BrandCatalogue.ChainFineDays ? 2 : t.Breach >= BrandCatalogue.ChainEndDays ? 3 : 0;
        }

        /// <summary>The name brings its customers only while the shop is in good standing: not from the day the chain writes.</summary>
        public static bool ChainGives(Taken t)
        {
            return t.Breach < BrandCatalogue.ChainWarnDays;
        }

        public static int ChainFine(Taken t, int index)
        {
            return Math.Max(CompanyCatalogue.AtPrices(BrandCatalogue.ChainFineLeast, index), t.Average * BrandCatalogue.ChainFineDaysOfRoyalty);
        }

        /// <summary>What leaving costs: days of royalty, and never less than a share of the fee at today's prices.</summary>
        public static int ChainExit(Taken t, int index)
        {
            return Math.Max(CompanyCatalogue.Share(ChainFee(t.Chain, index), BrandCatalogue.ChainExitLeastShare), t.Average * BrandCatalogue.ChainExitDays);
        }
    }
}
