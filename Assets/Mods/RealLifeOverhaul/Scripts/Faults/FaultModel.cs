using System.Collections.Generic;

namespace RealLifeOverhaul.Faults
{
    /// <summary>What has gone wrong in the street. Saved and sent as a number.</summary>
    public enum FaultKind
    {
        Lamp = 0,        // a street lamp gone dark: its lights and its lamp head off until a crew mends it
        Pothole = 1,     // a hole in a lane: a jolt and tyre wear for a car over it, traffic a little slower there
        Litter = 2,      // rubbish heaped on a pavement: fewer customers near it, the district's mood
        Hotspot = 3,     // a place where crimes really happened lately: nothing to see
        Unsafe = 4       // an open manhole or a fallen sign on a pavement: a passer-by trips now and then
    }

    /// <summary>What the City Office makes of a report checked against the city's real faults.</summary>
    public enum FaultVerdict
    {
        True = 0,        // a real fault where it was reported: a crew goes
        Nothing = 1,     // nothing there: "we looked and found nothing"
        Warned = 2,      // nothing again: a warning
        Barred = 3,      // too many reports of nothing: no crew for this player's reports for a few days
        Clean = 4        // a business inspected and found clean
    }

    /// <summary>
    /// One fault in the city, as rolled by whoever runs the city (the player alone, or the host of a Going Public
    /// session) and kept in the save. Where it is was worked out once, when it was rolled; every machine shows it there.
    /// </summary>
    public sealed class Fault
    {
        public int Id;
        public FaultKind Kind;
        public string District;      // the game's neighbourhood id
        public string Address;       // the nearest building's address in words, for the posts; may be empty
        public float X, Y, Z;        // where it is: the lamp's foot, the hole's middle, the heap's, the crime's, the manhole's
        public float Heading;        // degrees about the vertical: which way it lies
        public int Variant;          // which of its looks (litter's scatter, an open manhole or a fallen sign)
        public long Since;           // game minutes since day 0: when it appeared
        public int Reports;          // the city's people's reports of it so far
        public int Case = -1;        // the City Office's case acting on it, or -1
        public long CaseAt = -1;     // game minutes: when that case took it
        public long FixAt = -1;      // game minutes: gone then (a cleaning round, a patrol); -1 while nothing is sent or works are under way
        public int Crimes;           // a hot spot: the crimes there
        public long LastCrime = -1;  // ...and the latest of them

        public bool Fixing => Case >= 0;
    }

    /// <summary>A player's reports of nothing: how many in the week, the last day, and until when their reports bring no crew.</summary>
    public sealed class FaultStrike
    {
        public string Reporter;      // "me" alone, or "p:" and a player's id in a session
        public int Count;
        public int Day = -1;
        public int BarredUntil = -1; // a day: barred while today is before it
        public int WarnedDay = -1;
    }

    /// <summary>A fault mended: its kind, its district and the day. The time since tells a lamp's age.</summary>
    public struct FaultMended
    {
        public FaultKind Kind;
        public string District;
        public int Day;
    }

    /// <summary>Everything the city's faults keep in a save (FaultStore) and the host sends its guests (FaultShare, the faults only).</summary>
    public sealed class FaultFile
    {
        public const int MendedMost = 48;   // mended faults remembered, the newest
        public const int StrikesMost = 16;

        public readonly List<Fault> Faults = new List<Fault>(16);
        public readonly List<FaultStrike> Strikes = new List<FaultStrike>(4);
        public readonly List<FaultMended> Mended = new List<FaultMended>(16);
        public int NextId = 1;
        public long LastRoll = -1;    // game minutes: the hour last rolled
        public long WetUntil = -1;    // game minutes: rain fell lately (potholes open after it) until then
        public long StormUntil = -1;  // ...a storm
        public long FrostUntil = -1;  // ...a frost

        public Fault Find(int id)
        {
            for (int i = 0; i < Faults.Count; i++)
            {
                if (Faults[i].Id == id)
                {
                    return Faults[i];
                }
            }
            return null;
        }

        public Fault OfCase(int caseId)
        {
            for (int i = 0; caseId >= 0 && i < Faults.Count; i++)
            {
                if (Faults[i].Case == caseId)
                {
                    return Faults[i];
                }
            }
            return null;
        }

        public int Count(FaultKind kind)
        {
            int n = 0;
            for (int i = 0; i < Faults.Count; i++)
            {
                n += Faults[i].Kind == kind ? 1 : 0;
            }
            return n;
        }

        public FaultStrike Strike(string reporter, bool make)
        {
            for (int i = 0; i < Strikes.Count; i++)
            {
                if (Strikes[i].Reporter == reporter)
                {
                    return Strikes[i];
                }
            }
            if (!make || string.IsNullOrEmpty(reporter))
            {
                return null;
            }
            var s = new FaultStrike { Reporter = reporter };
            Strikes.Add(s);
            while (Strikes.Count > StrikesMost)
            {
                Strikes.RemoveAt(0);
            }
            return s;
        }
    }

    /// <summary>One district as the roll weighs it.</summary>
    public sealed class FaultDistrict
    {
        public string Id;
        public int Spending;   // the council's upkeep there: -1 short (a cut to cleaning, a poorer district), 0, +1 generous
        public int Busy;       // 0 to 3: its streets' traffic and its shops
        public int Night;      // 0 to 3: its bars, clubs and restaurants
    }

    /// <summary>The city as one hour's roll reads it.</summary>
    public sealed class FaultFacts
    {
        public long Now;             // game minutes since day 0
        public int Option = 1;       // how many: 0 a few, 1 some, 2 many (Settings.CityFaultsCount)
        public bool Rain, Storm, Frost;
        public int Litter;           // the council's street cleaning: -1 cut, 0, +1 more
        public readonly List<FaultDistrict> Districts = new List<FaultDistrict>(12);

        public int Hour => (int)(Now / 60 % 24);
        public int Day => (int)(Now / 1440);
    }

    /// <summary>A fault the roll wants: its kind and the district (an index into the facts' list). The director finds its place.</summary>
    public struct FaultWant
    {
        public FaultKind Kind;
        public int District;
        public int Variant;
        public float Heading;
    }

    /// <summary>
    /// The source of chance for the faults. The live one (FaultDice) is truly random; the offline checks give one of
    /// their own, so the rules can be checked exactly.
    /// </summary>
    public interface IFaultDice
    {
        /// <summary>A whole number from 0 up to but not including <paramref name="most"/> (0 when most is 1 or less).</summary>
        int Next(int most);
    }
}
