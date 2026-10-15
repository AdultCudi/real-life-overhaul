using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The city's faults in a Going Public session (Core/SharedCity, part K): the host rolls every fault and sends them
    /// all, whole, whenever one appears, is taken by a crew or is mended (a dozen at most: a short line); a guest takes the
    /// host's as they are and never rolls its own. A guest that joins late is sent them with the rest of the city's parts
    /// (the snapshot). An RLO that knows no part K skips it (SharedCity looks a part up by its letter and passes over one
    /// it has not). Also the faults' lines of the save (FaultStore), so there is one way to write a fault.
    ///
    ///     K1                                   the head: the layout
    ///     id|kind|district|address|x|y|z|heading|variant|since|reports|case|caseAt|fixAt|crimes|lastCrime
    ///
    /// one line each, '\n' between. The district and the address are escaped ('%', '|' and the line's end). Game-free.
    /// Cost: a short line when a fault changes.
    /// </summary>
    public static class FaultShare
    {
        public const char Part = 'K';
        public const string Head = "K1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The faults, whole, for the guests; null for none to send (no file).</summary>
        public static string Write(FaultFile file)
        {
            if (file == null)
            {
                return null;
            }
            var b = new StringBuilder(32 + file.Faults.Count * 96);
            b.Append(Head);
            for (int i = 0; i < file.Faults.Count; i++)
            {
                b.Append('\n');
                Line(file.Faults[i], b);
            }
            return b.ToString();
        }

        /// <summary>A guest: the host's faults in place of its own. False, with nothing changed, if the text is not a whole part of this layout.</summary>
        public static bool Apply(FaultFile file, string text)
        {
            if (file == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] lines = text.Split('\n');
            if (lines[0] != Head)
            {
                return false;
            }
            var read = new List<Fault>(lines.Length);
            int next = file.NextId;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }
                Fault f = Read(lines[i]);
                if (f == null)
                {
                    return false;
                }
                read.Add(f);
                next = Math.Max(next, f.Id + 1);
            }
            file.Faults.Clear();
            file.Faults.AddRange(read);
            file.NextId = next;
            return true;
        }

        /// <summary>One fault as a line.</summary>
        public static void Line(Fault f, StringBuilder b)
        {
            b.Append(f.Id.ToString(Inv)).Append('|').Append(((int)f.Kind).ToString(Inv)).Append('|').Append(Escape(f.District)).Append('|').Append(Escape(f.Address)).Append('|')
             .Append(f.X.ToString("R", Inv)).Append('|').Append(f.Y.ToString("R", Inv)).Append('|').Append(f.Z.ToString("R", Inv)).Append('|').Append(f.Heading.ToString("R", Inv)).Append('|')
             .Append(f.Variant.ToString(Inv)).Append('|').Append(f.Since.ToString(Inv)).Append('|').Append(f.Reports.ToString(Inv)).Append('|').Append(f.Case.ToString(Inv)).Append('|')
             .Append(f.CaseAt.ToString(Inv)).Append('|').Append(f.FixAt.ToString(Inv)).Append('|').Append(f.Crimes.ToString(Inv)).Append('|').Append(f.LastCrime.ToString(Inv));
        }

        /// <summary>A fault from its line; null if the line is not one. A line with more fields (a later version's) reads its first sixteen.</summary>
        public static Fault Read(string line)
        {
            string[] p = line != null ? line.Split('|') : null;
            if (p == null || p.Length < 16)
            {
                return null;
            }
            var f = new Fault();
            int kind;
            if (!int.TryParse(p[0], NumberStyles.Integer, Inv, out f.Id) || !int.TryParse(p[1], NumberStyles.Integer, Inv, out kind) || kind < 0 || kind >= FaultRules.Kinds
                || !float.TryParse(p[4], NumberStyles.Float, Inv, out f.X) || !float.TryParse(p[5], NumberStyles.Float, Inv, out f.Y) || !float.TryParse(p[6], NumberStyles.Float, Inv, out f.Z)
                || !float.TryParse(p[7], NumberStyles.Float, Inv, out f.Heading) || !int.TryParse(p[8], NumberStyles.Integer, Inv, out f.Variant)
                || !long.TryParse(p[9], NumberStyles.Integer, Inv, out f.Since) || !int.TryParse(p[10], NumberStyles.Integer, Inv, out f.Reports)
                || !int.TryParse(p[11], NumberStyles.Integer, Inv, out f.Case) || !long.TryParse(p[12], NumberStyles.Integer, Inv, out f.CaseAt)
                || !long.TryParse(p[13], NumberStyles.Integer, Inv, out f.FixAt) || !int.TryParse(p[14], NumberStyles.Integer, Inv, out f.Crimes)
                || !long.TryParse(p[15], NumberStyles.Integer, Inv, out f.LastCrime) || float.IsNaN(f.X) || float.IsNaN(f.Z))
            {
                return null;
            }
            f.Kind = (FaultKind)kind;
            f.District = Unescape(p[2]);
            f.Address = Unescape(p[3]);
            return f;
        }

        public static string Escape(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("%", "%25").Replace("|", "%7C").Replace("\n", "%0A").Replace("^", "%5E");
        }

        public static string Unescape(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("%5E", "^").Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%");
        }
    }
}
