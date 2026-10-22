using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one way the mod has the game draw again a player's business's customers still to come today, and the one
    /// place it reads that list. While Meaningful Ambience (another mod, which adds customers as the hour turns) is
    /// loaded, the hour under way is kept as it was. A redraw is asked for (<see cref="Rewritten"/>, <see cref="Redraw"/>)
    /// and waits (Core/RedrawRules): once a business, with its last figure, and not at all if the list stands drawn with
    /// it. <see cref="CustomerDrawPace"/> draws the waiting ones about a millisecond a frame, or at once where spreading
    /// is not safe or the list is needed (<see cref="Entries"/>, a save, the shop the player is in); never in a later
    /// hour than asked. The lists are not saved and each Going Public machine draws only its own businesses.
    /// COST: nothing per frame while nothing waits; an asking is a dictionary look-up.
    /// </summary>
    public static class CustomerDraw
    {
        public const int NoFigure = int.MinValue;   // a business with no promotion figure: nothing to compare, drawn whenever asked

        public static IModLogger Logger;

        /// <summary>Raised after each redraw by the mod: whatever was booked on the old customers is gone.</summary>
        public static event Action<BuildingRegistration> Redrawn;

        private struct Round
        {
            public string Why;
            public int Asked, Kept, Drawn, Late, Frames;
            public double Ms, Worst;
        }

        private static readonly RedrawQueue<BuildingRegistration> Queue = new RedrawQueue<BuildingRegistration>();
        private static readonly Dictionary<BuildingRegistration, int> WriteFrames = new Dictionary<BuildingRegistration, int>(64);   // frame of each figure's last write
        private static FieldInfo _table;
        private static Dictionary<Address, List<CustomerEntry>> _tableValue;   // once read: its field is readonly
        private static bool _looked;
        private static readonly List<CustomerEntry> Kept = new List<CustomerEntry>(32);
        private static readonly HashSet<CustomerEntry> KeptSet = new HashSet<CustomerEntry>();
        private static CustomerDrawPace _pace;
        private static int _held;          // batches of writes under way (Hold)
        private static int _morningDay = -1;
        private static Round _round;
        private static double _clock;      // game minutes, at the pace's last frame
        private static float _clockStep;   // seconds of game time that frame
        private static int _clockFrame = -1;
        private static float _seen;        // the fastest game minutes a second seen this round
        private static float _speed = 1f;  // game minutes a second by the game's setting, read as a round starts
        private static Action _onSave, _onSkip, _skipSeen;
        private static readonly double TicksPerMs = Stopwatch.Frequency / 1000.0;

        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>The game's list of a business's customers today, a waiting redraw made first; null if it keeps none.</summary>
        public static List<CustomerEntry> Entries(BuildingRegistration reg)
        {
            if (reg == null || reg.Address == null)
            {
                return null;
            }
            if (Queue.Waiting > 0 && Queue.Waits(reg))
            {
                DrawNow(reg);
            }
            return List(reg);
        }

        /// <summary>True while a business waits for a redraw: what is to go on the new customers waits with it.</summary>
        public static bool Waits(BuildingRegistration reg)
        {
            return reg != null && Queue.Waiting > 0 && Queue.Waits(reg);
        }

        /// <summary>How a list stands now, taken before the mod adds to it (<see cref="Added"/>).</summary>
        public static DrawPrint Print(List<CustomerEntry> list)
        {
            return list == null ? default(DrawPrint) : DrawPrint.Of(list, list.Count, list.Count > 0 ? list[list.Count - 1] : null);
        }

        /// <summary>
        /// The mod has added customers of its own to a list: one that stood as last drawn still does, so a later asking
        /// with the same figure is not drawn again.
        /// </summary>
        public static void Added(BuildingRegistration reg, DrawPrint before)
        {
            if (reg != null)
            {
                Queue.Added(reg, before, Print(List(reg)));
            }
        }

        private static List<CustomerEntry> List(BuildingRegistration reg)
        {
            Dictionary<Address, List<CustomerEntry>> table = reg != null && reg.Address != null ? Table() : null;
            List<CustomerEntry> list;
            return table != null && table.TryGetValue(reg.Address, out list) ? list : null;
        }

        private static Dictionary<Address, List<CustomerEntry>> Table()
        {
            if (_tableValue != null)
            {
                return _tableValue;
            }
            if (!_looked)
            {
                _looked = true;
                try
                {
                    _table = GameContract.CustomerTable.Field;
                    if (_table == null || _table.FieldType != typeof(Dictionary<Address, List<CustomerEntry>>))
                    {
                        _table = null;
                        Logger?.Warn("Customers: the game's list of a day's customers is not where it was; "
                                     + "the hour under way is not kept on a redraw, the basket adds nothing, and every redraw asked for is made.");
                    }
                }
                catch (Exception e)
                {
                    _table = null;
                    Logger?.Error(e);
                }
            }
            Dictionary<Address, List<CustomerEntry>> table = _table != null ? _table.GetValue(null) as Dictionary<Address, List<CustomerEntry>> : null;
            if (table != null && _table.IsInitOnly)
            {
                _tableValue = table;
            }
            return table;
        }

        /// <summary>Asks for a redraw whatever the figure (opening hours or a rota changed).</summary>
        public static void Redraw(BuildingRegistration reg)
        {
            Ask(reg, true);
        }

        /// <summary>A footfall figure was just written: asks for a redraw unless the list stands drawn with it already.</summary>
        public static void Rewritten(BuildingRegistration reg)
        {
            Ask(reg, false);
        }

        /// <summary>A figure is about to be written: notes the frame, for <see cref="Morning"/>.</summary>
        public static void Writing(BuildingRegistration reg)
        {
            if (reg != null)
            {
                WriteFrames[reg] = Time.frameCount;
            }
        }

        private static void Ask(BuildingRegistration reg, bool always)
        {
            if (reg == null)
            {
                return;
            }
            if (Queue.Waiting == 0 && _held == 0)
            {
                Speed();   // a round may start
            }
            _round.Asked++;
            GameInstance save = SaveGameManager.Current;
            int hour = save != null ? save.Day * 24 + save.Hour : 0;
            if (!Queue.Ask(reg, Figure(reg), always, hour, Print(List(reg))))
            {
                _round.Kept++;   // drawn with this figure already
                if (Queue.Waiting == 0 && _held == 0)
                {
                    Close();
                }
                return;
            }
            if (_held > 0)
            {
                Wake();   // Release decides; the pace is a frame away in any case
                return;
            }
            if (!Spreading() || ReferenceEquals(reg, Inside()))
            {
                DrawNow(reg);
                if (Queue.Waiting == 0)
                {
                    Close();
                }
                return;
            }
            Wake();
        }

        private static int Figure(BuildingRegistration reg)
        {
            return reg.promotion != null ? reg.promotion.total : NoFigure;
        }

        /// <summary>
        /// A batch of writes begins: what it asks for waits for its end (one redraw a business, with its last figure).
        /// Every Hold is ended by a <see cref="Release"/> in a finally.
        /// </summary>
        public static void Hold(string why)
        {
            if (_held++ == 0)
            {
                if (_round.Why == null)
                {
                    _round.Why = why;
                }
                if (Queue.Waiting == 0)
                {
                    Speed();   // a round may start
                }
            }
        }

        public static void Release()
        {
            if (_held <= 0)
            {
                _held = 0;
                return;
            }
            if (--_held > 0)
            {
                return;
            }
            if (Queue.Waiting == 0)
            {
                Close();
                return;
            }
            if (!Spreading())
            {
                Settle(null);
                return;
            }
            BuildingRegistration inside = Inside();
            if (inside != null && Queue.Waits(inside))
            {
                DrawNow(inside);
            }
            Wake();
        }

        /// <summary>
        /// The game's new day has just drawn every business with its figure: noted, so a figure left as the game wrote it
        /// is not drawn again. Called first in EconomyDirector.OnDay, before any feature writes a figure. COST: one walk
        /// over the save's registrations a game day.
        /// </summary>
        public static void Morning()
        {
            GameInstance save = SaveGameManager.Current;
            Dictionary<Address, List<CustomerEntry>> table = save != null && save.BuildingRegistrations != null && save.Day != _morningDay ? Table() : null;
            if (table == null)
            {
                return;
            }
            _morningDay = save.Day;
            int frame = Time.frameCount, noted = 0, dropped = 0;
            List<BuildingRegistration> all = save.BuildingRegistrations;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !reg.RentedByPlayer || !reg.HasValidAddress || !GoingPublic.Mine(reg) || reg.Address == null)
                {
                    continue;
                }
                List<CustomerEntry> list;
                table.TryGetValue(reg.Address, out list);
                if (Queue.Replaced(reg, list))
                {
                    dropped++;   // asked for before the game's draw, which made a new list
                }
                if (list == null || reg.promotion == null || (WriteFrames.TryGetValue(reg, out int wrote) && wrote == frame) || Cut(reg))
                {
                    Queue.Forget(reg);
                    continue;
                }
                Queue.Drew(reg, reg.promotion.total, Print(list));
                noted++;
            }
            if (Verbose)
            {
                Logger?.Info("Customers check (the game's day " + save.Day + "): " + noted + " business(es) noted as drawn by the game with the figure each has now"
                             + (dropped > 0 ? ", " + dropped + " redraw(s) asked for before its draw left to it" : string.Empty) + ": PASS");
            }
            if (Queue.Waiting == 0 && _held == 0)
            {
                Close();
            }
        }

        // Campaigns none of which runs: the game may have stopped them after its draw (MarketingHelper.RunDaily, unpaid).
        private static bool Cut(BuildingRegistration reg)
        {
            List<MarketingCampaign> campaigns = reg.marketingCampaigns;
            if (campaigns == null || campaigns.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < campaigns.Count; i++)
            {
                if (campaigns[i] != null && campaigns[i].enabled)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Draws everything that waits now; without a city, forgets it.</summary>
        public static void Settle(string why)
        {
            if (Queue.Waiting == 0)
            {
                return;
            }
            if (why != null && _round.Why == null)
            {
                _round.Why = why;
            }
            if (!Ready())
            {
                Queue.Clear();
                _round = default(Round);
                return;
            }
            long began = Stopwatch.GetTimestamp();
            while (Queue.Next(out BuildingRegistration reg, out int figure, out int hour))
            {
                Draw(reg, figure, hour);
            }
            _round.Ms += (Stopwatch.GetTimestamp() - began) / TicksPerMs;
            Close();
        }

        private static void DrawNow(BuildingRegistration reg)
        {
            if (Queue.Take(reg, out int figure, out int hour))
            {
                long began = Stopwatch.GetTimestamp();
                Draw(reg, figure, hour);
                _round.Ms += (Stopwatch.GetTimestamp() - began) / TicksPerMs;
            }
        }

        // Drawn with the figure it was asked with, put on its promotion for the draw; the figure standing is put back after.
        private static void Draw(BuildingRegistration reg, int figure, int hour)
        {
            if (reg == null)
            {
                return;
            }
            GameInstance save = SaveGameManager.Current;
            if (save != null && RedrawRules.Late(hour, save.Day * 24 + save.Hour))
            {
                _round.Late++;
            }
            _round.Drawn++;
            Promotion promotion = reg.promotion;
            int stood = promotion != null ? promotion.total : NoFigure;
            bool swap = promotion != null && figure != NoFigure && stood != figure;
            DayOfWeekOrdered day = TimeHelper.GetDayOfWeek();
            try
            {
                if (swap)
                {
                    promotion.total = figure;
                }
                Draw(reg, day);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                if (swap)
                {
                    promotion.total = stood;
                }
                Action<BuildingRegistration> told = Redrawn;
                if (told != null)
                {
                    try
                    {
                        told(reg);
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                }
            }
            Queue.Drew(reg, swap ? figure : stood, Print(List(reg)));
        }

        private static void Draw(BuildingRegistration reg, DayOfWeekOrdered day)
        {
            if (!OtherMods.MeaningfulAmbience.Loaded)
            {
                CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, day);
                return;
            }
            Kept.Clear();
            KeptSet.Clear();
            try
            {
                int today = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
                List<CustomerEntry> before = List(reg);
                for (int i = 0; before != null && i < before.Count; i++)
                {
                    CustomerEntry e = before[i];
                    if (Underway(e, today, hour))
                    {
                        Kept.Add(e);
                        KeptSet.Add(e);
                    }
                }
                CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, day);
                List<CustomerEntry> after = List(reg);
                if (after == null || Kept.Count == 0 || !BusinessHelper.IsBusinessOpen(reg, hour) || !reg.AreAllRequirementsMet())
                {
                    return;   // shut now, or no customers drawn: the hour is as the game left it
                }
                // The hour under way as it was: what the game just drew for it goes, what was there comes back.
                for (int i = after.Count - 1; i >= 0; i--)
                {
                    if (Underway(after[i], today, hour) && !KeptSet.Contains(after[i]))
                    {
                        after.RemoveAt(i);
                    }
                }
                for (int i = 0; i < Kept.Count; i++)
                {
                    if (!after.Contains(Kept[i]))
                    {
                        after.Add(Kept[i]);
                    }
                }
            }
            finally
            {
                Kept.Clear();
                KeptSet.Clear();
            }
        }

        private static bool Underway(CustomerEntry e, int today, int hour)
        {
            return e != null && !e.completed && e.spawnTime != null && e.spawnTime.Day == today && e.spawnTime.Hour == hour;
        }

        /// <summary>
        /// One frame of <see cref="CustomerDrawPace"/>: all at once if spreading is not safe; nothing while the game still
        /// simulates the hour that ended (it reads the lists); else the shop the player is in, then the waiting ones in
        /// order, at least one, within the budget. False once nothing waits.
        /// </summary>
        internal static bool Pace()
        {
            if (_held > 0)
            {
                _held = 0;   // a batch still open a frame later was left open by a fault
            }
            if (Queue.Waiting == 0)
            {
                Close();
                return false;
            }
            if (!Ready())
            {
                Queue.Clear();
                _round = default(Round);
                return false;
            }
            Clock();
            if (!Spreading())
            {
                Settle("the hour's end, fast time or a session");
                return false;
            }
            if (BusinessSimulatorHelper.Work.HasPendingWork)
            {
                return true;
            }
            long began = Stopwatch.GetTimestamp();
            int done = 0;
            BuildingRegistration inside = Inside();
            if (inside != null && Queue.Waits(inside))
            {
                DrawNow(inside);
                done++;
            }
            while (Queue.Waiting > 0 && RedrawRules.More(done, (Stopwatch.GetTimestamp() - began) / TicksPerMs))
            {
                Queue.Next(out BuildingRegistration reg, out int figure, out int hour);
                Draw(reg, figure, hour);
                done++;
            }
            double ms = (Stopwatch.GetTimestamp() - began) / TicksPerMs;
            _round.Ms += ms;
            _round.Worst = Math.Max(_round.Worst, ms);
            _round.Frames++;
            if (Queue.Waiting == 0)
            {
                Close();
                return false;
            }
            return true;
        }

        // How fast the game's clock was seen to run, for RedrawRules.StepMost.
        private static void Clock()
        {
            GameInstance save = SaveGameManager.Current;
            double clock = (save.Day * 24.0 + save.Hour) * 60.0 + save.Minute;
            float step = Time.deltaTime;
            int frame = Time.frameCount;
            if (_clockFrame == frame - 1)
            {
                _seen = Math.Max(_seen, RedrawRules.Seen(_clock, clock, _clockStep, step));
            }
            _clock = clock;
            _clockStep = step;
            _clockFrame = frame;
        }

        private static bool Spreading()
        {
            GameInstance save = SaveGameManager.Current;
            if (_pace == null || CustomerDrawPace.Off || save == null)
            {
                return false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool skipping = uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
            float step = RedrawRules.StepMost(Time.maximumDeltaTime, Time.timeScale, Math.Max(_speed, _seen));
            return RedrawRules.Spread(!GoingPublic.InSession, skipping, Time.timeScale, step, save.Minute);
        }

        private static bool Ready()
        {
            return SaveGameManager.Current != null && !LoadScene.isLoading && !GameManager.isCitySceneBeingUnloaded;
        }

        private static BuildingRegistration Inside()
        {
            BuildingManager manager = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            return manager != null ? manager.buildingRegistration : null;
        }

        private static void Wake()
        {
            if (_pace != null && !CustomerDrawPace.Off && !_pace.enabled)
            {
                Keep();   // the save's and time skip's handlers, whatever the host's second did
                Speed();
                _pace.enabled = true;
            }
        }

        // Read as a round starts, not each frame: the game's settings box an enum on each read.
        private static void Speed()
        {
            try
            {
                _speed = Math.Max(1f, PlayerPrefSettings.GameSpeed);
            }
            catch (Exception)
            {
                _speed = 1f;
            }
        }

        // A round is over. A redraw made after its hour turned is logged always: it should never happen.
        private static void Close()
        {
            Round r = _round;
            _round = default(Round);
            _seen = 0f;
            if (r.Late > 0)
            {
                Logger?.Warn("Customers: " + r.Late + " business(es) drawn again after the game hour they were asked in had turned (" + (r.Why ?? "as asked") + ").");
            }
            if (Verbose && r.Asked > 0)
            {
                Logger?.Info("Customers check (" + (r.Why ?? "as asked") + "): " + r.Asked + " asked for, " + r.Kept + " left as drawn already with their figure, " + r.Drawn + " drawn again"
                             + (r.Frames > 0 ? " over " + r.Frames + " frame(s) (the longest " + r.Worst.ToString("0.00", CultureInfo.InvariantCulture) + " ms)" : " at once") + ", "
                             + r.Ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms in all; "
                             + (r.Late == 0 ? "every one within the hour it was asked in: PASS" : r.Late + " after their hour had turned: FAIL"));
            }
        }

        /// <summary>Business pressure's director hosts the pace.</summary>
        internal static void Host(CustomerDrawPace pace)
        {
            _pace = pace;
        }

        /// <summary>
        /// Called once a second by the host: keeps the handlers on the game's save and time skip, which the game empties
        /// on every load. A reference comparison each while nothing changed.
        /// </summary>
        public static void Keep()
        {
            SaveHook.Keep(_onSave ?? (_onSave = Saving));
            EventHold.Keep(ref GlobalEvents.onTimeMachineStarted, _onSkip ?? (_onSkip = Guard.Wrap(Skipping)), ref _skipSeen);
        }

        private static void Saving()
        {
            Settle("a save");
        }

        private static void Skipping()
        {
            Settle("the game's time skip");
        }

        /// <summary>The pace was switched off by its guard: what waits is drawn now.</summary>
        internal static void Stopped(CustomerDrawPace pace)
        {
            if (!ReferenceEquals(pace, _pace) || Queue.Waiting == 0)
            {
                return;
            }
            Settle("the pace stopped");
        }

        /// <summary>The city is unloading: forgets everything and takes the handlers off.</summary>
        public static void Forget(CustomerDrawPace pace)
        {
            if (pace != null && !ReferenceEquals(pace, _pace))
            {
                return;   // another city's host has started since
            }
            _pace = null;
            Queue.Clear();
            WriteFrames.Clear();
            _held = 0;
            _morningDay = -1;
            _round = default(Round);
            _seen = 0f;
            _clockFrame = -1;
            SaveHook.Drop(_onSave);
            if (_onSkip != null)
            {
                GlobalEvents.onTimeMachineStarted -= _onSkip;
            }
            _skipSeen = null;
        }
    }

    /// <summary>
    /// The pace of <see cref="CustomerDraw"/>'s redraws, added by BusinessDirector: on only while a redraw waits, so it
    /// costs nothing per frame while idle; while on, about a millisecond a frame (RedrawRules.BudgetMs). Without it,
    /// every redraw is made at once.
    /// </summary>
    public sealed class CustomerDrawPace : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "CustomerDrawPace", typeof(CustomerDrawPace));

        /// <summary>True once the guard has switched the pace off: every redraw is made at once since.</summary>
        public static bool Off => FrameGuard.Off;

        private void Awake()
        {
            enabled = false;
            CustomerDraw.Host(this);
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: CustomerDrawPace.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                if (!CustomerDraw.Pace())
                {
                    enabled = false;
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // Only a switch-off by its guard leaves anything waiting; OnDestroy forgets it all.
        private void OnDisable()
        {
            try
            {
                if (FrameGuard.Off)
                {
                    CustomerDraw.Stopped(this);
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        private void OnDestroy()
        {
            try
            {
                CustomerDraw.Forget(this);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }
    }
}
