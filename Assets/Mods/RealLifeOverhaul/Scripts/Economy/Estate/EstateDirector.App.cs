using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The director's side of the app: notices that open it, its badge, its page when something changes, and the Planner's rows.</summary>
    public sealed partial class EstateDirector
    {
        /// <summary>A notification of the game's that opens the Property app on a tab, with something picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 11f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        internal static Dictionary<string, string> Data(string name, string value, string name2 = null, string value2 = null, string name3 = null, string value3 = null)
        {
            var data = new Dictionary<string, string>(3) { { name, value ?? string.Empty } };
            if (name2 != null)
            {
                data[name2] = value2 ?? string.Empty;
            }
            if (name3 != null)
            {
                data[name3] = value3 ?? string.Empty;
            }
            return data;
        }

        public void Open(int tab, string pick)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>The app has been opened: what was done in the game's own apps since the morning is read.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                if (On(EstatePart.Property))
                {
                    int added, dropped;
                    EstateGame.ReadHoldings(_state, out added, out dropped);
                    EstateGame.ReadAgency(_state.Agency);
                    if (added > 0 || dropped > 0)
                    {
                        EstateGame.WriteValues(_state, _climate.Index);
                        WriteRents(false);
                        BuildAlerts(Save.Day);
                    }
                }
                ReadHotels(Save.Day);
                _state.Hire.Spaces = _state.Hire.Running ? EstateGame.Spaces(_state.Hire.Lot) : 0;
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>Something has changed: the Planner's rows and the badge are made again, and the app's page if it is open.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            BuildAlerts(Save.Day);
            Badge();
            _view?.Changed();
            EstateBridges.Changed?.Invoke();
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : Waiting());
            }
        }

        /// <summary>How many things wait for an answer from the player.</summary>
        public int Waiting()
        {
            if (!_running || _state == null || Save == null)
            {
                return 0;
            }
            int day = Save.Day, waiting = 0;
            if (On(EstatePart.Property))
            {
                for (int i = 0; i < _state.Holdings.Count; i++)
                {
                    EstateHolding h = _state.Holdings[i];
                    waiting += h.ApplicantCount + (h.Offer > 0 ? 1 : 0) + EstateRules.UnitsToAnswer(h, day);
                    waiting += Duties && h.Managed && h.Manager != (int)EstateManager.Manager ? EstateRules.CallsWaiting(h) : 0;
                }
                for (int i = 0; i < _state.Agency.Open.Count; i++)
                {
                    waiting += _state.Agency.Open[i].Stage == (int)InstructionStage.Offered ? 1 : 0;
                }
            }
            for (int i = 0; On(EstatePart.Hotel) && i < _state.Hotels.Count; i++)
            {
                waiting += _state.Hotels[i].AskRooms > 0 ? 1 : 0;
            }
            return waiting;
        }

        partial void PropertyAlerts(int day);
        partial void HotelAlerts(int day);
        partial void HireAlerts(int day);

        private void BuildAlerts(int day)
        {
            _alerts.Clear();
            try
            {
                if (On(EstatePart.Property))
                {
                    PropertyAlerts(day);
                }
                if (On(EstatePart.Hotel))
                {
                    HotelAlerts(day);
                }
                if (On(EstatePart.Hire))
                {
                    HireAlerts(day);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // a row that cannot be made is left out: the rules are not touched
            }
        }

        internal void Alert(int a, int b, int day, string title, string what, string money, string ifMissed)
        {
            if (_alerts.Count < EstateCatalogue.AlertsMost)
            {
                _alerts.Add(new EstateAlert { A = a, B = b, Day = day, Title = title, What = what, Money = money, IfMissed = ifMissed });
            }
        }
    }
}
