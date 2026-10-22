using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The three parts of the property business, each with an option of its own.</summary>
    public enum EstatePart
    {
        Property = 0,   // the market, letting, works, selling and the agency
        Hotel = 1,
        Hire = 2,
        Count = 3
    }

    /// <summary>One row for the Planner, made on the day event and when the player changes something.</summary>
    public struct EstateAlert
    {
        public int A, B;        // the Planner's id for the row: what it is, and which one
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// The property business: buildings, an estate agency, a hotel and car hire. No Update and no timer, so no cost a
    /// frame: the credit side's second, hour and day call <see cref="EachSecond"/>, <see cref="OnHour"/> and
    /// <see cref="OnDay"/>. What it writes into the game is put back when it stops; a fault stops one part, or the
    /// feature, for the session.
    /// </summary>
    public sealed partial class EstateDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeEstate";
        public const int TabMarket = 0, TabMine = 1, TabTenants = 2, TabWorks = 3, TabAgency = 4, TabHotel = 5, TabHire = 6;

        private static readonly string[] PartNames = { "property", "the hotel", "car hire" };
        private static readonly string[] AppGlyphs = { "Icon-Building", "Icon-Neighborhood", "Icon-Shop" };

        public static IModLogger Logger;
        public static EstateDirector Instance { get; private set; }

        private readonly List<EstateAlert> _alerts = new List<EstateAlert>(EstateCatalogue.AlertsMost);
        private readonly List<AgencyEvent> _events = new List<AgencyEvent>(8);
        private readonly bool[] _partBroken = new bool[(int)EstatePart.Count];

        private EstateState _state;
        private EstateClimate _climate = EstateClimate.Steady;
        private string _beforeTests;   // the state's line before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;          // a fault outside the parts: off for the session
        private bool _holding;         // the rents are the game's own for a save: put back on the next game hour
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private bool _cityRead;        // the city's buildings have been read for this start
        private int _parts;            // which parts were on at the last look, a bit each
        private EstateApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay, _soldBack;   // (_soldBack: the hour's look at buildings sold)
        private readonly List<string> _handedBack = new List<string>(2);

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public EstateState State => _running ? _state : null;

        /// <summary>The city as it was read this morning, or when the feature started today.</summary>
        public EstateClimate Climate => _climate;

        public IReadOnlyList<EstateAlert> Alerts => _alerts;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _soldBack = () => SoldBack(false);
            EconomyDirector.Follow(StartSeats.Estate, _second);   // its first second after a load waits for its seat (Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since: the state and the figures are its
            }
            // Nothing goes into the save, but the buildings' prices are game assets that outlive a city: put back even now.
            try
            {
                StandDown("the city is unloading", false, false, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _running = false;
            }
            Instance = null;
        }

        /// <summary>Whether a part runs: the feature is running, the part's option is on and no fault has stopped it.</summary>
        public bool On(EstatePart part)
        {
            return _running && !_broken && !_partBroken[(int)part] && Wanted(part);
        }

        private static bool Wanted(EstatePart part)
        {
            switch (part)
            {
                case EstatePart.Property: return Settings.Estate;
                case EstatePart.Hotel: return Settings.EstateHotel;
                case EstatePart.Hire: return Settings.EstateHire;
                default: return false;
            }
        }

        private static bool AnyWanted => Settings.Estate || Settings.EstateHotel || Settings.EstateHire;

        private int PartsOn()
        {
            int mask = 0;
            for (int p = 0; p < (int)EstatePart.Count; p++)
            {
                mask |= On((EstatePart)p) ? 1 << p : 0;
            }
            return mask;
        }

        private static string PartList(int mask)
        {
            string list = string.Empty;
            for (int p = 0; p < PartNames.Length; p++)
            {
                if ((mask & (1 << p)) != 0)
                {
                    list += (list.Length > 0 ? ", " : string.Empty) + PartNames[p];
                }
            }
            return list.Length > 0 ? list : "none";
        }

        private static readonly int HotSecond = HotSpots.Slot("Economy: EstateDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);
            if (_broken)
            {
                return;
            }
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                Fault("the timer", e);
            }
        }

        private void Slow()
        {
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            EconomyDirector credit = EconomyDirector.Instance;
            if (!Settings.Economy || !AnyWanted || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!AnyWanted ? "the options are off" : "the credit side is not running", true, true, false);
                }
                return;
            }
            if (!_running && !StartUp())
            {
                return;
            }
            SaveHook.Keep(_onSave);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            int scale = EstateGame.RentScale;
            if (scale != _climate.RentScale)
            {
                // Every unit's market rent follows the option from now on; a lease keeps the rent it was agreed at.
                int was = EstateRules.RentShare(_climate.RentScale);
                _climate.RentScale = scale;
                Changed();
                Logger?.Info("Estate: rental income set to " + (scale / 10) + "% of the market's rent (was " + (was / 10) + "%); leases already agreed keep their rent.");
            }
            int parts = PartsOn();
            if (parts == _parts)
            {
                return;
            }
            int before = _parts;
            _parts = parts;
            PartsChanged(before, parts, Save.Day);
            Changed();
            if (Verbose)
            {
                Logger?.Info("Estate check (options): parts on " + PartList(parts) + " (were " + PartList(before) + "); what a part that went off had written is put back: PASS");
            }
        }

        // A part's switch was changed: what no longer applies is taken back at once.
        private void PartsChanged(int before, int now, int day)
        {
            bool property = (now & 1) != 0;
            if (property != ((before & 1) != 0))
            {
                if (property)
                {
                    ApplyProperty(day);
                }
                else
                {
                    WithdrawProperty();
                }
            }
            if ((now & (1 << (int)EstatePart.Hire)) == 0 && (before & (1 << (int)EstatePart.Hire)) != 0)
            {
                RecallCars("the option is off");
            }
            if ((now & (1 << (int)EstatePart.Hire)) != 0)
            {
                HireReconcile();
            }
        }

        // False while the city's buildings are not there to be read: the next second tries again.
        private bool StartUp()
        {
            GameInstance save = Save;
            if (!_cityRead)
            {
                if (!EstateGame.ReadCity())
                {
                    return false;
                }
                _cityRead = true;
            }
            int today = save.Day;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            EstateState state = EstateStore.Load(save.modData, EstateStore.Key, out version);
            bool fresh = state == null;
            if (fresh)
            {
                state = new EstateState { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = today, YearFrom = today };
            }
            string before;
            _beforeTests = save.modData.TryGetValue(EstateStore.TestKey, out before) && !string.IsNullOrEmpty(before) ? before : null;
            state.LastDay = today;   // days that passed while this was off are not counted
            _state = state;
            _holding = false;
            _running = true;
            _parts = PartsOn();
            _climate = EstateGame.Climate(today);
            int added = 0, dropped = 0;
            if (On(EstatePart.Property))
            {
                SoldBack(true);   // a building sold and put back as the player's for nothing is the buyer's again before it is read as the player's
                EstateGame.ReadHoldings(_state, out added, out dropped);
                EstateGame.ReadAgency(_state.Agency);
                ApplyProperty(today);
            }
            ReadHotels(today);
            if (On(EstatePart.Hire))
            {
                HireReconcile();
            }
            BuildAlerts(today);
            Logger?.Info("Estate: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + EstateGame.Buildings + " building(s) in the city, "
                         + _state.Holdings.Count + " the player's" + (added > 0 ? " (" + added + " new to the books)" : string.Empty)
                         + (dropped > 0 ? " (" + dropped + " no longer the player's)" : string.Empty) + ", the market at " + _state.Real + ", " + _state.Hotels.Count + " hotel(s), "
                         + _state.Hire.Cars.Count + " car(s) on hire, " + _state.Agency.Open.Count + " instruction(s), parts on: " + PartList(_parts) + ".");
            return true;
        }

        // The property part comes on: every building's value and the rents of those the player lets himself are written.
        private void ApplyProperty(int day)
        {
            if (!_cityRead && !(_cityRead = EstateGame.ReadCity()))
            {
                return;
            }
            int added, dropped;
            EstateGame.ReadHoldings(_state, out added, out dropped);
            int written = EstateGame.WriteValues(_state, _climate.Index);
            int rents = WriteRents(false);
            if (Verbose)
            {
                Logger?.Info("Estate check (applied, day " + day + "): " + written + " building value(s) written at a market of " + _state.Real + " and prices of "
                             + (_climate.Index / 10f).ToString("0.0", CultureInfo.InvariantCulture) + ", " + rents + " rent(s) of buildings the player lets himself: PASS");
            }
        }

        // The property part goes off, or everything stops: every value and every rent is the game's own again.
        private void WithdrawProperty()
        {
            int rents = _state != null && Save != null ? WriteRents(true) : 0;
            int values = EstateGame.RestoreValues();
            _cityRead = false;
            if (Verbose || values > 0)
            {
                Logger?.Info("Estate check (restored): " + values + " building value(s) and " + rents + " rent(s) are the game's own again: PASS");
            }
        }

        // The feature stops and every figure it changed is the game's own again; with 'keep' the state goes into the save first.
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (!unloading && was)
            {
                RecallCars(why);
            }
            if (keep && _running && Save != null && _state != null)
            {
                Write(Save);
            }
            if (unloading)
            {
                EstateGame.RestoreValues();   // the save in memory goes with the city: only the game's assets are put back
                _cityRead = false;
            }
            else if (was)
            {
                WithdrawProperty();
            }
            _running = false;
            _holding = false;
            _alerts.Clear();
            if (!unloading)
            {
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Estate: stopped (" + why + ").");
            }
        }

        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
        }

        /// <summary>A fault outside the parts: one line, every figure put back, and nothing more of the feature this session.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault", false, false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Estate: switched off for this session after an error in " + where + ". Every building's value and rent is the game's own again, and the cars on hire are back or come back "
                         + "with the next load; the game's daily run and the rest of the economy are not affected.");
        }

        /// <summary>A fault in one part: that part is off for the session and what it wrote is taken back; the others go on.</summary>
        private void PartFault(EstatePart part, string where, Exception e)
        {
            _partBroken[(int)part] = true;
            Logger?.Error(e);
            Logger?.Warn("Estate: " + PartNames[(int)part] + " switched off for this session after an error in " + where
                         + ". What it wrote is taken back; the other parts, the game's daily run and the rest of the economy are not affected.");
            try
            {
                int before = _parts;
                _parts = PartsOn();
                if (Save != null)
                {
                    PartsChanged(before, _parts, Save.Day);
                }
            }
            catch (Exception second)
            {
                Fault("putting a part's figures back", second);
            }
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new EstateApp(this);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_estate_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(96, 190, 150, 255), new Color32(34, 120, 96, 255), 0.42f, "AppIcon-Property"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Estate: the game's phone is not built as this version expects, so the Property app is left off. The rules still run, and the Planner and the notifications "
                                 + "still say what needs an answer.");
                }
                return;
            }
            _view = view;
            Badge();
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        // The game's hour event: every game hour, and first thing in its NewDay, before its daily run.
        private void OnHour()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                if (_holding)
                {
                    _holding = false;
                    if (On(EstatePart.Property))
                    {
                        WriteRents(false);   // the game's nightly run may follow at once: it pays what stands now
                    }
                }
                Part(EstatePart.Property, "a sold building", _soldBack);   // (also first thing in the game's NewDay: before its daily run could sell it again)
                if (_state.Hire.Cars.Count > 0)
                {
                    try
                    {
                        HireHour(Save.Day);
                    }
                    catch (Exception e)
                    {
                        PartFault(EstatePart.Hire, "the hour", e);
                    }
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        // The game's day event, after its daily run, the credit side, the city's economy and the company.
        private void OnDay()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day <= _state.LastDay)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                _holding = false;
                _state.LastDay = day;   // a jump of many days is one pass, for today
                _climate = EstateGame.Climate(day);
                if (EstateRules.YearTurned(_state.YearFrom, day, _climate.DaysPerYear))
                {
                    NewYear(day);
                }
                Part(EstatePart.Property, "the day", () => PropertyDay(day));
                Part(EstatePart.Hotel, "the day", () => HotelDay(day));
                if (On(EstatePart.Hire) || _state.Hire.Cars.Count > 0)
                {
                    try
                    {
                        HireDay(day);
                    }
                    catch (Exception e)
                    {
                        PartFault(EstatePart.Hire, "the day", e);
                    }
                }
                try
                {
                    TradingYear(day);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);   // the year's figures are kept: the next morning tries again
                    Logger?.Warn("Estate: the tax on the hotels and car hire could not be settled today; it is tried again tomorrow.");
                }
                Changed();
                if (Verbose)
                {
                    Logger?.Info("Estate check (day " + day + "): " + _state.Holdings.Count + " building(s), the market " + _state.Real + ", prices "
                                 + (_climate.Index / 10f).ToString("0.0", CultureInfo.InvariantCulture) + ", cycle " + _climate.Cycle + ", " + _state.Agency.Open.Count + " instruction(s), "
                                 + _state.Hotels.Count + " hotel(s), " + _state.Hire.Count(HireState.Out) + " of " + _state.Hire.Cars.Count + " car(s) out, " + _alerts.Count
                                 + " row(s) for the Planner; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms in all: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        /// <summary>
        /// Buildings sold that are among the player's again with no purchase since (EstateGame.HandBackSold): handed back to their
        /// buyers, the player told once a sale. With <paramref name="books"/> (a start), the game's books are searched first for
        /// sales made before they were remembered. Over the handful of sales remembered; nothing at all while there are none.
        /// </summary>
        private void SoldBack(bool books)
        {
            int found = books ? EstateGame.SalesInBooks(_state, Save.Day) : 0;
            if (_state.Sold.Count == 0)
            {
                return;
            }
            _handedBack.Clear();
            int back = EstateGame.HandBackSold(_state, _handedBack);
            for (int i = 0; i < _handedBack.Count; i++)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_sold_back", Data("building", _handedBack[i]), TabMine, null);
            }
            if (back > 0)
            {
                Changed();
            }
            if (Verbose && (books || back > 0))
            {
                Logger?.Info("Estate check (buildings sold, day " + Save.Day + (books ? ", a start" : string.Empty) + "): " + _state.Sold.Count + " sale(s) remembered"
                             + (books ? ", " + found + " found in the game's books of buildings held again from before them" : string.Empty) + "; " + back
                             + " handed back to their buyers (" + _handedBack.Count + " told): PASS");
            }
        }

        private void Part(EstatePart part, string where, Action what)
        {
            if (!On(part))
            {
                return;
            }
            try
            {
                what();
            }
            catch (Exception e)
            {
                PartFault(part, where, e);
            }
        }

        // The rents of buildings the player lets himself are in the save's entries: the game's own until the next game hour.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null || _state == null)
                {
                    return;
                }
                int put = 0;
                if (On(EstatePart.Property))
                {
                    _holding = true;
                    put = WriteRents(true);
                }
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Estate check (before a save): " + put + " rent(s) put at the game's own until the next game hour; " + _state.Hire.Count(HireState.Out)
                                 + " car(s) out on hire are on the save's own list of vehicles as they always are; the state written under its own key: PASS");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[EstateStore.Key] = EstateStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[EstateStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(EstateStore.TestKey);
            }
        }

        /// <summary>For the save tools: every figure is the game's own again; the timer starts it again if an option is on.</summary>
        public void StopAll()
        {
            try
            {
                StandDown("the save tools asked", true, true, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            try
            {
                StandDown("the save's line was removed", false, false, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _state = null;
            _beforeTests = null;
        }
    }
}
