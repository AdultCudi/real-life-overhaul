using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city's economy with the rest of the mod (Economy/City/CityBridges). The city's economy calls no other
        /// feature and none calls it: what they have to do with each other is here.
        ///
        ///   Day           the season of a game day (the calendar's, Events/Weather.SeasonOf), the spell the weather has
        ///                 rolled for it and a thunderstorm on now (only while "Weather matters" is on), and the street
        ///                 event the calendar has for it (only while street events are on)
        ///   Footfall      a business's promotion figure, written by business pressure's own mechanism
        ///                 (BusinessDirector.Footfall), which asks <see cref="BusinessGame.Ambient"/> for what the city's
        ///                 economy adds: one writer of that figure, whoever asks
        ///   Copycat       a rival opens beside a business that does well: business pressure's own "a competitor
        ///                 undercuts you", started through its own rules (BusinessDirector.StartIssue)
        ///   RollScale     the cycle's chance of a business trouble on business pressure's morning roll
        ///   WeightOf      rivals undercut more often in a slowdown or a recession
        ///
        /// Nothing per district is public in the mod's other features (crime, council building sites and street events
        /// are kept by place or by day, not by neighbourhood), so nothing of theirs pushes a district's income.
        /// </summary>
        public static class CityLinks
        {
            internal static void Connect()
            {
                CityBridges.Day = Day;
                CityBridges.Footfall = BusinessDirector.Footfall;
                CityBridges.Copycat = Copycat;
                BusinessGame.Ambient = CityEconomyDirector.FootfallOf;
                BusinessDirector.RollScale = CityEconomyDirector.TroubleScale;
                BusinessDirector.WeightOf = Weight;
            }

            internal static void Disconnect()
            {
                CityBridges.Reset();
                BusinessGame.Ambient = null;
                BusinessDirector.RollScale = null;
                BusinessDirector.WeightOf = null;
                // The city is unloading or the mod is being switched off: the wholesale prices are the game's own again
                // now, not only when the city's economy's own object is destroyed at the end of the frame.
                CityGame.RestoreWholesale();
                LendingTerms.BasePoints = 0f;
                AccountRules.BaseBp = 0;
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
                return kind == BusinessCatalogue.Undercut ? own * CityEconomyDirector.UndercutScale() / 1000 : own;
            }
        }
    }
}
