using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Seasons;
using RealLifeOverhaul.Suppliers;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Suppliers and contracts (Suppliers/SupplierBridges) with the rest of the mod:
        ///
        ///   KindOf             the city's kinds of goods (Economy/City), never a raw material the exchange prices
        ///   Day                the calendar's season, the weather (the spells and a summer storm day of the seasons, all of the save's own seed) and city event of a day
        ///   Credit             the credit side's rating
        ///   Manager,
        ///   Budget             a manager of the company's with stock orders on at a business who is at work or stood in for, and that manager's budget for the month
        ///   Reserved           the investors' floor of the player's firms (Tech's firm account): a payment to a supplier never takes it
        ///   RivalName,
        ///   RivalMove,
        ///   Outlets            the rival tycoons: their outlets buy by the same rules, out of their worth
        ///   SupplyLine         the tycoons' lines on the rich list and their panel
        ///   SupplyComing,
        ///   SupplyBrought      the company's managers count what a supply contract brings when they order stock
        ///   News.Extra         the morning's City Ledger: a supplier in trouble, gone under, back or grown, and shops left short
        ///
        /// Asked on a day's or an hour's pass or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class SupplierLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<SupplyStory> Stories = new List<SupplyStory>(2);

            internal static void Connect()
            {
                SupplierBridges.KindOf = KindOf;
                SupplierBridges.Day = Day;
                SupplierBridges.Credit = Credit;
                SupplierBridges.Manager = Manager;
                SupplierBridges.Budget = Budget;
                SupplierBridges.Reserved = Reserved;
                SupplierBridges.RivalName = RivalName;
                SupplierBridges.RivalMove = RivalMove;
                SupplierBridges.Outlets = Outlets;
                RivalBridges.SupplyLine = tycoon => SupplierDirector.Instance != null ? SupplierDirector.Instance.RivalLine(tycoon) : string.Empty;
                CompanyBridges.SupplyComing = (reg, item, from, to) => SupplierDirector.Instance != null ? SupplierDirector.Instance.Coming(reg, item, from, to) : 0;
                CompanyBridges.SupplyBrought = (reg, item, day, hour) => SupplierDirector.Instance != null ? SupplierDirector.Instance.BroughtSince(reg, item, day, hour) : 0;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                SupplierBridges.Reset();
                RivalBridges.SupplyLine = null;
                CompanyBridges.SupplyComing = null;
                CompanyBridges.SupplyBrought = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static int KindOf(string item)
            {
                try
                {
                    Func<string, double> exchange = CityBridges.MaterialFactor;
                    double material = exchange != null ? exchange(item) : double.NaN;
                    return material > 0.0 && !double.IsInfinity(material) ? -1 : CityGame.KindOf(item);   // a raw material is the exchange's to price and the futures' to bring
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.KindOf", e);
                    return -1;
                }
            }

            // The weather of a day as a delivery is judged by it, the day before and for the rival shops: only what the mod honestly knows of any day, from the save's own seed
            // and so the same on every load and every machine. The city's economy reads the live sky for today (a storm that is raging); a supplier does not, and a summer
            // storm is the seasons' own seeded storm day (SeasonLookRules.RainOn), known a day ahead like the spells (Weather.SpellOf).
            private static CityDay Day(int day)
            {
                try
                {
                    Func<int, CityDay> conditions = CityBridges.Day;
                    CityDay d = conditions != null ? conditions(day) : new CityDay { Season = 1 };
                    bool stormDay = Settings.Seasons && EventCalendar.Salt != 0 && SeasonLookRules.RainOn(day, GameCalendar.DaysPerYear, EventCalendar.Salt).Storm;
                    return SupplierRules.Forecast(d, Settings.WeatherMatters, Settings.Seasons, stormDay);
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Day", e);
                    return new CityDay { Season = 1 };
                }
            }

            private static int Credit()
            {
                try
                {
                    return CreditLedger.File != null ? CreditLedger.File.LastScore : 0;
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Credit", e);
                    return 0;
                }
            }

            private static bool Manager(BuildingRegistration reg)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    return company != null && reg != null && company.DecidesStock(CompanyGame.Place(reg));
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Manager", e);
                    return false;
                }
            }

            private static bool Budget(BuildingRegistration reg, long money)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    return company != null && reg != null && company.SpendStock(CompanyGame.Place(reg), money);
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Budget", e);
                    return false;
                }
            }

            private static long Reserved()
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.FloorNow : 0L;   // cents
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Reserved", e);
                    return 0L;
                }
            }

            // A tycoon while they are forward and the rivals are not resting (a Going Public session); null otherwise.
            private static Tycoon Forward(int index)
            {
                RivalFile f = RivalDirector.Instance != null && !GoingPublic.InSession ? RivalDirector.Instance.File : null;
                Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                return t != null && t.Active ? t : null;
            }

            private static string RivalName(int index)
            {
                try
                {
                    return Forward(index) != null && index < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[index].Name : null;
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.RivalName", e);
                    return null;
                }
            }

            // Supply moves a tycoon's worth: a saving or their supplier's margin adds to it; a cost comes off it, but only off the worth over their floor (the start's
            // worth at today's prices, which the rivals raise a worth to each day: a cost from the floor itself would be made up the next morning).
            private static void RivalMove(int index, long money)
            {
                try
                {
                    Tycoon t = Forward(index);
                    if (t == null || money == 0)
                    {
                        return;
                    }
                    long floor = RivalRules.At(t.Def.StartWorth, Prices.Level);
                    t.Worth += money > 0 ? money : -Math.Min(-money, Math.Max(0L, t.Worth - floor));
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.RivalMove", e);
                }
            }

            private static void Outlets(List<RivalOutletRef> into, List<string> types)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null && !GoingPublic.InSession ? RivalDirector.Instance.File : null;
                    for (int i = 0; f != null && i < f.Outlets.Count; i++)
                    {
                        RivalOutlet o = f.Outlets[i];
                        if (o != null && Forward(o.Tycoon) != null)
                        {
                            into.Add(new RivalOutletRef { Tycoon = o.Tycoon, Key = RivalFirms.Key(o) });
                            types.Add(o.Type ?? string.Empty);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Outlets", e);
                }
            }

            // Yesterday's news of the suppliers in the morning's City Ledger, after the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Paper (the pieces before)", e);
                }
                try
                {
                    SupplierDirector director = SupplierDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    director.Stories(day, Stories);
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        into.Add(new NewsItem { Key = Stories[i].Key, Data = Stories[i].Data });
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("SupplierLinks.Paper", e);
                }
            }
        }
    }
}
