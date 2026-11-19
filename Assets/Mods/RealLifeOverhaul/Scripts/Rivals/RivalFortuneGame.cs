using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Buildings;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>One of the game's rivals with its fortune, in the Rivals app's order.</summary>
    public struct RivalFortuneRow
    {
        public string Id;
        public string Name;
        public RivalFortune Fortune;
    }

    /// <summary>
    /// The game side of a rival's fortune (<see cref="RivalFortuneRules"/>): the market value and market rent of the buildings the game lists as
    /// the rival's, read the way the game's own real estate table reads them (Building.GetMarketValue, GetBuildingDailyMarketRent). A reading
    /// is kept for each rival until the game day or its number of buildings changes, because a page asks again every time it redraws.
    /// COST: a pass over a rival's buildings once a game day and when a count changes; nothing per frame and nothing at all for a reading kept.
    /// </summary>
    public static class RivalFortuneGame
    {
        private struct Kept
        {
            public int Day;
            public int Count;
            public long Trade;
            public RivalFortune Fortune;
        }

        private static readonly Dictionary<string, Kept> Readings = new Dictionary<string, Kept>(32, StringComparer.Ordinal);
        private static readonly List<RivalData> Ranked = new List<RivalData>(32);

        /// <summary>The fortune of a rival (by its id) owning these buildings with these weekly takings, kept for the day.</summary>
        public static RivalFortune Of(string rivalId, List<BuildingRegistration> owned, float weeklyTakings, int day)
        {
            int count = owned != null ? owned.Count : 0;
            long trade = (long)Math.Round(weeklyTakings);
            if (!string.IsNullOrEmpty(rivalId) && Readings.TryGetValue(rivalId, out Kept k) && k.Day == day && k.Count == count && k.Trade == trade)
            {
                return k.Fortune;
            }
            long property = 0, rent = 0;
            int read = 0;
            for (int i = 0; i < count; i++)
            {
                Building b = GameRegistrations.BuildingOf(owned[i]);
                if (b == null)
                {
                    continue;
                }
                try
                {
                    property += (long)b.GetMarketValue();
                    rent += b.GetBuildingDailyMarketRent();
                    read++;
                }
                catch (Exception)
                {
                    // a building the game cannot value counts as a building and as nothing else
                    read++;
                }
            }
            RivalFortune f = RivalFortuneRules.Make(Math.Max(read, count), property, rent, trade);
            if (!string.IsNullOrEmpty(rivalId))
            {
                Readings[rivalId] = new Kept { Day = day, Count = count, Trade = trade, Fortune = f };
            }
            return f;
        }

        /// <summary>Every ranked rival with its fortune, the top of the Rivals app's ranking first; the number of them.</summary>
        public static int Read(List<RivalFortuneRow> into, int day)
        {
            into.Clear();
            RivalRanking.Read(Ranked);
            for (int i = 0; i < Ranked.Count; i++)
            {
                RivalData r = Ranked[i];
                float trade = 0f;
                try
                {
                    trade = r.WeeklyIncome;
                }
                catch (Exception)
                {
                    // a business with no days on record: the game's own figure is not there
                }
                into.Add(new RivalFortuneRow { Id = r.id, Name = r.rivalName, Fortune = Of(r.id, r.ownedBuildings, trade, day) });
            }
            return into.Count;
        }
    }
}
