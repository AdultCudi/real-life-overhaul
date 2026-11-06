using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city's economy (CityBridges) with the season, the weather and the street's events, business pressure
        /// (footfall through its one writer, copycat rivals, the morning roll), the paper, the Planner's Finance rows and
        /// the raw materials exchange's prices.
        /// </summary>
        public static class CityLinks
        {
            private const int RowDecision = 20, RowSupply = 21;

            internal static void Connect()
            {
                CityBridges.Day = Day;
                CityBridges.Footfall = BusinessDirector.Footfall;
                CityBridges.Copycat = Copycat;
                CityBridges.MaterialFactor = MaterialFactor;
                BusinessGame.Ambient = CityEconomyDirector.FootfallOf;
                BusinessDirector.RollScale = CityEconomyDirector.TroubleScale;
                BusinessDirector.WeightOf = Weight;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                CityBridges.Reset();
                BusinessGame.Ambient = null;
                BusinessDirector.RollScale = null;
                BusinessDirector.WeightOf = null;
                News.Extra = null;
                // The wholesale prices are the game's own again now, not only once the director is destroyed.
                CityGame.RestoreWholesale();
                LendingTerms.BasePoints = 0f;
                AccountRules.BaseBp = 0;
            }

            private static double MaterialFactor(string itemName)
            {
                try
                {
                    Markets.MarketDirector market = Markets.MarketDirector.Instance;
                    return market != null ? market.MaterialFactor(itemName) : double.NaN;
                }
                catch (Exception e)
                {
                    Fault("CityLinks.MaterialFactor", e);
                    return double.NaN;
                }
            }

            private static CityDay Day(int day)
            {
                var d = new CityDay { Season = 1 };
                try
                {
                    d.Season = (int)Weather.SeasonOf(day);
                    if (Settings.WeatherMatters)
                    {
                        Spell spell = Weather.SpellOf(day);
                        bool today = SaveGameManager.Current != null && SaveGameManager.Current.Day == day;
                        d.Weather = spell == Spell.HeatWave ? CityWeather.Heat : spell == Spell.Ice ? CityWeather.Ice : spell == Spell.ColdSnap ? CityWeather.Cold
                            : today && Weather.Sky == Sky.Storm ? CityWeather.Storm : CityWeather.Fair;
                    }
                    switch (EventCalendar.For(day).Kind)
                    {
                        case EventKind.Market: d.Event = CityEvent.Market; break;
                        case EventKind.FarmersMarket: d.Event = CityEvent.FarmersMarket; break;
                        case EventKind.Parade: d.Event = CityEvent.Parade; break;
                        case EventKind.Protest: d.Event = CityEvent.Protest; break;
                        case EventKind.FunRun: d.Event = CityEvent.FunRun; break;
                        case EventKind.FilmShoot: d.Event = CityEvent.FilmShoot; break;
                        case EventKind.Fireworks: d.Event = CityEvent.Fireworks; break;
                    }
                }
                catch (Exception e)
                {
                    Fault("CityLinks.Day", e);
                }
                return d;
            }

            private static bool Copycat(BuildingRegistration reg)
            {
                try
                {
                    BusinessDirector director = BusinessDirector.Instance;
                    return director != null && director.StartIssue(BusinessCatalogue.Undercut, reg);
                }
                catch (Exception e)
                {
                    Fault("CityLinks.Copycat", e);
                    return false;
                }
            }

            private static int Weight(string kind, int own)
            {
                try
                {
                    return kind == BusinessCatalogue.Undercut ? own * CityEconomyDirector.UndercutScale() / 1000 : own;
                }
                catch (Exception e)
                {
                    Fault("CityLinks.Weight", e);
                    return own;
                }
            }

            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    CityEconomyDirector director = CityEconomyDirector.Instance;
                    CityState state = director != null ? director.State : null;
                    if (state == null)
                    {
                        return;
                    }
                    IReadOnlyList<CityNews> pieces = director.Paper(day);
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        Dictionary<string, string> data;
                        string key = CityText.News(pieces[i], state.Base, CityGame.Name, out data);
                        into.Add(new NewsItem { Key = key, Data = data });
                    }
                }
                catch (Exception e)
                {
                    Fault("CityLinks.Paper", e);
                }
            }

            /// <summary>
            /// Rows for the Planner's Finance source: the central bank's announced decision (id 20 with the day) and a
            /// shortage or glut from tomorrow in goods the player stocks (id 21 with the kind). Read only.
            /// </summary>
            internal static void Rows(string source, int today, PlannerList into)
            {
                try
                {
                    CityEconomyDirector director = CityEconomyDirector.Instance;
                    CityState state = director != null ? director.State : null;
                    if (state == null)
                    {
                        return;
                    }
                    if (state.Pending && CityModel.DaysToTurn(state, director.MonthDays) == 1)
                    {
                        PlannerEntry e = into.Add(source, RowDecision, today + 1, PlannerCategory.Money, today + 1);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_macro_plan_rate_title");
                            e.What = CityText.Decision(state.PendingMove, state.PendingReason, MacroCatalogue.Clamp(state.Base + state.PendingMove, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost));
                        }
                    }
                    for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
                    {
                        SupplyLine line = state.Supply[k];
                        if (!line.Announced(state.Age) || (state.Buys & (1 << k)) == 0)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(source, RowSupply, k, PlannerCategory.Money, today + 1);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T(line.Shock == SupplyShock.Shortage ? "reallife_macro_plan_shortage_title" : "reallife_macro_plan_glut_title", "kind", CityText.Kind(k));
                            e.What = CityText.ShockEffect(line.Shock, CityModel.ShockPercent(line, director.Level), line.Days);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("CityLinks.Rows", e);
                }
            }
        }
    }
}
