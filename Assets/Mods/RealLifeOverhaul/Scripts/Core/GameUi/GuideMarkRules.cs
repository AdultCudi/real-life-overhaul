using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>What the watch of the mod's marks found of one mark (<see cref="GuideMarkRules.Fate"/>).</summary>
    public enum GuideMarkFate
    {
        Keep,           // on the guider as this mark, its owner at work, its time not up
        Lost,           // the guider is no longer this mark's: only the claim goes
        OwnerGone,      // the component that keeps the mark was destroyed without taking it off
        OwnerStopped,   // ...or the guard stopped its updates (its faults, or its feature off)
        SceneOver,      // what it marks is over by the owner's own test, and nothing took the mark off
        TimeUp,         // the latest its owner could need it has passed
        Unloaded        // the city is unloading or a save loading: every mark goes
    }

    /// <summary>
    /// How much a mark needs the guider (<see cref="GuideMarkRules.MayTake"/>). Most marks point at somewhere the player
    /// may go and wait while another has the guider. A course (a driving lesson or test, a run) is followed checkpoint by
    /// checkpoint against the clock, so it takes the guider from any mark that is not a course, which has it back after.
    /// </summary>
    public enum GuideMarkRank
    {
        Point,    // waits while another mark has the guider: the default
        Course    // takes the guider from a mark that is not a course
    }

    /// <summary>One mark on the game's guider as the book and the watch see it (GuideMark; a stand-in offline).</summary>
    public interface IGuideMarkEntry
    {
        /// <summary>The guider still shows this mark (<see cref="GuideMarkRules.Ours"/>).</summary>
        bool StillOurs();

        /// <summary>The component that keeps the mark has been destroyed.</summary>
        bool OwnerGone { get; }

        /// <summary>The guard of that component is off: it no longer runs.</summary>
        bool OwnerStopped { get; }

        /// <summary>What the mark is for still goes on: the owner's own test, true when it gave none.</summary>
        bool Wanted { get; }

        /// <summary>The latest time (the game's scaled seconds) the owner can need the mark; 0 for no limit.</summary>
        float Until { get; }

        /// <summary>Whether the mark takes the guider from another (<see cref="GuideMarkRules.MayTake"/>).</summary>
        GuideMarkRank Rank { get; }

        /// <summary>Forgets the mark, first resetting the guider if <paramref name="clear"/> and it is still this mark's.</summary>
        void Drop(GuideMarkFate why, bool clear);
    }

    /// <summary>
    /// When a mark on the game's one JobDestination guider (shared by the game's delivery jobs, the mod's jobs and a dozen
    /// features) is still that mark's, and when a mark left up by an owner no longer looking after it is taken off.
    ///
    /// Whose it is: the guider marks a place through one stand-in object that every user moves, so the object says nothing.
    /// A mark has the guider while it points at that object, no other mark took it since (the book's holder), and its
    /// words are the very string this mark gave; anyone else gives their own words, so a mark never takes off another's at
    /// the same place. Only where the words cannot be read (none given, or a game without the field) does the place decide.
    ///
    /// Who has it when two want it: the first keeps it, but a course takes it from a mark that is not one
    /// (<see cref="MayTake"/>), so a scene nearby never hides the checkpoint; the other asks again and has it back after.
    ///
    /// It goes without its owner when the owner's component was destroyed or stopped by the guard, the owner's test says
    /// it is over, or its latest time has passed; a mark that lost the guider is only forgotten. Game-free, checked offline
    /// (EconomyCheck GuideMarkChecks); the book allocates nothing once its list has grown to the marks up at once.
    /// </summary>
    public static class GuideMarkRules
    {
        public const float WatchEvery = 1f;    // seconds of real time between two looks of the watch: a mark outliving its owner is off within a second
        public const float SceneGrace = 20f;   // seconds of game time past a street scene's hard limit before its mark is taken off regardless

        /// <summary>
        /// The guider still shows the mark: it marks something, through this mark's stand-in, no other mark put there since,
        /// and its words are this mark's; where the words cannot be read (<paramref name="wordsKnown"/> false), the stand-in
        /// is where this mark put it.
        /// </summary>
        public static bool Ours(bool guiderMarks, bool sameTarget, bool holder, bool wordsKnown, bool sameWords, bool samePlace)
        {
            return guiderMarks && sameTarget && holder && (wordsKnown ? sameWords : samePlace);
        }

        /// <summary>
        /// A mark without the guider may take it: the guider marks nothing, or another mark of the mod's still has it and this
        /// one is a course while the holder is not. A job's or the game's own marker is never taken; of two of the same rank
        /// the first keeps it.
        /// </summary>
        public static bool MayTake(bool guiderMarks, bool heldByMark, GuideMarkRank holder, GuideMarkRank rank)
        {
            if (!guiderMarks)
            {
                return true;
            }
            return heldByMark && rank == GuideMarkRank.Course && holder != GuideMarkRank.Course;
        }

        /// <summary>The latest time an owner gave (0: none) has come.</summary>
        public static bool TimeUp(float until, float now)
        {
            return until > 0f && now >= until;
        }

        /// <summary>What becomes of one mark at a look of the watch.</summary>
        public static GuideMarkFate Fate(bool ours, bool ownerGone, bool ownerStopped, bool wanted, float until, float now)
        {
            if (!ours)
            {
                return GuideMarkFate.Lost;
            }
            if (ownerGone)
            {
                return GuideMarkFate.OwnerGone;
            }
            if (ownerStopped)
            {
                return GuideMarkFate.OwnerStopped;
            }
            if (!wanted)
            {
                return GuideMarkFate.SceneOver;
            }
            return TimeUp(until, now) ? GuideMarkFate.TimeUp : GuideMarkFate.Keep;
        }
    }

    /// <summary>
    /// The marks up (or that think they are), and the holder: the one that last put itself on the guider. A mark comes in
    /// when it takes the guider and leaves when taken off or forgotten.
    /// </summary>
    public sealed class GuideMarkBook
    {
        private readonly List<IGuideMarkEntry> _live = new List<IGuideMarkEntry>(8);

        /// <summary>The mark that last put itself on the guider; null after it was taken off.</summary>
        public IGuideMarkEntry Holder { get; private set; }

        public int Count => _live.Count;

        /// <summary>The mark just put itself on the guider: it is up, and the holder.</summary>
        public void Took(IGuideMarkEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            if (!_live.Contains(entry))
            {
                _live.Add(entry);
            }
            Holder = entry;
        }

        /// <summary>The mark is off or forgotten: out of the book, no longer the holder.</summary>
        public void Let(IGuideMarkEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            _live.Remove(entry);
            if (ReferenceEquals(Holder, entry))
            {
                Holder = null;
            }
        }

        /// <summary>This mark last put itself on the guider.</summary>
        public bool Holds(IGuideMarkEntry entry)
        {
            return entry != null && ReferenceEquals(Holder, entry);
        }

        /// <summary>
        /// <paramref name="entry"/>, without the guider, may take it now (<see cref="GuideMarkRules.MayTake"/>): it is free, or
        /// the holder, still on it, gives way to a course. A few compares and one look at the holder.
        /// </summary>
        public bool MayTake(IGuideMarkEntry entry, bool guiderMarks)
        {
            if (!guiderMarks)
            {
                return true;
            }
            IGuideMarkEntry holder = Holder;
            bool held = entry != null && holder != null && !ReferenceEquals(holder, entry) && holder.StillOurs();
            return entry != null && GuideMarkRules.MayTake(true, held, held ? holder.Rank : GuideMarkRank.Point, entry.Rank);
        }

        /// <summary>
        /// One look of the watch at <paramref name="now"/> (scaled seconds): each mark whose owner is gone or stopped, whose
        /// scene is over or time up, is taken off (the guider reset if still its); one that lost the guider is forgotten.
        /// Returns the marks taken off.
        /// </summary>
        public int Sweep(float now)
        {
            int off = 0;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (i >= _live.Count)
                {
                    continue;   // a drop let more than its own mark go
                }
                IGuideMarkEntry entry = _live[i];
                GuideMarkFate fate = GuideMarkRules.Fate(entry.StillOurs(), entry.OwnerGone, entry.OwnerStopped, entry.Wanted, entry.Until, now);
                if (fate == GuideMarkFate.Keep)
                {
                    continue;
                }
                bool clear = fate != GuideMarkFate.Lost;
                entry.Drop(fate, clear);
                Let(entry);   // in case the entry did not let itself go
                off += clear ? 1 : 0;
            }
            return off;
        }

        /// <summary>Takes every mark off, the guider reset where still a mark's: the city unloading or a save loading. Returns how many.</summary>
        public int DropAll()
        {
            int all = _live.Count;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (i >= _live.Count)
                {
                    continue;
                }
                IGuideMarkEntry entry = _live[i];
                entry.Drop(GuideMarkFate.Unloaded, true);
                Let(entry);
            }
            _live.Clear();
            Holder = null;
            return all;
        }
    }
}
