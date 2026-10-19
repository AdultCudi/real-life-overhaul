using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One of the player's businesses as it stands now: its place, its firm kind (-1 for a business that is no tech firm) and its name.</summary>
    public struct BusinessNow
    {
        public string Place;
        public int Kind;
        public string Name;
    }

    /// <summary>A move the game's moving company made with the business's settings moved too: the business named <see cref="Name"/> left <see cref="From"/> for <see cref="To"/>.</summary>
    public sealed class MoveSeen
    {
        public string From = string.Empty, To = string.Empty, Name = string.Empty;
        public int Day, Hour;
    }

    /// <summary>What following a move took along, for the log.</summary>
    public struct MoveCount
    {
        public int Firm;          // the firm's own record (its founding, reputation, debt, register, takings of 30 days)
        public int Closed;        // a firm of the player's that stood at the new building before, whose business the move replaced
        public int Products, Contracts, Clients, People, Ads, Caps, CapsDropped;

        public int Total => Firm + Products + Contracts + Clients + People + Ads + Caps;
    }

    /// <summary>Whether the Tech parts follow a move now.</summary>
    public enum MoveVerdict
    {
        No = 0,      // no move of the moving company's accounts for the data, or not without doubt
        Yes = 1,
        Later = 2    // a move to a tech firm while the firms part is not running: asked again when it is
    }

    /// <summary>
    /// The game's moving company can move a business to another building of the player's, its name, type, staff and settings
    /// too (MovingServiceHelper.MoveBusiness, BizManTransfer). The Tech parts keep a business's data under its building's
    /// place key, so after such a move the firm stood at an empty building and was closed with its products. Here the data
    /// follows the move whole: the firm, its products, contracts, professionals, ads and cap table, and a contract's own
    /// client. A move is only followed when the game's own account of it says where the business went and nothing leaves a
    /// doubt; a part not running then follows it when it starts, from <see cref="TalentFile.Moves"/>. Game-free.
    /// </summary>
    public static class MoveRules
    {
        public const int PartPeople = 1, PartHiring = 2, PartFirms = 4, PartVenture = 8, PartsAll = 15;
        public const int RecordsMost = 16;   // moves kept in the save: enough to know a later one from one already followed
        public const int HopsMost = 8;       // moves in a row followed at once (moved, then moved again before the parts looked)
        public const string DoneKey = "ba:messagetype_dialog_moving_service_done";
        public const string DoneSoldKey = "ba:messagetype_dialog_moving_service_done_items_sold";
        public const string OriginField = "businessName", DestinationField = "businessName2";

        /// <summary>True for the moving company's message that a move is done (MovingServiceHelper.SendDoneMessage).</summary>
        public static bool IsDone(string key) => key == DoneKey || key == DoneSoldKey;

        /// <summary>
        /// The name and address in a composed name, "name (address)" as the game's BuildingRegistration.GetComposedName writes
        /// it for a business. False for a bare address, which is what the origin of a move reads once its business has gone.
        /// </summary>
        public static bool Composed(string composed, out string name, out string address)
        {
            name = null;
            address = null;
            if (string.IsNullOrEmpty(composed) || composed.Length < 5 || composed[composed.Length - 1] != ')')
            {
                return false;
            }
            int at = composed.LastIndexOf(" (", System.StringComparison.Ordinal);
            if (at <= 0)
            {
                return false;
            }
            name = composed.Substring(0, at);
            address = composed.Substring(at + 2, composed.Length - at - 3);
            return address.Length > 0;
        }

        public static int IndexOf(IReadOnlyList<BusinessNow> now, string place)
        {
            for (int i = 0; now != null && i < now.Count; i++)
            {
                if (now[i].Place == place)
                {
                    return i;
                }
            }
            return -1;
        }

        // A tech firm's kind at a place now, -1 for a business that is none, -2 for no business of the player's.
        private static int KindAt(IReadOnlyList<BusinessNow> now, string place)
        {
            int i = IndexOf(now, place);
            return i < 0 ? -2 : now[i].Kind;
        }

        private static void Add(List<string> into, string place)
        {
            if (!string.IsNullOrEmpty(place) && !into.Contains(place))
            {
                into.Add(place);
            }
        }

        /// <summary>
        /// The places that hold the parts' data with no business of the player's there now for it: a firm or cap table where no
        /// tech firm stands, the mod's own professionals, ads and a contract's own client where no business stands. Only these
        /// are asked about moves.
        /// </summary>
        public static void Loose(FirmsFile firms, TalentFile talent, HiringFile hiring, VentureFile venture, IReadOnlyList<BusinessNow> now, int day, List<string> into)
        {
            into.Clear();
            for (int i = 0; firms != null && i < firms.Firms.Count; i++)
            {
                if (KindAt(now, firms.Firms[i].Place) < 0)
                {
                    Add(into, firms.Firms[i].Place);
                }
            }
            for (int i = 0; firms != null && i < firms.Contracts.Count; i++)
            {
                Contract w = firms.Contracts[i];
                if (w.Stage <= (int)WorkStage.Active && !string.IsNullOrEmpty(w.OwnPlace) && IndexOf(now, w.OwnPlace) < 0)
                {
                    Add(into, w.OwnPlace);
                }
            }
            for (int i = 0; talent != null && i < talent.People.Count; i++)
            {
                Profile p = talent.People[i];
                if (p.Payroll && !string.IsNullOrEmpty(p.Place) && IndexOf(now, p.Place) < 0)
                {
                    Add(into, p.Place);
                }
            }
            for (int i = 0; hiring != null && i < hiring.Ads.Count; i++)
            {
                JobAd ad = hiring.Ads[i];
                if (day <= ad.To && IndexOf(now, ad.Place) < 0)
                {
                    Add(into, ad.Place);
                }
            }
            for (int i = 0; venture != null && i < venture.Caps.Count; i++)
            {
                if (KindAt(now, venture.Caps[i].Place) < 0)
                {
                    Add(into, venture.Caps[i].Place);
                }
            }
        }

        private static bool Earlier(MoveSeen a, MoveSeen b) => a.Day < b.Day || (a.Day == b.Day && a.Hour < b.Hour);

        private static bool NotBefore(MoveSeen m, int day, int hour) => m.Day > day || (m.Day == day && m.Hour >= hour);

        /// <summary>True if a move is among those the parts have followed.</summary>
        public static bool Recorded(TalentFile talent, MoveSeen m)
        {
            for (int i = 0; talent != null && i < talent.Moves.Count; i++)
            {
                MoveRecord r = talent.Moves[i];
                if (r.From == m.From && r.To == m.To && r.Day == m.Day && r.Hour == m.Hour)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether the data at <paramref name="from"/> follows a move, and where to (through any moves after, to where the business stands
        /// now under its name). A firm's record follows the first move away on or after its founding, to a tech firm of its kind; one the
        /// move replaced is closed. Data with no firm's record follows only the one move away not followed yet, and only while nothing
        /// of the player's stands at the old building. <paramref name="firms"/> is null while the firms part is not running.
        /// </summary>
        public static MoveVerdict Judge(string from, IReadOnlyList<MoveSeen> seen, FirmsFile firms, TalentFile talent, IReadOnlyList<BusinessNow> now,
            out MoveSeen move, out string to)
        {
            move = null;
            to = null;
            if (string.IsNullOrEmpty(from) || seen == null)
            {
                return MoveVerdict.No;
            }
            Firm v = firms != null ? firms.FirmAt(from) : null;
            MoveSeen first = null;
            int open = 0;
            for (int i = 0; i < seen.Count; i++)
            {
                MoveSeen m = seen[i];
                if (m == null || m.From != from || m.To == from || string.IsNullOrEmpty(m.To))
                {
                    continue;
                }
                if (v != null ? m.Day < v.Founded : Recorded(talent, m))
                {
                    continue;
                }
                open++;
                if (first == null || Earlier(m, first))
                {
                    first = m;
                }
            }
            if (first == null || (v == null && open > 1))
            {
                return MoveVerdict.No;
            }
            MoveSeen at = first;
            string place = first.To;
            int here = -1;
            for (int hop = 0; hop <= HopsMost; hop++)
            {
                here = IndexOf(now, place);
                if (here >= 0 && now[here].Name == first.Name)
                {
                    break;
                }
                MoveSeen next = null;
                for (int i = 0; i < seen.Count; i++)
                {
                    MoveSeen m = seen[i];
                    if (m != null && m.From == place && m.To != place && m.Name == first.Name && NotBefore(m, at.Day, at.Hour) && (next == null || Earlier(m, next)))
                    {
                        next = m;
                    }
                }
                here = -1;
                if (next == null)
                {
                    break;
                }
                at = next;
                place = next.To;
            }
            if (here < 0 || place == from)
            {
                return MoveVerdict.No;
            }
            BusinessNow b = now[here];
            if (v != null)
            {
                Firm d = firms.FirmAt(place);
                if (b.Kind != v.Kind || KindAt(now, from) >= 0 || (d != null && d.Founded > at.Day))
                {
                    return MoveVerdict.No;
                }
            }
            else
            {
                if (IndexOf(now, from) >= 0)
                {
                    return MoveVerdict.No;
                }
                if (b.Kind >= 0 && firms == null)
                {
                    return MoveVerdict.Later;
                }
            }
            move = at;
            to = place;
            return MoveVerdict.Yes;
        }

        /// <summary>Every part given follows a move now (a null part is not running). The counts say what went.</summary>
        public static MoveCount Follow(string from, string to, FirmsFile firms, TalentFile talent, HiringFile hiring, VentureFile venture, int day)
        {
            var n = new MoveCount();
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to)
            {
                return n;
            }
            Firms(firms, from, to, day, ref n);
            People(talent, from, to, ref n);
            Ads(hiring, from, to, ref n);
            Caps(venture, from, to, n.Firm > 0, n.Closed > 0, ref n);
            return n;
        }

        /// <summary>The firm, its products and contracts, and a contract's own client follow; a firm at the new building before is closed.</summary>
        public static void Firms(FirmsFile f, string from, string to, int day, ref MoveCount n)
        {
            if (f == null)
            {
                return;
            }
            Firm v = f.FirmAt(from);
            if (v != null)
            {
                Firm d = f.FirmAt(to);
                if (d != null && d != v)
                {
                    FirmsRules.Close(f, d, day);
                    n.Closed++;
                }
                v.Place = to;
                n.Firm++;
            }
            for (int i = 0; i < f.Products.Count; i++)
            {
                if (f.Products[i].Place == from)
                {
                    f.Products[i].Place = to;
                    n.Products++;
                }
            }
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                Contract w = f.Contracts[i];
                if (w.Place == from)
                {
                    w.Place = to;
                    n.Contracts++;
                }
                if (w.OwnPlace == from)
                {
                    w.OwnPlace = to;
                    n.Clients++;
                }
            }
        }

        /// <summary>The professionals at the old building follow (the game's own employees are moved by the game; their files follow too).</summary>
        public static void People(TalentFile t, string from, string to, ref MoveCount n)
        {
            for (int i = 0; t != null && i < t.People.Count; i++)
            {
                if (t.People[i].Place == from)
                {
                    t.People[i].Place = to;
                    n.People++;
                }
            }
        }

        /// <summary>The player's job ads for the old building follow, so a hire starts where the business is.</summary>
        public static void Ads(HiringFile h, string from, string to, ref MoveCount n)
        {
            for (int i = 0; h != null && i < h.Ads.Count; i++)
            {
                if (h.Ads[i].Place == from)
                {
                    h.Ads[i].Place = to;
                    n.Ads++;
                }
            }
        }

        /// <summary>
        /// The cap table follows. Where the firm's record moved (<paramref name="whole"/>) it takes the place of one at the new
        /// building, and the cap table of a firm the move replaced (<paramref name="replaced"/>) goes with that firm; otherwise one
        /// already at the new building is kept and the old one is left as it is.
        /// </summary>
        public static void Caps(VentureFile v, string from, string to, bool whole, bool replaced, ref MoveCount n)
        {
            if (v == null)
            {
                return;
            }
            Cap a = v.CapOf(from), b = v.CapOf(to);
            if (b != null && (replaced || (whole && a != null)))
            {
                v.Caps.Remove(b);
                n.CapsDropped++;
                b = null;
            }
            if (a != null && b == null)
            {
                a.Place = to;
                n.Caps++;
            }
        }

        /// <summary>Keeps a move the parts followed, with the parts that did; the oldest goes past <see cref="RecordsMost"/>.</summary>
        public static MoveRecord Record(TalentFile talent, string from, string to, int day, int hour, int parts, bool whole, bool replaced)
        {
            if (talent == null)
            {
                return null;
            }
            for (int i = 0; i < talent.Moves.Count; i++)
            {
                MoveRecord r = talent.Moves[i];
                if (r.From == from && r.To == to && r.Day == day && r.Hour == hour)
                {
                    r.Parts |= parts & PartsAll;
                    r.Whole |= whole;
                    r.Replaced |= replaced;
                    return r;
                }
            }
            var made = new MoveRecord { From = from, To = to, Day = day, Hour = hour, Parts = parts & PartsAll, Whole = whole, Replaced = replaced };
            talent.Moves.Add(made);
            while (talent.Moves.Count > RecordsMost)
            {
                talent.Moves.RemoveAt(0);
            }
            return made;
        }

        /// <summary>
        /// A part starting follows the moves made while it was not running, oldest first, and is marked as having followed them.
        /// Pass the starting part's file (the others null). Returns the moves it followed.
        /// </summary>
        public static int CatchUp(TalentFile talent, int part, FirmsFile firms, HiringFile hiring, VentureFile venture, int day, ref MoveCount n)
        {
            int followed = 0;
            for (int i = 0; talent != null && i < talent.Moves.Count; i++)
            {
                MoveRecord r = talent.Moves[i];
                if ((r.Parts & part) != 0)
                {
                    continue;
                }
                if (part == PartFirms)
                {
                    int had = n.Firm, closed = n.Closed;
                    Firms(firms, r.From, r.To, day, ref n);
                    r.Whole |= n.Firm > had;
                    r.Replaced |= n.Closed > closed;
                }
                else if (part == PartHiring)
                {
                    Ads(hiring, r.From, r.To, ref n);
                }
                else if (part == PartVenture)
                {
                    Caps(venture, r.From, r.To, r.Whole, r.Replaced, ref n);
                }
                else if (part == PartPeople)
                {
                    People(talent, r.From, r.To, ref n);
                }
                r.Parts |= part;
                followed++;
            }
            return followed;
        }
    }
}
