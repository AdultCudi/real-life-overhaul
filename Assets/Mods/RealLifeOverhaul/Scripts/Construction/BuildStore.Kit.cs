using System;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The builder's licence level and the land's monthly asks in the save's line ("^a:"): the level first, then each district's ask ring as its name and its
    /// months and figures. A line without the part has level 0 (the day's pass earns it again from the builds done) and no asks; a part of a newer version is
    /// read as far as this one knows it. Written only once there is something in it.
    /// </summary>
    public static partial class BuildStore
    {
        private static void WriteKit(StringBuilder t, BuildState s)
        {
            if (s.Firm.Level <= 0 && s.Asks.Count == 0)
            {
                return;
            }
            t.Append("^a:").Append(s.Firm.Level.ToString(Inv));
            for (int i = 0; i < s.Asks.Count; i++)
            {
                AskRing a = s.Asks[i];
                t.Append('|').Append(Esc(a.District)).Append('=');
                for (int k = 0; k < a.Count; k++)
                {
                    t.Append(k > 0 ? "," : string.Empty).Append(a.Months[k].ToString(Inv)).Append(':').Append(a.Asks[k].ToString(Inv));
                }
            }
        }

        // The licence and asks part; false for a part of another's.
        private static bool ReadKit(char part, string body, BuildState s)
        {
            if (part != 'a')
            {
                return false;
            }
            string[] fields = body.Split('|');
            if (fields.Length > 0 && int.TryParse(fields[0], System.Globalization.NumberStyles.Integer, Inv, out int level))
            {
                s.Firm.Level = BuildCatalogue.Clamp(level, 0, LevelRules.Levels - 1);
            }
            for (int i = 1; i < fields.Length && s.Asks.Count < AskRules.DistrictsMost; i++)
            {
                int at = fields[i].IndexOf('=');
                if (at <= 0)
                {
                    continue;
                }
                AskRing ring = s.AskOf(Uri.UnescapeDataString(fields[i].Substring(0, at)), true);
                if (ring == null)
                {
                    continue;
                }
                foreach (string pair in fields[i].Substring(at + 1).Split(','))
                {
                    int colon = pair.IndexOf(':');
                    if (colon > 0 && int.TryParse(pair.Substring(0, colon), System.Globalization.NumberStyles.Integer, Inv, out int month)
                        && long.TryParse(pair.Substring(colon + 1), System.Globalization.NumberStyles.Integer, Inv, out long ask))
                    {
                        ring.Count = AskRules.Put(ring.Months, ring.Asks, ring.Count, month, ask);
                    }
                }
            }
            return true;
        }
    }
}
