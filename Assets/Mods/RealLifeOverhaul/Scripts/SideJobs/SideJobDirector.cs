using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using UI.Elements;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>
    /// Taxi and delivery shifts in the player's own vehicle (docs/features/side-jobs.md): a sign-on desk outside the cab office
    /// and one outside the parcel depot, each beside its firm's help-wanted board. At a desk the game's panel over it offers to
    /// sign on; the press opens the game's own work panel with the vehicle, the terms and the firm's books, and ten minutes on
    /// its clock later the jobs' director drives the shift as its ride-hail or courier job in desk mode (Jobs/DeskShift). This
    /// component owns the desks, the driver's figures and their line in the save, and says what a shift came to, fuel included,
    /// when it is signed off. No Update. COST: nothing a frame; a one-second look (a few comparisons, one read of the tank while
    /// a shift is on); a frame-by-frame wait only for the moment the work panel takes to close and the street to start after signing on.
    /// </summary>
    public sealed partial class SideJobDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static SideJobDirector Instance { get; private set; }

        private const float StartWait = 12f;   // real seconds at most between the sign-on's clock ending and the shift starting: the street has to be running again

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly GameUi.WorldPanel _panel = new GameUi.WorldPanel { Name = "sign-on desk" };
        private SideJobState _state = new SideJobState();
        private SideJobDesks _desks;
        private bool _loaded, _broken;
        private Action _onSave, _onPress;
        private Func<int> _leftCab, _leftParcels;
        private Action<DeskTake> _bookCab, _bookParcels;
        private int _keyFrame = -1;
        private int _shown = int.MinValue;     // the desk and the button the panel was last written for
        private ShiftSkip _skip;               // the sign-on while it is on the game's work panel

        // The shift that is on, for what is said when it is signed off.
        private bool _on;
        private SideKind _onKind;
        private CarController _onCar;
        private float _fuelLast;               // litres in the tank at the last look
        private float _burned;                 // litres burned since signing on (a fill-up in between is not counted back)
        private DeskTake _sum;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            root.AddComponent<SideJobDirector>();
        }

        /// <summary>True while the desk's panel has the interaction key (its button): the mod's own door and car key stands aside.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._keyFrame == Time.frameCount || (Instance._shown != int.MinValue && Instance._panel.Showing));

        /// <summary>True while a save is read, the options are on and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Settings.SideJobs && Settings.Jobs;

        /// <summary>The driver's figures. Read them only while <see cref="Running"/>.</summary>
        public SideJobState State => _state;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _desks = new SideJobDesks(transform, Logger);
            _onSave = OnSave;
            _onPress = Press;
            _leftCab = () => _state.Left(SideKind.Cab);
            _leftParcels = () => _state.Left(SideKind.Parcels);
            _bookCab = take => Booked(SideKind.Cab, take);
            _bookParcels = take => Booked(SideKind.Parcels, take);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            HidePanel();
            _skip?.Close();
            _skip = null;
            _desks.Dispose();
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
            HidePanel();
            Logger?.Error(e);
            Logger?.Warn("Side jobs: switched off for this session after the error above. The Gig Line and the boards work as before; a shift that is on goes on and is paid.");
        }

        private void Look()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.SideJobs || !Settings.Jobs)
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
            _state.NewDay(SideJobGame.Day);
            Watch();
            _desks.Tick(game.playerController.transform.position, SideJobGame.OnFoot(game));
            Panel();
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = SideJobStore.Load(save.modData, out int version) ?? new SideJobState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: the firms' other drivers come from it
            }
            _state.NewDay(SideJobGame.Day);
            _loaded = true;
            Logger?.Info("Side jobs: " + (version > 0 ? "read the save's line (version " + version + ")" : "no line in this save yet") + ": "
                         + _state.At(SideKind.Cab).Shifts + " cab shift(s) and " + _state.At(SideKind.Parcels).Shifts + " delivery shift(s) signed off so far.");
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
            save.modData[SideJobStore.Key] = SideJobStore.Write(_state);
        }

        // The option was switched off: a shift that is on is called off and paid, the desks are taken in, the figures stay in the save.
        private void PutAway()
        {
            PutAside();
            Write();
            _loaded = false;
            Logger?.Info("Side jobs: switched off; the desks are taken in and the figures kept in the save.");
        }

        /// <summary>Everything of the session is taken back: the sign-on on the work panel, a shift that is on, the panel and the desks.</summary>
        public void PutAside()
        {
            _skip?.Close();
            _skip = null;
            if (SideJobBridges.OnDeskShift())
            {
                SideJobBridges.CallOff();
            }
            if (_on)
            {
                SignedOff();
            }
            HidePanel();
            _desks.Dispose();
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(SideJobStore.Key);
            }
            _state = new SideJobState();
            _loaded = false;
        }

        // The game's panel over the desk the player stands at: sign on, or sign off while a desk shift is on.
        private void Panel()
        {
            int at = _desks.At;
            if (at < 0 || SideJobGame.UiOpen() || SideJobBridges.Busy())
            {
                HidePanel();
                return;
            }
            var kind = (SideKind)at;
            bool on = SideJobBridges.OnDeskShift();
            int shown = at | (on ? 4 : 0);
            if (shown != _shown)
            {
                _shown = shown;
                _panel.Lines(SideJobText.T(SideJobText.FirmKey(kind)), _desks.Address(kind), null, GameUi.WorldPanel.White());
                _panel.Buttons(on ? "reallife_job_desk_button_off" : "reallife_job_desk_button_on", _onPress, true, null, null, false);
                _panel.For(_desks.Over(kind));
                _panel.Position = _desks.Over(kind);
                if (Verbose)
                {
                    Logger?.Info("Side jobs check (desk panel): at the desk of " + kind + " outside '" + _desks.Shop(kind) + "', " + _desks.Address(kind)
                                 + ": the game's own panel offers to sign " + (on ? "off" : "on") + " on the game's interact key: PASS");
                }
            }
            _panel.Show();
        }

        private void HidePanel()
        {
            if (_shown != int.MinValue)
            {
                _shown = int.MinValue;
                _panel.Hide();
            }
        }

        // The button of the desk's panel: run by the game on its interact key or on a click.
        private void Press()
        {
            _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
            if (_broken || !_loaded)
            {
                return;
            }
            try
            {
                int at = _desks.At;
                if (at < 0)
                {
                    return;
                }
                HidePanel();
                if (SideJobBridges.OnDeskShift())
                {
                    SideJobBridges.SignOff();   // the jobs' own ending: a fare or a run left undone counts as it does on the phone
                    return;
                }
                Offer((SideKind)at);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The desk looks at the vehicle and, if it will do, puts the sign-on on the game's work panel.
        private void Offer(SideKind kind)
        {
            if (_skip != null && _skip.Open)
            {
                return;
            }
            CarController car = SideJobBridges.Car(SideJobRules.CarWithin);
            SideCar facts = SideJobGame.Read(car, out string name, out string id);
            SideRefusal refusal = SideJobRules.Refusal(kind, facts, _state.Left(kind));
            if (refusal != SideRefusal.None)
            {
                Refuse(kind, refusal, facts);
                return;
            }
            var sign = new SignOn(this, kind, car, facts, id, SideJobText.Fill("reallife_job_desk_sign_title", "firm", SideJobText.T(SideJobText.FirmKey(kind))), Labels(kind, facts, name));
            _skip = ShiftSkip.Offer(this, sign);
            if (_skip == null)
            {
                SideJobGame.Tell(NotificationType.Warning, "reallife_job_no_now", 7f);
                Logger?.Info("Side jobs: the game's work panel would not take the sign-on (another panel, or the clock is running).");
                return;
            }
            if (Verbose)
            {
                Logger?.Info("Side jobs check (sign-on): " + kind + " with '" + name + "' (" + facts.Seats + " seats, room for " + facts.Cargo + ", condition " + facts.Condition + "%, fuel "
                             + facts.Fuel + "%) is on the game's own work panel, " + SideJobRules.SignOnMinutes + " game minutes, " + _state.Left(kind) + " left of the desk's day: PASS");
            }
        }

        private void Refuse(SideKind kind, SideRefusal refusal, in SideCar facts)
        {
            SideJobGame.Tell(NotificationType.Warning, SideJobText.RefusalKey(refusal), 9f,
                "metres", SideJobRules.CarWithin.ToString("0", Inv), "seats", SideJobRules.CabSeats.ToString(Inv), "have", facts.Seats.ToString(Inv),
                "condition", facts.Condition.ToString(Inv), "least", (kind == SideKind.Cab ? SideJobRules.CabCondition : SideJobRules.ParcelCondition).ToString(Inv),
                "fuel", SideJobRules.FuelLeast.ToString(Inv), "count", SideJobRules.DayMost(kind).ToString(Inv));
            Logger?.Info("Side jobs: the desk of " + kind + " refused the driver: " + refusal + " (found " + facts.Found + ", motor " + facts.Motor + ", licensed " + facts.Licensed
                         + ", endorsed " + facts.Endorsed + ", seats " + facts.Seats + ", room " + facts.Cargo + ", condition " + facts.Condition + "%, fuel " + facts.Fuel + "%, left today "
                         + _state.Left(kind) + ").");
        }

        // What the work panel says before the start: the vehicle, the terms, the day, and the firm's books of the day before.
        private LabelInfo[] Labels(SideKind kind, in SideCar facts, string name)
        {
            Color32 white = GameUi.WorldPanel.White();
            JobDef def = Def(kind);
            float rate = Prices.Level / 1000f;
            string car, terms;
            if (kind == SideKind.Cab)
            {
                car = SideJobText.Fill("reallife_job_desk_sign_cab_car", "vehicle", name, "seats", facts.Seats.ToString(Inv), "condition", facts.Condition.ToString(Inv),
                    "fuel", facts.Fuel.ToString(Inv));
                terms = SideJobText.Fill("reallife_job_desk_sign_cab_terms", "fare", JobText.Money(SideJobRules.Fare(def.UnitPay, def.PayPerMetre, 600f, SideJobRules.Par(600f), rate)),
                    "share", (SideJobRules.CommissionMille / 10).ToString(Inv), "left", _state.Left(kind).ToString(Inv));
            }
            else
            {
                car = SideJobText.Fill("reallife_job_desk_sign_parcel_car", "vehicle", name, "drops", SideJobRules.Drops(facts.Cargo, false).ToString(Inv), "condition", facts.Condition.ToString(Inv),
                    "fuel", facts.Fuel.ToString(Inv));
                terms = SideJobText.Fill("reallife_job_desk_sign_parcel_terms", "drop", JobText.Money(SideJobRules.DropPay(def.UnitPay, def.PayPerMetre, 300f, rate, 1000, false)),
                    "left", _state.Left(kind).ToString(Inv));
            }
            SideBooks books = Books(kind, _state.Day - 1);
            string firm = SideJobText.Fill(kind == SideKind.Cab ? "reallife_job_desk_sign_cab_books" : "reallife_job_desk_sign_parcel_books", "drivers", books.Drivers.ToString(Inv),
                "gross", JobText.Money(books.Gross), "kept", JobText.Money(books.Kept), "best", JobText.Money(books.Best));
            return new[]
            {
                new LabelInfo(car, white, false), new LabelInfo(terms, white, false), new LabelInfo(firm, white, false),
                new LabelInfo(SideJobText.T("reallife_job_desk_sign_costs"), white, false)
            };
        }

        /// <summary>A day of a firm's other drivers, by the rules the player is paid by, at today's prices. Asked on a press.</summary>
        public SideBooks Books(SideKind kind, int day)
        {
            JobDef def = Def(kind);
            return SideJobRules.Books(_state.Seed, day, kind, Prices.Level, def.UnitPay, def.PayPerMetre, JobCatalogue.LevelBonus);
        }

        private static JobDef Def(SideKind kind)
        {
            return JobCatalogue.Find(kind == SideKind.Cab ? JobCatalogue.Taxi : JobCatalogue.Courier);
        }

        // The ten minutes at the desk are over: once the work panel has closed and the clock is the street's again, the shift starts.
        private void Signed(SignOn sign)
        {
            _skip = null;
            if (!_broken && Running)
            {
                StartCoroutine(Guard.Routine(BeginSoon(sign)));
            }
        }

        private IEnumerator BeginSoon(SignOn sign)
        {
            float until = Time.unscaledTime + StartWait;
            while (Time.unscaledTime < until && !SideJobGame.StreetReady())
            {
                yield return null;   // the panel closes within a frame or two of its clock ending; the street takes a moment more
            }
            try
            {
                Begin(sign.Kind, sign.Car, new DeskShift
                {
                    Cab = sign.Kind == SideKind.Cab, Seats = sign.Facts.Seats, Drops = SideJobRules.Drops(sign.Facts.Cargo, false), Vehicle = sign.Vehicle, Test = false,
                    Left = sign.Kind == SideKind.Cab ? _leftCab : _leftParcels, Booked = sign.Kind == SideKind.Cab ? _bookCab : _bookParcels
                });
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The jobs' director takes the shift, or says why not.
        private bool Begin(SideKind kind, CarController car, DeskShift desk)
        {
            string refused = SideJobBridges.Start(kind, desk);
            if (refused != null)
            {
                SideJobGame.Tell(NotificationType.Warning, refused, 9f, "metres", SideJobRules.CarWithin.ToString("0", Inv), "days", JobCatalogue.SackedDays.ToString(Inv));
                Logger?.Info("Side jobs: the shift of " + kind + " could not start (" + refused + ").");
                return false;
            }
            _on = true;
            _onTest = desk.Test;
            _onKind = kind;
            _onCar = car;
            _fuelLast = car != null ? SideJobGame.Fuel(car) : 0f;
            _burned = 0f;
            _sum = default;
            SideJobGame.Tell(NotificationType.Success, kind == SideKind.Cab ? "reallife_job_desk_started_cab" : "reallife_job_desk_started_parcels", 9f,
                "firm", SideJobText.T(SideJobText.FirmKey(kind)));
            Logger?.Info("Side jobs: signed on at " + kind + (desk.Test ? " by the test button" : string.Empty) + ": " + desk.Seats + " seats, " + desk.Drops + " drop(s) a run, "
                         + _state.Left(kind) + " left of the desk's day.");
            return true;
        }

        // A fare or a run was paid: onto the driver's figures and the shift's sum.
        private void Booked(SideKind kind, DeskTake take)
        {
            if (!_onTest)
            {
                _state.Book(kind, take.Units, take.Gross, take.Kept, take.Tips, take.Net);   // a test's fares are nobody's day
            }
            _sum.Units += take.Units;
            _sum.Gross += take.Gross;
            _sum.Kept += take.Kept;
            _sum.Tips += take.Tips;
            _sum.Net += take.Net;
            _sum.Metres += take.Metres;
        }

        // While a desk shift is on: the tank is read once a second, and the shift's end is noticed.
        private void Watch()
        {
            if (!_on)
            {
                return;
            }
            if (_onCar != null)
            {
                float now = SideJobGame.Fuel(_onCar);
                if (now < _fuelLast)
                {
                    _burned += _fuelLast - now;
                }
                _fuelLast = now;
            }
            if (!SideJobBridges.OnDeskShift())
            {
                SignedOff();
            }
        }

        // The shift is over, however it ended: the fuel it burned is priced at the pump, and one notice says what it came to.
        private void SignedOff()
        {
            _on = false;
            _onCar = null;
            SideKind kind = _onKind;
            int fuel = SideJobRules.FuelCost(_burned, SideJobBridges.PumpCents());
            if (!_onTest)
            {
                _state.Fuel(kind, fuel);
                _state.At(kind).Shifts++;
                Write();
            }
            _onTest = false;
            string firm = SideJobText.T(SideJobText.FirmKey(kind));
            if (_sum.Units <= 0)
            {
                SideJobGame.Tell(NotificationType.Info, "reallife_job_desk_shift_none", 10f, "firm", firm, "fuel", JobText.Money(fuel));
            }
            else
            {
                SideJobGame.Tell(NotificationType.Success, kind == SideKind.Cab ? "reallife_job_desk_shift_cab" : "reallife_job_desk_shift_parcels", 14f,
                    "firm", firm, "units", _sum.Units.ToString(Inv), "gross", JobText.Money(_sum.Gross), "kept", JobText.Money(_sum.Kept), "tips", JobText.Money(_sum.Tips),
                    "net", JobText.Money(_sum.Net), "fuel", JobText.Money(fuel));
            }
            Logger?.Info("Side jobs check (signed off): " + kind + ": " + _sum.Units + " unit(s), customers paid " + _sum.Gross + ", the firm kept " + _sum.Kept + ", tips " + _sum.Tips
                         + ", " + _sum.Net + " reached the driver through the jobs' own slips; " + _burned.ToString("F1", Inv) + " l of fuel burned, " + fuel + " at the pump; "
                         + _state.Left(kind) + " left of the desk's day: " + (_sum.Kept <= _sum.Gross && _sum.Net >= 0 ? "PASS" : "FAIL (the sums do not add up)"));
        }

        /// <summary>The sign-on at a desk as ten minutes on the game's work panel; whole, the shift starts.</summary>
        private sealed class SignOn : ISkippableShift
        {
            private readonly SideJobDirector _director;
            private readonly LabelInfo[] _labels;
            private bool _over;

            public readonly SideKind Kind;
            public readonly CarController Car;
            public readonly SideCar Facts;
            public readonly string Vehicle;

            public SignOn(SideJobDirector director, SideKind kind, CarController car, SideCar facts, string vehicle, string title, LabelInfo[] labels)
            {
                _director = director;
                Kind = kind;
                Car = car;
                Facts = facts;
                Vehicle = vehicle;
                SkipTitle = title;
                _labels = labels;
            }

            public string SkipTitle { get; }

            public int SkipMinutes => SideJobRules.SignOnMinutes;

            public LabelInfo[] SkipLabels() => _labels;

            public void SkipStarted()
            {
            }

            public void SkipWorked(int minutes)
            {
            }

            public void SkipEnded(int minutes, bool whole)
            {
                if (_over)
                {
                    return;
                }
                _over = true;
                if (whole)
                {
                    _director.Signed(this);
                }
                else
                {
                    _director._skip = null;   // cancelled or stopped at the desk: nothing was signed
                }
            }
        }
    }
}
