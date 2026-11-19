using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The flats part of the property business: single flats and studios of residential buildings the player does not own, bought as a home on
    /// cash or a mortgage, owned with a value that moves with the building's, lived in or let, and sold on the market. The rules are
    /// <see cref="FlatRules"/> and the letting and sale rules of <see cref="EstateRules"/> on a one-unit holding each flat keeps; the game is
    /// <see cref="EstateGame"/>'s flats file. Nothing per frame: the listing is drawn when the day, the app or a purchase asks, and a flat's
    /// day runs with the property business's own morning.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private readonly List<FlatBuilding> _flatBuildings = new List<FlatBuilding>(512);
        private readonly List<FlatListing> _flatList = new List<FlatListing>(256);
        private int _flatListDay = -1;
        private bool _flatListDirty = true;

        /// <summary>The flats the player owns; empty while the feature is not running.</summary>
        public FlatBook Flats => _running && _state != null ? _state.Flats : null;

        internal static bool FlatsAllowed => Settings.Estate && Settings.EstateFlats;

        private bool FlatsOn => On(EstatePart.Flats) && On(EstatePart.Property);

        /// <summary>The flats for sale today, drawn again when the day or a purchase has changed them; none in a shared city or with the part off.</summary>
        public IReadOnlyList<FlatListing> FlatListings()
        {
            if (!FlatsOn || Save == null)
            {
                _flatList.Clear();
                _flatListDay = -1;
                return _flatList;
            }
            int day = Save.Day;
            if (!_flatListDirty && _flatListDay == day)
            {
                return _flatList;
            }
            _flatList.Clear();
            EstateGame.ReadFlatBuildings(_state, _climate.Index, _flatBuildings, _state.Flats);
            FlatBook book = _state.Flats;
            for (int i = 0; i < _flatBuildings.Count; i++)
            {
                FlatRules.Listings(_flatList, _flatBuildings[i], _state.Seed, day, (place, slot) => book.Find(place, slot) != null);
            }
            _flatListDay = day;
            _flatListDirty = false;
            if (Verbose)
            {
                Logger?.Info("Estate check (flats for sale, day " + day + "): " + _flatList.Count + " unit(s) of " + _flatBuildings.Count + " building(s) listed in window "
                             + FlatRules.Window(day) + ": PASS");
            }
            return _flatList;
        }

        /// <summary>One listed unit, or false when it is not for sale now.</summary>
        public bool ListingOf(string place, int slot, out FlatListing found)
        {
            IReadOnlyList<FlatListing> list = FlatListings();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Slot == slot && list[i].Place == place)
                {
                    found = list[i];
                    return true;
                }
            }
            found = default(FlatListing);
            return false;
        }

        /// <summary>The deposit at a percent of a price; the whole price for cash.</summary>
        public static int FlatDeposit(int price, int percent) => FlatRules.Deposit(price, percent);

        /// <summary>What the bank would do with this flat at this deposit and term, as the buying page shows it.</summary>
        public ProductAnswer FlatMortgage(int price, int percent, int term, out int financed, out int instalment)
        {
            financed = instalment = 0;
            CreditDesk desk = EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;
            if (desk == null)
            {
                return ProductAnswer.Off;
            }
            ProductOffer offer;
            ProductAnswer answer = desk.QuoteFlat(price, percent, term, out financed, out offer);
            if (answer == ProductAnswer.Yes)
            {
                instalment = AccountRules.InstalmentFor(financed, Mathf.Clamp(term, ProductTerms.TermLeast(ProductKind.Mortgage), ProductTerms.TermMost(ProductKind.Mortgage)));
            }
            return answer;
        }

        /// <summary>Why a listed flat cannot be bought now at the price shown, or None. Asked before any money moves, and by the page to grey its buttons.</summary>
        public FlatRefusal MayBuyFlat(string place, int slot, int shownPrice, int percent)
        {
            FlatListing l;
            bool listed = ListingOf(place, slot, out l);
            return FlatRules.MayBuy(FlatsOn && _running, GoingPublic.InSession, listed, _state != null ? _state.Flats.Flats.Count : 0, Save != null ? Mathf.FloorToInt(Save.Money) : 0,
                listed ? FlatDeposit(l.Price, percent) : 0, shownPrice, listed ? l.Price : shownPrice);
        }

        /// <summary>
        /// A listed flat is bought: refused before any money if anything of it has moved since the page was written; the deposit (or the whole
        /// price in cash) is taken, a mortgage opened for the rest, the building's registration rented by the player at no rent, and the flat is theirs.
        /// <paramref name="percent"/> 100 is cash. False, with nothing taken, when refused.
        /// </summary>
        public bool BuyFlat(string place, int slot, int shownPrice, int percent, int term)
        {
            FlatRefusal why = MayBuyFlat(place, slot, shownPrice, percent);
            if (why != FlatRefusal.None)
            {
                TellRefusal(why);
                return false;
            }
            FlatListing l;
            ListingOf(place, slot, out l);
            bool cash = percent >= FlatCatalogue.PayInFull;
            int deposit = FlatDeposit(l.Price, percent), financed = 0;
            CreditDesk desk = EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;
            ProductOffer offer = default(ProductOffer);
            if (!cash)
            {
                ProductAnswer answer = desk != null ? desk.QuoteFlat(l.Price, percent, term, out financed, out offer) : ProductAnswer.Off;
                if (answer != ProductAnswer.Yes)
                {
                    TellRefusal(FlatRefusal.Terms);
                    return false;
                }
                deposit = l.Price - financed;
            }
            string name = FlatName(l.Name, l.Kind);
            if (!Cost(deposit, EstateGame.FlatBuyKey, name, null, cash ? "a flat bought for cash" : "the deposit on a flat bought on a mortgage of " + financed, false, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            int back;
            if (!EstateGame.TakeHome(place, out back))
            {
                Pay(deposit, EstateGame.FlatBuyKey, name, null, "the deposit given back: the building could not be made the player's home");
                TellRefusal(FlatRefusal.Gone);
                return false;
            }
            var f = new OwnedFlat
            {
                Place = place, Slot = slot, Kind = l.Kind, Units = l.Units, Paid = l.Price, BoughtDay = Save.Day, Value = l.Price,
                BilledMonth = FlatRules.MonthOf(Save.Day, MonthDaysNow)
            };
            EstateHolding h = f.Hold;
            h.Place = f.Key;
            h.Use = (int)EstateUse.Homes;
            h.Cond = l.Cond;
            h.Income = l.Income;
            h.Base = l.Price;
            h.Paid = l.Price;
            h.Managed = false;
            _state.Flats.Flats.Add(f);
            EstateGame.ReadFlat(f, _state);
            if (back > 0)
            {
                Pay(back, EstateGame.FlatDepositKey, name, null, "the deposit of the rent the purchase ended");
            }
            if (!cash && desk != null)
            {
                desk.OpenFlatAccount(offer, l.Price, financed, term, f.Key, name);
            }
            _flatListDirty = true;
            Tell(NotificationType.Success, cash ? "reallife_flat_note_bought" : "reallife_flat_note_bought_mortgage",
                Data("flat", name, "amount", EstateText.Money(l.Price), "deposit", EstateText.Money(deposit)), TabMine, FlatPick(f));
            if (Verbose)
            {
                Logger?.Info("Estate check (a flat bought, " + name + "): the lease cost " + l.Price + " of a freehold share of " + l.Freehold + "; " + deposit + " paid now, " + financed
                             + " on the mortgage; the building's registration is the player's at no rent, so it is a home: PASS");
            }
            Changed();
            return true;
        }

        private void TellRefusal(FlatRefusal why)
        {
            Notifications.ShowError(RefusalKey(why));
        }

        private static string RefusalKey(FlatRefusal why)
        {
            switch (why)
            {
                case FlatRefusal.Off: return "reallife_flat_refuse_off";
                case FlatRefusal.Session: return "reallife_flat_refuse_session";
                case FlatRefusal.Gone: return "reallife_flat_refuse_gone";
                case FlatRefusal.Most: return "reallife_flat_refuse_most";
                case FlatRefusal.Cash: return "reallife_flat_refuse_cash";
                case FlatRefusal.Moved: return "reallife_flat_refuse_moved";
                case FlatRefusal.NotMine: return "reallife_flat_refuse_not_mine";
                case FlatRefusal.Let: return "reallife_flat_refuse_let";
                case FlatRefusal.Selling: return "reallife_flat_refuse_selling";
                default: return "reallife_flat_refuse_terms";
            }
        }

        /// <summary>The words that name a flat on a line: its address and its kind.</summary>
        internal static string FlatName(string address, int kind)
        {
            return EstateText.T("reallife_flat_name", "address", address ?? string.Empty, "kind", EstateText.T(FlatKindKeys[FlatCatalogue.Clamp(kind, 0, FlatCatalogue.Kinds - 1)]));
        }

        internal static readonly string[] FlatKindKeys = { "reallife_flat_kind_studio", "reallife_flat_kind_one", "reallife_flat_kind_two", "reallife_flat_kind_three" };

        internal static string FlatPick(OwnedFlat f) => "flt:" + f.Key;

        /// <summary>
        /// A notice about a unit, a call or an applicant of a flat (a pick of a building's own tabs, "unt:flat:...", "app:flat:...", "own:flat:...") opens the flat's page on the
        /// Mine tab, where a flat is managed along with its tenants' and calls' rows on the Tenants and Works tabs.
        /// </summary>
        private static void FlatNoticePick(ref int tab, ref string pick)
        {
            if (pick == null || pick.Length < 9 || string.CompareOrdinal(pick, 4, "flat:", 0, 5) != 0)
            {
                return;
            }
            string key = pick.Substring(4);
            if (!pick.StartsWith("own:", StringComparison.Ordinal))
            {
                int at = key.LastIndexOf(':');
                key = at > 4 ? key.Substring(0, at) : key;   // "unit" or "applicant" after the flat's own key
            }
            tab = TabMine;
            pick = "flt:" + key;
        }

        private int MonthDaysNow => MacroCatalogue.MonthDays(Math.Max(1, _climate.DaysPerYear));

        /// <summary>Whether a flat of this key is the player's: what the credit desk asks about a mortgage secured by one. Unknown while the business is not running or in a shared city.</summary>
        internal static int FlatSecurityState(string key)
        {
            EstateDirector d = Instance;
            if (d == null || !d._running || d._state == null || GoingPublic.InSession)
            {
                return CreditDesk.FlatUnknown;
            }
            return d._state.Flats.Find(key) != null ? CreditDesk.FlatHeld : CreditDesk.FlatGone;
        }

        // ------------------------------------------------------------------------------------------------- what a flat is, for the pages

        /// <summary>The service charge a month: a share of what the flat would let for.</summary>
        public int FlatService(OwnedFlat f)
        {
            return FlatRules.ServiceMonth(FlatMarketRent(f), MonthDaysNow);
        }

        /// <summary>The unit's share of the building's property tax a month.</summary>
        public int FlatTax(OwnedFlat f)
        {
            return FlatRules.TaxMonth(f.TaxYear, f.Share);
        }

        /// <summary>The rent the flat would let for a day at the market's.</summary>
        public int FlatMarketRent(OwnedFlat f)
        {
            f.Hold.Base = f.Value;
            return EstateRules.MarketRent(f.Hold, _climate);
        }

        /// <summary>What is owed on the flat's mortgage, 0 for none.</summary>
        public int FlatOwed(OwnedFlat f)
        {
            CreditDesk desk = EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;
            return desk != null ? desk.OwedOn(f.Key) : 0;
        }

        // ------------------------------------------------------------------------------------------------- the flat's day

        private void FlatsDay(int day)
        {
            FlatBook book = _state.Flats;
            FoldFlats(day);
            int monthDays = MonthDaysNow, month = FlatRules.MonthOf(day, monthDays), billed = 0, sold = 0, rented = 0;
            for (int i = book.Flats.Count - 1; i >= 0; i--)
            {
                OwnedFlat f = book.Flats[i];
                if (EstateGame.ReadFlat(f, _state))
                {
                    int value = FlatRules.Value(EstateGame.BuildingValue(f.Place, _state, _climate.Index), f.Place, f.Units, f.Hold.Income, f.Slot);
                    f.Value = value > 0 ? value : f.Value;
                }
                f.Hold.Base = f.Value;
                f.Hold.Paid = f.Paid;
                if (f.Seen && FlatRules.BillDue(f.BilledMonth, month))
                {
                    FlatBills(f, month);
                    billed++;
                }
                FlatWorks(f, day);
                if (f.Hold.Managed)
                {
                    FlatLetDay(f, day);
                    rented++;
                }
                if (FlatSaleDay(f, day))
                {
                    sold++;
                }
            }
            if (Verbose && book.Flats.Count + sold > 0)
            {
                Logger?.Info("Estate check (flats, day " + day + "): " + book.Flats.Count + " held, month " + month + " billed for " + billed + ", " + rented + " let, " + sold + " sold: PASS");
            }
        }

        /// <summary>A month's service charge and the unit's share of the property tax: each its own bank line, once for the month.</summary>
        private void FlatBills(OwnedFlat f, int month)
        {
            f.BilledMonth = month;
            string name = FlatName(f.Name, f.Kind);
            int service = FlatService(f), tax = FlatTax(f);
            string owner = string.IsNullOrEmpty(f.Landlord) ? EstateText.T("reallife_flat_landlord") : f.Landlord;
            Cost(service, EstateGame.FlatServiceKey, name + ", " + owner, null, "a month's service charge to " + owner, true, false);
            Cost(tax, EstateGame.FlatTaxKey, name, null, "the flat's share of the building's property tax for a month", true, false);
            f.Hold.CostYear += service + tax;
        }

        /// <summary>
        /// A flat's works end, whoever lives in it; the morning of a flat the player lets: wear, the tenant's day, the landlord's duties (satisfaction, repair
        /// calls, the city's inspection, the manager chosen) by the rules a building's units have, then the applicants. The manager value passes through as it is.
        /// </summary>
        private void FlatWorks(OwnedFlat f, int day)
        {
            bool granted;
            EstateWork done = EstateRules.Finish(f.Hold, _state.Seed, day, _climate, out granted);
            if (done != EstateWork.None)
            {
                WorkDone(f.Hold, done, granted);
            }
        }

        private void FlatLetDay(OwnedFlat f, int day)
        {
            EstateHolding h = f.Hold;
            EstateUnit u = h.Units[0];
            bool duties = Duties;
            string name = FlatName(f.Name, f.Kind), pick = FlatPick(f);
            EstateRules.Lapse(h, day);
            EstateRules.Wear(h);
            if (duties)
            {
                PeopleMorning(h, day);
                ServicesMorning(h, day);
            }
            int rent = u.Rent;
            TenancyDay d = EstateDay.Unit(_state, h, 0, day, _climate, duties);
            if (d.Lump > 0)
            {
                Pay(d.Lump, EstateGame.FlatRentKey, name, null, "arrears paid");
                h.RentYear += d.Lump;
                ManagerCut(h, d.Lump, "arrears paid");
            }
            if (d.Pays && rent > 0)
            {
                Pay(rent, EstateGame.FlatRentKey, name, null, "a day's rent of a flat let");
                h.RentYear += rent;
                ManagerCut(h, rent, "a day's rent");
            }
            if (d.Evicted || d.Left || d.Quit)
            {
                Tell(NotificationType.Warning, d.Evicted ? "reallife_estate_note_evicted" : "reallife_estate_note_left",
                    Data("building", name, "amount", EstateText.Money(d.Lump), "lost", EstateText.Money(d.WrittenOff)), TabMine, pick);
            }
            else if (d.Missed && u.ArrearsDays == 1)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_missed", Data("building", name, "amount", EstateText.Money(rent)), TabMine, pick);
            }
            if (duties)
            {
                DutiesMorning(h, day);
            }
            bool barred = duties && EstateRules.Barred(h);
            RentApplicant a = barred ? null : EstateRules.Apply(h, 0, _state.Seed, day, _climate, false, duties ? EstateRules.ApplicantLift(h) : 1000);
            if (a != null)
            {
                Tell(NotificationType.Info, "reallife_estate_note_applied", Data("building", name, "who", EstateText.Tenant(h.Use, a.Kind), "amount", EstateText.Money(a.Offer)), TabMine, pick);
            }
            if (duties && h.Manager != (int)EstateManager.Self)
            {
                AgentLets(h, day);
            }
        }

        /// <summary>The offers and the day of a sale; true when the flat has gone. Money moves only here, through the same lines as a building's sale.</summary>
        private bool FlatSaleDay(OwnedFlat f, int day)
        {
            EstateHolding h = f.Hold;
            string name = FlatName(f.Name, f.Kind), pick = FlatPick(f);
            if (h.SaleDay > 0 && day >= h.SaleDay)
            {
                if (h.SaleFails)
                {
                    h.SaleDay = h.SalePrice = 0;
                    h.SaleFails = false;
                    Tell(NotificationType.Warning, "reallife_flat_note_sale_fell", Data("flat", name), TabMine, pick);
                    return false;
                }
                if (GoingPublic.InSession)
                {
                    h.SaleDay = day + 1;   // who owns what is the host's record in a session: the sale completes the first day played alone
                    return false;
                }
                CompleteFlatSale(f, day);
                return true;
            }
            if (h.Offer > 0 && day > h.OfferLapse)
            {
                h.Offer = h.OfferLapse = 0;
                Tell(NotificationType.Info, "reallife_estate_note_offer_lapsed", Data("building", name), TabMine, pick);
            }
            if (h.SaleDay == 0 && f.Seen)
            {
                int offer = EstateRules.SaleOffer(h, _state.Seed, day, f.Value, _climate.Cycle);
                if (offer > 0)
                {
                    Tell(NotificationType.Success, "reallife_estate_note_offer", Data("building", name, "amount", EstateText.Money(offer), "day", h.OfferLapse.ToString()), TabMine, pick);
                }
            }
            return false;
        }

        private void CompleteFlatSale(OwnedFlat f, int day)
        {
            EstateHolding h = f.Hold;
            int price = h.SalePrice;
            string name = FlatName(f.Name, f.Kind);
            int fee = EstateRules.AgentFee(price, _climate.Index), tax = EstateRules.GainsTax(price, fee, f.Paid, h.Spent, _climate.TaxPercent);
            Pay(price, EstateGame.FlatSoldKey, name, null, "a flat sold for " + price);
            EconomyDirector credit = EconomyDirector.Instance;
            if (credit != null && credit.Desk != null)
            {
                credit.Desk.SettleFlatSale(f.Key, price);   // what is owed on its mortgage comes out of the sale first
            }
            Cost(fee, EstateGame.FeeKey, name, null, "the agent's fee on a sale for " + price, true);
            Cost(tax, EstateGame.GainsKey, name, null, "tax on the gain over " + f.Paid + " paid", true, false);
            EstateGame.FlatLeft(f.Place);
            _state.Flats.Flats.Remove(f);
            _flatListDirty = true;
            Tell(NotificationType.Success, "reallife_flat_note_sold", Data("flat", name, "amount", EstateText.Money(price), "fee", EstateText.Money(fee + tax)), TabMine, null);
            if (Verbose)
            {
                Logger?.Info("Estate check (a flat sold, " + name + "): " + price + " came in, the selling fee " + fee + " and the tax on the gain " + tax
                             + "; the mortgage was settled out of it; the home is the market's rent now: PASS");
            }
        }

        /// <summary>
        /// A building the player has bought whole takes in the flats they held in it: the unit's share of the price paid is credited so it is not
        /// paid for twice, what the flat cost over it counts against a gain, a mortgage on it is the building's, and a tenant's lease ends.
        /// </summary>
        private void FoldFlats(int day)
        {
            FlatBook book = _state.Flats;
            for (int i = book.Flats.Count - 1; i >= 0; i--)
            {
                OwnedFlat f = book.Flats[i];
                if (!EstateGame.OwnsBuilding(f.Place))
                {
                    continue;
                }
                EstateGame.ReadFlat(f, _state);
                int price = EstateGame.PurchasePrice(f.Place);
                int credit = FlatRules.FoldCredit(price, f.Place, f.Units, f.Hold.Income, f.Slot);
                string name = FlatName(f.Name, f.Kind);
                Pay(credit, EstateGame.FlatFoldKey, name, null, "a flat folded into the building bought whole: its share of the price is not paid twice");
                EstateHolding h = _state.Holding(f.Place);
                if (h != null)
                {
                    h.Spent += FlatRules.FoldBasis(f.Paid, credit);
                }
                EconomyDirector direct = EconomyDirector.Instance;
                if (direct != null && direct.Desk != null)
                {
                    direct.Desk.MoveSecurity(f.Key, EstateGame.BuildingKey(f.Place), f.Name, price);
                }
                f.Hold.Units[0].Vacant();
                book.Flats.RemoveAt(i);
                _flatListDirty = true;
                Tell(NotificationType.Info, "reallife_flat_note_folded", Data("flat", name, "amount", EstateText.Money(credit)), TabMine, h != null ? "own:" + h.Place : null);
                if (Verbose)
                {
                    Logger?.Info("Estate check (a flat folded, " + name + "): the building was bought for " + price + "; the unit's " + credit
                                 + " is credited, not paid twice; the mortgage on it is the building's: PASS");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- what the player does with a flat

        private OwnedFlat FlatOf(string key)
        {
            return FlatsOn && _state != null ? _state.Flats.Find(key) : null;
        }

        /// <summary>A flat is put up for sale at a share of its value, or taken off (0).</summary>
        public void ListFlat(string key, int share)
        {
            OwnedFlat f = FlatOf(key);
            if (f == null || f.Hold.SaleDay > 0)
            {
                return;
            }
            EstateHolding h = f.Hold;
            h.List = share <= 0 ? 0 : EstateCatalogue.Share(f.Value, EstateCatalogue.Clamp(share, EstateCatalogue.ListLeast, EstateCatalogue.ListMost));
            h.Offer = h.OfferLapse = 0;
            Changed();
        }

        public void AcceptFlatOffer(string key)
        {
            OwnedFlat f = FlatOf(key);
            if (f != null && EstateRules.AcceptOffer(f.Hold, _state.Seed, Save.Day, _climate.Cycle))
            {
                Changed();
            }
        }

        public void RefuseFlatOffer(string key)
        {
            OwnedFlat f = FlatOf(key);
            if (f != null && f.Hold.Offer > 0)
            {
                f.Hold.Offer = f.Hold.OfferLapse = 0;
                Changed();
            }
        }

        /// <summary>The player lets the flat, or stops: only a flat with nobody in it goes back.</summary>
        public void LetFlat(string key, bool on)
        {
            OwnedFlat f = FlatOf(key);
            if (f == null)
            {
                return;
            }
            EstateHolding h = f.Hold;
            if (on && !h.Managed && h.SaleDay == 0)
            {
                h.Managed = true;
                h.Units[0].ToLet = true;
                h.Units[0].VoidDays = 0;
            }
            else if (!on && h.Managed && !h.Units[0].Let)
            {
                h.Managed = false;
                h.ClearApplicants();
            }
            Changed();
        }

        public void SetFlatAsk(string key, int ask)
        {
            OwnedFlat f = FlatOf(key);
            if (f != null)
            {
                f.Hold.Units[0].Ask = EstateCatalogue.Clamp(ask, EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
                Changed();
            }
        }

        public void AcceptFlatApplicant(string key, int id)
        {
            OwnedFlat f = FlatOf(key);
            if (f != null && EstateRules.Accept(f.Hold, id, Save.Day))
            {
                Changed();
            }
        }

        public void RefuseFlatApplicant(string key, int id)
        {
            OwnedFlat f = FlatOf(key);
            RentApplicant a = f != null ? f.Hold.ApplicantOf(id) : null;
            if (a != null)
            {
                f.Hold.Units[0].Applicants.Remove(a);
                Changed();
            }
        }

        public void ServeFlatNotice(string key)
        {
            OwnedFlat f = FlatOf(key);
            if (f != null && EstateRules.ServeNotice(f.Hold, 0, Save.Day, _climate.RentLever))
            {
                Changed();
            }
        }

        public void FlatToCourt(string key)
        {
            OwnedFlat f = FlatOf(key);
            if (f != null && EstateRules.MayGoToCourt(f.Hold, 0, Save.Day)
                && Cost(EstateRules.CourtFee(_climate.Index), EstateGame.CourtKey, FlatName(f.Name, f.Kind), null, "a court's fee for a flat", false))
            {
                EstateRules.GoToCourt(f.Hold, 0, Save.Day);
                Changed();
            }
        }

        private void FlatsWritten(Dictionary<string, string> data)
        {
            FlatStore.Save(_state.Flats, data);
        }

        /// <summary>How many things of the flats wait for an answer.</summary>
        private int FlatsWaiting()
        {
            int waiting = 0;
            for (int i = 0; FlatsOn && _state != null && i < _state.Flats.Flats.Count; i++)
            {
                EstateHolding h = _state.Flats.Flats[i].Hold;
                waiting += h.ApplicantCount + (h.Offer > 0 ? 1 : 0) + EstateRules.UnitsToAnswer(h, Save.Day);
                waiting += Duties && h.Managed && h.Manager != (int)EstateManager.Manager ? EstateRules.CallsWaiting(h) : 0;
            }
            return waiting;
        }

        // ------------------------------------------------------------------------------------------------- the test tools

        /// <summary>Options, Test tools: the cheapest flat for sale is bought for cash, or the reason it is not.</summary>
        public void TestFlatBuy()
        {
            IReadOnlyList<FlatListing> list = FlatListings();
            int best = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (best < 0 || list[i].Price < list[best].Price)
                {
                    best = i;
                }
            }
            if (best < 0)
            {
                Notifications.ShowError("reallife_flat_test_none");
                return;
            }
            FlatListing l = list[best];
            bool bought = BuyFlat(l.Place, l.Slot, l.Price, FlatCatalogue.PayInFull, 0);
            Logger?.Info("Estate test: flat for sale at " + l.Name + " (" + l.Kind + ") for " + l.Price + " tried for cash: " + (bought ? "bought" : "refused"));
            Open(TabMine, null);
        }

        /// <summary>Options, Test tools: the month's service charge and tax are taken now for every flat held, once for the month.</summary>
        public void TestFlatBills()
        {
            if (!FlatsOn || Save == null)
            {
                Notifications.ShowError("reallife_flat_refuse_off");
                return;
            }
            FlatBook book = _state.Flats;
            int month = FlatRules.MonthOf(Save.Day, MonthDaysNow);
            for (int i = 0; i < book.Flats.Count; i++)
            {
                book.Flats[i].BilledMonth = month - 1;
                FlatBills(book.Flats[i], month);
            }
            Logger?.Info("Estate test: the month's service charge and tax taken for " + book.Flats.Count + " flat(s), each once for month " + month);
            Changed();
        }
    }
}
