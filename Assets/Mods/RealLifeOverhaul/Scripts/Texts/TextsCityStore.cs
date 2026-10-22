using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What the city's new talk of data v6 keeps in the save, so that a load carries on rather than telling everything
    /// again: where the player's life stood at the last look (TextLifeMark), the life events of the last weeks with who
    /// texted about them and whether gossip passed them on, and what was last seen and said of each of the staff. One line
    /// under its own key, records separated by '^', fields by '|', texts escaped as TextsStore's:
    ///
    ///     v=1^n=day|children|marriedDay|degreeDay|court|listedDay|businessDay|worth|worthDay
    ///        ^e=kind|day|value|child|adopted|texted|done|gossiped^...^s=id|raise|asked|sick|hoursWeek|lastDay|seenDay^...
    ///
    /// New fields go at the end of a record; a record of a kind this version does not know is passed over; a line of a
    /// later version, or one that cannot be read, gives none and is kept in the save as it was read (Core/SaveLines).
    /// Game-free (EconomyCheck, TextsCityChecks.cs). COST: nothing unless a save is read or written.
    /// </summary>
    public sealed class TextsCityStore
    {
        public const string SaveKey = "reallife:texts_city";
        public const int Version = 1;
        public const int StaffMost = 400;   // staff marks kept at most (the game's own staff of the player's businesses)

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public readonly TextLifeMark Mark = new TextLifeMark();
        public readonly List<TextLifeEvent> Events = new List<TextLifeEvent>(8);
        public readonly List<TextStaffMark> Staff = new List<TextStaffMark>(16);

        /// <summary>The mark of one of the staff, made when first asked for; null for no id.</summary>
        public TextStaffMark StaffMark(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            for (int i = 0; i < Staff.Count; i++)
            {
                if (Staff[i].Id == id)
                {
                    return Staff[i];
                }
            }
            if (Staff.Count >= StaffMost)
            {
                Staff.RemoveAt(0);
            }
            var m = new TextStaffMark { Id = id };
            Staff.Add(m);
            return m;
        }

        /// <summary>The marks of staff not seen for a week let go (they left), and the events past their days. Returns how many went.</summary>
        public int Prune(int day)
        {
            int gone = TextsCityRules.Prune(Events, day);
            for (int i = Staff.Count - 1; i >= 0; i--)
            {
                if (day - Staff[i].SeenDay > 7)
                {
                    Staff.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }

        public void Clear()
        {
            Mark.Day = -1;
            Mark.Children = Mark.MarriedDay = Mark.DegreeDay = Mark.CourtClosed = Mark.ListedDay = Mark.BusinessDay = 0;
            Mark.Worth = 0;
            Mark.WorthDay = -1;
            Events.Clear();
            Staff.Clear();
        }

        // ---------------------------------------------------------------- the save

        public string Write()
        {
            var t = new StringBuilder(64 + Events.Count * 40 + Staff.Count * 40);
            t.Append("v=").Append(Version.ToString(Inv));
            t.Append("^n=").Append(Mark.Day.ToString(Inv)).Append('|').Append(Mark.Children.ToString(Inv)).Append('|').Append(Mark.MarriedDay.ToString(Inv))
                .Append('|').Append(Mark.DegreeDay.ToString(Inv)).Append('|').Append(Mark.CourtClosed.ToString(Inv)).Append('|').Append(Mark.ListedDay.ToString(Inv))
                .Append('|').Append(Mark.BusinessDay.ToString(Inv)).Append('|').Append(Mark.Worth.ToString(Inv)).Append('|').Append(Mark.WorthDay.ToString(Inv));
            for (int i = 0; i < Events.Count; i++)
            {
                TextLifeEvent e = Events[i];
                t.Append("^e=");
                TextsStore.Escape(e.Kind, t);
                t.Append('|').Append(e.Day.ToString(Inv)).Append('|');
                TextsStore.Escape(e.Value ?? string.Empty, t);
                t.Append('|');
                TextsStore.Escape(e.Child ?? string.Empty, t);
                t.Append('|').Append(e.Adopted ? '1' : '0').Append('|').Append(e.Texted.ToString(Inv)).Append('|').Append(e.Done ? '1' : '0').Append('|').Append(e.Gossiped ? '1' : '0');
            }
            for (int i = 0; i < Staff.Count; i++)
            {
                TextStaffMark m = Staff[i];
                t.Append("^s=");
                TextsStore.Escape(m.Id, t);
                t.Append('|').Append(m.RaiseSeen.ToString(Inv)).Append('|').Append(m.AskTold.ToString(Inv)).Append('|').Append(m.SickTold.ToString(Inv))
                    .Append('|').Append(m.HoursWeek.ToString(Inv)).Append('|').Append(m.LastDay.ToString(Inv)).Append('|').Append(m.SeenDay.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>Reads a line into this store (emptied first). False if it is of a later version or cannot be read (the store is then empty).</summary>
        public bool Read(string text)
        {
            Clear();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] records = text.Split('^');
            if (records[0] != "v=" + Version.ToString(Inv))
            {
                return false;
            }
            for (int i = 1; i < records.Length; i++)
            {
                string r = records[i];
                if (r.Length < 2 || r[1] != '=')
                {
                    Clear();
                    return false;
                }
                string[] f = r.Substring(2).Split('|');
                bool ok;
                switch (r[0])
                {
                    case 'n': ok = ReadMark(f); break;
                    case 'e': ok = ReadEvent(f); break;
                    case 's': ok = ReadStaff(f); break;
                    default: ok = true; break;   // (a record of a later version's kind: passed over)
                }
                if (!ok)
                {
                    Clear();
                    return false;
                }
            }
            return true;
        }

        private bool ReadMark(string[] f)
        {
            if (f.Length < 9 || !Int(f[0], out int day) || !Int(f[1], out int children) || !Int(f[2], out int married) || !Int(f[3], out int degree)
                || !Int(f[4], out int court) || !Int(f[5], out int listed) || !Int(f[6], out int business)
                || !long.TryParse(f[7], NumberStyles.Integer, Inv, out long worth) || !Int(f[8], out int worthDay))
            {
                return false;
            }
            Mark.Day = day;
            Mark.Children = children;
            Mark.MarriedDay = married;
            Mark.DegreeDay = degree;
            Mark.CourtClosed = court;
            Mark.ListedDay = listed;
            Mark.BusinessDay = business;
            Mark.Worth = worth;
            Mark.WorthDay = worthDay;
            return true;
        }

        private bool ReadEvent(string[] f)
        {
            if (f.Length < 8 || !Int(f[1], out int day) || !Int(f[5], out int texted))
            {
                return false;
            }
            string value = TextsStore.Unescape(f[2]), child = TextsStore.Unescape(f[3]);
            Events.Add(new TextLifeEvent
            {
                Kind = TextsStore.Unescape(f[0]), Day = day, Value = value.Length > 0 ? value : null, Child = child.Length > 0 ? child : null, Adopted = f[4] == "1",
                Texted = texted, Done = f[6] == "1", Gossiped = f[7] == "1"
            });
            return true;
        }

        private bool ReadStaff(string[] f)
        {
            if (f.Length < 7 || !Int(f[1], out int raise) || !Int(f[2], out int asked) || !Int(f[3], out int sick) || !Int(f[4], out int hours)
                || !Int(f[5], out int last) || !Int(f[6], out int seen))
            {
                return false;
            }
            Staff.Add(new TextStaffMark { Id = TextsStore.Unescape(f[0]), RaiseSeen = raise, AskTold = asked, SickTold = sick, HoursWeek = hours, LastDay = last, SeenDay = seen });
            return true;
        }

        private static bool Int(string s, out int value) => int.TryParse(s, NumberStyles.Integer, Inv, out value);
    }
}
