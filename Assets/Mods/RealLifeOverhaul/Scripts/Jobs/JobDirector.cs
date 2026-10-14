using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using NWH.VehiclePhysics2.Damage;
using PlayerActivity;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Jobs the player takes themselves: one at a time, found on the phone (<see cref="JobPhone"/>),
    /// played on the street. The director owns the job that is on: starts it, ticks it five
    /// times a second, keeps its figures on the HUD (<see cref="ShiftCard"/>), pays through the game's
    /// own transactions, and ends it cleanly whatever happens to the player meanwhile (hospital,
    /// custody, sleep, a time skip, going indoors, a vanilla mission, a load). It keeps the
    /// experience, strikes and daily limits (<see cref="JobState"/>) in the save's modData, and with
    /// them one line for a shift that is on when the game is saved (<see cref="JobSave"/>): a load
    /// settles it at once, so nothing earned is lost and nothing is paid twice.
    ///
    /// With no job on, Update is two comparisons; once a second it looks at the day and the save
    /// hook. With one on, the per-frame work is keeping the card off the screen while a menu is open;
    /// everything else is on the 0.2 s tick. Anything unexpected is logged once, the job is
    /// ended with everything given back, and jobs stay off for the session.
    /// </summary>
    public class JobDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("jobs", "JobDirector", typeof(JobDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float TickInterval = 0.2f;
        private const float CrashSpeed = 3f;         // m/s of closing speed that counts as a crash on the job
        private const float CrashGap = 2.5f;         // seconds between two crashes that both count
        private const float EnergyFloor = 12f;       // work never takes the player below this (the game faints them at zero)
        private const float WorstEnergyMultiplier = 1.9f;
        private const string SalaryCategory = "ba:transactioncategory_salaryincome";
        private static readonly Color GuideColour = new Color(0.20f, 0.62f, 0.95f, 1f);

        public static IModLogger Logger;
        public static JobDirector Instance { get; private set; }

        /// <summary>True while a job's panel has the interaction key (its first button): the mod's own E/F (enter a door, get in a car) stands aside.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._interactFrame == Time.frameCount
                                 || (Instance._job != null && Instance._job.OwnsInteractKey)
                                 || (Instance._job == null && Instance._boards != null && Instance._boards.InReach)
                                 || (Instance._job == null && Instance._career != null && Instance._career.Near >= 0));

        /// <summary>
        /// True while any job is on. Going indoors ends a shift, so the mod's own key never takes the
        /// player through a door meanwhile (the game's own click on a building still does).
        /// </summary>
        public static bool OnShift => Instance != null && Instance._job != null;

        // ---- for the rest of the mod (life events, crime): read-only, never null-dangerous
        /// <summary>The id of the job in progress, or null.</summary>
        public static string ActiveJobId => Instance != null && Instance._job != null ? Instance._job.Def.Id : null;
        /// <summary>The id of the last job the player worked this session, or null.</summary>
        public static string LastJobId { get; private set; }
        /// <summary>What the player has earned from jobs today.</summary>
        public static int EarnedToday => JobState.Loaded ? JobState.EarnedToday : 0;

        private readonly JobContext _context = new JobContext();
        private readonly StringBuilder _text = new StringBuilder(256);
        private JobCast _cast;
        private JobProps _props;
        private JobBody _body;
        private JobStand _stand;
        private JobUniform _uniform;
        private ShiftCard _card;
        private bool _cardFailed;
        private readonly GameUi.WorldPanel _boardPanel = new GameUi.WorldPanel();
        private Action _onBoard;
        private int _boardShown = int.MinValue;   // the board (and employer) the panel's words were last written for
        private JobRental _rental;
        private JobBoards _boards;
        private CareerDirector _career;
        private float _lastInside = -100f;   // when the player was last inside the building the job works in
        private float _guidedTime;
        private Job _job;
        private int _interactFrame = -1;   // the frame in which a board's button took the key, whichever component runs first
        private float _nextTick;
        private float _nextSlow;
        private bool _broken;
        private bool _wasPulledOver;
        private float _momentAt = -1f;   // Time.time at which the job is owed its Moment; negative: none
        private CarController _hookedCar;
        private float _lastCrash = -100f;
        private bool _guided;            // the game's own job guider (arrow, city map) points at the job's marker
        private Vector3 _guidedAt;
        private bool _guiderBroken;

        private bool PrewarmCast()
        {
            return !Settings.Jobs && !Settings.DrivingSchool || _cast.Prewarm(JobCast.Size);
        }

        private void Awake()
        {
            Instance = this;
            JobState.Reset();
            _cast = new JobCast(this, Logger);
            ScenePrewarm.Add("jobs: the two people of job scenes being made", PrewarmCast);
            _context.Director = this;
            _context.Cast = _cast;
            _props = new JobProps(transform);
            _body = new JobBody(_props, Logger);
            _stand = new JobStand(this, _body, Logger);
            _uniform = new JobUniform(Logger);
            SolidFootprint.Reset();
            _onBoard = BoardClicked;
            _rental = new JobRental(Logger);
            _boards = new JobBoards(_props, Logger);
            _career = new CareerDirector(this, Logger);
            CareerState.Reset();
            _context.Props = _props;
            _context.Self = _body;
            _context.Stand = _stand;
            _context.Uniform = _uniform;
            _context.Rental = _rental;
            _context.Animations = new ClipJobAnimations();   // the mod's own clips where the bundle has them, the game's gestures where not
            _context.Logger = Logger;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                if (_job != null)
                {
                    Job job = _job;
                    _job = null;
                    job.End(JobEnd.Interrupted);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Unhook();
            _stand.Clear();
            _body.Release();
            _uniform.Off();       // the player's own clothes, if the city is still there to draw them in
            _uniform.Forget();
            _boardPanel.Hide();
            if (_card != null)
            {
                _card.Destroy();
                _card = null;
            }
            _rental.Dispose();
            _boards.Dispose();
            _career.Dispose();
            CareerState.Reset();
            _cast.Dispose();
            _props.Dispose();
            JobState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Jobs: JobDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_broken)
            {
                // Jobs are off for the session, but a van that was out on hire is still seen back.
                if (Time.unscaledTime >= _nextSlow)
                {
                    _nextSlow = Time.unscaledTime + 1f;
                    _rental.Tick();
                }
                return;
            }
            try
            {
                float now = Time.unscaledTime;
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    Slow(now);
                }
                if (_job == null)
                {
                    return;   // (at a board the game's own panel has the key: its button calls BoardClicked)
                }
                if (_momentAt >= 0f && Time.time >= _momentAt)
                {
                    _momentAt = -1f;
                    _job.Moment(Time.time);   // the instant something changes hands: to the frame, not to the tick
                }
                if (Time.time >= _nextTick)
                {
                    _nextTick = Time.time + TickInterval;
                    Tick(Time.time);
                }
                if (_job != null)
                {
                    Draw();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Jobs: switched off for this session after the error above; whatever was on the street was taken back.");
                _broken = true;
                EndJob(JobEnd.Fault, null);
                HideBoardPanel();
            }
        }

        // ---------------------------------------------------------------- once a second

        private void Slow(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                return;
            }
            if (!JobState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                JobState.Load(SaveGameManager.Current.modData, TimeHelper.CurrentDay);
                _rental.Load(SaveGameManager.Current.modData);
                JobPhone.Setup();
                CareerState.Load(SaveGameManager.Current.modData);
                CareerPhone.Setup();
                CourierJob.ClearLeftovers(Logger);   // a parcel that was in the player's hands when the game was saved
                SettleSaved(SaveGameManager.Current.modData);   // a shift that was on when the game was saved: paid what it was owed, once
                Logger?.Info("Jobs: ready. Reference letter: " + JobState.HasReference + ", earned this week: " + JobState.EarnedThisWeek + ".");
            }

            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

            if (JobState.NewDay(TimeHelper.CurrentDay, out int weekEarned, out int weekShifts) && Settings.Jobs)
            {
                var data = new Dictionary<string, string>(2)
                {
                    { "amount", JobText.Money(weekEarned) }, { "shifts", weekShifts.ToString() }
                };
                Notifications.Show(NotificationType.Info, "reallife_job_week_summary", data, 12f);
            }

            _rental.Tick();
            if (_job == null)
            {
                Boards(gm);
            }
            else
            {
                HideBoardPanel();
                _career.Slow(gm.playerController.transform.position, false, true);   // the rota goes on while a job is worked
            }

        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                if (JobState.Loaded && SaveGameManager.Current != null)
                {
                    if (SaveGameManager.Current.modData == null)
                    {
                        SaveGameManager.Current.modData = new Dictionary<string, string>();
                    }
                    JobState.Save(SaveGameManager.Current.modData);
                    CareerState.Save(SaveGameManager.Current.modData);
                    _rental.Save(SaveGameManager.Current.modData);
                    SaveGameManager.Current.modData[JobSave.Key] = LiveLine();   // empty when no shift is on: a line is never left over from an earlier save
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- a shift across a save (JobSave)

        private readonly SavedShift _saved = new SavedShift();   // one, written over at each save

        // The line a save carries for the shift that is on: what it has been paid and what it is owed now, by the job's own
        // rule for stopping early. Nothing of the shift changes: it goes on after the save. Empty for no shift, a test
        // button's shift (it counts for nothing), or a job whose figures cannot be read.
        private string LiveLine()
        {
            Job job = _job;
            CareerShift shift = _context.Shift;
            if (job == null || _broken || _context.Test || (shift != null && !shift.Counts))
            {
                return string.Empty;
            }
            try
            {
                _saved.JobId = job.Def.Id;
                _saved.Day = TimeHelper.CurrentDay;
                _saved.Minutes = TimeHelper.NowInMinutes();
                _saved.Paid = job.Shift;
                _saved.Owed = job.Unpaid();
                _saved.Rota = shift != null;
                _saved.Employer = shift != null ? shift.Employer.Id : string.Empty;
                _saved.Hourly = shift != null ? shift.Hourly : 0f;
                _saved.Late = shift != null && shift.Late;
                _saved.StartMinutes = shift != null ? shift.StartMinutes : 0f;
                string line = JobSave.Write(_saved);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Jobs check (saved shift): '" + job.Def.Id + "' is on at the save: paid so far " + _saved.Paid.Total + " for " + _saved.Paid.Units + " unit(s), owed "
                                 + _saved.Owed.Total + " for " + _saved.Owed.Units + (shift != null ? ", " + shift.Employer.Id + " at " + shift.Hourly.ToString("F1") + " an hour" : string.Empty)
                                 + "; written to the save: " + (line.Length > 0 ? "PASS" : "FAIL") + ".");
                }
                return line;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return string.Empty;
            }
        }

        // After a load: a shift that was on when the game was saved is settled, once, as one that ended early through
        // nobody's fault. The line is emptied before anything is paid, so whatever happens below it is never settled twice.
        private void SettleSaved(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue(JobSave.Key, out string line) || string.IsNullOrEmpty(line))
            {
                return;
            }
            data[JobSave.Key] = string.Empty;
            try
            {
                JobDef def = JobSave.Read(line, out SavedShift saved) ? JobCatalogue.Find(saved.JobId) : null;
                if (def == null)
                {
                    Logger?.Warn("Jobs check (saved shift): the save's line for a shift in progress does not add up and was dropped (" + line + "): FAIL.");
                    return;
                }
                ShiftSettlement sum = JobSave.Settle(saved);
                Employer employer = saved.Rota ? CareerCatalogue.Find(saved.Employer) : null;
                // What was done and not paid: the game's own transaction, the energy, the skill, the level and the day's amount, as any slip.
                PayOut(def, saved.Owed, def.Id == JobCatalogue.Lead && employer != null && employer.LeadSkill != null ? employer.LeadSkill : def.Skill, true);
                if (saved.Rota)
                {
                    _career.SettleSaved(def, saved, sum);   // the basic wage for the hours clocked, and the shift on the rota's count
                }
                else if (sum.Worked && JobCatalogue.IndexOf(def.Id) >= 0)
                {
                    JobState.ShiftCut(def);   // a shift worked; the strikes stay as they were
                }
                var words = new Dictionary<string, string>(5)
                {
                    { "job", JobText.T(def.NameKey) }, { "amount", JobText.Money(sum.Total) }, { "units", sum.UnitsOwed.ToString() },
                    { "hours", sum.Hours.ToString() }, { "employer", employer != null ? JobText.T(employer.NameKey) : string.Empty }
                };
                Notifications.Show(sum.Total > 0 ? NotificationType.Success : NotificationType.Info,
                    saved.Rota && employer != null ? "reallife_job_saved_rota" : sum.Total > 0 ? "reallife_job_saved_paid" : "reallife_job_saved_nothing", words, 14f);
                Logger?.Info("Jobs check (saved shift): settled '" + def.Id + "' saved on day " + saved.Day + ": paid " + sum.Work + " for " + sum.UnitsOwed + " unit(s) owed"
                             + (saved.Rota ? ", basic wage " + sum.Wage + " for " + sum.Hours + " h at " + saved.Hourly.ToString("F1") : string.Empty)
                             + "; " + saved.Paid.Total + " had been paid before the save; no strike, no mark: PASS.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Jobs check (saved shift): settling the saved shift failed (see above); its line is dropped: FAIL.");
            }
        }

        // ---------------------------------------------------------------- finding and starting work

        /// <summary>The jobs there are, for the phone. Never null.</summary>
        public static JobDef[] Offers => JobCatalogue.All;

        /// <summary>
        /// What every pay is multiplied by for this player: the options page's pay slider, and less for somebody who runs businesses of their own
        /// (<see cref="CareerCatalogue.OwnerFactor"/>). The one place it is worked out: the work is paid by it and every quote of pay is made with it.
        /// </summary>
        public static float PayScale()
        {
            // The pay option, less for somebody who runs businesses of their own, and at today's prices: pay follows the
            // city's price level as every wage does (Core/Prices).
            return Mathf.Clamp(Settings.JobPay, 25, 300) / 100f * CareerCatalogue.OwnerFactor(BusinessGame.RevenueBusinesses()) * Prices.Level / 1000f;
        }

        /// <summary>
        /// Whether a job can be taken right now. If not, the reason is a key the phone can say.
        /// </summary>
        public bool Available(JobDef def, out string reasonKey)
        {
            reasonKey = null;
            if (_broken || !Settings.Jobs || !JobState.Loaded)
            {
                reasonKey = "reallife_job_no_off";
            }
            else if (_job != null)
            {
                reasonKey = "reallife_job_no_busy";
            }
            else if (!Free(out reasonKey))
            {
            }
            else if (JobState.Sacked(def, TimeHelper.CurrentDay))
            {
                reasonKey = "reallife_job_no_sacked";
            }
            else if (GigOffers.DayOf(def.Id, TimeHelper.CurrentDay) == GigDay.Closed)
            {
                reasonKey = "reallife_job_no_today";   // the day's board (GigOffers): not every gig is on every day
            }
            else if (JobState.LeftToday(def) <= 0)
            {
                reasonKey = "reallife_job_no_daily";
            }
            else if (SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                reasonKey = "reallife_job_no_energy";
            }
            else if (def.Id == JobCatalogue.Taxi && !Licences.Driving.Has(LicenceClass.Taxi))   // ride-hail needs the taxi endorsement (Scripts/Licences)
            {
                reasonKey = "reallife_job_no_taxi_licence";
            }
            else if ((def.Needs & JobNeed.Reference) != 0 && !JobState.HasReference)
            {
                reasonKey = "reallife_job_no_reference";
            }
            else if ((def.Needs & JobNeed.CleanRecord) != 0 && !JobState.CleanRecord(TimeHelper.CurrentDay))
            {
                reasonKey = "reallife_job_no_record";
            }
            else if ((def.Needs & JobNeed.Car) != 0 && NearestCar(60f) == null)
            {
                _rental.CanHire(false, out reasonKey);   // no car of their own near: a van can be hired, if they can pay for it
            }
            return reasonKey == null;
        }

        // What must hold for any job, test buttons included: the player is on the street and nothing else has them.
        private static bool Free(out string reasonKey)
        {
            reasonKey = null;
            var uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (uis == null || save == null || LoadScene.isLoading || FuneralHelper.PlayerDead || EnergyHelper.goingToHospital
                || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen || CasinoBoatManager.IsOnCasinoBoat || SubwaySystem.IsRiding)
            {
                reasonKey = "reallife_job_no_now";
            }
            else if (save.currentPlayerMission != null)
            {
                reasonKey = "reallife_job_no_mission";   // the game's own delivery jobs come first, always
            }
            else if (BuildingManager.IsInsideBuilding)
            {
                reasonKey = "reallife_job_no_indoors";
            }
            else if (!TrafficTools.Street)
            {
                reasonKey = "reallife_test_refused_traffic_off";   // out of doors, but the street is not running yet (a menu, fast-forward)
            }
            return reasonKey == null;
        }

        /// <summary>Starts a job from the phone. False with the reason if it cannot be started.</summary>
        public bool Take(JobDef def, out string reasonKey)
        {
            return Available(def, out reasonKey) && StartJob(def, false, out reasonKey);
        }

        /// <summary>Options-page test button: starts a job where the player stands, whatever their level, record or day.</summary>
        public void TestStart(string id)
        {
            JobDef def = JobCatalogue.Find(id);
            if (def == null || _broken || !JobState.Loaded)
            {
                return;
            }
            if (_job != null)
            {
                EndJob(JobEnd.Interrupted, null);
            }
            if (!Free(out string reason) || !StartJob(def, true, out reason))
            {
                Logger?.Info("Jobs: test of '" + id + "' could not start (" + reason + ").");
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
            }
        }

        /// <summary>The player stops the job themselves (phone, or the test button).</summary>
        public void Quit()
        {
            if (_job != null)
            {
                EndJob(JobEnd.Quit, null);
            }
        }

        public bool HasJob => _job != null;
        public JobDef Current => _job != null ? _job.Def : null;
        public int ShiftTotal => _job != null ? _job.Shift.Total : 0;

        /// <summary>One line per job in the catalogue: the only place a job's class is named.</summary>
        private static Job Create(string id)
        {
            switch (id)
            {
                case JobCatalogue.Flyers: return new FlyerJob();
                case JobCatalogue.Courier: return new CourierJob();
                case JobCatalogue.Taxi: return new TaxiJob();
                case JobCatalogue.Cleaner: return new CleanerJob();
                case JobCatalogue.Stocker: return new StockerJob();
                case JobCatalogue.Vendor: return new VendorJob();
                case JobCatalogue.Busker: return new BuskerJob();
                case JobCatalogue.Dispatch: return new DispatchJob();
                case JobCatalogue.Lead: return new LeadJob();
                default: return null;
            }
        }

        private bool StartJob(JobDef def, bool test, out string reasonKey, CareerShift shift = null, BookedGig gig = null)
        {
            reasonKey = "reallife_job_no_now";
            Job job = Create(def.Id);
            if (job == null)
            {
                return false;
            }
            RefreshContext();
            _context.Test = test;
            _context.Shift = shift;
            _context.Gig = gig;
            float scale = PayScale();   // the pay option, and less for somebody who runs businesses of their own
            _context.Level = JobState.Level(def);
            // A freelance gig is offered as the Gig Line quoted it (the same day, hour and gig give the same offer); an employer's shift,
            // a booked set and a test button are worked at the table's figures.
            bool freelance = shift == null && gig == null && !test;
            _context.Offer = freelance ? GigOffers.Draw(def, TimeHelper.CurrentDay, TimeHelper.CurrentHour, GigWords.Sky()) : GigOffer.Table(def);
            _context.Rate = JobCatalogue.PayFactor(_context.Level) * scale * (shift != null ? shift.Rank.Piece : 1f) * _context.Offer.Fee;
            if (shift != null)
            {
                shift.Hourly *= scale;
            }
            bool begun;
            try
            {
                begun = job.Begin(_context);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                begun = false;
            }
            if (!begun)
            {
                reasonKey = job.Refusal ?? "reallife_job_no_here";
                SafeEnd(job, JobEnd.Interrupted);
                Unhook();
                _context.Car = null;
                _context.Body = null;
                _context.Shift = null;
                _context.Gig = null;
                return false;
            }
            _job = job;
            _boards.Away();
            HideBoardPanel();
            LastJobId = def.Id;
            _wasPulledOver = PoliceAttention.PulledOver;
            _momentAt = -1f;
            _nextTick = 0f;
            Logger?.Info("Jobs: '" + def.Id + "' started" + (test ? " by the test button" : string.Empty) + ", level " + _context.Level + ", rate " + _context.Rate.ToString("F2") + ".");
            if (Meta.Dev.Verbose)
            {
                GigWords.Log(Logger, _context.Offer, TimeHelper.CurrentDay, TimeHelper.CurrentHour);
            }
            reasonKey = null;
            return true;
        }

        // ---------------------------------------------------------------- while one is on

        /// <summary>The job that is on is called (Job.Moment) in the frame this time comes. One at a time; negative cancels.</summary>
        public void MomentAt(float time)
        {
            _momentAt = _job != null ? time : -1f;
        }

        private void Tick(float now)
        {
            if (Interrupted(now, out string reasonKey))
            {
                EndJob(JobEnd.Interrupted, reasonKey);
                return;
            }
            var premises = InstanceBehavior<BuildingManager>.Instance;
            if (premises != null && (premises.enteringBuilding || premises.exitingBuilding))
            {
                return;   // being taken through a door: nothing is asked of anyone until they are through
            }
            RefreshContext();
            _body.Tick(now);

            // Signalled to pull over for something they did: not a patrol car still on its way, and not the wait after
            // a crash to see whether the driver stops (that already cost the job a crash).
            bool pulledOver = PoliceAttention.PulledOver;
            if (pulledOver && !_wasPulledOver)
            {
                if (!_context.Test)
                {
                    JobState.PoliceStop(TimeHelper.CurrentDay);
                    if (_context.Shift != null && _context.Shift.Counts)
                    {
                        CareerState.PoliceStop();
                    }
                }
                Logger?.Info("Jobs: the player was signalled to pull over during '" + _job.Def.Id + "'.");
                _job.OnPoliceStop();
            }
            _wasPulledOver = pulledOver;

            _job.Tick(now);
            Guide();
            if (_cast.AnyOut)
            {
                _cast.Cull();
            }
            if (_job.Over)
            {
                EndJob(_job.Outcome, null);
            }
        }

        // Anything that takes the player off the street or out of their own hands ends the job; what was done is paid.
        private bool Interrupted(float now, out string reasonKey)
        {
            reasonKey = null;
            var uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (uis == null || save == null || LoadScene.isLoading)
            {
                return true;
            }
            if (_job.PassingTime)
            {
                _lastInside = now;   // the game's clock is working the shift: the street stops for its time skip and takes a moment to start again
            }
            if (FuneralHelper.PlayerDead || EnergyHelper.goingToHospital)
            {
                reasonKey = "reallife_job_end_hospital";
            }
            else if (uis.timeMachine.isRunning && !_job.PassingTime)
            {
                reasonKey = "reallife_job_end_time";    // custody, sleep, a taxi ride, the game's own work (not the job's own shift left to the clock)
            }
            else if (save.currentPlayerMission != null)
            {
                reasonKey = "reallife_job_end_mission";
            }
            else if (BuildingManager.IsInsideBuilding)
            {
                // Indoors ends a shift, except in the one building a job says it works in (a client's shop).
                if (_job.WorksInside(InstanceBehavior<BuildingManager>.Instance.building))
                {
                    _lastInside = now;
                }
                else
                {
                    reasonKey = "reallife_job_end_indoors";
                }
            }
            else if (!TrafficTools.Street && now - _lastInside > 6f)   // the street takes a moment to start up again outside a door
            {
                reasonKey = "reallife_job_end_indoors";
            }
            else if (save.Energy <= 1f)
            {
                reasonKey = "reallife_job_end_exhausted";
            }
            else if (!Settings.Jobs)
            {
                reasonKey = "reallife_job_end_off";
            }
            return reasonKey != null;
        }

        private void RefreshContext()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            _context.Player = gm.playerController;
            _context.Indoors = BuildingManager.IsInsideBuilding;
            _body.Bind(gm.playerController);
            var car = gm.selectedVehicle as CarController;
            _context.PlayerPosition = car != null ? car.transform.position : gm.playerController.transform.position;
            if (car != _context.Car)
            {
                _context.Car = car;
                _context.Body = car != null ? car.GetComponent<Rigidbody>() : null;   // looked up once per vehicle
                Hook(car);
            }
            _context.Velocity = _context.Body != null ? _context.Body.velocity : Vector3.zero;
            _context.Speed = _context.Velocity.magnitude;
        }

        private void Draw()
        {
            // Everything of a job is in the game's own interface. The one thing done per frame is to keep its figures off
            // the screen while a menu of the game's is open (two comparisons when nothing changes); they stay up behind the
            // game's activity panel, which is the shift being worked.
            if (_card != null)
            {
                _card.Show(!ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen);
            }
        }

        // The game's own destination guider, the one its delivery jobs use: the place also shows on the city map.
        // Moved only when the job's marker moves. If the game refuses it once, the mod's own marker is all there is.
        private void Guide()
        {
            if (_guiderBroken || _job == null)
            {
                return;
            }
            try
            {
                if (!_job.HasMarker || _context.Indoors)   // indoors the mod's own marker points the way: the game's arrow is for streets
                {
                    Unguide();
                }
                else if (!_guided || (_job.MarkerAt - _guidedAt).sqrMagnitude > 1f
                         || (_guidedTime < _rental.SpawnedAt + 1f && Time.time > _rental.SpawnedAt + 1f))   // a van just hired resets the game's guider a frame after it appears
                {
                    _guidedTime = Time.time;
                    GuidersManager.SetGuiderTarget(_job.MarkerAt, _job.MarkerLabel, JobPhone.Icon, GuideColour, DirectionGuiderType.JobDestination);
                    _guided = true;
                    _guidedAt = _job.MarkerAt;
                }
            }
            catch (Exception e)
            {
                _guiderBroken = true;
                _guided = false;
                Logger?.Error(e);
                Logger?.Warn("Jobs: the game's destination guider could not be used; only the mod's own marker is shown.");
            }
        }

        private void Unguide()
        {
            if (!_guided)
            {
                return;
            }
            _guided = false;
            try
            {
                // Only ours to clear while no delivery of the game's own is using it.
                if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission == null)
                {
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                }
            }
            catch (Exception e)
            {
                _guiderBroken = true;
                Logger?.Error(e);
            }
        }

        // A screen of the game's has the keys: its menus, and the face-to-face dialogue a board opens.
        private static bool UiOpen()
        {
            if (ThirdPersonCamera.IsUiOpen())
            {
                return true;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen;
        }

        // ---------------------------------------------------------------- crashes on the job

        private void Hook(CarController car)
        {
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                car.vehicleController.onCollision.AddListener(OnCollision);
                _hookedCar = car;
            }
        }

        private void Unhook()
        {
            if (_hookedCar != null && _hookedCar.vehicleController != null)
            {
                _hookedCar.vehicleController.onCollision.RemoveListener(OnCollision);
            }
            _hookedCar = null;
        }

        private void OnCollision(Collision collision)
        {
            if (_job == null || _broken || Time.time - _lastCrash < CrashGap)
            {
                return;
            }
            try
            {
                float speed = collision.relativeVelocity.magnitude;
                if (speed < CrashSpeed || !DamageHandler.IsCollisionValid(collision))
                {
                    return;
                }
                _lastCrash = Time.time;
                _job.OnCrash(speed);
                if (_context.Shift != null && _context.Shift.Counts)
                {
                    CareerState.Crash();   // what the boss reads out at the next review
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- pay

        /// <summary>
        /// Pays a slip: the game's own transaction under the job's name, the units counted
        /// towards the level and the day, energy and skill through the game's own systems,
        /// and the slip on screen. A slip that comes to nothing is still shown, so the player
        /// sees why.
        /// </summary>
        public void Pay(Job job, in PaySlip given, string titleKey)
        {
            PaySlip slip = given;
            job.TakeEventMoney(ref slip);   // a big tip or a speed bonus earned since the last slip goes on this one
            JobDef def = job.Def;
            int total = slip.Total;
            job.Shift.Add(slip);
            // A shift in charge trains what the employer's line of work calls for (logistics at a depot, people elsewhere).
            // Work done by a test button is paid (so the slip can be seen) but earns no experience.
            PayOut(def, slip, def.Id == JobCatalogue.Lead && _context.Shift != null && _context.Shift.Employer.LeadSkill != null ? _context.Shift.Employer.LeadSkill : def.Skill,
                !_context.Test);
            // (one notification for the whole shift, when it ends: see SlipNote)
            Logger?.Info("Jobs: '" + def.Id + "' paid " + total + " for " + JobText.T(titleKey) + " (base " + slip.Base + ", distance " + slip.Distance + ", tips " + slip.Tips
                         + ", bonus " + slip.Bonus + ", penalties " + slip.Penalty + ") for " + slip.Units + " unit(s).");
        }

        // What a slip does, for a shift that is on and for one settled from a save: the money through the game's own
        // transaction, the energy and the skill through the game's own systems, the units towards the level and the day.
        private static void PayOut(JobDef def, in PaySlip slip, string skill, bool counts)
        {
            int total = slip.Total;
            if (total > 0)
            {
                GameManager.ChangeMoneySafe(total, new TransactionInfo(def.TransactionKey, SalaryCategory), null, null, force: true);
            }
            if (slip.Units <= 0)
            {
                return;
            }
            SpendEnergy(def.UnitEnergy * slip.Units);
            if (skill != null && def.UnitSkill > 0f)
            {
                EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
                if (self != null)
                {
                    self.IncreaseSkill(skill, def.UnitSkill * slip.Units);
                }
            }
            if (counts && JobState.Worked(def, slip.Units, total))
            {
                int level = JobState.Level(def);
                JobText.Toast(NotificationType.Success,
                    level == JobCatalogue.ReferenceLevel ? "reallife_job_level_reference" : "reallife_job_level_up", "level", level.ToString(), 10f);
            }
        }

        // Tiring, never dangerous: work stops short of the energy at which the game takes over.
        private static void SpendEnergy(float amount)
        {
            float room = (SaveGameManager.Current.Energy - EnergyFloor) / WorstEnergyMultiplier;
            amount = Mathf.Min(amount, room);
            if (amount > 0f)
            {
                EnergyHelper.SpentEnergyOnce(amount);
            }
        }

        /// <summary>
        /// The pay slip of a gig shown in the game's own interface: one of the game's notifications, which also stays
        /// in the game's list of them. Said even when it comes to nothing, so the player sees why.
        /// </summary>
        private void SlipNote(Job job)
        {
            PaySlip slip = job.Shift;
            var data = new Dictionary<string, string>(3)
            {
                { "job", JobText.T(job.Def.NameKey) }, { "total", JobText.Money(slip.Total) }, { "lines", SlipLines(slip) }
            };
            Notifications.Show(slip.Total > 0 ? NotificationType.Success : NotificationType.Info, "reallife_job_slip_note", data, 14f);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Jobs: pay slip shown as one of the game's notifications: " + job.Def.Id + ", " + slip.Total + " (" + data["lines"] + ").");
            }
        }

        /// <summary>A slip's lines that are not zero, as one line of words: "Work $66, Tips $14".</summary>
        public string SlipLines(in PaySlip slip)
        {
            _text.Length = 0;
            Part("reallife_job_slip_base", slip.Base, false);
            Part("reallife_job_slip_distance", slip.Distance, false);
            Part("reallife_job_slip_tips", slip.Tips, false);
            Part("reallife_job_slip_bonus", slip.Bonus, false);
            Part("reallife_job_slip_penalty", slip.Penalty, true);
            return _text.Length > 0 ? _text.ToString() : JobText.Money(0f);
        }

        private void Part(string key, int amount, bool minus)
        {
            if (amount != 0)
            {
                _text.Append(_text.Length > 0 ? ", " : string.Empty).Append(JobText.T(key)).Append(' ').Append(minus ? "-" : string.Empty).Append(JobText.Money(amount));
            }
        }

        /// <summary>The copy of the game's job panel that a job writes its figures on: made the first time one asks, null if it cannot be made.</summary>
        public ShiftCard Card()
        {
            if ((_card == null || !_card.Alive) && !_cardFailed)
            {
                _card = ShiftCard.Make(Logger);
                _cardFailed = _card == null;
            }
            return _card;
        }

        // ---------------------------------------------------------------- ending

        private void EndJob(JobEnd why, string reasonKey)
        {
            Job job = _job;
            if (job == null)
            {
                return;
            }
            _job = null;
            Unhook();
            Unguide();
            _context.Car = null;
            _context.Body = null;
            bool failed = why == JobEnd.Failed || (why == JobEnd.Quit && job.QuitIsFailure);
            if (failed && HomeHealth.SickNoteActive)
            {
                failed = false;   // a doctor's sick note (Home/DoctorVisit): a shift lost while it runs is no strike
                JobText.Toast(NotificationType.Info, "reallife_doctor_note_used");
            }
            SafeEnd(job, why);
            if (why != JobEnd.Fault && job.HasEventMoney)
            {
                try
                {
                    Pay(job, default, "reallife_job_slip_events");   // a tip earned after the job's last slip
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _cast.ReleaseAll();   // whoever a job forgot
            _stand.Clear();       // the place of work lets go of the player
            _body.Release();      // the player is their own again, whatever the job was doing with them
            _uniform.Off();       // ...and in their own clothes
            if (_card != null)
            {
                _card.Show(false);
            }
            _rental.Return();     // a hired van goes back once the player is out of it
            CareerShift shift = _context.Shift;
            _context.Shift = null;
            _context.Gig = null;
            if (shift != null)
            {
                _career.ShiftEnded(job, shift, why, failed);   // the basic wage and the payslip, after the work's own slip
            }
            else
            {
                try
                {
                    SlipNote(job);   // also after a fault: what was earned before it is paid and said
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            try
            {
                JobDef def = job.Def;
                Logger?.Info("Jobs: '" + def.Id + "' ended (" + why + (reasonKey != null ? ", " + reasonKey : string.Empty) + "), " + job.Shift.Units
                             + " unit(s), " + job.Shift.Total + " earned this shift.");
                if (reasonKey != null)
                {
                    JobText.Toast(NotificationType.Warning, reasonKey, "amount", JobText.Money(job.Shift.Total), 9f);
                }
                if (why == JobEnd.Fault || _context.Test || !JobState.Loaded)
                {
                    return;   // a test or a fault never counts for or against the player
                }
                if ((job.Shift.Units > 0 || failed) && JobCatalogue.IndexOf(def.Id) >= 0)   // the two career-only kinds keep no strikes: the employer keeps marks
                {
                    if (JobState.ShiftEnded(def, failed, TimeHelper.CurrentDay))
                    {
                        JobText.Toast(NotificationType.Error, "reallife_job_sacked", "days", JobCatalogue.SackedDays.ToString(), 12f);
                        ModState.AddModifier(ModState.MoodDownBig);
                    }
                    else if (failed)
                    {
                        JobText.Toast(NotificationType.Warning, "reallife_job_strike", "left",
                            (JobCatalogue.StrikesToSack - JobState.Of(def).Strikes).ToString(), 10f);
                        ModState.AddModifier(ModState.MoodDown);
                    }
                    else if (job.Shift.Tips > 0 || why == JobEnd.Done)
                    {
                        ModState.AddModifier(ModState.MoodUp);   // a good shift: tipped, or seen through to the end
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void SafeEnd(Job job, JobEnd why)
        {
            try
            {
                job.End(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- shared lookups

        // One of the game's job vans is nobody's car for a job, except the one hired for this shift.
        private static bool Usable(CarController car)
        {
            return !(car.vehicleInstance is Vehicles.DeliveryDriverJob.DeliveryVehicleInstance)
                   || (Instance != null && ReferenceEquals(Instance._rental.Car, car));
        }

        /// <summary>
        /// The car a driving job is worked with: the player's own if one is near, else a hired
        /// van put at the roadside near them (the day's fee is charged the first time). Null if neither.
        /// </summary>
        public CarController CarFor(bool test, out string reasonKey)
        {
            reasonKey = null;
            CarController own = NearestCar(60f);
            if (own != null)
            {
                return own;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                reasonKey = "reallife_job_no_car";
                return null;
            }
            return _rental.Hire(gm.playerController.transform.position, test, out reasonKey);
        }

        // ---------------------------------------------------------------- help-wanted boards

        // Once a second while no job is on: where the boards stand, and whether the player is at one.
        private void Boards(GameManager gm)
        {
            bool onFoot = Settings.Jobs && JobState.Loaded && !BuildingManager.IsInsideBuilding && gm.selectedVehicle == null && TrafficTools.Street;
            if (!onFoot)
            {
                _boards.Away();
            }
            else
            {
                _boards.Tick(gm.playerController.transform.position);
            }
            _career.Slow(gm.playerController.transform.position, onFoot, false);   // the employers behind the boards: their bosses, the rota
            // At an employer's office the game's own panel over the board offers to speak to its boss; at any other
            // board, to read it. The game writes the key after the button's words and runs the button on it or on a click.
            int employer = CareerDirector.On ? _career.Near : -1;
            int board = employer >= 0 ? _career.DepotOf(employer) : _boards.InReach ? _boards.Near : -1;
            if (board < 0 || board >= _boards.Count || UiOpen())
            {
                HideBoardPanel();
                return;
            }
            int shown = board | ((employer + 1) << 12);
            if (shown != _boardShown)
            {
                _boardShown = shown;
                _boardPanel.Lines(employer >= 0 ? JobText.T(CareerCatalogue.All[employer].NameKey) : JobText.T("reallife_job_board_title"), _boards.Address(board), null,
                    GameUi.WorldPanel.White());
                _boardPanel.Buttons(employer >= 0 ? "reallife_career_board_speak" : "reallife_job_board_read", _onBoard, true, null, null, false);
                _boardPanel.Position = _boards.At(board) + Vector3.up * 1.9f;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Jobs check (board panel): at the board of " + _boards.Address(board) + (employer >= 0 ? " (" + CareerCatalogue.All[employer].Id + "'s office)" : string.Empty)
                                 + ": the game's own panel offers the board on the game's interact key.");
                }
            }
            _boardPanel.Show();
        }

        private void HideBoardPanel()
        {
            if (_boardShown != int.MinValue)
            {
                _boardShown = int.MinValue;
                _boardPanel.Hide();
            }
        }

        // The button of the board's panel: run by the game on its interact key or on a click.
        private void BoardClicked()
        {
            if (_job == null && !_broken)
            {
                _interactFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                OpenBoard();
            }
        }

        // ---------------------------------------------------------------- careers (Jobs/Career*)

        public CareerDirector Career => _career;
        public JobBoards HelpBoards => _boards;
        public IJobAnimations Animations => _context.Animations;

        /// <summary>True while the job that is on is a rota shift for an employer.</summary>
        public bool OnRotaShift => _job != null && _context.Shift != null;

        /// <summary>
        /// Starts a rota shift's work for an employer where the player stands. A test ends whatever job is on first
        /// and waives energy; otherwise the same conditions as any job taken from the phone.
        /// </summary>
        public bool StartShift(JobDef def, CareerShift shift, bool test, out string reasonKey)
        {
            reasonKey = null;
            if (_broken || !Settings.Jobs || !JobState.Loaded)
            {
                reasonKey = "reallife_job_no_off";
                return false;
            }
            if (_job != null)
            {
                if (!test)
                {
                    reasonKey = "reallife_job_no_busy";
                    return false;
                }
                EndJob(JobEnd.Interrupted, null);
            }
            if (!Free(out reasonKey))
            {
                return false;
            }
            if (!test && SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                reasonKey = "reallife_job_no_energy";
                return false;
            }
            return StartJob(def, test, out reasonKey, shift, null);
        }

        /// <summary>Starts a set the agency booked, at its venue.</summary>
        public bool StartGig(BookedGig gig, out string reasonKey)
        {
            reasonKey = null;
            JobDef def = JobCatalogue.Find(JobCatalogue.Busker);
            if (_broken || !Settings.Jobs || !JobState.Loaded || _job != null || def == null)
            {
                reasonKey = "reallife_job_no_busy";
                return false;
            }
            return Free(out reasonKey) && StartJob(def, false, out reasonKey, null, gig);
        }

        // ---------------------------------------------------------------- test buttons for careers

        private int _testEmployer = -1;

        /// <summary>Options page: signs on with the next employer in turn (leaving the current one with no mark), on a rota that works today.</summary>
        public void TestSignOn()
        {
            if (!CareerTestReady())
            {
                return;
            }
            _testEmployer = (_testEmployer + 1) % CareerCatalogue.All.Length;
            int day = TimeHelper.CurrentDay;
            CareerRota rota = CareerCatalogue.Rotas[0];
            for (int i = 0; i < CareerCatalogue.Rotas.Length; i++)
            {
                if (CareerCatalogue.RotaDay(CareerCatalogue.Rotas[i].Days, day + 1))
                {
                    rota = CareerCatalogue.Rotas[i];   // the first shift is tomorrow
                }
            }
            _career.SignOn(_testEmployer, rota, true);
        }

        /// <summary>Options page: the review is due now; it is held with the boss (or by phone where the employer has no office).</summary>
        public void TestReview()
        {
            if (!CareerTestReady())
            {
                return;
            }
            if (!CareerState.Employed)
            {
                TestRefusals.Tell(Logger, "Career review", TestRefusal.NothingFits, 0, "not employed: sign on first");
                JobText.Toast(NotificationType.Warning, "reallife_career_no_employer", 8f);
                return;
            }
            CareerState.ForceReview();
            Employer employer = CareerState.Employer;
            var data = new Dictionary<string, string>(2) { { "boss", JobText.T(employer.BossKey) }, { "employer", JobText.T(employer.NameKey) } };
            Notifications.Show(NotificationType.Info, "reallife_career_review_due", data, 10f);
            if (!BuildingManager.IsInsideBuilding && !UiOpen() && Free(out _))
            {
                CareerPhone.OpenInPerson(CareerState.EmployerIndex);   // the tester need not walk to the office
            }
        }

        /// <summary>Options page: up one rank at the current employer.</summary>
        public void TestPromote()
        {
            if (!CareerTestReady())
            {
                return;
            }
            if (!CareerState.Employed || !CareerState.Promote())
            {
                TestRefusals.Tell(Logger, "Career promotion", TestRefusal.NothingFits, 0, CareerState.Employed ? "already at the top rank" : "not employed");
                JobText.Toast(NotificationType.Warning, CareerState.Employed ? "reallife_career_test_top" : "reallife_career_no_employer", 8f);
                return;
            }
            JobText.Toast(NotificationType.Success, "reallife_career_test_promoted", "title", JobText.T(CareerState.CurrentRank.TitleKey), 9f);
        }

        /// <summary>
        /// Options page: a shift at a given kind of work here and now, on no record. The dispatcher's desk is the
        /// courier firm's; a lead shift is the current employer's if it has one, else the retail chain's; a rota
        /// shift is whatever the current rank works.
        /// </summary>
        public void TestShift(CareerWork work, bool current)
        {
            if (!CareerTestReady())
            {
                return;
            }
            Employer employer;
            CareerRank rank;
            if (current)
            {
                if (!CareerState.Employed)
                {
                    TestRefusals.Tell(Logger, "Career shift", TestRefusal.NothingFits, 0, "not employed: sign on first");
                    JobText.Toast(NotificationType.Warning, "reallife_career_no_employer", 8f);
                    return;
                }
                employer = CareerState.Employer;
                rank = CareerState.CurrentRank;
            }
            else
            {
                employer = CareerCatalogue.Find(work == CareerWork.Dispatch ? CareerCatalogue.Courier : CareerCatalogue.Retail);
                if (CareerState.Employed && CareerState.CurrentRank.Work == work)
                {
                    employer = CareerState.Employer;
                }
                rank = null;
                for (int i = 0; i < employer.Ranks.Length && rank == null; i++)
                {
                    rank = employer.Ranks[i].Work == work ? employer.Ranks[i] : null;
                }
            }
            string reason = null;
            if (rank == null || !_career.TestShift(employer, rank, out reason))
            {
                Logger?.Info("Careers: test shift could not start (" + reason + ").");
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now", 8f);
            }
        }

        /// <summary>Options page: the agency books a set at a venue close by.</summary>
        public void TestGig()
        {
            if (!CareerTestReady())
            {
                return;
            }
            if (_career.GigPending || !_career.BookGig(true))
            {
                TestRefusals.Tell(Logger, "Booked set", TestRefusal.NothingFits, 0, _career.GigPending ? "a set is already booked" : "no open business with a clear front within 160 m");
                return;
            }
            Notifications.Show(NotificationType.Info, "reallife_career_gig_booked_toast",
                new Dictionary<string, string>(2) { { "venue", _career.GigVenue }, { "fee", JobText.Money(_career.GigFee) } }, 10f);
        }

        private bool CareerTestReady()
        {
            TestRefusal why = _broken || !JobState.Loaded || !CareerState.Loaded ? TestRefusal.NotLoaded
                : !CareerDirector.On ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Career", why);
                return false;
            }
            return true;
        }

        // The same list the Gig Line reads out, face to face with the board.
        private void OpenBoard()
        {
            try
            {
                if (!Free(out string reason))
                {
                    JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
                    return;
                }
                HideBoardPanel();
                int employer = CareerDirector.On ? _career.Near : -1;
                _boards.Away();
                if (employer >= 0)
                {
                    CareerPhone.OpenInPerson(employer);   // the office behind the board: its boss, and through them the board's own list
                }
                else
                {
                    JobPhone.OpenInPerson();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// The player's car for a job: the one being driven, else the nearest of their own cars
        /// within a distance. Scooters, hand trucks and the game's job van do not count; a van
        /// hired for the shift does. Null if none.
        /// </summary>
        public static CarController NearestCar(float within)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                return null;
            }
            if (gm.selectedVehicle is CarController driven && Usable(driven))
            {
                return driven;
            }
            Vector3 from = gm.playerController.transform.position;
            CarController best = null;
            float bestSqr = within * within;
            List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < own.Count; i++)
            {
                if (!(own[i] is CarController car) || !car.isActiveAndEnabled || car.vehicleInstance == null || !GoingPublic.MineVehicle(car) || !Usable(car))
                {
                    continue;
                }
                float sqr = (car.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = car;
                }
            }
            return best;
        }
    }
}
