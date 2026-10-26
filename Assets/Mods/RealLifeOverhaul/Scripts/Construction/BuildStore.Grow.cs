using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The city's growth in the save's line, after every part of before (plans/growables.md): "^x:" the council's zoning of free
    /// squares (a record a column: its strip, its space and a digit a row), "^u:" the grown buildings (the next day one may start,
    /// the money the city had for its land, then a record each). Each is written only once there is something in it, so a line
    /// of a city that has grown nothing is as before; an older reader passes both over, and a line without them reads as the plan's
    /// zoning and nothing grown.
    /// </summary>
    public static partial class BuildStore
    {
        private static void WriteGrow(StringBuilder t, BuildState s)
        {
            if (s.CouncilCells.Count > 0)
            {
                t.Append("^x:");
                int n = 0;
                foreach (KeyValuePair<long, string> kv in s.CouncilCells)
                {
                    if (n >= CouncilKept)
                    {
                        break;
                    }
                    t.Append(n++ > 0 ? ";" : string.Empty).Append((kv.Key / 1000L).ToString(Inv)).Append('|').Append((kv.Key % 1000L).ToString(Inv)).Append('|').Append(Esc(kv.Value));
                }
            }
            if (s.NextGrowDay <= 0 && s.Grows.Count == 0 && s.CityLand <= 0L)
            {
                return;   // a city that has grown nothing writes nothing: its line is as before
            }
            t.Append("^u:").Append(s.NextGrowDay.ToString(Inv)).Append('|').Append(s.CityLand.ToString(Inv)).Append('|').Append(s.CityLandTax.ToString(Inv));
            for (int i = Math.Max(0, s.Grows.Count - BuildState.GrowsMost); i < s.Grows.Count; i++)
            {
                GrowFile g = s.Grows[i];
                t.Append(';');
                Join(t, g.Id, (int)g.Stage, (int)g.Owner, g.Spaces, (int)g.Use, g.Rival, (int)g.Tier, g.Day, g.Until, g.SiteId, g.Handed, g.Condition, g.EmptyDays, g.RunDown ? 1 : 0,
                    g.Week, g.Project);
                t.Append('|').Append(Esc(g.Lot)).Append('|').Append(Esc(g.Holding)).Append('|').Append(Esc(g.Design)).Append('|').Append(g.Price.ToString(Inv));
            }
        }

        private const int CouncilKept = 4000;   // council records kept at most: one a free column of the district, far fewer in any save

        // The growth's parts; false for a part of another's.
        private static bool ReadGrow(char part, string body, BuildState s)
        {
            switch (part)
            {
                case 'x':
                    foreach (string rec in Records(body))
                    {
                        if (s.CouncilCells.Count >= CouncilKept)
                        {
                            break;
                        }
                        var r = new Fields(rec);
                        int strip = r.I(-1), space = r.I(-1);
                        string digits = ZoneRules.Clean(r.S(), 1);
                        if (strip > 0 && space >= 0 && space < 1000 && digits.Length > 0)
                        {
                            s.CouncilCells[CellRules.Key(strip, space)] = digits;
                        }
                    }
                    return true;
                case 'u':
                {
                    string[] records = body.Split(';');
                    var head = new Fields(records[0]);
                    s.NextGrowDay = Math.Max(0, head.I(0));
                    s.CityLand = Math.Max(0L, head.L(0L));
                    s.CityLandTax = Math.Max(0L, head.L(0L));
                    for (int i = 1; i < records.Length && s.Grows.Count < BuildState.GrowsMost; i++)
                    {
                        if (records[i].Length == 0)
                        {
                            continue;
                        }
                        var r = new Fields(records[i]);
                        var g = new GrowFile
                        {
                            Id = r.I(0), Stage = (GrowStage)BuildCatalogue.Clamp(r.I(0), 0, (int)GrowStage.Dropped), Owner = (GrowOwner)BuildCatalogue.Clamp(r.I(0), 0, (int)GrowOwner.Player),
                            Spaces = BuildCatalogue.Clamp(r.I(1), 1, BuildCatalogue.SiteSpacesMost), Use = (BuildUse)BuildCatalogue.Clamp(r.I(0), 0, (int)BuildUse.Count - 1),
                            Rival = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.RivalCount - 1), Tier = (GrowTier)BuildCatalogue.Clamp(r.I(1), 0, (int)GrowTier.Count - 1),
                            Day = r.I(0), Until = r.I(0), SiteId = Math.Max(0, r.I(0)), Handed = Math.Max(0, r.I(0)),
                            Condition = BuildCatalogue.Clamp(r.I(GrowRules.ConditionFull), 0, GrowRules.ConditionFull), EmptyDays = Math.Max(0, r.I(0)), RunDown = r.I(0) == 1,
                            Week = r.I(-1), Project = Math.Max(0, r.I(0))
                        };
                        g.Lot = r.S();
                        g.Holding = r.S();
                        g.Design = r.S();
                        g.Price = Math.Max(0L, r.L(0L));
                        if (g.Id > 0 && g.Lot.Length > 0)
                        {
                            s.Grows.Add(g);
                        }
                    }
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
