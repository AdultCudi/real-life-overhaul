using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a sitting is for; never saved.</summary>
    public enum SittingKind
    {
        Class = 0,      // a class of a course, at its campus or online at home
        Homework = 1,   // a piece of homework or revision, at home or at the campus
        Exam = 2,       // a course's exam
        Work = 3,       // a day at a job
        Overtime = 4,   // more hours of it
        Contract = 5,   // freelance work or a practice's client
        Interview = 6,  // a job interview
        Tutoring = 7,   // a tutoring session given
        Ceremony = 8    // a graduation day
    }

    /// <summary>How a sitting's record was settled, once (<see cref="SittingRecord.Settle"/>).</summary>
    public enum SittingEnd
    {
        Open = 0,        // not settled yet
        Ended = 1,       // its activity ended with minutes sat: they are counted
        CalledOff = 2,   // it ended before it began (the walk there stopped): nothing counted
        LetGo = 3        // the director let it go (its activity gone from the game's panel unsettled, or the part stopping): nothing counted
    }

    /// <summary>
    /// A sitting's own record, from the moment it is handed to an activity of the game's panel until it is settled: what it is
    /// for, the day it was begun on and whether it has begun, so that its end is counted by the record itself, once, whatever
    /// the director holds as its current sitting. Game-free (EconomyCheck runs it); the activity is kept as an object.
    /// </summary>
    public sealed class SittingRecord
    {
        public readonly SittingKind Kind;
        public readonly int Of;          // the course's id, the job's, the contract's
        public readonly int Length;      // game minutes asked for
        public readonly bool Tiring;     // it spends the player's energy while it runs
        public int Day = -1;             // the save's day it was handed out on; -1 until then
        public object Holder;            // the activity on the game's panel that runs it (an IPlayerActivity in the mod)

        public bool Begun { get; private set; }
        public SittingEnd End { get; private set; }

        public SittingRecord(SittingKind kind, int of, int length, bool tiring)
        {
            Kind = kind;
            Of = of;
            Length = length < 1 ? 1 : length;
            Tiring = tiring;
        }

        public bool Settled => End != SittingEnd.Open;

        /// <summary>The player has sat down (or the clock has started): false if it was settled first, and then it never begins.</summary>
        public bool Begin()
        {
            if (Settled)
            {
                return false;
            }
            Begun = true;
            return true;
        }

        /// <summary>Settled once: true the first time, with how; false every time after, so a second end counts nothing.</summary>
        public bool Settle(SittingEnd how)
        {
            if (Settled || how == SittingEnd.Open)
            {
                return false;
            }
            End = how;
            return true;
        }
    }

    /// <summary>What the once-a-second look does with an open sitting (<see cref="SittingRules.Look"/>).</summary>
    public enum SittingLook
    {
        Keep = 0,     // its activity is still on the game's panel
        Finish = 1,   // the panel has let its activity go: the activity is finished (its minutes so far counted once), and what it leaves unsettled is let go
        Drop = 2      // settled already: taken out of the book
    }

    /// <summary>
    /// The sittings open now, by their records: the truth the hourly pass and the end of a sitting read, never one reference
    /// that can be cleared or replaced while a sitting runs. Rarely more than one; a look costs a loop over them. Game-free.
    /// </summary>
    public sealed class SittingBook
    {
        private readonly List<SittingRecord> _open = new List<SittingRecord>(2);

        public int Count => _open.Count;

        public SittingRecord this[int i] => _open[i];

        /// <summary>A sitting handed to an activity: in the book until it is settled. Holding it again changes nothing; a settled one is not taken.</summary>
        public void Hold(SittingRecord r)
        {
            if (r != null && !r.Settled && !_open.Contains(r))
            {
                _open.Add(r);
            }
        }

        /// <summary>Taken out of the book (settled, or let go); nothing if it was not in it.</summary>
        public void Release(SittingRecord r)
        {
            if (r != null)
            {
                _open.Remove(r);
            }
        }

        /// <summary>True while this record is in the book: handed out, and neither let go nor taken out after its end.</summary>
        public bool Holds(SittingRecord r)
        {
            return r != null && _open.Contains(r);
        }

        /// <summary>True while a sitting of this kind (and of this course, job or post; -1 for any) is open and unsettled.</summary>
        public bool Has(SittingKind kind, int of = -1)
        {
            for (int i = 0; i < _open.Count; i++)
            {
                SittingRecord r = _open[i];
                if (!r.Settled && r.Kind == kind && (of < 0 || r.Of == of))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True while a class of the course is being sat (or the player is on the way to it): the hourly pass never marks it missed then.</summary>
        public bool InClass(int course) => course >= 0 && Has(SittingKind.Class, course);

        /// <summary>The course whose class is open now, -1 for none.</summary>
        public int ClassOf()
        {
            for (int i = 0; i < _open.Count; i++)
            {
                if (!_open[i].Settled && _open[i].Kind == SittingKind.Class)
                {
                    return _open[i].Of;
                }
            }
            return -1;
        }

        /// <summary>True while a class, homework or an exam is open.</summary>
        public bool Studying => Has(SittingKind.Class) || Has(SittingKind.Homework) || Has(SittingKind.Exam);

        /// <summary>True while a working day or its overtime is open.</summary>
        public bool Working => Has(SittingKind.Work) || Has(SittingKind.Overtime);
    }

    /// <summary>Game-free rules for a sitting's life: the once-a-second look and whether a class sat counts.</summary>
    public static class SittingRules
    {
        /// <summary>What the once-a-second look does: an open sitting whose activity the game's panel still shows is kept, else finished.</summary>
        public static SittingLook Look(bool settled, bool shown)
        {
            if (settled)
            {
                return SittingLook.Drop;
            }
            return shown ? SittingLook.Keep : SittingLook.Finish;
        }

        /// <summary>The fewest minutes of a class of this length that count (StudyCatalogue.CountedShare of it, rounded up).</summary>
        public static int Needed(int length)
        {
            int l = length < 1 ? 1 : length;
            return (l * StudyCatalogue.CountedShare + 999) / 1000;
        }

        /// <summary>True if a class sat for these minutes of its length counts: at least StudyCatalogue.CountedShare of it.</summary>
        public static bool Counts(int minutes, int length)
        {
            return minutes >= Needed(length);
        }
    }
}
