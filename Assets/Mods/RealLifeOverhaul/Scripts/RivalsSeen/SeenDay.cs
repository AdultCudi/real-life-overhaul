using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The day and the hour of the rivals seen around the city, as the director plays them: each rival's weekly mark and car, the day's sightings planned from where
    /// their week really has them, the sightings whose hour has come, the player seen in turn, and the paper's lines of the morning after. Game-free: the director and
    /// the offline checks call the same code. A sighting is only ever written from <see cref="SeenRules.Where"/> and the form the real figures gave, so it can say no
    /// more than is so. Cost: a pass over the day's rivals once a game day, a pass over the day's few sightings once a game hour.
    /// </summary>
    public static class SeenDay
    {
        /// <summary>
        /// The hours a sighting may be written at, in the order they are looked at, each once: lunch, dinner, the beach, the Hamptons, late at the office and the
        /// office on a Saturday, of which the beach's hour is lunch's and late at the office is dinner's (12:00, 19:00, 17:00, 11:00 with the catalogue's hours).
        /// Where the week has a rival at each says what is seen; an hour asked twice would only roll the same roll again.
        /// </summary>
        private static readonly int[] Hours = Once(SeenCatalogue.LunchFrom, SeenCatalogue.DinnerFrom, SeenCatalogue.BeachFrom + 1, SeenCatalogue.BeachTo + 1,
            SeenCatalogue.WorkTo + 1, SeenCatalogue.WorkFrom + 2);

        /// <summary>The hours a day's sightings are planned at, in their order: for the checks.</summary>
        public static int HourCount => Hours.Length;

        public static int HourAt(int index) => index >= 0 && index < Hours.Length ? Hours[index] : -1;

        // Each hour once, in the order first given.
        private static int[] Once(params int[] hours)
        {
            var kept = new List<int>(hours.Length);
            for (int i = 0; i < hours.Length; i++)
            {
                if (!kept.Contains(hours[i]))
                {
                    kept.Add(hours[i]);
                }
            }
            return kept.ToArray();
        }

        /// <summary>
        /// A new day: every rival's record is found or opened, marked when its week is over, and their car follows their rung (a change is a sighting, the first car is
        /// not); then the day's sightings are planned and the old ones dropped. Days missed (a long sleep, a load) are not played: nothing is written about a day nobody
        /// saw. Returns how many sightings the day got. Does nothing twice for one day.
        /// </summary>
        public static int Run(SeenFile f, IList<SeenSubject> subjects, int day, int level, ISeenCity city)
        {
            if (f == null || subjects == null || day == f.Day)
            {
                return 0;
            }
            f.Day = day;
            f.Hour = -1;
            int made = 0;
            for (int i = 0; i < subjects.Count; i++)
            {
                SeenSubject s = subjects[i];
                if (s == null || string.IsNullOrEmpty(s.Key))
                {
                    continue;
                }
                SeenRecord r = f.Of(s.Key);
                if (s.Gone)
                {
                    continue;   // out of the picture: their record stays as it was, and nothing is written
                }
                if (r == null)
                {
                    r = new SeenRecord { Key = s.Key };
                    f.Records.Add(r);
                }
                SeenRules.Mark(r, s, day, level);
                made += Car(f, r, s, day, city, SeenCatalogue.DayMost - made) ? 1 : 0;
            }
            made += Plan(f, subjects, day, city, SeenCatalogue.DayMost - made);
            Prune(f, subjects);
            return made;
        }

        // The car a rival's rung gives them: taken quietly the first time, and as a sighting when it changes after the hold. On a day that already has its sightings a
        // change waits for the next, so a week in which many figures turn never fills the feed with cars.
        private static bool Car(SeenFile f, SeenRecord r, SeenSubject s, int day, ISeenCity city, int room)
        {
            int turn = SeenRules.CarTurn(r, day);
            int cars = city != null ? city.Cars : 0;
            if (turn == 0 || cars <= 0 || (turn != 2 && room <= 0))
            {
                return false;
            }
            int rung = SeenRules.Rung(r.Tier, r.Form);
            int index = SeenRules.CarIndex(rung, cars, SeenRules.CarPick(f.Seed, SeenRules.Who(s.Key)));
            string car = index >= 0 ? city.Car(index) : null;
            if (string.IsNullOrEmpty(car))
            {
                return false;
            }
            string old = r.Car;
            r.Rung = rung;
            r.Car = car;
            r.CarDay = day;
            if (turn == 2 || car == old)
            {
                return false;
            }
            f.Notes.Add(new SeenNote
            {
                Day = day, Hour = SeenCatalogue.WorkFrom, Kind = turn > 0 ? SeenNoteKind.NewCar : SeenNoteKind.CarDown, Spot = SeenSpot.Nowhere, Form = r.Form,
                Key = s.Key, Name = s.Name ?? string.Empty, Firm = s.Firm ?? string.Empty, Car = car, Old = old ?? string.Empty
            });
            return true;
        }

        // The day's sightings: for each rival and each hour a sighting may be written at, where their week has them; a roll at each of those hours says whether
        // somebody saw them there, the first that comes up is theirs for the day, and the lowest draws among the rivals are kept. A place with no line for how the
        // business is doing gives nothing, the office only on a Saturday, and a meal needs a real eating place to be at.
        private static int Plan(SeenFile f, IList<SeenSubject> subjects, int day, ISeenCity city, int room)
        {
            if (room <= 0)
            {
                return 0;
            }
            int made = 0, first = f.Notes.Count;
            for (int i = 0; i < subjects.Count; i++)
            {
                SeenSubject s = subjects[i];
                SeenRecord r = s != null && !s.Gone ? f.Of(s.Key) : null;
                if (r == null)
                {
                    continue;
                }
                int who = SeenRules.Who(s.Key);
                for (int k = 0; k < Hours.Length; k++)
                {
                    int hour = Hours[k];
                    SeenWhere w = SeenRules.Where(f, r, s, day, hour);
                    if (w.Spot == SeenSpot.Nowhere || !Writes(w.Spot, r.Form, day) || SeenRules.Roll(f.Seed, who, day * 24 + hour, SeenRules.PurposeNote) >= SeenCatalogue.SightingChance)
                    {
                        continue;
                    }
                    SeenNote n = Note(f, r, s, w, day, hour, city);
                    if (n == null)
                    {
                        continue;
                    }
                    int order = SeenRules.Roll(f.Seed, who, day * 24 + hour, SeenRules.PurposeNote + 100);
                    made += Keep(f, n, order, first, room) ? 1 : 0;
                    break;   // one sighting of one rival at a place a day (a change of car is news of its own)
                }
            }
            Orders.Clear();
            return Math.Min(made, room);
        }

        private static readonly List<int> Orders = new List<int>(SeenCatalogue.DayMost + 1);

        // Among the day's planned sightings the lowest draws stay, at most 'room' of them, in the order of their draws.
        private static bool Keep(SeenFile f, SeenNote n, int order, int first, int room)
        {
            int at = Orders.Count;
            while (at > 0 && Orders[at - 1] > order)
            {
                at--;
            }
            if (at >= room)
            {
                return false;
            }
            Orders.Insert(at, order);
            f.Notes.Insert(first + at, n);
            if (Orders.Count > room)
            {
                Orders.RemoveAt(room);
                f.Notes.RemoveAt(first + room);
                return false;   // one came in and one went out: the count stays
            }
            return true;
        }

        /// <summary>True where a place has a line for how the business is doing (the catalogue's table): nothing is written about the others.</summary>
        public static bool Writes(SeenSpot spot, SeenForm form)
        {
            int s = (int)spot, f = (int)form;
            return s >= 0 && s < SeenCatalogue.PlaceLines.Length && f >= 0 && f < SeenCatalogue.PlaceLines[s].Length && SeenCatalogue.PlaceLines[s][f].Length > 0;
        }

        /// <summary>
        /// The same on a day: the office's line says a rival was there all Saturday, so it is written on a Saturday only; on a working day the office is where
        /// everybody is and nothing to write about.
        /// </summary>
        public static bool Writes(SeenSpot spot, SeenForm form, int day) => Writes(spot, form) && (spot != SeenSpot.Office || SeenRules.Weekday(day) == SeenRules.Saturday);

        // A sighting at a place: a meal names the eating place of their district the week's pick gives them, the office their firm. Null where there is nothing real to name.
        private static SeenNote Note(SeenFile f, SeenRecord r, SeenSubject s, SeenWhere w, int day, int hour, ISeenCity city)
        {
            var n = new SeenNote
            {
                Day = day, Hour = hour, Kind = SeenNoteKind.Place, Spot = w.Spot, Form = r.Form, Key = s.Key, Name = s.Name ?? string.Empty, Firm = s.Firm ?? string.Empty,
                Car = r.Car ?? string.Empty
            };
            if (w.Spot == SeenSpot.Lunch || w.Spot == SeenSpot.Dinner)
            {
                string district = DistrictOf(f, s, city);
                if (city == null || string.IsNullOrEmpty(district) || !city.Eatery(district, w.Grade, SeenRules.VenuePick(f.Seed, SeenRules.Who(s.Key), day), day, hour, out string name)
                    || string.IsNullOrEmpty(name))
                {
                    return null;
                }
                n.Place = name;
                n.District = district;
            }
            else if ((w.Spot == SeenSpot.Office || w.Spot == SeenSpot.Late) && n.Firm.Length == 0)
            {
                return null;
            }
            return n;
        }

        /// <summary>The district a rival eats in: their own, or with none of their own one of the city's, theirs for good by the save's seed. Empty with no districts.</summary>
        public static string DistrictOf(SeenFile f, SeenSubject s, ISeenCity city)
        {
            if (!string.IsNullOrEmpty(s.District))
            {
                return s.District;
            }
            int pick = city != null ? SeenRules.DistrictPick(f.Seed, SeenRules.Who(s.Key), city.Districts) : -1;
            return pick >= 0 ? city.District(pick) ?? string.Empty : string.Empty;
        }

        // The sightings kept: the newest, and no record of somebody the day's list no longer has unless the player knows them or a line about them is still kept.
        private static void Prune(SeenFile f, IList<SeenSubject> subjects)
        {
            Trim(f);
            for (int i = f.Records.Count - 1; i >= 0; i--)
            {
                SeenRecord r = f.Records[i];
                bool listed = false, written = false;
                for (int k = 0; k < subjects.Count && !listed; k++)
                {
                    listed = subjects[k] != null && subjects[k].Key == r.Key;
                }
                for (int k = 0; k < f.Notes.Count && !written; k++)
                {
                    written = f.Notes[k].Key == r.Key;
                }
                if (!listed && !written && r.Known == 0)
                {
                    f.Records.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The hour has turned: every sighting of today whose hour has come, and has not gone out yet, is handed to <paramref name="post"/> (Flare) and marked, at most
        /// <see cref="SeenCatalogue.PostsMost"/> a day; one of an earlier day is marked without being sent (its day is over). A sighting of a rival the day's list
        /// no longer has, or has as gone (<paramref name="subjects"/>, asked again at the hour), is dropped unsent: somebody defeated at ten is not seen at lunch at
        /// noon, nor in the next paper. An hour is gone through once, so a save and a load in the middle of a day sends nothing twice. Returns how many went out.
        /// </summary>
        public static int Hour(SeenFile f, IList<SeenSubject> subjects, int day, int hour, Action<SeenNote> post)
        {
            if (f == null || day != f.Day || hour <= f.Hour)
            {
                return 0;
            }
            f.Hour = hour;
            int sent = 0, today = Posted(f, day);
            for (int i = 0; i < f.Notes.Count; i++)
            {
                SeenNote n = f.Notes[i];
                if (n.Posted)
                {
                    continue;
                }
                if (n.Key.Length > 0 && !Here(subjects, n.Key))
                {
                    f.Notes.RemoveAt(i--);
                    continue;
                }
                if (n.Day < day)
                {
                    n.Posted = true;
                    continue;
                }
                if (n.Day == day && n.Hour <= hour && today + sent < SeenCatalogue.PostsMost)
                {
                    n.Posted = true;
                    post?.Invoke(n);
                    sent++;
                }
            }
            return sent;
        }

        /// <summary>The posts of a day that count against <see cref="SeenCatalogue.PostsMost"/>: everything that went out, the player's line too, but a test's.</summary>
        public static int Posted(SeenFile f, int day)
        {
            int count = 0;
            for (int i = 0; f != null && i < f.Notes.Count; i++)
            {
                SeenNote n = f.Notes[i];
                count += n.Day == day && n.Posted && !n.Test ? 1 : 0;
            }
            return count;
        }

        /// <summary>True while the day's list has a rival and not as gone. With no list to ask, everybody is taken to be there.</summary>
        public static bool Here(IList<SeenSubject> subjects, string key)
        {
            if (subjects == null)
            {
                return true;
            }
            for (int i = 0; i < subjects.Count; i++)
            {
                SeenSubject s = subjects[i];
                if (s != null && string.Equals(s.Key, key, StringComparison.Ordinal))
                {
                    return !s.Gone;
                }
            }
            return false;
        }

        /// <summary>
        /// The player seen in turn, as the rivals are: out in a car of the ladder's upper rungs, or at a table of somebody else's eating place, somebody now and then
        /// writes about it. Only somebody with a business of their own is known to the column, only once the gap since the last line is over, only while the day
        /// has room under <see cref="SeenCatalogue.PostsMost"/> (the line counts among the day's posts), and only by a roll for the hour. The line names the car or
        /// the place the player is really in or at, and goes to <paramref name="post"/> at once. Null when nothing is written.
        /// </summary>
        public static SeenNote Player(SeenFile f, int day, int hour, bool known, string name, int carRung, string car, string place, string district, Action<SeenNote> post)
        {
            if (f == null || !known || string.IsNullOrEmpty(name) || day - f.PlayerDay < SeenCatalogue.PlayerGapDays
                || SeenRules.Roll(f.Seed, 0, day * 24 + hour, SeenRules.PurposePlayer) >= SeenCatalogue.PlayerChance || Posted(f, day) >= SeenCatalogue.PostsMost)
            {
                return null;
            }
            bool byCar = !string.IsNullOrEmpty(car) && carRung >= SeenCatalogue.PlayerCarRung, atTable = !string.IsNullOrEmpty(place) && !string.IsNullOrEmpty(district);
            if (!byCar && !atTable)
            {
                return null;
            }
            var n = new SeenNote
            {
                Day = day, Hour = hour, Kind = atTable ? SeenNoteKind.PlayerPlace : SeenNoteKind.PlayerCar, Spot = SeenSpot.Nowhere, Form = SeenForm.Steady, Name = name,
                Place = atTable ? place : string.Empty, District = atTable ? district ?? string.Empty : string.Empty, Car = atTable ? string.Empty : car, Posted = true
            };
            f.PlayerDay = day;
            f.Notes.Add(n);
            Trim(f);
            post?.Invoke(n);
            return n;
        }

        /// <summary>
        /// A test button's sighting (Options, Tests): of the first rival due now whose place has a line for how their business is doing, where their week has them
        /// at this hour, a meal only at a real eating place; marked as a test's and as gone out, so it comes on top of the day's posts and "take back what the tests
        /// did" finds it. Null with nobody to write about.
        /// </summary>
        public static SeenNote TestNote(SeenFile f, IList<SeenSubject> subjects, List<SeenDue> due, int day, int hour, ISeenCity city)
        {
            for (int i = 0; f != null && subjects != null && due != null && i < due.Count; i++)
            {
                SeenSubject s = due[i].Subject >= 0 && due[i].Subject < subjects.Count ? subjects[due[i].Subject] : null;
                SeenRecord r = s != null ? f.Of(s.Key) : null;
                SeenSpot spot = due[i].Where.Spot;
                bool meal = spot == SeenSpot.Lunch || spot == SeenSpot.Dinner;
                string place = due[i].Place ?? string.Empty;
                if (r == null || !Writes(spot, r.Form, day) || (meal && place.Length == 0) || ((spot == SeenSpot.Office || spot == SeenSpot.Late) && string.IsNullOrEmpty(s.Firm)))
                {
                    continue;
                }
                var n = new SeenNote
                {
                    Day = day, Hour = hour, Kind = SeenNoteKind.Place, Spot = spot, Form = r.Form, Key = s.Key, Name = s.Name ?? string.Empty, Firm = s.Firm ?? string.Empty,
                    Place = meal ? place : string.Empty, District = meal ? DistrictOf(f, s, city) : string.Empty, Car = r.Car, Posted = true, Test = true
                };
                f.Notes.Add(n);
                Trim(f);
                return n;
            }
            return null;
        }

        /// <summary>
        /// The sightings within their bounds, the oldest going first: the days' and the player's within <see cref="SeenCatalogue.NotesKept"/>, a test's within
        /// <see cref="SeenCatalogue.TestNotesKept"/> beside them. A test's never pushes out one of the days', so taking the tests back leaves the days' as they were.
        /// </summary>
        public static void Trim(SeenFile f)
        {
            int tests = 0;
            for (int i = 0; f != null && i < f.Notes.Count; i++)
            {
                tests += f.Notes[i].Test ? 1 : 0;
            }
            int days = f != null ? f.Notes.Count - tests : 0;
            for (int i = 0; f != null && i < f.Notes.Count && (days > SeenCatalogue.NotesKept || tests > SeenCatalogue.TestNotesKept);)
            {
                bool test = f.Notes[i].Test;
                if (test ? tests > SeenCatalogue.TestNotesKept : days > SeenCatalogue.NotesKept)
                {
                    f.Notes.RemoveAt(i);
                    tests -= test ? 1 : 0;
                    days -= test ? 0 : 1;
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// "Take back what the tests did": every sighting a test button wrote goes, and nothing else is touched (the days' marks, cars, sightings and greetings stay
        /// as the days made them, none of the days' sightings having made room for a test's). Returns how many went.
        /// </summary>
        public static int TakeBack(SeenFile f)
        {
            int taken = 0;
            for (int i = f != null ? f.Notes.Count - 1 : -1; i >= 0; i--)
            {
                if (f.Notes[i].Test)
                {
                    f.Notes.RemoveAt(i);
                    taken++;
                }
            }
            return taken;
        }

        /// <summary>
        /// The society lines of a morning's paper: the sightings of the day before, a change of car first, <see cref="SeenCatalogue.PaperMost"/> at most. Read from the
        /// file only, so the same day gives the same lines however often it is asked.
        /// </summary>
        public static void Paper(SeenFile f, int day, List<SeenNote> into)
        {
            into.Clear();
            if (f == null)
            {
                return;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < f.Notes.Count && into.Count < SeenCatalogue.PaperMost; i++)
                {
                    SeenNote n = f.Notes[i];
                    bool car = n.Kind == SeenNoteKind.NewCar || n.Kind == SeenNoteKind.CarDown;
                    if (n.Day == day - 1 && car == (pass == 0))
                    {
                        into.Add(n);
                    }
                }
            }
        }

        /// <summary>The newest sighting of a rival, or null.</summary>
        public static SeenNote Last(SeenFile f, string key)
        {
            for (int i = f != null ? f.Notes.Count - 1 : -1; i >= 0; i--)
            {
                if (f.Notes[i].Key == key)
                {
                    return f.Notes[i];
                }
            }
            return null;
        }

        /// <summary>The locale key of a sighting's line; empty for one with no line.</summary>
        public static string LineKey(SeenNote n)
        {
            switch (n.Kind)
            {
                case SeenNoteKind.NewCar: return SeenCatalogue.NewCarLine;
                case SeenNoteKind.CarDown: return SeenCatalogue.CarDownLine;
                case SeenNoteKind.PlayerCar: return SeenCatalogue.PlayerCarLine;
                case SeenNoteKind.PlayerPlace: return SeenCatalogue.PlayerPlaceLine;
                default: return Writes(n.Spot, n.Form, n.Day) ? SeenCatalogue.PlaceLines[(int)n.Spot][(int)n.Form] : string.Empty;
            }
        }
    }
}
