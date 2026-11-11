using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Controllers;
using Entities;
using Localizor;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.GameUi;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Working at a desk of one's own tech firm (OwnDeskRules). Inside a firm of the player's, a free desk is a thing to click
    /// to work, as a job's desk is (Careers/DeskGame); the player walks to it, the game's own work panel offers a stint
    /// (Jobs/ShiftSkip: Start, the skip-time button, fast forward, Stop), they sit at it as the game seats its own staff
    /// (WorkActivity), and every minute worked is kept on the firm by the day. The next morning's crew counts those minutes as
    /// a professional of the player's role, level and skills would give them (<see cref="OwnShare"/>), with no pay. Energy
    /// goes at the desk's own rate; Careers is told the hours (learning, a long week's burnout, a restraint clause). The length
    /// of a stint is the player's to pick on the panel's slider before it starts (<see cref="OwnDeskRules.LeastHours"/> to
    /// <see cref="OwnDeskRules.MostHours"/> hours, eight to begin with): every figure above follows the minutes worked, so
    /// nothing else assumes a day. The firms' own staff and the rivals work the professions' day and are not touched. Cost:
    /// nothing per frame; once a second inside a firm a look at the desks found on entering (the building's items read once a
    /// visit, and again at most every ten seconds while none is free).
    /// </summary>
    public sealed partial class TechDirector
    {
        private const float DeskLookSeconds = 10f;   // real seconds between looks over the building's desks while none was free

        private readonly List<EmployeeStationController> _desks = new List<EmployeeStationController>(16);
        private Action<EntityController> _onDesk;
        private string _deskInside;                  // the firm (its place key) the player stands in, once a second; null outside
        private float _deskLookAt;
        private OwnStint _stint;                     // the player's stint at a desk, from the panel's offer until it is over
        private int _ownHours = OwnDeskRules.DefaultHours;   // the hours last chosen on the panel's slider; kept in the save (OwnDeskRules.HoursKey), the default for a save without them
        private EmployeeStationController _sharedDesk;   // the free desk offered through the desk sessions' panel, theirs to click (TechBridges.SessionDesk); null otherwise

        // Once a second while the firms run: the firm the player stands in, a free desk of it to click, a stint watched.
        private void DeskSecond()
        {
            BuildingRegistration reg = FirmsGame.InsideFirm();
            string place = reg != null ? TalentGame.Place(reg) : null;
            if (place != _deskInside)
            {
                _deskInside = place;
                _desks.Clear();
                if (place != null)
                {
                    if (FirmOf(place) == null)
                    {
                        // a firm founded since the morning is taken in, as opening the Tech app takes it in
                        ReadPeople(Save.Day);
                        ReadFirms(Save.Day);
                        Crews();
                    }
                    FirmsGame.Desks(_desks);
                    _deskLookAt = Time.realtimeSinceStartup + DeskLookSeconds;
                }
            }
            WatchStint(reg, place);
            OfferDesk(place != null && FirmOf(place) != null ? reg : null);
        }

        // A free desk of the firm the player is in is the one they may click; nothing to click elsewhere, while they work, or with working for oneself off.
        // The desk sessions' own desk (Living population) answers its click with their panel, which offers this work too: another
        // free desk is clicked for the work first, and where theirs is the only free one, the work is offered there through it.
        private void OfferDesk(BuildingRegistration reg)
        {
            EmployeeStationController free = null, session = null;
            if (reg != null && _stint == null && Settings.CareersOwn)
            {
                session = TechBridges.SessionDeskNow();
                free = FreeDesk(session);
                if (free == null && Time.realtimeSinceStartup >= _deskLookAt)
                {
                    FirmsGame.Desks(_desks);   // a desk placed or freed since the last look
                    _deskLookAt = Time.realtimeSinceStartup + DeskLookSeconds;
                    free = FreeDesk(session);
                }
            }
            _sharedDesk = free != null && ReferenceEquals(free, session) ? free : null;
            if (free == null || _sharedDesk != null)
            {
                if (DeskGame.OtherTarget != null && _onDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onDesk))
                {
                    DeskGame.OtherTarget = null;
                    DeskGame.OtherClicked = null;
                }
                return;
            }
            if (!DeskGame.Clickable && !DeskGame.Install())
            {
                return;
            }
            if (_onDesk == null)
            {
                _onDesk = DeskClicked;
            }
            DeskGame.OtherTarget = free;
            DeskGame.OtherClicked = _onDesk;
        }

        // The first desk of the building's (in the order of its items) that nobody sits at now, the sessions' desk only when no other is free.
        private EmployeeStationController FreeDesk(EmployeeStationController session)
        {
            bool sessionFree = false;
            for (int i = 0; i < _desks.Count; i++)
            {
                if (FirmsGame.Free(_desks[i]))
                {
                    if (!ReferenceEquals(_desks[i], session))
                    {
                        return _desks[i];
                    }
                    sessionFree = true;
                }
            }
            return sessionFree ? session : null;
        }

        /// <summary>
        /// True while a click on a free desk of the firm the player stands in leads to work at it (their own click, or the Work button of the desk sessions' panel at their desk): a contract
        /// of the player's rides on it (TechBridges.ContractDesk), so Careers clicks no desk of its own there (CareersBridges.FirmDesk).
        /// </summary>
        public bool OwnDeskOffered => _firmsOn && _stint == null && (_sharedDesk != null || (_onDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onDesk)));

        /// <summary>True while the work is offered at the desk sessions' own desk, the only free one (their panel then offers it; SessionBridges.WorkHere).</summary>
        public bool WorksAt(EmployeeStationController desk)
        {
            return _firmsOn && _stint == null && desk != null && ReferenceEquals(desk, _sharedDesk) && FirmsGame.Free(desk);
        }

        /// <summary>The work at the sessions' desk chosen on their panel: as a click on a free desk, with its refusals said (SessionBridges.Work).</summary>
        public void WorkAt(EmployeeStationController desk)
        {
            if (WorksAt(desk))
            {
                DeskClicked(desk);
            }
        }

        // A click on the desk: the contract of the player's that Careers would have them work here (paid work for a client) and the firm's own work are told apart by
        // OwnPlaceRules.ChoiceAt. One that can be worked goes to the walk, or the other's reason is said; where both can be worked the player is asked which they mean.
        private void DeskClicked(EntityController e)
        {
            EmployeeStationController desk = e as EmployeeStationController;
            FirmPlace firm = DeskFirm(desk);
            if (firm == null)
            {
                return;
            }
            string client = TechBridges.ContractClientAt(desk);
            switch (OwnPlaceRules.ChoiceAt(client != null, DeskRefusal(firm, out _, out _) == null))
            {
                case DeskChoice.Contract:
                    ContractDeskWork(desk);
                    break;
                case DeskChoice.Ask:
                    AskDeskWork(desk, client, firm.File.Name);
                    break;
                default:
                    FirmDeskWork(desk);
                    break;
            }
        }

        // The firm of the player's they stand in, for a click on a desk of it; null while a stint is on, the firms are off or no save is loaded.
        private FirmPlace DeskFirm(EmployeeStationController desk)
        {
            BuildingRegistration reg = FirmsGame.InsideFirm();
            FirmPlace firm = reg != null ? FirmOf(TalentGame.Place(reg)) : null;
            return firm == null || desk == null || _stint != null || !_firmsOn || Save == null ? null : firm;
        }

        // Both can be worked at this desk: the game's question box, the contract on its confirm and the firm's work on its cancel, which is what a click on the desk always was.
        private void AskDeskWork(EmployeeStationController desk, string client, string firm)
        {
            bool asked = GameConfirm.TryShow("reallife_own_pick_title".Localize(), "reallife_own_pick_body".Localize(new { client, firm = firm ?? string.Empty }),
                () => ContractDeskWork(desk), () => FirmDeskWork(desk), "reallife_own_pick_contract", "reallife_own_pick_firm", false, false);
            if (!asked)
            {
                FirmDeskWork(desk);   // the game has no question box the mod can call: the click as it was
            }
        }

        // The contract at this desk, as Careers takes the click; the firm's own work if it can no longer be worked (an answer given late).
        private void ContractDeskWork(EmployeeStationController desk)
        {
            if (!TechBridges.ContractAt(desk))
            {
                FirmDeskWork(desk);
            }
        }

        // The firm's own work at this desk: why not, in the game's notification, or the player walks to it and the game's work panel offers a stint.
        private void FirmDeskWork(EmployeeStationController desk)
        {
            FirmPlace firm = DeskFirm(desk);
            if (firm == null || !FirmsGame.Free(desk))
            {
                return;
            }
            string why = DeskRefusal(firm, out OwnerStanding standing, out string professions);
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, Data("professions", professions), 10f);
                return;
            }
            if (Save.Energy <= 0f)
            {
                Notifications.ShowInsufficientEnergy();
                return;
            }
            DeskGame.WalkTo(desk, () =>
            {
                try
                {
                    OpenStint(firm, desk, standing);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
            });
        }

        // Why the player cannot work at a desk of a firm now, as a key; null if they can, with their standing.
        private static string DeskRefusal(FirmPlace firm, out OwnerStanding standing, out string professions)
        {
            if (!TechBridges.OwnerAt(firm.Reg != null ? firm.Reg.businessTypeName : null, out standing, out string why, out professions))
            {
                return why;
            }
            return FirmsGame.Shut(firm.Reg) ? "reallife_tech_own_why_shut" : null;
        }

        private void OpenStint(FirmPlace firm, EmployeeStationController desk, OwnerStanding standing)
        {
            if (_stint != null || !FirmsGame.Free(desk))
            {
                return;
            }
            var stint = new OwnStint(this, desk, firm.File.Place, firm.File.Name, standing, _ownHours);
            stint.Skip = ShiftSkip.Offer(this, stint);
            _stint = stint.Skip != null ? stint : null;
        }

        // While a stint is on: over if the player has left the firm, or is no longer the one at the desk (the game seated its own employee).
        private void WatchStint(BuildingRegistration reg, string place)
        {
            OwnStint stint = _stint;
            if (stint == null)
            {
                return;
            }
            if (stint.Skip == null || !stint.Skip.Open)
            {
                EndStint(stint, false);   // the panel let it go before it began, or ended it unseen
                return;
            }
            if (reg == null || place != stint.Place || (stint.Begun && !FirmsGame.PlayerAt(stint.Desk)))
            {
                stint.Skip.Close();
                EndStint(stint, false);
            }
        }

        private void StintStarted(OwnStint stint)
        {
            BuildingRegistration reg = FirmsGame.InsideFirm();
            if (!ReferenceEquals(stint, _stint) || reg == null || TalentGame.Place(reg) != stint.Place || !FirmsGame.Free(stint.Desk))
            {
                stint.Skip?.Close();
                EndStint(stint, false);
                return;
            }
            if (_onDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onDesk))
            {
                DeskGame.OtherTarget = null;
                DeskGame.OtherClicked = null;
            }
            stint.Begun = true;   // first: from here every end of the stint stands the player up (EndStint), whatever fails below
            if (!DeskGame.SitAt(stint.Desk))
            {
                stint.Skip?.Close();   // the game would not seat them: given back free to walk and told why; the panel closes with nothing worked
                EndStint(stint, false);
                return;
            }
            FirmsGame.Tiring(stint.Desk, true);
            Logger?.Info("Tech: the player works at a desk of '" + stint.Name + "' from hour " + Save.Hour + " of day " + Save.Day + " as "
                         + TalentCatalogue.Role(stint.Standing.Role).Key + " (level " + stint.Standing.Level + ") for " + stint.Hours + " hour(s).");
        }

        // The clock moved: the minutes since the last count go on the firm, under today.
        private void StintWorked(OwnStint stint, int minutes)
        {
            if (!stint.Begun || minutes <= stint.Credited || _firmsFile == null || Save == null)
            {
                return;
            }
            OwnDeskRules.Credit(_firmsFile.FirmAt(stint.Place), Save.Day, minutes - stint.Credited);
            stint.Credited = minutes;
        }

        // The stint over, whichever way: the player stands up, the game's own employee comes back to the desk, Careers is told, and so is the player.
        private void EndStint(OwnStint stint, bool told)
        {
            if (ReferenceEquals(_stint, stint))
            {
                _stint = null;
            }
            if (!stint.Begun)
            {
                return;   // it never began: nothing worked, nothing to stand up from
            }
            stint.Begun = false;
            FirmsGame.Tiring(null, false);
            EmployeeStationController desk = stint.Desk;
            DeskGame.StandFrom(desk);   // the desk let go only while the player is at it; the pose and the work hold always
            if (desk != null && isActiveAndEnabled)
            {
                StartCoroutine(Guard.Routine(RestaffSoon(desk)));
            }
            int minutes = stint.Credited, day = Save != null ? Save.Day : 0;
            int points = OwnDeskRules.Points(stint.Standing, minutes, day);
            if (minutes > 0)
            {
                try
                {
                    TechBridges.OwnerWorked?.Invoke(stint.Standing.Prof, stint.Standing.Rank, minutes);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            if (told && minutes > 0)
            {
                TellTech(NotificationType.Success, "reallife_tech_own_note_done", Data("firm", stint.Name, "time", TechText.Minutes(minutes), "points", points.ToString(CultureInfo.InvariantCulture)),
                    TabFirms, TechApp.FirmRow + stint.Place);
            }
            Logger?.Info("Tech: the player's stint at a desk of '" + stint.Name + "' is over after " + minutes + " minute(s): about " + points + " work point(s) for its next morning.");
            ChangedTech();
        }

        // A frame after the player stands up, as the game's WorkActivity waits before it seats the desk's own employee again.
        private IEnumerator RestaffSoon(EmployeeStationController desk)
        {
            yield return null;
            try
            {
                DeskGame.Restaff(desk);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The firms stand down: a stint is called off, the click taken back.
        private void DeskDown()
        {
            SeatTestDown();
            if (_stint != null)
            {
                OwnStint stint = _stint;
                stint.Skip?.Close();
                EndStint(stint, false);
            }
            if (_onDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onDesk))
            {
                DeskGame.OtherTarget = null;
                DeskGame.OtherClicked = null;
            }
            _deskInside = null;
            _sharedDesk = null;
            _desks.Clear();
        }

        /// <summary>
        /// The owner's day at a desk of a firm in the morning's crew: yesterday's minutes there, as Careers reads the player now
        /// (switched off, or no longer qualified, and they count for nothing). Their engineering joins the firm's seniors and faults.
        /// </summary>
        private void OwnShare(FirmPlace firm, ref Crew own, ref int seniors, ref long faultWeighted)
        {
            firm.OwnMinutes = OwnDeskRules.MinutesOn(firm.File, Save.Day - 1);
            firm.OwnPoints = 0;
            firm.OwnRole = -1;
            if (firm.OwnMinutes <= 0 || firm.Reg == null || !TechBridges.OwnerAt(firm.Reg.businessTypeName, out OwnerStanding s, out _, out _))
            {
                return;
            }
            firm.OwnRole = s.Role;
            firm.OwnPoints = OwnDeskRules.Join(ref own, s, firm.OwnMinutes, Save.Day, ref seniors, ref faultWeighted);
        }

        // With Developer diagnostics on, once a morning for each firm the owner worked at: their points against a professional's of the same profile and hours.
        private void OwnShareChecked(int day)
        {
            if (!Verbose)
            {
                return;
            }
            for (int i = 0; i < _firms.Count; i++)
            {
                FirmPlace firm = _firms[i];
                if (firm.OwnMinutes <= 0)
                {
                    continue;
                }
                if (firm.OwnRole < 0 || !TechBridges.OwnerAt(firm.Reg.businessTypeName, out OwnerStanding s, out string why, out _))
                {
                    Logger?.Info("Tech check (own desk, day " + day + "): " + firm.OwnMinutes + " minute(s) of the owner's at '" + firm.File.Name
                                 + "' count for nothing: Careers no longer has them as a professional it employs: PASS");
                    continue;
                }
                Profile p = OwnDeskRules.AsProfile(s);
                int hours = firm.OwnMinutes / 60, rest = firm.OwnMinutes % 60;
                long staff = (long)TalentRules.Output(p, TalentCatalogue.Clamp(s.Main, 0, 100), day) * FirmsCatalogue.PointsPerDay * hours / TalentCatalogue.DayHours / 1000;
                int whole = OwnDeskRules.Points(s, hours * 60, day);
                Logger?.Info("Tech check (own desk, day " + day + "): the owner's " + firm.OwnMinutes + " minute(s) at '" + firm.File.Name + "' as " + TalentCatalogue.Role(s.Role).Key
                             + " level " + s.Level + ": " + firm.OwnPoints + " point(s); a professional of the same profile gives " + staff + " for the " + hours
                             + " whole hour(s), the owner " + whole + " (and " + rest + " minute(s) more): " + (whole == staff && firm.OwnPoints >= whole ? "PASS" : "FAIL"));
            }
        }

        /// <summary>The firm page's word on working there oneself: who the player would be at a desk and what it gives, or what is missing.</summary>
        internal void OwnDeskLine(InfoBox box, FirmPlace firm)
        {
            if (!Settings.CareersOwn)
            {
                box.Line(TechText.T("reallife_tech_own_page_off"), CellTone.Yellow);
                return;
            }
            if (!TechBridges.OwnerAt(firm.Reg != null ? firm.Reg.businessTypeName : null, out OwnerStanding s, out string why, out string professions))
            {
                box.Line(why == "reallife_tech_own_why_none" ? TechText.T("reallife_tech_own_page_none", "professions", professions) : TechText.T("reallife_tech_own_page_off"), CellTone.Yellow);
                return;
            }
            box.Line(TechText.Fill("reallife_tech_own_page_can", "role", TechText.Role(s.Role), "level", TechText.Level(s.Level),
                "points", OwnDeskRules.Points(s, OwnDeskRules.StintMinutes, Save != null ? Save.Day : 0).ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>For Options > Tests: a full stint at a desk of the firm the player stands in (or the first) counted as worked today, for tomorrow's crew.</summary>
        public void TestOwnDesk()
        {
            if (!_firmsOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return;
            }
            ReadPeople(Save.Day);
            ReadFirms(Save.Day);
            Crews();
            BuildingRegistration inside = FirmsGame.InsideFirm();
            FirmPlace firm = inside != null ? FirmOf(TalentGame.Place(inside)) : null;
            firm = firm ?? (_firms.Count > 0 ? _firms[0] : null);
            if (firm == null)
            {
                Notifications.ShowError("reallife_tech_test_own_none");
                return;
            }
            if (!TechBridges.OwnerAt(firm.Reg.businessTypeName, out OwnerStanding s, out string why, out string professions))
            {
                Notifications.Show(NotificationType.Warning, why, Data("professions", professions), 10f);
                return;
            }
            int day = Save.Day, before = OwnDeskRules.MinutesOn(firm.File, day), stint = OwnDeskRules.MinutesFor(_ownHours);   // the stint as the slider last stood
            OwnDeskRules.Credit(firm.File, day, stint);
            int after = OwnDeskRules.MinutesOn(firm.File, day);
            var own = new Crew();
            int seniors = 0;
            long faults = 0;
            int points = OwnDeskRules.Join(ref own, s, stint, day, ref seniors, ref faults);
            int given = own.Engineering + own.Testing + own.Design + own.Product + own.Selling;
            bool ok = after == Math.Min(OwnDeskRules.DayMost, before + stint) && points == OwnDeskRules.Points(s, stint, day)
                      && (TalentCatalogue.Role(s.Role).Family == Family.Support ? own.Support == 1 : given == points);
            Logger?.Info("Tech check (own desk test): a stint of " + stint / 60 + " hour(s) at '" + firm.File.Name + "' counted under day " + day
                         + " (" + before + " minute(s) before, " + after + " after) as "
                         + TalentCatalogue.Role(s.Role).Key + " level " + s.Level + ": " + points + " point(s) for tomorrow's crew, " + given + " in its buckets: " + (ok ? "PASS" : "FAIL"));
            if (!ok)
            {
                Notifications.ShowError("reallife_tech_test_own_fail");
                return;
            }
            TellTech(NotificationType.Success, "reallife_tech_own_note_done", Data("firm", firm.File.Name, "time", TechText.Minutes(stint),
                "points", points.ToString(CultureInfo.InvariantCulture)), TabFirms, TechApp.FirmRow + firm.File.Place);
            ChangedTech();
        }

        /// <summary>A stint at a desk as the game's work panel runs it (Jobs/ShiftSkip): its words, and its steps handed to the director.</summary>
        private sealed class OwnStint : ISlidingShift
        {
            private readonly TechDirector _director;
            public readonly EmployeeStationController Desk;
            public readonly string Place, Name;
            public readonly OwnerStanding Standing;
            public ShiftSkip Skip;
            public bool Begun;       // sat down at the desk, the clock running
            public int Credited;     // minutes of it already put on the firm
            public int Hours;        // the hours it runs: the slider's, as it stood when the panel opened until the player moves it

            public OwnStint(TechDirector director, EmployeeStationController desk, string place, string name, OwnerStanding standing, int hours)
            {
                Hours = OwnDeskRules.ClampHours(hours);
                _director = director;
                Desk = desk;
                Place = place;
                Name = name ?? string.Empty;
                Standing = standing;
            }

            public string SkipTitle => TechText.T("reallife_tech_own_title", "firm", Name);

            public int SkipMinutes => OwnDeskRules.MinutesFor(Hours);

            public int SlideLeast => OwnDeskRules.LeastHours;

            public int SlideMost => OwnDeskRules.MostHours;

            public int SlideNow => Hours;

            // The slider moved: the hours kept for the next stint too (written with the save), the panel's length follows.
            public void Slide(int value)
            {
                Hours = OwnDeskRules.ClampHours(value);
                _director._ownHours = Hours;
            }

            // The hours chosen, the time they end at if the stint began now, and the work points they give the next morning.
            public (string key, object arguments) SlideInfo()
            {
                int minutes = SkipMinutes, day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
                return ("reallife_tech_own_slider", new
                {
                    hours = Hours,
                    time = TimeHelper.Now().AddMinutes(minutes).GetFormattedTime(),
                    points = OwnDeskRules.Points(Standing, minutes, day).ToString(CultureInfo.InvariantCulture)
                });
            }

            public LabelInfo[] SkipLabels()
            {
                int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
                return new[]
                {
                    DeskGame.Label(TechText.Fill("reallife_tech_own_line", "role", TechText.Role(Standing.Role), "level", TechText.Level(Standing.Level),
                        "points", OwnDeskRules.Points(Standing, OwnDeskRules.StintMinutes, day).ToString(CultureInfo.InvariantCulture)), 0),
                    DeskGame.Label(TechText.T("reallife_tech_own_line_unpaid"), 0)
                };
            }

            public void SkipStarted() => _director.StintStarted(this);

            public void SkipWorked(int minutes) => _director.StintWorked(this, minutes);

            public void SkipEnded(int minutes, bool whole)
            {
                _director.StintWorked(this, minutes);
                _director.EndStint(this, true);
            }
        }
    }
}
