using System;
using System.Collections.Generic;
using System.Diagnostics;
using Entities;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The staff rota planner's part: the places, one business's sheet, the player's choices. Idle cost: nothing per
    /// frame and no timer; a sheet is worked out by a job (CompanyDirector.RotaJobs.cs) and kept while still true.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        public const int TabRota = 5;
        private const int RotaAlertKind = 30;       // the Planner's id for "the rota has gaps", beside the company's own (1 to 14)
        private const double RotaSliceMs = 2.0;     // milliseconds of a frame a job may take
        private const int RotaStepsAtOnce = 2;      // steps between two looks at the clock, each a fraction of a millisecond
        private const int RotaRestartsMost = 3;     // times a job starts again because its business changed under it
        private const int JobShow = 0, JobFill = 1, JobFillHours = 2, JobTell = 3;
        private const int StageRead = 0, StageAdvise = 1, StageRate = 2, StageWrite = 4, StageLadder = 5;

        private readonly List<RotaPlace> _rotaPlaces = new List<RotaPlace>(16), _rotaPool = new List<RotaPlace>(16);
        private readonly List<RotaUndo> _rotaUndo = new List<RotaUndo>(4);
        private readonly List<RotaJob> _rotaJobs = new List<RotaJob>(4);
        private readonly List<string> _rotaUnread = new List<string>(2);   // places the game's figures could not be read for this session
        private readonly RotaSheet _rotaSheet = new RotaSheet();
        private readonly RotaSolver _rotaSolver = new RotaSolver();
        private readonly RotaAdvisor _rotaAdvisor = new RotaAdvisor();
        private readonly RotaLadder _rotaLadder = new RotaLadder();
        private readonly Dictionary<string, int> _rotaModes = new Dictionary<string, int>(8);   // staff first or lean, by place (RotaStore)
        private bool _rotaModesUnread;              // the save's line is of a version this one cannot read: kept as it is (Core/SaveLines)
        private readonly int[] _rotaPromotion = new int[RotaCatalogue.Days];
        private readonly bool[] _rotaBarred = new bool[RotaCatalogue.Cells];
        private readonly Stopwatch _rotaClock = new Stopwatch();
        private Func<string, int> _rotaRaise;
        private bool _rotaBroken, _rotaSheetGood, _rotaWorking;
        private int _rotaStage, _rotaRestarts, _rotaFrames, _rotaTestFilled, _rotaTestShifts;
        private double _rotaSpent, _rotaLongest;
        private BuildingRegistration _rotaOwnOf;    // the business whose own promotion figure was last asked for...
        private int _rotaOwnHour, _rotaOwn;         // ...in which game hour, and what it was
        private string _rotaWriting;                // the place a write is under way at: a fault puts its week back
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
            Logger?.Warn("Company: the staff rota planner switched off for this session after an error in " + where
                         + ". No rota is written by it any more; the game's own schedule and the rest of the company are not affected.");
        }

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

        /// <summary>The sheet if worked out and still true; else null, and a job is started. Costs one stamp.</summary>
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
                    _rotaSheet.Place = place;   // the list of places is made afresh from time to time
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

        /// <summary>True while a job for a business waits or runs.</summary>
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

        internal bool RotaUnread(string place) => _rotaUnread.Contains(place);

        private bool RotaFresh(RotaPlace place)
        {
            return _rotaSheetGood && ReferenceEquals(_rotaSheet.Reg, place.Reg) && _rotaSheet.Stamp == RotaGame.Stamp(place, RotaDays(place));
        }

        private void RotaUnreadable(RotaPlace place, Exception e)
        {
            _rotaSheetGood = false;
            _rotaUnread.Add(place.Place);
            Logger?.Error(e);
            Logger?.Warn("Company: the staff rota planner could not read " + place.Name
                         + " from the game and leaves it out for this session; its page says so. Other businesses are still planned.");
        }

        private CompanyShop RotaShopOf(RotaPlace place)
        {
            for (int i = _shops.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_shops[i].Reg, place.Reg))
                {
                    return _shops[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Fills <see cref="_rotaPromotion"/> with the promotion figure the mod will have written on each weekday ahead
        /// (-1: as it stands), by the function that writes it (BusinessCatalogue.Written). Returns a number that changes
        /// when any of it does, or an agreed raise there; bit 1 is set while the late opening permit is held or licences are off.
        /// </summary>
        private int RotaDays(RotaPlace place)
        {
            CompanyShop shop = RotaShopOf(place);
            // Day 1 is a Monday (TimeHelper.GetDayOfWeek).
            int today = Save.Day, weekday = ((today - 1) % RotaCatalogue.Days + RotaCatalogue.Days) % RotaCatalogue.Days, sum = 0;
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
            int raises = _state != null ? StaffRules.RaisesAt(_state.Staff, place.Place) : 0;   // the wages are read with them
            return (unchecked(sum * 4) + (On(CompanyPart.Staff) ? 1 : 0) + (permit ? 2 : 0)) ^ (RotaModeOf(place.Place) << 24)
                   ^ (RotaCleaned(place) ? 1 << 27 : 0) ^ ((shop != null ? shop.Income : 0) << 12) ^ (_index << 4) ^ unchecked(raises << 2);
        }

        // A business's own promotion figure before the mod's, as the mod's one writer of it says; asked once a game hour.
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

        /// <summary>How a business weighs what its people ask for: the player's choice, or <see cref="RotaMode.Auto"/>.</summary>
        internal int RotaModeOf(string place)
        {
            int mode;
            return place != null && _rotaModes.TryGetValue(place, out mode) ? mode : RotaMode.Auto;
        }

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
                if (_rotaModesUnread)
                {
                    // The line this version could not read gives way to the choice made now, or it would be put back at every save.
                    SaveLines.Release(Save.modData, RotaStore.Key);
                    _rotaModesUnread = false;
                    Logger?.Info("Company: the staff rota planner's line in this save, of a version this one cannot read, is replaced by the choice made now.");
                }
                RotaSave(Save);
                Logger?.Info("Company: the staff rota planner's choice for " + place + " is now "
                             + (mode == RotaMode.StaffFirst ? "staff first" : mode == RotaMode.Lean ? "lean" : "whichever comes to more") + ".");
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
                SaveLines.Unreadable(save.modData, RotaStore.Key, "its version is not one this version of the mod reads");
                Logger?.Warn("Company: the staff rota planner's choices in this save are not of a version this one reads; every business weighs for itself"
                             + " this session, and the line is kept in the save as it is.");
            }
            else if (Verbose)
            {
                Logger?.Info("Company check (rota choices): " + _rotaModes.Count + " business(es) with a choice of their own read from the save (version "
                             + RotaStore.Version + "): PASS");
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

        // True for a business the cleaning service cleans on its round (Wiring/ServicesLinks).
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

        /// <summary>True while the cleaning service is there to be asked: the page names it as a way out for a cleaner.</summary>
        internal static bool RotaCleaningService => CompanyBridges.Cleaned != null;

        // The going rate against the game's wage formula where a business stands, thousandths.
        private int RotaMarket(RotaPlace place)
        {
            CompanyShop shop = RotaShopOf(place);
            return StaffRules.Market(_index, _cycle, shop != null ? shop.Income : CompanyCatalogue.IncomeMiddle);
        }

        /// <summary>The other business with the most unstaffed open hours and a station these skills fit; null with none.</summary>
        internal RotaPlace RotaBusier(RotaPlace from, EmployeeInstance who)
        {
            RotaPlace best = null;
            try
            {
                for (int i = 0; who != null && who.characterData != null && who.characterData.skills != null && i < _rotaPlaces.Count; i++)
                {
                    RotaPlace p = _rotaPlaces[i];
                    if (p == from || p.Reg == null || p.Type == RotaType.Warehouse || p.Shut || p.GapHours <= 0 || p.Reg.itemInstances == null
                        || (best != null && p.GapHours <= best.GapHours))
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
    }
}
