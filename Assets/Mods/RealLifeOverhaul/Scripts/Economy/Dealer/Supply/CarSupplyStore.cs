using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The showroom's suppliers in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^s:0,1,2,0,0,1^d:412^k:place,0,2,0,0,0,1;place,...^o:57=1;58=2
    ///
    /// Parts are divided by '^' and begin with a letter and a colon. s is the supplier of each kind, d the day the managers last
    /// ordered, k each showroom's standing order (its place escaped, then a number a kind), o the deliveries a supplier is bringing
    /// (the delivery's id and the supplier). A newer version adds a part with a new letter or numbers at the end of a record: this
    /// reader takes what it knows and puts every number inside its bounds. A save from before has no line: every kind comes from
    /// the wholesaler. Numbers are written and read in the invariant culture.
    /// </summary>
    public static class CarSupplyStore
    {
        public const string Key = "reallife:dealer_supply";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static CarSupplyState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is (Core/SaveLines)
        }

        public static string Write(CarSupplyState s)
        {
            var t = new StringBuilder(96 + s.Keeps.Count * 48 + s.Orders.Count * 8);
            t.Append("v=").Append(Version.ToString(Inv)).Append("^s:");
            for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
            {
                t.Append(k > 0 ? "," : string.Empty).Append(s.Supplier[k].ToString(Inv));
            }
            t.Append("^d:").Append(s.OrderedDay.ToString(Inv));
            if (s.Keeps.Count > 0)
            {
                t.Append("^k:");
                for (int i = 0; i < s.Keeps.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty).Append(Uri.EscapeDataString(s.Keeps[i].Place ?? string.Empty));
                    for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                    {
                        t.Append(',').Append(s.Keeps[i].Keep[k].ToString(Inv));
                    }
                }
            }
            if (s.Orders.Count > 0)
            {
                t.Append("^o:");
                for (int i = 0; i < s.Orders.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty).Append(s.Orders[i].ToString(Inv)).Append('=').Append(s.OrderFrom[i].ToString(Inv));
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static CarSupplyState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version))
            {
                return null;
            }
            var s = new CarSupplyState();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 's':
                        string[] f = body.Split(',');
                        for (int k = 0; k < CarSupplyCatalogue.Kinds && k < f.Length; k++)
                        {
                            s.Supplier[k] = Int(f[k]);
                        }
                        break;
                    case 'd':
                        s.OrderedDay = Int(body.Split(',')[0], -1);
                        break;
                    case 'k':
                        foreach (string record in body.Split(';'))
                        {
                            string[] r = record.Split(',');
                            string place = Uri.UnescapeDataString(r[0]);
                            CarSupplyKeep keep = s.KeepOf(place, true);
                            for (int k = 0; keep != null && k < CarSupplyCatalogue.Kinds && k + 1 < r.Length; k++)
                            {
                                keep.Keep[k] = Int(r[k + 1]);
                            }
                        }
                        break;
                    case 'o':
                        foreach (string record in body.Split(';'))
                        {
                            string[] r = record.Split('=');
                            if (r.Length >= 2 && int.TryParse(r[0], NumberStyles.Integer, Inv, out int id) && id > 0 && s.Orders.Count < DealerCatalogue.DeliveriesMost)
                            {
                                s.Ordered(id, DealerCatalogue.Clamp(Int(r[1]), 0, CarSupplyCatalogue.Suppliers - 1));
                            }
                        }
                        break;
                }
            }
            CarSupplyRules.Bound(s);
            return s;
        }

        private static int Int(string text, int fallback = 0)
        {
            return int.TryParse(text, NumberStyles.Integer, Inv, out int n) ? n : fallback;
        }
    }
}
