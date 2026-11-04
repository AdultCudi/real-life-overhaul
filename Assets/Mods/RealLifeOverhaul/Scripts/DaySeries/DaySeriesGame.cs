using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul.DaySeries
{
    /// <summary>
    /// The one place the day series reads the game. Called when the day changes, never from a frame. Worth is stored in
    /// thousands of dollars. City businesses are the ones that are not the player's, named as the game names them.
    /// </summary>
    public static class DaySeriesGame
    {
        private static readonly List<string> Names = new List<string>(24);
        private static readonly List<int> Wealth = new List<int>(24);
        private static readonly int[] Usual = { 2000000, 2000000, 2000000 };

        public static void Record(DaySeriesBook book, int day)
        {
            if (book == null || day <= 0)
            {
                return;
            }
            if (PriceCutRules.Due(book, day))
            {
                EndCut(book);   // the 14 days are over: the holds the cut put on come off, the player's own stay
            }
            long cash = RivalGame.Cash;
            book.Set(DaySeriesCatalogue.Cash, day, DaySeriesRules.ClampDollars(cash));
            book.Set(DaySeriesCatalogue.Score, day, CreditLedger.Value);
            book.Set(DaySeriesCatalogue.WorthYou, day, DaySeriesRules.WorthUnits(RivalGame.Worth()));
            Categories(book, day);
            Rivals(book, day);
            Fund(book, day);
            Customers(book, day);
            Borrow(book, day);
            Moves(book, day);
            Retail(book, day);
            CityRetail(book, day);
        }

        /// <summary>Ends the price cut at once or at its end: the one place the book and the player's plans are put back together.</summary>
        public static void EndCut(DaySeriesBook book)
        {
            if (CompanyDirector.Instance != null)
            {
                CompanyDirector.Instance.EndCut(book);
            }
            else
            {
                PriceCutRules.End(book, null);
            }
        }

        /// <summary>Starts the price cut today. Returns how many prices moved; a cut that moved none is not on.</summary>
        public static int StartCut(DaySeriesBook book, int day)
        {
            return CompanyDirector.Instance != null ? CompanyDirector.Instance.StartCut(book, day) : 0;
        }

        private static readonly List<string> CityNames = new List<string>(DaySeriesCatalogue.CityGoodsMost);
        private static readonly List<int> CityCents = new List<int>(DaySeriesCatalogue.CityGoodsMost);

        // The city's shop price of the goods the News pages follow, one a good, in cents. The goods are the same on every machine.
        private static void CityRetail(DaySeriesBook book, int day)
        {
            CityGame.RetailPrices(CityNames, CityCents, DaySeriesCatalogue.CityGoodsMost);
            for (int i = 0; i < CityNames.Count; i++)
            {
                string key = DaySeriesCatalogue.CityPrice(CityNames[i]);
                if (key.Length > 0 && CityCents[i] > 0)
                {
                    book.Set(key, day, CityCents[i]);
                }
            }
        }

        // One retail price a good, in cents, from the player's own plans. A plan of another player is not read.
        private static void Retail(DaySeriesBook book, int day)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.pricingManagerPlans == null)
            {
                return;
            }
            int wrote = 0;
            for (int i = 0; i < save.pricingManagerPlans.Count && wrote < 80; i++)
            {
                var plan = save.pricingManagerPlans[i];
                if (plan == null || plan.appliedPrices == null || !CompanyDirector.PlanMine(plan))
                {
                    continue;
                }
                for (int p = 0; p < plan.appliedPrices.Count && wrote < 80; p++)
                {
                    var price = plan.appliedPrices[p];
                    if (price == null || string.IsNullOrEmpty(price.itemName) || price.price <= 0f)
                    {
                        continue;
                    }
                    string key = DaySeriesCatalogue.Price(price.itemName);
                    if (key.Length == 0)
                    {
                        continue;
                    }
                    if (book.Set(key, day, (int)Math.Round(price.price * 100.0)))
                    {
                        wrote++;
                    }
                }
            }
        }

        // Yesterday's categories: the game still has that day's movements, and today has barely started.
        private static void Categories(DaySeriesBook book, int day)
        {
            if (day <= 1)
            {
                return;
            }
            var flow = new CashFlow();
            flow.Read(CashFlow.Yesterday, day, null);
            for (int i = 0; i < CashCategory.Count; i++)
            {
                long net = (long)Math.Round(flow.CategoryIn[i] + flow.CategoryOut[i]);
                book.Set(DaySeriesCatalogue.Category(i), day - 1, DaySeriesRules.ClampDollars(net));
            }
        }

        private static void Rivals(DaySeriesBook book, int day)
        {
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            int level = Prices.Level;
            for (int i = 0; i < RivalCatalogue.TycoonCount; i++)
            {
                long worth = 0;
                if (file != null && i < file.Tycoons.Length && file.Tycoons[i].Active)
                {
                    worth = file.Tycoons[i].Worth;
                }
                book.Set(DaySeriesCatalogue.Tycoon(i), day, DaySeriesRules.WorthUnits(worth));
            }
            for (int i = 0; i < RivalCatalogue.Fortunes.Length; i++)
            {
                book.Set(DaySeriesCatalogue.Fortune(i), day, DaySeriesRules.WorthUnits(RivalRules.At(RivalCatalogue.Fortunes[i].Worth, level)));
            }
        }

        private static void Fund(DaySeriesBook book, int day)
        {
            OwnBank bank = OwnBankDirector.Instance != null ? OwnBankDirector.Instance.Bank : null;
            long dollars = bank != null ? bank.FundValue / 100L : 0L;
            book.Set(DaySeriesCatalogue.Fund, day, DaySeriesRules.ClampDollars(dollars));
        }

        private static void Customers(DaySeriesBook book, int day)
        {
            BroadbandCompany company = UtilitiesDirector.Instance != null ? UtilitiesDirector.Instance.Company : null;
            Network network = company != null ? company.Network : null;
            int customers = network != null && network.Running ? BroadbandRules.Connections(network) : 0;
            book.Set(DaySeriesCatalogue.Customers, day, customers);
        }

        // Rivals and city businesses, sorted by name so every machine rolls the same loans.
        private static void Borrow(DaySeriesBook book, int day)
        {
            Names.Clear();
            Wealth.Clear();
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            for (int i = 0; file != null && i < file.Tycoons.Length; i++)
            {
                Tycoon t = file.Tycoons[i];
                if (!t.Active || t.Worth <= 0)
                {
                    continue;
                }
                Add(t.Def.Holding, (int)Math.Min(int.MaxValue, t.Worth));
            }
            int level = Prices.Level;
            for (int i = 0; i < RivalCatalogue.Fortunes.Length; i++)
            {
                long worth = RivalRules.At(RivalCatalogue.Fortunes[i].Worth, level);
                Add(RivalCatalogue.Fortunes[i].Name, (int)Math.Min(int.MaxValue, worth));
            }
            GameInstance save = RivalGame.Save;
            if (save != null && save.BuildingRegistrations != null)
            {
                int added = 0;
                for (int i = 0; i < save.BuildingRegistrations.Count && added < 8; i++)
                {
                    BuildingRegistration reg = save.BuildingRegistrations[i];
                    if (reg == null || GoingPublic.Mine(reg))
                    {
                        continue;
                    }
                    string name = RivalGame.Name(reg);
                    if (name == "?" || string.IsNullOrEmpty(name))
                    {
                        continue;
                    }
                    int wealth = 200000 + DaySeriesRules.Draw(book.Seed, 1, name.Length, 4, 400000);
                    Add(name, wealth);
                    added++;
                }
            }
            Sort();
            int banks = BankTerms.Banks != null && BankTerms.Banks.Count > 0 ? Math.Min(BankRules.AppsMost, BankTerms.Banks.Count) : 0;
            if (banks > 0)
            {
                DaySeriesRules.Borrow(book, day, Names.ToArray(), Wealth.ToArray(), banks, Usual);
            }
        }

        // A rival who borrowed or bought shares today is in the feed as well, under the city's largest lender.
        private static void Moves(DaySeriesBook book, int day)
        {
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            if (file == null || BankTerms.Banks == null || BankTerms.Banks.Count == 0)
            {
                return;
            }
            for (int i = 0; i < file.Moves.Count; i++)
            {
                RivalMove m = file.Moves[i];
                if (m.Announced != day || (m.Kind != RivalMoveKind.Borrowed && m.Kind != RivalMoveKind.Shares))
                {
                    continue;
                }
                string who = m.Tycoon >= 0 && m.Tycoon < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[m.Tycoon].Holding : null;
                if (!string.IsNullOrEmpty(who))
                {
                    book.AddLoan(who, 0, day, (int)Math.Max(1000L, m.Amount));
                }
            }
        }

        private static void Add(string name, int wealth)
        {
            if (string.IsNullOrEmpty(name) || wealth <= 0 || Names.Count >= 24)
            {
                return;
            }
            for (int i = 0; i < Names.Count; i++)
            {
                if (Names[i] == name)
                {
                    return;
                }
            }
            Names.Add(name);
            Wealth.Add(wealth);
        }

        private static void Sort()
        {
            for (int a = 1; a < Names.Count; a++)
            {
                string name = Names[a];
                int wealth = Wealth[a];
                int b = a - 1;
                while (b >= 0 && string.CompareOrdinal(Names[b], name) > 0)
                {
                    Names[b + 1] = Names[b];
                    Wealth[b + 1] = Wealth[b];
                    b--;
                }
                Names[b + 1] = name;
                Wealth[b + 1] = wealth;
            }
        }
    }
}
