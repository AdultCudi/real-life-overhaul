using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The career ladder in the game: offices at help-wanted boards, the boss, the rota, clocking on, payslips, reviews, notice
    /// and booked sets. Called by <see cref="JobDirector"/> once a second: a few comparisons unless the hour has changed.
    /// </summary>
    public sealed partial class CareerDirector
    {
        private const float DeskReach = 45f;        // metres: desk and lead shifts are clocked on to this near the depot
        private const float OfficeNear = 25f;       // metres: the office's mark comes off once the player is this near the board
        private const float OfficeSeconds = 900f;   // real seconds the office's mark stays up if the player never gets there
        private const string WageKey = "reallife_transaction_career_wage";
        private const string SalaryCategory = "ba:transactioncategory_salaryincome";

        private readonly JobDirector _director;
        private readonly IModLogger _logger;
        private readonly JobCast _cast;
        private readonly int[] _depot = new int[CareerCatalogue.All.Length];   // the board each employer is at, or -1
        private readonly CareerShift _shift = new CareerShift();
        private readonly StringBuilder _text = new StringBuilder(256);
        private readonly GuideMark _officeMark = new GuideMark("the career's office");   // the game's destination marker, for "Show me the office"
        private float _officeUntil;       // unscaled time the mark comes off; 0 while it is not up
        private int _officeBoard = -1;
        private bool _mapped;
        private bool _failed;

        private int _lastHour = -1;
        private bool _dismissAfter;
        private int _line = -1;

        public CareerDirector(JobDirector director, IModLogger logger)
        {
            _director = director;
            _logger = logger;
            _officeMark.Keep(director);   // goes with the job director, or when the guard stops it
            _cast = new JobCast(director, logger);
            for (int i = 0; i < _depot.Length; i++)
            {
                _depot[i] = -1;
            }
        }

        public static bool On => Settings.Jobs && Settings.Careers;

        /// <summary>The employer at whose depot the player stands (by its board or its boss), or -1.</summary>
        public int Near { get; private set; } = -1;

        public void Dispose()
        {
            Unguide();
            HideOffice();
            _boss = null;
            _cast.Dispose();
        }

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

        private bool DepotPlace(int employer, out Vector3 place, out string address)
        {
            JobBoards boards = _director.HelpBoards;
            int board = DepotOf(employer);
            if (boards == null || board < 0 || board >= boards.Count)
            {
                // An employer with no board (the building trades'): its works site or its floor, nearest the player.
                place = default;
                address = null;
                return employer >= 0 && employer < CareerCatalogue.All.Length && CareerCatalogue.All[employer].Premises.Length == 0
                       && CareerLinks.WorkplaceOf(CareerCatalogue.All[employer], PlayerPosition(), out place, out address);
            }
            place = boards.Front(board);
            address = boards.Address(board);
            return true;
        }

        /// <summary>The address of the employer's office as the game writes it, or null if the employer has no board in this city.</summary>
        public string OfficeAddress(int employer)
        {
            return DepotPlace(employer, out _, out string address) && CareerOfficeRules.Has(address) ? address : null;
        }

        /// <summary>The key for a message sending the player to the office, with the address put in the data when there is a board.</summary>
        public string OfficeKey(string key, int employer, Dictionary<string, string> data)
        {
            string address = OfficeAddress(employer);
            if (address != null && data != null)
            {
                data[CareerOfficeRules.AddressField] = address;
            }
            return CareerOfficeRules.Key(key, address);
        }

        /// <summary>"Show me the office": the game's destination marker on the board until the player is near or 15 minutes pass. False without a board or with the marker in use.</summary>
        public bool ShowOffice(int employer)
        {
            if (!DepotPlace(employer, out Vector3 place, out string address))
            {
                return false;
            }
            string name = JobText.T(CareerCatalogue.All[employer].NameKey) + (CareerOfficeRules.Has(address) ? ", " + address : string.Empty);
            bool shown = _officeMark.Show(place + Vector3.up * 1.4f, name);
            if (shown)
            {
                _officeBoard = DepotOf(employer);
                _officeUntil = Time.unscaledTime + OfficeSeconds;
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers check (office): '" + name + "' at " + place.ToString("F1", CultureInfo.InvariantCulture) + " is "
                              + (shown ? "on the game's destination marker: PASS" : "NOT marked (the marker is on a job or an outing): FAIL") + ".");
            }
            return shown;
        }

        // Once a second, and only while the mark is up: comes off when the player is near the board or the time is up.
        private void OfficeMark(Vector3 player, JobBoards boards)
        {
            if (_officeUntil <= 0f)
            {
                return;
            }
            if (Time.unscaledTime >= _officeUntil || _officeBoard < 0 || _officeBoard >= boards.Count || FlatSqr(boards.Front(_officeBoard) - player) < OfficeNear * OfficeNear)
            {
                HideOffice();
            }
        }

        private void HideOffice()
        {
            if (_officeUntil <= 0f)
            {
                return;
            }
            _officeUntil = 0f;
            _officeBoard = -1;
            _officeMark.Hide();
        }

        // Each employer takes the board at its kind of building, best match first, the rest what is left: the same in every game.
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
            _text.Length = 0;
            for (int e = 0; e < all.Length; e++)
            {
                _text.Append(all[e].Id).Append(_depot[e] >= 0 ? " at " + boards.Address(_depot[e]) + " (" + boards.Kind(_depot[e]) + ")" : " has no office").Append("; ");
            }
            _logger?.Info("Careers: " + _text);
        }

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
                    HideOffice();
                    PutAway();
                    Near = -1;
                    return;
                }
                Presence(player, onFoot, boards);
                OfficeMark(player, boards);
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
                Dismiss("reallife_career_dismissed_late");   // a third mark whose dismissal was still to come (a save between, or a faulted shift)
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
                    var data = new Dictionary<string, string>(4)
                    {
                        { "employer", JobText.T(employer.NameKey) }, { "time", CareerClock.Span(start) }, { "title", JobText.T(CareerState.CurrentRank.TitleKey) }
                    };
                    Notifications.Show(NotificationType.Info, OfficeKey("reallife_career_reminder", CareerState.EmployerIndex, data), data, 12f);
                    CareerPhone.Text(OfficeKey("reallife_career_text_reminder", CareerState.EmployerIndex, data), data);
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

        // The game's own cashier job, read only: an hour worked there counts for the CV and as retail experience.
        private void CountCashierHour()
        {
            try
            {
                if (JobHelper.IsPlayerWorking() && BuildingManager.IsInsideBuilding
                    && !GoingPublic.Mine(InstanceBehavior<BuildingManager>.Instance.buildingRegistration))
                {
                    CareerState.CashierHours++;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

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
            if (!Begin(CareerState.Employer, CareerState.CurrentRank, CareerState.Hourly, CareerState.Piece, late, true, out reasonKey))
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
            return Begin(employer, rank, rank.Hourly, CareerCatalogue.PieceWith(rank, 0), false, false, out reasonKey);
        }

        private bool Begin(Employer employer, CareerRank rank, float hourly, float piece, bool late, bool counts, out string reasonKey)
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
            bool hasDepot = DepotPlace(index, out Vector3 depot, out string depotAddress);
            if (rank.Work != CareerWork.Gig && hasDepot && counts && FlatSqr(depot - PlayerPosition()) > DeskReach * DeskReach)
            {
                reasonKey = CareerOfficeRules.Key("reallife_career_at_depot", depotAddress);
                return false;
            }
            int day = TimeHelper.CurrentDay;
            bool rentDue = rank.Has(CareerPerk.OwnCart) && counts && CareerState.CartPaidDay != day;
            if (rentDue && SaveGameManager.Current.Money < CareerCatalogue.CartRent)
            {
                reasonKey = "reallife_career_cart_no_money";
                return false;
            }
            JobDef def = JobCatalogue.Find(rank.Work == CareerWork.Dispatch ? JobCatalogue.Dispatch : rank.Work == CareerWork.Lead ? JobCatalogue.Lead : rank.JobId ?? employer.Job);
            if (def == null)
            {
                reasonKey = "reallife_job_no_now";
                return false;
            }
            _shift.Employer = employer;
            _shift.Rank = rank;
            _shift.Hourly = hourly;
            _shift.Piece = piece;
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
            _logger?.Info("Careers: shift for " + employer.Id + " as '" + rank.TitleKey + "' (" + def.Id + "), " + hourly.ToString("F1", CultureInfo.InvariantCulture) + "/h, work paid at "
                          + piece.ToString("F2", CultureInfo.InvariantCulture) + "x"
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

        /// <summary>A rota shift is over: the basic wage for its hours, the payslip, the record, and what the record now leads to.</summary>
        public void ShiftEnded(Job job, CareerShift shift, JobEnd why, bool failed)
        {
            try
            {
                int piece = job.Shift.Total;
                int hours = 0;
                if (!failed && job.Shift.Units > 0)   // (a job pays what was done before a fault, and its hours too)
                {
                    hours = CareerCatalogue.PaidHours(TimeHelper.NowInMinutes() - shift.StartMinutes, shift.Late);
                }
                int wage = CareerCatalogue.Wage(hours, shift.Hourly);
                if (wage > 0)
                {
                    GameManager.ChangeMoneySafe(wage, new TransactionInfo(WageKey, SalaryCategory), null, null, force: true);
                    if (shift.Counts)
                    {
                        JobState.Worked(job.Def, 0, wage);
                    }
                }
                Payslip(job, shift, piece, hours, wage);
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
                    var data = new Dictionary<string, string>(3) { { "boss", JobText.T(employer.BossKey) }, { "employer", JobText.T(employer.NameKey) } };
                    Notifications.Show(NotificationType.Info, OfficeKey("reallife_career_review_due", CareerState.EmployerIndex, data), data, 12f);
                    CareerPhone.Text(OfficeKey("reallife_career_text_review_due", CareerState.EmployerIndex, data), data);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Settles a rota shift on at the save (<see cref="JobSave"/>): the wage for the hours clocked, and the shift counted if worked; no complaint, no mark.</summary>
        public void SettleSaved(JobDef def, SavedShift saved, in ShiftSettlement sum)
        {
            if (sum.Wage > 0)
            {
                GameManager.ChangeMoneySafe(sum.Wage, new TransactionInfo(WageKey, SalaryCategory), null, null, force: true);
                JobState.Worked(def, 0, sum.Wage);
            }
            if (sum.Worked && CareerState.Employed && CareerState.Employer.Id == saved.Employer)
            {
                CareerState.ShiftDone(false, sum.Tips, sum.Earned);
            }
            _logger?.Info("Careers: a shift at " + saved.Employer + " that was on at a save was settled: " + sum.Hours + " h basic " + sum.Wage + ", work " + sum.Work
                          + (sum.Worked ? "; counted as a shift worked." : "; no work had been done: not counted as a shift."));
        }

        // The game takes no tax from wages (its own job pays the rate in full), so the payslip's tax line reads zero.
        private void Payslip(Job job, CareerShift shift, int piece, int hours, int wage)
        {
            var data = new Dictionary<string, string>(6)
            {
                { "employer", JobText.T(shift.Employer.NameKey) }, { "total", JobText.Money(piece + wage) }, { "lines", _director.SlipLines(job.Shift) },
                { "hours", hours.ToString() }, { "rate", JobText.Money(shift.Hourly) }, { "wage", JobText.Money(wage) }
            };
            Notifications.Show(piece + wage > 0 ? NotificationType.Success : NotificationType.Info,
                CareerCatalogue.SlipKey(shift.Rank, shift.Late), data, 16f);
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers: payslip shown as one of the game's notifications: total " + (piece + wage) + " (" + data["lines"] + "; basic " + hours + " h at "
                              + shift.Hourly.ToString("F1", CultureInfo.InvariantCulture) + " = " + wage + (shift.Late ? "; late: 1 h docked" : string.Empty) + ").");
            }
        }

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
