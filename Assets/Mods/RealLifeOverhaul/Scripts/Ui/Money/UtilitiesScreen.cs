using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Utilities app on the kit: bills, the internet's plans and the broadband company, each with the buttons the old
    /// app had (pay, apply, buy, cancel, found, dig, extend, set the plans, sell) and the question before a large sum. The
    /// facts are read from the save when its generation changes, and the model written from them and the player's choices
    /// when something changed. Nothing a frame; nothing allocated while idle.
    /// </summary>
    public sealed class UtilitiesScreen : IUiScreen
    {
        public const string ScreenId = "utilities";

        private readonly IUtilitiesSource _source;
        private readonly UtilitiesWords _words;
        private readonly UtilitiesFacts _facts = new UtilitiesFacts();
        private readonly UtilitiesView _view = new UtilitiesView();
        private UtilitiesFaceModel _m;
        private string _accountKey, _placeKey;
        private float _accountsOffset, _placesOffset, _plansOffset;
        private int _accountsReveal = -1, _placesReveal = -1, _plansReveal = -1;
        private int _seen = -1;
        private bool _dirty = true;
        private bool _fromList;   // the question about a district came from the list of places to dig in

        public UtilitiesScreen(IUtilitiesSource source, UtilitiesWords words)
        {
            _source = source;
            _words = words;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            int wanted = _source is IUiTabWant asked ? asked.TakeWanted() : -1;
            if (wanted >= 0)
            {
                ShowTab(wanted);
            }
            if (_source != null && (_seen != _source.Generation || _m == null))
            {
                _seen = _source.Generation;
                _source.Read(_facts);
                Follow();
            }
            _m = UtilitiesBuild.Make(_facts, _view, _words);
            if (_m.Accounts != null)
            {
                _m.Accounts.Offset = _accountsOffset;
                _m.Accounts.Reveal = _accountsReveal;
            }
            if (_m.Places != null)
            {
                _m.Places.Offset = _placesOffset;
                _m.Places.Reveal = _placesReveal;
            }
            if (_m.Plans != null)
            {
                _m.Plans.Offset = _plansOffset;
                _m.Plans.Reveal = _plansReveal;
            }
            _accountsReveal = _placesReveal = _plansReveal = -1;
            UtilitiesFace.Lay(s, _m, frame, dark, keys);
            _accountsOffset = _m.Accounts != null ? _m.Accounts.Offset : 0f;
            _placesOffset = _m.Places != null ? _m.Places.Offset : 0f;
            _plansOffset = _m.Plans != null ? _m.Plans.Offset : 0f;
            _dirty = false;
        }

        // The chosen account and place stay the same ones as the lists change under them.
        private void Follow()
        {
            _view.Account = IndexOf(_facts.Accounts, _accountKey, _view.Account);
            _accountKey = _view.Account >= 0 && _view.Account < _facts.Accounts.Length ? _facts.Accounts[_view.Account].Key : null;
            int place = _view.Place;
            _view.Place = IndexOfPlace(_facts.Places, _placeKey, _view.Place);
            if (_view.Place != place)
            {
                _view.Option = -1;
            }
            _placeKey = _view.Place >= 0 && _view.Place < _facts.Places.Length ? _facts.Places[_view.Place].Key : null;
        }

        private static int IndexOf(UtilityAccountFact[] list, string key, int fallback)
        {
            for (int i = 0; key != null && i < list.Length; i++)
            {
                if (string.Equals(list[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return Math.Max(0, Math.Min(Math.Max(0, list.Length - 1), fallback));
        }

        private static int IndexOfPlace(UtilityPlaceFact[] list, string key, int fallback)
        {
            for (int i = 0; key != null && i < list.Length; i++)
            {
                if (string.Equals(list[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return Math.Max(0, Math.Min(Math.Max(0, list.Length - 1), fallback));
        }

        public bool Press(int command, int arg)
        {
            bool done = _view.Sheet != UtilitiesAsk.None ? PressSheet(command, arg) : PressPage(command, arg);
            _dirty |= done;
            return done;
        }

        private bool PressPage(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                return ShowTab(arg);
            }
            if (command == UtilitiesFace.AccountsBase + UiDataTable.Row)
            {
                return SelectAccount(arg);
            }
            if (command == UtilitiesFace.PlacesBase + UiDataTable.Row)
            {
                return SelectPlace(arg);
            }
            if (command == UtilitiesFace.PlansBase + UiDataTable.Row)
            {
                return arg >= 0 && Place() != null && arg < Place().Options.Length && Choose(arg);
            }
            switch (command)
            {
                case UtilitiesFace.CardAction: return Account(arg);
                case UtilitiesFace.CardSwitch: return Debit();
                case UtilitiesFace.BuySwitch: _view.Contract = !_view.Contract; return true;
                case UtilitiesFace.BuyAction: return Connection(arg);
                case UtilitiesFace.HeroAction: return Hero(arg);
                case UtilitiesFace.DistrictAct: _fromList = false; return District(arg);
                default: return false;
            }
        }

        private UtilityAccountFact Chosen() => _view.Account >= 0 && _view.Account < _facts.Accounts.Length ? _facts.Accounts[_view.Account] : null;

        private UtilityPlaceFact Place() => _view.Place >= 0 && _view.Place < _facts.Places.Length ? _facts.Places[_view.Place] : null;

        private bool ShowTab(int tab)
        {
            if (tab < 0 || tab > 2)
            {
                return false;
            }
            _view.Tab = tab;
            _view.Sheet = UtilitiesAsk.None;
            return true;
        }

        private bool SelectAccount(int row)
        {
            if (row < 0 || row >= _facts.Accounts.Length)
            {
                return false;
            }
            _view.Account = row;
            _accountKey = _facts.Accounts[row].Key;
            return true;
        }

        private bool SelectPlace(int row)
        {
            if (row < 0 || row >= _facts.Places.Length)
            {
                return false;
            }
            if (_view.Place != row)
            {
                _view.Option = -1;
            }
            _view.Place = row;
            _placeKey = _facts.Places[row].Key;
            return true;
        }

        private bool Choose(int option)
        {
            _view.Option = option;
            return true;
        }

        // Pay what is owed, or apply for the account (by phone, while there is no office to visit).
        private bool Account(int which)
        {
            UtilityAccountFact a = Chosen();
            if (a == null)
            {
                return false;
            }
            return _source != null && _source.Do(which == 0 ? UtilitiesAct.Pay : UtilitiesAct.Apply, _view.Account, 0, 0, 0);
        }

        private bool Debit()
        {
            UtilityAccountFact a = Chosen();
            return a != null && a.CanDebit && _source != null && _source.Do(UtilitiesAct.Debit, _view.Account, a.AutoDebit ? 0 : 1, 0, 0);
        }

        // Buy or change the plan chosen at the place, cancel the connection, or have it reconnected.
        private bool Connection(int which)
        {
            UtilityPlaceFact p = Place();
            if (p == null || _source == null)
            {
                return false;
            }
            if (which == 1)
            {
                return _source.Do(UtilitiesAct.Cancel, _view.Place, 0, 0, 0);
            }
            if (which == 2)
            {
                return _source.Do(UtilitiesAct.Reconnect, _view.Place, 0, 0, 0);
            }
            int option = UtilitiesBuild.ChosenOption(p, _view);
            if (option < 0)
            {
                return false;
            }
            UtilityOptionFact o = p.Options[option];
            bool done = _source.Do(UtilitiesAct.Buy, _view.Place, o.Provider, o.Plan, _view.Contract && !o.Own ? 1 : 0);
            if (done)
            {
                _view.Option = -1;
            }
            return done;
        }

        // The hero's buttons: the districts to dig in, the settings, the licence.
        private bool Hero(int which)
        {
            UtilityNetworkFact n = _facts.Network;
            if (which == 0 && n.State == 1)
            {
                _view.Sheet = UtilitiesAsk.DigList;
                return true;
            }
            if (which == 1 && n.State == 1)
            {
                _view.Sheet = UtilitiesAsk.Settings;
                return true;
            }
            if (which == 2 && n.State == 0 && n.HasHeadquarters)
            {
                _view.Sheet = UtilitiesAsk.Found;
                return true;
            }
            return false;
        }

        // A district's button: dig where nothing is dug, or extend a network to the whole district. Each asks first.
        private bool District(int district)
        {
            UtilityNetworkFact n = _facts.Network;
            if (n.State != 1 || district < 0 || district >= n.Districts.Length)
            {
                return false;
            }
            UtilityDistrictFact d = n.Districts[district];
            _view.District = district;
            if (d.State == 0)
            {
                _view.Sheet = UtilitiesAsk.Build;
                return true;
            }
            if (d.State == 2 && d.ExtendCost > 0)
            {
                _view.Sheet = UtilitiesAsk.Extend;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- the sheet

        private bool PressSheet(int command, int arg)
        {
            switch (command)
            {
                case UtilitiesFace.SheetClose:
                    _view.Sheet = UtilitiesAsk.None;
                    return true;
                case UtilitiesFace.SheetAnswer:
                    return arg == 1 ? Confirm() : CloseQuestion();
                case UtilitiesFace.SheetPick:
                    _fromList = true;
                    return District(arg);
                case UtilitiesFace.SheetAction:
                    _view.Sheet = UtilitiesAsk.Sell;
                    return true;
                case UtilitiesFace.SheetLess:
                case UtilitiesFace.SheetMore:
                    return Setting(arg, command == UtilitiesFace.SheetMore ? 1 : -1);
                default:
                    return false;
            }
        }

        // A question answered no goes back to where it came from: the settings of the sale, the places to dig in, or the page.
        private bool CloseQuestion()
        {
            _view.Sheet = _view.Sheet == UtilitiesAsk.Sell ? UtilitiesAsk.Settings : _view.Sheet == UtilitiesAsk.Build && _fromList ? UtilitiesAsk.DigList : UtilitiesAsk.None;
            return true;
        }

        private bool Confirm()
        {
            if (_source == null)
            {
                return false;
            }
            UtilitiesAsk ask = _view.Sheet;
            bool done = ask == UtilitiesAsk.Found ? _source.Do(UtilitiesAct.Found, 0, 0, 0, 0)
                : ask == UtilitiesAsk.Build ? _source.Do(UtilitiesAct.Dig, _view.District, 0, 0, 0)
                : ask == UtilitiesAsk.Extend ? _source.Do(UtilitiesAct.Extend, _view.District, 0, 0, 0)
                : ask == UtilitiesAsk.Sell && _source.Do(UtilitiesAct.Sell, 0, 0, 0, 0);
            _view.Sheet = UtilitiesAsk.None;
            return done || ask != UtilitiesAsk.None;
        }

        // A setting a step up or down: the prices, the upkeep, the marketing budget.
        private bool Setting(int which, int step)
        {
            UtilityNetworkFact n = _facts.Network;
            if (_source == null || n.State != 1)
            {
                return false;
            }
            if (which == 0)
            {
                int next = Math.Max(n.PriceLeast, Math.Min(n.PriceMost, n.PriceShare + step * Math.Max(1, n.PriceStep)));
                return next != n.PriceShare && _source.Do(UtilitiesAct.Prices, next, 0, 0, 0);
            }
            if (which == 1)
            {
                int next = Math.Max(n.MaintenanceLeast, Math.Min(n.MaintenanceMost, n.Maintenance + step * Math.Max(1, n.MaintenanceStep)));
                return next != n.Maintenance && _source.Do(UtilitiesAct.Maintenance, next, 0, 0, 0);
            }
            int budget = Math.Max(0, Math.Min(n.MarketingBudgets.Length - 1, n.Marketing + step));
            return budget != n.Marketing && _source.Do(UtilitiesAct.Marketing, budget, 0, 0, 0);
        }

        // ------------------------------------------------------------------------------------------------- the host's calls

        public void Focused(int command, int arg)
        {
            if (_view.Sheet != UtilitiesAsk.None)
            {
                return;
            }
            bool changed = false;
            if (command == UtilitiesFace.AccountsBase + UiDataTable.Row && arg != _view.Account)
            {
                changed = SelectAccount(arg);
            }
            else if (command == UtilitiesFace.PlacesBase + UiDataTable.Row && arg != _view.Place)
            {
                changed = SelectPlace(arg);
            }
            else if (command == UtilitiesFace.PlansBase + UiDataTable.Row && Place() != null && arg < Place().Options.Length && arg != UtilitiesBuild.ChosenOption(Place(), _view))
            {
                changed = Choose(arg);
            }
            _dirty |= changed;
        }

        public bool Back()
        {
            if (_view.Sheet == UtilitiesAsk.None)
            {
                return false;
            }
            CloseQuestion();
            _dirty = true;
            return true;
        }

        public void Tab(int step)
        {
            if (_view.Sheet != UtilitiesAsk.None)
            {
                return;
            }
            _view.Tab = (_view.Tab + step + 3) % 3;
            _dirty = true;
        }

        // Alt runs the page's main button, Option its second: the old app's own keys.
        public bool Act(UiAction action)
        {
            if (_view.Sheet != UtilitiesAsk.None || (action != UiAction.Alt && action != UiAction.Option))
            {
                return false;
            }
            bool done;
            if (_view.Tab == 0)
            {
                UtilityAccountFact a = Chosen();
                done = action == UiAction.Alt && a != null && (a.Owed > 0 ? Account(0) : a.Offer != UtilityOffer.None && _facts.ByPhone && Account(1));
            }
            else if (_view.Tab == 1)
            {
                done = action == UiAction.Alt && Connection(0);
            }
            else
            {
                int state = _facts.Network.State;
                done = action == UiAction.Option ? Hero(1) : state == 0 ? Hero(2) : Hero(0);
            }
            _dirty |= done;
            return done;
        }

        public void Reveal(int list, int index)
        {
            if (_m == null)
            {
                return;
            }
            if (list == _m.AccountsList)
            {
                _accountsReveal = index;
            }
            else if (list == _m.PlacesList)
            {
                _placesReveal = index;
            }
            else if (list == _m.PlansList)
            {
                _plansReveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_m == null)
            {
                return;
            }
            float by = -notches * UiTokens.S16;
            if (clip == _m.AccountsClip)
            {
                _accountsOffset = Math.Max(0f, _accountsOffset + by);
            }
            else if (clip == _m.PlacesClip)
            {
                _placesOffset = Math.Max(0f, _placesOffset + by);
            }
            else if (clip == _m.PlansClip)
            {
                _plansOffset = Math.Max(0f, _plansOffset + by);
            }
            _dirty = true;
        }
    }
}
