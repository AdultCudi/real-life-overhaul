using UI.Notification;

namespace RealLifeOverhaul.Schemes
{
    // The pages: each is built by the game-free SchemePages from what the game holds now, shown on the panel, and an
    // answer's press is carried out here. A press that cannot be done says why in a notice and moves no money.
    public sealed partial class SchemeDirector
    {
        private static readonly SchemeWords Words = new SchemeWords { Fill = SchemeText.Fill, Money = SchemeText.Money };

        private void ShowClaim(string place, int cover)
        {
            if (Shop(place, out SchemeShopInfo shop) && SchemeBridges.CoverMost != null)
            {
                _panel.Show(SchemePages.Claim(Words, place, shop.Name, cover, SchemeBridges.CoverMost(cover)));
            }
        }

        private void ShowExpense()
        {
            _panel.Show(SchemePages.Expense(Words, AverageTakings(), SchemeBridges.TaxPercent != null ? SchemeBridges.TaxPercent() : 0));
        }

        private void ShowBooks(string place)
        {
            if (Shop(place, out SchemeShopInfo shop))
            {
                SchemeShop kept = _state.Shop(place, false);
                _panel.Show(SchemePages.Books(Words, place, shop.Name, kept != null ? kept.Lift : 0, kept != null ? kept.Wash : 0, _state.Dirty));
            }
        }

        private void ShowLift(string place)
        {
            if (Shop(place, out SchemeShopInfo shop))
            {
                SchemeShop kept = _state.Shop(place, false);
                _panel.Show(SchemePages.Lift(Words, place, shop.Name, kept != null ? kept.Lift : 0));
            }
        }

        private void ShowWash(string place)
        {
            if (Shop(place, out SchemeShopInfo shop))
            {
                SchemeShop kept = _state.Shop(place, false);
                _panel.Show(SchemePages.Wash(Words, place, shop.Name, shop.Cash, shop.Average, kept != null ? kept.Wash : 0, _state.Dirty, _state.Watched, SchemeGame.Level,
                    SchemeBridges.GameTax != null ? SchemeBridges.GameTax() : 0));
            }
        }

        private void ShowClub(string place)
        {
            if (Shop(place, out SchemeShopInfo shop))
            {
                bool people = !SchemeGame.InSession && SchemeBridges.People != null && SchemeBridges.People();
                _panel.Show(SchemePages.Club(Words, place, shop.Name, people, SchemeGame.Level));
            }
        }

        /// <summary>The fund a rival offers, as a page: put the money in, have it looked at first, or turn it down. False with no offer open.</summary>
        public bool ShowFund()
        {
            SchemeFund f = Ready("a fund's page") ? _state.Fund : null;
            if (f == null || SchemeGame.InSession || !SchemeRules.FundStands(_state, SchemeGame.Day))
            {
                return false;
            }
            SchemeGame.PutPhoneAway();
            return _panel.Show(SchemePages.Fund(Words, RivalName(f.Tycoon), RivalHolding(f.Tycoon), f, SchemeGame.Level));
        }

        // An answer's press, carried out: the map from what a page offers to what the director does is SchemePress.Carry.
        private void Press(SchemePress p)
        {
            p.Carry(this);
        }

        void ISchemeActs.Claim(string place, int cover, int size) => DoClaim(place, cover, size);

        void ISchemeActs.Expense(int size) => DoExpense(size);

        void ISchemeActs.OpenLift(string place) => ShowLift(place);

        void ISchemeActs.OpenWash(string place) => ShowWash(place);

        void ISchemeActs.Lift(string place, int permille) => SetLift(place, permille);

        void ISchemeActs.Wash(string place, int permille) => SetWash(place, permille);

        void ISchemeActs.Club(string place, int size) => DoClub(place, size);

        void ISchemeActs.FundPut() => Answer(FundPut());

        void ISchemeActs.FundLook()
        {
            Answer(FundLook());
            ShowFund();
        }

        void ISchemeActs.FundDown() => Answer(FundTurnDown());

        // What a press came to, as one of the game's notices.
        private static void Answer(SchemeSaid said)
        {
            if (!string.IsNullOrEmpty(said.Key))
            {
                SchemeGame.Tell(NotificationType.Info, said.Key, said.Pairs != null && said.Pairs.Length > 1 ? SchemeGame.Data(said.Pairs) : null);
            }
        }
    }
}
