using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the raw materials exchange and the player's futures and options. Once a second it
    /// follows the option and the city's economy, learns the game's raw materials once a session and, after a close, has the
    /// city's economy write the exchange's new prices as the importers'. It does a future's delivery when its day's close
    /// asks for one (after the frame's market work), tells settlements and calls, and answers the Futures tab. Nothing is
    /// sent in a Going Public session: the exchange comes in the host's market, and each player's contracts are their own.
    /// Nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private Action<DerivNote> _derivTold;
        private Func<DerivContract, string> _contractName;
        private readonly List<string> _materialNames = new List<string>(64);
        private readonly List<long> _materialBases = new List<long>(64);
        private readonly Dictionary<string, RawMaterial> _byName = new Dictionary<string, RawMaterial>(64);
        private readonly List<DerivNote> _deliveries = new List<DerivNote>(4);
        private readonly HashSet<string> _used = new HashSet<string>();
        private MarketState _byNameOf;
        private int _byNameCount = -1;
        private bool _materialsKnown, _derivsWere, _livedWere;
        private int _fixSeen = -1;
        private float _usedAt = -100f;

        // Every engine made, at a load or a host's market taken on, tells its contracts here and learns the materials.
        private void HookDerivs()
        {
            if (_derivTold == null)
            {
                _derivTold = DerivHeard;
                _contractName = DerivText.Name;
            }
            _engine.DerivTold = _derivTold;
            _engine.ContractName = _contractName;
            _materialsKnown = false;
            _fixSeen = -1;
            FollowDerivs();
            KnowMaterials();
        }

        private void FollowDerivs()
        {
            _engine.DerivsOn = Settings.MarketsDerivatives;
            Func<bool> city = MarketBridges.CityPrices;
            _engine.MaterialsLive = city != null && city();
            _engine.Deliveries = !GoingPublic.InSession;
        }

        // Once a second: the option and the city's economy followed, the materials learned, the importers' prices written
        // again after a close or when the option changed.
        private void DerivsSecond()
        {
            FollowDerivs();
            if (!_materialsKnown)
            {
                KnowMaterials();
            }
            List<RawMaterial> all = _engine.State.Materials;
            int fixDay = all.Count > 0 ? all[0].FixDay : 0;
            bool on = Settings.MarketsDerivatives, live = _engine.MaterialsLive;
            if (fixDay != _fixSeen || on != _derivsWere || live != _livedWere)
            {
                _fixSeen = fixDay;
                _derivsWere = on;
                _livedWere = live;
                try
                {
                    MarketBridges.MaterialsFixed?.Invoke();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        // The game's raw materials into the exchange, once a session: new ones join at their first price.
        private void KnowMaterials()
        {
            int found = MaterialGame.Find(_materialNames, _materialBases, MarketBridges.OwnWholesale);
            if (found == 0)
            {
                return;   // the game's recipes are not loaded yet: next second
            }
            _materialsKnown = true;
            int added = _engine.KnowMaterials(_materialNames, _materialBases, _following);
            if (added > 0 || Verbose)
            {
                Logger?.Info("Stock market: the raw materials exchange has " + _engine.State.Materials.Count + " of the game's raw materials (" + added + " new, " + found
                             + " in the game's recipes)" + (_following ? ", the host's prices followed." : "."));
            }
        }

        /// <summary>
        /// A raw material's price against its own for the city's economy to write as the importers' (Economy/City); NaN while
        /// the exchange does not price it: the market or the option off, or an item it does not know.
        /// </summary>
        public double MaterialFactor(string itemName)
        {
            if (!Running || !Settings.MarketsDerivatives || string.IsNullOrEmpty(itemName))
            {
                return double.NaN;
            }
            List<RawMaterial> all = _engine.State.Materials;
            if (!ReferenceEquals(_byNameOf, _engine.State) || _byNameCount != all.Count)
            {
                _byName.Clear();
                for (int i = 0; i < all.Count; i++)
                {
                    _byName[all[i].Name] = all[i];
                }
                _byNameOf = _engine.State;
                _byNameCount = all.Count;
            }
            return _byName.TryGetValue(itemName, out RawMaterial m) && m.Base > 0 ? m.Factor : double.NaN;
        }

        // ---------------------------------------------------------------- the Futures tab's questions

        private DerivWhy DerivReady()
        {
            return !Running ? DerivWhy.Closed : Behind ? DerivWhy.Behind : _engine.Held ? DerivWhy.Held : !Trading ? DerivWhy.Closed : DerivWhy.None;
        }

        public bool MaterialsLive => Running && _engine.MaterialsLive;

        public int ContractDays(int[] into)
        {
            return Running ? _engine.ContractDays(into) : 0;
        }

        public DerivQuote QuoteFuture(string material, int settleDay, bool buying, long qty)
        {
            return Running ? _engine.QuoteFuture(material, settleDay, buying, qty) : new DerivQuote { Why = DerivWhy.Closed };
        }

        public DerivQuote QuoteOption(bool call, string material, int stockId, int settleDay, int strikeStepBp, long qty)
        {
            return Running ? _engine.QuoteOption(call, material, stockId, settleDay, strikeStepBp, qty) : new DerivQuote { Why = DerivWhy.Closed };
        }

        /// <summary>Whether a contract as quoted can be bought now; None when it can.</summary>
        public DerivWhy CanBuy(ContractKind kind, string material, int stockId, int settleDay, long qty, in DerivQuote q)
        {
            DerivWhy why = DerivReady();
            return why != DerivWhy.None ? why : _engine.CanOpen(kind, material, stockId, settleDay, qty, q, Trading);
        }

        public DerivContract Buy(ContractKind kind, string material, int stockId, int settleDay, int strikeStepBp, long qty, string placeKey, string placeName, out DerivWhy why)
        {
            FinishMoment();
            why = DerivReady();
            if (why != DerivWhy.None)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(placeKey) && (kind != ContractKind.Long || GoingPublic.InSession))
            {
                placeKey = null;   // only a future bought is delivered, and never in a session
            }
            try
            {
                DerivContract c = _engine.OpenContract(kind, material, stockId, settleDay, strikeStepBp, qty, placeKey, placeName, Trading, false, out why);
                if (c != null)
                {
                    Logger?.Info("Stock market: bought " + DerivText.Name(c) + ": " + c.Qty + " at " + c.Price + (c.IsFuture ? " micros a unit, margin " + c.Margin / 100
                                 : " micros strike for a premium of " + c.Premium + " micros") + (c.Warehouse.Length > 0 ? ", to be delivered to " + c.PlaceName : string.Empty) + ".");
                }
                Moved();
                return c;
            }
            catch (Exception e)
            {
                Fault("buying a future or an option", e);
                return null;
            }
        }

        /// <summary>A contract sold or bought back now; what it brought, false with the reason when it cannot be.</summary>
        public bool CloseContract(int id, out long money, out DerivWhy why)
        {
            FinishMoment();
            money = 0;
            why = DerivReady();
            if (why != DerivWhy.None)
            {
                return false;
            }
            try
            {
                DerivContract c = _engine.State.Derivs.Contract(id);
                bool done = _engine.CloseContract(id, Trading, out money, out why);
                if (done)
                {
                    Logger?.Info("Stock market: closed " + DerivText.Name(c) + " at " + c.EndPrice + " micros a unit; it made " + c.Result / 100 + " in all.");
                }
                Moved();
                return done;
            }
            catch (Exception e)
            {
                Fault("closing a future or an option", e);
                return false;
            }
        }

        public bool PayCall(out long paid, out DerivWhy why)
        {
            FinishMoment();
            paid = 0;
            if (!Running)
            {
                why = DerivWhy.Closed;
                return false;
            }
            bool done = _engine.PayCall(out paid, out why);
            if (done)
            {
                Logger?.Info("Stock market: " + paid + " paid into the futures' margin account to meet its call.");
            }
            Moved();
            return done;
        }

        public long ClosePrice(DerivContract c)
        {
            return Running && c != null ? _engine.ClosePrice(c) : 0;
        }

        public long DerivWorth()
        {
            return Running ? _engine.DerivWorth() : 0;
        }

        /// <summary>The player's warehouses and factories a future bought may be delivered to; none in a Going Public session.</summary>
        public void Places(List<string> keys, List<string> names)
        {
            keys.Clear();
            names.Clear();
            if (Running && !GoingPublic.InSession)
            {
                MaterialGame.Places(keys, names);
            }
        }

        /// <summary>Whether the player's factories use a material now; read again at most once a few seconds, when a page is written.</summary>
        public bool Uses(string material)
        {
            if (UnityEngine.Time.realtimeSinceStartup - _usedAt > 5f)
            {
                _usedAt = UnityEngine.Time.realtimeSinceStartup;
                MaterialGame.Used(_used);
            }
            return _used.Contains(material);
        }

        // ---------------------------------------------------------------- what contracts did

        private void DerivHeard(DerivNote n)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null)
            {
                return;
            }
            DerivContract c = n.Contract > 0 ? s.Derivs.Contract(n.Contract) : null;
            string name = c != null ? DerivText.Name(c) : string.Empty;
            if (Verbose)
            {
                Logger?.Info("Stock market check (futures and options, day " + s.Day + "): " + n.Kind + (c != null ? " " + name + ", " + c.Qty + " at " + c.Price : string.Empty) + ", A " + n.A
                             + ", B " + n.B + ", price " + n.Price + "; the margin account " + s.Derivs.Balance / 100 + " for margins of " + DerivRules.Needed(s.Derivs) / 100 + ": "
                             + (s.Derivs.Balance >= 0 || n.Kind == DerivNoteKind.Shortfall ? "PASS" : "the account is under nothing with nothing forced"));
            }
            if (c != null && c.Test)
            {
                return;   // the test tools say what they did in the log
            }
            switch (n.Kind)
            {
                case DerivNoteKind.Deliver:
                    _deliveries.Add(n);   // done after the frame's market work (DeliverDue)
                    break;
                case DerivNoteKind.Settled:
                    if (c != null && c.End != ContractEnd.Delivered)
                    {
                        Queue(n.A >= 0 ? NotificationType.Success : NotificationType.Info, "reallife_mkt_drv_note_settled", "name", name, "price", DerivText.Unit(n.Price), "result",
                            DerivText.Signed(n.A));
                    }
                    break;
                case DerivNoteKind.Exercised:
                    Queue(NotificationType.Success, "reallife_mkt_drv_note_exercised", "name", name, "price", DerivText.Unit(n.Price), "amount", MarketText.Money(n.A));
                    break;
                case DerivNoteKind.Lapsed:
                    Queue(NotificationType.Info, "reallife_mkt_drv_note_lapsed", "name", name, "price", DerivText.Unit(n.Price));
                    break;
                case DerivNoteKind.Gone:
                    Queue(NotificationType.Info, "reallife_mkt_drv_note_gone", "name", name, "amount", MarketText.Money(n.A));
                    break;
                case DerivNoteKind.Called:
                    Queue(NotificationType.Warning, "reallife_mkt_drv_note_call", "amount", MarketText.Money(n.A), "day", n.B.ToString());
                    break;
                case DerivNoteKind.Forced:
                    Queue(NotificationType.Warning, "reallife_mkt_drv_note_forced", "name", name, "result", DerivText.Signed(n.A));
                    break;
                case DerivNoteKind.Shortfall:
                    Queue(NotificationType.Error, "reallife_mkt_drv_note_shortfall", "amount", MarketText.Money(n.A));
                    break;
            }
        }

        // A future's goods put on its place's pallets and charged to it; what did not fit, or could not be paid for, was
        // settled in cash already (its margin's money is settled to the day's price).
        private void DeliverDue()
        {
            for (int i = 0; i < _deliveries.Count; i++)
            {
                DerivNote n = _deliveries[i];
                DerivContract c = _engine != null ? _engine.State.Derivs.Contract(n.Contract) : null;
                string name = c != null ? DerivText.Name(c) : MaterialGame.Name(n.Material);
                try
                {
                    long delivered = MaterialGame.Deliver(n.Warehouse, n.Material, n.Qty, n.Price, out long charged, out string place);
                    if (c != null)
                    {
                        c.Delivered = delivered;
                        c.End = delivered > 0 ? ContractEnd.Delivered : ContractEnd.Settled;
                    }
                    Logger?.Info("Stock market: " + name + ": " + delivered + " of " + n.Qty + " delivered to " + (place.Length > 0 ? place : "nowhere (the place is gone)") + " for " + charged
                                 + "; the rest settled in cash.");
                    if (delivered >= n.Qty)
                    {
                        Queue(NotificationType.Success, "reallife_mkt_drv_note_delivered", "count", delivered.ToString("N0"), "material", MaterialGame.Name(n.Material), "place", place,
                            "price", DerivText.Unit(n.Price), "amount", MarketText.Money(charged));
                    }
                    else
                    {
                        Queue(NotificationType.Warning, "reallife_mkt_drv_note_delivered_part", "count", delivered.ToString("N0"), "of", n.Qty.ToString("N0"), "material",
                            MaterialGame.Name(n.Material), "place", place.Length > 0 ? place : DerivText.Place(c));
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Logger?.Warn("Stock market: " + name + " could not be delivered; it is settled in cash.");
                }
            }
            _deliveries.Clear();
        }

        // Leaving a save: every contract at its fair price, the margin account paid out.
        private long DerivsSettled(StringBuilder log)
        {
            return _engine.LeaveDerivs(log);
        }

        // ---------------------------------------------------------------- the test buttons

        /// <summary>Test button: the Stocks app opened on its Futures tab.</summary>
        public void TestFutures()
        {
            FinishMoment();
            const string Test = "Open the Futures tab";
            if (!TestReady(Test, false))
            {
                return;
            }
            if (_view == null)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_test_no_app");
                return;
            }
            _view.Want(MarketApp.TabFutures);
            OpenAppPartial();
            Logger?.Info(Test + " test: the app is open on its Futures tab (" + State.Materials.Count + " raw material(s), " + DerivRules.OpenCount(State.Derivs) + " contract(s) open).");
        }

        /// <summary>Test button: every open contract settles now as at its day, at today's prices.</summary>
        public void TestSettle()
        {
            FinishMoment();
            const string Test = "My futures and options settle now";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (DerivRules.OpenCount(State.Derivs) == 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_drv_test_none");
                return;
            }
            int n = _engine.SettleContractsNow();
            Logger?.Info(Test + " test: " + n + " contract(s) settled now at today's prices.");
            Moved();
        }

        /// <summary>Test button: the materials of the player's futures move a tenth against them, then the margin is looked at.</summary>
        public void TestCall()
        {
            FinishMoment();
            const string Test = "My futures' materials move against me";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_drv_test_session");   // the exchange is everyone's in a session
                return;
            }
            int moved = _engine.MoveAgainst(DerivCatalogue.TestMovePct / 100.0);
            if (moved == 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_drv_test_no_future");
                return;
            }
            DerivBook b = State.Derivs;
            Logger?.Info(Test + " test: " + moved + " material(s) moved " + DerivCatalogue.TestMovePct + "% against the futures; the margin account " + b.Balance / 100 + " for margins of "
                         + DerivRules.Needed(b) / 100 + (b.CallDay > 0 ? ", a call of " + b.CallAmount + " due by day " + b.CallDay : ", no call") + ".");
            Moved();
        }
    }
}
