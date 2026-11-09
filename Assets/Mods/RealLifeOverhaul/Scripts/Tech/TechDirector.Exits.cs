using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Exits (Tech/Venture/ExitRules): offers for the player's firms, a sale through the firm account, the way to the market, the player's own shares sold later,
    /// the soft landing, and the city's startups as real places (a founder who is a person of the city, a floor of a tower, people hired from the market the
    /// player hires from). Under the options: with Startups off none of it runs. Nothing per frame: it works on the day, on a press, in a desk session.
    /// </summary>
    public sealed partial class TechDirector
    {
        /// <summary>A notice's or a link's row for a firm's page of exits (Funding tab, the cap table's third page).</summary>
        public const string ExitRow = "exits:";

        private ExitFile _exits;
        private readonly List<ExitBuyer> _buyers = new List<ExitBuyer>(12);
        private readonly List<string> _bankNames = new List<string>(3);
        private readonly List<KeyValuePair<int, string>> _listedNames = new List<KeyValuePair<int, string>>(8);
        private readonly List<StartupTenant> _tenants = new List<StartupTenant>(VentureCatalogue.StartupsMost);
        private static readonly StartupTenant[] NoTenants = new StartupTenant[0];

        /// <summary>What exits keep; null while the venture part is off.</summary>
        public ExitFile Exits => _ventureOn ? _exits : null;

        /// <summary>The startups that rent a floor, for the headquarters; none while the venture part is off.</summary>
        internal IReadOnlyList<StartupTenant> StartupTenants => _ventureOn && _exits != null ? _tenants : (IReadOnlyList<StartupTenant>)NoTenants;

        private void StartExits()
        {
            ExitFile file = ExitStore.Load(Save.modData, out int version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new ExitFile();
            }
            if (file.Seed == 0)
            {
                file.Seed = Math.Max(1, Math.Abs(unchecked(_venture.Seed * 31 + 7)));   // the host's file seeds it: the same startups give the same founders and floors
            }
            _exits = file;
            _tenants.Clear();
            RebuildTenants();
            try
            {
                ExitBridges.Sessions?.Invoke(true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Logger?.Info("Tech: exits " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + Save.Day + ": " + file.Firms.Count
                + " firm(s) with something standing, "
                         + file.Founders.Count + " founder(s) of the city's startups.");
        }

        private void StopExits(bool keep)
        {
            if (_exits == null)
            {
                return;
            }
            if (keep && Save != null)
            {
                WriteExits(Save.modData);
            }
            try
            {
                ExitBridges.Sessions?.Invoke(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _exits = null;
            _tenants.Clear();
        }

        private void WriteExits(Dictionary<string, string> data)
        {
            if (_exits != null && data != null)
            {
                data[ExitStore.Key] = ExitStore.Write(_exits);
            }
        }

        private void ExitsDay(int day)
        {
            if (_exits == null || _exits.LastDay >= day)
            {
                return;
            }
            _exits.LastDay = day;
            PlacesDay(day);
            FirmExitsDay(day);
        }

        // ---------------------------------------------------------------- the city's startups as real places

        // Each startup of the city has a founder who is a person of the city when the population runs (an invented one when it does not), a floor of a tower it lets,
        // and takes people from the market the player hires from. A startup first seen today (an older save's) is given all three, seeded.
        private void PlacesDay(int day)
        {
            bool people = ExitBridges.PeopleRun();
            int towers = Math.Min(HqTowers.Towers(HiringCatalogue.RivalFirms.Length), Headquarters.TowerNames.Length);
            bool changed = false;
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                FounderRec r = _exits.FounderOf(s.Id);
                if (r == null)
                {
                    r = ExitPlaces.Invent(_exits, s, day);
                    _exits.Founders.Add(r);
                    changed = true;
                }
                if (!s.Alive)
                {
                    if (r.Tower >= 0)
                    {
                        r.Tower = -1;
                        r.Slot = 0;
                        changed = true;
                    }
                    continue;
                }
                if (r.Tower < 0 && towers > 0)
                {
                    changed |= ExitPlaces.Let(_exits, r, towers);
                }
                if (people && !r.Linked && ExitBridges.ResidentOf(s.Id, out ResidentInfo info))
                {
                    ExitPlaces.Resident(r, s, info);
                    changed = true;
                }
                if (_hiringOn && _hiring != null)
                {
                    Seeker taken = ExitPlaces.Hire(_hiring, _exits, r, s, r.Hired, Level(s.Burn), day);
                    if (taken != null && Verbose)
                    {
                        Logger?.Info("Tech check (startups, day " + day + "): " + s.Name + " took " + taken.P.Name + " from the labour market (seeker " + taken.Id + "): PASS");
                    }
                }
            }
            for (int i = _exits.Founders.Count - 1; i >= 0; i--)
            {
                if (_venture.StartupOf(_exits.Founders[i].Startup) == null)
                {
                    _exits.Founders.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed)
            {
                RebuildTenants();
                ChangedTech();
            }
        }

        private void RebuildTenants()
        {
            _tenants.Clear();
            if (_venture == null || _exits == null)
            {
                return;
            }
            for (int i = 0; i < _exits.Founders.Count; i++)
            {
                FounderRec r = _exits.Founders[i];
                Startup s = r.Tower >= 0 ? _venture.StartupOf(r.Startup) : null;
                if (s != null && s.Alive)
                {
                    _tenants.Add(new StartupTenant { Company = HiringCatalogue.RivalFirms.Length + s.Id, Name = s.Name, Tower = r.Tower, Slot = r.Slot });
                }
            }
            _tenants.Sort((a, b) => a.Tower != b.Tower ? a.Tower.CompareTo(b.Tower) : a.Slot.CompareTo(b.Slot));
        }

        /// <summary>A startup's founder and where it works; null for a startup not looked at yet.</summary>
        internal FounderRec FounderOf(int startup) => _ventureOn && _exits != null ? _exits.FounderOf(startup) : null;

        /// <summary>A startup's floor: the tower, floor, street and number, once the headquarters have put it on one.</summary>
        internal bool StartupFloor(Startup s, out string tower, out int floor, out string street, out int number)
        {
            tower = street = null;
            floor = number = 0;
            TechSites sites = TechSites.Instance;
            return s != null && sites != null && sites.FloorOf(s.Name, out tower, out floor, out street, out number);
        }

        internal bool StartupDestination(Startup s) => StartupFloor(s, out _, out _, out string street, out int number) && FirmsGame.Destination(street, number);

        // ---------------------------------------------------------------- offers, and the way to the market, by the day

        private void FirmExitsDay(int day)
        {
            int mood = VentureRules.Mood(HiringGame.Cycle);
            bool gathered = false;
            for (int i = 0; i < _firms.Count; i++)
            {
                FirmPlace firm = _firms[i];
                if (firm.Reg == null)
                {
                    continue;
                }
                string place = firm.File.Place;
                FirmExit e = _exits.Exit(place, true);
                Cap c = _venture.CapOf(place);
                ExitRules.Round(c, e, day);
                if (ExitRules.Lapse(e, day) > 0)
                {
                    ChangedTech();
                }
                ExitRules.Pricing priced = ExitRules.Price(_exits, e, day);
                if (priced == ExitRules.Pricing.Priced)
                {
                    ListFirm(firm, e, day);
                }
                else if (priced == ExitRules.Pricing.CalledOff)
                {
                    TellTech(NotificationType.Warning, "reallife_exit_note_called_off", Data("firm", firm.File.Name, "bank", e.BankName), TabFunding, ExitRow + place);
                    ChangedTech();
                }
                long mrr = FirmsRules.Arr(_firmsFile, place) / 12;
                if (mrr < ExitCatalogue.OfferMrrLeast && !ExitRules.Failing(c, BurnOf(firm)))
                {
                    continue;
                }
                if (!gathered)
                {
                    gathered = true;
                    GatherBuyers();
                }
                FirmValue(firm, out long value, out int growthBp);
                ExitOffer o = ExitRules.Offer(_exits, e, day, mood, mrr, value, growthBp, _buyers) ?? ExitRules.Hire(_exits, e, c, BurnOf(firm), day, _buyers);
                if (o != null)
                {
                    TellTech(NotificationType.Info, o.Hire ? "reallife_exit_note_hire" : "reallife_exit_note_offer",
                        Data("firm", firm.File.Name, "buyer", o.Name, "amount", TechText.Cents(Level(o.Price)), "day", o.Lapse.ToString(CultureInfo.InvariantCulture)),
                            TabFunding, ExitRow + place);
                    Logger?.Info("Tech: " + o.Name + " offers " + TechText.Cents(Level(o.Price)) + (o.Hire ? " for the team of " : " for ") + firm.File.Name + " on day "
                        + day + " (growth " + growthBp
                                 + " bp a month, value " + TechText.Cents(Level(value)) + "), standing to day " + o.Lapse + ".");
                    ChangedTech();
                }
            }
        }

        // The firm's burn a day at the start's prices: what it loses a day, nothing for a firm that makes a profit.
        private long BurnOf(FirmPlace firm) => Math.Max(0L, Base(-FirmProfit(firm)));

        /// <summary>A firm's worth by its books (FirmsRules.Value) and how its takings grew over the month before, basis points; cents at the start's prices.</summary>
        internal void FirmValue(FirmPlace firm, out long value, out int growthBp)
        {
            growthBp = 0;
            if (TechStatusRules.Change(firm.File.Revenue30, firm.File.Prev30, firm.File.PrevDays, out int percent))
            {
                growthBp = TalentCatalogue.Clamp(percent * 100, -5000, 20000);
            }
            long arr = FirmsRules.Arr(_firmsFile, firm.File.Place);
            value = FirmsRules.Value(firm.File, arr, growthBp);
        }

        private void GatherBuyers()
        {
            _buyers.Clear();
            _listedNames.Clear();
            ExitBridges.Listed?.Invoke(_listedNames);
            for (int i = 0; i < _listedNames.Count; i++)
            {
                _buyers.Add(new ExitBuyer { Buyer = 0, Id = _listedNames[i].Key, Name = _listedNames[i].Value, Cash = long.MaxValue });
            }
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                if (s.Alive && s.Stage >= (int)RoundStage.SeriesA && s.Cash > 0)
                {
                    _buyers.Add(new ExitBuyer { Buyer = 1, Id = s.Id, Name = s.Name, Cash = s.Cash });
                }
            }
        }

        // ---------------------------------------------------------------- a sale of the firm

        /// <summary>What a sale at an offer's price would come to now: the pot, the investors' lines and what the wallet gains.</summary>
        internal SaleResult ExitPreview(string place, ExitOffer o)
        {
            return ExitRules.Settle(_ventureOn ? _venture.CapOf(place) : null, o != null ? o.Price : 0, Base(VentureGame.Cash), Save != null ? Save.Day : 0);
        }

        /// <summary>
        /// An offer taken: the business passes to its buyer as the game's sale of a business ends one (the staff go along), the buyer's money comes in, and the
        /// investors' lines of the waterfall are paid out of it before the player has anything. Refused while the business cannot be handed over.
        /// </summary>
        internal bool ExitAccept(string place, int offerId)
        {
            FirmPlace firm = FirmOf(place);
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            ExitOffer o = e != null ? e.OfferOf(offerId) : null;
            if (firm == null || o == null || Save == null || firm.Reg == null)
            {
                return false;
            }
            int day = Save.Day;
            string name = firm.File.Name;
            if (day > o.Lapse)
            {
                e.Offers.Remove(o);
                ChangedTech();
                return false;
            }
            Startup buyer = o.Buyer == 1 ? _venture.StartupOf(o.BuyerId) : null;
            if (o.Buyer == 1 && (buyer == null || !buyer.Alive || buyer.Cash < o.Price))
            {
                e.Offers.Remove(o);
                Notifications.Show(NotificationType.Warning, "reallife_exit_note_buyer_gone", Data("firm", name, "buyer", o.Name), 8f, null, null, true, false);
                ChangedTech();
                return false;
            }
            if (!ExitBridges.CanHand(place))
            {
                Notifications.ShowError("reallife_exit_refuse_hand_over");
                return false;
            }
            Cap c = _venture.CapOf(place);
            bool funded = c != null && c.Acc.Open;
            int fixtures = ExitBridges.Fixtures != null ? Math.Max(0, ExitBridges.Fixtures(place)) : 0;   // read while the business is still the player's
            if (funded)
            {
                c.Acc.Wound = true;   // before anything moves: while the firm is sold, the wallet's floor for it is settled by the sale and not counted again
            }
            if (ExitBridges.HandOver == null || !ExitBridges.HandOver(place, o.Name))
            {
                if (funded)
                {
                    c.Acc.Wound = false;
                }
                Notifications.ShowError("reallife_exit_refuse_hand_over");
                return false;
            }
            long mrr = FirmsRules.Arr(_firmsFile, place) / 12;
            SaleResult r = ExitRules.Settle(c, o.Price, Base(VentureGame.Cash), day);
            VentureGame.Give(Level(r.Price), VentureGame.ExitKey, name);
            if (r.Paid > 0)
            {
                VentureGame.Pay(Level(r.Paid), VentureGame.WindKey, name, null, true);
            }
            if (buyer != null)
            {
                buyer.Cash -= o.Price;
                buyer.Mrr += Math.Min(mrr, Math.Max(buyer.Mrr, 1));
            }
            long gain = ExitGains.ForSale(_gains, c, place, name, r.Player, fixtures * 100L, day, Prices.Level);   // before the cap table goes
            string lead = c != null ? LeadName(c) : string.Empty;
            float evidence = funded ? StartupMoney.Evidence(c) : 0f;
            int suit = 0, fraud = 0;
            if (r.Gap > 0)
            {
                long owed = Level(r.Gap) * (1000 + MoneyCatalogue.CostsPermille) / 1000;
                suit = TechBridges.InvestorClaim != null ? TechBridges.InvestorClaim(owed / 100, name, lead) : 0;
                ShutDoors(day);
                TechBridges.Fame?.Invoke(3, -MoneyCatalogue.StandingFall);
                fraud = r.Fraud && TechBridges.InvestorFraud != null ? TechBridges.InvestorFraud((int)Math.Min(int.MaxValue, Level(r.Taken) / 100), evidence) : 0;
            }
            if (c != null)
            {
                _venture.Caps.Remove(c);
            }
            e.Offers.Clear();
            e.SoldDay = day;
            e.SoldFor = r.Price;
            e.SoldTo = o.Name;
            e.Stage = ListStage.None;
            RefreshFloor(day);
            ExitBridges.Practised?.Invoke(TalentCatalogue.Selling, 2);
            ExitBridges.Practised?.Invoke(TalentCatalogue.Analysis, 1);
            Story(o.Hire ? "reallife_tech_news_firm_hired" : "reallife_tech_news_firm_sold", "firm", name, "buyer", o.Name);
            string key = r.Gap > 0 ? (r.Fraud ? "reallife_exit_note_sold_fraud" : "reallife_exit_note_sold_claim") : o.Hire ? "reallife_exit_note_hired" : "reallife_exit_note_sold";
            Notifications.Show(r.Gap > 0 ? NotificationType.Error : NotificationType.Success, key,
                Data("firm", name, "buyer", o.Name, "amount", TechText.Cents(Level(r.Price)), "investors", TechText.Cents(Level(r.Paid)), "you", TechText.Cents(Level(Math.Max(0L, r.Player))),
                    "gap", TechText.Cents(Level(r.Gap))), 12f, null, null, true, false);
            GainNotice(name, gain);
            Logger?.Info("Tech: " + name + " " + (o.Hire ? "(its team) " : string.Empty) + "sold to " + o.Name + " on day " + day + " for " + TechText.Cents(Level(r.Price))
                + ": the firm's account held "
                         + TechText.Cents(Level(r.Cash)) + ", a director's loan " + TechText.Cents(Level(r.Loan)) + ", the bridge " + TechText.Cents(Level(r.Bridge))
                             + "; the investors were owed "
                         + TechText.Cents(Level(r.Owed)) + " and paid " + TechText.Cents(Level(r.Paid)) + ", claim " + TechText.Cents(Level(r.Gap)) + " (suit " + suit
                             + ", fraud case " + fraud + "); the wallet "
                         + (r.Player >= 0 ? "gained " : "gave ") + TechText.Cents(Level(Math.Abs(r.Player))) + GainLog(gain) + ".");
            ChangedTech();
            return true;
        }

        internal void OpenExits(string place) => OpenTech(TabFunding, ExitRow + place);

        /// <summary>The player's whole company on the city's stock market: the stock market's own page for it is opened, as "Ask for a listing" did
        /// before the firm had a way of its own.</summary>
        internal void ListWholeCompany(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null)
            {
                return;
            }
            bool taken = TechBridges.Ipo != null && TechBridges.Ipo();
            Notifications.Show(taken ? NotificationType.Success : NotificationType.Info, taken ? "reallife_vc_note_listing_taken" : "reallife_vc_note_listing_closed",
                Data("firm", firm.File.Name), 8f, null, null, true, false);
        }

        internal void ExitDecline(string place, int offerId)
        {
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            ExitOffer o = e != null ? e.OfferOf(offerId) : null;
            if (o != null)
            {
                e.Offers.Remove(o);
                ChangedTech();
            }
        }

        /// <summary>A counter to an offer, one an offer: 0 a tenth more, 1 a fifth, 2 a third. The buyer pays more, holds at its price, or walks
        /// away. The result: 0 more, 1 held, 2 walked, -1 nothing done.</summary>
        internal int ExitCounter(string place, int offerId, int step)
        {
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            ExitOffer o = e != null ? e.OfferOf(offerId) : null;
            FirmPlace firm = FirmOf(place);
            if (o == null || firm == null || Save == null || o.Countered)
            {
                return -1;
            }
            int result = ExitRules.Counter(_exits, o, step, TechBridges.SkillNow(TalentCatalogue.Selling), TechBridges.SkillNow(TalentCatalogue.Analysis),
                TechBridges.SkillNow(TalentCatalogue.Relationships), Save.Day);
            ExitBridges.Practised?.Invoke(TalentCatalogue.Selling, 1);
            ExitBridges.Practised?.Invoke(TalentCatalogue.Analysis, 1);
            if (result == ExitRules.CounterWalked)
            {
                e.Offers.Remove(o);
            }
            Logger?.Info("Tech: a counter to " + o.Name + "'s offer for " + firm.File.Name + " (" + ExitCatalogue.CounterSteps[TalentCatalogue.Clamp(step, 0, 2)] + " thousandths more): "
                         + (result == ExitRules.CounterRaised ? "met, the price is " + TechText.Cents(Level(o.Price)) : result == ExitRules.CounterHeld
                             ? "the buyer holds its price" : "the buyer walks away") + ".");
            ChangedTech();
            return result;
        }

        // ---------------------------------------------------------------- the way to the market

        /// <summary>The banks the player may choose from, the largest first; one default when the market names none.</summary>
        internal IReadOnlyList<string> ExitBanks()
        {
            _bankNames.Clear();
            ExitBridges.Banks?.Invoke(_bankNames);
            if (_bankNames.Count == 0)
            {
                _bankNames.Add(TechText.T("reallife_exit_bank_default"));
            }
            return _bankNames;
        }

        /// <summary>The bankers engaged: the retainer is paid by the firm, and the firm's worth is taken from its books.</summary>
        internal bool ExitBankers(string place, int index)
        {
            FirmPlace firm = FirmOf(place);
            FirmExit e = _exits != null ? _exits.Exit(place, true) : null;
            if (firm == null || e == null || Save == null || firm.Reg == null)
            {
                return false;
            }
            IReadOnlyList<string> banks = ExitBanks();
            int bank = TalentCatalogue.Clamp(index, 0, banks.Count - 1);
            long mrr = FirmsRules.Arr(_firmsFile, place) / 12;
            FirmValue(firm, out long value, out _);
            if (!ExitRules.ListReady(mrr, e) || value <= 0)
            {
                Notifications.ShowError("reallife_exit_refuse_not_ready");
                return false;
            }
            long fee = ExitRules.Retainer(value);
            if (!VentureGame.Pay(Level(fee), VentureGame.BankersKey, firm.File.Name, firm.Reg))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            ExitRules.Engage(e, mrr, bank, banks[bank], bank, value, Save.Day);
            TellTech(NotificationType.Info, "reallife_exit_note_bankers", Data("firm", firm.File.Name, "bank", banks[bank], "fee", TechText.Cents(Level(fee))), TabFunding, ExitRow + place);
            ChangedTech();
            return true;
        }

        /// <summary>The roadshow held (at the desk or from the page): the demand is set by the market's mood, the player's Selling and Relationships
        /// and the bank's reach; the book is priced some days on.</summary>
        internal bool ExitRoadshow(string place, int approach)
        {
            FirmPlace firm = FirmOf(place);
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            if (firm == null || e == null || Save == null || e.Stage != ListStage.Bankers)
            {
                return false;
            }
            int selling = TechBridges.SkillNow(TalentCatalogue.Selling), rel = TechBridges.SkillNow(TalentCatalogue.Relationships);
            if (!ExitRules.Hold(e, VentureRules.Mood(HiringGame.Cycle), selling, rel, approach, Save.Day))
            {
                return false;
            }
            ExitBridges.Practised?.Invoke(TalentCatalogue.Selling, 2);
            ExitBridges.Practised?.Invoke(TalentCatalogue.Relationships, 2);
            Logger?.Info("Tech: the roadshow of " + firm.File.Name + " held on day " + Save.Day + " (way " + e.Choice + ", demand " + e.Demand + "): the range "
                + TechText.Cents(Level(e.Low)) + " to "
                         + TechText.Cents(Level(e.High)) + ", priced on day " + e.PriceDay + ".");
            ChangedTech();
            return true;
        }

        internal void ExitWithdraw(string place)
        {
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            if (e != null && ExitRules.Withdraw(e))
            {
                ChangedTech();
            }
        }

        // The book priced: the firm lists. The new shares are the public's, in the cap table; what the offer raised after the bank reaches the firm as money raised (the
        // wallet must keep it, as for any round), and the player's shares are locked up.
        private void ListFirm(FirmPlace firm, FirmExit e, int day)
        {
            string place = firm.File.Place;
            Cap c = CapFor(place, true);
            if (c == null)
            {
                e.Stage = ListStage.None;
                return;
            }
            long net = ExitRules.List(c, e, day);
            VentureGame.Give(Level(net), VentureGame.RaiseKey, firm.File.Name, firm.Reg);
            c.Acc.Name = firm.File.Name;
            RefreshFloor(day);
            Story("reallife_tech_news_listing", "name", firm.File.Name, "amount", TechText.Cents(Level(c.Post)));
            TellTech(NotificationType.Success, "reallife_exit_note_listed", Data("firm", firm.File.Name, "price", TechText.Cents(Level(e.Price)), "amount", TechText.Cents(Level(net)),
                "days", ExitCatalogue.LockUpDays.ToString(CultureInfo.InvariantCulture)), TabFunding, ExitRow + place);
            Logger?.Info("Tech: " + firm.File.Name + " listed on day " + day + " at " + TechText.Cents(Level(e.Price)) + " before the money (bank " + e.BankName
                + "): the public bought " + ExitCatalogue.FloatBp
                         + " bp for " + TechText.Cents(Level(e.Gross)) + ", " + TechText.Cents(Level(net)) + " reached the firm; the player's shares are locked up to day " + e.LockTo + ".");
            ChangedTech();
        }

        /// <summary>The days the player's shares stay locked up after a listing; 0 when they are free.</summary>
        internal int ExitLockLeft(string place)
        {
            FirmExit e = _exits != null && Save != null ? _exits.Exit(place, false) : null;
            return e != null && ExitRules.Locked(e, Save.Day) ? e.LockTo - Save.Day : 0;
        }

        // ---------------------------------------------------------------- the player's own shares, sold later

        /// <summary>How many of the player's shares the round's investors (or the public) may buy now, what a share fetches and the discount; 0 when none.</summary>
        internal long ExitRoom(string place, out long cents, out int discount)
        {
            cents = 0;
            discount = 0;
            Cap c = _ventureOn ? _venture.CapOf(place) : null;
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            if (c == null || e == null || Save == null)
            {
                return 0;
            }
            discount = ExitRules.Discount(TechBridges.SkillNow(TalentCatalogue.Selling), TechBridges.SkillNow(TalentCatalogue.Relationships), e.Listed);
            long room = ExitRules.Room(c, e, Save.Day, Year);
            cents = room > 0 ? ExitRules.SharePrice(c, room, discount) : 0;
            return room;
        }

        /// <summary>The most of the player's vested shares the window allows is sold, at the round's price less the discount. The money is the
        /// player's own and the firm's account is not touched.</summary>
        internal bool ExitSell(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            if (firm == null || c == null || e == null || Save == null)
            {
                return false;
            }
            int day = Save.Day;
            if (ExitRules.Locked(e, day))
            {
                Notifications.Show(NotificationType.Warning, "reallife_exit_note_locked", Data("firm", firm.File.Name, "days",
                    (e.LockTo - day).ToString(CultureInfo.InvariantCulture)), 8f, null, null, true, false);
                return false;
            }
            long room = ExitRoom(place, out _, out int discount);
            if (room <= 0)
            {
                Notifications.Show(NotificationType.Info, "reallife_exit_note_no_room", Data("firm", firm.File.Name), 8f, null, null, true, false);
                return false;
            }
            long held = ExitRules.PlayerShares(c);
            long paid = ExitRules.Sell(c, e, room, discount, LeadWho(c), day, Year);
            if (paid <= 0)
            {
                return false;
            }
            long floor = _floorNow;
            VentureGame.Give(Level(paid), VentureGame.ShareSaleKey, firm.File.Name);   // the player's own shares: no address, no part of the firm's account
            long gain = ExitGains.ForShares(_gains, c, place, firm.File.Name, paid, held, room, day, Prices.Level);
            GainNotice(firm.File.Name, gain);
            ExitBridges.Practised?.Invoke(TalentCatalogue.Selling, 1);
            RefreshFloor(day);
            Logger?.Info("Tech: the player sold " + room + " share(s) of " + firm.File.Name + " for " + TechText.Cents(Level(paid)) + " (" + discount
                + " thousandths under the price); the floor stayed at "
                         + TechText.Cents(floor) + " and is " + TechText.Cents(_floorNow) + GainLog(gain) + ".");
            ChangedTech();
            return true;
        }

        private static int LeadWho(Cap c)
        {
            for (int i = c.Holders.Count - 1; i >= 0; i--)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc)
                {
                    return c.Holders[i].Who;
                }
            }
            return 0;
        }

        // ---------------------------------------------------------------- the desk sessions

        /// <summary>Why a session of exits cannot be held at a firm now (a key), or null: 0 an offer waits, 1 the bankers are engaged and the roadshow is next.</summary>
        internal string ExitBlocked(string place, int which)
        {
            FirmExit e = _ventureOn && _exits != null ? _exits.Exit(place, false) : null;
            if (e == null || FirmOf(place) == null)
            {
                return "reallife_tech_desk_exit_why_none";
            }
            return which == 0 ? (e.Offers.Count > 0 ? null : "reallife_tech_desk_exit_why_none") : (e.Stage == ListStage.Bankers ? null : "reallife_tech_desk_exit_why_none");
        }

        /// <summary>A session held: the offer's choices are accept, counter (a fifth more) and decline; the roadshow's are the three ways to hold it.</summary>
        internal bool ExitSession(string place, string name, int which, int choice, out string key, out string[] pairs)
        {
            pairs = new[] { "firm", name ?? string.Empty };
            key = "reallife_tech_desk_exit_why_none";
            FirmExit e = _exits != null ? _exits.Exit(place, false) : null;
            if (e == null || Save == null)
            {
                return false;
            }
            if (which == 1)
            {
                if (!ExitRoadshow(place, choice))
                {
                    return false;
                }
                key = "reallife_tech_desk_exit_road_held";
                pairs = new[] { "firm", name ?? string.Empty, "day", e.PriceDay.ToString(CultureInfo.InvariantCulture), "low", TechText.Cents(Level(e.Low)), "high",
                    TechText.Cents(Level(e.High)) };
                return true;
            }
            ExitOffer o = e.Offers.Count > 0 ? e.Offers[0] : null;
            if (o == null)
            {
                return false;
            }
            string buyer = o.Name;
            if (choice == 0)
            {
                if (!ExitAccept(place, o.Id))
                {
                    key = "reallife_tech_desk_exit_why_blocked";
                    return false;
                }
                key = "reallife_tech_desk_exit_offer_accepted";
            }
            else if (choice == 1)
            {
                int result = ExitCounter(place, o.Id, 1);
                if (result < 0)
                {
                    key = "reallife_tech_desk_exit_why_countered";
                    return false;
                }
                key = result == ExitRules.CounterRaised ? "reallife_tech_desk_exit_offer_met" : result == ExitRules.CounterHeld ? "reallife_tech_desk_exit_offer_held"
                    : "reallife_tech_desk_exit_offer_walked";
            }
            else
            {
                ExitDecline(place, o.Id);
                key = "reallife_tech_desk_exit_offer_declined";
            }
            pairs = new[] { "firm", name ?? string.Empty, "buyer", buyer, "amount", TechText.Cents(Level(o.Price)) };
            return true;
        }
    }
}
