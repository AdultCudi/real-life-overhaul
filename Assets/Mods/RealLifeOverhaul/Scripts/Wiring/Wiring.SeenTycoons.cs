using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.RivalsSeen;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The game-free part of SeenLinks: the rich list's tycoons as the rivals seen around the city list them, read from the rivals' own file. Compiled into
        /// EconomyCheck, so the checks drive the very mapping the game runs. COST: a walk of the tycoons and their outlets once a game day; a lookup an hour.
        /// </summary>
        public static class SeenTycoons
        {
            /// <summary>
            /// Each tycoon who has come forward, taken over or not: somebody taken over is gone and seen nowhere, one still to come forward is not on the list at
            /// all. Their worth and its week, a counter of the player's that holds them back, the businesses they run and the game's rival business their first
            /// one stands on (its address is their office, its district theirs).
            /// </summary>
            public static void Map(RivalFile f, int day, List<SeenSubject> into)
            {
                for (int i = 0; f != null && i < f.Tycoons.Length; i++)
                {
                    Tycoon t = f.Tycoons[i];
                    if (t == null || t.ActiveDay < 0)
                    {
                        continue;
                    }
                    var s = new SeenSubject
                    {
                        Key = "t" + i.ToString(CultureInfo.InvariantCulture), Index = i, Name = t.Def.Name, Firm = t.Def.Holding, Tycoon = true, Figure = t.Worth, Before = t.WeekAgo,
                        ShopsBefore = -1, Pressed = day < t.SlowUntil || day < t.BarredUntil, Gone = !t.Active
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

            /// <summary>True while the tycoon at a place of the rich list is still there: come forward and not taken over.</summary>
            public static bool Here(RivalFile f, int index)
            {
                Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                return t != null && t.Active;
            }
        }
    }
}
