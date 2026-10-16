using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What the game's computer at home offers beside its own Play a game and Pass the time (Careers/ComputerButtons):
    /// the class button for an online or distance class (<see cref="Class"/>), to attend now or greyed with when its next
    /// class is, and the homework button for a piece of homework that is open (<see cref="Homework"/>). Game-free.
    /// </summary>
    public struct ComputerOffer
    {
        public int Class;      // the course whose online class the class button is for; -1 for no class button
        public bool ClassNow;  // ...its class can be attended now (else the button is greyed with NextAt)
        public int NextAt;     // ...its next class, day * 1440 + its starting minute; -1 while it is on now
        public int Homework;   // the course whose homework the homework button is for; -1 for no homework button

        public bool Any => Class >= 0 || Homework >= 0;

        public static ComputerOffer None => new ComputerOffer { Class = -1, NextAt = -1, Homework = -1 };
    }

    /// <summary>How a class, a piece of homework or an exam at a desk or a computer ended, as <see cref="ClassTally.Settle"/> says it once.</summary>
    public enum TallyEnd
    {
        Settled = 0,     // it had been settled already: nothing more is counted
        CalledOff = 1,   // it ended before it began (the walk there called off): nothing is counted, nothing is said
        Counted = 2      // it had begun: its minutes are counted, once
    }

    /// <summary>
    /// The minutes of one class, piece of homework or exam at a desk or a computer, from the moment the player sits down
    /// until it is over, counted to the enrolment once whichever way it ends: the end of the time, Stop, fast forward to
    /// the end, the game's skip time, energy running out, the game's panel letting it go (a phone call, a click
    /// elsewhere, the building left), the city unloading. Game-free; a few integer sums a minute of the game's clock.
    /// </summary>
    public sealed class ClassTally
    {
        public readonly int Length;   // game minutes the sitting is for
        public int Done { get; private set; }
        public bool Begun { get; private set; }
        public bool Over { get; private set; }

        public ClassTally(int length)
        {
            Length = Math.Max(1, length);
        }

        /// <summary>The player has sat down: from now on the minutes count.</summary>
        public void Begin()
        {
            if (!Over)
            {
                Begun = true;
            }
        }

        /// <summary>Minutes of the game's clock passed while sitting. True once the whole length has passed.</summary>
        public bool Add(int minutes)
        {
            if (!Begun || Over || minutes <= 0)
            {
                return Begun && !Over && Done >= Length;
            }
            Done = Math.Min(Length, Done + minutes);
            return Done >= Length;
        }

        /// <summary>The minutes still to go, for the game's panel and its skip time.</summary>
        public int Left => Math.Max(0, Length - Done);

        /// <summary>The end, said once: how it ended, the minutes to count and whether the whole was sat.</summary>
        public TallyEnd Settle(out int minutes, out bool whole)
        {
            minutes = 0;
            whole = false;
            if (Over)
            {
                return TallyEnd.Settled;
            }
            Over = true;
            if (!Begun)
            {
                return TallyEnd.CalledOff;
            }
            minutes = Done;
            whole = Done >= Length;
            return TallyEnd.Counted;
        }
    }

    public static class ClassPlayRules
    {
        /// <summary>True for a course whose classes are held online, from home: an online course, or one followed at a distance.</summary>
        public static bool HeldOnline(CourseDef c, StudyMode mode)
        {
            return c != null && (mode == StudyMode.Distance || c.Campus == Campus.Online);
        }

        /// <summary>
        /// True if the class of an enrolment can be attended at this minute (wherever it is held), with the minutes it
        /// still runs: the course at its classes, no fee owed, today's class not yet counted, inside its window
        /// (<see cref="StudyRules.Joinable"/>). The one rule the Campus app, the desks and the computer read.
        /// </summary>
        public static bool ClassNow(Enrolment e, int day, int minuteOfDay, out int minutes)
        {
            minutes = 0;
            CourseDef c = e != null ? e.Def : null;
            if (c == null || e.Stage != (int)EnrolStage.Studying || !c.Timetabled || e.FeeLate || e.SettledDay >= day)
            {
                return false;
            }
            return StudyRules.Joinable(c, (StudyMode)e.Mode, day, minuteOfDay, out minutes);
        }

        /// <summary>
        /// What the computer at home offers now: the first online or distance class that can be attended now, else the
        /// one whose next class comes soonest (greyed); and the first course with homework open. In the order of the
        /// courses, so the same on every look. <paramref name="sitting"/>: the course whose class is being sat, or -1.
        /// </summary>
        public static ComputerOffer AtComputer(IList<Enrolment> courses, int day, int minuteOfDay, int sitting)
        {
            ComputerOffer offer = ComputerOffer.None;
            int nextCourse = -1, nextAt = int.MaxValue;
            for (int i = 0; courses != null && i < courses.Count; i++)
            {
                Enrolment e = courses[i];
                CourseDef c = e != null ? e.Def : null;
                if (c == null)
                {
                    continue;
                }
                if (offer.Homework < 0 && StudyRules.HomeworkOpen(e))
                {
                    offer.Homework = c.Id;
                }
                if (offer.ClassNow || !HeldOnline(c, (StudyMode)e.Mode))
                {
                    continue;
                }
                if (sitting != c.Id && ClassNow(e, day, minuteOfDay, out _))
                {
                    offer.Class = c.Id;
                    offer.ClassNow = true;
                    offer.NextAt = -1;
                    continue;
                }
                int at = StudyRules.NextClass(e, day, minuteOfDay, sitting);
                if (at >= 0 && at < nextAt)
                {
                    nextAt = at;
                    nextCourse = c.Id;
                }
            }
            if (!offer.ClassNow && nextCourse >= 0)
            {
                offer.Class = nextCourse;
                offer.NextAt = nextAt;
            }
            return offer;
        }
    }
}
