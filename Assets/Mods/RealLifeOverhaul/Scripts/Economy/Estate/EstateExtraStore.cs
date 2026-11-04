using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What wave 4 added to the property business, in two lines of the save's modData of their own so an old save loads as it was:
    /// <see cref="TenantsKey"/> holds each let unit's named tenant, rent record and improvement request, and each building's last
    /// inspection and booked services; <see cref="RentRingKey"/> holds the city's mean rent a month. Game-free. Parts divided by '^'
    /// (u units, b buildings), records by ';', fields by '|', a payment's or a booking's own fields by ','. A later version only adds
    /// fields at a record's end.
    /// </summary>
    public static class EstateExtraStore
    {
        public const string TenantsKey = "reallife:estate_tenants";
        public const string RentRingKey = "reallife:estate_rent_ring";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------------------------------------- the tenants line

        public static string WriteTenants(EstateState s)
        {
            var t = new StringBuilder(64 + s.Holdings.Count * 96);
            t.Append("v=").Append(Version).Append("^u:");
            bool first = true;
            for (int i = 0; i < s.Holdings.Count && i < EstateCatalogue.HoldingsMost; i++)
            {
                EstateHolding h = s.Holdings[i];
                for (int n = 0; n < h.Units.Count && n < EstateCatalogue.UnitsMost; n++)
                {
                    EstateUnit u = h.Units[n];
                    if (!u.Let || (u.PersonId <= 0 && u.PaymentCount == 0 && u.PeriodFrom <= 0 && u.Improve == 0 && u.Refused == 0))
                    {
                        continue;
                    }
                    t.Append(first ? string.Empty : ";").Append(EstateStore.Clean(h.Place)).Append('|');
                    first = false;
                    Ints(t, n, u.PersonId, u.PersonSince, u.PeriodFrom, u.PeriodLate, u.PeriodPaid, u.Improve, u.ImproveDay, u.ImproveCost, u.Refused, u.PaymentCount);
                    t.Append('|');
                    for (int p = 0; p < u.PaymentCount && p < u.Payments.Length; p++)
                    {
                        RentPayment r = u.Payments[p];
                        t.Append(p > 0 ? "," : string.Empty).Append(r.Due.ToString(Inv)).Append('.').Append(r.Paid.ToString(Inv)).Append('.').Append(r.Amount.ToString(Inv)).Append('.')
                            .Append(((int)r.State).ToString(Inv));
                    }
                }
            }
            t.Append("^b:");
            first = true;
            for (int i = 0; i < s.Holdings.Count && i < EstateCatalogue.HoldingsMost; i++)
            {
                EstateHolding h = s.Holdings[i];
                bool until = false;
                for (int k = 1; k < h.ServiceUntil.Length; k++)
                {
                    until |= h.ServiceUntil[k] > 0;
                }
                if (h.LastInspectDay <= 0 && h.Services.Count == 0 && !until)
                {
                    continue;
                }
                t.Append(first ? string.Empty : ";").Append(EstateStore.Clean(h.Place)).Append('|');
                first = false;
                Ints(t, h.LastInspectDay, h.LastInspectScore, h.LastInspect);
                t.Append('|');
                for (int k = 1; k < h.ServiceUntil.Length; k++)
                {
                    t.Append(k > 1 ? "," : string.Empty).Append(h.ServiceUntil[k].ToString(Inv));
                }
                t.Append('|');
                for (int b = 0; b < h.Services.Count && b < EstateCatalogue.ServicesMost; b++)
                {
                    ServiceBooking v = h.Services[b];
                    t.Append(b > 0 ? "," : string.Empty).Append(v.Kind.ToString(Inv)).Append('.').Append(v.Day.ToString(Inv)).Append('.').Append(v.Fee.ToString(Inv)).Append('.')
                        .Append(Uri.EscapeDataString(v.Contractor ?? string.Empty).Replace(".", "%2E"));
                }
            }
            return t.ToString();
        }

        /// <summary>The saved tenants put onto the state's units and buildings (which the main line made); false when the line cannot be read. Nothing is thrown.</summary>
        public static bool ReadTenants(EstateState s, string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return false;
            }
            try
            {
                string[] parts = text.Split('^');
                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    if (body.Length == 0)
                    {
                        continue;
                    }
                    string[] records = body.Split(';');
                    for (int r = 0; r < records.Length; r++)
                    {
                        if (part[0] == 'u')
                        {
                            ReadUnit(s, records[r]);
                        }
                        else if (part[0] == 'b')
                        {
                            ReadBuilding(s, records[r]);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void ReadUnit(EstateState s, string record)
        {
            string[] f = record.Split('|');
            if (f.Length < 12)
            {
                return;
            }
            EstateHolding h = s.Holding(f[0]);
            EstateUnit u = h != null ? h.Unit(Int(f[1])) : null;
            if (u == null || !u.Let)
            {
                return;
            }
            u.PersonId = Math.Max(0, Int(f[2]));
            u.PersonSince = Math.Max(0, Int(f[3]));
            u.PeriodFrom = Math.Max(0, Int(f[4]));
            u.PeriodLate = Math.Max(0, Int(f[5]));
            u.PeriodPaid = Math.Max(0, Int(f[6]));
            u.Improve = EstateCatalogue.Clamp(Int(f[7]), 0, EstateCatalogue.ImproveKinds);
            u.ImproveDay = Math.Max(0, Int(f[8]));
            u.ImproveCost = Math.Max(0, Int(f[9]));
            u.Refused = Math.Max(0, Int(f[10]));
            int count = EstateCatalogue.Clamp(Int(f[11]), 0, u.Payments.Length);
            string[] payments = f.Length > 12 && f[12].Length > 0 ? f[12].Split(',') : new string[0];
            u.PaymentCount = 0;
            for (int p = 0; p < payments.Length && u.PaymentCount < count; p++)
            {
                string[] q = payments[p].Split('.');
                if (q.Length < 4)
                {
                    continue;
                }
                u.Payments[u.PaymentCount++] = new RentPayment { Due = Int(q[0]), Paid = Int(q[1]), Amount = Math.Max(0, Int(q[2])), State = (byte)EstateCatalogue.Clamp(Int(q[3]), 0, 2) };
            }
        }

        private static void ReadBuilding(EstateState s, string record)
        {
            string[] f = record.Split('|');
            if (f.Length < 4)
            {
                return;
            }
            EstateHolding h = s.Holding(f[0]);
            if (h == null)
            {
                return;
            }
            h.LastInspectDay = Math.Max(0, Int(f[1]));
            h.LastInspectScore = Int(f[2]);
            h.LastInspect = EstateCatalogue.Clamp(Int(f[3]), 0, 2);
            string[] until = f.Length > 4 && f[4].Length > 0 ? f[4].Split(',') : new string[0];
            for (int k = 0; k < until.Length && k + 1 < h.ServiceUntil.Length; k++)
            {
                h.ServiceUntil[k + 1] = Math.Max(0, Int(until[k]));
            }
            h.Services.Clear();
            string[] bookings = f.Length > 5 && f[5].Length > 0 ? f[5].Split(',') : new string[0];
            for (int b = 0; b < bookings.Length && h.Services.Count < EstateCatalogue.ServicesMost; b++)
            {
                string[] q = bookings[b].Split('.');
                if (q.Length < 3)
                {
                    continue;
                }
                int kind = Int(q[0]);
                if (kind < 1 || kind > EstateCatalogue.CallKinds)
                {
                    continue;
                }
                h.Services.Add(new ServiceBooking { Kind = kind, Day = Int(q[1]), Fee = Math.Max(1, Int(q[2])), Contractor = q.Length > 3 ? Uri.UnescapeDataString(q[3]) : string.Empty });
            }
        }

        // ------------------------------------------------------------------------------------------------- the city's rent ring

        public static string WriteRing(EstateState s)
        {
            var t = new StringBuilder(64);
            t.Append("v=").Append(Version).Append('|').Append(s.RentRingMonth.ToString(Inv)).Append('|').Append(s.RentRingCount.ToString(Inv)).Append('|');
            for (int i = 0; i < s.RentRingCount && i < s.RentRing.Length; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(s.RentRing[i].ToString(Inv));
            }
            t.Append('|');
            for (int i = 0; i < s.Marks.Count && i < EstateCatalogue.LandlordsMost; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(Uri.EscapeDataString(s.Marks[i].Id).Replace(".",
                    "%2E")).Append('.').Append(s.Marks[i].Rent.ToString(Inv)).Append('.').Append(s.Marks[i].Prev.ToString(Inv));
            }
            return t.ToString();
        }

        public static bool ReadRing(EstateState s, string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return false;
            }
            try
            {
                string[] f = text.Split('|');
                if (f.Length < 4)
                {
                    return false;
                }
                s.RentRingMonth = Math.Max(0, Int(f[1]));
                int count = EstateCatalogue.Clamp(Int(f[2]), 0, s.RentRing.Length);
                string[] values = f[3].Length > 0 ? f[3].Split(',') : new string[0];
                s.RentRingCount = 0;
                for (int i = 0; i < values.Length && s.RentRingCount < count; i++)
                {
                    s.RentRing[s.RentRingCount++] = Math.Max(0, Int(values[i]));
                }
                s.Marks.Clear();
                string[] marks = f.Length > 4 && f[4].Length > 0 ? f[4].Split(',') : new string[0];
                for (int i = 0; i < marks.Length && s.Marks.Count < EstateCatalogue.LandlordsMost; i++)
                {
                    string[] q = marks[i].Split('.');
                    if (q.Length >= 2 && q[0].Length > 0)
                    {
                        s.Marks.Add(new LandlordMark { Id = Uri.UnescapeDataString(q[0]), Rent = Math.Max(0, Int(q[1])), Prev = q.Length > 2 ? Math.Max(0, Int(q[2])) : 0 });
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Both lines read into a state (a missing line leaves what the state holds).</summary>
        public static void Read(EstateState s, Dictionary<string, string> data)
        {
            string text;
            if (data != null && data.TryGetValue(TenantsKey, out text))
            {
                ReadTenants(s, text);
            }
            if (data != null && data.TryGetValue(RentRingKey, out text))
            {
                ReadRing(s, text);
            }
        }

        public static void Write(EstateState s, Dictionary<string, string> data)
        {
            data[TenantsKey] = WriteTenants(s);
            data[RentRingKey] = WriteRing(s);
        }

        private static void Ints(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int Int(string text)
        {
            int n;
            return int.TryParse(text, NumberStyles.Integer, Inv, out n) ? n : 0;
        }
    }
}
