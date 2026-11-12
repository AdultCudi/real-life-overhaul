using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The rules of the rivals seen around the city: how a business is doing and what its owner can afford, both read from their real figures; the car that goes with
    /// it; the seeded week that says where each rival is at an hour; who the world may show; and what greeting one is worth. Game-free, checked offline. Every draw is a
    /// hash of the save's seed, the rival, the day and its purpose, so the same save gives the same week on every machine and every load. Cost: a few sums a rival, on
    /// the day's pass, the hour or a press; nothing per frame.
    /// </summary>
    public static class SeenRules
    {
        public const int WeekDays = 7;
        public const int Saturday = 5, Sunday = 6;   // a week starts on Monday, as the game's does (TimeHelper.GetDayOfWeek)

        private const int PurposeLunch = 1, PurposeDinner = 2, PurposeBeach = 3, PurposeVenue = 4, PurposeOrder = 5, PurposeCar = 6, PurposeHello = 7, PurposeDistrict = 8;
        internal const int PurposeNote = 9, PurposePlayer = 10;

        /// <summary>True while the feature does anything at all: its option, the living economy's master option, and no Going Public session (the city's people are the host's).</summary>
        public static bool Runs(bool option, bool living, bool session) => option && living && !session;

        // ---- the save's seed, and when the look may go on

        /// <summary>The rivals' own feature, as the look reads it (SeenBridges.Rivals): not started for this save yet, giving no tycoons, or running and awake.</summary>
        public const int RivalsWait = 0, RivalsOff = 1, RivalsOn = 2;

        /// <summary>
        /// The rivals' own feature from what can be read of it: off when it was never made, its option is off or a fault switched it off (no tycoons will come, so
        /// nothing waits for them); on while it runs and is not at rest in a Going Public session; else not started for this save yet.
        /// </summary>
        public static int RivalsState(bool made, bool option, bool broken, bool running, bool resting)
        {
            if (!made || !option || broken)
            {
                return RivalsOff;
            }
            return running && !resting ? RivalsOn : RivalsWait;
        }

        /// <summary>
        /// What a look does with the save: nothing before a save is ready; with the feature not wanted, what was loaded is put away; and nothing is loaded, passed
        /// or put out until the rivals' own feature has made up its mind (running, or off), so a new file, a load's first day and the day after a Going Public
        /// session all see its tycoons.
        /// </summary>
        public static SeenStep Look(bool ready, bool wanted, bool loaded, int rivals)
        {
            if (!ready)
            {
                return SeenStep.Wait;
            }
            if (!wanted)
            {
                return loaded ? SeenStep.PutAway : SeenStep.Wait;
            }
            return rivals == RivalsWait ? SeenStep.Wait : SeenStep.Go;
        }

        /// <summary>True when the day's rivals are read again: a new day, or the rivals' own feature come on or gone off since they were read.</summary>
        public static bool ReadAgain(int day, int readDay, int rivals, int readRivals) => day != readDay || rivals != readRivals;

        /// <summary>
        /// The seed of a new file: a hash of the playthrough's own id (the game's GameInstance.seed, made once with the save, through SeenBridges.Playthrough), so
        /// the same save gives the same seed on every machine and every load. Never 0, which is a file that has none yet.
        /// </summary>
        public static int SeedOf(string playthrough)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; playthrough != null && i < playthrough.Length; i++)
                {
                    h = (h ^ playthrough[i]) * 16777619u;
                }
                h = (h ^ 0x52534545u) * 2654435761u;   // the feature's own salt: another feature's hash of the same id draws apart from this one
                h ^= h >> 16;
                int seed = (int)(h & 0x7FFFFFFF);
                return seed == 0 ? 1 : seed;
            }
        }

        /// <summary>
        /// A file read from a save, set against the game's day: the day of the player's last line, a car's day or a sighting's day past today (a damaged line, or
        /// one carried over from a later save) is brought back, so no gap or hold waits for a day that has not come. Returns how many were brought back.
        /// </summary>
        public static int Settle(SeenFile f, int today)
        {
            if (f == null)
            {
                return 0;
            }
            int moved = 0;
            if (f.PlayerDay > today)
            {
                f.PlayerDay = today;
                moved++;
            }
            for (int i = 0; i < f.Records.Count; i++)
            {
                if (f.Records[i].CarDay > today)
                {
                    f.Records[i].CarDay = today;
                    moved++;
                }
            }
            for (int i = f.Notes.Count - 1; i >= 0; i--)
            {
                if (f.Notes[i].Day > today)
                {
                    f.Notes.RemoveAt(i);
                    moved++;
                }
            }
            return moved;
        }

        /// <summary>A number from 0 to 999, the same on every machine for the same inputs.</summary>
        public static int Roll(int seed, int who, int day, int purpose)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)who * 2246822519u ^ (uint)day * 3266489917u ^ (uint)purpose * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % 1000u);
            }
        }

        /// <summary>A rival's key as a number: the same on every machine (never the runtime's own hash of a string, which a runtime may change).</summary>
        public static int Who(string key)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; key != null && i < key.Length; i++)
                {
                    h = (h ^ key[i]) * 16777619u;
                }
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>The day of the week, 0 Monday to 6 Sunday, as the game counts it from day 1.</summary>
        public static int Weekday(int day) => ((day - 1) % WeekDays + WeekDays) % WeekDays;

        // ---- how the business is doing

        /// <summary>The change of a figure against what it was, per mille; 0 where there was nothing before to compare with.</summary>
        public static int Trend(long now, long before)
        {
            if (before <= 0)
            {
                return 0;
            }
            long t = (now - before) * 1000L / before;
            return (int)Math.Max(-1000L, Math.Min(10000L, t));
        }

        /// <summary>
        /// How a rival's business is doing, from their real figures alone: the week's change of a tycoon's worth or an owner's takings, the businesses won or lost, and
        /// a counter of the player's that holds a tycoon back. With nothing to compare with it is steady: nothing is guessed.
        /// </summary>
        public static SeenForm Form(bool tycoon, long figure, long before, int shops, int shopsBefore, bool pressed)
        {
            int trend = Trend(figure, before);
            if (tycoon)
            {
                return pressed || trend <= -SeenCatalogue.TycoonTrouble ? SeenForm.Trouble : trend >= SeenCatalogue.TycoonThrive ? SeenForm.Thriving : SeenForm.Steady;
            }
            bool lost = shopsBefore >= 0 && shops < shopsBefore, won = shopsBefore >= 0 && shops > shopsBefore;
            if (trend <= -SeenCatalogue.OwnerTrouble || (lost && trend < SeenCatalogue.OwnerThrive))
            {
                return SeenForm.Trouble;
            }
            return trend >= SeenCatalogue.OwnerThrive || won ? SeenForm.Thriving : SeenForm.Steady;
        }

        /// <summary>A rival's means, 0 modest to 4 very rich, from their real figure brought back to the start's prices (the level is tenths of a point).</summary>
        public static int Tier(bool tycoon, long figure, int level)
        {
            long atStart = figure * 1000L / SeenCatalogue.Clamp(level, 500, 4000);
            long[] from = tycoon ? SeenCatalogue.TycoonTierFrom : SeenCatalogue.OwnerTierFrom;
            int tier = 0;
            for (int i = 1; i < from.Length && i < SeenCatalogue.Tiers; i++)
            {
                if (atStart >= from[i])
                {
                    tier = i;
                }
            }
            return tier;
        }

        /// <summary>
        /// The weekly mark of a rival's record: on the first look, and then every seventh day, the figure now is set against the one before it and the form and the means
        /// are read again. Between two marks they stay, so a rival's week is one week. True if a mark was made.
        /// </summary>
        public static bool Mark(SeenRecord r, SeenSubject s, int day, int level)
        {
            if (r == null || s == null || s.Gone)
            {
                return false;
            }
            bool first = r.MarkDay < 0;
            if (!first && day - r.MarkDay < WeekDays && day >= r.MarkDay)
            {
                return false;
            }
            r.Before = first ? Math.Max(0L, s.Before) : r.Figure;
            r.ShopsBefore = first ? s.ShopsBefore : r.Shops;
            r.Figure = s.Figure;
            r.Shops = s.Shops;
            r.MarkDay = day;
            r.Name = s.Name ?? string.Empty;
            r.Form = Form(s.Tycoon, r.Figure, r.Before, r.Shops, r.ShopsBefore, s.Pressed);
            r.Tier = Tier(s.Tycoon, r.Figure, level);
            return true;
        }

        // ---- the car

        /// <summary>The rung of the car ladder for their means and how the business is doing: one up when it thrives, one down when it is in trouble.</summary>
        public static int Rung(int tier, SeenForm form)
        {
            int rung = SeenCatalogue.Clamp(tier, 0, SeenCatalogue.Tiers - 1) + (form == SeenForm.Thriving ? 1 : form == SeenForm.Trouble ? -1 : 0);
            return SeenCatalogue.Clamp(rung, 0, SeenCatalogue.Rungs - 1);
        }

        /// <summary>
        /// The car of a rung among the game's cars, the cheapest first: each rung has its share of the ladder, and a rival's own pick (0 to 999) says which of its cars.
        /// -1 with no cars.
        /// </summary>
        public static int CarIndex(int rung, int cars, int pick)
        {
            if (cars <= 0)
            {
                return -1;
            }
            rung = SeenCatalogue.Clamp(rung, 0, SeenCatalogue.Rungs - 1);
            int from = rung * cars / SeenCatalogue.Rungs, to = (rung + 1) * cars / SeenCatalogue.Rungs;
            to = Math.Max(to, from + 1);
            int index = from + SeenCatalogue.Clamp(pick, 0, 999) * (to - from) / 1000;
            return SeenCatalogue.Clamp(index, 0, cars - 1);
        }

        /// <summary>The rung a car of the ladder stands on (the reverse of <see cref="CarIndex"/>): the player's own car is read by it. -1 for no car.</summary>
        public static int RungOf(int index, int cars)
        {
            if (cars <= 0 || index < 0)
            {
                return -1;
            }
            index = Math.Min(index, cars - 1);
            for (int rung = SeenCatalogue.Rungs - 1; rung >= 0; rung--)
            {
                if (index >= rung * cars / SeenCatalogue.Rungs)
                {
                    return rung;
                }
            }
            return 0;
        }

        /// <summary>A rival's own pick among the cars of a rung: theirs for good, so the same rival keeps to the same taste.</summary>
        public static int CarPick(int seed, int who) => Roll(seed, who, 0, PurposeCar);

        /// <summary>
        /// A rival's car against the rung their figures now give them: 0 for no change (the same rung, or the car held less than <see cref="SeenCatalogue.CarHoldDays"/>),
        /// 1 for a dearer car, -1 for a cheaper one. A rival with no car on record takes theirs quietly (2): what they drove before is not known, so nothing is said.
        /// </summary>
        public static int CarTurn(SeenRecord r, int day)
        {
            int rung = Rung(r.Tier, r.Form);
            if (r.Rung < 0 || string.IsNullOrEmpty(r.Car))
            {
                return 2;
            }
            if (rung == r.Rung || day - r.CarDay < SeenCatalogue.CarHoldDays)
            {
                return 0;
            }
            return rung > r.Rung ? 1 : -1;
        }

        // ---- the week

        /// <summary>How smart an eating place they go to: by their means, a step down in trouble and a step up when the business thrives.</summary>
        public static int Grade(int tier, SeenForm form)
        {
            int grade = tier >= 3 ? 2 : tier >= 1 ? 1 : 0;
            grade += form == SeenForm.Thriving ? 1 : form == SeenForm.Trouble ? -1 : 0;
            return SeenCatalogue.Clamp(grade, 0, SeenCatalogue.Grades - 1);
        }

        /// <summary>
        /// Where a rival is at an hour of a day. A working day: the office, lunch out by a roll that follows how the business is doing, the office again (a thriving
        /// rival leaves an hour early, one in trouble stays late), dinner out by another roll. A Saturday: the Hamptons for a thriving rival with a house there or the
        /// means for it, an afternoon on the beach for a steady one by a roll, the office for one in trouble. A Sunday: the beach and the Hamptons for the same
        /// thriving rival with a house there or the means for it, home for everybody else.
        /// Somebody gone by their source's rules is nowhere, and somebody in the Hamptons is not at lunch in town. In a city without the Hamptons (<paramref name="coast"/>
        /// false) nobody goes there: a weekend is spent in town.
        /// </summary>
        public static SeenWhere Where(int seed, int who, SeenForm form, int tier, bool hamptons, bool gone, int day, int hour, bool coast = true)
        {
            var w = new SeenWhere { Spot = SeenSpot.Nowhere };
            if (gone || hour < 0 || hour > 23)
            {
                return w;
            }
            int f = SeenCatalogue.Clamp((int)form, 0, (int)SeenForm.Count - 1);
            int weekday = Weekday(day);
            bool away = coast && form == SeenForm.Thriving && (hamptons || tier >= SeenCatalogue.HamptonsTier);
            bool lunchHour = hour >= SeenCatalogue.LunchFrom && hour < SeenCatalogue.LunchTo;
            bool beachHour = hour >= SeenCatalogue.BeachFrom && hour < SeenCatalogue.BeachTo;
            bool lunchOut = Roll(seed, who, day, PurposeLunch) < SeenCatalogue.LunchOut[f];
            if (weekday == Saturday || weekday == Sunday)
            {
                if (away)
                {
                    bool there = hour >= SeenCatalogue.HamptonsFrom && hour < (weekday == Sunday ? SeenCatalogue.BeachTo : SeenCatalogue.HamptonsTo);
                    w.Spot = beachHour ? SeenSpot.Beach : there ? SeenSpot.Hamptons : SeenSpot.Nowhere;
                    return w;
                }
                if (weekday == Sunday)
                {
                    return w;
                }
                if (form == SeenForm.Trouble)
                {
                    w.Spot = hour >= SeenCatalogue.WorkFrom && hour < SeenCatalogue.WorkTo ? SeenSpot.Office : SeenSpot.Nowhere;
                    return w;
                }
                if (coast && form == SeenForm.Steady && Roll(seed, who, day, PurposeBeach) < SeenCatalogue.SteadyBeach)
                {
                    w.Spot = beachHour ? SeenSpot.Beach : SeenSpot.Nowhere;
                    return w;
                }
                if (lunchHour && lunchOut)
                {
                    w.Spot = SeenSpot.Lunch;
                    w.Grade = Grade(tier, form);
                }
                return w;
            }
            if (lunchHour)
            {
                w.Spot = lunchOut ? SeenSpot.Lunch : SeenSpot.Office;
                w.Grade = lunchOut ? Grade(tier, form) : 0;
                w.Alone = lunchOut && form == SeenForm.Trouble;
                return w;
            }
            int leaves = form == SeenForm.Thriving ? SeenCatalogue.ThrivingLeaves : SeenCatalogue.WorkTo;
            if (hour >= SeenCatalogue.WorkFrom && hour < leaves)
            {
                w.Spot = SeenSpot.Office;
                return w;
            }
            if (form == SeenForm.Trouble && hour >= SeenCatalogue.WorkTo && hour < SeenCatalogue.LateTo)
            {
                w.Spot = SeenSpot.Late;
                return w;
            }
            if (hour >= SeenCatalogue.DinnerFrom && hour < SeenCatalogue.DinnerTo && Roll(seed, who, day, PurposeDinner) < SeenCatalogue.DinnerOut[f])
            {
                w.Spot = SeenSpot.Dinner;
                w.Grade = Grade(tier, form);
            }
            return w;
        }

        /// <summary>The place of a rival's record at an hour: <see cref="Where"/> on what the record holds. Somebody never marked is nowhere.</summary>
        public static SeenWhere Where(SeenFile f, SeenRecord r, SeenSubject s, int day, int hour)
        {
            if (f == null || r == null || s == null || r.MarkDay < 0)
            {
                return new SeenWhere { Spot = SeenSpot.Nowhere };
            }
            return Where(f.Seed, Who(s.Key), r.Form, r.Tier, s.Hamptons, s.Gone, day, hour, f.Coast);
        }

        /// <summary>The next place a rival is at today from an hour on, and when; Nowhere if the day holds nothing more.</summary>
        public static SeenSpot Next(SeenFile f, SeenRecord r, SeenSubject s, int day, int fromHour, out int atHour)
        {
            for (int h = Math.Max(0, fromHour); h < 24; h++)
            {
                SeenSpot spot = Where(f, r, s, day, h).Spot;
                if (spot != SeenSpot.Nowhere)
                {
                    atHour = h;
                    return spot;
                }
            }
            atHour = -1;
            return SeenSpot.Nowhere;
        }

        /// <summary>Which of the places that fit a rival goes to on a day (0 to 999): the same all day.</summary>
        public static int VenuePick(int seed, int who, int day) => Roll(seed, who, day, PurposeVenue);

        /// <summary>
        /// Which of a district's eating places, the cheapest address first, a grade and a pick give: each grade has its third of the list (with fewer than three places
        /// the grades share them), and the pick says which of that third. -1 with none.
        /// </summary>
        public static int VenueIndex(int count, int grade, int pick)
        {
            if (count <= 0)
            {
                return -1;
            }
            grade = SeenCatalogue.Clamp(grade, 0, SeenCatalogue.Grades - 1);
            int from = Math.Min(count - 1, grade * count / SeenCatalogue.Grades), to = Math.Max(from + 1, (grade + 1) * count / SeenCatalogue.Grades);
            return SeenCatalogue.Clamp(from + SeenCatalogue.Clamp(pick, 0, 999) * (to - from) / 1000, 0, count - 1);
        }

        /// <summary>The district a rival with none of their own eats in: one of the city's, theirs for good. -1 with no districts.</summary>
        public static int DistrictPick(int seed, int who, int districts) => districts <= 0 ? -1 : Roll(seed, who, 0, PurposeDistrict) * districts / 1000;

        // ---- in the world

        /// <summary>
        /// Everybody who is somewhere this hour, in their turn (a seeded order that changes with the hour, so the same two are not always first). Fills a list the caller
        /// keeps: nothing is allocated once it has grown to the day's rivals.
        /// </summary>
        public static void Due(SeenFile f, IList<SeenSubject> subjects, int day, int hour, List<SeenDue> into)
        {
            into.Clear();
            if (f == null || subjects == null)
            {
                return;
            }
            for (int i = 0; i < subjects.Count; i++)
            {
                SeenSubject s = subjects[i];
                SeenRecord r = s != null ? f.Of(s.Key) : null;
                SeenWhere w = Where(f, r, s, day, hour);
                if (w.Spot == SeenSpot.Nowhere)
                {
                    continue;
                }
                int who = Who(s.Key);
                var due = new SeenDue { Subject = i, Where = w, Pick = VenuePick(f.Seed, who, day), Order = Roll(f.Seed, who, day * 24 + hour, PurposeOrder) };
                int at = into.Count;
                into.Add(due);
                while (at > 0 && (into[at - 1].Order > due.Order || (into[at - 1].Order == due.Order && into[at - 1].Subject > due.Subject)))
                {
                    into[at] = into[at - 1];
                    at--;
                }
                into[at] = due;
            }
        }

        /// <summary>True if one more rival may be put in the world: under the cap, and the living city's pool keeps people for its own scenes.</summary>
        public static bool MayPut(int outNow, int poolFree) => outNow >= 0 && outNow < SeenCatalogue.WorldMost && poolFree > SeenCatalogue.PoolKeep;

        /// <summary>
        /// Who is tried this look, if anybody: the first in the hour's turn whose place has a building, who does not stand already, whose placing has not failed
        /// this hour, and whose place is in the ring round the player (x, z); nobody at the cap or with the living city's pool short. One a look at most, so a
        /// street of places with no spot to stand costs one placing a look and each of them once an hour. -1 for nobody.
        /// </summary>
        public static int PutPick(IList<SeenSubject> subjects, List<SeenDue> due, string[] outKeys, int outCount, int poolFree, float x, float z)
        {
            if (subjects == null || due == null || !MayPut(outCount, poolFree))
            {
                return -1;
            }
            for (int i = 0; i < due.Count; i++)
            {
                SeenDue d = due[i];
                if (d.Building == null || d.Failed || d.Subject < 0 || d.Subject >= subjects.Count)
                {
                    continue;
                }
                float dx = d.X - x, dz = d.Z - z;
                if (InRing(dx * dx + dz * dz) && !IsOut(subjects[d.Subject].Key, outKeys, outCount))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A placing failed: that rival is not tried again until the hour turns, when the hour's list is made afresh.</summary>
        public static void Fail(List<SeenDue> due, int index)
        {
            if (due == null || index < 0 || index >= due.Count)
            {
                return;
            }
            SeenDue d = due[index];
            d.Failed = true;
            due[index] = d;
        }

        /// <summary>True for a key among the first <paramref name="count"/> of those standing.</summary>
        public static bool IsOut(string key, string[] outKeys, int count)
        {
            for (int i = 0; outKeys != null && i < count && i < outKeys.Length; i++)
            {
                if (string.Equals(outKeys[i], key, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if somebody may appear at a spot: only where the player is not looking, but for a test button's, which is put in front of the player.</summary>
        public static bool MayStand(bool inView, bool test) => test || !inView;

        /// <summary>
        /// Whether somebody standing in the world goes this look: taken back by the living city whatever else; never while the player is talking with them; left
        /// behind past <see cref="SeenCatalogue.GoneMetres"/>; their hour at the place over (a test's never is) and nobody looking.
        /// </summary>
        public static SeenLeave Leaves(bool lost, bool talking, float sqrMetres, bool test, bool still, bool inView)
        {
            if (lost)
            {
                return SeenLeave.Taken;
            }
            if (talking)
            {
                return SeenLeave.Stays;
            }
            if (LeftBehind(sqrMetres))
            {
                return SeenLeave.Behind;
            }
            return !test && !still && !inView ? SeenLeave.Over : SeenLeave.Stays;
        }

        /// <summary>True while the hour's list still has a rival at that place.</summary>
        public static bool Still(IList<SeenSubject> subjects, List<SeenDue> due, string key, SeenSpot spot)
        {
            for (int i = 0; subjects != null && due != null && i < due.Count; i++)
            {
                int s = due[i].Subject;
                if (s >= 0 && s < subjects.Count && string.Equals(subjects[s].Key, key, StringComparison.Ordinal))
                {
                    return due[i].Where.Spot == spot;
                }
            }
            return false;
        }

        /// <summary>
        /// Tenths of a real second until the greeting's panel is looked at again: <see cref="SeenCatalogue.GlanceTenths"/> while somebody stands within
        /// <see cref="SeenCatalogue.GlanceMetres"/> of the player (metres squared), else ten, once a second with the rest of the look. A runner covers
        /// <see cref="SeenCatalogue.RunMetres"/> a second, more than the panel's reach, so a look a second could miss them; nothing is looked at a frame.
        /// </summary>
        public static int Glance(float nearestSqr)
        {
            return nearestSqr <= (float)SeenCatalogue.GlanceMetres * SeenCatalogue.GlanceMetres ? SeenCatalogue.GlanceTenths : 10;
        }

        /// <summary>
        /// The spots by a door in the order they are tried: the side of the pavement point before it (-1 or 1) and how far along the kerb
        /// (<see cref="SeenCatalogue.DoorAlong"/>, the nearer first). False past the last.
        /// </summary>
        public static bool DoorSpot(int attempt, out int side, out float along)
        {
            if (attempt < 0 || attempt >= SeenCatalogue.DoorAlong.Length * 2)
            {
                side = 0;
                along = 0f;
                return false;
            }
            side = attempt % 2 == 0 ? -1 : 1;
            along = SeenCatalogue.DoorAlong[attempt / 2];
            return true;
        }

        /// <summary>
        /// An address key as the rivals' sources write an office ("street#number", the number in invariant digits) taken apart without a string made: where the
        /// street ends (the last '#') and the number. False for a key that is not one.
        /// </summary>
        public static bool Address(string key, out int cut, out int number)
        {
            cut = key != null ? key.LastIndexOf('#') : -1;
            number = 0;
            int at = cut + 1;
            bool minus = cut >= 0 && at < key.Length && key[at] == '-';
            at += minus ? 1 : 0;
            if (cut < 0 || at >= key.Length || (key[at] == '0' && key.Length - at > 1))
            {
                cut = -1;
                return false;   // no digits, or a leading zero: not how a number is written
            }
            long n = 0;
            for (int i = at; i < key.Length; i++)
            {
                char c = key[i];
                if (c < '0' || c > '9' || n > int.MaxValue)
                {
                    cut = -1;
                    return false;
                }
                n = n * 10 + (c - '0');
            }
            n = minus ? -n : n;
            if (n < int.MinValue || n > int.MaxValue)
            {
                cut = -1;
                return false;
            }
            number = (int)n;
            return true;
        }

        /// <summary>
        /// Where the name of a car type's model stands in the type's id: the part between its first and second '_', which is the name the game gives the model's
        /// car (StringIdExtensions.GetIdWithoutType; its private driver loads the car by it). False with none.
        /// </summary>
        public static bool CarId(string type, out int from, out int length)
        {
            int cut = type != null ? type.IndexOf('_') : -1;
            int end = cut >= 0 ? type.IndexOf('_', cut + 1) : -1;
            from = cut + 1;
            length = cut < 0 ? 0 : (end < 0 ? type.Length : end) - from;
            return cut >= 0 && length > 0;
        }

        /// <summary>True if a record's key is that of the game's rival owner with this id ("g" and the id), asked without making the key.</summary>
        public static bool IsOwnerKey(string key, string gameId)
        {
            return key != null && gameId != null && key.Length == gameId.Length + 1 && key[0] == 'g' && string.CompareOrdinal(key, 1, gameId, 0, gameId.Length) == 0;
        }

        /// <summary>True if a record's key is that of the tycoon at this place of the rich list ("t" and the index), asked without making the key.</summary>
        public static bool IsTycoonKey(string key, int index)
        {
            if (key == null || key.Length < 2 || key[0] != 't' || index < 0 || (key[1] == '0' && key.Length > 2))
            {
                return false;
            }
            long n = 0;
            for (int i = 1; i < key.Length; i++)
            {
                char c = key[i];
                if (c < '0' || c > '9' || n > int.MaxValue)
                {
                    return false;
                }
                n = n * 10 + (c - '0');
            }
            return n == index;
        }

        /// <summary>True if a place that far from the player (metres, squared) is near enough for its rival to be put out, and not so near that they would appear beside the player.</summary>
        public static bool InRing(float sqrMetres)
        {
            return sqrMetres >= (float)SeenCatalogue.LeastMetres * SeenCatalogue.LeastMetres && sqrMetres <= (float)SeenCatalogue.NearMetres * SeenCatalogue.NearMetres;
        }

        /// <summary>True if a rival that far behind (metres, squared) is taken away whether or not they are in view.</summary>
        public static bool LeftBehind(float sqrMetres) => sqrMetres > (float)SeenCatalogue.GoneMetres * SeenCatalogue.GoneMetres;

        // ---- knowing them

        /// <summary>How well a rival must know the player before they say where they will be: the social life's own bar for a friend, in tenths.</summary>
        public static int KnowsFrom => (int)(SocialRules.FriendFamiliarity * 10f);

        /// <summary>
        /// A greeting: what the day's first talk face to face is worth by the social life's own rule (<see cref="SocialRules.Chat"/>), in tenths of a point, added to how
        /// well they know the player and held at the scale's top. A second greeting on the same day is worth nothing, however often it is made. Returns what was gained.
        /// </summary>
        public static int Greet(SeenRecord r, int day, bool warm)
        {
            if (r == null)
            {
                return 0;
            }
            bool first = r.GreetDay != day;
            SocialWorth worth = SocialRules.Chat(first, true, false, warm, true);
            int gain = (int)Math.Round(worth.Familiarity * 10f);
            int before = r.Known;
            r.Known = SeenCatalogue.Clamp(r.Known + Math.Max(0, gain), 0, SeenCatalogue.GreetWarmMost * 10);
            r.GreetDay = day;
            r.Greets = Math.Min(int.MaxValue - 1, r.Greets) + 1;
            return r.Known - before;
        }

        /// <summary>Which of a rival's two ways of saying hello today (0 or 1).</summary>
        public static int HelloPick(int seed, int who, int day) => Roll(seed, who, day, PurposeHello) % 2;

        /// <summary>
        /// The player says hello to a rival standing in the world: the greeting is counted here, once a day by <see cref="Greet"/>, and what the talk is about is
        /// made before it opens. The talk gets values only, so nothing said in it earns anything. Null for somebody not on record.
        /// </summary>
        public static SeenTalk Hello(SeenFile f, string key, string name, int day)
        {
            SeenRecord r = f != null && key != null ? f.Of(key) : null;
            if (r == null)
            {
                return null;
            }
            bool knew = r.Known >= KnowsFrom;
            int gained = Greet(r, day, true);
            return new SeenTalk { Name = string.IsNullOrEmpty(name) ? r.Name : name, Form = r.Form, Known = knew, Car = r.Car, Pick = HelloPick(f.Seed, Who(key), day), Gained = gained };
        }
    }
}
