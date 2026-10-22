using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The utilities' part that lets a place go: read each hour, a place no longer held closes with its last bill, and an
    /// underground car park's account is written off. Cost: one walk of the save's registrations an hour.
    /// </summary>
    public sealed partial class UtilitiesDirector
    {
        private readonly List<PlaceInfo> _before = new List<PlaceInfo>(16);   // the places as last read, for the name of one given up
        private readonly List<BuildingRegistration> _unserved = new List<BuildingRegistration>(2);   // the player's car parks, never supplied
        private readonly List<string> _gone = new List<string>(2);   // the places let go in this reading, said once each

        /// <summary>The places read again, and the accounts of any no longer the player's closed with their last bill.</summary>
        private void Refresh()
        {
            ReadPlaces();
            if (CloseGivenUp(UtilitiesGame.Today))
            {
                _view?.Changed();
            }
        }

        // the player's places now, the last ones kept beside them for a moment for the name of a place let go
        private void ReadPlaces()
        {
            _before.Clear();
            _before.AddRange(_places);
            UtilitiesGame.Places(_places, _unserved);
            for (int i = 0; i < _unserved.Count; i++)
            {
                UtilitiesBridges.Release?.Invoke(_unserved[i]);   // a car park the utilities closed before they knew better opens again
            }
        }

        // every account to its fate (UtilitiesRules.Fate); a place given up leaves the grace; true if any was written off or closed
        private bool CloseGivenUp(int today)
        {
            _gone.Clear();
            bool changed = false;
            for (int i = _state.Accounts.Count - 1; i >= 0; i--)
            {
                Account a = _state.Accounts[i];
                string place = a != null ? a.Place : null;
                bool held = Holds(place);
                switch (UtilitiesRules.Fate(a, held, Unserved(place)))
                {
                    case AccountFate.WriteOff:
                        WriteOff(a);
                        _state.Accounts.RemoveAt(i);
                        changed = true;
                        continue;
                    case AccountFate.LastBill:
                        Close(a, today);
                        GivenUp(place);
                        changed = true;
                        break;
                }
                if (UtilitiesRules.Fate(a, held, false) == AccountFate.TakeOut)
                {
                    _state.Accounts.RemoveAt(i);
                }
            }
            for (int i = _state.Grace.Count - 1; i >= 0; i--)
            {
                if (!Holds(_state.Grace[i]))
                {
                    _state.Grace.RemoveAt(i);   // taken again, it is a business started now
                }
            }
            if (_state.Grace.Count == 0)
            {
                _state.GraceUntil = 0;   // none left to cover: nothing to end or tell
            }
            return changed;
        }

        private bool Unserved(string place)
        {
            for (int i = 0; i < _unserved.Count; i++)
            {
                if (string.Equals(UtilitiesGame.Place(_unserved[i]), place, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // an account of a car park: nothing of it was ever owed, and a deposit on it comes back
        private void WriteOff(Account a)
        {
            int owed = a.Owed + UtilitiesRules.Money(a.Accrued);
            int back = UtilitiesRules.WriteOff(a);
            if (back > 0)
            {
                UtilitiesGame.Give(back, "reallife_transaction_util_deposit_back", null);
            }
            _toldClosed.Remove(a.Place);
            Logger?.Info("Utilities: " + a.Service + " at " + a.Place + " written off: an underground car park has no premises to supply; "
                         + owed + " owed or used is not asked for" + (back > 0 ? ", " + back + " of the deposit back." : "."));
        }

        // a place no longer held: told afresh if it is ever theirs again, one line with Developer diagnostics on
        private void GivenUp(string place)
        {
            _toldClosed.Remove(place);
            if (!Meta.Dev.Verbose || _gone.Contains(place))
            {
                return;
            }
            _gone.Add(place);
            string name = place;
            for (int i = 0; i < _before.Count; i++)
            {
                if (string.Equals(_before[i].Place, place, StringComparison.Ordinal))
                {
                    name = _before[i].Name;
                    break;
                }
            }
            Logger?.Info("Utilities check (a place let go): " + name + " (" + place + ") is no longer the player's: its accounts closed with their last bill on day "
                         + UtilitiesGame.Today + ", and it is not closed or billed again: PASS");
        }
    }
}
