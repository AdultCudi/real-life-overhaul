using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>One member of the player's staff as the population links them to a resident.</summary>
    public struct StaffRef
    {
        public string Id;          // EmployeeInstance.id
        public string Key;         // the address they are assigned to ("street|number"), or null
        public int Pay;            // cents an hour (EmployeeInstance.hourlyWage)
        public int Hours;          // a week
        public int Age;            // years
        public bool Female;
    }

    /// <summary>One person of another system's to be a resident: the system's kind and id, and what is known of them to find a fitting resident.</summary>
    public struct LinkAsk
    {
        public LinkKind Kind;
        public string Key;
        public int Age;
        public bool Female;
        public int Prefer;         // a field (Field as a number) a resident of is looked for first; 0 for any
    }

    /// <summary>
    /// The people of other systems as residents (the brief's second step): the game's own staff at the player's businesses, and the mod's people (a
    /// partner, roommates, household staff, somebody met, the tech sector's professionals and jobseekers), each linked to a person record by their system's
    /// id. A new one is a resident of a fitting age and sex (one out of work first), the same on every machine for the same city; the link keeps that
    /// person out of the population's own ending, moving and hiring (their system owns them), and is how later work moves those systems onto the residents.
    /// A member of the game's staff is in the job the game gives them, at the game's pay; one the game no longer has is out of work again. Game-free.
    /// COST: nothing per frame; once a game day a pass over the links and, for a new one, a walk over the people from a seeded start.
    /// </summary>
    public static class PopulationLinks
    {
        public const int AgeGap = 6;   // years a linked resident's age may be off the person's own

        /// <summary>The player's staff linked: each member a resident in their job at the game's pay; one no longer on the staff out of work. Returns the links made today.</summary>
        public static int Staff(PopulationState s, List<StaffRef> staff, int day)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int made = 0;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                StaffRef r = staff[i];
                if (string.IsNullOrEmpty(r.Id) || !seen.Add(r.Id))
                {
                    continue;
                }
                int p = Linked(s, LinkKind.Employee, r.Id);
                if (p < 0)
                {
                    p = Link(s, LinkKind.Employee, r.Id, r.Age, r.Female, day);
                    made += p >= 0 ? 1 : 0;
                }
                if (p < 0)
                {
                    continue;
                }
                PersonTable pt = s.People;
                int e = s.EmployerOfKey(r.Key);
                if (pt.Job[p] != e)
                {
                    if (e >= 0)
                    {
                        LabourRules.Hire(s, p, e, day);
                    }
                    else
                    {
                        LabourRules.Lose(s, p, day, false);
                        pt.Status[p] = (byte)Status.Worker;   // on the payroll with no place to work (not yet assigned)
                    }
                }
                pt.Status[p] = (byte)Status.Worker;
                pt.Pay[p] = Math.Max(0, r.Pay);
                pt.Hours[p] = (byte)PopulationCatalogue.Clamp(r.Hours, 1, 80);
            }
            for (int i = s.Links.Count - 1; i >= 0; i--)
            {
                PersonLink link = s.Links[i];
                if (link.Kind != LinkKind.Employee || seen.Contains(link.Key))
                {
                    continue;
                }
                int p = SlotOf(s, link);
                if (p >= 0)
                {
                    s.People.Flags[p] = (byte)(s.People.Flags[p] & ~PersonFlags.Linked);
                    if (s.People.Status[p] == (byte)Status.Worker)
                    {
                        LabourRules.Lose(s, p, day, false);   // let go by the player (or gone): out of work, their last pay kept for their benefit
                    }
                }
                s.Links.RemoveAt(i);
            }
            return made;
        }

        /// <summary>
        /// The other systems' people as residents: each asked one linked (a fitting resident for a new one), every other link (not the staff's) no longer asked for let go.
        /// Their status stays the population's own reading of them; the link keeps them theirs. Returns the links made.
        /// </summary>
        public static int People(PopulationState s, List<LinkAsk> asks, int day)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int made = 0;
            for (int i = 0; asks != null && i < asks.Count; i++)
            {
                LinkAsk a = asks[i];
                if (string.IsNullOrEmpty(a.Key) || !seen.Add((int)a.Kind + ":" + a.Key))
                {
                    continue;
                }
                if (Linked(s, a.Kind, a.Key) < 0 && Link(s, a.Kind, a.Key, a.Age, a.Female, day, a.Prefer) >= 0)
                {
                    made++;
                }
            }
            for (int i = s.Links.Count - 1; i >= 0; i--)
            {
                PersonLink link = s.Links[i];
                if (link.Kind == LinkKind.Employee || seen.Contains((int)link.Kind + ":" + link.Key))
                {
                    continue;
                }
                int p = SlotOf(s, link);
                if (p >= 0)
                {
                    s.People.Flags[p] = (byte)(s.People.Flags[p] & ~PersonFlags.Linked);
                }
                s.Links.RemoveAt(i);
            }
            return made;
        }

        /// <summary>The record of the person a link names: their age, sex, field and skill (the record only, nothing of how they look); false with no such link.</summary>
        public static bool Record(PopulationState s, LinkKind kind, string key, int day, out int age, out bool female, out int field, out int skill)
        {
            age = field = skill = 0;
            female = false;
            int p = Linked(s, kind, key);
            if (p < 0)
            {
                return false;
            }
            age = s.Age(p, day);
            female = s.People.Female[p] == 1;
            field = s.People.Field[p];
            skill = s.People.Skill[p];
            return true;
        }

        /// <summary>The slot of the person a link names, or -1 (the person is gone).</summary>
        public static int Linked(PopulationState s, LinkKind kind, string key)
        {
            for (int i = 0; i < s.Links.Count; i++)
            {
                if (s.Links[i].Kind == kind && string.Equals(s.Links[i].Key, key, StringComparison.Ordinal))
                {
                    int p = SlotOf(s, s.Links[i]);
                    if (p >= 0)
                    {
                        return p;
                    }
                    s.Links.RemoveAt(i);   // the person it named is gone: linked again below
                    return -1;
                }
            }
            return -1;
        }

        /// <summary>
        /// A linked person taken on by an employer, as the population hires anyone (<see cref="LabourRules.Hire"/>): the one at <paramref name="employerKey"/> (its
        /// building, or the other firms of that office building) when it has a vacancy, else the first of the city's own with a vacancy in the person's field from a
        /// start drawn from their key. Never the player's employers, whose people are the game's staff. False with no such person or no vacancy anywhere: nobody is moved.
        /// </summary>
        public static bool Employ(PopulationState s, LinkKind kind, string key, string employerKey, int day)
        {
            int p = Linked(s, kind, key);
            if (p < 0 || s.Employers.Count == 0)
            {
                return false;
            }
            EmployerTable et = s.Employers;
            int e = Open(s, employerKey);
            e = e >= 0 ? e : Open(s, employerKey != null ? employerKey + "|t" : null);
            if (e < 0)
            {
                int field = s.People.Field[p] > 0 ? s.People.Field[p] : (int)Field.Office;
                int start = (int)(PopDraw.Hash(s.Seed, PopDraw.Link, KeyHash(key), day) % (uint)et.Count);
                for (int n = 0; n < et.Count && e < 0; n++)
                {
                    int c = (start + n) % et.Count;
                    var kindOf = (EmployerKind)et.Kind[c];
                    if (et.Field[c] == field && !et.Players[c] && et.Vacancies(c) > 0 && (kindOf == EmployerKind.Business || kindOf == EmployerKind.Tenants || kindOf == EmployerKind.Mod))
                    {
                        e = c;
                    }
                }
            }
            if (e < 0)
            {
                return false;
            }
            if (s.People.Job[p] != e)
            {
                LabourRules.Hire(s, p, e, day);
            }
            return true;
        }

        private static int Open(PopulationState s, string key)
        {
            int e = s.EmployerOfKey(key);
            return e >= 0 && !s.Employers.Players[e] && s.Employers.Vacancies(e) > 0 ? e : -1;
        }

        // The slot of a link's person: its slot when the id there is still theirs (slots are kept in the save), else a search by the id.
        private static int SlotOf(PopulationState s, PersonLink link)
        {
            if (link.Slot >= 0 && link.Slot < s.People.Count && s.People.Id[link.Slot] == link.Person)
            {
                return link.Slot;
            }
            for (int p = 0; p < s.People.Count; p++)
            {
                if (s.People.Id[p] == link.Person)
                {
                    return p;
                }
            }
            return -1;
        }

        /// <summary>
        /// A person of another system's linked to a fitting resident: of their sex and an age near theirs, not linked already, out of work first and in
        /// work after, from a start drawn from their id; -1 with none (they are left for the next day).
        /// </summary>
        public static int Link(PopulationState s, LinkKind kind, string key, int age, bool female, int day, int prefer = 0)
        {
            PersonTable pt = s.People;
            if (pt.Count == 0)
            {
                return -1;
            }
            int start = (int)(PopDraw.Hash(s.Seed, PopDraw.Link, KeyHash(key), (int)kind) % (uint)pt.Count);
            age = Math.Max(PopulationCatalogue.AdultLeast, age);
            for (int pass = 0; pass < 3; pass++)
            {
                for (int n = 0; n < pt.Count; n++)
                {
                    int p = (start + n) % pt.Count;
                    if (pt.Id[p] == 0 || (pt.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) != 0 || (pt.Female[p] == 1) != female || pt.Household[p] < 0)
                    {
                        continue;
                    }
                    if (prefer > 0 && pass < 2 && pt.Field[p] != prefer)
                    {
                        continue;
                    }
                    int a = s.Age(p, day);
                    if (Math.Abs(a - age) > AgeGap * (pass + 1))
                    {
                        continue;
                    }
                    var status = (Status)pt.Status[p];
                    bool fits = pass == 0 ? status == Status.Unemployed : pass == 1 ? status == Status.Worker || status == Status.Home : status != Status.Child && status != Status.Pupil;
                    if (!fits)
                    {
                        continue;
                    }
                    pt.Flags[p] |= PersonFlags.Linked;
                    s.Links.Add(new PersonLink { Kind = kind, Key = key, Person = pt.Id[p], Slot = p });
                    return p;
                }
            }
            return -1;
        }

        // A hash of an id, the same in every runtime (string.GetHashCode is not).
        private static int KeyHash(string key)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; key != null && i < key.Length; i++)
                {
                    h = h * 31 + key[i];
                }
                return h;
            }
        }
    }
}
