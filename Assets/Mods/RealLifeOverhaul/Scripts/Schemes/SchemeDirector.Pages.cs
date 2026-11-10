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
                _panel.Show(SchemePages.Wash(Words, place, shop.Name, shop.Cash, shop.Average, kept != null ? kept.Wash : 0, _state.Dirty, _state.Watched, SchemeGame.Level));
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
            if (f == null || f.State != FundState.Open)
            {
                return false;
            }
            SchemeGame.PutPhoneAway();
            return _panel.Show(SchemePages.Fund(Words, RivalName(f.Tycoon), RivalHolding(f.Tycoon), f, SchemeGame.Level));
        }

        // An answer's press, carried out: the one map from what a page offers to what the director does.
        private void Press(SchemePress p)
        {
            switch (p.Do)
            {
                case SchemeDo.Claim:
                    DoClaim(p.Place, p.A, p.B);
                    break;
                case SchemeDo.Expense:
                    DoExpense(p.B);
                    break;
                case SchemeDo.OpenLift:
                    ShowLift(p.Place);
                    break;
                case SchemeDo.OpenWash:
                    ShowWash(p.Place);
                    break;
                case SchemeDo.Lift:
                    SetLift(p.Place, p.A);
                    break;
                case SchemeDo.Wash:
                    SetWash(p.Place, p.A);
                    break;
                case SchemeDo.Club:
                    DoClub(p.Place, p.B);
                    break;
                case SchemeDo.FundPut:
                    Answer(FundPut());
                    break;
                case SchemeDo.FundLook:
                    Answer(FundLook());
                    ShowFund();
                    break;
                case SchemeDo.FundDown:
                    Answer(FundTurnDown());
                    break;
            }
        }

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
