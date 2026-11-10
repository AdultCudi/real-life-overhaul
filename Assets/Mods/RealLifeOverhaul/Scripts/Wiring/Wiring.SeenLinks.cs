using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.RivalsSeen;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The rivals seen around the city (RivalsSeen/SeenBridges) with the rest of the mod:
        ///
        ///   Tycoons            the rich list's tycoons who have come forward, each as the rivals' own file has them: worth, the week's mark, a counter that holds them
        ///                      back, the businesses they run and the game's rival business their first one stands on
        ///   Seed               the rivals' own seed, for a new file's
        ///   Post               a society line on the City Ledger's Flare account
        ///   SeenLine           a tycoon's line on their panel and the rich list: their car and where they tend to be found
        ///   News.Extra         the morning's City Ledger: the society lines of the day before
        ///
        /// Asked on a day's pass, an hour's or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class SeenLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;

            internal static void Connect()
            {
                SeenBridges.Tycoons = Tycoons;
                SeenBridges.Seed = Seed;
                SeenBridges.Post = (words, hour, minute) => FlareDirector.Instance != null && FlareDirector.Instance.Society(words, hour, minute);
                RivalBridges.SeenLine = tycoon => SeenDirector.Instance != null ? SeenDirector.Instance.TycoonLine(tycoon) : string.Empty;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                SeenBridges.Reset();
                RivalBridges.SeenLine = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static RivalFile Rivals()
            {
                RivalDirector rivals = RivalDirector.Instance;
                return rivals != null && rivals.Running && !rivals.Resting ? rivals.File : null;
            }

            private static int Seed()
            {
                try
                {
                    RivalFile f = Rivals();
                    return f != null ? f.Seed : 0;
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Seed", e);
                    return 0;
                }
            }

            // Each tycoon who has come forward, taken over or not: somebody taken over is gone, and is seen nowhere. One still to come forward is not on the list at all.
            private static void Tycoons(List<SeenSubject> into)
            {
                try
                {
                    RivalFile f = Rivals();
                    if (f == null)
                    {
                        return;
                    }
                    int day = RivalGame.Day;
                    for (int i = 0; i < f.Tycoons.Length; i++)
                    {
                        Tycoon t = f.Tycoons[i];
                        if (t == null || t.ActiveDay < 0)
                        {
                            continue;
                        }
                        var s = new SeenSubject
                        {
                            Key = "t" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), Name = t.Def.Name, Firm = t.Def.Holding, Tycoon = true, Figure = t.Worth,
                            Before = t.WeekAgo, ShopsBefore = -1, Pressed = day < t.SlowUntil || day < t.BarredUntil, Gone = !t.Active
                        };
                        for (int o = 0; o < f.Outlets.Count; o++)
                        {
                            RivalOutlet outlet = f.Outlets[o];
                            if (outlet == null || outlet.Tycoon != i)
                            {
                                continue;
                            }
                            s.Shops++;
                            if (s.Office.Length == 0 && !string.IsNullOrEmpty(outlet.Seat))
                            {
                                s.Office = outlet.Seat;   // the game's rival business their outlet stands on: its address, as the city's table writes one
                                s.District = outlet.District ?? string.Empty;
                            }
                        }
                        into.Add(s);
                    }
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Tycoons", e);
                }
            }

            // The society lines of yesterday in the morning's City Ledger, after the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Paper (the pieces before)", e);
                }
                try
                {
                    SeenDirector.Instance?.Stories(day, into);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Paper", e);
                }
            }
        }
    }
}
