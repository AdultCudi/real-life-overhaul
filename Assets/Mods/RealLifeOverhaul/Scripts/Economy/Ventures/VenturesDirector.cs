using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// Investors (docs/features/economy-investors.md): the save's file, the app, and the rules run on the game's day and
    /// hour. No Update: its cost is a one-second timer on the real clock.
    /// </summary>
    public sealed partial class VenturesDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeVentures";
        private static readonly string[] AppGlyphs = { "Icon-Building", "Icon-Shop" };

        public static IModLogger Logger;
        public static VenturesDirector Instance { get; private set; }

        private readonly VenturesWallet _wallet = new VenturesWallet();
        private readonly List<BuildingRegistration> _businesses = new List<BuildingRegistration>(16);
        private readonly List<DealEvent> _said = new List<DealEvent>(8);
        private readonly List<string> _names = new List<string>(VenturesCatalogue.RivalsListed);
        private readonly StringBuilder _log = new StringBuilder(600);

        private VenturesFile _file;
        private VenturesApp _view;
        private PhoneApp _app;
        private PitchMeeting _meeting;
        private bool _running;         // the file is open
        private bool _broken;          // a fault: off for the session
        private bool _gaveUp;          // the game's phone is not as expected: no app, the rest runs
        private bool _offLooked;       // with the option off, the save has been looked at once for anything that carries on
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen, _openInvestors;
        private Func<int, int> _fileClaim;

        public bool Running => _running && !_broken;

        /// <summary>The save's file; null while Ventures is not running.</summary>
        public VenturesFile File => _running ? _file : null;

        /// <summary>The save's file may yet be opened: the option is on, or it is off and the one look for deals that carry on has not been made.</summary>
        public bool Opening => !_running && !_broken && (Settings.VenturesInvestors || !_offLooked);

        /// <summary>How demanding investors are: the option the presets set.</summary>
        public static int Level => VenturesCatalogue.Level(Settings.VenturesLevel);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _openInvestors = () => Open(VenturesPhone.Investors);
            _fileClaim = amount => EconomyBridges.FileClaim != null ? EconomyBridges.FileClaim(amount) : 0;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            // Nothing is saved here: the game's current save may be another one by now.
            try
            {
                StandDown(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // The first look is a second after the city's objects are made, never in the frame they are made in.
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
                while (StartLine.Holds(StartSeats.Ventures))
                {
                    yield return null;   // the first tick after a load waits for a frame of its own (Core/StartLine)
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
        }

        private static readonly int HotTick = HotSpots.Slot("Economy: VenturesDirector.Tick");
        // Once a second: start when the save is ready, hold the handlers and the app. With the option off no offer, pitch or
        // meeting is new, but deals, promises and offers already made carry on: the file stays open until none is left, and a
        // save opened with the option off is looked at once for them.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.VenturesInvestors)
            {
                if (_running && !_file.CarriesOn)
                {
                    StandDown(true);
                    _offLooked = true;
                }
                if (!_running)
                {
                    if (_offLooked)
                    {
                        return;
                    }
                    _offLooked = true;
                    if (!StartUp(true))
                    {
                        return;
                    }
                }
            }
            else
            {
                _offLooked = false;
                if (!_running)
                {
                    StartUp(false);
                }
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            if (_meeting != null)
            {
                _meeting.Watch();
            }
        }

        // Opens the save's file, a new one if it has none; with 'carryOnly' (the option off) only a file with deals, promises or
        // offers that carry on. True if it is open.
        private bool StartUp(bool carryOnly)
        {
            int today = Save.Day, version;
            VenturesFile file = VenturesStore.Load(Save.modData, out version);
            if (carryOnly && (file == null || !file.CarriesOn))
            {
                return false;
            }
            bool fresh = file == null;
            if (fresh)
            {
                file = new VenturesFile { StartDay = today, OfferDay = today, SettledDay = today, MarketDay = today };
                unchecked
                {
                    Cast.Make(file, (int)VenturesRandom.Hash(Environment.TickCount, today * 7919 + (int)Save.Money, (int)DateTime.UtcNow.Ticks), Rivals());
                }
            }
            else if (file.Investors.Count == 0 || file.Companies.Count == 0)
            {
                // A line that lost a part: the same seed makes the same cast, and only the missing part is taken from it.
                var again = new VenturesFile();
                Cast.Make(again, file.Seed, Rivals());
                if (file.Investors.Count == 0)
                {
                    file.Investors.AddRange(again.Investors);
                }
                if (file.Companies.Count == 0)
                {
                    file.Companies.AddRange(again.Companies);
                }
            }
            _file = file;
            _running = true;
            if (fresh)
            {
                Logger?.Info("Ventures: a file is opened for this save on day " + today + " (seed " + file.Seed + "): " + file.Investors.Count + " investors, "
                             + file.Companies.Count + " listed companies.");
            }
            if (Verbose)
            {
                Logger?.Info("Ventures check (start): file version " + (fresh ? "new" : version.ToString()) + ", " + file.Deals.Count + " deal(s), " + file.Offers.Count + " offer(s), "
                             + file.Pledges.Count + " promise(s), " + file.Holdings.Count + " holding(s), market day " + file.MarketSteps + ", name with investors " + file.Reputation
                             + ", level " + Level + ", climate " + VenturesBridges.Climate.Phase + " at " + VenturesBridges.Climate.BankRate.ToString("0.##", CultureInfo.InvariantCulture) + "%"
                             + (carryOnly ? " (the option is off: what was agreed carries on, nothing new comes). " : ". ") + CastLine() + ": PASS");
            }
            Badge();
            return true;
        }

        // The game's named rivals, each under a company name made from its own.
        private List<string> Rivals()
        {
            VenturesGame.RivalNames(_names);
            for (int i = 0; i < _names.Count; i++)
            {
                _names[i] = FinanceText.T("reallife_ven_company_rival", "name", _names[i]);
            }
            return _names;
        }

        private string CastLine()
        {
            _log.Length = 0;
            _log.Append("Cast: ");
            for (int i = 0; i < _file.Investors.Count; i++)
            {
                Investor v = _file.Investors[i];
                _log.Append(i > 0 ? "; " : string.Empty).Append(v.Name).Append(" (").Append(v.Kind).Append(", ").Append(v.Temper).Append(", ").Append(v.ChequeLeast).Append(" to ")
                    .Append(v.ChequeMost).Append(", diligence ").Append(v.Diligence).Append(", likes ").Append(v.Likes).Append(')');
            }
            _log.Append(". Companies: ");
            for (int i = 0; i < _file.Companies.Count; i++)
            {
                Company c = _file.Companies[i];
                _log.Append(i > 0 ? "; " : string.Empty).Append(c.Name).Append(" (").Append(c.Sector).Append(c.Rival ? ", a rival of the game's" : string.Empty).Append(", ")
                    .Append((c.PriceCents / 100f).ToString("0.00", CultureInfo.InvariantCulture)).Append(", move ").Append(c.VolBp).Append(" bp, dividend ").Append(c.YieldBp).Append(" bp)");
            }
            return _log.ToString();
        }

        // 'keep' saves the file first: only for a stop in the middle of play, when the current save is certainly the file's.
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && _file != null)
            {
                VenturesStore.Save(Save.modData, _file);
            }
            _running = false;
            _file = null;
            if (_meeting != null)
            {
                _meeting.Dispose();
                _meeting = null;
            }
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
            _gaveUp = false;
        }

        /// <summary>A fault: one line, and nothing more of Ventures this session. What the save holds of it is left as it is.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            _wallet.BookDay = null;
            try
            {
                StandDown(false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Ventures: switched off for this session after an error in " + where + ". Investors, deals and the market stand still; what the save holds of them is untouched, "
                         + "and the game's daily run and the rest of the economy are not affected.");
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new VenturesApp(this);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_ven_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(120, 148, 235, 255), new Color32(62, 78, 176, 255), 0.42f, "AppIcon-Ventures"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Ventures: the game's phone is not built as this version expects, so the Ventures app is left off. "
                                 + "Deals that exist still pay their share and the market still moves.");
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

        internal string NameOf(int investorId)
        {
            Investor investor = _file != null ? _file.Investor(investorId) : null;
            return investor != null ? investor.Name : string.Empty;
        }

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value).</summary>
        internal void Tell(NotificationType type, string key, Action click, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length > 1)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // a dictionary each: the game keeps a notification's data
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(click), true, false);
        }

        internal void Detail(string what)
        {
            if (Verbose)
            {
                Logger?.Info("Ventures check: " + what + ".");
            }
        }

        private void Badge()
        {
            if (_app != null && _file != null)
            {
                _app.SetBadge(_app.Showing ? 0 : _file.Unseen);
            }
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && _file != null)
                {
                    VenturesStore.Save(Save.modData, _file);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
