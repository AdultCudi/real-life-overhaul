using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// The one way the mod has the game draw again the customers still to come today at a business of the player's
    /// (business pressure's footfall, which every feature's part of a business's figure goes through, and the brands' and
    /// the rota planner's opening hours), and the one place it reads that list.
    ///
    /// The game's own redraw (CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness) throws away every customer
    /// not yet served, the hour under way included, and draws them all again: a few hundred new entries and a count over
    /// the list for every hour. Meaningful Ambience, another author's mod, adds its customers for the hour under way once,
    /// when the hour turns: a redraw in the middle of the hour threw them away until the next. So while that mod is
    /// switched on, the customers of the hour under way are kept as they were (theirs and the game's) and only the hours
    /// after it are drawn again, unless the business is not open this hour or the game draws it no customers (then the hour
    /// is as the game leaves it: empty). Without that mod the redraw is the game's own, untouched.
    ///
    /// ONE REDRAW, SPREAD, WITHIN THE HOUR. In the frame a game day starts the game has just drawn every business itself,
    /// and the features then each wrote their part of the figure and had the day drawn again at once: one business three
    /// or four times in that frame, hundreds in all, the heaviest frame of the day (likewise the city's start after a load,
    /// the interiors' first round, a Going Public host's update and the city's strength changed). Now a redraw is asked for
    /// (<see cref="Rewritten"/>, <see cref="Redraw"/>) and waits (Core/RedrawRules): once a business however often it is
    /// asked for, drawn with the figure of its last asking (as the last of those redraws had it), and not at all where its
    /// list is drawn with the figure it has now already (by the game's own day, <see cref="Morning"/>, or by the mod) and
    /// nobody has drawn it since. The waiting ones are drawn by <see cref="CustomerDrawPace"/>, about a millisecond a frame
    /// (after the game's own simulation of the hour that ended has read its lists), in the order they were asked for; at
    /// once whenever spreading is not safe or something needs the list (RedrawRules.Spread): the hour's last ten game
    /// minutes (the game's simulation of the hour reads the list when it turns), a Going Public session (its clock can move
    /// on by an hour in a frame), fast forward or the game's time skip (sleep, the time machine), the game saving, a feature
    /// reading the list (<see cref="Entries"/>), the shop the player stands in, and the end of a batch of writes made in one
    /// go (<see cref="Hold"/>) when spreading is not on. A redraw is never made in a later hour than it was asked in.
    ///
    /// The customer lists are not saved (CustomerEntriesHelper.Init draws them as a save loads) and each Going Public
    /// machine draws only its own businesses (the owner's list is what Going Public passes on to the others): a redraw made
    /// a few frames later within the same game hour changes nothing saved or shared.
    ///
    /// The list is the game's private table of every business's customers for the day, read by reflection once (the field
    /// comes from Core/GameContract the first time; afterwards it is a dictionary look-up). Nothing of the game's is called
    /// that the game does not itself call for the same thing (the draw is drawn with the figure it was asked with: one put
    /// on the business's own promotion for the draw and taken off after, as the rota planner's forecast does). Cost:
    /// nothing per frame while nothing waits (the pace is switched off); while redraws wait, the pace's frame is at most
    /// about a millisecond of them (always one); an asking is a dictionary look-up. With "Developer diagnostics" on, one line
    /// a round.
    /// </summary>
    public static class CustomerDraw
    {
        public const int NoFigure = int.MinValue;   // a business with no promotion figure: nothing to compare, drawn whenever asked

        public static IModLogger Logger;

        /// <summary>A business's customers still to come today were drawn again (by this mod): whatever it had booked for it is gone. Raised after each redraw; a handler must not throw.</summary>
        public static event Action<BuildingRegistration> Redrawn;

        private struct Round
        {
            public string Why;
            public int Asked, Kept, Drawn, Late, Frames;
            public double Ms, Worst;
        }

        private static readonly RedrawQueue<BuildingRegistration> Queue = new RedrawQueue<BuildingRegistration>();
        private static readonly Dictionary<BuildingRegistration, int> WriteFrames = new Dictionary<BuildingRegistration, int>(64);   // the frame each business's figure was last written in
        private static FieldInfo _table;
        private static Dictionary<Address, List<CustomerEntry>> _tableValue;   // the table itself, once read (its field is readonly)
        private static bool _looked;
        private static readonly List<CustomerEntry> Kept = new List<CustomerEntry>(32);
        private static readonly HashSet<CustomerEntry> KeptSet = new HashSet<CustomerEntry>();
        private static CustomerDrawPace _pace;
        private static int _held;          // batches of writes under way (Hold): what they ask for waits for their end
        private static int _morningDay = -1;
        private static Round _round;
        private static double _clock;      // the game's clock at the pace's last frame, in game minutes
        private static float _clockStep;   // that frame's step of game time, seconds
        private static int _clockFrame = -1;
        private static float _seen;        // the most game minutes a second of game time the clock was seen to run at this round
        private static float _speed = 1f;  // the game's minutes a second of game time by its own setting, read as a round starts
        private static Action _onSave, _onSkip, _skipSeen;
        private static readonly double TicksPerMs = Stopwatch.Frequency / 1000.0;

        private static bool Verbose => Meta.Dev.Verbose;

        // ---------------------------------------------------------------- the list

        /// <summary>The customers of the day at a business of the player's, the game's own list; null if the game keeps none for it. A redraw of it that waits is made first.</summary>
        public static List<CustomerEntry> Entries(BuildingRegistration reg)
        {
            if (reg == null || reg.Address == null)
            {
                return null;
            }
            if (Queue.Waiting > 0 && Queue.Waits(reg))
            {
                DrawNow(reg);   // (a feature is about to read or add to it: as it would have found it)
            }
            return List(reg);
        }

        /// <summary>True while a business waits for its customers to be drawn again (within this game hour): what is to go on the new ones waits with it.</summary>
        public static bool Waits(BuildingRegistration reg)
        {
            return reg != null && Queue.Waiting > 0 && Queue.Waits(reg);
        }

        /// <summary>How a business's list stands now (Core/RedrawRules): before the mod adds to it itself, see <see cref="Added"/>.</summary>
        public static DrawPrint Print(List<CustomerEntry> list)
        {
            return list == null ? default(DrawPrint) : DrawPrint.Of(list, list.Count, list.Count > 0 ? list[list.Count - 1] : null);
        }

        /// <summary>
        /// The mod has added customers of its own to a business's list (an office's extra clients, Scripts/Interiors): a list
        /// that stood as last drawn before is known to stand so still, so that a later asking with the same figure is not
        /// drawn again for it.
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
                return _tableValue;   // (the game's table is one object for as long as it runs: a static readonly field)
            }
            if (!_looked)
            {
                _looked = true;
                try
                {
                    _table = GameContract.CustomerTable.Field;   // (the game's private piece, resolved once: Core/GameContract)
                    if (_table == null || _table.FieldType != typeof(Dictionary<Address, List<CustomerEntry>>))
                    {
                        _table = null;
                        Logger?.Warn("Customers: the game's list of a day's customers is not where it was; the hour under way is not kept on a redraw, the basket adds nothing, and every redraw asked for is made.");
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

        // ---------------------------------------------------------------- asking

        /// <summary>
        /// The game draws again the customers still to come today at a business of the player's, whatever its figure (its
        /// opening hours or its rota changed): waits for its turn, or is made at once where waiting is not safe (the class comment).
        /// </summary>
        public static void Redraw(BuildingRegistration reg)
        {
            Ask(reg, true);
        }

        /// <summary>
        /// A business's footfall figure has just been written (business pressure's writer, BusinessGame.Footfall): its customers
        /// still to come are drawn again with that figure, unless its list stands drawn with it already.
        /// </summary>
        public static void Rewritten(BuildingRegistration reg)
        {
            Ask(reg, false);
        }

        /// <summary>A business's figure is about to be written (any write: with a redraw or not). Notes the frame, for <see cref="Morning"/>.</summary>
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
                Speed();   // (a round may start: the game's speed as it is set now)
            }
            _round.Asked++;
            GameInstance save = SaveGameManager.Current;
            int hour = save != null ? save.Day * 24 + save.Hour : 0;
            if (!Queue.Ask(reg, Figure(reg), always, hour, Print(List(reg))))
            {
                _round.Kept++;   // (its list is drawn with this figure already)
                if (Queue.Waiting == 0 && _held == 0)
                {
                    Close();
                }
                return;
            }
            if (_held > 0)
            {
                Wake();   // the batch's end decides (Release); the pace is a frame away in any case
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

        // ---------------------------------------------------------------- batches of writes

        /// <summary>
        /// A batch of writes begins (a feature's day, the city's start, a host's update, a round of the interiors): what is
        /// asked for in it waits for its end, and is drawn then at once if waiting is not safe (one business once, with its
        /// last figure, in a Going Public session too). Every Hold is ended by a <see cref="Release"/>, in a finally.
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
                    Speed();   // (a round may start: the game's speed as it is set now)
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

        // ---------------------------------------------------------------- the game's day

        /// <summary>
        /// The game's new day has just drawn every business's customers (BusinessHelper.RunDaily) with the figure each has
        /// now: noted, so that a business whose figure the mod's features leave as the game wrote it is not drawn again.
        /// Called first thing in the economy's day (Economy/EconomyDirector.OnDay), which is the first of the mod's handlers
        /// of the game's day to write a figure (Core/StartRules: the credit side's seat is the first). Left unknown, and so
        /// drawn when asked: a business whose figure was written in this frame already, and one with marketing campaigns
        /// none of which runs (the game stops them all after its draw when the marketing cannot be paid, MarketingHelper.RunDaily).
        /// One walk over the save's registrations, once a game day.
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
                    dropped++;   // (asked for before the game's draw, which made it a new list: wiped by the game's own, as a redraw made then was)
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

        // Marketing campaigns, none of which runs: maybe stopped by the game after its draw, for want of the money.
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

        // ---------------------------------------------------------------- drawing

        /// <summary>Everything that waits is drawn now (the game saves, its time skip starts, the pace stops). Nothing is done without a city.</summary>
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

        // The draw itself, with the figure the business was asked for with (as the redraw made at the asking had it: a
        // figure written since without a redraw, say the game's own put back for a save, is left as it stands after).
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
                    return;   // (a business shut now, or that the game draws no customers for, has none this hour: as the game left it)
                }
                // The hour under way as it was: what the game has just drawn for it goes, what was there comes back.
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

        // A customer of the hour under way still to come.
        private static bool Underway(CustomerEntry e, int today, int hour)
        {
            return e != null && !e.completed && e.spawnTime != null && e.spawnTime.Day == today && e.spawnTime.Hour == hour;
        }

        // ---------------------------------------------------------------- the pace

        /// <summary>
        /// One frame of the pace (<see cref="CustomerDrawPace"/>): everything at once if spreading is not safe now; nothing
        /// while the game is still serving the hour that ended (its simulation reads the lists over its own frames, and a
        /// redraw would take away the customers it has not served); else the shop the player stands in first, then the
        /// waiting ones in their order, at least one, while the frame's share is under the budget. False once nothing waits.
        /// </summary>
        internal static bool Pace()
        {
            if (_held > 0)
            {
                _held = 0;   // (a batch is one call: one still open a frame later was left open by a fault, and is closed)
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

        // The game's clock as the pace sees it each frame: how fast it was seen to run (for RedrawRules.StepMost).
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
                Keep();   // (the save's and the time skip's handlers, whatever became of the host's second)
                Speed();
                _pace.enabled = true;
            }
        }

        // The game's minutes a second of game time by its own setting (GameManager.SetMinutesMultiplier is given it): read
        // as a round of redraws starts, not each frame (the game's settings are kept by an enum, a box each read).
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

        // A round is over (nothing waits): its line, with "Developer diagnostics" on; a redraw made after its hour had
        // turned is said whatever is switched on (it should never be).
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
                             + (r.Frames > 0 ? " over " + r.Frames + " frame(s) (the longest " + r.Worst.ToString("0.00") + " ms)" : " at once") + ", " + r.Ms.ToString("0.0") + " ms in all; "
                             + (r.Late == 0 ? "every one within the hour it was asked in: PASS" : r.Late + " after their hour had turned: FAIL"));
            }
        }

        // ---------------------------------------------------------------- the host and the game's events

        /// <summary>Business pressure's director hosts the pace (its Awake); the pace's own guard and frames are its own.</summary>
        internal static void Host(CustomerDrawPace pace)
        {
            _pace = pace;
        }

        /// <summary>
        /// Once a second from the host (Business/BusinessDirector): the handlers kept on the game's save and on the start of
        /// its time skip, which the game empties on every load. A reference comparison each while nothing changed.
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

        /// <summary>The pace has stopped (switched off by its guard, or the city is going): what waits is drawn now, or forgotten with the city.</summary>
        internal static void Stopped(CustomerDrawPace pace)
        {
            if (!ReferenceEquals(pace, _pace) || Queue.Waiting == 0)
            {
                return;
            }
            Settle("the pace stopped");
        }

        /// <summary>The city is going (the host's OnDestroy): nothing waits, nothing is known, the handlers are taken off.</summary>
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
    /// The pace of the customers' redraws (Core/CustomerDraw): switched on when a redraw waits and off again once none
    /// does, so it costs nothing per frame while idle. While on, its frame draws the waiting businesses for about a
    /// millisecond (RedrawRules.BudgetMs), always one, or all at once where spreading is not safe. Added by business
    /// pressure's director (Business/BusinessDirector), which keeps the handlers it needs; while there is none, every redraw
    /// asked for is made at once.
    /// </summary>
    public sealed class CustomerDrawPace : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "CustomerDrawPace", typeof(CustomerDrawPace));   // its Update (Core/Guard)

        /// <summary>True once the guard has switched the pace off after its faults: every redraw is made at once since.</summary>
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
                enabled = false;   // (switched off after its faults: Core/Guard)
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

        // Switched off by its guard after its faults: what waits is drawn now, and every redraw is made at once since. (Off
        // by itself, nothing waits; going with the city, OnDestroy forgets it all.)
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
