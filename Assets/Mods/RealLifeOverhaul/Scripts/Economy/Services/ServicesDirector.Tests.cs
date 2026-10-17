using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The options page's test buttons: each does at once what otherwise takes days and opens the app on it. Their money is
    /// real, but no test sells anything by itself.
    /// </summary>
    public sealed partial class ServicesDirector
    {
        private bool TestReady(string test, bool cleaning)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !_running || _broken || !(cleaning ? CleaningOn : SaleOn))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        private void TestDone(string what, int tab, string pick)
        {
            Logger?.Warn("Services test: " + what + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_svc_test_done", null, 7f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
            Open(tab, pick);
        }

        private void TestGuard(string test, bool cleaning, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                if (cleaning)
                {
                    CleaningFault("a test (" + test + ")", e);
                }
                else
                {
                    SaleFault("a test (" + test + ")", e);
                }
            }
        }

        /// <summary>"Test: a cleaning round": every business on the list is due now, whatever its hours or floor; the round goes to Player.log visit by visit.</summary>
        public void TestRound()
        {
            if (!TestReady("Cleaning round", true))
            {
                return;
            }
            TestGuard("round", true, () =>
            {
                ReadAll();
                if (_state.Crew.Count == 0)
                {
                    TestRefusals.TellKey(Logger, "Cleaning round", "reallife_svc_test_no_crew");
                    return;
                }
                if (_state.Clients.Count == 0)
                {
                    TestRefusals.TellKey(Logger, "Cleaning round", "reallife_svc_test_no_clients");
                    return;
                }
                // For this one planning every client is served as needed, under a threshold nothing is above.
                var mode = new int[_state.Clients.Count];
                var threshold = new int[_state.Clients.Count];
                for (int i = 0; i < _state.Clients.Count; i++)
                {
                    mode[i] = _state.Clients[i].Mode;
                    threshold[i] = _state.Clients[i].Threshold;
                    _state.Clients[i].Mode = ServicesCatalogue.ModeAsNeeded;
                    _state.Clients[i].Threshold = 101;
                }
                try
                {
                    Replan("the test asked for a round");
                }
                finally
                {
                    for (int i = 0; i < _state.Clients.Count; i++)
                    {
                        _state.Clients[i].Mode = mode[i];
                        _state.Clients[i].Threshold = threshold[i];
                    }
                }
                int now = ServicesGame.Now, planned = 0;
                string list = string.Empty;
                for (int i = 0; i < _visits.Count; i++)
                {
                    DayVisit v = _visits[i];
                    if (v.End > now)
                    {
                        CleaningClient c = _state.Client(v.Place);
                        list += (planned > 0 ? "; " : string.Empty) + v.CleanerName + " to '" + (c != null ? c.Name : v.Place) + "' " + Clock(v.Leave) + " out, " + Clock(v.Start) + " to "
                                + Clock(v.End) + " (" + v.Metres + " m)";
                        planned++;
                    }
                }
                if (planned == 0)
                {
                    TestRefusals.TellKey(Logger, "Cleaning round", "reallife_svc_test_no_round", _shifts.Count + " shift(s) today, " + _jobs.Count + " visit(s) due: " + LeftOver());
                    return;
                }
                TestDone("a round from " + Clock(now) + " with every business due: " + planned + " visit(s): " + list + ". Each is cleaned hour by hour as its time comes", TabCleaning, null);
            });
        }

        /// <summary>"Test: a cleaning contract offer": a business of somebody else's asks now.</summary>
        public void TestContractOffer()
        {
            if (!TestReady("Contract offer", true))
            {
                return;
            }
            TestGuard("contract offer", true, () =>
            {
                if (_state.Offers.Count >= ServicesCatalogue.OffersMost)
                {
                    TestRefusals.TellKey(Logger, "Contract offer", "reallife_svc_test_offers_full");
                    return;
                }
                ServicesGame.Candidates(_candidates);
                int found = _candidates.Count, was = _state.LastOfferDay;
                CleaningOffer offer = ContractRules.Draw(_state, _candidates, Save.Day, ServicesGame.Index);
                _state.LastOfferDay = was;   // the test does not move the day the next real one is looked for
                _candidates.Clear();
                if (offer == null)
                {
                    TestRefusals.TellKey(Logger, "Contract offer", "reallife_svc_test_no_candidates", found + " business(es) of others that the game would clean");
                    return;
                }
                TestDone("'" + offer.Name + "' asks for a contract: " + offer.Fee + " a week, a floor kept at " + offer.Standard + ", " + offer.Sqm + " m2, open until day " + offer.Until
                         + " (of " + found + " businesses that could ask)", TabContracts, "offer:" + offer.Place);
            });
        }

        /// <summary>"Test: a contract's week": the first contract's week ends now, and the fee is really paid or the penalty really charged.</summary>
        public void TestContractWeek()
        {
            if (!TestReady("Contract week", true))
            {
                return;
            }
            TestGuard("contract week", true, () =>
            {
                CleaningClient c = null;
                for (int i = 0; i < _state.Clients.Count && c == null; i++)
                {
                    c = _state.Clients[i].Outside ? _state.Clients[i] : null;
                }
                if (c == null)
                {
                    TestRefusals.TellKey(Logger, "Contract week", "reallife_svc_test_no_contract");
                    return;
                }
                int day = Save.Day, under = c.DaysUnder;
                c.WeekFrom = day - ServicesCatalogue.ContractWeekDays;
                ContractWeek week = ContractRules.Week(c, day, out int fee, out int penalty);
                int slot = ((day % 7) + 7) % 7;
                if (week == ContractWeek.Paid)
                {
                    Pay(fee, ServicesGame.FeeKey, c.Name, null, null, "the test ended the week: " + under + " morning(s) under the standard");
                    _state.FeeIncome[slot] += fee;
                }
                else
                {
                    Charge(penalty, ServicesGame.PenaltyKey, c.Name, null, null, false, true, "the test ended the week: " + under + " morning(s) under the standard");
                    _state.FeeIncome[slot] -= penalty;
                    if (week == ContractWeek.Lost)
                    {
                        _state.Clients.Remove(c);
                        Replan("a contract was lost");
                    }
                }
                TestDone("the week of '" + c.Name + "' ended now with " + under + " morning(s) under its standard of " + c.Standard + ": " + week + ", fee " + fee + ", penalty " + penalty
                         + " (the money is real)", TabContracts, week == ContractWeek.Lost ? null : "con:" + c.Place);
            });
        }

        // The player's business that is worth most; null if they run none that takes money.
        private BuildingRegistration BestBusiness(out SaleBreakdown best)
        {
            best = default(SaleBreakdown);
            BuildingRegistration chosen = null;
            ServicesGame.Sellable(_regs);
            for (int i = 0; i < _regs.Count; i++)
            {
                SaleBreakdown b = Value(_regs[i]);
                if (chosen == null || b.Value > best.Value)
                {
                    chosen = _regs[i];
                    best = b;
                }
            }
            _regs.Clear();
            return chosen;
        }

        /// <summary>"Test: an offer for my best business": the business worth most goes on the market if it is not, and an offer comes now.</summary>
        public void TestSaleOffer()
        {
            if (!TestReady("Sale offer", false))
            {
                return;
            }
            TestGuard("sale offer", false, () =>
            {
                BuildingRegistration reg = BestBusiness(out SaleBreakdown b);
                if (reg == null)
                {
                    TestRefusals.TellKey(Logger, "Sale offer", "reallife_svc_test_no_business");
                    return;
                }
                if (b.Value <= 0)
                {
                    TestRefusals.TellKey(Logger, "Sale offer", "reallife_svc_refuse_worthless");
                    return;
                }
                string place = ServicesGame.Place(reg);
                SaleListing l = _state.Listing(place) ?? SaleRules.List(_state, place, ServicesStore.Clean(ServicesGame.Name(reg)), b.Value, ServicesCatalogue.AskingDefault, Save.Day);
                if (l == null || _state.SaleOffers.Count >= ServicesCatalogue.OffersKept)
                {
                    TestRefusals.TellKey(Logger, "Sale offer", "reallife_svc_refuse_listings_full");
                    return;
                }
                int made = l.OffersMade, next = l.NextOfferDay;
                SaleOffer offer = null;
                for (int tries = 0; offer == null && tries < 4; tries++)
                {
                    offer = MakeOffer(l, Save.Day);   // at the highest asking price a turn may bring nobody: the next turn is tried
                }
                l.OffersMade = made + (offer != null ? 1 : 0);   // the test uses one of the listing's turns, and does not move the day of the next
                l.NextOfferDay = next;
                if (offer == null)
                {
                    TestRefusals.TellKey(Logger, "Sale offer", "reallife_svc_test_no_buyer");
                    return;
                }
                TestDone("an offer for '" + l.Name + "' (worth " + b.Value + ", asking " + l.Asking + "): " + (BuyerKind)offer.Buyer + " offers " + offer.Price + ", until day "
                         + offer.Deadline + ". Accepting it is a real sale, after the game's question", TabSale, "bid:" + offer.Id);
            });
        }

        /// <summary>"Test: a sale on paper": a quick sale of the business worth most, worked out step by step into Player.log; nothing is changed.</summary>
        public void TestSalePaper()
        {
            if (!TestReady("Sale on paper", false))
            {
                return;
            }
            TestGuard("sale on paper", false, () =>
            {
                BuildingRegistration reg = BestBusiness(out SaleBreakdown b);
                if (reg == null)
                {
                    TestRefusals.TellKey(Logger, "Sale on paper", "reallife_svc_test_no_business");
                    return;
                }
                string place = ServicesGame.Place(reg), name = ServicesGame.Name(reg);
                int price = SaleRules.Quick(b.Value), cash = ServicesGame.Cash, staff = ServicesGame.StaffAt(reg), items = reg.itemInstances != null ? reg.itemInstances.Count : 0;
                SaleMoney m = SaleRules.Settle(price, 0, b, cash);
                SaleBlock block = ServicesGame.Blocked(reg);
                string buyer = ServicesGame.BuyerFor(reg);
                bool sums = b.Value == b.Goodwill + b.Fixtures && b.Net == b.Value - b.Investors - b.Loans && m.Left == m.Now - m.Investors - m.Loans && m.Now == price;
                Logger?.Warn("Services test: a sale on paper of '" + name + "' (" + place + "). NOTHING IS CHANGED. Value: " + Ventures.VenturesCatalogue.OfferValueDays
                             + " days of a recent daily profit give " + b.Base + ", by type " + b.TypePct + "%, district " + b.DistrictPct + "%, the economy " + b.ClimatePct
                             + "%, days of books " + b.HistoryPct + "% = goodwill " + b.Goodwill + "; stock and furniture " + b.Fixtures + " (" + items
                             + " item(s) at the game's resale price); worth " + b.Value + ". A quick sale pays " + price + " at once. Out of it first: investors " + b.Investors
                             + ", business loans " + b.Loans + "; left " + m.Left
                             + (m.Short > 0 ? "; SHORT by " + m.Short + " even with the cash of " + cash + ": the sale would be refused" : string.Empty)
                             + ". The landlord's deposit of " + b.Deposit + " would come back beside it. Steps a real sale would take: 1 book " + m.Now
                             + "; 2 buy the investors out through Ventures; 3 pay the loans off; 4 " + staff
                             + " employee(s) stay with you unassigned (a quick sale keeps nobody), the game's own shut-down runs, " + items
                             + " item(s) leave the building, the lease ends; 5 the building "
                             + (reg.BuildingOwnedByPlayer ? "stays your property"
                                 : buyer != null ? "becomes '" + buyer + "', a business of the game's own" : "is free to rent (the game has no business of this kind for a building of this size)")
                             + "; 6 the capital gain of " + m.Gain + " is noted. In the way right now: " + (block == SaleBlock.None ? "nothing" : block.ToString()) + ".");
                Logger?.Info("Services check (a sale on paper, " + name + "): the value is its goodwill and its things, what is left is the price less investors and loans, "
                             + "and nothing of the game's was written: " + (sums ? "PASS" : "FAIL: the sums do not add up"));
                Changed();
                Notifications.Show(NotificationType.Info, "reallife_svc_test_paper", new Dictionary<string, string>(2) { { "business", name }, { "amount", ServicesText.Money(price) } }, 9f, null,
                    Guard.Wrap(() => Open(TabSale, "sell:" + place)), true, false);
                Open(TabSale, "sell:" + place);
            });
        }

        /// <summary>"Test: a part of a price falls due": the next part of the first price being paid in parts is paid now.</summary>
        public void TestPart()
        {
            if (!TestReady("Sale part", false))
            {
                return;
            }
            TestGuard("part", false, () =>
            {
                if (_state.Dues.Count == 0)
                {
                    TestRefusals.TellKey(Logger, "Sale part", "reallife_svc_test_no_parts");
                    return;
                }
                SaleDue d = _state.Dues[0];
                d.NextDay = Save.Day;
                int part = SaleRules.TakeDue(d, Save.Day);
                Pay(part, ServicesGame.PartKey, d.Name, null, null, "the test brought a part forward; " + d.Left + " still to come");
                string name = d.Name;
                int left = d.Left;
                if (d.Left <= 0)
                {
                    _state.Dues.RemoveAt(0);
                }
                TestDone("a part of " + part + " for '" + name + "' was paid now; " + left + " still to come (the money is real, and is not paid a second time)", TabSale, null);
            });
        }
    }
}
