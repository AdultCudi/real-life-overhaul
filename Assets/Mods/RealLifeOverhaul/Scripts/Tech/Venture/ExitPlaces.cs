using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the population knows of a resident a founder is: their age, sex, field and skill, as the record holds them (never a model, a face or a body). Game-free.</summary>
    public struct ResidentInfo
    {
        public int Age;
        public bool Female;
        public int Field;       // Population.Field as a number
        public int Skill;       // 0 to 100
    }

    /// <summary>
    /// The city's startups as real places: a founder who is a person of the city, a floor of a tower, and people hired from the labour market the player hires
    /// from. All seeded by the host's file, so the same file gives the same answers; nothing here is sent anywhere. Game-free.
    /// </summary>
    public static class ExitPlaces
    {
        // ---------------------------------------------------------------- founders

        /// <summary>A founder invented, as the city's startups had them before: an age, a sex and a skill drawn from the seed and the startup's quality.</summary>
        public static FounderRec Invent(ExitFile f, Startup s, int day)
        {
            var r = new FounderRec { Startup = s.Id, Seen = day };
            r.Age = TechRandom.Range(f.Seed, s.Id, 91, 0, ExitCatalogue.FounderAgeLeast, ExitCatalogue.FounderAgeMost);
            r.Female = TechRandom.Mille(f.Seed, s.Id, 92) < 500;
            r.Skill = TalentCatalogue.Clamp(s.Quality / 10, 0, 100);
            r.Field = ExitCatalogue.SectorField[TalentCatalogue.Clamp(s.Sector, 0, ExitCatalogue.SectorField.Length - 1)];
            return r;
        }

        /// <summary>The field of the city's people a founder in a sector would come from.</summary>
        public static int FieldOf(int sector) => ExitCatalogue.SectorField[TalentCatalogue.Clamp(sector, 0, ExitCatalogue.SectorField.Length - 1)];

        /// <summary>
        /// The founder is a resident of the city: their age, sex and skill are the record's. The startup's quality (what the founders can do) follows the
        /// record: half what it was, half what the resident's skill makes. Done once, when the resident is linked. Returns the quality the startup has now.
        /// </summary>
        public static int Resident(FounderRec r, Startup s, ResidentInfo p)
        {
            if (r.Linked)
            {
                return s.Quality;
            }
            r.Linked = true;
            r.Age = Math.Max(ExitCatalogue.FounderAgeLeast, p.Age);
            r.Female = p.Female;
            r.Field = p.Field;
            r.Skill = TalentCatalogue.Clamp(p.Skill, 0, 100);
            s.Quality = TalentCatalogue.Clamp((s.Quality + QualityOf(r.Skill)) / 2, 100, 950);
            return s.Quality;
        }

        /// <summary>What a founder of a skill can do, 0 to 1000 as a startup's quality is.</summary>
        public static int QualityOf(int skill) => TalentCatalogue.Clamp(150 + TalentCatalogue.Clamp(skill, 0, 100) * 8, 100, 950);

        // ---------------------------------------------------------------- floors

        /// <summary>
        /// A startup lets a floor of a tower: the tower with the fewest startups (a tie goes by the seed), and the lowest place on it that no other startup holds.
        /// Returns false when there is no tower.
        /// </summary>
        public static bool Let(ExitFile f, FounderRec r, int towers)
        {
            if (towers <= 0)
            {
                r.Tower = -1;
                r.Slot = 0;
                return false;
            }
            var count = new int[towers];
            for (int i = 0; i < f.Founders.Count; i++)
            {
                FounderRec o = f.Founders[i];
                if (!ReferenceEquals(o, r) && o.Tower >= 0 && o.Tower < towers)
                {
                    count[o.Tower]++;
                }
            }
            int best = 0;
            for (int t = 1; t < towers; t++)
            {
                if (count[t] < count[best] || (count[t] == count[best] && TechRandom.Mille(f.Seed, r.Startup, 93, t) < TechRandom.Mille(f.Seed, r.Startup, 93, best)))
                {
                    best = t;
                }
            }
            int slot = 1;
            while (Held(f, r, best, slot))
            {
                slot++;
            }
            r.Tower = best;
            r.Slot = slot;
            return true;
        }

        private static bool Held(ExitFile f, FounderRec r, int tower, int slot)
        {
            for (int i = 0; i < f.Founders.Count; i++)
            {
                if (!ReferenceEquals(f.Founders[i], r) && f.Founders[i].Tower == tower && f.Founders[i].Slot == slot)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The floor each startup holds, from 1: after the floors of the companies already in the tower, one each in the order they let them, with no gap, and
        /// none past what a building holds. <paramref name="rivals"/> is the number of floors the tower's companies hold; <paramref name="floorOf"/> gets 0 for
        /// a startup without a tower or with no room.
        /// </summary>
        public static void Floors(IList<StartupTenant> tenants, int[] rivals, int[] floorOf)
        {
            for (int i = 0; i < floorOf.Length; i++)
            {
                floorOf[i] = 0;
            }
            for (int t = 0; t < rivals.Length; t++)
            {
                int rank = 0;
                int last = 0;
                while (true)
                {
                    int next = -1;
                    for (int i = 0; tenants != null && i < tenants.Count && i < floorOf.Length; i++)
                    {
                        if (tenants[i].Tower == t && tenants[i].Slot > last && (next < 0 || tenants[i].Slot < tenants[next].Slot))
                        {
                            next = i;
                        }
                    }
                    if (next < 0)
                    {
                        break;
                    }
                    rank++;
                    last = tenants[next].Slot;
                    floorOf[next] = rivals[t] + rank <= ExitCatalogue.TowerFloorsMost ? rivals[t] + rank : 0;
                }
            }
        }

        // ---------------------------------------------------------------- the labour market

        /// <summary>The people a startup wants: its wages' share of the burn over what a person asks a month. Both at the same level of prices (cents a day, cents an hour).</summary>
        public static int Target(long burnPerDay, int askPerHour)
        {
            if (burnPerDay <= 0 || askPerHour <= 0)
            {
                return 0;
            }
            long wages = burnPerDay * 30 * ExitCatalogue.WagesShare / 1000;
            long each = (long)askPerHour * ExitCatalogue.HoursMonth;
            return (int)Math.Max(1L, Math.Min(ExitCatalogue.PeopleMost, wages / Math.Max(1L, each)));
        }

        /// <summary>What a person asks an hour on the open market now, cents: the mean of those still looking; 0 with nobody.</summary>
        public static int MeanAsk(HiringFile h)
        {
            long sum = 0;
            int n = 0;
            for (int i = 0; i < h.Seekers.Count; i++)
            {
                Seeker s = h.Seekers[i];
                if (!s.Taken && !s.Passive && s.Ask > 0)
                {
                    sum += s.Ask;
                    n++;
                }
            }
            return n > 0 ? (int)(sum / n) : 0;
        }

        /// <summary>True when the player has an application of the seeker's in hand that a hire by a startup would take from under them.</summary>
        public static bool Held(HiringFile h, int seeker)
        {
            for (int i = 0; i < h.Applicants.Count; i++)
            {
                Applicant a = h.Applicants[i];
                if (a.Seeker == seeker && a.Live && a.Stage >= (int)ApplyStage.Offered)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A startup short of people takes one from the market the player hires from: a seeker still looking, not on a search firm's brief and not one the player
        /// has an offer out to or has taken on. They are marked taken, so the player's list loses them as a rival's hire does; the labour market's own day
        /// withdraws any application of the player's to them. Returns the seeker, or null.
        /// </summary>
        public static Seeker Hire(HiringFile h, ExitFile f, FounderRec r, Startup s, int people, long burnToday, int day)
        {
            if (h == null || people >= Target(burnToday, MeanAsk(h)) || TechRandom.Mille(f.Seed, s.Id, day, 94) >= ExitCatalogue.HireChance)
            {
                return null;
            }
            int n = h.Seekers.Count;
            if (n == 0)
            {
                return null;
            }
            int start = TechRandom.Range(f.Seed, s.Id, day, 95, 0, n - 1);
            for (int k = 0; k < n; k++)
            {
                Seeker c = h.Seekers[(start + k) % n];
                if (c.Taken || c.Passive || Held(h, c.Id))
                {
                    continue;
                }
                c.Taken = true;
                c.Employer = s.Name;
                r.Hired++;
                return c;
            }
            return null;
        }
    }
}
