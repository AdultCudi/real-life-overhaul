using System;
using System.Globalization;
using BAModAPI;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A sale agreed at a desk inside the firm. A business cannot change hands with its owner in it, so the desk session records the player's word (ExitRules.Agree: the
    /// offer, its price and last day, kept in the save) and the sale completes through ExitAccept, the way an accepted offer always does, once the player has stepped
    /// outside: on the game's exit building event, on the first look after a load, or on the day's pass. The same refusals are named if something else is in the way then, and
    /// the agreement lapses with a notice if the offer's last day passes first. The player can call it off on the Exits page. Nothing per frame: a flag is set by an event and
    /// read by the one-second timer, and while no sale is agreed that is one comparison.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const string BusyRefusal = "reallife_svc_refuse_busy", InsideRefusal = "reallife_svc_refuse_inside";

        private Action<Address> _onLeft, _leftSeen;   // the game's event for the player leaving a building
        private bool _agreeTry;                       // a sale is agreed and the player may now be outside: look at it on the next second
        private string _agreeTold = string.Empty;     // the last refusal told for an agreed sale (its place and the reason), so a refusal is said once and not at each exit

        /// <summary>True if a sale of any firm is agreed and waiting for the player to step outside.</summary>
        internal bool AnyAgreed()
        {
            for (int i = 0; _exits != null && i < _exits.Firms.Count; i++)
            {
                if (_exits.Firms[i].Agreed != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnLeft(Address address)
        {
            if (_exits != null && AnyAgreed())
            {
                _agreeTry = true;   // the game has just left a building: the one-second timer looks once the fade is done
            }
        }

        /// <summary>
        /// Each second while exits run: the handler for the game's exit building event is on it (the game empties the event at each load), and a sale that
        /// may now complete is looked at once. A refusal for the game being busy asks again the next second; any other waits for the next exit, load or day.
        /// </summary>
        private void KeepAgreed()
        {
            if (_onLeft == null)
            {
                _onLeft = Guard.Wrap<Address>(OnLeft);
            }
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _onLeft, ref _leftSeen);
            if (!_agreeTry || _exits == null || Save == null)
            {
                return;
            }
            _agreeTry = false;
            int day = Save.Day;
            for (int i = _exits.Firms.Count - 1; i >= 0; i--)
            {
                FirmExit e = _exits.Firms[i];
                if (e.Agreed != null && FinishAgreed(e, day))
                {
                    _agreeTry = true;
                }
            }
        }

        private void DropAgreedHandler()
        {
            if (_onLeft != null)
            {
                GlobalEvents.onExitBuilding -= _onLeft;
            }
            _leftSeen = null;
            _agreeTry = false;
            _agreeTold = string.Empty;
        }

        /// <summary>
        /// The day's look at every agreed sale: one past its last day lapses with a notice, one whose offer went another way is forgotten, and one still standing is looked at
        /// for completing as soon as the player is outside.
        /// </summary>
        private void AgreedDay(int day)
        {
            for (int i = _exits.Firms.Count - 1; i >= 0; i--)
            {
                FirmExit e = _exits.Firms[i];
                if (e.Agreed == null)
                {
                    continue;
                }
                if (FirmOf(e.Place) == null)
                {
                    e.Agreed = null;   // the firm is no longer the player's
                    ChangedTech();
                    continue;
                }
                switch (ExitRules.Check(e, day))
                {
                    case ExitRules.AgreedState.Lapsed:
                        LapseAgreed(e, day);
                        break;
                    case ExitRules.AgreedState.Gone:
                        e.Agreed = null;
                        ChangedTech();
                        break;
                    default:
                        _agreeTry = true;
                        break;
                }
            }
        }

        private void LapseAgreed(FirmExit e, int day)
        {
            FirmPlace firm = FirmOf(e.Place);
            ExitAgreed a = e.Agreed;
            e.Agreed = null;
            ExitOffer o = e.OfferOf(a.Offer);
            if (o != null)
            {
                e.Offers.Remove(o);
            }
            string name = firm != null ? firm.File.Name : e.Place;
            TellTech(NotificationType.Warning, "reallife_exit_note_agreed_lapsed", Data("firm", name, "buyer", a.Name, "day", a.Lapse.ToString(CultureInfo.InvariantCulture)),
                TabFunding, ExitRow + e.Place);
            Logger?.Info("Tech: the sale of " + name + " to " + a.Name + " agreed on day " + a.Day + " lapsed on day " + day
                         + ": the player did not step outside by day " + a.Lapse + ".");
            ChangedTech();
        }

        /// <summary>One agreed sale, completed if nothing is in the way. True if it should be looked at again in a second (the game is busy).</summary>
        private bool FinishAgreed(FirmExit e, int day)
        {
            switch (ExitRules.Check(e, day))
            {
                case ExitRules.AgreedState.None:
                    return false;
                case ExitRules.AgreedState.Lapsed:
                    LapseAgreed(e, day);
                    return false;
                case ExitRules.AgreedState.Gone:
                    e.Agreed = null;
                    ChangedTech();
                    return false;
            }
            FirmPlace firm = FirmOf(e.Place);
            if (firm == null || firm.Reg == null)
            {
                e.Agreed = null;
                ChangedTech();
                return false;
            }
            string why = ExitBridges.WhyNotHand(e.Place);
            if (why == BusyRefusal)
            {
                return true;
            }
            if (why == InsideRefusal)
            {
                return false;   // in the firm again: the next exit looks again
            }
            if (why != null)
            {
                string told = e.Place + "|" + why;
                if (told != _agreeTold)
                {
                    _agreeTold = told;
                    TellTech(NotificationType.Warning, "reallife_exit_note_agreed_blocked", Data("firm", firm.File.Name, "buyer", e.Agreed.Name, "why", TechText.T(why)),
                        TabFunding, ExitRow + e.Place);
                }
                return false;
            }
            int offer = e.Agreed.Offer;
            string buyer = e.Agreed.Name;
            if (ExitAccept(e.Place, offer, e.Agreed.Terms))
            {
                Logger?.Info("Tech: the sale of " + firm.File.Name + " to " + buyer + " agreed inside the firm completed on day " + day + " once the player was outside.");
                return false;
            }
            if (e.OfferOf(offer) == null)
            {
                e.Agreed = null;   // the buyer could no longer pay: ExitAccept said so and took the offer back
                ChangedTech();
            }
            return false;
        }

        /// <summary>
        /// The player's word to an offer on the terms they chose, given at a desk inside the firm; the sale completes on those terms when they step outside. False if one is already
        /// agreed, the offer has lapsed, or the terms cannot be made now (the reason is told, as ExitAccept would). With the terms off, or for a team bought for its people, it is cash.
        /// </summary>
        internal bool ExitAgree(string place, ExitOffer o, TermChoice terms = default(TermChoice))
        {
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            if (o == null || o.Hire || _terms == null)
            {
                terms = default(TermChoice);
            }
            if (Save == null || e == null || e.Agreed != null || ContractRefused(place, null))
            {
                return false;
            }
            if (terms.Kind != TermKind.Cash)
            {
                TermPlan plan = TermPlanFor(place, o, terms, out _);
                if (!plan.Ok)
                {
                    Notifications.ShowError(plan.Why);
                    return false;
                }
            }
            if (!ExitRules.Agree(e, o, Save.Day, terms))
            {
                return false;
            }
            _agreeTold = string.Empty;
            Logger?.Info("Tech: the player agreed to sell " + place + " to " + o.Name + " for " + TechText.Cents(Level(o.Price)) + " on terms " + terms.Kind + " on day " + Save.Day
                         + ", to complete when they step outside (the offer stands to day " + o.Lapse + ").");
            ChangedTech();
            return true;
        }

        /// <summary>An agreed sale called off from the Exits page: the offer stands as before, to its last day.</summary>
        internal bool ExitCallOff(string place)
        {
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            FirmPlace firm = FirmOf(place);
            ExitAgreed a = e != null ? e.Agreed : null;
            if (a == null || !ExitRules.CallOff(e))
            {
                return false;
            }
            Notifications.Show(NotificationType.Info, "reallife_exit_note_agreed_off",
                Data("firm", firm != null ? firm.File.Name : place, "buyer", a.Name, "day", a.Lapse.ToString(CultureInfo.InvariantCulture)), 8f, null, null, true, false);
            Logger?.Info("Tech: the player called off the agreed sale of " + place + " to " + a.Name + ".");
            ChangedTech();
            return true;
        }
    }
}
