using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The terms a firm's sale can be accepted on (Tech/Venture/SaleTermRules): part in the buyer's shares, a slice of the firm kept, an earn-out, or staying as the boss on a contract.
    /// Chosen on the exits page and shown there with every figure before the game's confirm box. The deals are saved in a line of their own (SaleTermStore) and read by the day: one
    /// comparison for a deal with no month to run, a few bank lines on a pay day. Under the Startups option, like the exits they belong to. Nothing per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private TermFile _terms;
        private readonly List<TermEvent> _termEvents = new List<TermEvent>(8);

        /// <summary>The deals of the terms of sales; null while the venture part is off.</summary>
        internal TermFile Terms => _ventureOn ? _terms : null;

        private void LoadSaleTerms()
        {
            _terms = SaleTermStore.Load(Save.modData, out _) ?? new TermFile();
            if (_terms.Seed == 0)
            {
                _terms.Seed = Math.Max(1, _exits != null ? _exits.Seed : 1);
            }
        }

        private void WriteSaleTerms(Dictionary<string, string> data)
        {
            string line = _terms != null ? SaleTermStore.Write(_terms) : null;
            if (line != null)
            {
                data[SaleTermStore.Key] = line;
            }
            else
            {
                data.Remove(SaleTermStore.Key);
            }
        }

        /// <summary>
        /// True from the day of a sale with a boss contract until the business has actually left the player: while the contract runs, after it ended (resigned, dismissed, at its
        /// term) and while the hand-over is waiting on something. Its owner is the buyer all that time, so nothing is offered, listed, raised or sold for it.
        /// Read from the save when the venture part is
        /// off, so switching the option off does not free a firm a contract holds.
        /// </summary>
        internal bool Contracted(string place)
        {
            TermFile f = TermsKnown();
            return f != null && f.BossOf(place) != null;
        }

        private TermFile _coldTerms;
        private string _coldLine;

        private TermFile TermsKnown()
        {
            if (_ventureOn && _terms != null)
            {
                return _terms;
            }
            string line = null;
            if (Save == null || Save.modData == null || !Save.modData.TryGetValue(SaleTermStore.Key, out line) || line == null)
            {
                return null;
            }
            if (!ReferenceEquals(line, _coldLine))
            {
                _coldTerms = SaleTermStore.Load(Save.modData, out _);   // only when a business is asked about with the venture part off: rare, and kept until the line changes
                _coldLine = line;
            }
            return _coldTerms;
        }

        /// <summary>The part of a firm the player owns, thousandths: none from the day of a sale with a boss contract (it is the buyer's), else what the cap table says
        /// (all of it with no investors).</summary>
        internal int OwnShare(string place)
        {
            if (ContractOf(place) != null)
            {
                return 0;
            }
            Cap c = _ventureOn ? _venture.CapOf(place) : null;
            return c != null ? VentureRules.PlayerShare(c) : 1000;
        }

        /// <summary>Refuses a thing a contract's firm may not do (list, raise, be sold again): says why in a notice and returns true when a contract still holds the place.</summary>
        internal bool ContractRefused(string place, string firm)
        {
            TermDeal d = ContractOf(place);
            if (d == null)
            {
                return false;
            }
            Notifications.Show(NotificationType.Warning, "reallife_terms_why_contract", Data("firm", string.IsNullOrEmpty(firm) ? d.Firm : firm, "buyer", d.Buyer), 8f, null, null, true, false);
            return true;
        }

        /// <summary>The contract that holds a business, or null.</summary>
        internal TermDeal ContractOf(string place) => _ventureOn && _terms != null ? _terms.BossOf(place) : null;

        /// <summary>A firm's figures for the written targets: its month of takings (what a listing counts as its month), and its paying customers.</summary>
        internal void TermFigures(FirmPlace firm, out long takings, out long customers)
        {
            takings = firm != null ? Math.Max(firm.File.Revenue30, FirmsRules.Arr(_firmsFile, firm.File.Place) / 12) : 0;
            customers = 0;
            for (int i = 0; firm != null && _firmsFile != null && i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                customers += p.Place == firm.File.Place && p.Live ? Math.Max(0L, p.Users) : 0;
            }
        }

        /// <summary>
        /// What a choice of terms comes to for an offer now, with the sale it belongs to: the pot, the investors' lines and the player's own part, which is what a held-back sum
        /// or a kept stake comes out of. Asked by the pages for every figure they show, and by the sale itself.
        /// </summary>
        internal TermPlan TermPlanFor(string place, ExitOffer o, TermChoice choice, out SaleResult sale)
        {
            sale = ExitPreview(place, o);
            if (o == null || o.Hire || _terms == null)
            {
                choice = default(TermChoice);
            }
            FirmPlace firm = FirmOf(place);
            TermFigures(firm, out long takings, out long customers);
            int stock = o != null && o.Buyer == 0 ? o.BuyerId : 0;
            int cents = 0;
            if (stock > 0 && (choice.Kind == TermKind.Shares || choice.Kind == TermKind.Boss))
            {
                cents = ShareQuote(stock);
            }
            return SaleTermRules.Plan(choice, o != null ? o.Price : 0, sale.Player, takings, customers, stock, cents, Prices.Level, _terms != null ? _terms.OpenCount() : 0);
        }

        /// <summary>The market's price of a listed buyer's stock in real cents, 0 when it cannot say (the market is off, the stock is gone, the player is a guest).</summary>
        private static int ShareQuote(int stock)
        {
            try
            {
                return ExitBridges.ShareQuote != null ? Math.Max(0, ExitBridges.ShareQuote(stock)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Whether the terms offered for this offer include shares of the buyer: only a listed company of the market whose stock can be bought now.</summary>
        internal bool TermsHaveShares(ExitOffer o) => o != null && !o.Hire && o.Buyer == 0 && ShareQuote(o.BuyerId) > 0;

        // ---------------------------------------------------------------- the sale on these terms

        // What the accepted terms do once the money is in: the deal is written down, the shares are put in the player's portfolio under their lock, and the notice says it all once.
        private void TermsBegin(TermPlan plan, TermChoice choice, ExitOffer o, string place, string name, int day, long mrr, long customers, int level)
        {
            TermDeal d = SaleTermRules.Begin(_terms, plan, choice, place, name, o.Name, o.BuyerId, o.Buyer, day, mrr, customers);
            if (plan.Kind == TermKind.Shares && !GrantShares(plan.Stock, plan.Shares, plan.ShareCents, d.LockTo))
            {
                // The market could not take them after all: the player is paid what they were worth, and the deal is closed with nothing standing.
                VentureGame.Give(ExitGains.Level(plan.Deferred, level), VentureGame.ExitKey, name);
                d.State = TermState.Done;
                Logger?.Warn("Tech: the shares of " + o.Name + " could not be put in the portfolio; their worth was paid in cash.");
                return;
            }
            string key = plan.Kind == TermKind.Shares ? "reallife_terms_note_shares" : plan.Kind == TermKind.Slice ? "reallife_terms_note_slice"
                : plan.Kind == TermKind.Earn ? "reallife_terms_note_earn" : "reallife_terms_note_boss";
            Notifications.Show(NotificationType.Success, key, Data("firm", name, "buyer", o.Name, "now", TechText.Cents(Level(plan.Now)), "held", TechText.Cents(Level(plan.Deferred)),
                "shares", TechText.Whole(plan.Shares), "stake", TechText.Mille(plan.Mille), "days", plan.LockDays.ToString(CultureInfo.InvariantCulture),
                    "years", plan.Years.ToString(CultureInfo.InvariantCulture), "day", (plan.Kind == TermKind.Boss || plan.Kind == TermKind.Earn ? d.Due : d.EndDay)
                        .ToString(CultureInfo.InvariantCulture)), 14f, null, null, true, false);
            Logger?.Info("Tech: " + name + " sold to " + o.Name + " on terms (kind " + plan.Kind + "), deal " + d.Id + ": paid at once " + TechText.Cents(Level(plan.Now)) + " of "
                + TechText.Cents(Level(plan.Price)) + ", " + TechText.Cents(Level(plan.Deferred))
                         + " held back or kept; the deal can pay at most " + TechText.Cents(Level(plan.Most)) + " more.");
        }

        private static bool GrantShares(int stock, long shares, int cents, int lockTo)
        {
            try
            {
                return ExitBridges.ShareGrant != null && shares > 0 && ExitBridges.ShareGrant(stock, shares, cents, lockTo);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- the day

        // Every deal with something to do: its months stepped, what they pay paid, one notice for each thing that ends or is judged.
        private void TermsDay(int day)
        {
            if (_terms == null)
            {
                return;
            }
            for (int i = 0; i < _terms.Deals.Count; i++)
            {
                TermDeal d = _terms.Deals[i];
                if (!d.Live)
                {
                    continue;
                }
                TermWorld w = TermWorldOf(d);
                FirmPlace firm = d.Kind == TermKind.Boss ? FirmOf(d.Place) : null;
                d.Absent = d.Kind == TermKind.Boss && firm == null ? d.Absent + 1 : 0;
                _termEvents.Clear();
                SaleTermRules.Day(_terms, d, day, w, _termEvents);
                for (int k = 0; k < _termEvents.Count; k++)
                {
                    TermDo(_termEvents[k], firm, w, day);
                }
                if (d.Kind == TermKind.Boss && !d.Open && !d.Handed)
                {
                    if (_mendTold.Remove(d))
                    {
                        Notifications.Show(NotificationType.Warning, "reallife_terms_note_mended", Data("firm", d.Firm, "buyer", d.Buyer), 14f, null, null, true, false);
                    }
                    TermHand(d, firm);
                }
            }
            if (_feeBack > 0)
            {
                Notifications.Show(NotificationType.Info, "reallife_terms_note_fee_back", Data("amount", TechText.Cents(_feeBack)), 14f, null, null, true, false);
                _feeBack = 0;
            }
        }

        /// <summary>What the day sees round a deal: a contract's firm with its real figures, and the market's price of the buyer's shares for the vesting. Nothing for the rest.</summary>
        internal TermWorld TermWorldOf(TermDeal d)
        {
            var w = new TermWorld();
            FirmPlace firm = d.Kind == TermKind.Boss && d.Live ? FirmOf(d.Place) : null;
            if (firm != null)
            {
                w.FirmHere = true;
                TermFigures(firm, out w.Takings, out w.Customers);
            }
            else if (d.Kind == TermKind.Boss && d.Live && d.Absent < SaleTermCatalogue.AbsentMost)
            {
                w.FirmHere = true;   // not found today, which a poor read of the firms can do: the contract waits, with the figures of the sale, before it is called gone
                w.Takings = d.Mrr0;
                w.Customers = d.Cust0;
            }
            if (d.Kind == TermKind.Boss && d.Shares > 0 && d.Open)
            {
                w.StockCents = ShareQuote(d.Stock);
                w.StockLive = w.StockCents > 0;
            }
            return w;
        }

        private void TermDo(in TermEvent e, FirmPlace firm, in TermWorld w, int day)
        {
            TermDeal d = e.Deal;
            string name = d.Firm;
            long cents = Level(e.Cents);
            switch (e.Kind)
            {
                case TermEventKind.SliceDividend:
                    VentureGame.Give(cents, VentureGame.TermDividendKey, name);
                    WithholdDividend(cents, name);
                    break;
                case TermEventKind.SliceBought:
                    VentureGame.Give(cents, VentureGame.TermBuyoutKey, name);
                    Notifications.Show(NotificationType.Success, "reallife_terms_note_slice_end", Data("firm", name, "buyer", d.Buyer, "amount", TechText.Cents(cents)), 12f, null, null, true, false);
                    break;
                case TermEventKind.EarnPaid:
                    VentureGame.Give(cents, VentureGame.TermEarnKey, name);
                    Notifications.Show(NotificationType.Success, "reallife_terms_note_earn_paid", Data("firm", name, "buyer", d.Buyer, "amount", TechText.Cents(cents)), 12f, null, null, true, false);
                    break;
                case TermEventKind.EarnLapsed:
                    Notifications.Show(NotificationType.Warning, "reallife_terms_note_earn_lapsed",
                        Data("firm", name, "buyer", d.Buyer, "target", SaleTermText.TargetWords(this, d)), 12f, null, null, true, false);
                    break;
                case TermEventKind.SharesFree:
                    Notifications.Show(NotificationType.Info, "reallife_terms_note_shares_free", Data("firm", name, "shares", TechText.Whole(e.Shares)), 10f, null, null, true, false);
                    break;
                case TermEventKind.Salary:
                    VentureGame.Give(cents, VentureGame.TermSalaryKey, name);
                    break;
                case TermEventKind.VestShares:
                    if (GrantShares(d.Stock, e.Shares, w.StockCents, 0))
                    {
                        Notifications.Show(NotificationType.Success, "reallife_terms_note_vest_shares", Data("firm", name, "buyer", d.Buyer, "shares", TechText.Whole(e.Shares),
                            "year", e.Count.ToString(CultureInfo.InvariantCulture)), 10f, null, null, true, false);
                    }
                    else
                    {
                        VentureGame.Give(cents, VentureGame.TermBonusKey, name);   // the market could not take them: their worth at the grant's price is paid as the bonus
                        d.Paid += e.Cents;
                    }
                    break;
                case TermEventKind.VestCash:
                    VentureGame.Give(cents, VentureGame.TermBonusKey, name);
                    Notifications.Show(NotificationType.Success, "reallife_terms_note_vest_cash", Data("firm", name, "buyer", d.Buyer, "amount", TechText.Cents(cents),
                        "year", e.Count.ToString(CultureInfo.InvariantCulture)), 10f, null, null, true, false);
                    break;
                case TermEventKind.Strike:
                    Notifications.Show(NotificationType.Warning, "reallife_terms_note_strike", Data("firm", name, "buyer", d.Buyer, "takings", TechText.Cents(Level(e.Cents)),
                        "least", TechText.Cents(Level(d.Mrr0 * SaleTermCatalogue.KeepMille / 1000))), 12f, null, null, true, false);
                    break;
                case TermEventKind.Dismissed:
                    Notifications.Show(NotificationType.Error, "reallife_terms_note_dismissed", Data("firm", name, "buyer", d.Buyer), 12f, null, null, true, false);
                    break;
                case TermEventKind.Ended:
                    Notifications.Show(NotificationType.Info, d.State == TermState.Resigned ? "reallife_terms_note_resigned" : "reallife_terms_note_ended",
                        Data("firm", name, "buyer", d.Buyer), 12f, null, null, true, false);
                    break;
                case TermEventKind.Gone:
                    Notifications.Show(NotificationType.Warning, "reallife_terms_note_gone", Data("firm", name, "buyer", d.Buyer), 12f, null, null, true, false);
                    break;
                case TermEventKind.Remit:
                    TermRemit(d, firm, day);
                    break;
            }
            ExitGains.Note(_gains, day, name, SaleTermRules.Taxing(d, e).Gain);   // a buyout and a prize over what was held back are capital gains, noted once
            if (Verbose && e.Kind != TermEventKind.Remit)
            {
                Logger?.Info("Tech check (sale terms, day " + day + "): deal " + d.Id + " " + e.Kind + " " + TechText.Cents(cents) + ", paid so far " + TechText.Cents(Level(d.Paid)) + " of at most "
                    + TechText.Cents(Level(d.Most)) + ": " + (SaleTermRules.WithinRules(d) ? "PASS" : "FAIL: the deal paid more than its rules"));
            }
            ChangedTech();
        }

        // A contract's firm is the owner's: each month its profit goes to the buyer, and the buyer carries a loss up to a part of the sale's month of takings while the contract runs and
        // the whole of it for the days after the contract ended, until the business is handed over. The player's wallet is where the money is, so the line is the settlement and nothing
        // else of the firm's accounts changes.
        private void TermRemit(TermDeal d, FirmPlace firm, int day)
        {
            int days = Math.Min(day - d.Synced, SaleTermCatalogue.MonthDays);
            if (firm == null || firm.Reg == null || days <= 0)
            {
                return;
            }
            int since = d.Synced;
            d.Synced = day;
            long sum = FirmProfitSum(firm, days);
            SaleTermRules.Hold(_terms, since + 1, day, sum / 100);   // its owner's profit is out of the player's taxable takings
            if (sum > 0)
            {
                // deductible by the game's own tax, which counts the firm's profit in the player's business income until it is paid on
                VentureGame.PayDeductible(sum, VentureGame.TermRemitKey, d.Firm, TechText.T("reallife_terms_deduct"));
            }
            else if (sum < 0)
            {
                long cover = SaleTermRules.Cover(sum, days, SaleTermRules.GapDays(d, since, day), Level(d.Mrr0 * SaleTermCatalogue.CoverMille / 1000));
                VentureGame.Give(cover, VentureGame.TermCoverKey, d.Firm);
            }
        }

        // The tax withheld from a stake's dividend, as the stock market's dividends are: its own rate, moved by the council's business tax, as a line of its own.
        private void WithholdDividend(long cents, string name)
        {
            long tax;
            try
            {
                tax = ExitBridges.DividendTax != null ? ExitBridges.DividendTax(cents / 100) * 100 : 0L;
            }
            catch (Exception)
            {
                tax = 0L;
            }
            VentureGame.Pay(Math.Min(tax, cents), VentureGame.TermDividendTaxKey, name, null, true);
        }

        /// <summary>The profit of contract firms that was their owners', whole money, between two days; read from the save when the venture part is
        /// off, so switching it off lifts nothing out of a quarter.</summary>
        internal int HeldBetween(int fromDay, int toDay)
        {
            TermFile f = _terms ?? (Save != null ? SaleTermStore.Load(Save.modData, out _) : null);
            return (int)Math.Max(-int.MaxValue / 2, Math.Min(int.MaxValue / 2, SaleTermRules.Held(f, fromDay, toDay)));
        }

        // A contract that has ended gives its business to the buyer as the sale does: whenever the game allows, tried on each day until it does. The days up to the hand-over are
        // settled with the buyer first, while the firm can still be read, and what stands in the way is named to the player (once, and again when it changes or a week has passed).
        private readonly Dictionary<int, KeyValuePair<string, int>> _handNote = new Dictionary<int, KeyValuePair<string, int>>(4);
        private readonly HashSet<TermDeal> _mendTold = new HashSet<TermDeal>();
        private long _feeBack;

        private void TermHand(TermDeal d, FirmPlace firm)
        {
            if (firm == null || firm.Reg == null)
            {
                d.Handed = d.Absent >= SaleTermCatalogue.AbsentMost;   // gone from the player's hands for good, not merely missing from a poor read of the firms
                return;
            }
            int day = Save != null ? Save.Day : d.Seen;
            string why = ExitBridges.WhyNotHand(d.Place);
            if (why == null && (ExitBridges.HandOver == null || ExitBridges.HandOverReady == null || !ExitBridges.HandOverReady(d.Place)))
            {
                why = SaleTermRules.WhyBoss;
            }
            if (why == null)
            {
                TermRemit(d, firm, day);   // the buyer's profit or loss up to today is settled before the firm leaves
                if (!ExitBridges.HandOver(d.Place, d.Buyer))
                {
                    why = ExitBridges.GenericRefusal;
                }
                else if (ExitBridges.StillHeld != null && ExitBridges.StillHeld(d.Place))
                {
                    why = ExitBridges.GenericRefusal;   // said done, and the business is still the player's: never taken as handed over
                    Logger?.Warn("Tech: the hand-over of " + d.Firm + " said it was done but the business is still the player's; it is tried again tomorrow.");
                }
            }
            if (why != null)
            {
                TellHandWait(d, why, day);
                return;
            }
            d.Handed = true;
            _handNote.Remove(d.Id);
            FirmExit e = _exits != null ? _exits.Exit(d.Place, false) : null;
            if (e != null)
            {
                e.Offers.Clear();
            }
            RefreshFloor(Save.Day);
            Notifications.Show(NotificationType.Info, "reallife_terms_note_handed", Data("firm", d.Firm, "buyer", d.Buyer), 12f, null, null, true, false);
            Logger?.Info("Tech: " + d.Firm + " was handed to " + d.Buyer + " when the contract of deal " + d.Id + " ended (state " + d.State + ").");
            ChangedTech();
        }

        // The hand-over that cannot be made yet: told when the cause first shows, when it changes, and once a week while it lasts. Never every day.
        private void TellHandWait(TermDeal d, string why, int day)
        {
            string wait = SaleTermRules.WaitKey(why);
            if (_handNote.TryGetValue(d.Id, out KeyValuePair<string, int> told) && told.Key == wait && day - told.Value < 7)
            {
                return;
            }
            _handNote[d.Id] = new KeyValuePair<string, int>(wait, day);
            Notifications.Show(NotificationType.Warning, "reallife_terms_note_hand_wait", Data("firm", d.Firm, "buyer", d.Buyer, "why", TechText.T(wait)), 14f, null, null, true, false);
            Logger?.Info("Tech: " + d.Firm + " cannot be handed to " + d.Buyer + " on day " + day + " (" + why + "); it is tried again each day.");
        }

        // ---------------------------------------------------------------- a save that had it wrong (1.0.19)

        /// <summary>
        /// Once in a save, at the first load: a contract marked handed over while the business never left the player (the hand-over once took a contract's own hold for "not yours any more")
        /// is opened again so the hand-over is made, and bankers' retainers paid for a firm a contract held (a listing that could never happen) are paid back, with one notice for each.
        /// Nothing is taken back from what a player was paid.
        /// </summary>
        private void MendContracts()
        {
            if (_terms == null || Save == null || _exits == null)
            {
                return;
            }
            if ((_terms.Mended & TermMend.Handed) == 0)
            {
                _terms.Mended |= TermMend.Handed;
                for (int i = 0; i < _terms.Deals.Count; i++)
                {
                    TermDeal d = _terms.Deals[i];
                    FirmPlace firm = d.Kind == TermKind.Boss && !d.Open && d.Handed && d.State != TermState.Gone ? FirmOf(d.Place) : null;
                    if (firm != null && firm.Reg != null && firm.File.Founded <= d.Day && _terms.BossOf(d.Place) == null && ExitBridges.StillHeld != null && ExitBridges.StillHeld(d.Place))
                    {
                        d.Handed = false;
                        d.Absent = 0;
                        d.Synced = Save.Day;   // the days before were borne by the player and are not settled after the fact
                        _mendTold.Add(d);
                        Logger?.Info("Tech: the contract of deal " + d.Id + " for " + d.Firm + " was marked handed over, but the business is still the player's; it is handed to " + d.Buyer + " now.");
                    }
                }
            }
            if ((_terms.Mended & TermMend.Fees) == 0)
            {
                _terms.Mended |= TermMend.Fees;
                for (int i = 0; i < _exits.Firms.Count; i++)
                {
                    FirmExit e = _exits.Firms[i];
                    TermDeal d = LastBossDeal(e.Place);
                    bool onTheWay = e.OnTheWay && d != null && d.Live;
                    bool failed = d != null && e.Fee > 0 && e.Price > 0 && e.PriceDay > d.Day && (e.Stage == ListStage.None || e.Stage == ListStage.CalledOff);
                    if (e.Fee <= 0 || (!onTheWay && !failed))
                    {
                        continue;
                    }
                    FirmPlace firm = FirmOf(e.Place);
                    _feeBack += GiveFeeBack(firm != null ? firm.File.Name : d.Firm, firm != null ? firm.Reg : null, e);
                }
            }
        }

        private TermDeal LastBossDeal(string place)
        {
            TermDeal last = null;
            for (int i = 0; _terms != null && i < _terms.Deals.Count; i++)
            {
                if (_terms.Deals[i].Kind == TermKind.Boss && _terms.Deals[i].Place == place)
                {
                    last = _terms.Deals[i];
                }
            }
            return last;
        }

        /// <summary>The bankers' retainer of a way to the market that did not happen is given back, once: the way is closed and the fee cleared.
        /// Returns the cents given back, at today's prices.</summary>
        private long GiveFeeBack(string name, BuildingRegistration reg, FirmExit e)
        {
            long cents = Level(e.Fee);
            VentureGame.Give(cents, VentureGame.BankersKey, name, reg);   // the same line as the retainer, the other way, so the firm's books net it out
            e.Fee = 0;
            e.Stage = ListStage.None;
            e.Demand = e.Choice = 0;
            e.PriceDay = 0;
            e.Price = 0;
            Logger?.Info("Tech: the bankers' retainer of " + TechText.Cents(cents) + " for " + name + " was given back: its listing could not happen.");
            ChangedTech();
            return cents;
        }

        /// <summary>The player resigns a contract: the pay stops, years not yet vested and an earn-out not yet judged are lost, and the business passes to the buyer.</summary>
        internal bool TermResign(int id)
        {
            TermDeal d = _terms != null ? _terms.Of(id) : null;
            if (d == null || Save == null)
            {
                return false;
            }
            _termEvents.Clear();
            if (!SaleTermRules.Resign(d, _termEvents, Save.Day))
            {
                return false;
            }
            FirmPlace firm = FirmOf(d.Place);
            var w = new TermWorld { FirmHere = firm != null && firm.Reg != null };
            for (int k = 0; k < _termEvents.Count; k++)
            {
                TermDo(_termEvents[k], firm, w, Save.Day);
            }
            TermHand(d, firm);
            return true;
        }

        // ---------------------------------------------------------------- the test tool

        /// <summary>Every deal standing is moved a year on, as though the year had passed, and the day's pass is run: months paid, vesting years delivered, earn-outs judged.</summary>
        public void TestTermsYear()
        {
            if (!TestVentures() || _terms == null || Save == null)
            {
                return;
            }
            bool any = false;
            for (int i = 0; i < _terms.Deals.Count; i++)
            {
                TermDeal d = _terms.Deals[i];
                if (!d.Live)
                {
                    continue;
                }
                any = true;
                d.Day -= SaleTermCatalogue.YearDays;
                d.Seen -= SaleTermCatalogue.YearDays;
                d.Synced -= SaleTermCatalogue.YearDays;
                d.EndDay -= d.EndDay > 0 ? SaleTermCatalogue.YearDays : 0;
                d.Due -= d.Due > 0 ? SaleTermCatalogue.YearDays : 0;
                d.LockTo -= d.LockTo > 0 ? SaleTermCatalogue.YearDays : 0;
                d.Review -= d.Review > 0 ? SaleTermCatalogue.YearDays : 0;
            }
            if (!any)
            {
                Notifications.ShowError("reallife_terms_test_none");
                return;
            }
            TermsDay(Save.Day);
        }
    }
}
