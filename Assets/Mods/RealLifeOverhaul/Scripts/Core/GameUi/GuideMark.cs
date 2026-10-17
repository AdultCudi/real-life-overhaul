using System;
using System.Reflection;
using BAModAPI;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A mark on a place that is no building, shown the way the game marks a job's destination: its own direction
    /// guider (UI.Guiders.DirectionGuider, the one of type JobDestination), which draws the marker over the place,
    /// the arrow at the edge of the screen while the place is out of view, the distance and the name, and puts the
    /// place on the city map. Nothing is drawn by the mod.
    ///
    /// The game has one guider of that type and the mod's jobs and outings use it too. A mark therefore takes it
    /// only while it is free (or already this mark's), and takes off only what it put there: whoever was first
    /// keeps the guider, except that a course (<see cref="Ranked"/>: a driving test's checkpoints, a run's) takes it from
    /// a mark of the mod's that is not one, which has it back once the course lets it go. Whose the guider is follows <see cref="GuideMarkRules.Ours"/>: the guider points at the
    /// stand-in this mark set, no other mark has put itself there since, and the guider's words are still the very
    /// string this mark gave it (its place, where no words can be read). <see cref="Hide"/> clears the guider whenever
    /// that holds, whatever this mark believed, and never when it does not.
    ///
    /// A mark never outlives its owner: an owner may say which component keeps it (<see cref="Keep"/>), how to tell
    /// that what it marks goes on (<see cref="While"/>) and the latest time it can need it (<see cref="Until"/>), and
    /// the watch (<see cref="GuideMarkWatch"/>) takes off, once a second, a mark whose keeper was destroyed or stopped
    /// by the guard, whose scene is over, or whose time is up. Every mark goes when the city unloads
    /// (<see cref="HideAll"/>).
    ///
    /// The cost: nothing per frame of the mod's. <see cref="Show"/> does work only when the place or the name has
    /// changed (asking again costs a few compares and one read of the guider's words, a field of the game's read by
    /// reflection, nothing allocated); the game moves its own marker.
    /// </summary>
    public sealed class GuideMark : IGuideMarkEntry
    {
        public static IModLogger Logger;

        private static readonly GuideMarkBook Book = new GuideMarkBook();   // the marks up, and which one last took the guider
        private static readonly FieldInfo WordsField = GameContract.PoiText.Field;   // PointOfInterest._text: the guider's words (null: the place alone says whose it is)

        private readonly string _owner;   // what the mark is for, in English, for Player.log
        private Transform _target;     // the guider's own target object while the mark is up: how the mark knows the guider is still its
        private Vector3 _at;
        private string _name;
        private string _words;         // the very string given to the guider (null when none was: the game writes its own then)
        private bool _listed;          // in the book
        private Behaviour _keeper;     // the component whose life keeps the mark (Keep), or none
        private bool _kept;
        private FeatureGuard _keeperGuard;
        private Func<bool> _while;     // the owner's own test that what the mark is for goes on (While), or none
        private GuideMarkRank _rank;   // how much the owner needs the guider (Ranked): a course takes it from a mark that is not one

        public GuideMark() : this(null)
        {
        }

        /// <param name="owner">What the mark is for, in English, for Player.log: "traffic accidents".</param>
        public GuideMark(string owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// The latest time (Time.time, the game's scaled seconds) the owner can need the mark; 0 (the default) for no
        /// limit. Past it the mark is not shown, and the watch takes it off whatever its owner is doing.
        /// </summary>
        public float Until { get; set; }

        /// <summary>The mod's marks up at the moment.</summary>
        public static int Up => Book.Count;

        /// <summary>
        /// The component that looks after the mark: when it is destroyed without taking the mark off, or the guard stops
        /// it (its faults, or its feature switched off), the watch takes the mark off. Called once (Awake); returns this.
        /// </summary>
        public GuideMark Keep(Behaviour keeper)
        {
            _keeper = keeper;
            _kept = !ReferenceEquals(keeper, null);
            _keeperGuard = _kept ? Guard.OfOwner(keeper.GetType()) : null;
            return this;
        }

        /// <summary>
        /// The owner's test that what the mark is for goes on (a scene still running), asked by the watch once a second
        /// while the mark is up: when it says no, the mark goes. Made once and kept (it is never made per call); returns this.
        /// </summary>
        public GuideMark While(Func<bool> wanted)
        {
            _while = wanted;
            return this;
        }

        /// <summary>
        /// How much the owner needs the guider (<see cref="GuideMarkRules.MayTake"/>): a course, checkpoints the player
        /// follows against the clock, takes it from a mark of the mod's that is not one. Called once; returns this.
        /// </summary>
        public GuideMark Ranked(GuideMarkRank rank)
        {
            _rank = rank;
            return this;
        }

        /// <summary>True while this mark is the one on the game's guider.</summary>
        public bool Showing
        {
            get
            {
                if (ReferenceEquals(_target, null))
                {
                    return false;
                }
                try
                {
                    return Mine(Guider());
                }
                catch (Exception)
                {
                    return false;   // (the interface is going: nothing of it is this mark's any more)
                }
            }
        }

        /// <summary>
        /// Puts the mark on a place with a name (final words). False if the game's guider is not there or is marking
        /// something else (a course takes it from a mark of the mod's that is not one), or the mark's time is up; it can
        /// be asked again later. Asking again with the same place and name costs a few compares.
        /// </summary>
        public bool Show(Vector3 at, string name)
        {
            DirectionGuider guider = null;
            bool asked = false;
            try
            {
                if (GuideMarkRules.TimeUp(Until, Time.time))
                {
                    if (!ReferenceEquals(_target, null))
                    {
                        Hide();
                    }
                    return false;
                }
                guider = Guider();
                if (guider == null)
                {
                    return false;
                }
                bool mine = Mine(guider);
                if (mine && ReferenceEquals(name, _name) && (at - _at).sqrMagnitude < 0.01f)
                {
                    return true;
                }
                GuideMark gaveWay = null;
                if (!mine)
                {
                    bool marks = guider.target != null;
                    if (!Book.MayTake(this, marks))
                    {
                        return false;   // a job, an outing or another mark has it
                    }
                    gaveWay = marks ? Book.Holder as GuideMark : null;   // a mark of the mod's that gives way to this course
                }
                GuidersManager manager = InstanceBehavior<GuidersManager>.Instance;
                DirectionGuider look = manager.destinationGuider != null ? manager.destinationGuider : guider;
                string words = name ?? string.Empty;
                _at = at;
                _name = name;
                _words = words.Length > 0 ? words : null;
                asked = true;
                GuidersManager.SetGuiderTarget(at, words, look.poiICon, GuidersManager.GetGuiderColor(DirectionGuiderType.JobDestination), DirectionGuiderType.JobDestination);
                _target = guider.target;
                if (ReferenceEquals(_target, null))
                {
                    Release();
                    return false;
                }
                Took();
                if (gaveWay != null && Meta.Dev.Verbose)
                {
                    Logger?.Info("Guide mark check (a course over a mark): '" + words + "' of " + (_owner ?? "a feature") + " took the game's destination marker from '"
                                 + (gaveWay._name ?? string.Empty) + "' of " + (gaveWay._owner ?? "a feature") + ", which has it back once the course lets it go: PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                // The game may have set the guider before its call failed: kept as this mark's, so that Hide and the
                // watch still take it off (they reset it only if it shows this mark).
                if (asked && guider != null && !ReferenceEquals(guider.target, null))
                {
                    _target = guider.target;
                    Took();
                }
                else
                {
                    Release();
                }
                return false;
            }
        }

        /// <summary>
        /// Takes the mark off if the guider still shows it, whatever this mark believed (another feature may have had the
        /// guider meanwhile); never what another put there. Safe at any time, and when the interface has gone.
        /// </summary>
        public void Hide()
        {
            if (ReferenceEquals(_target, null) && !_listed)
            {
                return;
            }
            ResetIfMine();
            Release();
        }

        /// <summary>Takes every mark of the mod's off (the guider reset where it still shows one): the city is unloading, or another is loading.</summary>
        public static void HideAll(string why)
        {
            int all = Book.DropAll();
            if (all > 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Guide mark check (" + why + "): " + all + " mark(s) of the mod's taken off, none left: PASS.");
            }
        }

        /// <summary>One look of the watch (<see cref="GuideMarkWatch"/>, once a second): marks whose owner is gone or stopped, or whose time is up, go.</summary>
        public static void Sweep()
        {
            if (Book.Count > 0)
            {
                Book.Sweep(Time.time);
            }
        }

        bool IGuideMarkEntry.StillOurs()
        {
            try
            {
                return Mine(Guider());
            }
            catch (Exception)
            {
                return false;
            }
        }

        GuideMarkRank IGuideMarkEntry.Rank => _rank;

        bool IGuideMarkEntry.OwnerGone => _kept && _keeper == null;

        bool IGuideMarkEntry.OwnerStopped => _keeperGuard != null && _keeperGuard.Off;

        bool IGuideMarkEntry.Wanted
        {
            get
            {
                try
                {
                    return _while == null || _while();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    return false;   // (a test that cannot be answered cannot keep the marker up)
                }
            }
        }

        void IGuideMarkEntry.Drop(GuideMarkFate why, bool clear)
        {
            string words = _name;
            bool cleared = clear && ResetIfMine();
            Release();
            if (!cleared || why == GuideMarkFate.Unloaded)
            {
                return;
            }
            string line = "'" + (words ?? string.Empty) + "' of " + (_owner ?? "a feature") + " was still on the game's destination marker ";
            if (why == GuideMarkFate.SceneOver)
            {
                // The owner's own test ended it, as it was meant to: a self-check line.
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Guide mark check (scene over): " + line + "after what it marked was over; taken off by the watch: PASS.");
                }
                return;
            }
            // Its owner should have taken it off: said once for each mark that had to be, never per frame.
            Logger?.Warn("Guide mark: " + line + (why == GuideMarkFate.OwnerGone ? "after its owner had gone" : why == GuideMarkFate.OwnerStopped ? "after its owner had stopped"
                         : "past the latest its owner could need it") + "; taken off.");
        }

        // Whether the guider shows this mark (GuideMarkRules.Ours).
        private bool Mine(DirectionGuider guider)
        {
            if (ReferenceEquals(_target, null) || guider == null)
            {
                return false;
            }
            Transform target = guider.target;
            bool marks = target != null;
            bool same = marks && ReferenceEquals(target, _target);
            bool known = same && _words != null && WordsField != null && guider.poi != null;
            bool words = known && ReferenceEquals(WordsField.GetValue(guider.poi), _words);
            bool place = same && !known && (target.position - _at).sqrMagnitude < 0.01f;
            return GuideMarkRules.Ours(marks, same, Book.Holds(this), known, words, place);
        }

        // Resets the guider if it shows this mark. True if it did.
        private bool ResetIfMine()
        {
            if (ReferenceEquals(_target, null))
            {
                return false;
            }
            try
            {
                if (Mine(Guider()))
                {
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return false;
        }

        private void Took()
        {
            Book.Took(this);
            _listed = true;
        }

        private void Release()
        {
            _target = null;
            _name = null;
            _words = null;
            if (_listed)
            {
                _listed = false;
                Book.Let(this);
            }
        }

        private static DirectionGuider Guider()
        {
            GuidersManager manager = InstanceBehavior<GuidersManager>.Instance;
            return manager != null ? manager.jobDestinationGuider : null;
        }
    }
}
