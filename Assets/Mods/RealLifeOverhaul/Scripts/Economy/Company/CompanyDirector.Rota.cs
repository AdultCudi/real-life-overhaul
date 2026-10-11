using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

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
    /// are, and a rota of the mod's own making, written only when the player presses the button, through the same
    /// fields and the same follow-up calls as the game's own (RotaGame.Write).
    ///
    /// NOTHING RUNS BY ITSELF. No Update, no timer, nothing on the hour. The list of places is read on the day
    /// event (one pass, for the Planner's row "the rota has gaps", which is a count of open hours with nobody on a
    /// shift and no more), when the app opens and after a rota is written. A business is worked out when it is
    /// picked on the tab, and kept until something it was worked out from changes (RotaGame.Stamp). Nothing is saved:
    /// every figure is worked out from the game, and the rota that was there before a fill is kept in memory only,
    /// until the app is closed.
    ///
    /// A business whose figures cannot be read from the game is left out for the session with one line, and its page
    /// says so; a fault in writing a rota switches the whole planner off for the session. Either way the rest of the
    /// company goes on.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        public const int TabRota = 5;
        private const int RotaAlertKind = 30;   // the Planner's id for "the rota has gaps", beside the company's own (1 to 14)

        private readonly List<RotaPlace> _rotaPlaces = new List<RotaPlace>(16), _rotaPool = new List<RotaPlace>(16);
        private readonly List<RotaUndo> _rotaUndo = new List<RotaUndo>(4);
        private readonly RotaSheet _rotaSheet = new RotaSheet();
        private readonly RotaSolver _rotaSolver = new RotaSolver();
        private readonly RotaAdvisor _rotaAdvisor = new RotaAdvisor();
        private readonly RotaPlan _rotaPlan = new RotaPlan();
        private readonly int[] _rotaScale = new int[RotaCatalogue.Days];
        private readonly List<string> _rotaUnread = new List<string>(2);   // places whose figures could not be read from the game this session
        private Func<string, int> _rotaRaise;
        private bool _rotaBroken, _rotaSheetGood;
        private string _rotaMade;               // the place whose rota as it stands is the planner's own...
        private long _rotaMadeStamp;            // ...for as long as nothing it was made from has changed

        /// <summary>Whether the planner runs: the company is running, its option is on and no fault has stopped it.</summary>
        public bool RotaOn => _running && !_broken && !_rotaBroken && Settings.EconomyRota;

        internal RotaAdvisor RotaAdvisor => _rotaAdvisor;

        private void RotaFault(string where, Exception e)
        {
            _rotaBroken = true;
            _rotaSheetGood = false;
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
            _rotaSheetGood = false;
            _rotaSheet.Place = null;
            _rotaSheet.Reg = null;
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
        /// The sheet of a business: read from the game and worked out if it is not the one kept, or if anything it was
        /// worked out from has changed since. Null if the planner is off or has just stopped on a fault.
        /// </summary>
        internal RotaSheet RotaSheetFor(RotaPlace place)
        {
            if (!RotaOn || place == null || place.Reg == null || Save == null || _rotaUnread.Contains(place.Place))
            {
                return null;
            }
            try
            {
                float started = Time.realtimeSinceStartup;
                int extra = RotaScales(place);
                long stamp = RotaGame.Stamp(place, extra);
                if (_rotaSheetGood && ReferenceEquals(_rotaSheet.Reg, place.Reg) && _rotaSheet.Stamp == stamp)
                {
                    _rotaSheet.Place = place;   // (the list of places is made again from time to time: the business is the same one)
                    return _rotaSheet;
                }
                _rotaSheetGood = false;
                _rotaRaise = _rotaRaise ?? RotaRaise;
                RotaGame.Read(_rotaSheet, place, _rotaScale, _rotaRaise);
                _rotaSheet.Stamp = stamp;
                RotaSheet sheet = _rotaSheet;
                _rotaAdvisor.Advise(sheet.Shop, sheet.Problem, _rotaSolver, sheet.Now.At, sheet.Outlook);
                // Left as the page and the fill button need it: the worth of the hours it keeps now, and the rota it has rated against them.
                _rotaAdvisor.Grid(sheet.Shop, sheet.Shop.Open, sheet.Problem);
                _rotaSolver.Rate(sheet.Problem, sheet.Now);
                sheet.Late = On(CompanyPart.Licences) && RotaLate(sheet.Outlook.Staffed) && !RotaLate(sheet.Shop.Open);
                sheet.Shown = _rotaMade == place.Place && _rotaMadeStamp == stamp ? sheet.Now : null;
                _rotaSheetGood = true;
                if (Verbose)
                {
                    RotaOutlook o = sheet.Outlook;
                    Logger?.Info("Company check (rota, " + place.Name + "): " + sheet.Stations.Count + " station(s) in " + sheet.Shop.Groups + " group(s), " + sheet.Workers.Count + " employee(s), "
                                 + o.CustomersWeek + " customers a week by the game's calculator, " + sheet.Shop.Margin + " a customer in hundredths (" + (sheet.FromBooks ? "the books" : "the prices")
                                 + "); hours now " + o.HoursNow + ", with the staff " + o.HoursStaffed + ", with all it takes " + o.HoursBest + "; a week in hundredths " + o.ProfitNow + ", " + o.ProfitStaffed + ", "
                                 + o.ProfitBest + "; the rota it has covers " + sheet.Now.CoveredHours + " of " + sheet.Now.WantedHours + " wanted hours with " + sheet.Now.UnmetCount + " demand(s) unmet; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS");
                }
                return _rotaSheet;
            }
            catch (Exception e)
            {
                // One business the game's figures cannot be read for: it is left out, and the others are still planned.
                _rotaSheetGood = false;
                _rotaUnread.Add(place.Place);
                Logger?.Error(e);
                Logger?.Warn("Company: the staff rota planner could not read " + place.Name + " from the game and leaves it out for this session; its page says so. Other businesses are still planned.");
                return null;
            }
        }

        /// <summary>True for a business the planner could not read from the game this session.</summary>
        internal bool RotaUnread(string place)
        {
            return _rotaUnread.Contains(place);
        }

        private int RotaRaise(string id)
        {
            StaffFile f = On(CompanyPart.Staff) && _state != null ? _state.Employee(id) : null;
            return f != null ? f.Raise : 0;
        }

        // What each weekday's customers are against today's, thousandths: the game's figure holds what the company adds to
        // footfall today (a campaign, overtime, a strike: all through the business's promotion figure, see RotaGame), and on
        // another day of the coming week that may have ended or begun. The company's own rules say what it is on each day.
        // Returns a number that changes when the scales do.
        private int RotaScales(RotaPlace place)
        {
            CompanyShop shop = null;
            for (int i = 0; i < _shops.Count; i++)
            {
                shop = ReferenceEquals(_shops[i].Reg, place.Reg) ? _shops[i] : shop;
            }
            int today = Save.Day, weekday = ((today - 1) % RotaCatalogue.Days + RotaCatalogue.Days) % RotaCatalogue.Days, sum = 0;   // (the game's own count: day 1 is a Monday, TimeHelper.GetDayOfWeek)
            int city = shop != null ? CityEconomyDirector.FootfallOf(place.Reg) : 0;
            int now = shop != null && shop.Applied ? shop.Asked : 0;
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                int then = shop != null && Promoted(shop) && !_holding ? Footfall(shop, today + (d - weekday + RotaCatalogue.Days) % RotaCatalogue.Days) : now;
                _rotaScale[d] = then == now ? 1000 : Mathf.Clamp(1000 * Mathf.Max(10, 100 + city + then) / Mathf.Max(10, 100 + city + now), 100, 4000);
                sum = sum * 31 + _rotaScale[d];
            }
            return sum + (On(CompanyPart.Staff) ? 1 : 0) + (On(CompanyPart.Licences) ? 2 : 0);
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

        /// <summary>Why a business's rota cannot be filled, as a text key; null if it can.</summary>
        internal static string RotaRefusal(RotaSheet sheet)
        {
            return sheet == null ? "reallife_rota_off"
                : sheet.Place.Type == RotaType.Warehouse ? "reallife_rota_warehouse"
                : sheet.Stations.Count == 0 ? "reallife_rota_no_stations"
                : sheet.Workers.Count == 0 ? "reallife_rota_no_staff"
                : null;
        }

        /// <summary>
        /// "Fill the rota" and "Use these hours and fill the rota": asked for by the player on the page. The game's
        /// own confirm dialogue comes first where there is a rota to write over or hours to change, as it comes
        /// before the game's own auto fill.
        /// </summary>
        public void FillRota(string place, bool useHours)
        {
            if (!RotaOn)
            {
                return;
            }
            try
            {
                RotaSheet sheet = RotaSheetFor(RotaPlaceAt(place));
                string refusal = sheet == null && RotaUnread(place) ? "reallife_rota_cannot" : RotaRefusal(sheet) ?? (useHours && !sheet.Outlook.Advised ? "reallife_rota_no_hours" : null);
                if (refusal != null)
                {
                    Notifications.Show(NotificationType.Warning, refusal, null, 8f, null, null, true, false);
                    return;
                }
                if (!useHours && !RotaGame.HasRota(sheet))
                {
                    RotaWrite(place, false, false, true);
                    return;
                }
                HudConfirm.Show(null, useHours ? "reallife_rota_confirm_hours" : "reallife_rota_confirm", () => RotaWrite(place, useHours, false, true), null, null, null, true, false);
            }
            catch (Exception e)
            {
                RotaFault("the fill button", e);
            }
        }

        // The rota is worked out and written. The week as it was is kept first (the oldest one, if it is filled twice).
        // Returns the shifts written, or -1 if nothing was.
        private int RotaWrite(string place, bool useHours, bool test, bool tell)
        {
            if (!RotaOn)
            {
                return -1;
            }
            try
            {
                RotaPlace at = RotaPlaceAt(place);
                RotaSheet sheet = RotaSheetFor(at);   // (asked again: the dialogue was open for a while, and the sheet is read afresh if anything changed meanwhile)
                if (RotaRefusal(sheet) != null || (useHours && !sheet.Outlook.Advised))
                {
                    return -1;
                }
                float started = Time.realtimeSinceStartup;
                int kept = RotaUndoAt(place);
                if (kept < 0)
                {
                    _rotaUndo.Add(RotaGame.Keep(at, test));
                }
                else
                {
                    _rotaUndo[kept].Kept |= test;
                }
                // With "use these hours" the rota is the one that came with the advice; otherwise one for the hours the business keeps (the sheet's problem holds their worth).
                RotaPlan plan = useHours ? sheet.Outlook.Plan : _rotaSolver.Solve(sheet.Problem, _rotaPlan);
                float solved = Time.realtimeSinceStartup;
                string name = at.Name;   // (the list of places is made again below, and its entries with it)
                int shifts = RotaGame.Write(sheet, plan, useHours ? sheet.Outlook.Staffed : null);
                _rotaSheetGood = false;
                RotaRead();
                at = RotaPlaceAt(place);
                _rotaMade = at != null ? place : null;
                _rotaMadeStamp = at != null ? RotaGame.Stamp(at, RotaScales(at)) : 0;
                Logger?.Info("Company: rota written for " + name + (useHours ? ", with the advised opening hours (" + sheet.Outlook.HoursStaffed + " a week)" : string.Empty) + ": " + shifts + " shift(s), "
                             + plan.CoveredHours + " of " + plan.WantedHours + " wanted hours covered, " + plan.UnmetCount + " demand(s) unmet" + (plan.Level > 0 ? " (optional demands gave way)" : string.Empty)
                             + ", wages " + plan.Wage / 100 + " a week; worked out in " + ((solved - started) * 1000f).ToString("0.0") + " ms, written in " + ((Time.realtimeSinceStartup - solved) * 1000f).ToString("0.0") + " ms.");
                if (tell)
                {
                    Notifications.Show(plan.Full ? NotificationType.Success : NotificationType.Info, plan.Full ? "reallife_rota_note_filled" : "reallife_rota_note_partly", new Dictionary<string, string>
                    {
                        { "business", name }, { "covered", plan.CoveredHours.ToString() }, { "wanted", plan.WantedHours.ToString() }, { "shifts", shifts.ToString() }
                    }, 9f, null, null, true, false);
                    Changed();
                }
                return shifts;
            }
            catch (Exception e)
            {
                RotaFault("writing a rota", e);
                return -1;
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
                if (RotaPutBack(place) >= 0)
                {
                    Notifications.Show(NotificationType.Success, "reallife_rota_note_back", new Dictionary<string, string> { { "business", RotaPlaceAt(place) != null ? RotaPlaceAt(place).Name : "?" } }, 7f, null, null, true, false);
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
            if (where == null)
            {
                return -1;   // (the business is no longer the player's: there is nothing to put a rota back into)
            }
            string name = where.Name;   // (the list of places is made again below, and its entries with it)
            int shifts = RotaGame.PutBack(where, undo);
            _rotaSheetGood = false;
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

        /// <summary>"Test: fill every business's rota": each business with stations and staff gets a rota for the hours it keeps, without the confirm dialogue. The weeks as they were are kept until "Test: put every rota back".</summary>
        public void TestRotaFillAll()
        {
            if (!RotaTestReady("Rota fill"))
            {
                return;
            }
            int filled = 0, shifts = 0;
            var places = new List<string>(_rotaPlaces.Count);   // (the list of places is read again after each rota written: their names are taken first)
            for (int i = 0; i < _rotaPlaces.Count; i++)
            {
                places.Add(_rotaPlaces[i].Place);
            }
            for (int i = 0; RotaOn && i < places.Count; i++)
            {
                int written = RotaWrite(places[i], false, true, false);
                if (written >= 0)
                {
                    filled++;
                    shifts += written;
                }
            }
            Logger?.Warn("Company test: " + filled + " rota(s) filled, " + shifts + " shift(s) written; the weeks as they were are kept for 'Test: put every rota back'.");
            Notifications.Show(NotificationType.Info, "reallife_rota_test_filled", new Dictionary<string, string> { { "count", filled.ToString() }, { "shifts", shifts.ToString() } }, 9f, null, () => Open(TabRota, null), true, false);
            Changed();
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
