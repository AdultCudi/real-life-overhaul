using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The Economy app (OutlookBridges) with what the other features keep: the jobs the cycle moves (the tech sector's unemployment
        /// and the careers' vacancies), the stock market's index and sectors, the council's books, and the demand, market and buildings
        /// of the mod's own districts. It only reads. The Finance app's button and the city's notices open the app through
        /// EconomyBridges.
        /// </summary>
        public static class OutlookLinks
        {
            internal static void Connect()
            {
                OutlookBridges.Jobs = Jobs;
                OutlookBridges.Market = Market;
                OutlookBridges.Stocks = Stocks;
                OutlookBridges.Sectors = Sectors;
                OutlookBridges.SectorName = SectorName;
                OutlookBridges.Council = Council;
                OutlookBridges.District = District;
                EconomyBridges.OpenEconomyApp = tab => OutlookDirector.Instance != null && OutlookDirector.Instance.Open(tab);
                EconomyBridges.EconomyAppOn = () => Settings.EconomyApp && OutlookDirector.Instance != null && OutlookDirector.Instance.Installed;
                EconomyBridges.EconomyVerdict = () => OutlookDirector.Instance?.VerdictWord();
            }

            internal static void Disconnect()
            {
                OutlookBridges.Reset();
                EconomyBridges.OpenEconomyApp = null;
                EconomyBridges.EconomyAppOn = null;
                EconomyBridges.EconomyVerdict = null;
            }

            private static bool Jobs(int cycle, out int unemployment, out int vacancies, out int seekers, out int layoffs)
            {
                unemployment = vacancies = seekers = layoffs = 0;
                if (!Settings.CareersJobs && !Settings.TechPeople)
                {
                    return false;
                }
                try
                {
                    cycle = Mathf.Clamp(cycle, 0, 3);
                    unemployment = HiringRules.Unemployment(cycle);
                    vacancies = Careers.ProRules.OpeningsWanted(cycle, false);
                    seekers = HiringRules.Target(cycle, false);
                    layoffs = Careers.LifeRules.RedundancyByCycle[cycle];
                    return true;
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.Jobs", e);
                    return false;
                }
            }

            private static bool Market(OutlookReadings r)
            {
                try
                {
                    MarketDirector director = MarketDirector.Instance;
                    MarketState s = director != null ? director.State : null;
                    if (s == null || s.IndexLast <= 0)
                    {
                        return false;
                    }
                    r.MarketOn = true;
                    r.MarketOpen = director.Trading;
                    r.Stocks = s.IndexLast;
                    r.StocksDayBp = MarketHistory.IndexDayMoveBp(s);
                    for (int sector = 0; sector < (int)MarketSector.Count; sector++)
                    {
                        if (s.SectorLast[sector] <= 0)
                        {
                            continue;
                        }
                        OutlookSector entry = r.NextSector();
                        entry.Id = sector;
                        entry.Last = s.SectorLast[sector];
                        entry.DayBp = MarketHistory.SectorDayMoveBp(s, sector);
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.Market", e);
                    r.MarketOn = false;
                    return false;
                }
            }

            // The index's closes by game day (the market is shut some days), today's live level in the last place.
            private static int Stocks(int endDay, int count, int[] into)
            {
                int found = 0;
                try
                {
                    MarketDirector director = MarketDirector.Instance;
                    MarketState s = director != null ? director.State : null;
                    if (s == null || count <= 0 || into == null)
                    {
                        return 0;
                    }
                    count = Math.Min(count, into.Length);
                    List<Candle> daily = s.IndexDaily;
                    for (int k = daily.Count - 1; k >= 0; k--)
                    {
                        Candle c = daily[k];
                        int slot = count - 1 - (endDay - c.Day);
                        if (slot >= count)
                        {
                            continue;   // a day after the one asked for
                        }
                        if (slot < 0)
                        {
                            break;
                        }
                        if (c.C > 0)
                        {
                            into[slot] = c.C;
                            found++;
                        }
                    }
                    if (s.IndexLast > 0)
                    {
                        found += into[count - 1] == OutlookCatalogue.NoValue ? 1 : 0;
                        into[count - 1] = s.IndexLast;
                    }
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.Stocks", e);
                }
                return found;
            }

            private static int Sectors(int sector, int count, int[] into)
            {
                try
                {
                    MarketDirector director = MarketDirector.Instance;
                    MarketState s = director != null ? director.State : null;
                    if (s == null || sector < 0 || sector >= (int)MarketSector.Count || into == null)
                    {
                        return 0;
                    }
                    List<int> closes = s.SectorDaily[sector];
                    int n = Math.Min(Math.Min(count, into.Length), closes.Count);
                    for (int i = 0; i < n; i++)
                    {
                        into[i] = closes[closes.Count - n + i];
                    }
                    return n;
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.Sectors", e);
                    return 0;
                }
            }

            private static string SectorName(int sector)
            {
                try
                {
                    return MarketText.Sector((MarketSector)Mathf.Clamp(sector, 0, (int)MarketSector.Count - 1));
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.SectorName", e);
                    return string.Empty;
                }
            }

            private static bool Council(OutlookReadings r)
            {
                try
                {
                    CivicDirector civic = CivicDirector.Instance;
                    if (civic == null || !civic.Running || civic.Office == null || civic.Government == null)
                    {
                        return false;
                    }
                    OfficeState state = civic.Office.State;
                    CivicGovernment government = civic.Government;
                    if (state == null || !state.ReserveSet || government.State == null)
                    {
                        return false;
                    }
                    BudgetWeek week = OfficeRules.Week(government.Electorate(), government.State.Levers, state.Budget);
                    r.CouncilOn = true;
                    r.Reserve = Prices.At((int)Mathf.Clamp(state.Reserve, int.MinValue / 2, int.MaxValue / 2));
                    r.Revenue = Prices.At(week.Revenue);
                    r.Spending = Prices.At(week.Spending);
                    for (int d = 0; d < r.Departments.Length && d < state.Budget.Length; d++)
                    {
                        r.Departments[d] = state.Budget[d];
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.Council", e);
                    r.CouncilOn = false;
                    return false;
                }
            }

            private static bool District(string district, int[] demand, out int level, out int growing)
            {
                level = 0;
                growing = -1;
                try
                {
                    int index = DistrictSplit.Index(district);
                    if (index < 0 || !DistrictBuildings.Lives)
                    {
                        return false;
                    }
                    for (int use = 0; use < demand.Length && use < DemandRules.Uses; use++)
                    {
                        demand[use] = DistrictBuildings.Demand(use, index);
                    }
                    level = DistrictBuildings.MarketLevel(index);
                    ConstructionDirector construction = ConstructionDirector.Instance;
                    growing = construction != null && construction.GrowthOn ? construction.GrowingIn(index) : -1;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("OutlookLinks.District", e);
                    return false;
                }
            }
        }
    }
}
