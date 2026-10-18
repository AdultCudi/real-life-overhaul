using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Helpers;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Jobs the player takes themselves, one at a time: starts the one that is on, ticks it, pays it through the game's own
    /// transactions and ends it cleanly whatever happens to the player. Experience, strikes and daily limits
    /// (<see cref="JobState"/>) and a shift on at a save (<see cref="JobSave"/>) are kept in the save's modData.
    /// Cost: idle, two comparisons a frame and a one-second step; with a job on, one menu test a frame and a 0.2 s tick.
    /// A fault ends the job, gives everything back and turns jobs off for the session.
    /// </summary>
    public partial class JobDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("jobs", "JobDirector", typeof(JobDirector));

        private const float TickInterval = 0.2f;   // seconds

        public static IModLogger Logger;
        public static JobDirector Instance { get; private set; }

        /// <summary>True while a job's panel has the interaction key (its first button): the mod's own E/F (enter a door, get in a car) stands aside.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._interactFrame == Time.frameCount
                                 || (Instance._job != null && Instance._job.OwnsInteractKey)
                                 || (Instance._job == null && Instance._boards != null && Instance._boards.InReach)
                                 || (Instance._job == null && Instance._career != null && Instance._career.Near >= 0));

        /// <summary>True while a job is on. Going indoors would end it, so the mod's own door key stands aside.</summary>
        public static bool OnShift => Instance != null && Instance._job != null;

        // For the rest of the mod: safe to read at any time.
        /// <summary>The id of the job in progress, or null.</summary>
        public static string ActiveJobId => Instance != null && Instance._job != null ? Instance._job.Def.Id : null;
        /// <summary>The id of the last job the player worked this session, or null.</summary>
        public static string LastJobId { get; private set; }
        public static int EarnedToday => JobState.Loaded ? JobState.EarnedToday : 0;

        private readonly JobContext _context = new JobContext();
        private JobCast _cast;
        private JobProps _props;
        private JobBody _body;
        private JobStand _stand;
        private JobUniform _uniform;
        private ShiftCard _card;
        private bool _cardFailed;
        private JobRental _rental;
        private JobBoards _boards;
        private CareerDirector _career;
        private float _lastInside = -100f;   // when the player was last inside the building the job works in
        private Job _job;
        private int _interactFrame = -1;   // the frame in which a board's button took the key, whichever component runs first
        private float _nextTick;
        private float _nextSlow;
        private bool _broken;
        private bool _wasPulledOver;
        private float _momentAt = -1f;   // Time.time at which the job is owed its Moment; negative: none

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
            if (_job != null)
            {
                Job job = _job;
                _job = null;
                SafeEnd(job, JobEnd.Interrupted);
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
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
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
                    return;   // at a board the game's panel button calls BoardClicked
                }
                if (_momentAt >= 0f && Time.time >= _momentAt)
                {
                    _momentAt = -1f;
                    _job.Moment(Time.time);   // to the frame, not to the tick
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
                SettleSaved(SaveGameManager.Current.modData);   // a shift on at the save: paid what it was owed, once
                Logger?.Info("Jobs: ready. Reference letter: " + JobState.HasReference + ", earned this week: " + JobState.EarnedThisWeek + ".");
            }

            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // one relay in the game's save event: allocates nothing while it is unchanged

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

        private Action _onSave;   // made once: Core/SaveHook compares it

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
                    SaveGameManager.Current.modData[JobSave.Key] = LiveLine();   // empty when no shift is on: no line is left from an earlier save
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The jobs there are, for the phone. Never null.</summary>
        public static JobDef[] Offers => JobCatalogue.All;

        /// <summary>The factor on every pay and quote: the pay option, less for owners (<see cref="CareerCatalogue.OwnerFactor"/>), at the city's price level.</summary>
        public static float PayScale()
        {
            return Mathf.Clamp(Settings.JobPay, 25, 300) / 100f * CareerCatalogue.OwnerFactor(BusinessGame.RevenueBusinesses()) * Prices.Level / 1000f;
        }

        /// <summary>Whether a job can be taken now; if not, the reason as a key the phone can say.</summary>
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
                reasonKey = "reallife_job_no_today";   // not every gig is on every day
            }
            else if (JobState.LeftToday(def) <= 0)
            {
                reasonKey = "reallife_job_no_daily";
            }
            else if (SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                reasonKey = "reallife_job_no_energy";
            }
            else if (def.Id == JobCatalogue.Taxi && !Licences.Driving.Has(LicenceClass.Taxi))   // ride-hail needs the taxi endorsement
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
                _rental.CanHire(false, out reasonKey);   // no car near: a van can be hired, if they can pay for it
            }
            return reasonKey == null;
        }

        // What must hold for any job, test buttons included: the player is on the street and nothing else has them.
        private static bool Free(out string reasonKey)
        {
            reasonKey = null;
            var uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (uis == null || save == null || LoadScene.isLoading || GameMembers.PlayerDead || EnergyHelper.goingToHospital
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
                reasonKey = "reallife_test_refused_traffic_off";   // out of doors, but the street is not running yet
            }
            return reasonKey == null;
        }

        /// <summary>Starts a job from the phone. False with the reason if it cannot be started.</summary>
        public bool Take(JobDef def, out string reasonKey)
        {
            return Available(def, out reasonKey) && StartJob(def, false, out reasonKey);
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
            float scale = PayScale();
            _context.Level = JobState.Level(def);
            // A freelance gig is worked as the Gig Line quoted it; the rest at the table's figures.
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
            Logger?.Info("Jobs: '" + def.Id + "' started" + (test ? " by the test button" : string.Empty) + ", level " + _context.Level + ", rate "
                + _context.Rate.ToString("F2", CultureInfo.InvariantCulture) + ".");
            if (Meta.Dev.Verbose)
            {
                GigWords.Log(Logger, _context.Offer, TimeHelper.CurrentDay, TimeHelper.CurrentHour);
            }
            reasonKey = null;
            return true;
        }

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

            // Signalled to pull over for something done: not a patrol on its way, nor the wait after a crash, which cost the job already.
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
            // The one per-frame task: the card hides under a game menu, but stays up behind the activity panel.
            if (_card != null)
            {
                _card.Show(!ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen);
            }
        }

        public CareerDirector Career => _career;
        public JobBoards HelpBoards => _boards;
        public IJobAnimations Animations => _context.Animations;

        public bool OnRotaShift => _job != null && _context.Shift != null;

        /// <summary>Starts a rota shift's work where the player stands. A test ends any job first and waives the energy needed.</summary>
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

        public bool StartGig(BookedGig gig, out string reasonKey)
        {
            reasonKey = null;
            JobDef def = JobCatalogue.Find(JobCatalogue.Busker);
            if (_broken || !Settings.Jobs || !JobState.Loaded || def == null)
            {
                reasonKey = "reallife_job_no_off";
                return false;
            }
            if (_job != null)
            {
                reasonKey = "reallife_job_no_busy";
                return false;
            }
            return Free(out reasonKey) && StartJob(def, false, out reasonKey, null, gig);
        }
    }
}
