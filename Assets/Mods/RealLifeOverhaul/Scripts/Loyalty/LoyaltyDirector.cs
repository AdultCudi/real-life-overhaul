using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>
    /// Loyalty cards for the player's shops (docs/features/loyalty.md): a scheme a business, switched on from the Loyalty tab
    /// in BizMan, shared in its settings by the shops of a brand. At the game's day (after its daily run) each scheme lives
    /// yesterday by <see cref="LoyaltyRules"/> from the game's own books of that day, its rewards and running costs are booked
    /// into yesterday's statement, what the player chose comes into force and today's lift is written through business
    /// pressure's one writer of a business's footfall; at the game's hour the hours a shop could serve nobody are counted and
    /// members' baskets take a product more. Only the player's own businesses, on this machine. No Update. COST: nothing per
    /// frame; a few comparisons a second, a walk of the schemes once a game hour and a walk of the save's businesses once a day
    /// while a scheme runs.
    /// </summary>
    public sealed partial class LoyaltyDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static LoyaltyDirector Instance { get; private set; }

        private readonly Dictionary<string, BuildingRegistration> _shops = new Dictionary<string, BuildingRegistration>(16, StringComparer.Ordinal);
        private readonly Dictionary<BuildingRegistration, LoyaltyFile> _byReg = new Dictionary<BuildingRegistration, LoyaltyFile>(8);
        private readonly Dictionary<BuildingRegistration, int> _due = new Dictionary<BuildingRegistration, int>(4);   // baskets waiting for a redraw, by the hour due
        private readonly List<BuildingRegistration> _scratch = new List<BuildingRegistration>(4);
        private readonly List<LoyaltyRival> _rivals = new List<LoyaltyRival>(16);
        private LoyaltyState _state = new LoyaltyState();
        private bool _loaded, _broken;
        private int _passDay = -1, _revision;
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen;
        private Action<BuildingRegistration> _onRedrawn;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            LoyaltyGame.Logger = logger;
            root.AddComponent<LoyaltyDirector>();
        }

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Settings.Loyalty;

        /// <summary>Moves whenever something the page shows may have changed.</summary>
        public int Revision => _revision;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            LoyaltyTab.Begin();
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _onRedrawn = Redrawn;
            CustomerDraw.Redrawn += _onRedrawn;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;   // the handlers that were put on: Guard.Wrap's
            GlobalEvents.onNewDay -= _onDay;
            CustomerDraw.Redrawn -= _onRedrawn;
            LoyaltyTab.Remove();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Look();
                }
                catch (Exception e)
                {
                    Fault(e);
                }
            }
        }

        private void Fault(Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Loyalty: switched off for this session after the error above; every footfall change of the schemes' was put back and the save keeps them as they were.");
            try
            {
                StandDown();
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
        }

        private void Look()
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded || InstanceBehavior<UIs>.Instance == null || !LoyaltyGame.Ready)
            {
                return;
            }
            if (!Settings.Loyalty)
            {
                if (_loaded)
                {
                    PutAway();
                }
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            LoyaltyTab.Keep();
            int day = LoyaltyGame.Day;
            if (_passDay != day)
            {
                Pass(day);   // a load, or the option switched on: the day's handler has not run for this day here
            }
            if (_due.Count > 0)
            {
                Due(day, LoyaltyGame.Hour);
            }
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = LoyaltyStore.Load(save.modData, out int version) ?? new LoyaltyState();
            _loaded = true;
            _passDay = -1;
            _due.Clear();
            _revision++;
            LoyaltyTab.Stale();
            Logger?.Info("Loyalty: " + (version > 0 ? "read the save's line (version " + version + "): " + _state.Files.Count + " scheme(s)" : "no scheme yet") + ".");
        }

        private void OnSave()
        {
            try
            {
                if (_loaded)
                {
                    Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The save's line: a scheme of a business that is no longer the player's, or one long over, is not kept.
        private void Write()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int today = LoyaltyGame.Day;
            for (int i = _state.Files.Count - 1; i >= 0; i--)
            {
                LoyaltyFile f = _state.Files[i];
                if (BusinessGame.Held(f.Place) == null || LoyaltyRules.Spent(f, today))
                {
                    _state.Files.RemoveAt(i);
                }
            }
            if (_state.Files.Count > 0)
            {
                save.modData[LoyaltyStore.Key] = LoyaltyStore.Write(_state);
            }
            else if (!SaveLines.IsKept(save.modData, LoyaltyStore.Key))
            {
                save.modData.Remove(LoyaltyStore.Key);
            }
        }

        // The option was switched off: the line is written, the schemes wait as they are and bring nobody meanwhile.
        private void PutAway()
        {
            Write();
            StandDown();
            _loaded = false;
            _revision++;
            LoyaltyTab.Stale();
            Logger?.Info("Loyalty: switched off; the schemes are kept in the save and run again when it is on.");
        }

        // Every business's footfall written again without the schemes' part.
        private void StandDown()
        {
            _scratch.Clear();
            foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
            {
                if (pair.Value.Written != 0)
                {
                    _scratch.Add(pair.Key);
                    pair.Value.Written = 0;
                }
            }
            _byReg.Clear();   // FootfallOf answers nothing now
            _due.Clear();
            CustomerDraw.Hold("the loyalty cards stood down");
            try
            {
                for (int i = 0; i < _scratch.Count; i++)
                {
                    WriteFigure(_scratch[i], false);
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            _scratch.Clear();
        }

        /// <summary>The Guide's reset: every scheme of this save forgotten. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(LoyaltyStore.Key);
            }
            StandDown();
            _state = new LoyaltyState();
            _loaded = false;
            _revision++;
            LoyaltyTab.Stale();
            Logger?.Info("Loyalty: every scheme forgotten.");
        }

        // The player's businesses by place, each scheme's business, its brand and how many of the brand's other shops run the card.
        private void Map()
        {
            LoyaltyGame.Shops(_shops);
            _byReg.Clear();
            LoyaltyChain chain = LoyaltyBridges.Chain;
            for (int i = 0; i < _state.Files.Count; i++)
            {
                LoyaltyFile f = _state.Files[i];
                if (_shops.TryGetValue(f.Place, out BuildingRegistration reg))
                {
                    _byReg[reg] = f;
                }
                f.Chain = string.Empty;
                try
                {
                    f.Chain = chain != null ? chain(f.Place, out string _) ?? string.Empty : string.Empty;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            for (int i = 0; i < _state.Files.Count; i++)
            {
                _state.Files[i].Sisters = Sisters(_state.Files[i], _state.Files[i].Chain);
            }
        }

        // The other shops of a brand whose scheme runs or starts tomorrow.
        private int Sisters(LoyaltyFile but, string chain)
        {
            int n = 0;
            for (int i = 0; !string.IsNullOrEmpty(chain) && i < _state.Files.Count; i++)
            {
                LoyaltyFile f = _state.Files[i];
                n += !ReferenceEquals(f, but) && (f.On || f.Want) && string.Equals(f.Chain, chain, StringComparison.Ordinal) ? 1 : 0;
            }
            return n;
        }

        // The other owners' schemes of a business's kind in its district, by the rules the player's own runs by.
        private void ReadRivals(BuildingRegistration reg, LoyaltyFile f, int today)
        {
            _rivals.Clear();
            LoyaltyGame.Rivals(reg, today, _rivals);
            try
            {
                LoyaltyBridges.Outlets?.Invoke(GameRegistrations.District(reg), reg.businessTypeName, today, _rivals);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            f.Rivals = _rivals.Count;
            f.Rivalry = LoyaltyRules.Rivalry(_rivals, out f.RivalsRunning);
            _rivals.Clear();
        }

        // A business's footfall written again through business pressure, with today's customers still to come drawn again.
        private static bool WriteFigure(BuildingRegistration reg, bool newDay)
        {
            try
            {
                return LoyaltyBridges.Footfall != null && LoyaltyBridges.Footfall(reg, true, newDay, out int _, out int _);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>
        /// What a business's scheme adds to its footfall today, per cent; asked whenever business pressure writes its figure: a
        /// dictionary look-up. Nothing while the feature is off or the scheme is.
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            LoyaltyDirector d = Instance;
            return d != null && d.Running && reg != null && d._byReg.TryGetValue(reg, out LoyaltyFile f) && f.On ? f.Lift : 0;
        }

        /// <summary>Told as a figure is written: the per cent of customers the scheme's lift really added inside the bounds the figure keeps.</summary>
        public static void Carried(BuildingRegistration reg, int percent)
        {
            LoyaltyDirector d = Instance;
            if (d != null && reg != null && d._byReg.TryGetValue(reg, out LoyaltyFile f))
            {
                f.Carried = LoyaltyRules.Clamp(percent, 0, LoyaltyRules.LiftMost);
            }
        }

        /// <summary>A business's scheme, or null for none; a dictionary look-up.</summary>
        public LoyaltyFile At(BuildingRegistration reg)
        {
            return Running && reg != null && _byReg.TryGetValue(reg, out LoyaltyFile f) ? f : null;
        }

        /// <summary>The brand a business's card is shared with and its name; null for a card of its own.</summary>
        public static string ChainOf(BuildingRegistration reg, out string name)
        {
            name = null;
            try
            {
                LoyaltyChain chain = LoyaltyBridges.Chain;
                return chain != null && reg != null ? chain(LoyaltyGame.Place(reg), out name) : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        // ------------------------------------------------------------------ what the player does

        private static LoyaltyRefusal Say(LoyaltyRefusal r)
        {
            if (r != LoyaltyRefusal.None)
            {
                Notifications.ShowError(LoyaltyText.Why(r));
            }
            return r;
        }

        /// <summary>What switching the scheme on at a business costs today: nothing to take an ending back, less to join the brand's running scheme.</summary>
        public int SetupCost(BuildingRegistration reg)
        {
            LoyaltyFile f = At(reg);
            if (f != null && (f.On || f.Want))
            {
                return 0;
            }
            string chain = ChainOf(reg, out string _);
            return Prices.At(LoyaltyRules.SetupCost(Sisters(f, chain)));
        }

        /// <summary>
        /// The player switches the scheme on at a business: the set-up is paid now and the cards are out from tomorrow morning.
        /// <paramref name="shown"/> is the cost the question box said (-1: not asked): if it has moved, nothing is taken.
        /// </summary>
        public LoyaltyRefusal SwitchOn(BuildingRegistration reg, int shown = -1)
        {
            if (!Running)
            {
                return Say(LoyaltyRefusal.Off);
            }
            if (!LoyaltyGame.IsShop(reg))
            {
                return Say(LoyaltyRefusal.NotShop);
            }
            LoyaltyFile f = At(reg);
            if (f != null && f.Want)
            {
                return LoyaltyRefusal.None;
            }
            if (f == null && _state.Files.Count >= LoyaltyRules.FilesMost)
            {
                return Say(LoyaltyRefusal.Full);
            }
            int cost = SetupCost(reg);
            if (shown >= 0 && shown != cost)
            {
                _revision++;
                return Say(LoyaltyRefusal.Cash);   // the price charged is the price shown, or nothing is charged
            }
            if (!LoyaltyGame.Charge(reg, cost, LoyaltyGame.SetupKey, null, false))
            {
                return Say(LoyaltyRefusal.Cash);
            }
            int today = LoyaltyGame.Day;
            if (f == null)
            {
                f = _state.File(LoyaltyGame.Place(reg)) ?? new LoyaltyFile { Place = LoyaltyGame.Place(reg) };
                if (!_state.Files.Contains(f))
                {
                    _state.Files.Add(f);
                }
                f.Base = LoyaltyGame.Average(reg, out f.MarginMille);
                f.LastDay = today;
            }
            f.Want = true;
            f.Paid += cost;
            Map();
            Adopt(f);
            ReadRivals(reg, f, today);
            _revision++;
            Logger?.Info("Loyalty: a scheme at " + LoyaltyGame.Name(reg) + " switched on for " + cost
                         + (f.Chain.Length > 0 ? ", shared with " + f.Sisters + " other shop(s) of its brand" : string.Empty) + "; the cards are out from tomorrow morning.");
            LoyaltyGame.Tell(NotificationType.Success, f.On ? "reallife_loyalty_note_kept" : "reallife_loyalty_note_started",
                LoyaltyGame.Data("business", LoyaltyGame.Name(reg), "amount", Economy.FinanceText.Money(cost)));
            return LoyaltyRefusal.None;
        }

        // A shop joining its brand's scheme takes the brand's settings.
        private void Adopt(LoyaltyFile f)
        {
            for (int i = 0; f.Chain.Length > 0 && i < _state.Files.Count; i++)
            {
                LoyaltyFile other = _state.Files[i];
                if (!ReferenceEquals(other, f) && (other.On || other.Want) && string.Equals(other.Chain, f.Chain, StringComparison.Ordinal))
                {
                    f.NextRate = other.NextRate;
                    f.Push = other.Push;
                    f.NextOffer = other.NextOffer;
                    return;
                }
            }
        }

        /// <summary>The player ends the scheme at a business: it runs to the end of the day, and its members fall away after.</summary>
        public LoyaltyRefusal End(BuildingRegistration reg)
        {
            LoyaltyFile f = At(reg);
            if (f == null)
            {
                return Say(LoyaltyRefusal.Off);
            }
            if (f.Want)
            {
                f.Want = false;
                _revision++;
                Logger?.Info("Loyalty: the scheme at " + LoyaltyGame.Name(reg) + " ends with the day.");
                LoyaltyGame.Tell(NotificationType.Info, "reallife_loyalty_note_ending", LoyaltyGame.Data("business", LoyaltyGame.Name(reg)));
            }
            return LoyaltyRefusal.None;
        }

        // One choice of a scheme stepped on, for every shop of its brand that runs the card: they share one set of terms.
        private LoyaltyRefusal Choose(BuildingRegistration reg, Action<LoyaltyFile> step, Action<LoyaltyFile, LoyaltyFile> copy)
        {
            LoyaltyFile f = At(reg);
            if (f == null)
            {
                return Say(LoyaltyRefusal.Off);
            }
            step(f);
            for (int i = 0; f.Chain.Length > 0 && i < _state.Files.Count; i++)
            {
                LoyaltyFile other = _state.Files[i];
                if (!ReferenceEquals(other, f) && (other.On || other.Want) && string.Equals(other.Chain, f.Chain, StringComparison.Ordinal))
                {
                    copy(f, other);
                }
            }
            _revision++;
            return LoyaltyRefusal.None;
        }

        private static readonly Action<LoyaltyFile> StepRate = f => f.NextRate = LoyaltyRules.Next(f.NextRate, 0, LoyaltyRules.Rates - 1);
        private static readonly Action<LoyaltyFile, LoyaltyFile> CopyRate = (from, to) => to.NextRate = from.NextRate;
        private static readonly Action<LoyaltyFile> StepPush = f => f.Push = LoyaltyRules.Next(f.Push, 0, LoyaltyRules.Pushes - 1);
        private static readonly Action<LoyaltyFile, LoyaltyFile> CopyPush = (from, to) => to.Push = from.Push;
        private static readonly Action<LoyaltyFile> StepOffer = f => f.NextOffer = LoyaltyRules.Next(f.NextOffer, LoyaltyFile.NoDay, LoyaltyRules.WeekDays - 1);
        private static readonly Action<LoyaltyFile, LoyaltyFile> CopyOffer = (from, to) => to.NextOffer = from.NextOffer;

        /// <summary>The reward rate's next step, in force from tomorrow morning.</summary>
        public LoyaltyRefusal NextRate(BuildingRegistration reg) => Choose(reg, StepRate, CopyRate);

        /// <summary>The sign-up push's next step, at once: it only changes what a new member costs and how many join.</summary>
        public LoyaltyRefusal NextPush(BuildingRegistration reg) => Choose(reg, StepPush, CopyPush);

        /// <summary>The members' day's next choice (none, then Monday to Sunday), in force after its notice.</summary>
        public LoyaltyRefusal NextOffer(BuildingRegistration reg) => Choose(reg, StepOffer, CopyOffer);
    }
}
