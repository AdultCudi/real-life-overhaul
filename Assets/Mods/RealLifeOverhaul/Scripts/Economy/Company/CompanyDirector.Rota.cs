using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const int StageRead = 0, StageAdvise = 1, StageRate = 2, StageSolve = 3, StageWrite = 4;

        // One thing asked of the planner: a business worked out for its page, a rota filled, or the notice that ends a run of test fills.
        private struct RotaJob
        {
            public string Place;
            public int Kind;
            public bool Test;
        }

        private readonly List<RotaPlace> _rotaPlaces = new List<RotaPlace>(16), _rotaPool = new List<RotaPlace>(16);
        private readonly List<RotaUndo> _rotaUndo = new List<RotaUndo>(4);
        private readonly List<RotaJob> _rotaJobs = new List<RotaJob>(4);
        private readonly List<string> _rotaUnread = new List<string>(2);   // places whose figures could not be read from the game this session
        private readonly RotaSheet _rotaSheet = new RotaSheet();
        private readonly RotaSolver _rotaSolver = new RotaSolver();
        private readonly RotaAdvisor _rotaAdvisor = new RotaAdvisor();
        private readonly RotaPlan _rotaPlan = new RotaPlan();
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
            StaffFile f = On(CompanyPart.Staff) && _state != null ? _state.Employee(id) : null;
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
            return unchecked(sum * 4) + (On(CompanyPart.Staff) ? 1 : 0) + (permit ? 2 : 0);
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

        private void RotaAsk(string place, int kind, bool test)
        {
            for (int i = 0; i < _rotaJobs.Count; i++)
            {
                if (_rotaJobs[i].Place == place && _rotaJobs[i].Kind == kind)
                {
                    return;   // asked already
                }
            }
            _rotaJobs.Add(new RotaJob { Place = place, Kind = kind, Test = test });
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
                        _rotaStage = StageSolve;
                        return RotaToFill(job, sheet);
                    }
                    _rotaSheetGood = false;
                    _rotaRaise = _rotaRaise ?? RotaRaise;
                    int days = RotaDays(place);
                    RotaGame.Read(sheet, place, _rotaPromotion, _rotaRaise);
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
                    if (RotaGame.Stamp(place, RotaDays(place)) != sheet.Stamp)
                    {
                        return RotaAgain(job, place);   // (the business changed while it was being worked out: what was read is no longer it)
                    }
                    sheet.Late = !sheet.Permit && RotaLate(sheet.Shop.Open);
                    sheet.Shown = _rotaMade == place.Place && _rotaMadeStamp == sheet.Stamp ? sheet.Now : null;
                    _rotaSheetGood = true;
                    RotaCheck(place, sheet);
                    _rotaStage = StageSolve;
                    return RotaToFill(job, sheet);
                case StageSolve:
                    _rotaStage = _rotaSolver.Step(RotaStepsAtOnce) ? StageWrite : StageSolve;
                    return false;
                default:
                    if (!RotaFresh(place))
                    {
                        return RotaAgain(job, place);   // (never a rota from a reading that is no longer true)
                    }
                    RotaWrite(job, place, sheet, job.Kind == JobFillHours ? sheet.Outlook.Plan : _rotaPlan);
                    return true;
            }
        }

        // The sheet is ready. A job that only shows it is done; a fill goes on to its rota: the one that came with the
        // advice, or one for the hours the business keeps (the sheet's problem holds their worth), made over the coming frames.
        private bool RotaToFill(RotaJob job, RotaSheet sheet)
        {
            if (job.Kind == JobShow || RotaRefusal(sheet) != null || (job.Kind == JobFillHours && !sheet.Outlook.Advised))
            {
                return true;
            }
            if (job.Kind == JobFillHours)
            {
                _rotaStage = StageWrite;
            }
            else
            {
                _rotaSolver.Start(sheet.Problem, _rotaPlan);
            }
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
        public void FillRota(string place, bool useHours)
        {
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
                string refusal = sheet == null ? "reallife_rota_cannot" : RotaRefusal(sheet) ?? (useHours && !sheet.Outlook.Advised ? "reallife_rota_no_hours" : null);
                if (refusal != null)
                {
                    Notifications.Show(NotificationType.Warning, refusal, new Dictionary<string, string>
                    {
                        { "stations", RotaCatalogue.StationsMost.ToString() }, { "staff", RotaCatalogue.WorkersMost.ToString() }
                    }, 8f, null, null, true, false);
                    return;
                }
                int kind = useHours ? JobFillHours : JobFill;
                if (!useHours && !RotaGame.HasRota(sheet))
                {
                    RotaAsk(place, kind, false);
                    return;
                }
                HudConfirm.Show(null, useHours ? "reallife_rota_confirm_hours" : "reallife_rota_confirm", () => RotaConfirmed(place, kind), null, null, null, true, false);
            }
            catch (Exception e)
            {
                RotaFault("the fill button", e);
            }
        }

        private void RotaConfirmed(string place, int kind)
        {
            try
            {
                if (RotaOn)
                {
                    RotaAsk(place, kind, false);
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
        private void RotaWrite(RotaJob job, RotaPlace at, RotaSheet sheet, RotaPlan plan)
        {
            bool useHours = job.Kind == JobFillHours;
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
            int shifts = RotaGame.Write(sheet, plan, useHours ? sheet.Outlook.Staffed : null);
            double wrote = _rotaClock.Elapsed.TotalMilliseconds - before;
            _rotaSheetGood = false;
            _rotaOwnOf = null;
            RotaRead();
            at = RotaPlaceAt(place);
            _rotaMade = at != null ? place : null;
            _rotaMadeStamp = at != null ? RotaGame.Stamp(at, RotaDays(at)) : 0;
            _rotaTestFilled += job.Test ? 1 : 0;
            _rotaTestShifts += job.Test ? shifts : 0;
            Logger?.Info("Company: rota written for " + name + (useHours ? ", with the advised opening hours (" + sheet.Outlook.HoursStaffed + " a week)" : string.Empty) + ": " + shifts + " shift(s), "
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
            if (!RotaTestReady("Rota open"))
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
