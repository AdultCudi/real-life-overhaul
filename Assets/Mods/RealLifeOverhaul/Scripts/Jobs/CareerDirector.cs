using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using UI.Guiders;
using UI.Notification;
using Vehicles.VehicleTypes;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The career ladder (CareerCatalogue, CareerState) put into the game: where each employer is
    /// (one of the help-wanted boards: its office is the building the board stands at), the boss
    /// who stands by that board when the player comes near, the rota (reminders, lateness,
    /// no-shows, sick notes), clocking on to a shift, the payslip, reviews, notice and dismissal,
    /// and the agency's booked sets.
    ///
    /// Owned and called by <see cref="JobDirector"/>: once a second (<see cref="Slow"/>), which is
    /// a few comparisons unless the game's hour has changed or the player is within sight of a
    /// depot. It has a cast of its own (the boss and one colleague: two characters, made the
    /// first time they are needed, shelved when the player leaves), never the jobs' two.
    /// </summary>
    public sealed class CareerDirector
    {
        private const float BossAppears = 75f;      // metres from a depot at which its boss is put out, if that can be done unseen
        private const float BossNotNearer = 22f;    // ...and never in view nearer than this
        private const float BossGoes = 100f;
        private const float BossReach = 2.4f;
        private const float DeskReach = 45f;        // desk and lead shifts are clocked on to this near the depot
        private const float GigReach = 14f;
        private const string WageKey = "reallife_transaction_career_wage";
        private const string SalaryCategory = "ba:transactioncategory_salaryincome";
        private static readonly Color GuideColour = new Color(0.20f, 0.62f, 0.95f, 1f);

        private readonly JobDirector _director;
        private readonly IModLogger _logger;
        private readonly JobCast _cast;
        private readonly int[] _depot = new int[CareerCatalogue.All.Length];   // the board each employer is at, or -1
        private readonly CareerShift _shift = new CareerShift();
        private readonly StringBuilder _text = new StringBuilder(256);
        private bool _mapped;
        private bool _failed;

        private StreetActor _boss;
        private int _bossOf = -1;
        private Vector3 _bossAt;
        private StreetActor _mate;         // on a rota day: whoever is on the same shift, waiting at the office
        private int _mateIndex;
        private bool _mateGreeted;
        private bool _greeted;
        private int _noRoomAt = -1;        // a depot whose boss could not be stood anywhere: not asked again this visit
        private int _lastHour = -1;
        private bool _dismissAfter;
        private string[] _prompts;
        private int _line = -1;

        private BookedGig _gig;
        private int _gigDay;
        private bool _gigGuided;
        private float _gigRetry;

        public CareerDirector(JobDirector director, IModLogger logger)
        {
            _director = director;
            _logger = logger;
            _cast = new JobCast(director, logger);
            for (int i = 0; i < _depot.Length; i++)
            {
                _depot[i] = -1;
            }
        }

        public static bool On => Settings.Jobs && Settings.Careers;

        /// <summary>The employer at whose depot the player stands (by its board or its boss), or -1.</summary>
        public int Near { get; private set; } = -1;

        /// <summary>The key prompt at a depot: "speak to ...". Null when not at one.</summary>
        public string Prompt => Near >= 0 && _prompts != null ? _prompts[Near] : null;

        /// <summary>True while a booked set is waiting to be played.</summary>
        public bool GigPending => _gig != null;

        public string GigVenue => _gig != null ? _gig.VenueName : null;

        public void Dispose()
        {
            Unguide();
            _boss = null;
            _cast.Dispose();
        }

        // ---------------------------------------------------------------- where the employers are

        /// <summary>The board an employer's office is at, or -1 if it has none in this city.</summary>
        public int DepotOf(int employer)
        {
            return employer >= 0 && employer < _depot.Length ? _depot[employer] : -1;
        }

        /// <summary>The employer whose office is at a board, or -1.</summary>
        public int EmployerAt(int board)
        {
            for (int i = 0; i < _depot.Length; i++)
            {
                if (_depot[i] == board && board >= 0)
                {
                    return i;
                }
            }
            return -1;
        }

        public bool DepotPlace(int employer, out Vector3 place, out string address)
        {
            JobBoards boards = _director.HelpBoards;
            int board = DepotOf(employer);
            if (boards == null || board < 0 || board >= boards.Count)
            {
                place = default;
                address = null;
                return false;
            }
            place = boards.Front(board);
            address = boards.Address(board);
            return true;
        }

        // Each employer takes the board at the kind of building it would have its office in, best match first; the rest
        // take what is left, in order. The boards are the same in every game, so the employers are too.
        private void Map(JobBoards boards)
        {
            _mapped = true;
            int count = boards.Count;
            var taken = new bool[count];
            Employer[] all = CareerCatalogue.All;
            for (int e = 0; e < all.Length; e++)
            {
                _depot[e] = -1;
                for (int p = 0; p < all[e].Premises.Length && _depot[e] < 0; p++)
                {
                    for (int b = 0; b < count; b++)
                    {
                        if (!taken[b] && boards.Kind(b) != null && boards.Kind(b).IndexOf(all[e].Premises[p], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _depot[e] = b;
                            taken[b] = true;
                            break;
                        }
                    }
                }
            }
            for (int e = 0; e < all.Length; e++)
            {
                for (int b = 0; b < count && _depot[e] < 0; b++)
                {
                    if (!taken[b])
                    {
                        _depot[e] = b;
                        taken[b] = true;
                    }
                }
            }
            _prompts = new string[all.Length];
            _text.Length = 0;
            for (int e = 0; e < all.Length; e++)
            {
                _prompts[e] = JobText.T("reallife_career_prompt_boss", "boss", JobText.T(all[e].BossKey), "employer", JobText.T(all[e].NameKey));
                _text.Append(all[e].Id).Append(_depot[e] >= 0 ? " at " + boards.Address(_depot[e]) + " (" + boards.Kind(_depot[e]) + ")" : " has no office").Append("; ");
            }
            _logger?.Info("Careers: " + _text);
        }

        // ---------------------------------------------------------------- once a second

        /// <param name="onFoot">The player is on foot out of doors with no job on: a boss may be put out and spoken to.</param>
        public void Slow(Vector3 player, bool onFoot, bool jobOn)
        {
            if (_failed || !CareerState.Loaded)
            {
                return;
            }
            try
            {
                JobBoards boards = _director.HelpBoards;
                if (!_mapped && boards != null && boards.Searched)
                {
                    Map(boards);
                }
                int hour = TimeHelper.CurrentHour;
                if (hour != _lastHour)
                {
                    _lastHour = hour;
                    if (!On)
                    {
                        CareerState.SkipTo(TimeHelper.CurrentDay);   // switched off: no rota day is held against anybody meanwhile
                    }
                    else if (!CareerPhone.Ready)
                    {
                        CareerPhone.Setup();   // switched on after the save was loaded
                    }
                    if (On)
                    {
                        Hourly(TimeHelper.CurrentDay, hour, jobOn);
                    }
                }
                if (!On || !_mapped)
                {
                    PutAway();
                    Near = -1;
                    return;
                }
                Presence(player, onFoot, boards);
                if (_gig != null)
                {
                    Gig(player, onFoot && !jobOn);
                }
                if (_cast.AnyOut)
                {
                    _cast.Cull();
                }
            }
            catch (Exception e)
            {
                _failed = true;
                PutAway();
                Unguide();
                Near = -1;
                _logger?.Error(e);
                _logger?.Warn("Careers: switched off for this session after the error above; the Gig Line and the boards work as before.");
            }
        }

        // The boss of the nearest depot stands by its board while the player is about; nobody is ever put down in view close by.
        private void Presence(Vector3 player, bool onFoot, JobBoards boards)
        {
            int nearest = -1;
            float best = BossGoes * BossGoes;
            for (int e = 0; e < _depot.Length; e++)
            {
                if (_depot[e] < 0 || _depot[e] >= boards.Count)
                {
                    continue;
                }
                float sqr = FlatSqr(boards.Front(_depot[e]) - player);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = e;
                }
            }
            if (_boss != null && (nearest != _bossOf || !_boss.IsValid))
            {
                PutAway();
            }
            if (nearest != _noRoomAt)
            {
                _noRoomAt = -1;
            }
            if (nearest >= 0 && _boss == null && onFoot && nearest != _noRoomAt && best < BossAppears * BossAppears)
            {
                PutBoss(nearest, boards, best);
            }

            // The colleague says hello when the player walks up: a wave at somebody who is there to be waved at.
            if (_mate != null && _mate.IsValid && !_mateGreeted && onFoot && nearest >= 0 && FlatSqr(_mate.Position - player) < 3.2f * 3.2f)
            {
                _mateGreeted = true;
                _mate.Face(player);
                _director.Animations.Play(_mate.Character, JobClip.Thank);
                Colleague(CareerCatalogue.All[nearest], _mateIndex);
            }

            // At the board, or beside the boss: the key speaks to the office.
            Near = -1;
            if (nearest >= 0 && onFoot)
            {
                bool atBoard = boards.Near == _depot[nearest];
                bool atBoss = _boss != null && _boss.IsValid && FlatSqr(_boss.Position - player) < BossReach * BossReach;
                if (atBoard || atBoss)
                {
                    Near = nearest;
                    if (_boss != null && _boss.IsValid && !_greeted)
                    {
                        _greeted = true;
                        _boss.Face(player);
                        _director.Animations.Play(_boss.Character, JobClip.Thank);   // a nod and a wave: there is somebody in front of them
                        CareerLinks.TellMet(CareerCatalogue.All[nearest], -1);
                    }
                }
            }
        }

        private void PutBoss(int employer, JobBoards boards, float distanceSqr)
        {
            int board = _depot[employer];
            Vector3 outward = boards.Outward(board);
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 front = boards.Front(board);
            for (int side = -1; side <= 1; side += 2)
            {
                // A step and a half along the wall from the board, clear of it and of the door on its other side.
                if (!CitySpots.Stand(front + along * (side * 1.7f) - outward * 0.2f, 0.45f, out Vector3 spot))
                {
                    continue;
                }
                if (CitySpots.InView(spot) && distanceSqr < BossAppears * BossAppears * 0.36f && FlatSqr(spot - PlayerPosition()) < BossNotNearer * BossNotNearer * 4f)
                {
                    return;   // they would be seen appearing: wait until the player looks away or comes from further off
                }
                Employer who = CareerCatalogue.All[employer];
                StreetActor boss = _cast.Put(spot, false, who.Seed, who.BossFemale);
                if (boss == null)
                {
                    break;
                }
                boss.StandAt(spot, spot + outward);
                if (!boss.Work(employer % 2 == 0 ? "clipboard_write" : "arms_crossed"))
                {
                    boss.Work("arms_crossed");
                }
                _boss = boss;
                _bossOf = employer;
                _bossAt = spot;
                _greeted = false;
                PutMate(employer, who, front - along * (side * 1.9f) + outward * 0.5f, outward);
                _logger?.Info("Careers: " + who.Id + "'s boss is at the office (" + boards.Address(board) + ")" + (_mate != null ? ", and a colleague with them." : "."));
                return;
            }
            _noRoomAt = employer;
            _logger?.Info("Careers: no room to stand " + CareerCatalogue.All[employer].Id + "'s boss by the board; the board itself is the office.");
        }

        // On a day the player is on the rota here, whoever is on the same shift waits at the office too: on the other side
        // of the board from the boss, put out at the same (unseen) moment, with the same face every time.
        private void PutMate(int employer, Employer who, Vector3 place, Vector3 outward)
        {
            int day = TimeHelper.CurrentDay;
            if (!CareerState.Employed || CareerState.EmployerIndex != employer || !CareerState.ShiftOpen(day)
                || !CitySpots.Stand(place, 0.45f, out Vector3 spot) || FlatSqr(spot - _bossAt) < 1.5f * 1.5f)
            {
                return;
            }
            int mate = day % who.Colleagues.Length;
            StreetActor person = _cast.Put(spot, false, who.Seed + 11 * (mate + 1), (mate + who.Seed) % 2 == 0, false);
            if (person == null)
            {
                return;
            }
            person.StandAt(spot, spot + outward);
            person.Work("talk_b");   // listening, a nod, a look up the street: somebody waiting for the shift to start
            _mate = person;
            _mateIndex = mate;
            _mateGreeted = false;
        }

        private static Vector3 PlayerPosition()
        {
            PlayerController player = PlayerHelper.PlayerController;
            return player != null ? player.transform.position : Vector3.zero;
        }

        private void PutAway()
        {
            if (_boss != null)
            {
                _cast.Release(_boss);
                _boss = null;
            }
            if (_mate != null)
            {
                _cast.Release(_mate);
                _mate = null;
            }
            _bossOf = -1;
        }

        private static float FlatSqr(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }

        // ---------------------------------------------------------------- once a game hour

        private void Hourly(int day, int hour, bool jobOn)
        {
            CountCashierHour();
            if (_gig != null && day != _gigDay)
            {
                _gig = null;
                Unguide();
                JobText.Toast(NotificationType.Warning, "reallife_career_gig_missed", 9f);
            }
            if (!CareerState.Employed)
            {
                return;
            }
            Employer employer = CareerState.Employer;
            if (CareerState.Marks >= CareerCatalogue.MarksToDismiss && !jobOn)
            {
                Dismiss("reallife_career_dismissed_late");   // a third mark whose dismissal was still to come (saved in between, or a shift that faulted)
                return;
            }

            // A rota day that went by entirely (a night in hospital, a long sleep): dealt with one at a time.
            int due = CareerState.NextShiftDay(CareerState.HandledDay + 1);
            if (due > 0 && due < day)
            {
                Miss(due, employer);
                return;
            }
            if (CareerState.ShiftOpen(day) && !(jobOn && _director.OnRotaShift))
            {
                int start = CareerState.RotaHour;
                if (hour >= start - CareerCatalogue.ReminderHours && hour < start + CareerCatalogue.OnTimeHours && CareerState.RemindedDay != day)
                {
                    CareerState.RemindedDay = day;
                    var data = new Dictionary<string, string>(3)
                    {
                        { "employer", JobText.T(employer.NameKey) }, { "time", start.GetFormattedTime(0f, true) }, { "title", JobText.T(CareerState.CurrentRank.TitleKey) }
                    };
                    Notifications.Show(NotificationType.Info, "reallife_career_reminder", data, 12f);
                    CareerPhone.Text("reallife_career_text_reminder", data);
                }
                else if (hour >= start + CareerCatalogue.LateHours)
                {
                    Miss(day, employer);
                    return;
                }
            }
            if (CareerState.Employed && CareerState.OnNotice && CareerState.NoticeServed(day) && !jobOn)
            {
                FinishNotice(day);
            }
        }

        private void Miss(int day, Employer employer)
        {
            var data = new Dictionary<string, string>(3) { { "employer", JobText.T(employer.NameKey) }, { "boss", JobText.T(employer.BossKey) } };
            if (CareerLinks.SickNote(day))
            {
                CareerState.Excused(day);
                Notifications.Show(NotificationType.Info, "reallife_career_sick_excused", data, 10f);
                _logger?.Info("Careers: the shift of day " + day + " was excused (sick).");
                return;
            }
            bool last = CareerState.Missed(day);
            data["marks"] = CareerState.Marks.ToString();
            data["most"] = CareerCatalogue.MarksToDismiss.ToString();
            _logger?.Info("Careers: no-show on day " + day + " at " + employer.Id + ", marks " + CareerState.Marks + ".");
            if (last)
            {
                Dismiss("reallife_career_dismissed_noshow");
            }
            else
            {
                Notifications.Show(NotificationType.Error, "reallife_career_noshow", data, 12f);
                CareerPhone.Text("reallife_career_text_noshow", data);
                ModState.AddModifier(ModState.MoodDown);
            }
        }

        // The game's own employer job (the cashier), read only: an hour worked there is remembered for the CV and
        // counts as experience when signing on with the retail chain. Nothing of the game's job is touched.
        private void CountCashierHour()
        {
            try
            {
                if (JobHelper.IsPlayerWorking() && BuildingManager.IsInsideBuilding
                    && !InstanceBehavior<BuildingManager>.Instance.IsPlayerOwnedBusiness)
                {
                    CareerState.CashierHours++;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- signing on, leaving

        /// <summary>Whether an employer would take the player on today. If not, the reason is a key with {count}, {day}.</summary>
        public bool CanSignOn(int employer, out string reasonKey, out string value)
        {
            reasonKey = null;
            value = null;
            Employer who = CareerCatalogue.All[employer];
            int day = TimeHelper.CurrentDay;
            if (!On)
            {
                reasonKey = "reallife_career_no_off";
            }
            else if (CareerState.Employed && CareerState.EmployerIndex == employer)
            {
                reasonKey = "reallife_career_no_already";
            }
            else if (CareerState.Employed)
            {
                reasonKey = "reallife_career_no_other";
                value = JobText.T(CareerState.Employer.NameKey);
            }
            else if (CareerState.BannedAt(employer, day))
            {
                reasonKey = "reallife_career_no_banned";
                value = (CareerState.BannedUntil(employer) - day).ToString();
            }
            else if (CareerState.ReferenceScore(JobState.HasReference) < who.References)
            {
                reasonKey = "reallife_career_no_references";
                value = who.References.ToString();
            }
            return reasonKey == null;
        }

        public void SignOn(int employer, CareerRota rota, bool test)
        {
            Employer who = CareerCatalogue.All[employer];
            JobDef own = JobCatalogue.Find(who.Job);
            JobDef related = who.Related != null ? JobCatalogue.Find(who.Related) : null;
            int credit = CareerCatalogue.CarryOver(own != null ? JobState.Level(own) : 1, related != null ? JobState.Level(related) : 1);
            if (who.Id == CareerCatalogue.Retail)
            {
                credit = Mathf.Min(CareerCatalogue.ShiftsPerReview - 1, credit + CareerState.CashierHours / 8);   // a day at the game's own till counts
            }
            CareerState.SignOn(employer, rota, TimeHelper.CurrentDay, credit);
            _dismissAfter = false;
            var data = new Dictionary<string, string>(3)
            {
                { "employer", JobText.T(who.NameKey) }, { "title", JobText.T(who.Ranks[0].TitleKey) }, { "rota", JobText.T(rota.NameKey) }
            };
            Notifications.Show(NotificationType.Success, "reallife_career_signed", data, 12f);
            CareerPhone.Text("reallife_career_text_signed", data);
            ModState.AddModifier(ModState.MoodUp);
            CareerLinks.TellMet(who, -1);
            _logger?.Info("Careers: signed on with " + who.Id + (test ? " by the test button" : string.Empty) + ", rota " + rota.Days + "@" + rota.Hour
                          + ", experience credited " + credit + " shift(s).");
        }

        public void GiveNotice()
        {
            if (!CareerState.Employed || CareerState.OnNotice)
            {
                return;
            }
            CareerState.GiveNotice(TimeHelper.CurrentDay);
            _logger?.Info("Careers: notice given at " + CareerState.Employer.Id + ".");
        }

        private void FinishNotice(int day)
        {
            Employer employer = CareerState.Employer;
            CareerReference reference = CareerState.EarnedReference();
            CareerState.Leave(day, reference);
            var data = new Dictionary<string, string>(1) { { "employer", JobText.T(employer.NameKey) } };
            Notifications.Show(NotificationType.Info, reference == CareerReference.Good ? "reallife_career_left_good" : "reallife_career_left_plain", data, 12f);
            CareerPhone.Text(reference == CareerReference.Good ? "reallife_career_text_left_good" : "reallife_career_text_left_plain", data);
            _logger?.Info("Careers: notice worked at " + employer.Id + ", reference " + reference + ".");
        }

        public void WalkOut()
        {
            if (!CareerState.Employed)
            {
                return;
            }
            Employer employer = CareerState.Employer;
            CareerState.Leave(TimeHelper.CurrentDay, CareerReference.MarkedDown);
            _dismissAfter = false;
            _logger?.Info("Careers: walked out of " + employer.Id + ".");
        }

        private void Dismiss(string key)
        {
            if (!CareerState.Employed)
            {
                return;
            }
            Employer employer = CareerState.Employer;
            CareerState.Leave(TimeHelper.CurrentDay, CareerReference.MarkedDown);
            _dismissAfter = false;
            var data = new Dictionary<string, string>(3)
            {
                { "employer", JobText.T(employer.NameKey) }, { "boss", JobText.T(employer.BossKey) }, { "days", CareerCatalogue.BanDays.ToString() }
            };
            Notifications.Show(NotificationType.Error, key, data, 14f);
            CareerPhone.Text("reallife_career_text_dismissed", data);
            ModState.AddModifier(ModState.MoodDownBig);
            _logger?.Info("Careers: dismissed by " + employer.Id + " (" + key + ").");
        }

        // ---------------------------------------------------------------- a shift

        /// <summary>Why the shift cannot be clocked on to right now, or null if it can.</summary>
        public string ClockOnRefusal()
        {
            if (!On)
            {
                return "reallife_career_no_off";
            }
            if (!CareerState.Employed)
            {
                return "reallife_career_no_employer";
            }
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            if (!CareerState.ShiftOpen(day))
            {
                return "reallife_career_no_shift_today";
            }
            if (hour < CareerState.RotaHour - CareerCatalogue.EarlyHours)
            {
                return "reallife_career_too_early";
            }
            if (hour >= CareerState.RotaHour + CareerCatalogue.LateHours)
            {
                return "reallife_career_too_late";
            }
            return null;
        }

        /// <summary>Clocks on to today's shift and starts its work where the player is. False with the reason if it cannot be done.</summary>
        public bool ClockOn(out string reasonKey)
        {
            reasonKey = ClockOnRefusal();
            if (reasonKey != null)
            {
                return false;
            }
            int day = TimeHelper.CurrentDay;
            bool late = TimeHelper.CurrentHour >= CareerState.RotaHour + CareerCatalogue.OnTimeHours;
            if (!Begin(CareerState.Employer, CareerState.CurrentRank, CareerState.Hourly, late, true, out reasonKey))
            {
                return false;
            }
            Employer employer = CareerState.Employer;
            _dismissAfter = CareerState.ClockOn(day, late);
            if (late)
            {
                var data = new Dictionary<string, string>(2)
                {
                    { "marks", CareerState.Marks.ToString() }, { "most", CareerCatalogue.MarksToDismiss.ToString() }
                };
                Notifications.Show(NotificationType.Warning, "reallife_career_late", data, 10f);
            }
            if (!_mateGreeted || _mate == null)
            {
                Colleague(employer, TimeHelper.CurrentDay % employer.Colleagues.Length);   // not met at the office: they are on the same shift all the same
            }
            return true;
        }

        /// <summary>Test buttons: a shift at the current employer's rank (or the courier firm's desk) that goes on no record.</summary>
        public bool TestShift(Employer employer, CareerRank rank, out string reasonKey)
        {
            return Begin(employer, rank, rank.Hourly, false, false, out reasonKey);
        }

        private bool Begin(Employer employer, CareerRank rank, float hourly, bool late, bool counts, out string reasonKey)
        {
            reasonKey = null;
            if (_director.HasJob)
            {
                if (counts)
                {
                    reasonKey = "reallife_job_no_busy";
                    return false;
                }
                _director.Quit();   // a test: whatever was on ends first, on its own terms
            }
            int index = CareerCatalogue.IndexOf(employer.Id);
            bool hasDepot = DepotPlace(index, out Vector3 depot, out _);
            if (rank.Work != CareerWork.Gig && hasDepot && counts && FlatSqr(depot - PlayerPosition()) > DeskReach * DeskReach)
            {
                reasonKey = "reallife_career_at_depot";
                return false;
            }
            int day = TimeHelper.CurrentDay;
            bool rentDue = rank.Has(CareerPerk.OwnCart) && counts && CareerState.CartPaidDay != day;
            if (rentDue && SaveGameManager.Current.Money < CareerCatalogue.CartRent)
            {
                reasonKey = "reallife_career_cart_no_money";
                return false;
            }
            JobDef def = JobCatalogue.Find(rank.Work == CareerWork.Dispatch ? JobCatalogue.Dispatch : rank.Work == CareerWork.Lead ? JobCatalogue.Lead : employer.Job);
            if (def == null)
            {
                reasonKey = "reallife_job_no_now";
                return false;
            }
            _shift.Employer = employer;
            _shift.Rank = rank;
            _shift.Hourly = hourly;
            _shift.Late = late;
            _shift.Counts = counts;
            _shift.StartMinutes = TimeHelper.NowInMinutes();
            _shift.Depot = depot;
            _shift.HasDepot = hasDepot;
            if (!_director.StartShift(def, _shift, !counts, out reasonKey))
            {
                return false;
            }
            if (rentDue && GameManager.ChangeMoneySafe(-CareerCatalogue.CartRent, new TransactionInfo("reallife_transaction_cart_rent"), null, null, force: false))
            {
                CareerState.CartPaidDay = day;
                JobText.Toast(NotificationType.Info, "reallife_career_cart_rented", "amount", JobText.Money(CareerCatalogue.CartRent), 8f);
            }
            _logger?.Info("Careers: shift for " + employer.Id + " as '" + rank.TitleKey + "' (" + def.Id + "), " + hourly.ToString("F1") + "/h"
                          + (late ? ", late" : string.Empty) + (counts ? string.Empty : ", test") + ".");
            return true;
        }

        // Whoever is on the same shift: named, greeted, and told to a social feature if there is one.
        private void Colleague(Employer employer, int mate)
        {
            var data = new Dictionary<string, string>(2) { { "name", JobText.T(employer.Colleagues[mate]) }, { "employer", JobText.T(employer.NameKey) } };
            Notifications.Show(NotificationType.Info, JobText.Pick(MateLines, ref _line), data, 8f);
            CareerLinks.TellMet(employer, mate);
        }

        private static readonly string[] MateLines =
        {
            "reallife_career_mate_hello_1", "reallife_career_mate_hello_2", "reallife_career_mate_hello_3", "reallife_career_mate_hello_4"
        };

        /// <summary>
        /// A shift worked for an employer is over: the basic wage for its hours through the game's own
        /// transaction, the payslip, the record, and whatever the record now leads to.
        /// </summary>
        public void ShiftEnded(Job job, CareerShift shift, JobEnd why, bool failed)
        {
            try
            {
                int piece = job.Shift.Total;
                int hours = 0;
                if (!failed && job.Shift.Units > 0 && why != JobEnd.Fault)
                {
                    float elapsed = TimeHelper.NowInMinutes() - shift.StartMinutes;
                    hours = Mathf.Clamp(Mathf.CeilToInt(elapsed / 60f), 1, CareerCatalogue.ShiftHours) - (shift.Late ? 1 : 0);
                }
                int wage = hours > 0 ? Mathf.RoundToInt(hours * shift.Hourly) : 0;
                if (wage > 0)
                {
                    GameManager.ChangeMoneySafe(wage, new TransactionInfo(WageKey, SalaryCategory), null, null, force: true);
                    if (shift.Counts)
                    {
                        JobState.Worked(job.Def, 0, wage);
                    }
                }
                Payslip(shift, piece, hours, wage);
                _logger?.Info("Careers: shift ended (" + why + (failed ? ", failed" : string.Empty) + "): work " + piece + ", " + hours + " h basic " + wage + ".");
                if (!shift.Counts || !CareerState.Employed || why == JobEnd.Fault)
                {
                    return;
                }
                // Clocking on and doing none of the work is not a shift worked: it goes down as a complaint.
                CareerState.ShiftDone(failed || job.Shift.Units <= 0, job.Shift.Tips, piece + wage);
                if (_dismissAfter)
                {
                    Dismiss("reallife_career_dismissed_late");
                }
                else if (CareerState.OnNotice && CareerState.NoticeServed(TimeHelper.CurrentDay))
                {
                    FinishNotice(TimeHelper.CurrentDay);
                }
                else if (CareerState.ReviewDue)
                {
                    Employer employer = CareerState.Employer;
                    var data = new Dictionary<string, string>(2) { { "boss", JobText.T(employer.BossKey) }, { "employer", JobText.T(employer.NameKey) } };
                    Notifications.Show(NotificationType.Info, "reallife_career_review_due", data, 12f);
                    CareerPhone.Text("reallife_career_text_review_due", data);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // The payslip: what the work paid, the basic wage, and the tax line. The game takes no tax from wages (its own
        // employer job pays its hourly rate in full, and income tax is on business sales only), so the line reads zero.
        private void Payslip(CareerShift shift, int piece, int hours, int wage)
        {
            _text.Length = 0;
            _text.Append(JobText.T(shift.Rank.TitleKey)).Append('\n');
            Line("reallife_career_slip_work", JobText.Money(piece));
            Line(JobText.T("reallife_career_slip_basic", "hours", hours.ToString(), "rate", JobText.Money(shift.Hourly)), JobText.Money(wage), false);
            if (shift.Late)
            {
                Line("reallife_career_slip_late", JobText.T("reallife_career_slip_late_value"));
            }
            Line("reallife_career_slip_tax", JobText.Money(0f));
            _text.Append('\n').Append(JobText.T("reallife_job_slip_total")).Append("   <b><color=#7CE08A>").Append(JobText.Money(piece + wage)).Append("</color></b>");
            _director.ShowCareerSlip(JobText.T("reallife_career_slip_title", "employer", JobText.T(shift.Employer.NameKey)), _text.ToString());
        }

        private void Line(string key, string value, bool lookUp = true)
        {
            _text.Append(lookUp ? JobText.T(key) : key).Append("   <color=#FFFFFF>").Append(value).Append("</color>\n");
        }

        // ---------------------------------------------------------------- the review

        /// <summary>Whether the review can be held here: face to face at the office, or by phone for an employer with no office in this city.</summary>
        public bool CanReview(bool inPerson)
        {
            return CareerState.Employed && CareerState.ReviewDue && (inPerson || DepotOf(CareerState.EmployerIndex) < 0);
        }

        /// <summary>Holds the review. The numbers read out are the ones the outcome was decided from.</summary>
        public ReviewOutcome HoldReview(out float score, out int onTime, out int due, out int tips, out int complaints, out int crashes, out int stops)
        {
            onTime = CareerState.OnTime;
            due = CareerState.OnTime + CareerState.Late + CareerState.NoShow;
            tips = CareerState.Tips;
            complaints = CareerState.Complaints;
            crashes = CareerState.Crashes;
            stops = CareerState.Stops;
            Employer employer = CareerState.Employer;
            ReviewOutcome outcome = CareerState.Review(!CareerState.OnNotice, out score);
            _logger?.Info("Careers: review at " + employer.Id + ": score " + score.ToString("F0") + " (on time " + onTime + "/" + due + ", tips " + tips
                          + ", complaints " + complaints + ", crashes " + crashes + ", stops " + stops + ") -> " + outcome + ".");
            switch (outcome)
            {
                case ReviewOutcome.Promoted:
                    ModState.AddModifier(ModState.MoodUpBig);
                    break;
                case ReviewOutcome.Raise:
                    ModState.AddModifier(ModState.MoodUp);
                    break;
                case ReviewOutcome.Warning:
                    ModState.AddModifier(ModState.MoodDown);
                    break;
                case ReviewOutcome.Dismissed:
                    CareerState.Leave(TimeHelper.CurrentDay, CareerReference.MarkedDown);
                    ModState.AddModifier(ModState.MoodDownBig);
                    break;
            }
            return outcome;
        }

        // ---------------------------------------------------------------- booked sets

        /// <summary>Why the agency has no set to book right now, or null if it has.</summary>
        public string GigRefusal()
        {
            JobDef busker = JobCatalogue.Find(JobCatalogue.Busker);
            if (!On)
            {
                return "reallife_career_no_off";
            }
            if (JobState.Level(busker) < CareerCatalogue.GigNeedsLevel)
            {
                return "reallife_career_gig_no_level";
            }
            if (CareerState.GigDay == TimeHelper.CurrentDay || _gig != null)
            {
                return "reallife_career_gig_no_today";
            }
            if (JobState.Sacked(busker, TimeHelper.CurrentDay))
            {
                return "reallife_job_no_sacked";
            }
            return null;
        }

        /// <summary>Books a set at a venue near the player: an open business with a clear front. False if none was found.</summary>
        public bool BookGig(bool test)
        {
            if (!JobSpots.RandomDoor(PlayerPosition(), test ? 20f : 90f, test ? 160f : 480f, null, 0f, out DoorSpot spot, JobSites.AnyClient, 96))
            {
                return false;
            }
            _gig = new BookedGig
            {
                Venue = spot.Front,
                VenueName = JobSites.NameOf(spot),
                Fee = Mathf.RoundToInt(CareerCatalogue.GigFee * Mathf.Clamp(Settings.JobPay, 25, 300) / 100f)
            };
            _gigDay = TimeHelper.CurrentDay;
            if (!test)
            {
                CareerState.GigDay = _gigDay;
            }
            Guide();
            _logger?.Info("Careers: a set is booked at " + _gig.VenueName + " for " + _gig.Fee + ".");
            return true;
        }

        public int GigFee => _gig != null ? _gig.Fee : Mathf.RoundToInt(CareerCatalogue.GigFee * Mathf.Clamp(Settings.JobPay, 25, 300) / 100f);

        // The player has come to the venue on foot: the set begins, where they stand.
        private void Gig(Vector3 player, bool free)
        {
            if (!free || Time.unscaledTime < _gigRetry || FlatSqr(_gig.Venue - player) > GigReach * GigReach)
            {
                return;
            }
            BookedGig gig = _gig;
            _gig = null;
            Unguide();
            if (!_director.StartGig(gig, out string reason))
            {
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_here", 8f);
                _gig = gig;       // no room for a pitch just here: tried again in a while, from wherever they stand then
                _gigRetry = Time.unscaledTime + 12f;
                Guide();
            }
        }

        private void Guide()
        {
            try
            {
                if (_gig != null && SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission == null)
                {
                    GuidersManager.SetGuiderTarget(_gig.Venue, _gig.VenueName, JobPhone.Icon, GuideColour, DirectionGuiderType.JobDestination);
                    _gigGuided = true;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Unguide()
        {
            if (!_gigGuided)
            {
                return;
            }
            _gigGuided = false;
            try
            {
                if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission == null)
                {
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- shared

        /// <summary>True for a car of the Luxury kind (Driving/CarCatalogue): what premium cab work is done in.</summary>
        public static bool IsLuxury(CarController car)
        {
            try
            {
                VehicleType type = car != null && car.vehicleInstance != null ? car.vehicleInstance.VehicleType : null;
                if (type == null)
                {
                    return false;
                }
                Rigidbody body = car.GetComponent<Rigidbody>();
                var traits = new CarTraits(type.IsMotorVehicle, type.spawnInPlayerObject, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar,
                    body != null ? body.mass : 1400f);
                return CarCatalogue.For(type.vehicleTypeName, traits).Kind == CarKind.Luxury;
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
                return false;
            }
        }
    }
}
