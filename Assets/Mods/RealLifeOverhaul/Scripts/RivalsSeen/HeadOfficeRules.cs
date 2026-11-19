using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>An office building of the city that a rival could have for their head office, as the game's buildings give it. Read once for a choice (SeenGame.OfficeSites).</summary>
    public struct OfficeSite
    {
        public string Key;          // "street#number", as the rivals' sources write an office
        public string District;     // the game's own key
        public string Owner;        // the game's id of the rival who owns the building; empty for nobody
        public int Price;           // the game's market price a square metre there, dollars: what the district's standing is read from
    }

    /// <summary>A rival's head office, chosen once and kept (the save's part "o"): theirs for good while the building stays an office of the city.</summary>
    public sealed class SeenHead
    {
        public string Key = string.Empty;       // the rival: SeenSubject.Key
        public string Office = string.Empty;    // the building: "street#number"
        public string District = string.Empty;  // the game's own key of its district
        public bool Owned;                      // the building is one the rival owns; false: an office building chosen for their standing
    }

    /// <summary>
    /// Where each rival has their head office, by rule (game-free, seeded): a real office building of the city, one the rival owns where they own any, else an office building in
    /// the district that fits their rank (the richest rivals in the dearest district by the game's own prices, the others down the districts in step). Chosen once for a rival and kept
    /// in the save; chosen again only when the building has stopped being an office of the city. Two rivals never share a building. Every draw is a hash of the save's seed and the
    /// rival, and the buildings are taken in the order of their addresses, so the same save gives the same offices on every machine. Cost: a sort of the city's offices and a walk of
    /// the rivals, once a rival has none; nothing otherwise.
    /// </summary>
    public static class HeadOfficeRules
    {
        private const int PurposeOwned = 8101, PurposeFit = 8102;

        /// <summary>
        /// The rivals' order by standing: the rich list's tycoons first by worth, then the game's rival owners by their weekly income; a tie by key. Fills <paramref name="order"/> with
        /// places of the list.
        /// </summary>
        public static void RankOrder(IList<SeenSubject> subjects, List<int> order)
        {
            order.Clear();
            for (int i = 0; subjects != null && i < subjects.Count; i++)
            {
                if (subjects[i] != null && !subjects[i].Gone)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) =>
            {
                SeenSubject x = subjects[a], y = subjects[b];
                if (x.Tycoon != y.Tycoon)
                {
                    return x.Tycoon ? -1 : 1;
                }
                if (x.Figure != y.Figure)
                {
                    return x.Figure > y.Figure ? -1 : 1;
                }
                return string.CompareOrdinal(x.Key, y.Key);
            });
        }

        /// <summary>True when somebody of the day's rivals has no head office on record: the city's offices are read only then.</summary>
        public static bool Needs(SeenFile f, IList<SeenSubject> subjects)
        {
            for (int i = 0; subjects != null && i < subjects.Count; i++)
            {
                if (subjects[i] != null && !subjects[i].Gone && f.Head(subjects[i].Key) == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Chooses a head office for every live rival who has none, from the city's offices: one the rival owns first (a seeded pick among them, the lowest addresses first), else
        /// an office building in the district their rank fits, then in the nearest districts either side of it. A building another rival has is never taken. Returns how many were chosen.
        /// </summary>
        public static int Choose(SeenFile f, IList<SeenSubject> subjects, IList<OfficeSite> sites)
        {
            var order = new List<int>(subjects != null ? subjects.Count : 0);
            RankOrder(subjects, order);
            var all = new List<OfficeSite>(sites != null ? sites.Count : 0);
            for (int i = 0; sites != null && i < sites.Count; i++)
            {
                if (!string.IsNullOrEmpty(sites[i].Key))
                {
                    all.Add(sites[i]);
                }
            }
            all.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            List<string> ladder = Ladder(all);
            int made = 0;
            for (int rank = 0; rank < order.Count; rank++)
            {
                SeenSubject s = subjects[order[rank]];
                if (f.Head(s.Key) != null)
                {
                    continue;
                }
                int who = SeenRules.Who(s.Key);
                bool owned = true;
                OfficeSite pick = Pick(f, all, s, null, SeenRules.Roll(f.Seed, who, 0, PurposeOwned), true);
                if (pick.Key == null && ladder.Count > 0)
                {
                    owned = false;
                    int want = Math.Min(ladder.Count - 1, rank * ladder.Count / Math.Max(1, order.Count));
                    for (int step = 0; step < ladder.Count && pick.Key == null; step++)
                    {
                        int down = want + step, up = want - step;
                        if (down < ladder.Count)
                        {
                            pick = Pick(f, all, s, ladder[down], SeenRules.Roll(f.Seed, who, step, PurposeFit), false);
                        }
                        if (pick.Key == null && step > 0 && up >= 0)
                        {
                            pick = Pick(f, all, s, ladder[up], SeenRules.Roll(f.Seed, who, step, PurposeFit), false);
                        }
                    }
                }
                if (pick.Key == null)
                {
                    continue;
                }
                f.Heads.Add(new SeenHead { Key = s.Key, Office = pick.Key, District = pick.District ?? string.Empty, Owned = owned });
                made++;
            }
            return made;
        }

        // The districts of the offices, the dearest first by the mean of the game's price a square metre there; a tie by name.
        private static List<string> Ladder(List<OfficeSite> all)
        {
            var names = new List<string>(8);
            var sums = new List<long>(8);
            var counts = new List<int>(8);
            for (int i = 0; i < all.Count; i++)
            {
                string d = all[i].District ?? string.Empty;
                int at = names.IndexOf(d);
                if (at < 0)
                {
                    at = names.Count;
                    names.Add(d);
                    sums.Add(0);
                    counts.Add(0);
                }
                sums[at] += Math.Max(0, all[i].Price);
                counts[at]++;
            }
            var idx = new List<int>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                idx.Add(i);
            }
            idx.Sort((a, b) =>
            {
                long x = sums[a] / Math.Max(1, counts[a]), y = sums[b] / Math.Max(1, counts[b]);
                return x != y ? (x > y ? -1 : 1) : string.CompareOrdinal(names[a], names[b]);
            });
            var ladder = new List<string>(idx.Count);
            for (int i = 0; i < idx.Count; i++)
            {
                ladder.Add(names[idx[i]]);
            }
            return ladder;
        }

        // A building that fits and no rival has yet: the rival's own (owned), or one in a district owned by nobody or by them; by a seeded pick among those, in address order. Key null with none.
        private static OfficeSite Pick(SeenFile f, List<OfficeSite> all, SeenSubject s, string district, int roll, bool owned)
        {
            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                count += Fits(f, all[i], s, district, owned) ? 1 : 0;
            }
            if (count == 0)
            {
                return default(OfficeSite);
            }
            int want = Math.Min(count - 1, roll * count / 1000);
            for (int i = 0; i < all.Count; i++)
            {
                if (Fits(f, all[i], s, district, owned) && want-- == 0)
                {
                    return all[i];
                }
            }
            return default(OfficeSite);
        }

        private static bool Fits(SeenFile f, OfficeSite o, SeenSubject s, string district, bool owned)
        {
            if (f.HeadAt(o.Key) != null)
            {
                return false;
            }
            if (owned)
            {
                return !string.IsNullOrEmpty(s.Id) && string.Equals(o.Owner, s.Id, StringComparison.Ordinal);
            }
            return string.Equals(o.District ?? string.Empty, district, StringComparison.Ordinal) && (string.IsNullOrEmpty(o.Owner) || string.Equals(o.Owner, s.Id, StringComparison.Ordinal));
        }

        /// <summary>Puts the head offices on the day's rivals: their office is their head office from now on, and the page can say so. A rival with none is as the source had them.</summary>
        public static void Apply(SeenFile f, IList<SeenSubject> subjects)
        {
            for (int i = 0; subjects != null && i < subjects.Count; i++)
            {
                SeenSubject s = subjects[i];
                SeenHead h = s != null ? f.Head(s.Key) : null;
                if (h == null)
                {
                    continue;
                }
                s.Head = h.Office;
                s.HeadDistrict = h.District;
                s.HeadOwned = h.Owned;
                s.Office = h.Office;
            }
        }

        /// <summary>
        /// A head office whose building the city no longer has as an office, or that a rival gone no longer needs, is forgotten, so it is chosen again at the next pass. Returns how many.
        /// </summary>
        public static int Drop(SeenFile f, Func<string, bool> stillOffice, IList<SeenSubject> subjects)
        {
            int dropped = 0;
            for (int i = f.Heads.Count - 1; i >= 0; i--)
            {
                SeenHead h = f.Heads[i];
                bool gone = false;
                for (int k = 0; subjects != null && k < subjects.Count; k++)
                {
                    gone |= subjects[k] != null && subjects[k].Key == h.Key && subjects[k].Gone;
                }
                if (gone || (stillOffice != null && !stillOffice(h.Office)))
                {
                    f.Heads.RemoveAt(i);
                    dropped++;
                }
            }
            return dropped;
        }
    }
}
