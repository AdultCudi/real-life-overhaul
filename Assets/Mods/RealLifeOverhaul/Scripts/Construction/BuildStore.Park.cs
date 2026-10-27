using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Car parks in the save's line, after every part of before (docs/features/construction.md, "Parking lots"): "^j:" the days
    /// the city's builders may next begin one in each district and the money paid back for cars left in the player's own, then a
    /// record each (its figures, its land, its daily list, its leases and its offers). Written only once there is something in it,
    /// so a line of a save with no car park is as before; an older reader passes it over, and a line without it reads as no car
    /// park. A lease and an offer are a group of colon-divided fields with their text escaped, the groups divided by commas, the
    /// whole escaped again in its record.
    /// </summary>
    public static partial class BuildStore
    {
        private static void WritePark(StringBuilder t, BuildState s)
        {
            bool clock = false;
            for (int d = 0; d < s.ParkNextDay.Length; d++)
            {
                clock |= s.ParkNextDay[d] > 0;
            }
            if (s.CarParks.Count == 0 && !clock && s.ParkRefunded <= 0L)
            {
                return;
            }
            t.Append("^j:");
            for (int d = 0; d < s.ParkNextDay.Length; d++)
            {
                t.Append(d > 0 ? "," : string.Empty).Append(s.ParkNextDay[d].ToString(Inv));
            }
            t.Append('|').Append(s.ParkRefunded.ToString(Inv));
            for (int i = Math.Max(0, s.CarParks.Count - BuildState.CarParksMost); i < s.CarParks.Count; i++)
            {
                CarParkFile c = s.CarParks[i];
                t.Append(';');
                Join(t, c.Id, (int)c.Owner, c.Rival, (int)c.Stage, c.Spaces, c.Width, c.Depth, c.Mouth, c.Bays, (int)c.Mode, c.Price, c.Project, c.Day, c.Until, c.SiteId,
                    c.Opened, c.Cars, c.Seen, c.LastDay, c.Others, c.Own);
                t.Append('|').Append(c.Taken.ToString(Inv)).Append('|').Append(c.Earned.ToString(Inv)).Append('|').Append(c.Spent.ToString(Inv));
                t.Append('|').Append(Esc(c.Lot)).Append('|').Append(Esc(c.Rows));
                var daily = new StringBuilder(64);
                for (int k = 0; k < c.Daily.Count; k++)
                {
                    daily.Append(k > 0 ? "," : string.Empty).Append(c.Daily[k].ToString(Inv));
                }
                t.Append('|').Append(daily);
                var leases = new StringBuilder(64);
                for (int k = 0; k < c.Leases.Count && k < CarParkRules.LeasesMost; k++)
                {
                    CarParkLease l = c.Leases[k];
                    leases.Append(k > 0 ? "," : string.Empty).Append(l.Id.ToString(Inv)).Append(':').Append(l.Who.ToString(Inv)).Append(':').Append(l.Bays.ToString(Inv)).Append(':')
                        .Append(l.Monthly.ToString(Inv)).Append(':').Append(l.Start.ToString(Inv)).Append(':').Append(l.PaidTo.ToString(Inv)).Append(':').Append(l.Ending ? '1' : '0')
                        .Append(':').Append(Esc(l.Name));
                }
                t.Append('|').Append(Esc(leases.ToString()));
                var offers = new StringBuilder(64);
                for (int k = 0; k < c.Offers.Count && k < CarParkRules.OffersMost; k++)
                {
                    CarParkOffer o = c.Offers[k];
                    offers.Append(k > 0 ? "," : string.Empty).Append(o.Id.ToString(Inv)).Append(':').Append(o.Who.ToString(Inv)).Append(':').Append(o.Bays.ToString(Inv)).Append(':')
                        .Append(o.Monthly.ToString(Inv)).Append(':').Append(o.Expires.ToString(Inv)).Append(':').Append(Esc(o.Name));
                }
                t.Append('|').Append(Esc(offers.ToString()));
            }
        }

        // The car parks' part; false for a part of another's.
        private static bool ReadPark(char part, string body, BuildState s)
        {
            if (part != 'j')
            {
                return false;
            }
            string[] records = body.Split(';');
            string[] head = records[0].Split('|');
            string[] days = head.Length > 0 ? head[0].Split(',') : new string[0];
            for (int d = 0; d < s.ParkNextDay.Length && d < days.Length; d++)
            {
                s.ParkNextDay[d] = int.TryParse(days[d], NumberStyles.Integer, Inv, out int day) ? Math.Max(0, day) : 0;
            }
            s.ParkRefunded = head.Length > 1 && long.TryParse(head[1], NumberStyles.Integer, Inv, out long refunded) ? Math.Max(0L, refunded) : 0L;
            for (int i = 1; i < records.Length && s.CarParks.Count < BuildState.CarParksMost; i++)
            {
                if (records[i].Length == 0)
                {
                    continue;
                }
                var r = new Fields(records[i]);
                var c = new CarParkFile
                {
                    Id = r.I(0), Owner = (CarParkOwner)BuildCatalogue.Clamp(r.I(0), 0, (int)CarParkOwner.Builder), Rival = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.RivalCount - 1),
                    Stage = (CarParkStage)BuildCatalogue.Clamp(r.I(3), 0, (int)CarParkStage.Closed), Spaces = BuildCatalogue.Clamp(r.I(1), 1, BuildCatalogue.SiteSpacesMost),
                    Width = BuildCatalogue.Clamp(r.I(0), 0, 80), Depth = BuildCatalogue.Clamp(r.I(0), 0, 80), Mouth = BuildCatalogue.Clamp(r.I(1), 1, 4), Bays = BuildCatalogue.Clamp(r.I(0), 0, 2000),
                    Mode = (CarParkMode)BuildCatalogue.Clamp(r.I(0), 0, (int)CarParkMode.Lease), Price = BuildCatalogue.Clamp(r.I(0), 0, 100000), Project = Math.Max(0, r.I(0)),
                    Day = r.I(0), Until = r.I(0), SiteId = Math.Max(0, r.I(0)), Opened = Math.Max(0, r.I(0)), Cars = BuildCatalogue.Clamp(r.I(0), 0, 2000), Seen = r.I(-1),
                    LastDay = Math.Max(0, r.I(0)), Others = BuildCatalogue.Clamp(r.I(0), 0, 2000), Own = BuildCatalogue.Clamp(r.I(0), 0, 100000)
                };
                c.Taken = Math.Max(0L, r.L(0L));
                c.Earned = Math.Max(0L, r.L(0L));
                c.Spent = Math.Max(0L, r.L(0L));
                c.Lot = r.S();
                c.Rows = r.S();
                foreach (string day in r.S().Split(','))
                {
                    if (day.Length > 0 && int.TryParse(day, NumberStyles.Integer, Inv, out int dollars) && c.Daily.Count < CarParkFile.DailyKept)
                    {
                        c.Daily.Add(dollars);
                    }
                }
                foreach (string group in r.S().Split(','))
                {
                    string[] f = group.Split(':');
                    if (f.Length >= 8 && c.Leases.Count < CarParkRules.LeasesMost && int.TryParse(f[0], NumberStyles.Integer, Inv, out int id) && id > 0)
                    {
                        c.Leases.Add(new CarParkLease
                        {
                            Id = id, Who = BuildCatalogue.Clamp(Int(f[1]), 0, CarParkLease.Player), Bays = BuildCatalogue.Clamp(Int(f[2]), 1, CarParkRules.LeaseBaysMost),
                            Monthly = Math.Max(0L, Long(f[3])),
                            Start = Int(f[4]), PaidTo = Int(f[5]), Ending = f[6] == "1", Name = Unesc(f[7])
                        });
                    }
                }
                foreach (string group in r.S().Split(','))
                {
                    string[] f = group.Split(':');
                    if (f.Length >= 6 && c.Offers.Count < CarParkRules.OffersMost && int.TryParse(f[0], NumberStyles.Integer, Inv, out int id) && id > 0)
                    {
                        c.Offers.Add(new CarParkOffer
                        {
                            Id = id, Who = BuildCatalogue.Clamp(Int(f[1]), 0, CarParkLease.Rival), Bays = BuildCatalogue.Clamp(Int(f[2]), 1, CarParkRules.LeaseBaysMost),
                            Monthly = Math.Max(0L, Long(f[3])),
                            Expires = Int(f[4]), Name = Unesc(f[5])
                        });
                    }
                }
                if (c.Id > 0 && c.Lot.Length > 0)
                {
                    s.CarParks.Add(c);
                }
            }
            return true;
        }

        private static int Int(string text) => int.TryParse(text, NumberStyles.Integer, Inv, out int v) ? v : 0;

        private static long Long(string text) => long.TryParse(text, NumberStyles.Integer, Inv, out long v) ? v : 0L;

        private static string Unesc(string raw)
        {
            try
            {
                return raw.Length == 0 ? string.Empty : Uri.UnescapeDataString(raw);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
