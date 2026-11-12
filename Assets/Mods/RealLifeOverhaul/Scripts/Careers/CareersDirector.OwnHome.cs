using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A freelance contract worked at the computer at home, as a job's day from home is: its own button on the computer's panel beside Pass the time (Careers/ComputerButtons) for the
    /// contract due first (OwnRules.WorkedBefore), then the game's work panel with the hours slider and Start (ComputerSitting), the walk to the chair and the minutes settled once
    /// by the sitting's own record (ContractEnded). The Career app starts it itself only where the home has no computer with a chair to click, or the buttons are not in the game.
    /// Cost: a look through six contracts at most when the computer's panel opens; the home's items are looked through at most once a second, for a page of the Career app.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private ComputerController _homeComputer;   // the computer at home with its chair, looked for at most once a second
        private float _homeComputerAt = -10f;       // real seconds, when it was last looked for

        /// <summary>True while a contract is started at the computer at home: <see cref="OwnWay"/> and the computer panel's buttons in the game.</summary>
        internal bool OwnComputerWay => OwnWay && ComputerButtons.Installed && !ComputerButtons.Failed;

        // The computer at home with its chair that the contract's button is for: the home's items looked through at most once a second, as a page of the Career app asks where a contract starts.
        private bool HomeComputerHere()
        {
            if (Time.realtimeSinceStartup - _homeComputerAt >= 1f)
            {
                _homeComputerAt = Time.realtimeSinceStartup;
                _homeComputer = CareersGame.AtHome() ? DeskGame.HomeComputer() : null;
            }
            return _homeComputer != null;
        }

        /// <summary>The words of the home computer's button for the contract worked first there ("Work for {client}"), or null: no contract to work at a computer, or the way off.</summary>
        internal string ComputerContract()
        {
            Contract c = OwnComputerWay ? FirstContract(null) : null;
            return c != null ? CareersText.T("reallife_own_skip_title", "client", c.Client) : null;
        }

        /// <summary>
        /// The contract worked first at the computer at home, from its own button: the game's work panel is offered with the hours slider, and the walk and the seat follow on Start
        /// (ComputerSitting), as for a class. Null if offered, else the reason key.
        /// </summary>
        internal string ContractAtComputer(ComputerController computer)
        {
            Contract c = OwnComputerWay ? FirstContract(null) : null;
            if (c == null)
            {
                return "reallife_own_why_no_contract";
            }
            if (!CareersGame.AtHome() || computer == null || !DeskGame.Usable(computer) || computer.FindChair() == null)
            {
                return "reallife_own_why_desk";
            }
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            if (PlayerHelper.ItemInstanceInHands != null && !PlayerHelper.IsHoldingBag)
            {
                return "notification_need_empty_hands_to_interact";   // the game's own words, as its computer says them
            }
            int left = OwnRules.Left(c);
            var slider = new StintSlider(_ownHours, left, Settings.CareersOwnHours, OwnHoursChosen);
            string why = StartAtComputer(computer, ContractSitting(c, left), c.Client, slider);
            if (why == null && Verbose)
            {
                Logger?.Info("Careers check (own computer): the contract of " + c.Client + " is offered at the computer at home, " + left + " minute(s) left, the slider from "
                             + slider.Least + " to " + slider.Most + " hour(s) at " + slider.Now + ", the last pick kept at " + _ownHours + ": PASS");
            }
            return why;
        }
    }
}
