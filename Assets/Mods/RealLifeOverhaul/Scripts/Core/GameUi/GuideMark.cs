using System;
using System.Reflection;
using BAModAPI;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A mark on a place that is no building, as the game marks a job's destination: its own direction guider (the one of
    /// type JobDestination), which draws the marker, the arrow at the screen's edge, the distance and the name, and puts the
    /// place on the city map. Nothing is drawn by the mod.
    ///
    /// The game has one such guider and the mod's jobs and outings use it too, so a mark takes it only while it is free or
    /// its own, and takes off only what it put there; a course (<see cref="Ranked"/>: a driving test's checkpoints, a run's)
    /// takes it from a mark that is not one, which has it back after. Whose the guider is follows
    /// <see cref="GuideMarkRules.Ours"/>: it points at this mark's stand-in, no other mark took it since, and its words are
    /// the very string this mark gave. <see cref="Hide"/> clears it whenever that holds, and never when it does not.
    ///
    /// A mark never outlives its owner: <see cref="Keep"/>, <see cref="While"/> and <see cref="Until"/> say what keeps it,
    /// and the watch (<see cref="GuideMarkWatch"/>) takes one off once a second when its keeper is gone or stopped, its scene
    /// is over or its time is up; all go when the city unloads (<see cref="HideAll"/>).
    ///
    /// Costs nothing a frame of the mod's: <see cref="Show"/> works only when the place or name changed (otherwise a few
    /// compares and one reflected read of the guider's words); the game moves its own marker.
    /// </summary>
    public sealed class GuideMark : IGuideMarkEntry
    {
        public static IModLogger Logger;

        private static readonly GuideMarkBook Book = new GuideMarkBook();   // the marks up, and which one last took the guider
        private static readonly FieldInfo WordsField = GameContract.PoiText.Field;   // PointOfInterest._text, the guider's words; null: the place alone says whose it is

        private readonly string _owner;   // what the mark is for, in English, for Player.log
        private Transform _target;     // the guider's target object while the mark is up: how it knows the guider is still its
        private Vector3 _at;
        private string _name;
        private string _words;         // the very string given to the guider; null when none was (the game writes its own)
        private bool _listed;          // in the book
        private Behaviour _keeper;     // the component whose life keeps the mark (Keep), or none
        private bool _kept;
        private FeatureGuard _keeperGuard;
        private Func<bool> _while;     // the owner's test that what the mark is for goes on (While), or none
        private GuideMarkRank _rank;   // a course takes the guider from a mark that is not one

        public GuideMark() : this(null)
        {
        }

        /// <param name="owner">What the mark is for, in English, for Player.log: "traffic accidents".</param>
        public GuideMark(string owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// The latest Time.time (the game's scaled seconds) the owner can need the mark; 0 for no limit. Past it the mark is not
        /// shown, and the watch takes it off whatever its owner does.
        /// </summary>
        public float Until { get; set; }

        /// <summary>The mod's marks up at the moment.</summary>
        public static int Up => Book.Count;

        /// <summary>
        /// The component that looks after the mark: destroyed without taking it off, or stopped by the guard, and the watch
        /// takes the mark off. Called once (Awake); returns this.
        /// </summary>
        public GuideMark Keep(Behaviour keeper)
        {
            _keeper = keeper;
            _kept = !ReferenceEquals(keeper, null);
            _keeperGuard = _kept ? Guard.OfOwner(keeper.GetType()) : null;
            return this;
        }

        /// <summary>
        /// The owner's test that what the mark is for goes on, asked by the watch once a second while it is up; no takes the
        /// mark off. Make it once and keep it; returns this.
        /// </summary>
        public GuideMark While(Func<bool> wanted)
        {
            _while = wanted;
            return this;
        }

        /// <summary>How much the owner needs the guider (<see cref="GuideMarkRules.MayTake"/>). Called once; returns this.</summary>
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
                    return false;   // the interface is going: nothing of it is this mark's
                }
            }
        }

        /// <summary>
        /// Puts the mark on a place with a name in final words. False if the guider is not there or marks something else, or
        /// the mark's time is up; ask again later. The same place and name again cost a few compares.
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
                    gaveWay = marks ? Book.Holder as GuideMark : null;   // a mark of the mod's giving way to this course
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
                // The game may have set the guider before its call failed: kept as this mark's, so Hide and the watch still take it
                // off (they reset it only if it shows this mark).
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
        /// Takes the mark off if the guider still shows it, whatever this mark believed; never what another put there. Safe at
        /// any time, and after the interface has gone.
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

        /// <summary>Every mark of the mod's taken off, the guider reset where it still shows one: the city is unloading, or another loading.</summary>
        public static void HideAll(string why)
        {
            int all = Book.DropAll();
            if (all > 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Guide mark check (" + why + "): " + all + " mark(s) of the mod's taken off, none left: PASS.");
            }
        }

        /// <summary>The watch's look, once a second: marks whose owner is gone or stopped, or whose time is up, go.</summary>
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
                    return false;   // a test that cannot be answered cannot keep the marker up
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
                // The owner's test ended it, as meant: a self-check line.
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Guide mark check (scene over): " + line + "after what it marked was over; taken off by the watch: PASS.");
                }
                return;
            }
            // Its owner should have taken it off: said once for each such mark.
            Logger?.Warn("Guide mark: " + line + (why == GuideMarkFate.OwnerGone ? "after its owner had gone" : why == GuideMarkFate.OwnerStopped ? "after its owner had stopped"
                         : "past the latest its owner could need it") + "; taken off.");
        }

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

        // True if it did.
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
