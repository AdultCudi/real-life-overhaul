using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What the computer at home offers beside Pass the time: the class button (<see cref="Class"/>, now or greyed) and the homework button.</summary>
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

    /// <summary>The minutes of one class, homework or exam at a desk or computer from the moment the player sits, counted once however it ends. Game-free.</summary>
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

    /// <summary>How a study desk of the school, or a classroom door of the university, comes to the game's activity panel (<see cref="ClassPlayRules.WayToDesk"/>).</summary>
    public enum DeskWay
    {
        GameStudy = 0,   // the game's own study panel (the door's StartLearning), given the class by the mod's desk as it opens
        OwnPanel = 1     // the desk's own activity of the mod's, which is not the game's StudyActivity, on the game's panel
    }

    /// <summary>Game-free rules for classes played as the game's own studying: which button a computer shows, and where a class is held.</summary>
    public static class ClassPlayRules
    {
        /// <summary>The one rule for how a desk's panel is opened: the game's study panel alone; in a Going Public session the desk's own, as Going Public takes every StudyActivity.</summary>
        public static DeskWay WayToDesk(bool inSession)
        {
            return inSession ? DeskWay.OwnPanel : DeskWay.GameStudy;
        }

        /// <summary>True if a click on a university classroom door (room from 1) is the mod's: in a Going Public session only.</summary>
        public static bool TakesRoomClick(bool inSession, int room)
        {
            return inSession && room > 0;
        }

        /// <summary>True if the school's classes are offered by the panel where the player stands: in a Going Public session, where its desks' clicks are Going Public's.</summary>
        public static bool SchoolInPlace(bool inSession)
        {
            return inSession;
        }

        /// <summary>Whether the panel where the player stands offers a course's class and exam: not where a school desk, the home computer or a classroom offers them.</summary>
        public static void InPlace(bool classAtSchool, bool online, bool examAtSchool, bool inRooms, bool inSession, out bool classHere, out bool examHere)
        {
            bool desks = !SchoolInPlace(inSession);
            classHere = !inRooms && !online && !(classAtSchool && desks);
            examHere = !inRooms && !(examAtSchool && desks);
        }

        /// <summary>True for a course whose classes are held online, from home: an online course, or one followed at a distance.</summary>
        public static bool HeldOnline(CourseDef c, StudyMode mode)
        {
            return c != null && (mode == StudyMode.Distance || c.Campus == Campus.Online);
        }

        /// <summary>True if the enrolment's class can be attended at this minute, with the minutes it still runs: the one rule the Campus app, desks and computer read.</summary>
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

        /// <summary>What the home computer offers now: the first online class open now, else the soonest (greyed), and the first homework open; the same on every look.</summary>
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
