using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Entities;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The staff rota planner: what the director does with <see cref="RotaSolver"/> and <see cref="RotaAdvisor"/>.
    ///
    /// WHAT THE GAME ALREADY HAS, AND IS LEFT TO IT: a schedule screen in BizMan, and with an HR manager at a
    /// headquarters an "auto fill" that puts the employees of a business on its stations for the hours it is open,
    /// keeping their demands (Buildings.Schedule.ScheduleAutoFiller). It does not say which hours are worth
    /// opening, how many customers will come, how many people that takes or which stations are worth staffing.
    ///
    /// WHAT IS ADDED BESIDE IT, on the Company app's Rota tab: for each business the game's own customer forecast for
    /// every hour of the week, the hours that pay for their crew, the people that takes against the people there
    /// are, and a rota of the mod's own making, written only when the player presses the button
    /// (RotaGame.Write says exactly what is written and what of the game's own follow-up is left out).
    ///
    /// COST. Nothing per frame while nothing is asked: no Update, no timer, nothing on the hour. The list of places
    /// is read on the day event (one pass, for the Planner's row "the rota has gaps", which is a count of open hours
    /// with nobody on a shift and no more), when the app opens and after a rota is written. A business is worked
    /// out when it is picked on the tab or a button is pressed, as a JOB: the game is read in one frame, and the
    /// arithmetic and the rota are then made a few steps a frame for at most <see cref="RotaSliceMs"/> milliseconds
    /// of each frame, by a coroutine that exists only while a job runs. (The steps are the same whatever the cut, so
    /// the rota is the one the offline checks make; the game's runtime is several times slower than theirs, and the
    /// largest business would otherwise hold a frame.) What was worked out is kept until something it was worked out
    /// from changes (RotaGame.Stamp), and a job whose business changed while it ran starts again and writes nothing
    /// from the stale reading. Nothing is saved: every figure is worked out from the game, and the week that was
    /// there before a fill is kept in memory only, until the app is closed.
    ///
    /// A business whose figures cannot be read from the game is left out for the session with one line, and its page
    /// says so; a fault in writing a rota switches the whole planner off for the session. Either way the rest of the
    /// company goes on.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        public const int TabRota = 5;
        private const int RotaAlertKind = 30;       // the Planner's id for "the rota has gaps", beside the company's own (1 to 14)
        private const double RotaSliceMs = 2.0;     // milliseconds of one frame a job may take before it waits for the next
        private const int RotaStepsAtOnce = 2;      // steps between two looks at the clock: each is a fraction of a millisecond
        private const int RotaRestartsMost = 3;     // times a job starts again because its business changed under it, before it gives up
        private const int JobShow = 0, JobFill = 1, JobFillHours = 2, JobTell = 3;
        private const int StageRead = 0, StageAdvise = 1, StageRate = 2, StageSolve = 3, StageWrite = 4, StageLadder = 5;

        // One thing asked of the planner: a business worked out for its page, a rota filled, or the notice that ends a run of test fills.
        private struct RotaJob
        {
            public string Place;
            public int Kind;
            public bool Test;
            public int Rung;        // the plan of the ladder to write; less than nothing: a rota for the hours kept now
        }

        private readonly List<RotaPlace> _rotaPlaces = new List<RotaPlace>(16), _rotaPool = new List<RotaPlace>(16);
        private readonly List<RotaUndo> _rotaUndo = new List<RotaUndo>(4);
        private readonly List<RotaJob> _rotaJobs = new List<RotaJob>(4);
        private readonly List<string> _rotaUnread = new List<string>(2);   // places whose figures could not be read from the game this session
        private readonly RotaSheet _rotaSheet = new RotaSheet();
        private readonly RotaSolver _rotaSolver = new RotaSolver();
        private readonly RotaAdvisor _rotaAdvisor = new RotaAdvisor();
        private readonly RotaLadder _rotaLadder = new RotaLadder();
        private readonly Dictionary<string, int> _rotaModes = new Dictionary<string, int>(8);   // the player's choice for a business: staff first or lean (RotaStore)
        private bool _rotaModesUnread;              // the save holds a line of choices this version cannot read: it is left as it is until a choice is made
        private readonly int[] _rotaPromotion = new int[RotaCatalogue.Days];
        private readonly bool[] _rotaBarred = new bool[RotaCatalogue.Cells];
        private readonly Stopwatch _rotaClock = new Stopwatch();
        private Func<string, int> _rotaRaise;
        private bool _rotaBroken, _rotaSheetGood, _rotaWorking;
        private int _rotaStage, _rotaRestarts, _rotaFrames, _rotaTestFilled, _rotaTestShifts;
        private double _rotaSpent, _rotaLongest;
        private BuildingRegistration _rotaOwnOf;    // the business whose own promotion figure was last asked for...
        private int _rotaOwnHour, _rotaOwn;         // ...in which game hour, and what it was
        private string _rotaMade;                   // the place whose rota as it stands is the planner's own...
        private long _rotaMadeStamp;                // ...for as long as nothing it was made from has changed

        /// <summary>Whether the planner runs: the company is running, its option is on and no fault has stopped it.</summary>
        public bool RotaOn => _running && !_broken && !_rotaBroken && Settings.EconomyRota;

        private void RotaFault(string where, Exception e)
        {
            _rotaBroken = true;
            _rotaSheetGood = false;
            _rotaJobs.Clear();
            Logger?.Error(e);
            Logger?.Warn("Company: the staff rota planner switched off for this session after an error in " + where + ". No rota is written by it any more; the game's own schedule and the rest of the company are not affected.");
        }

        // ---------------------------------------------------------------- the places

        // The day event, the app opening, a rota written: the list of places and each one's gaps, read afresh.
        partial void RotaRead()
        {
            if (!RotaOn || Save == null)
            {
                _rotaPlaces.Clear();
                return;
            }
            try
            {
                RotaGame.Logger = Logger;
                RotaGame.ReadPlaces(_rotaPlaces, _rotaPool);
            }
            catch (Exception e)
            {
                RotaFault("reading the places", e);
            }
        }

        partial void RotaStop()
        {
            _rotaPlaces.Clear();
            _rotaUndo.Clear();
            _rotaUnread.Clear();
            _rotaJobs.Clear();
            _rotaSheetGood = false;
            _rotaSheet.Place = null;
            _rotaSheet.Reg = null;
            _rotaOwnOf = null;
            _rotaMade = null;
        }

        internal IReadOnlyList<RotaPlace> RotaPlaces => _rotaPlaces;

        internal RotaPlace RotaPlaceAt(string place)
        {
            for (int i = 0; place != null && i < _rotaPlaces.Count; i++)
            {
                if (string.Equals(_rotaPlaces[i].Place, place, StringComparison.Ordinal))
                {
                    return _rotaPlaces[i];
                }
            }
            return null;
        }

        // A business that is open with nobody on a shift, once in the Planner: counted when the places were read, never worked out here.
        partial void RotaAlerts(int day)
        {
            try
            {
                for (int i = 0; RotaOn && i < _rotaPlaces.Count; i++)
                {
                    RotaPlace place = _rotaPlaces[i];
                    if (place.Type != RotaType.Warehouse && !place.Shut && place.Staff > 0 && place.GapHours > 0)
                    {
                        Alert(RotaAlertKind, PlaceNumber(place.Place), day, CompanyText.T("reallife_rota_plan_title", "business", place.Name),
                            CompanyText.T("reallife_rota_plan_what", "hours", place.GapHours.ToString()), null, null);
                    }
                }
            }
            catch (Exception e)
            {
                RotaFault("the Planner's row", e);
            }
        }

        // ---------------------------------------------------------------- one business worked out

        /// <summary>
        /// The sheet of a business if it is worked out and nothing it was worked out from has changed. Otherwise
        /// null, and a job is started that works it out over the coming frames and then has the page written again
        /// (<see cref="RotaWorking"/> says so meanwhile). Asking costs one stamp: a walk over the business's items
        /// and the save's employees.
        /// </summary>
        internal RotaSheet RotaSheetFor(RotaPlace place)
        {
            if (!RotaOn || place == null || place.Reg == null || Save == null || _rotaUnread.Contains(place.Place))
            {
                return null;
            }
            try
            {
                if (RotaFresh(place))
                {
                    _rotaSheet.Place = place;   // (the list of places is made again from time to time: the business is the same one)
                    return _rotaSheet;
                }
                RotaAsk(place.Place, JobShow, false);
            }
            catch (Exception e)
            {
                RotaUnreadable(place, e);
            }
            return null;
        }

        /// <summary>True while a job for a business waits or runs: its page says the week is being worked out.</summary>
        internal bool RotaWorking(string place)
        {
            for (int i = 0; i < _rotaJobs.Count; i++)
            {
                if (_rotaJobs[i].Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for a business the planner could not read from the game this session.</summary>
        internal bool RotaUnread(string place)
        {
            return _rotaUnread.Contains(place);
        }

        private bool RotaFresh(RotaPlace place)
        {
            return _rotaSheetGood && ReferenceEquals(_rotaSheet.Reg, place.Reg) && _rotaSheet.Stamp == RotaGame.Stamp(place, RotaDays(place));
        }

        // One business the game's figures cannot be read for: it is left out, and the others are still planned.
        private void RotaUnreadable(RotaPlace place, Exception e)
        {
            _rotaSheetGood = false;
            _rotaUnread.Add(place.Place);
            Logger?.Error(e);
            Logger?.Warn("Company: the staff rota planner could not read " + place.Name + " from the game and leaves it out for this session; its page says so. Other businesses are still planned.");
        }

        private int RotaRaise(string id)
        {
            return RaiseOf(id);
        }

        /// <summary>The raise agreed for an employee on top of the game's wage, hundredths an hour; 0 for none (or with the staff part off).</summary>
        public int RaiseOf(string id)
        {
            StaffFile f = On(CompanyPart.Staff) && _state != null && id != null ? _state.Employee(id) : null;
            return f != null ? f.Raise : 0;
        }

        /// <summary>
        /// The promotion figure the mod will have written for a business on each weekday of the coming week, into
        /// <see cref="_rotaPromotion"/>; less than nothing for a day on which it is as it stands. Returns a number
        /// that changes when any of it does, or the late opening permit (its second bit: the permit is held, or
        /// licences are off).
        ///
        /// The game's forecast holds what the mod adds to footfall today, because the mod writes it into the
        /// business's promotion figure. On another day of the week a campaign, overtime, a strike or one of business
        /// pressure's issues may have ended or begun. That day's figure is worked out by the function that writes it
        /// (BusinessCatalogue.Written: the issue's change and the ambient one each inside their own bounds, the
        /// result a promotion of 0 to 100), from the business's own figure and the parts as they will be then; the
        /// game's calculator is then asked with it (RotaGame.ReadCustomers). The city's economy is taken as it is today.
        /// </summary>
        private int RotaDays(RotaPlace place)
        {
            CompanyShop shop = null;
            for (int i = 0; i < _shops.Count; i++)
            {
                shop = ReferenceEquals(_shops[i].Reg, place.Reg) ? _shops[i] : shop;
            }
            int today = Save.Day, weekday = ((today - 1) % RotaCatalogue.Days + RotaCatalogue.Days) % RotaCatalogue.Days, sum = 0;   // (the game's own count: day 1 is a Monday, TimeHelper.GetDayOfWeek)
            bool permit = !On(CompanyPart.Licences) || shop == null || LicenceRules.Valid(shop.File.Permits[(int)LicenceKind.Late], today);
            bool follows = shop != null && !_holding && Promoted(shop) && CompanyBridges.Footfall != null;
            Func<BuildingRegistration, int, int> issues = follows ? CompanyBridges.IssueFootfall : null;
            int city = follows ? CityEconomyDirector.FootfallOf(place.Reg) : 0;
            int issueNow = issues != null ? issues(place.Reg, today) : 0, companyNow = follows ? Footfall(shop, today) : 0;
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                int day = today + (d - weekday + RotaCatalogue.Days) % RotaCatalogue.Days;
                int issue = issues != null ? issues(place.Reg, day) : 0, company = follows ? Footfall(shop, day) : 0;
                _rotaPromotion[d] = -1;
                if (issue != issueNow || company != companyNow)
                {
                    _rotaPromotion[d] = BusinessCatalogue.Written(RotaOwn(place.Reg), issue, city + company, Save.gameVariables.baseCustomerPromotionMultiplier);
                }
                sum = unchecked(sum * 31 + _rotaPromotion[d]);
            }
            return (unchecked(sum * 4) + (On(CompanyPart.Staff) ? 1 : 0) + (permit ? 2 : 0)) ^ (RotaModeOf(place.Place) << 24) ^ (RotaCleaned(place) ? 1 << 27 : 0) ^ ((shop != null ? shop.Income : 0) << 12) ^ (_index << 4);
        }

        // A business's own promotion figure, before anything of the mod's: what the mod's one writer of that figure starts from.
        // Asked of that writer, which writes the figure again as it should stand at this moment (the same value: it never adds
        // up) and says what it started from. Once a game hour for a business, and only when a day of its week differs from today.
        private int RotaOwn(BuildingRegistration reg)
        {
            int hour = Save.Day * RotaCatalogue.Hours + Save.Hour;
            if (!ReferenceEquals(_rotaOwnOf, reg) || _rotaOwnHour != hour)
            {
                int own, written;
                _rotaOwn = CompanyBridges.Footfall(reg, false, false, out own, out written) ? own : reg.promotion != null ? reg.promotion.total : 0;
                _rotaOwnOf = reg;
                _rotaOwnHour = hour;
            }
            return _rotaOwn;
        }

        // Whether a week of hours is open late or early by the mod's own licence rule (LicenceRules.IsLate), block by block.
        private static bool RotaLate(bool[] open)
        {
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                for (int h = 0; h < RotaCatalogue.Hours;)
                {
                    if (!open[d * RotaCatalogue.Hours + h])
                    {
                        h++;
                        continue;
                    }
                    int from = h;
                    while (h < RotaCatalogue.Hours && open[d * RotaCatalogue.Hours + h])
                    {
                        h++;
                    }
                    if (LicenceRules.IsLate(from, h))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- jobs, a few steps a frame

        private void RotaAsk(string place, int kind, bool test, int rung = -1)
        {
            for (int i = 0; i < _rotaJobs.Count; i++)
            {
                if (_rotaJobs[i].Place == place && _rotaJobs[i].Kind == kind)
                {
                    return;   // asked already
                }
            }
            _rotaJobs.Add(new RotaJob { Place = place, Kind = kind, Test = test, Rung = rung });
            if (!_rotaWorking)
            {
                _rotaWorking = true;
                RotaBegin();
                StartCoroutine(RotaWork());
            }
        }

        private void RotaBegin()
        {
            _rotaStage = StageRead;
            _rotaRestarts = _rotaFrames = 0;
            _rotaSpent = _rotaLongest = 0;
        }

        // Lives only while a job waits: each frame it works for RotaSliceMs at most, then lets the frame go.
        private IEnumerator RotaWork()
        {
            while (RotaSlice())
            {
                yield return null;
            }
            _rotaWorking = false;
        }

        // One frame's share of the first job in the line. False when there is nothing left to do.
        private bool RotaSlice()
        {
            if (!RotaOn || Save == null || _rotaJobs.Count == 0)
            {
                _rotaJobs.Clear();
                return false;
            }
            RotaJob job = _rotaJobs[0];
            bool done;
            _rotaClock.Restart();
            try
            {
                do
                {
                    done = RotaStep(job);
                }
                while (!done && _rotaClock.Elapsed.TotalMilliseconds < RotaSliceMs);
            }
            catch (Exception e)
            {
                done = true;
                RotaPlace at = RotaPlaceAt(job.Place);
                if (_rotaStage == StageWrite || at == null)
                {
                    RotaFault("a rota for " + (at != null ? at.Name : job.Place), e);
                }
                else
                {
                    RotaUnreadable(at, e);
                }
            }
            double took = _rotaClock.Elapsed.TotalMilliseconds;
            _rotaFrames++;
            _rotaSpent += took;
            _rotaLongest = Math.Max(_rotaLongest, took);
            if (done)
            {
                if (_rotaJobs.Count > 0)
                {
                    _rotaJobs.RemoveAt(0);
                }
                RotaBegin();
                if (_view != null && job.Kind != JobTell)
                {
                    _view.Changed();
                }
            }
            return _rotaJobs.Count > 0;
        }

        // The next piece of a job. Reading the game and writing to it are each one piece, done within one frame; the
        // arithmetic and the rota between them are a few steps a piece. True when the job is finished.
        private bool RotaStep(RotaJob job)
        {
            if (job.Kind == JobTell)
            {
                Logger?.Warn("Company test: " + _rotaTestFilled + " rota(s) filled, " + _rotaTestShifts + " shift(s) written; the weeks as they were are kept for 'Test: put every rota back'.");
                Notifications.Show(NotificationType.Info, "reallife_rota_test_filled", new Dictionary<string, string> { { "count", _rotaTestFilled.ToString() }, { "shifts", _rotaTestShifts.ToString() } },
                    9f, null, () => Open(TabRota, null), true, false);
                Changed();
                return true;
            }
            RotaPlace place = RotaPlaceAt(job.Place);
            RotaSheet sheet = _rotaSheet;
            if (place == null || _rotaUnread.Contains(job.Place))
            {
                return true;   // (no longer the player's, or it could not be read)
            }
            switch (_rotaStage)
            {
                case StageRead:
                    if (RotaFresh(place))
                    {
                        sheet.Place = place;
                        _rotaStage = StageWrite;
                        return RotaToFill(job, sheet);
                    }
                    _rotaSheetGood = false;
                    _rotaRaise = _rotaRaise ?? RotaRaise;
                    int days = RotaDays(place);
                    RotaGame.Read(sheet, place, _rotaPromotion, _rotaRaise);
                    RotaGame.ReadDeep(sheet, RotaMarket(place));
                    sheet.Stamp = RotaGame.Stamp(place, days);
                    sheet.Permit = (days & 2) != 0;
                    if (!sheet.Permit)
                    {
                        RotaAdvisor.LateHours(_rotaBarred);
                    }
                    _rotaAdvisor.Begin(sheet.Shop, sheet.Problem, _rotaSolver, sheet.Now.At, sheet.Permit ? null : _rotaBarred, sheet.Outlook);
                    _rotaStage = StageAdvise;
                    return false;
                case StageAdvise:
                    _rotaStage = _rotaAdvisor.Step(RotaStepsAtOnce) ? StageRate : StageAdvise;
                    return false;
                case StageRate:
                    // Left as the page and the fill need it: the worth of the hours it keeps now, and the rota it has rated against them.
                    _rotaAdvisor.Grid(sheet.Shop, sheet.Shop.Open, sheet.Problem);
                    _rotaSolver.Rate(sheet.Problem, sheet.Now);
                    _rotaLadder.Begin(sheet.Shop, sheet.Problem, _rotaSolver, _rotaAdvisor, sheet.Outlook, sheet.Now, sheet.Permit ? null : _rotaBarred, RotaModeOf(place.Place), sheet.Ladder);
                    _rotaStage = StageLadder;
                    return false;
                case StageLadder:
                    // The ladder of plans: each a week of its own, valued the same way. A few steps a piece, like the advice.
                    if (!_rotaLadder.Step(RotaStepsAtOnce))
                    {
                        return false;
                    }
                    if (RotaGame.Stamp(place, RotaDays(place)) != sheet.Stamp)
                    {
                        return RotaAgain(job, place);   // (the business changed while it was being worked out: what was read is no longer it)
                    }
                    sheet.Late = !sheet.Permit && RotaLate(sheet.Shop.Open);
                    sheet.Shown = _rotaMade == place.Place && _rotaMadeStamp == sheet.Stamp ? sheet.Now : null;
                    _rotaSheetGood = true;
                    RotaCheck(place, sheet);
                    RotaPlansLog(place, sheet, false);
                    _rotaStage = StageWrite;
                    return RotaToFill(job, sheet);
                default:
                    if (!RotaFresh(place))
                    {
                        return RotaAgain(job, place);   // (never a rota from a reading that is no longer true)
                    }
                    RotaRung rung = job.Kind == JobFillHours ? RotaRungOf(sheet, job.Rung) : null;
                    if (job.Kind == JobFillHours && rung == null)
                    {
                        return true;   // (a plan that takes people still to be hired, or one that is no longer there: nothing is written)
                    }
                    RotaWrite(job, place, sheet, rung != null ? rung.Plan : sheet.Ladder.Fill, rung);
                    return true;
            }
        }

        // The sheet is ready. A job that only shows it is done; a fill goes on to its rota: the one that came with the
        // advice, or one for the hours the business keeps (the sheet's problem holds their worth), made over the coming frames.
        private bool RotaToFill(RotaJob job, RotaSheet sheet)
        {
            if (job.Kind == JobShow && job.Test)
            {
                RotaPlansLog(sheet.Place, sheet, true);
            }
            if (job.Kind == JobShow || RotaRefusal(sheet) != null || (job.Kind == JobFillHours && RotaRungOf(sheet, job.Rung) == null))
            {
                return true;
            }
            _rotaStage = StageWrite;
            return false;
        }

        private bool RotaAgain(RotaJob job, RotaPlace place)
        {
            _rotaSheetGood = false;
            _rotaStage = StageRead;
            if (++_rotaRestarts <= RotaRestartsMost)
            {
                return false;
            }
            Logger?.Warn("Company: " + place.Name + " kept changing while its rota was worked out (" + _rotaRestarts + " readings); nothing was written.");
            if (job.Kind != JobShow && !job.Test)
            {
                Notifications.Show(NotificationType.Warning, "reallife_rota_changed", new Dictionary<string, string> { { "business", place.Name } }, 8f, null, null, true, false);
            }
            return true;
        }

        // The self-check of a business worked out: its figures, and what the working out cost in frames and milliseconds against the share of a frame it may take.
        private void RotaCheck(RotaPlace place, RotaSheet sheet)
        {
            if (!Verbose)
            {
                return;
            }
            RotaOutlook o = sheet.Outlook;
            double now = _rotaClock.Elapsed.TotalMilliseconds, longest = Math.Max(_rotaLongest, now);
            Logger?.Info("Company check (rota, " + place.Name + "): " + sheet.Stations.Count + " station(s) in " + sheet.Shop.Groups + " group(s), " + sheet.Workers.Count + " employee(s), " + sheet.FixedCount
                         + " left as they are, " + o.CustomersWeek + " customers a week by the game's calculator, " + sheet.Shop.Margin + " a customer in hundredths (" + (sheet.FromBooks ? "the books" : "the prices")
                         + "); hours now " + o.HoursNow + ", with the staff " + o.HoursStaffed + ", with all it takes " + o.HoursBest + (o.Barred ? ", with a late opening permit " + o.HoursFree : string.Empty)
                         + "; a week in hundredths " + o.ProfitNow + ", " + o.ProfitStaffed + ", " + o.ProfitBest + "; the rota it has covers " + sheet.Now.CoveredHours + " of " + sheet.Now.WantedHours
                         + " wanted hours with " + sheet.Now.UnmetCount + " demand(s) unmet; worked out in " + (_rotaSpent + now).ToString("0.0") + " ms over " + (_rotaFrames + 1) + " frame(s), the longest share of a frame "
                         + longest.ToString("0.0") + " ms (wanted: about " + RotaSliceMs.ToString("0") + " ms, the frame that reads the game apart): " + (longest <= RotaSliceMs * 4 ? "PASS" : "SLOW"));
        }

        // ---------------------------------------------------------------- filling and putting back

        internal bool RotaHasUndo(string place)
        {
            return RotaUndoAt(place) >= 0;
        }

        private int RotaUndoAt(string place)
        {
            for (int i = 0; i < _rotaUndo.Count; i++)
            {
                if (_rotaUndo[i].Place == place)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Why a business's rota cannot be filled, as a text key (its text may take {stations} and {staff}, the most the planner keeps); null if it can.</summary>
        internal static string RotaRefusal(RotaSheet sheet)
        {
            return sheet == null ? "reallife_rota_off"
                : sheet.Place.Type == RotaType.Warehouse ? "reallife_rota_warehouse"
                : sheet.TooLarge ? "reallife_rota_too_large"
                : sheet.Stations.Count == 0 ? "reallife_rota_no_stations"
                : sheet.Workers.Count == 0 ? "reallife_rota_no_staff"
                : null;
        }

        /// <summary>
        /// "Fill the rota" and "Use these hours and fill the rota": asked for by the player on the page, which shows
        /// the buttons only for a business that is worked out. The game's own confirm dialogue comes first where
        /// there is a rota to write over or hours to change, as it comes before the game's own auto fill. The rota
        /// is then made and written by a job, over the coming frames.
        /// </summary>
        public void FillRota(string place, int rung)
        {
            bool useHours = rung >= 0;
            if (!RotaOn)
            {
                return;
            }
            try
            {
                RotaPlace at = RotaPlaceAt(place);
                RotaSheet sheet = at != null && RotaFresh(at) ? _rotaSheet : null;
                if (sheet == null && at != null && !RotaUnread(place))
                {
                    RotaAsk(place, JobShow, false);   // (it changed since the page was written: the page is written again, and the buttons with it)
                    Changed();
                    return;
                }
                string refusal = sheet == null ? "reallife_rota_cannot" : RotaRefusal(sheet) ?? (useHours && RotaRungOf(sheet, rung) == null ? "reallife_rota_no_hours" : null);
                if (refusal != null)
                {
                    Notifications.Show(NotificationType.Warning, refusal, new Dictionary<string, string>
                    {
                        { "stations", RotaCatalogue.StationsMost.ToString() }, { "staff", RotaCatalogue.WorkersMost.ToString() }
                    }, 8f, null, null, true, false);
                    return;
                }
                int kind = useHours ? JobFillHours : JobFill;
                if (!useHours && !RotaGame.HasRota(sheet) && sheet.Ladder.FillLeft.Count == 0)
                {
                    RotaAsk(place, kind, false, -1);
                    return;
                }
                RotaRung plan = useHours ? RotaRungOf(sheet, rung) : null;
                if (plan == null && sheet.Ladder.FillLeft.Count > 0)
                {
                    // The hours kept cannot hold somebody's least hours: said with the figures, and the plan that can is offered in its place.
                    RotaLeft left = sheet.Ladder.FillLeft[0];
                    RotaRung can = RotaRungOf(sheet, 1);
                    can = can != null && can.Left.Count == 0 ? can : null;
                    string who = left.Worker < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[left.Worker]) : "?";
                    HudConfirm.Show("reallife_rota_tab".Localize(), (can != null ? "reallife_rota_confirm_short_plan" : "reallife_rota_confirm_short").Localize(new
                    {
                        name = who, least = left.Least, room = left.Why == RotaClash.Shared ? left.Held : left.Room, count = sheet.Ladder.FillLeft.Count,
                        now = sheet.Ladder.Rungs[0].Sum.Hours, after = can != null ? can.Sum.Hours : sheet.Ladder.Rungs[0].Sum.Hours
                    }), () => RotaConfirmed(place, can != null ? JobFillHours : kind, can != null ? 1 : -1), null, null, null, false, false);
                    return;
                }
                if (plan == null)
                {
                    HudConfirm.Show(null, "reallife_rota_confirm", () => RotaConfirmed(place, kind, -1), null, null, null, true, false);
                    return;
                }
                // A plan that changes the opening hours is always asked about, with the hours now and after and what it brings.
                HudConfirm.Show("reallife_rota_tab".Localize(), "reallife_rota_confirm_plan".Localize(new
                {
                    now = sheet.Ladder.Rungs[0].Sum.Hours, after = plan.Sum.Hours, gain = CompanyText.Money((plan.Sum.Net - sheet.Ladder.Rungs[0].Sum.Net) / 100f)
                }), () => RotaConfirmed(place, kind, rung), null, null, null, false, false);
            }
            catch (Exception e)
            {
                RotaFault("the fill button", e);
            }
        }

        private void RotaConfirmed(string place, int kind, int rung)
        {
            try
            {
                if (RotaOn)
                {
                    RotaAsk(place, kind, false, rung);
                    Changed();
                }
            }
            catch (Exception e)
            {
                RotaFault("the fill button", e);
            }
        }

        // The last piece of a fill, in one frame: the sheet has just been found true of the business as it is. The week as it
        // was is kept first (the oldest one, if it is filled twice), then the rota is written.
        private void RotaWrite(RotaJob job, RotaPlace at, RotaSheet sheet, RotaPlan plan, RotaRung rung)
        {
            bool useHours = rung != null;
            if (plan.Stations != sheet.Stations.Count || (rung != null && (rung.Hires > 0 || rung.Workers != sheet.Workers.Count)))
            {
                return;   // (never a rota with people in it who are not there)
            }
            string place = job.Place, name = at.Name;   // (the list of places is made again below, and its entries with it)
            int kept = RotaUndoAt(place);
            if (kept < 0)
            {
                _rotaUndo.Add(RotaGame.Keep(at, job.Test));
            }
            else
            {
                _rotaUndo[kept].Kept |= job.Test;
            }
            double before = _rotaClock.Elapsed.TotalMilliseconds;
            int shifts = RotaGame.Write(sheet, plan, useHours ? rung.Open : null);
            double wrote = _rotaClock.Elapsed.TotalMilliseconds - before;
            _rotaSheetGood = false;
            _rotaOwnOf = null;
            RotaRead();
            at = RotaPlaceAt(place);
            _rotaMade = at != null ? place : null;
            _rotaMadeStamp = at != null ? RotaGame.Stamp(at, RotaDays(at)) : 0;
            _rotaTestFilled += job.Test ? 1 : 0;
            _rotaTestShifts += job.Test ? shifts : 0;
            Logger?.Info("Company: rota written for " + name + (useHours ? ", with the plan's opening hours (" + rung.Sum.Hours + " a week, " + sheet.Ladder.Rungs[0].Sum.Hours + " before)" : string.Empty) + ": " + shifts + " shift(s), "
                         + plan.CoveredHours + " of " + plan.WantedHours + " wanted hours covered, " + plan.UnmetCount + " demand(s) unmet" + (plan.Level > 0 ? " (optional demands gave way)" : string.Empty)
                         + ", " + sheet.FixedCount + " employee(s) left as they were, wages " + plan.Wage / 100 + " a week; worked out in " + _rotaSpent.ToString("0.0") + " ms over " + (_rotaFrames + 1)
                         + " frame(s), the longest share of a frame " + _rotaLongest.ToString("0.0") + " ms; written in " + wrote.ToString("0.0") + " ms.");
            if (!job.Test)
            {
                Notifications.Show(plan.Full ? NotificationType.Success : NotificationType.Info, plan.Full ? "reallife_rota_note_filled" : "reallife_rota_note_partly", new Dictionary<string, string>
                {
                    { "business", name }, { "covered", plan.CoveredHours.ToString() }, { "wanted", plan.WantedHours.ToString() }, { "shifts", shifts.ToString() }
                }, 9f, null, null, true, false);
                Changed();
            }
        }

        /// <summary>"Put the old rota back": the week as it was before the first fill since the app was opened.</summary>
        public void RotaBack(string place)
        {
            if (!RotaOn)
            {
                return;
            }
            try
            {
                string name = RotaPlaceAt(place) != null ? RotaPlaceAt(place).Name : "?";
                if (RotaPutBack(place) >= 0)
                {
                    Notifications.Show(NotificationType.Success, "reallife_rota_note_back", new Dictionary<string, string> { { "business", name } }, 7f, null, null, true, false);
                    Changed();
                }
            }
            catch (Exception e)
            {
                RotaFault("putting a rota back", e);
            }
        }

        private int RotaPutBack(string place)
        {
            int at = RotaUndoAt(place);
            RotaPlace where = RotaPlaceAt(place);
            if (at < 0)
            {
                return -1;
            }
            RotaUndo undo = _rotaUndo[at];
            _rotaUndo.RemoveAt(at);
            for (int i = _rotaJobs.Count - 1; i >= 0; i--)
            {
                if (_rotaJobs[i].Place == place && _rotaJobs[i].Kind != JobShow && (i > 0 || _rotaStage == StageRead))
                {
                    _rotaJobs.RemoveAt(i);   // (a fill of it that still waits is not wanted any more)
                }
            }
            if (where == null)
            {
                return -1;   // (the business is no longer the player's: there is nothing to put a rota back into)
            }
            string name = where.Name;   // (the list of places is made again below, and its entries with it)
            int shifts = RotaGame.PutBack(where, undo);
            _rotaSheetGood = false;
            _rotaOwnOf = null;
            _rotaMade = null;
            RotaRead();
            Logger?.Info("Company: the rota and opening hours of " + name + " are as they were before the planner wrote them: " + shifts + " shift(s) back.");
            return shifts;
        }

        /// <summary>The Company app has been closed: the weeks kept to be put back are let go (those a test tool kept stay until its own button).</summary>
        internal void RotaLeft()
        {
            for (int i = _rotaUndo.Count - 1; i >= 0; i--)
            {
                if (!_rotaUndo[i].Kept)
                {
                    _rotaUndo.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------- the ladder of plans

        /// <summary>
        /// The plan of a sheet's ladder that may be written: the best week of the people there are, if it is not
        /// the week as it is. Never a plan that takes somebody still to be hired. Null otherwise.
        /// </summary>
        internal static RotaRung RotaRungOf(RotaSheet sheet, int rung)
        {
            RotaLadderResult ladder = sheet != null ? sheet.Ladder : null;
            RotaRung r = ladder != null && rung > 0 && rung < ladder.Count ? ladder.Rungs[rung] : null;
            return r != null && RotaLadder.Writable(r) && r.Workers == sheet.Workers.Count ? r : null;
        }

        /// <summary>How a business weighs what its people ask for: the player's choice, or <see cref="RotaMode.Auto"/>.</summary>
        internal int RotaModeOf(string place)
        {
            int mode;
            return place != null && _rotaModes.TryGetValue(place, out mode) ? mode : RotaMode.Auto;
        }

        /// <summary>The player's choice for a business, from its page. The plans are worked out again with it; it is kept in the save.</summary>
        public void RotaChoose(string place, int mode)
        {
            try
            {
                if (!RotaOn || string.IsNullOrEmpty(place) || RotaModeOf(place) == mode)
                {
                    return;
                }
                if (mode == RotaMode.StaffFirst || mode == RotaMode.Lean)
                {
                    _rotaModes[place] = mode;
                }
                else
                {
                    _rotaModes.Remove(place);
                }
                _rotaModesUnread = false;
                RotaSave(Save);
                Logger?.Info("Company: the staff rota planner's choice for " + place + " is now " + (mode == RotaMode.StaffFirst ? "staff first" : mode == RotaMode.Lean ? "lean" : "whichever comes to more") + ".");
                Changed();
            }
            catch (Exception e)
            {
                RotaFault("a choice", e);
            }
        }

        partial void RotaLoad(GameInstance save)
        {
            string text = null;
            _rotaModes.Clear();
            _rotaModesUnread = save != null && save.modData != null && save.modData.TryGetValue(RotaStore.Key, out text) && !RotaStore.Read(text, _rotaModes);
            if (_rotaModesUnread)
            {
                Logger?.Warn("Company: the staff rota planner's choices in this save are not of a version this one reads; every business weighs for itself until a choice is made, and the line is left as it is until then.");
            }
            else if (Verbose)
            {
                Logger?.Info("Company check (rota choices): " + _rotaModes.Count + " business(es) with a choice of their own read from the save (version " + RotaStore.Version + "): PASS");
            }
        }

        partial void RotaSave(GameInstance save)
        {
            if (_rotaModesUnread || save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (_rotaModes.Count == 0)
            {
                save.modData.Remove(RotaStore.Key);
            }
            else
            {
                save.modData[RotaStore.Key] = RotaStore.Write(_rotaModes);
            }
        }

        // True for a business the cleaning service cleans on its round (asked through CompanyBridges: Wiring/ServicesLinks).
        private static bool RotaCleaned(RotaPlace place)
        {
            try
            {
                return CompanyBridges.Cleaned != null && place.Reg != null && CompanyBridges.Cleaned(place.Reg.Address);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True while the cleaning service is there to be asked (the page names it as a way out for a cleaner with too few hours).</summary>
        internal static bool RotaCleaningService => CompanyBridges.Cleaned != null;

        // The going rate against the game's wage formula where a business stands, thousandths.
        private int RotaMarket(RotaPlace place)
        {
            CompanyShop shop = null;
            for (int i = 0; i < _shops.Count; i++)
            {
                shop = ReferenceEquals(_shops[i].Reg, place.Reg) ? _shops[i] : shop;
            }
            return StaffRules.Market(_index, _cycle, shop != null ? shop.Income : CompanyCatalogue.IncomeMiddle);
        }

        /// <summary>
        /// Another business of the player's where somebody with one of these skills would find hours: the one with
        /// the most open hours that have nobody on a shift, among those with a station the skill fits. Null with none.
        /// Asked when a page is written for somebody whose hours cannot be met; a walk over the other businesses' items.
        /// </summary>
        internal RotaPlace RotaBusier(RotaPlace from, EmployeeInstance who)
        {
            RotaPlace best = null;
            try
            {
                for (int i = 0; who != null && who.characterData != null && who.characterData.skills != null && i < _rotaPlaces.Count; i++)
                {
                    RotaPlace p = _rotaPlaces[i];
                    if (p == from || p.Reg == null || p.Type == RotaType.Warehouse || p.Shut || p.GapHours <= 0 || p.Reg.itemInstances == null || (best != null && p.GapHours <= best.GapHours))
                    {
                        continue;
                    }
                    bool fits = false;
                    foreach (var item in p.Reg.itemInstances.Values)
                    {
                        string[] skills = item != null && item.ItemCached != null ? item.ItemCached.suitableSkills : null;
                        for (int k = 0; !fits && skills != null && k < skills.Length; k++)
                        {
                            for (int j = 0; !fits && j < who.characterData.skills.Count; j++)
                            {
                                fits = who.characterData.skills[j] != null && who.characterData.skills[j].name == skills[k];
                            }
                        }
                        if (fits)
                        {
                            break;
                        }
                    }
                    best = fits ? p : best;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Company: the other businesses could not be looked through for somebody's hours (" + e.Message + ").");
            }
            return best;
        }

        // One rung in a line of the log: its hours, what its week comes to and what that is made of. Money in whole units.
        private static string RotaRungLog(RotaSheet sheet, RotaRung r)
        {
            RotaWeekSum s = r.Sum;
            string what = r.Kind == RotaRungKind.AsItIs ? "as it is" : r.Kind == RotaRungKind.Best ? "best with the people (" + (r.Source == 0 ? "the week as it is" : r.Source == 1 ? "the hours kept, a new rota"
                : r.Source == 2 ? "the hours kept less those nobody can keep" : "the hours worth opening") + ")" : "hire " + r.Hires;
            for (int i = 0; i < r.Hires; i++)
            {
                what += (i == 0 ? " (" : ", ") + RotaGame.GroupSkill(sheet, r.Hire[i]) + " at " + (RotaLadder.Going(sheet.Shop, r.Hire[i]) / 100f).ToString("0.00") + (i == r.Hires - 1 ? ")" : string.Empty);
            }
            return what + ": " + s.Hours + " h open, " + s.Manned + " manned, " + s.StaffHours + " staff h (" + s.CleanHours + " cleaning), " + s.Served + " of " + s.Come + " customers served ("
                   + s.LostUnmanned + " found nobody, " + s.LostFull + " over what the stations serve), sales " + s.Sales / 100 + " (dirt takes " + s.Clean / 100 + "), wages " + s.Wages / 100 + ", fees " + s.Fees / 100
                   + ", theft " + s.Theft / 100 + ", night risk " + s.Night / 100 + ", demands " + s.Demands / 100 + ", net " + s.Net / 100 + (r.Kind != RotaRungKind.AsItIs ? ", gain " + r.Gain / 100 : string.Empty)
                   + ", " + r.Plan.UnmetCount + " demand(s) unmet";
        }

        // The ladder of a business in the log: the self-check line of each working out (verbose), and the whole of it for the test tool.
        private void RotaPlansLog(RotaPlace place, RotaSheet sheet, bool always)
        {
            if (!always && !Verbose)
            {
                return;
            }
            RotaLadderResult l = sheet.Ladder;
            RotaShop shop = sheet.Shop;
            bool pass = l.Count >= 1;
            int left = l.FillLeft.Count;
            for (int i = 1; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                left += r.Left.Count;
                for (int w = 0; w < sheet.Workers.Count && w < r.Workers; w++)
                {
                    pass &= RotaFit.Inside(sheet.Problem.Worker[w], r.HoursOf[w]);   // (the hours a week people ask for, both ends, in every plan)
                }
            }
            string hours = string.Empty, rungs = string.Empty;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                hours += (h > 0 ? " " : string.Empty) + l.Hourly[h];
            }
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                pass &= r.Kind != RotaRungKind.Hire || r.Gain >= RotaLadder.GainLeast;
                rungs += (i > 0 ? " > " : string.Empty) + (r.Kind == RotaRungKind.AsItIs ? "now" : r.Kind == RotaRungKind.Best ? "best" : "hire " + r.Hires) + " " + r.Sum.Hours + " h " + r.Sum.Net / 100;
            }
            string stop = l.Stop == RotaEnd.TooLittle ? "one more for " + RotaGame.GroupSkill(sheet, l.StopGroup) + " would add " + l.StopGain / 100 + ", under the " + l.StopNeed / 100 + " a rung must"
                : l.Stop == RotaEnd.Most ? "as many people as one ladder advises" : l.Stop == RotaEnd.NoAdvice ? "no hours are advised here" : "everybody the paying hours take is there";
            string line = "Company check (rota plans, " + place.Name + "): " + rungs + "; recommended rung " + l.Recommended + "; ends because " + stop + "; " + (l.Mode == RotaMode.StaffFirst ? "staff first" : "lean")
                          + (l.ModeAuto ? " (chosen by the week: lean " + l.LeanNet / 100 + ", staff first " + l.StaffNet / 100 + ")" : " (the player's choice)") + (shop.Cleaned ? "; the cleaning service cleans it" : string.Empty)
                          + "; everybody in every plan inside the hours a week they ask for or without hours (" + left + " left without, over the plans and the fill)"
                          + "; customers a week by the hour of the day (the game's calculator): " + hours + "; a customer " + shop.Margin + " hundredths, satisfaction " + shop.Content + ", cleanliness " + shop.CleanNow
                          + ", dirt a customer " + shop.DirtPerCustomer + " and a cleaner's hour " + shop.CleanPerHour + " (thousandths of a point), theft a customer " + shop.TheftRate + " hundredths up to " + shop.TheftLimit
                          + " an hour, a night shut " + shop.NightRisk + " hundredths; " + l.Steps + " step(s): " + (pass ? "PASS" : "FAIL");
            if (always)
            {
                Logger?.Warn(line);
            }
            else
            {
                Logger?.Info(line);
            }
            for (int i = 0; always && i < l.Count; i++)
            {
                Logger?.Warn("Company test (rota plans, " + place.Name + "), rung " + i + ", " + RotaRungLog(sheet, l.Rungs[i]) + ".");
            }
        }

        // ---------------------------------------------------------------- the test tools

        private bool RotaTestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyCompany || !RotaOn)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            RotaRead();
            if (_rotaPlaces.Count == 0)
            {
                TestRefusals.TellKey(Logger, test, "reallife_co_test_no_business");
                return false;
            }
            return RotaOn;
        }

        /// <summary>"Test: open the rota of my busiest business": the one the game's calculator gives the most customers in a week; with none that has customers, the one with the most staff.</summary>
        public void TestRotaOpen()
        {
            RotaTestBusiest(false);
        }

        /// <summary>"Test: show my busiest business's plans in the log": the same business, its ladder of plans rung by rung with what each week is made of.</summary>
        public void TestRotaPlans()
        {
            RotaTestBusiest(true);
        }

        private void RotaTestBusiest(bool plans)
        {
            if (!RotaTestReady(plans ? "Rota plans" : "Rota open"))
            {
                return;
            }
            try
            {
                RotaPlace best = null;
                int most = -1;
                for (int i = 0; i < _rotaPlaces.Count; i++)
                {
                    RotaPlace place = _rotaPlaces[i];
                    int customers = place.Type == RotaType.Customers ? RotaGame.WeekCustomers(place.Reg) : 0;
                    int key = customers * 1000 + Math.Min(999, place.Staff);
                    if (key > most)
                    {
                        most = key;
                        best = place;
                    }
                }
                if (plans)
                {
                    Logger?.Warn("Company test: the plans of " + best.Name + " (" + most / 1000 + " customers a week by the game's calculator, " + best.Staff + " staff) follow in the log"
                                 + (RotaFresh(best) ? "." : " once they are worked out, over the coming frames."));
                    if (RotaFresh(best))
                    {
                        RotaPlansLog(best, _rotaSheet, true);
                    }
                    else
                    {
                        RotaAsk(best.Place, JobShow, true);
                    }
                    return;
                }
                Logger?.Warn("Company test: the rota of " + best.Name + " opened (" + most / 1000 + " customers a week by the game's calculator, " + best.Staff + " staff).");
                Open(TabRota, "rota:" + best.Place);
            }
            catch (Exception e)
            {
                RotaFault("a test (open)", e);
            }
        }

        /// <summary>
        /// "Test: fill every business's rota": each business with staff gets a rota for the hours it keeps, without
        /// the confirm dialogue, one after another over the coming frames; a notice counts them at the end. The
        /// weeks as they were are kept until "Test: put every rota back".
        /// </summary>
        public void TestRotaFillAll()
        {
            if (!RotaTestReady("Rota fill"))
            {
                return;
            }
            try
            {
                _rotaTestFilled = _rotaTestShifts = 0;
                for (int i = 0; i < _rotaPlaces.Count; i++)
                {
                    if (_rotaPlaces[i].Type != RotaType.Warehouse && _rotaPlaces[i].Staff > 0)
                    {
                        RotaAsk(_rotaPlaces[i].Place, JobFill, true);
                    }
                }
                RotaAsk(string.Empty, JobTell, true);
            }
            catch (Exception e)
            {
                RotaFault("a test (fill)", e);
            }
        }

        /// <summary>"Test: put every rota back": every week kept is put back, the tests' and the player's own.</summary>
        public void TestRotaBackAll()
        {
            if (!RotaTestReady("Rota back"))
            {
                return;
            }
            int back = 0;
            try
            {
                while (_rotaUndo.Count > 0 && RotaOn)
                {
                    back += RotaPutBack(_rotaUndo[_rotaUndo.Count - 1].Place) >= 0 ? 1 : 0;
                }
            }
            catch (Exception e)
            {
                RotaFault("a test (back)", e);
            }
            if (back == 0)
            {
                TestRefusals.TellKey(Logger, "Rota back", "reallife_rota_test_nothing_back");
                return;
            }
            Logger?.Warn("Company test: " + back + " rota(s) put back as they were.");
            Notifications.Show(NotificationType.Info, "reallife_rota_test_put_back", new Dictionary<string, string> { { "count", back.ToString() } }, 7f, null, () => Open(TabRota, null), true, false);
            Changed();
        }
    }
}
