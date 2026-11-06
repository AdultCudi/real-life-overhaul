using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Staffing
{
    /// <summary>One entry of the reference: a business or a role, found by its name.</summary>
    public sealed class StaffEntry
    {
        public readonly bool IsPlace;
        public readonly string Id;
        public readonly string NameKey;     // also the entry's slug in the game's help and the key of its title there
        public readonly StaffPlace Place;
        public readonly StaffRole Role;

        public StaffEntry(StaffPlace place)
        {
            IsPlace = true;
            Id = place.Id;
            NameKey = place.NameKey;
            Place = place;
        }

        public StaffEntry(StaffRole role)
        {
            Id = role.Id;
            NameKey = role.NameKey;
            Role = role;
        }

        /// <summary>The key of the entry in the Guide's lists: "b:data_centre" for a business, "r:data_engineer" for a role.</summary>
        public string Key => (IsPlace ? "b:" : "r:") + Id;
    }

    /// <summary>
    /// The reference's lookups, game-free: every entry, the A to Z order, the search the game's own help uses (any part of a
    /// name, capitals and accents ignored), who uses a role, and the checks that hold the table together.
    /// </summary>
    public static class StaffIndex
    {
        private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;   // the game's help searches the same way

        private static StaffEntry[] _entries;

        /// <summary>Every business and role, in the order the table was written.</summary>
        public static StaffEntry[] Entries
        {
            get
            {
                if (_entries == null)
                {
                    StaffPlace[] places = StaffTable.Places;
                    StaffRole[] roles = StaffTable.Roles;
                    var all = new List<StaffEntry>(places.Length + roles.Length);
                    for (int i = 0; i < places.Length; i++)
                    {
                        all.Add(new StaffEntry(places[i]));
                    }
                    for (int i = 0; i < roles.Length; i++)
                    {
                        all.Add(new StaffEntry(roles[i]));
                    }
                    _entries = all.ToArray();
                }
                return _entries;
            }
        }

        /// <summary>The keys of the reference's own words (headings, joining words, the window), apart from those of the table's sentences and names.</summary>
        public static readonly string[] FixedKeys =
        {
            "reallife_staff_h_required", "reallife_staff_h_optional", "reallife_staff_h_also", "reallife_staff_h_does", "reallife_staff_h_gets", "reallife_staff_h_trains",
            "reallife_staff_h_open", "reallife_staff_any", "reallife_staff_how_many", "reallife_staff_without", "reallife_staff_no_use", "reallife_staff_need_required",
            "reallife_staff_need_optional", "reallife_staff_same", "reallife_staff_app", "reallife_staff_app_title", "reallife_staff_search_hint", "reallife_staff_cat_places",
                "reallife_staff_cat_roles",
            "reallife_staff_headline_found", "reallife_staff_empty_found", "reallife_staff_found_none", "reallife_staff_home", "reallife_staff_home_note", "reallife_staff_note_place",
            "reallife_staff_note_role", "reallife_staff_help_category", "reallife_staff_not_here", "reallife_staff_waits", "reallife_staff_open_button", "reallife_staff_guide_title",
            "reallife_staff_guide", "reallife_staff_test", "reallife_staff_test_pass", "reallife_staff_test_fail"
        };

        /// <summary>Every text key the reference reads: the fixed words, and each sentence, name and word of the table.</summary>
        public static void Keys(HashSet<string> into)
        {
            for (int i = 0; i < FixedKeys.Length; i++)
            {
                into.Add(FixedKeys[i]);
            }
            StaffPlace[] places = StaffTable.Places;
            for (int p = 0; p < places.Length; p++)
            {
                into.Add(places[p].NameKey);
                into.Add(places[p].AboutKey);
                for (int l = 0; l < places[p].Lines.Length; l++)
                {
                    StaffLine line = places[p].Lines[l];
                    into.Add(line.Does.Key);
                    if (line.Counts != null)
                    {
                        into.Add(line.Counts.Key);
                    }
                    if (line.Without != null)
                    {
                        into.Add(line.Without.Key);
                    }
                }
                for (int a = 0; a < places[p].Also.Length; a++)
                {
                    into.Add(places[p].Also[a].Key);
                }
            }
            StaffRole[] roles = StaffTable.Roles;
            for (int r = 0; r < roles.Length; r++)
            {
                into.Add(roles[r].NameKey);
                AddNotes(roles[r].About, into);
                AddNotes(roles[r].Gets, into);
                AddNotes(roles[r].Trains, into);
            }
            IReadOnlyList<string> words = StaffTable.Words;
            for (int w = 0; w < words.Count; w++)
            {
                into.Add(words[w]);
            }
        }

        private static void AddNotes(StaffNote[] notes, HashSet<string> into)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                into.Add(notes[i].Key);
            }
        }

        private static int[] _rivalShares;

        /// <summary>
        /// The thousandths of the rivals' adverts that ask for each family (Tech.Family order): the rivals are tech businesses, so they hire
        /// the families the table says a data centre, the cloud and the tech firms staff, a required role weighing three times an optional
        /// one. The player's own firms and the rivals' read the one table; a family no tech business names still gets a small share.
        /// </summary>
        public static int[] RivalFamilyShares
        {
            get
            {
                if (_rivalShares == null)
                {
                    var weights = new long[(int)Tech.Family.Count];
                    string[] tech = { "data_centre", "cloud_services", "hyperscaler", "software_studio", "game_studio", "it_consultancy", "cybersecurity_firm", "ai_lab", "web_agency",
                        "design_studio" };
                    StaffPlace[] places = StaffTable.Places;
                    for (int p = 0; p < places.Length; p++)
                    {
                        if (Array.IndexOf(tech, places[p].Id) < 0)
                        {
                            continue;
                        }
                        for (int l = 0; l < places[p].Lines.Length; l++)
                        {
                            StaffLine line = places[p].Lines[l];
                            for (int r = 0; r < line.Roles.Length; r++)
                            {
                                StaffRole role = RoleOf(line.Roles[r]);
                                if (role != null && role.Talent >= 0)
                                {
                                    weights[(int)Tech.TalentCatalogue.Role(role.Talent).Family] += (line.Need == StaffNeed.Required ? 3000 : 1000) / line.Roles.Length;
                                }
                            }
                        }
                    }
                    long total = 0;
                    for (int f = 0; f < weights.Length; f++)
                    {
                        weights[f] += 200;
                        total += weights[f];
                    }
                    var shares = new int[weights.Length];
                    int sum = 0;
                    for (int f = 0; f < shares.Length; f++)
                    {
                        shares[f] = (int)(weights[f] * 1000 / total);
                        sum += shares[f];
                    }
                    shares[0] += 1000 - sum;
                    _rivalShares = shares;
                }
                return _rivalShares;
            }
        }

        /// <summary>
        /// Every entry as the Discord guide and Uncle Fred read it: kind ("business" or "role"), id, title and body (the Guide's mark-up, links as
        /// bold names), from A to Z within each kind. Called by guidefacts on the built mod with English texts; the money is written as given.
        /// </summary>
        public static string[][] Export(Func<string, string> text, Func<long, string> money)
        {
            StaffStyle style = StaffPages.GuideStyle(text, money, false);
            var rows = new List<string[]>(96);
            for (int kind = 0; kind < 2; kind++)
            {
                foreach (StaffEntry entry in Sorted(kind == 0, text))
                {
                    rows.Add(new[] { kind == 0 ? "business" : "role", entry.Id, text(entry.NameKey), StaffPages.AsGuideText(StaffPages.Page(entry, style)) });
                }
            }
            return rows.ToArray();
        }

        /// <summary>The entry of a key ("b:data_centre"), or null.</summary>
        public static StaffEntry Find(string key)
        {
            StaffEntry[] all = Entries;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Key == key)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>The entry whose slug (its name key) is given, or null.</summary>
        public static StaffEntry FindSlug(string slug)
        {
            StaffEntry[] all = Entries;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].NameKey == slug)
                {
                    return all[i];
                }
            }
            return null;
        }

        public static StaffRole RoleOf(string id)
        {
            StaffRole[] roles = StaffTable.Roles;
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i].Id == id)
                {
                    return roles[i];
                }
            }
            return null;
        }

        /// <summary>The entries of one kind in A to Z order of their names as the reader's language writes them. Allocates the result; run on a click.</summary>
        public static List<StaffEntry> Sorted(bool places, Func<string, string> text)
        {
            var list = new List<StaffEntry>(64);
            StaffEntry[] all = Entries;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].IsPlace == places)
                {
                    list.Add(all[i]);
                }
            }
            var names = new Dictionary<StaffEntry, string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                names[list[i]] = text(list[i].NameKey);
            }
            CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
            list.Sort((a, b) =>
            {
                int order = compare.Compare(names[a], names[b], Loose);
                return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        /// <summary>The place in a name where a typed word is found, as the game's help finds it; -1 for none. An empty query finds everything (0).</summary>
        public static int Match(string name, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return 0;
            }
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(name ?? string.Empty, query.Trim(), Loose);
        }

        /// <summary>Every line of every business that names a role: the business and the line's number.</summary>
        public static void UsesOf(string roleId, List<KeyValuePair<StaffPlace, int>> into)
        {
            into.Clear();
            StaffPlace[] places = StaffTable.Places;
            for (int p = 0; p < places.Length; p++)
            {
                for (int l = 0; l < places[p].Lines.Length; l++)
                {
                    if (Array.IndexOf(places[p].Lines[l].Roles, roleId) >= 0)
                    {
                        into.Add(new KeyValuePair<StaffPlace, int>(places[p], l));
                    }
                }
            }
        }

        /// <summary>
        /// What is wrong with the table, in words: a line naming a role that is not there, a business with no line, a role used
        /// and with no way to be had, a note with no text, two entries of one name or slug. Empty when sound.
        /// </summary>
        public static void Problems(Func<string, string> text, List<string> into)
        {
            StaffPlace[] places = StaffTable.Places;
            StaffEntry[] all = Entries;
            var seen = new HashSet<string>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!seen.Add(all[i].NameKey) || !seen.Add(all[i].Key))
                {
                    into.Add("two entries share " + all[i].Key);
                }
            }
            var used = new HashSet<string>();
            for (int p = 0; p < places.Length; p++)
            {
                if (places[p].Lines.Length == 0)
                {
                    into.Add(places[p].Id + " has no staffing line");
                }
                for (int l = 0; l < places[p].Lines.Length; l++)
                {
                    StaffLine line = places[p].Lines[l];
                    if (line.Roles.Length == 0)
                    {
                        into.Add(places[p].Id + " line " + l + " names no role");
                    }
                    for (int r = 0; r < line.Roles.Length; r++)
                    {
                        if (RoleOf(line.Roles[r]) == null)
                        {
                            into.Add(places[p].Id + " line " + l + " names the unknown role " + line.Roles[r]);
                        }
                        used.Add(line.Roles[r]);
                    }
                }
            }
            foreach (string id in used)
            {
                StaffRole role = RoleOf(id);
                if (role != null && role.Gets.Length == 0)
                {
                    into.Add("the role " + id + " is used and has no way to be had");
                }
            }
        }
    }
}
