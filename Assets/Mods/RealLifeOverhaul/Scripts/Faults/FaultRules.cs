using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The city's faults by rule, game-free (checked offline: EconomyCheck, FaultChecks.cs). Faults are NOT made from a
    /// seed of the save, the day or the district: each game hour the city's runner rolls them with a real source of chance
    /// (<see cref="IFaultDice"/>; live, FaultDice), weighted by the city as it is that hour, so that no two games, not even
    /// two from the same save, break in the same places or at the same times. What has broken is kept in the save; what
    /// breaks next is never fixed in advance.
    ///
    ///   how likely   per game hour, for each kind, a chance in a thousand (<see cref="Chance"/>): street lamps fail more
    ///                where the council's upkeep is short, for a day after a storm, and at dusk; potholes open for a day
    ///                after rain and more after a frost, more at the rush hours; litter heaps up where cleaning was cut, at
    ///                night; unsafe places where upkeep is short and after a storm. A hot spot is not rolled by the hour: a
    ///                crime reported in the street may make one there (<see cref="Crime"/>).
    ///   where        the district by weight (<see cref="Weight"/>): a lamp by upkeep and by how long since one was mended
    ///                there, a pothole by upkeep and traffic, litter by traffic and night life, an unsafe place by upkeep.
    ///   how many     a few to a dozen at once by the option (<see cref="Most"/>), each kind a share of that.
    ///   seen         the city's people report a fault by how plain it is and how long it has been there (<see cref="Seen"/>);
    ///                a dark lamp only after dark.
    ///   reports      a report matches a real fault of its kind where it was made (<see cref="Match"/>): a crew goes and
    ///                the fault is gone when it is done. A report of nothing is answered so, and a player's reports of
    ///                nothing add up in the week: a warning at the second, at the third no crew for their reports for
    ///                <see cref="BarDays"/> days (<see cref="Nothing"/>).
    ///   what they do a dark lamp scales street crime near it at night (<see cref="DarkCrime"/>); a pothole wears a
    ///                car's tyres and jolts it, more at speed (<see cref="TyreWear"/>, <see cref="Jolt"/>), and slows
    ///                traffic a little there; litter costs customers near it (<see cref="LitterFootfall"/>); an unsafe
    ///                place trips a passer-by now and then (<see cref="TripChance"/>), never fatally.
    ///
    /// COST: a roll is a pass over a dozen faults and a dozen districts, once a game hour; nothing per frame.
    /// </summary>
    public static class FaultRules
    {
        public const int Kinds = 5;
        public static readonly int[] Most = { 4, 8, 12 };                 // faults at once in the whole city, by the option: a few, some, many
        public static readonly int[] KindShare = { 30, 30, 30, 30, 20 };  // per cent of that for each kind (Lamp, Pothole, Litter, Hotspot, Unsafe), at least one each
        public static readonly int[] BasePerMille = { 60, 40, 50, 0, 20 }; // chance in a thousand an hour of a new one of each kind, before the city's state
        public const int Upkeep = 1000;                                    // thousandths: the weight of ordinary upkeep
        public static readonly int[] UpkeepScale = { 1800, 1000, 600 };    // thousandths by upkeep -1, 0, +1: short upkeep, more breaks
        public const int StormScale = 2500, WetScale = 2200, FrostScale = 3000, RushScale = 1500, DuskScale = 1300, NightScale = 1500;   // thousandths
        public static readonly int[] LitterScale = { 2000, 1000, 500 };   // thousandths by the cleaning policy -1, 0, +1
        public const int WeatherMemory = 24 * 60;                          // game minutes a storm, rain or frost still counts after it
        public const int AgeDaysFull = 30;                                 // days since a lamp was last mended in a district: its weight doubled by then

        public const int HotspotPerMille = 450;          // a crime reported in the street: the chance it marks a hot spot there
        public const float HotspotJoin = 150f;           // metres: a crime this near a hot spot is one more of it
        public const int HotspotDays = 5;                // a hot spot with no crime for so many days is over
        public const int LitterDays = 4;                 // the city's own sweepers take a heap after so many days...
        public const int LitterDaysCut = 8;              // ...or so many where cleaning was cut
        public const int CaseForgetDays = 10;            // a case that took a fault and was never done: the fault is free again

        public const float MatchMetres = 300f;           // a report made where the player stands matches a fault this near
        public const int StrikeDays = 7;                 // reports of nothing count over a week
        public const int WarnAt = 2, BarAt = 3;          // ...a warning at the second, no crews at the third
        public const int BarDays = 3;                    // days a player's reports bring no crew

        public static readonly int[] SeenPerMille = { 30, 15, 25, 10, 30 };   // a fault's chance an hour of being reported by the city's people, on its first day
        public const int SeenAgeDays = 3;                // ...rising to four times that by this many days
        public const int CleanFixMinutes = 6 * 60;       // a cleaning crew sent: the heap is gone so many minutes later
        public const int PatrolFixMinutes = 2 * 24 * 60; // patrols sent: the hot spot is over then

        public const float DarkCrime = 1.35f;            // street crime near a dark lamp after dark
        public const float DarkNear = 60f;               // metres: near a dark lamp
        public const float LitterNear = 40f;             // metres: a business's door this near a heap loses customers
        public const int LitterPercent = -2, LitterMostPercent = -4;   // per cent of customers for each heap near, at most
        public const float PotholeRadius = 0.9f;         // metres from a hole's middle a wheel feels it
        public const int PotholeSlowKmh = 15;            // km/h off the lane's limit at a pothole
        public const int PotholeSlowest = 20;            // km/h: never slower than this
        public const int TripPerMille = 60;              // a passer-by at an unsafe place: the chance a look trips them
        public const int CleanFrom = 60;                 // a business's cleanliness (0 to 100) from which an inspection finds nothing

        /// <summary>The most faults at once in the city, by the option.</summary>
        public static int MostOf(int option)
        {
            return Most[Clamp(option, 0, Most.Length - 1)];
        }

        /// <summary>The most of one kind at once: its share of the city's most, at least one.</summary>
        public static int KindMost(int option, FaultKind kind)
        {
            return Math.Max(1, MostOf(option) * KindShare[(int)kind] / 100);
        }

        /// <summary>Whether a storm, rain or frost still counts: the file remembers each for a day after it.</summary>
        public static void Weather(FaultFile file, FaultFacts facts)
        {
            if (facts.Storm)
            {
                file.StormUntil = facts.Now + WeatherMemory;
            }
            if (facts.Rain || facts.Storm)
            {
                file.WetUntil = facts.Now + WeatherMemory;
            }
            if (facts.Frost)
            {
                file.FrostUntil = facts.Now + WeatherMemory;
            }
        }

        public static bool Stormy(FaultFile file, long now) => now < file.StormUntil;
        public static bool Wet(FaultFile file, long now) => now < file.WetUntil;
        public static bool Frosty(FaultFile file, long now) => now < file.FrostUntil;

        /// <summary>Thousandths by the council's upkeep (-1 short, 0, +1 generous).</summary>
        public static int UpkeepOf(int spending)
        {
            return UpkeepScale[Clamp(spending, -1, 1) + 1];
        }

        /// <summary>The city's upkeep over its districts, in thousandths (ordinary upkeep without districts).</summary>
        public static int CityUpkeep(FaultFacts facts)
        {
            if (facts.Districts.Count == 0)
            {
                return Upkeep;
            }
            long sum = 0;
            for (int i = 0; i < facts.Districts.Count; i++)
            {
                sum += UpkeepOf(facts.Districts[i].Spending);
            }
            return (int)(sum / facts.Districts.Count);
        }

        /// <summary>
        /// The chance in a thousand that a new fault of a kind appears somewhere in the city this hour, by the city's state
        /// (none for a hot spot: those come of crimes). Capped at 900.
        /// </summary>
        public static int Chance(FaultKind kind, FaultFacts facts, FaultFile file)
        {
            long c = BasePerMille[(int)kind];
            int hour = facts.Hour;
            bool night = hour >= 20 || hour < 4;
            switch (kind)
            {
                case FaultKind.Lamp:
                    c = c * CityUpkeep(facts) / 1000;
                    c = Stormy(file, facts.Now) ? c * StormScale / 1000 : c;
                    c = hour >= 18 && hour <= 21 ? c * DuskScale / 1000 : c;   // a tired lamp fails when it is switched on
                    break;
                case FaultKind.Pothole:
                    c = c * CityUpkeep(facts) / 1000;
                    c = Wet(file, facts.Now) ? c * WetScale / 1000 : c;
                    c = Frosty(file, facts.Now) ? c * FrostScale / 1000 : c;
                    c = (hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 19) ? c * RushScale / 1000 : c;
                    break;
                case FaultKind.Litter:
                    c = c * LitterScale[Clamp(facts.Litter, -1, 1) + 1] / 1000;
                    c = night ? c * NightScale / 1000 : c;
                    break;
                case FaultKind.Unsafe:
                    c = c * CityUpkeep(facts) / 1000;
                    c = Stormy(file, facts.Now) ? c * StormScale / 1000 : c;
                    break;
                default:
                    return 0;
            }
            return (int)Math.Min(900, c);
        }

        /// <summary>A district's weight for a fault of a kind (0 for none). <paramref name="ageDays"/>: days since a fault of the kind was mended there.</summary>
        public static int Weight(FaultKind kind, FaultDistrict d, int ageDays)
        {
            if (d == null || string.IsNullOrEmpty(d.Id))
            {
                return 0;
            }
            int busy = Clamp(d.Busy, 0, 3), night = Clamp(d.Night, 0, 3);
            switch (kind)
            {
                case FaultKind.Lamp: return UpkeepOf(d.Spending) * (1000 + Clamp(ageDays, 0, AgeDaysFull) * 1000 / AgeDaysFull) / 1000;
                case FaultKind.Pothole: return UpkeepOf(d.Spending) * (2 + busy) / 2;
                case FaultKind.Litter: return 1000 * (1 + busy + night);
                case FaultKind.Unsafe: return UpkeepOf(d.Spending);
                default: return 0;
            }
        }

        /// <summary>Days since a fault of a kind was last mended in a district; <see cref="AgeDaysFull"/> if none is remembered.</summary>
        public static int AgeDays(FaultFile file, FaultKind kind, string district, int day)
        {
            int last = int.MinValue;
            for (int i = 0; i < file.Mended.Count; i++)
            {
                if (file.Mended[i].Kind == kind && file.Mended[i].District == district)
                {
                    last = Math.Max(last, file.Mended[i].Day);
                }
            }
            return last == int.MinValue ? AgeDaysFull : Clamp(day - last, 0, AgeDaysFull);
        }

        /// <summary>A district by weight for a kind, as an index into the facts' list; -1 if none weighs anything.</summary>
        public static int PickDistrict(FaultKind kind, FaultFacts facts, FaultFile file, IFaultDice dice)
        {
            long total = 0;
            for (int i = 0; i < facts.Districts.Count; i++)
            {
                total += Weight(kind, facts.Districts[i], AgeDays(file, kind, facts.Districts[i].Id, facts.Day));
            }
            if (total <= 0)
            {
                return -1;
            }
            long pick = dice.Next((int)Math.Min(int.MaxValue, total));
            for (int i = 0; i < facts.Districts.Count; i++)
            {
                pick -= Weight(kind, facts.Districts[i], AgeDays(file, kind, facts.Districts[i].Id, facts.Day));
                if (pick < 0)
                {
                    return i;
                }
            }
            return facts.Districts.Count - 1;
        }

        /// <summary>
        /// One hour's roll: the faults wanted, into the list (cleared first), each kind under its own most and all under the
        /// city's. The weather of the hour is remembered first. Returns how many are wanted. Hot spots are not rolled here.
        /// </summary>
        public static int Roll(FaultFile file, FaultFacts facts, IFaultDice dice, List<FaultWant> into)
        {
            into.Clear();
            Weather(file, facts);
            file.LastRoll = facts.Now - facts.Now % 60;
            int most = MostOf(facts.Option);
            int total = file.Faults.Count;
            for (int k = 0; k < Kinds; k++)
            {
                var kind = (FaultKind)k;
                if (kind == FaultKind.Hotspot || total >= most || file.Count(kind) >= KindMost(facts.Option, kind))
                {
                    continue;
                }
                if (dice.Next(1000) >= Chance(kind, facts, file))
                {
                    continue;
                }
                int district = PickDistrict(kind, facts, file, dice);
                if (district < 0)
                {
                    continue;
                }
                into.Add(new FaultWant { Kind = kind, District = district, Variant = dice.Next(1000), Heading = dice.Next(360) });
                total++;
            }
            return into.Count;
        }

        /// <summary>A fault where the director found a place for one wanted: put in the file, its number the next.</summary>
        public static Fault Add(FaultFile file, FaultKind kind, string district, string address, float x, float y, float z, float heading, int variant, long now)
        {
            var f = new Fault
            {
                Id = file.NextId++, Kind = kind, District = district, Address = address ?? string.Empty, X = x, Y = y, Z = z, Heading = heading, Variant = variant, Since = now
            };
            file.Faults.Add(f);
            return f;
        }

        /// <summary>
        /// A crime reported in the street at a point: one more of a hot spot near it, or now and then a new hot spot (under
        /// the kind's most and the city's). Returns the new one, or null (one more of an old one, or none).
        /// </summary>
        public static Fault Crime(FaultFile file, int option, string district, string address, float x, float y, float z, long now, IFaultDice dice)
        {
            if (string.IsNullOrEmpty(district))
            {
                return null;
            }
            for (int i = 0; i < file.Faults.Count; i++)
            {
                Fault f = file.Faults[i];
                if (f.Kind == FaultKind.Hotspot && Flat(f.X - x, f.Z - z) <= HotspotJoin * HotspotJoin)
                {
                    f.Crimes++;
                    f.LastCrime = now;
                    return null;
                }
            }
            if (file.Faults.Count >= MostOf(option) || file.Count(FaultKind.Hotspot) >= KindMost(option, FaultKind.Hotspot) || dice.Next(1000) >= HotspotPerMille)
            {
                return null;
            }
            Fault made = Add(file, FaultKind.Hotspot, district, address, x, y, z, 0f, 0, now);
            made.Crimes = 1;
            made.LastCrime = now;
            return made;
        }

        /// <summary>
        /// The hour's turn: faults whose crew has done its work go (and are remembered as mended), hot spots with no crime
        /// for <see cref="HotspotDays"/> days end, the city's own sweepers take an old heap, and a case that took a fault and
        /// was never done lets it go. The ids gone, into the list (not cleared). True if anything changed.
        /// </summary>
        public static bool Turn(FaultFile file, long now, int litterPolicy, List<int> gone)
        {
            bool changed = false;
            int day = (int)(now / 1440);
            for (int i = file.Faults.Count - 1; i >= 0; i--)
            {
                Fault f = file.Faults[i];
                bool end = (f.FixAt >= 0 && now >= f.FixAt)
                           || (f.Kind == FaultKind.Hotspot && !f.Fixing && now - Math.Max(f.LastCrime, f.Since) >= HotspotDays * 1440L)
                           || (f.Kind == FaultKind.Litter && !f.Fixing && now - f.Since >= (litterPolicy < 0 ? LitterDaysCut : LitterDays) * 1440L);
                if (end)
                {
                    Mend(file, f, day);
                    file.Faults.RemoveAt(i);
                    gone?.Add(f.Id);
                    changed = true;
                    continue;
                }
                if (f.Fixing && f.FixAt < 0 && f.CaseAt >= 0 && now - f.CaseAt >= CaseForgetDays * 1440L)
                {
                    f.Case = -1;
                    f.CaseAt = -1;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>A fault mended (its works done): out of the file and remembered. False if it was not there.</summary>
        public static bool Fixed(FaultFile file, int id, int day)
        {
            for (int i = 0; i < file.Faults.Count; i++)
            {
                if (file.Faults[i].Id == id)
                {
                    Mend(file, file.Faults[i], day);
                    file.Faults.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private static void Mend(FaultFile file, Fault f, int day)
        {
            file.Mended.Add(new FaultMended { Kind = f.Kind, District = f.District, Day = day });
            while (file.Mended.Count > FaultFile.MendedMost)
            {
                file.Mended.RemoveAt(0);
            }
        }

        // ---------------------------------------------------------------- reports

        /// <summary>The fault kind a Flare report is about (FlareReport's number: 0 a road, 1 a lamp, 2 crime, 3 litter, 4 an unsafe place); false for the others.</summary>
        public static bool KindOf(int report, out FaultKind kind)
        {
            switch (report)
            {
                case 0: kind = FaultKind.Pothole; return true;
                case 1: kind = FaultKind.Lamp; return true;
                case 2: kind = FaultKind.Hotspot; return true;
                case 3: kind = FaultKind.Litter; return true;
                case 4: kind = FaultKind.Unsafe; return true;
                default: kind = FaultKind.Lamp; return false;
            }
        }

        /// <summary>The Flare report's number for a kind (the inverse of <see cref="KindOf"/>).</summary>
        public static int ReportOf(FaultKind kind)
        {
            switch (kind)
            {
                case FaultKind.Pothole: return 0;
                case FaultKind.Lamp: return 1;
                case FaultKind.Hotspot: return 2;
                case FaultKind.Litter: return 3;
                default: return 4;
            }
        }

        /// <summary>
        /// The real fault a report is about, or null: of its kind (one a crew has in hand already is still there, so a
        /// report of it is true); with a point (the report was made where the player stands) the nearest within
        /// <see cref="MatchMetres"/>, else one in the district reported (the longest standing).
        /// </summary>
        public static Fault Match(FaultFile file, FaultKind kind, string district, bool point, float x, float z)
        {
            Fault best = null;
            float bestSqr = MatchMetres * MatchMetres;
            for (int i = 0; i < file.Faults.Count; i++)
            {
                Fault f = file.Faults[i];
                if (f.Kind != kind)
                {
                    continue;
                }
                if (point)
                {
                    float sqr = Flat(f.X - x, f.Z - z);
                    if (sqr <= bestSqr)
                    {
                        bestSqr = sqr;
                        best = f;
                    }
                }
                else if (string.Equals(f.District, district, StringComparison.Ordinal) && (best == null || f.Since < best.Since))
                {
                    best = f;
                }
            }
            return best;
        }

        /// <summary>True while a player's reports bring no crew.</summary>
        public static bool Barred(FaultFile file, string reporter, int day)
        {
            FaultStrike s = file.Strike(reporter, false);
            return s != null && day < s.BarredUntil;
        }

        /// <summary>
        /// A player's report of nothing, counted over the week: Nothing at first, Warned at <see cref="WarnAt"/>, Barred from
        /// <see cref="BarAt"/> (their reports bring no crew for <see cref="BarDays"/> days, and the count starts again).
        /// </summary>
        public static FaultVerdict Nothing(FaultFile file, string reporter, int day)
        {
            FaultStrike s = file.Strike(reporter, true);
            if (s == null)
            {
                return FaultVerdict.Nothing;
            }
            if (s.Day < 0 || day - s.Day >= StrikeDays)
            {
                s.Count = 0;
            }
            s.Count++;
            s.Day = day;
            if (s.Count >= BarAt)
            {
                s.BarredUntil = day + BarDays;
                s.Count = 0;
                return FaultVerdict.Barred;
            }
            if (s.Count >= WarnAt)
            {
                s.WarnedDay = day;
                return FaultVerdict.Warned;
            }
            return FaultVerdict.Nothing;
        }

        /// <summary>True if a case may take the fault: no crew has it in hand yet (else the first case keeps it).</summary>
        public static bool Claimable(Fault f)
        {
            return f != null && !f.Fixing && f.FixAt < 0;
        }

        /// <summary>A case takes a fault: a crew is on its way. A cleaning round or patrols end it after their time; works when they are handed over.</summary>
        public static void Taken(Fault f, int caseId, long now, bool works)
        {
            f.Case = caseId;
            f.CaseAt = now;
            if (!works)
            {
                f.FixAt = now + (f.Kind == FaultKind.Hotspot ? PatrolFixMinutes : CleanFixMinutes);
            }
        }

        /// <summary>
        /// The faults the city's people report this hour, into the list (cleared first): each not yet taken by a crew, by how
        /// plain it is and how long it has stood (four times as likely by <see cref="SeenAgeDays"/> days); a dark lamp only
        /// after dark. Each one reported counts once more in its <see cref="Fault.Reports"/>.
        /// </summary>
        public static int Seen(FaultFile file, long now, IFaultDice dice, List<Fault> into)
        {
            into.Clear();
            int hour = (int)(now / 60 % 24);
            bool dark = hour >= 19 || hour < 6;
            for (int i = 0; i < file.Faults.Count; i++)
            {
                Fault f = file.Faults[i];
                if (f.Fixing || (f.Kind == FaultKind.Lamp && !dark))
                {
                    continue;
                }
                long age = Math.Max(0, now - f.Since);
                int scale = 1000 + (int)Math.Min(3000, age * 3000 / (SeenAgeDays * 1440L));
                if (dice.Next(1000) < SeenPerMille[(int)f.Kind] * scale / 1000)
                {
                    f.Reports++;
                    into.Add(f);
                }
            }
            return into.Count;
        }

        // ---------------------------------------------------------------- what they do

        /// <summary>True if a dark lamp stands within <paramref name="within"/> metres of a point.</summary>
        public static bool DarkAt(FaultFile file, float x, float z, float within)
        {
            for (int i = 0; i < file.Faults.Count; i++)
            {
                Fault f = file.Faults[i];
                if (f.Kind == FaultKind.Lamp && Flat(f.X - x, f.Z - z) <= within * within)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Street crime's scale near a dark lamp: <see cref="DarkCrime"/> after dark, 1 otherwise.</summary>
        public static float CrimeScale(bool dark, int hour)
        {
            return dark && (hour >= 19 || hour < 6) ? DarkCrime : 1f;
        }

        /// <summary>Heaps of litter within <paramref name="within"/> metres of a point.</summary>
        public static int LitterAt(FaultFile file, float x, float z, float within)
        {
            int n = 0;
            for (int i = 0; i < file.Faults.Count; i++)
            {
                Fault f = file.Faults[i];
                n += f.Kind == FaultKind.Litter && Flat(f.X - x, f.Z - z) <= within * within ? 1 : 0;
            }
            return n;
        }

        /// <summary>Litter in any district: true if the district has a heap.</summary>
        public static bool LitterIn(FaultFile file, string district)
        {
            for (int i = 0; district != null && i < file.Faults.Count; i++)
            {
                if (file.Faults[i].Kind == FaultKind.Litter && string.Equals(file.Faults[i].District, district, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Per cent of customers a business loses to heaps near its door: <see cref="LitterPercent"/> each, <see cref="LitterMostPercent"/> at most.</summary>
        public static int LitterFootfall(int heaps)
        {
            return Math.Max(LitterMostPercent, heaps * LitterPercent);
        }

        /// <summary>True if a wheel going from one point to another passes over a hole: the path comes within <see cref="PotholeRadius"/> of its middle.</summary>
        public static bool Over(float ax, float az, float bx, float bz, float px, float pz)
        {
            float dx = bx - ax, dz = bz - az;
            float length = dx * dx + dz * dz;
            float t = length > 0.0001f ? Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (pz - az) * dz) / length)) : 0f;
            float cx = ax + dx * t - px, cz = az + dz * t - pz;
            return cx * cx + cz * cz <= PotholeRadius * PotholeRadius;
        }

        /// <summary>The share of a tyre's life a hole takes at a speed (m/s): a little at walking pace, ten times that at 50 km/h.</summary>
        public static float TyreWear(float speed)
        {
            return 0.001f + Math.Max(0f, speed) * 0.0006f;
        }

        /// <summary>The jolt a hole gives a car at a speed (m/s): a change of upward speed in m/s, more the faster, at most 0.8.</summary>
        public static float Jolt(float speed)
        {
            return Math.Min(0.8f, 0.15f + Math.Max(0f, speed) * 0.03f);
        }

        /// <summary>A lane's limit at a pothole (km/h): a little slower, never below <see cref="PotholeSlowest"/>; a limit already below that stays.</summary>
        public static int SlowLimit(int kmh)
        {
            return kmh <= PotholeSlowest ? kmh : Math.Max(PotholeSlowest, kmh - PotholeSlowKmh);
        }

        /// <summary>True if a business of this cleanliness (0 to 100) is clean: an inspection finds nothing.</summary>
        public static bool Clean(int cleanliness)
        {
            return cleanliness >= CleanFrom;
        }

        // ---------------------------------------------------------------- small helpers

        public static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static float Flat(float dx, float dz)
        {
            return dx * dx + dz * dz;
        }
    }
}
