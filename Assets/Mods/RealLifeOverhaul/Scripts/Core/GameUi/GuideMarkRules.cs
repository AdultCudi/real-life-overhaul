using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>What the watch of the mod's marks found of one mark (<see cref="GuideMarkRules.Fate"/>).</summary>
    public enum GuideMarkFate
    {
        Keep,           // on the guider as this mark, its owner at work and its time not up
        Lost,           // the guider is no longer this mark's (another feature or the game has it, or it was reset): only the claim goes
        OwnerGone,      // the component that keeps the mark was destroyed without taking it off
        OwnerStopped,   // ...or its updates were stopped by the guard (its faults, or its feature switched off)
        SceneOver,      // what it marks is over (the owner's own test says so), and nothing took the mark off
        TimeUp,         // the latest its owner could need it has passed: whatever the owner is doing, it is not ending the mark
        Unloaded        // the city is unloading or a save is being loaded: every mark of the mod goes
    }

    /// <summary>One mark of the mod's on the game's guider, as the book and the watch see it (GuideMark; a stand-in offline).</summary>
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

        /// <summary>Forgets the mark (it leaves the book), and first resets the guider if <paramref name="clear"/> and the guider is still this mark's.</summary>
        void Drop(GuideMarkFate why, bool clear);
    }

    /// <summary>
    /// When a mark of the mod's on the game's one guider of its kind (the JobDestination guider, which the game's own
    /// delivery jobs, the mod's jobs and a dozen of its features share) is still that mark's, and when a mark left up
    /// by an owner that is no longer looking after it is taken off. A player, 0.28.0: an accident's "Pedestrian down"
    /// stayed on screen long after the scene was over.
    ///
    /// Whose it is: the guider marks a place that is no building through one stand-in object of the game's, which every
    /// user of the guider moves, so that object alone says nothing. A mark is the guider's while the guider points at
    /// that object, no other mark of the mod's has put itself there since (the book's holder), and the guider's words
    /// are still the very words (the same string) this mark gave it. Anyone else who sets the guider gives it their
    /// own words, so a mark never takes off another's, even one put at the same place; and the stand-in having moved
    /// does not make a mark that is still shown someone else's. Only where the words cannot be read (a mark given no
    /// words, which the game then names itself, or a game without the field) does the stand-in's place decide.
    ///
    /// When it goes without its owner: its owner's component was destroyed, the guard stopped that component, what it
    /// marks is over by its owner's own test, or the latest time its owner said it could need it has passed. A mark
    /// that has lost the guider is only forgotten.
    ///
    /// Plain comparisons: no Unity or game types; the book allocates nothing once its list has grown to the marks up at
    /// once (a handful). Checked offline (EconomyCheck, GuideMarkChecks).
    /// </summary>
    public static class GuideMarkRules
    {
        public const float WatchEvery = 1f;    // seconds of real time between two looks of the watch: a mark that outlives its owner is off within a second, and nothing is looked at per frame
        public const float SceneGrace = 20f;   // seconds of game time past the latest a street scene can end (its hard limit) before its mark is taken off whatever its owner is doing

        /// <summary>
        /// True while the guider still shows the mark: it marks something (<paramref name="guiderMarks"/>), through the
        /// stand-in this mark set (<paramref name="sameTarget"/>), with no other mark of the mod's put there since
        /// (<paramref name="holder"/>), and its words this mark's own (<paramref name="sameWords"/>); where the words
        /// cannot be read (<paramref name="wordsKnown"/> false), the stand-in where this mark put it (<paramref name="samePlace"/>).
        /// </summary>
        public static bool Ours(bool guiderMarks, bool sameTarget, bool holder, bool wordsKnown, bool sameWords, bool samePlace)
        {
            return guiderMarks && sameTarget && holder && (wordsKnown ? sameWords : samePlace);
        }

        /// <summary>True once the latest time an owner gave (0: none) has come.</summary>
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
    /// The marks of the mod's that are up (or think they are), and which of them last put itself on the guider (the
    /// holder). A mark comes in when it takes the guider and leaves when it is taken off or forgotten. One look of
    /// the watch goes through them all; a city that unloads drops them all.
    /// </summary>
    public sealed class GuideMarkBook
    {
        private readonly List<IGuideMarkEntry> _live = new List<IGuideMarkEntry>(8);

        /// <summary>The mark that last put itself on the guider; null after it was taken off.</summary>
        public IGuideMarkEntry Holder { get; private set; }

        /// <summary>Marks up.</summary>
        public int Count => _live.Count;

        /// <summary>The mark has just put itself on the guider: it is up, and the holder.</summary>
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

        /// <summary>The mark is off (or forgotten): out of the book, and no longer the holder.</summary>
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

        /// <summary>True when this mark is the one that last put itself on the guider.</summary>
        public bool Holds(IGuideMarkEntry entry)
        {
            return entry != null && ReferenceEquals(Holder, entry);
        }

        /// <summary>
        /// One look of the watch at the time given (the game's scaled seconds): each mark whose owner is gone or
        /// stopped, whose scene is over, or whose time is up, is taken off (the guider reset if still that mark's); one that has lost the
        /// guider is forgotten. Returns the marks taken off (the forgotten ones not counted).
        /// </summary>
        public int Sweep(float now)
        {
            int off = 0;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (i >= _live.Count)
                {
                    continue;   // (a drop that let more than its own mark go)
                }
                IGuideMarkEntry entry = _live[i];
                GuideMarkFate fate = GuideMarkRules.Fate(entry.StillOurs(), entry.OwnerGone, entry.OwnerStopped, entry.Wanted, entry.Until, now);
                if (fate == GuideMarkFate.Keep)
                {
                    continue;
                }
                bool clear = fate != GuideMarkFate.Lost;
                entry.Drop(fate, clear);
                Let(entry);   // (in case the entry did not let itself go)
                off += clear ? 1 : 0;
            }
            return off;
        }

        /// <summary>Takes every mark off (the guider reset where it is still the mark's): the city is unloading or a save loading. Returns how many there were.</summary>
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
