using System;
using System.Collections;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A freelance contract worked at a desk of the player's own office business, as a job's desk is worked (Careers/DeskGame's click, the walk, the game's work panel with its slider of
    /// hours, the seat, the stand-up): inside a business of theirs where a taken contract can be done, a free desk is a thing to click, and the click walks the player to it and offers
    /// the contract due first (OwnRules.WorkedBefore). A tech firm's own click carries the contract (TechBridges.ContractDesk), since the firm's own work waits on the same desk. The
    /// Career app starts a stint itself only where there is no desk to click: a trade's at the client's, a contract worked at home, an office with no desk free, the option off.
    /// Cost: one second's look inside a business of theirs (a loop over six contracts at most); the building's desks are read once a visit, and every ten seconds while none is free.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const float OwnDeskLookSeconds = 10f;   // real seconds between looks over the building's desks while none was free

        private readonly List<EmployeeStationController> _ownDesks = new List<EmployeeStationController>(16);
        private Action<EntityController> _onOwnDesk;
        private string _ownDeskInside;      // the business of the player's (its place key) they stand in, once a second; null outside
        private bool _ownDeskScanned;       // its desks were read this visit
        private float _ownDeskLookAt;
        private bool _ownDeskNone;          // in such a business with a contract to work here and no desk free to click
        private bool _ownDeskOff;           // stood down for the session after a fault
        private ContractStint _ownStint;    // the stint offered or under way, from the panel's offer until it is over
        private int _ownHours = OwnRules.SittingHoursDefault;   // the hours last picked on the slider, kept while the game runs

        /// <summary>True while a contract's desk work is started at the place, a free desk of an office business of the player's, not from the Career app: its switch on, the click there.</summary>
        internal bool OwnDeskWay => Settings.CareersOwnDesk && OwnOn && !_ownDeskOff && DeskGame.Clickable;

        /// <summary>The slider moved to these hours: kept for the next stint, while the game runs.</summary>
        internal void OwnHoursChosen(int hours)
        {
            _ownHours = hours;
        }

        /// <summary>
        /// True where the Career app starts a taken contract's stint itself, as it starts a job's day only where no desk could be had: a trade's at the client's, a contract worked at home,
        /// one in an office of the player's with no desk free to click, and every one with the desk way switched off.
        /// </summary>
        internal bool ContractFromApp(Contract c)
        {
            if (c == null || c.Stage != (int)ContractStep.Taken || OwnRules.Left(c) <= 0)
            {
                return false;
            }
            Profession p = c.Def;
            return !OwnDeskWay || p == null || OwnRules.IsTrade(p) || CareersGame.AtHome() || _ownDeskNone;
        }

        /// <summary>Show on map for a taken contract's work: a trade's client, else the first office business of the player's it can be done in. Null if done, else a reason key.</summary>
        internal string ShowContractPlace(int id)
        {
            Contract c = ContractOf(id);
            Profession p = c != null ? c.Def : null;
            if (c == null || p == null || c.Stage != (int)ContractStep.Taken)
            {
                return "reallife_own_why_no_contract";
            }
            if (OwnRules.IsTrade(p))
            {
                return ShowPlace(c.Client, c.Street, c.Number);
            }
            ReadMine();
            for (int i = 0; i < _mine.Count; i++)
            {
                if (OfficeFits(p, _mine[i]))
                {
                    return ShowPlace(TalentGame.NameOf(_mine[i]), _mine[i].StreetName, _mine[i].StreetNumber);
                }
            }
            return "reallife_own_why_no_office";
        }

        // Once a second while the working life runs: the business of the player's they stand in, a free desk of it to click for a contract of theirs, and a stint watched.
        private void OwnDeskSecond()
        {
            if (_ownDeskOff)
            {
                return;
            }
            try
            {
                BuildingRegistration reg = FirmsGame.InsideOwn();
                string place = reg != null ? TalentGame.Place(reg) : null;
                if (place != _ownDeskInside)
                {
                    _ownDeskInside = place;
                    _ownDesks.Clear();
                    _ownDeskScanned = false;
                }
                WatchOwnStint(reg, place);
                OfferOwnDesk(reg);
            }
            catch (Exception e)
            {
                if (!Guard.StandsDown(e, "own work at a desk"))
                {
                    Logger?.Error(e);
                    Logger?.Warn("Careers: own work at a desk is off for this session after the error above; the Career app starts a contract's work instead.");
                }
                _ownDeskOff = true;
                OwnDeskDown("the desks stood down");
            }
        }

        // The contract worked first at a desk of this business (OwnRules.WorkedBefore): taken, with work left, desk work and no trade's, of a profession done in such an office. Null for none.
        private Contract NextContractHere(BuildingRegistration reg)
        {
            WorkFile w = Work;
            Contract best = null;
            for (int i = 0; w != null && i < w.Contracts.Count; i++)
            {
                Contract c = w.Contracts[i];
                Profession p = c.Def;
                if (c.Stage == (int)ContractStep.Taken && OwnRules.Left(c) > 0 && p != null && !OwnRules.IsTrade(p) && OfficeFits(p, reg) && (best == null || OwnRules.WorkedBefore(c, best)))
                {
                    best = c;
                }
            }
            return best;
        }

        // The first desk of the building's (in the order of its items) that nobody sits at now.
        private EmployeeStationController FreeOwnDesk()
        {
            for (int i = 0; i < _ownDesks.Count; i++)
            {
                if (FirmsGame.Free(_ownDesks[i]))
                {
                    return _ownDesks[i];
                }
            }
            return null;
        }

        // A free desk of a business that is no tech firm is the one the player may click for a contract; nothing to click elsewhere, or while a stint is on.
        private void OfferOwnDesk(BuildingRegistration reg)
        {
            EmployeeStationController free = null;
            _ownDeskNone = false;
            if (reg != null && _ownStint == null && OwnDeskWay && NextContractHere(reg) != null)
            {
                free = FreeOwnDesk();
                if (free == null && (!_ownDeskScanned || Time.realtimeSinceStartup >= _ownDeskLookAt))
                {
                    FirmsGame.Desks(_ownDesks);   // the building's items read once a visit, and again at most every ten seconds while none is free
                    _ownDeskScanned = true;
                    _ownDeskLookAt = Time.realtimeSinceStartup + OwnDeskLookSeconds;
                    free = FreeOwnDesk();
                }
                _ownDeskNone = free == null;
            }
            if (free == null || FirmsGame.KindOf(reg.businessTypeName) >= 0)
            {
                ReleaseOwnClick();   // (a tech firm's own click carries the contract: TechBridges.ContractDesk)
                return;
            }
            if (_onOwnDesk == null)
            {
                _onOwnDesk = OwnDeskClicked;
            }
            if (DeskGame.OtherClicked != null && !ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk))
            {
                return;   // another part's click holds the second place (the bank branch's counter)
            }
            DeskGame.OtherTarget = free;
            DeskGame.OtherClicked = _onOwnDesk;
        }

        // The click taken back, only where it is this part's own.
        private void ReleaseOwnClick()
        {
            if (_onOwnDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk))
            {
                DeskGame.OtherTarget = null;
                DeskGame.OtherClicked = null;
            }
        }

        // A click on the free desk: the player walks to it and the game's work panel offers a stint of the contract worked first here.
        private void OwnDeskClicked(EntityController e)
        {
            ContractAtDesk(e as EmployeeStationController);
        }

        /// <summary>
        /// A contract of the player's worked at this free desk of the business they stand in: true when the click was taken, the walk begun or the refusal said; false when no contract can be
        /// worked here, and the caller offers its own work (a tech firm's, through TechBridges.ContractDesk).
        /// </summary>
        internal bool ContractAtDesk(EmployeeStationController desk)
        {
            BuildingRegistration reg = FirmsGame.InsideOwn();
            if (!_workOn || !OwnDeskWay || _ownStint != null || desk == null || reg == null || Save == null)
            {
                return false;
            }
            Contract c = NextContractHere(reg);
            if (c == null || !FirmsGame.Free(desk))
            {
                return false;
            }
            if (Save.Energy <= 0f)
            {
                Notifications.ShowInsufficientEnergy();
                return true;
            }
            int id = c.Id;
            DeskGame.WalkTo(desk, () =>
            {
                try
                {
                    OpenOwnStint(id, desk);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
            });
            return true;
        }

        // The player is at the desk: the game's work panel is offered with the contract's stint, or why not is said in a notice.
        private void OpenOwnStint(int id, EmployeeStationController desk)
        {
            Contract c = ContractOf(id);
            BuildingRegistration reg = FirmsGame.InsideOwn();
            if (c == null || reg == null || _ownStint != null || !FirmsGame.Free(desk) || c.Stage != (int)ContractStep.Taken || OwnRules.Left(c) <= 0)
            {
                return;
            }
            string why = OfferStint(c, desk, TalentGame.Place(reg));
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f);
            }
        }

        // The game's work panel offered with a contract's stint, at a desk the player has walked to or where they stand with none. Null if offered, else the reason key.
        private string OfferStint(Contract c, EmployeeStationController desk, string place)
        {
            int left = OwnRules.Left(c);
            if (desk == null && !Settings.CareersOwnHours)
            {
                return Offer(ContractSitting(c, OwnRules.FixedMinutes(left)));   // the stint as it always was: three hours where the player stands
            }
            if (_broken || !isActiveAndEnabled)
            {
                return "reallife_study_test_off";   // switched off: ShiftSkip would call a stint off at Start, as it needs its host enabled
            }
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            Sitting sitting = ContractSitting(c, left);
            var stint = new ContractStint(this, sitting, left, desk, place, _ownHours, Settings.CareersOwnHours);
            CloseApps();
            ShiftSkip skip = ShiftSkip.Offer(this, stint);
            if (skip == null)
            {
                return "reallife_study_why_game_busy";
            }
            stint.Skip = skip;
            _skip = skip;
            Hold(sitting, skip);
            _ownStint = stint;
            return null;
        }

        /// <summary>Start on the game's work panel: the player sits at the desk (a seat the game would not give ends the stint at once) and the contract's sitting begins.</summary>
        internal void OwnStintStarted(ContractStint stint)
        {
            if (!ReferenceEquals(stint, _ownStint) || stint.Over)
            {
                stint.Skip?.Close();
                FinishOwnStint(stint, 0, false);
                return;
            }
            if (stint.Desk != null)
            {
                BuildingRegistration reg = FirmsGame.InsideOwn();
                if (reg == null || TalentGame.Place(reg) != stint.Place || !FirmsGame.Free(stint.Desk))
                {
                    stint.Skip?.Close();
                    FinishOwnStint(stint, 0, false);
                    return;
                }
                ReleaseOwnClick();
                stint.Begun = true;   // first: from here every end of the stint stands the player up (FinishOwnStint), whatever fails below
                if (!DeskGame.SitAt(stint.Desk))
                {
                    stint.Skip?.Close();   // the game would not seat them: given back free to walk and told why; the panel closes with nothing worked
                    FinishOwnStint(stint, 0, false);
                    return;
                }
            }
            stint.Started = true;
            stint.Sitting.SkipStarted();
            Logger?.Info("Careers: the player works contract " + stint.Sitting.Of + (stint.Desk != null ? " at a desk of their business" : " where they stand") + " from hour "
                         + (Save != null ? Save.Hour : 0) + " of day " + (Save != null ? Save.Day : 0) + " for " + stint.SkipMinutes + " minute(s).");
        }

        /// <summary>The panel's end of the stint, by the time up, Stop, or the skip and fast forward: settled once.</summary>
        internal void OwnStintEnded(ContractStint stint, int minutes, bool whole)
        {
            FinishOwnStint(stint, minutes, whole);
        }

        // While a stint is on: over if the panel let it go before it began or ended it unseen (what was worked counts), or, at a desk, if the player has left the business or the game
        // has seated its own employee there.
        private void WatchOwnStint(BuildingRegistration reg, string place)
        {
            ContractStint stint = _ownStint;
            if (stint == null)
            {
                return;
            }
            if (stint.Skip == null || !stint.Skip.Open)
            {
                FinishOwnStint(stint, stint.Worked, false);
                return;
            }
            if (stint.Desk != null && (reg == null || place != stint.Place || (stint.Begun && !FirmsGame.PlayerAt(stint.Desk))))
            {
                int minutes = stint.Worked;
                stint.Skip.Close();
                FinishOwnStint(stint, minutes, false);
            }
        }

        // The stint over, whichever way, once: the player stands up, the desk's own employee comes back, and the sitting is settled through its record (a stint that never began counts nothing).
        private void FinishOwnStint(ContractStint stint, int minutes, bool whole)
        {
            if (stint.Over)
            {
                return;
            }
            stint.Over = true;
            if (ReferenceEquals(_ownStint, stint))
            {
                _ownStint = null;
            }
            EmployeeStationController desk = stint.Desk;
            if (stint.Begun && desk != null)
            {
                DeskGame.StandFrom(desk);   // the desk let go only while the player is at it; the pose and the work hold always
                if (isActiveAndEnabled)
                {
                    StartCoroutine(Guard.Routine(OwnRestaffSoon(desk)));
                }
            }
            if (stint.Started)
            {
                stint.Sitting.SkipEnded(minutes, whole);
            }
            else
            {
                stint.Sitting.CalledOff();
            }
            if (Verbose)
            {
                Logger?.Info("Careers check (own desk): the stint on contract " + stint.Sitting.Of + " is over after " + minutes + " of " + stint.SkipMinutes + " minute(s), "
                             + (stint.Started ? "settled once through its sitting" : "never begun, nothing counted") + ": PASS");
            }
        }

        // A frame after the player stands up, as the game's WorkActivity waits before it seats the desk's own employee again.
        private IEnumerator OwnRestaffSoon(EmployeeStationController desk)
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

        // The stint at a desk called off and the click taken back: the working life stopping, or own work at a desk standing down after a fault.
        private void OwnDeskDown(string why)
        {
            ContractStint stint = _ownStint;
            bool held = stint != null || (_onOwnDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk));
            if (stint != null)
            {
                stint.Skip?.Close();
                FinishOwnStint(stint, stint.Worked, false);
            }
            ReleaseOwnClick();
            _ownDeskInside = null;
            _ownDeskScanned = false;
            _ownDeskNone = false;
            _ownDesks.Clear();
            if (held && Verbose)
            {
                Logger?.Info("Careers: the desk for a contract let go (" + why + ").");
            }
        }
    }
}
