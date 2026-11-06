using System;
using System.Collections.Generic;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Each kind of a manager's decision and how it is undone. A decision does what the player's own answer or screen
    /// does, keeps what was there before in its six figures, and is undone only while that is still true. The figures:
    ///   raises      A raise added, B standing moved, C the day asked, D the pay asked, E the rest before, F the answer
    ///   offers      A raise added, B standing moved, C the day it came, D the offer, E the rest before, F 1 matched
    ///   overtime    A 1 agreed, B its cost a day, C the day offered, D the rest before, E of the budget
    ///   claims      A the claim, B 1 counted in the term, C its payout, D 1 put in, E what it adds to the next term
    ///   stock       A the delivery's day, B products changed; the detail holds them, units before and after
    ///   prices      A products changed; the detail holds them, cents before and after
    ///   rota        A shifts written (-1 a week missed, -2 one the planner could not write), B hours covered, C wanted;
    ///               or A -3 the week kept as it was because a new one would take B of the staff off it, -4 because it would
    ///               cover only B hours of the C wanted against D now (ManagerRules.Rota)
    ///   hiring      A the wage, B the skill, C of the budget
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private const int UndoCan = 0, UndoNotNow = 1, UndoNever = 2;

        private readonly List<ItemChange> _changes = new List<ItemChange>(8), _items = new List<ItemChange>(8);
        private readonly Dictionary<string, int> _lastSet = new Dictionary<string, int>(16, StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lines = new Dictionary<string, int>(16, StringComparer.Ordinal);
        private readonly List<DeliveryContract> _contracts = new List<DeliveryContract>(4);
        private readonly HashSet<string> _rounds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<EmployeeInstance> _candidates = new List<EmployeeInstance>(4);
        private readonly Dictionary<string, string> _rotaBy = new Dictionary<string, string>(4, StringComparer.Ordinal);   // who asked for a rota being worked out, by place
        private int[] _hireWage = new int[8], _hireGoing = new int[8], _hireSkill = new int[8];
        private int[] _rotaHoursNow = new int[16], _rotaHoursAfter = new int[16];   // each worker's hours in the week now and in the new one (ManagerRules.Rota)

        private void DecideRaises(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!On(CompanyPart.Staff) || mp.Shop == null)
            {
                return;
            }
            ManagerFile m = mp.Own;
            List<StaffFile> staff = _state.Staff;
            int band = Rule(mp, PolicyField.PayBand), most = Rule(mp, PolicyField.RaiseMost), every = Rule(mp, PolicyField.RaiseEvery);
            for (int i = 0; i < staff.Count; i++)
            {
                StaffFile f = staff[i];
                if (f.AskDay == 0 || f.Place != mp.Place.Place || f.Id == m.Id)
                {
                    continue;   // a manager's own request is the player's to answer
                }
                // The policy's ceiling: the band, the largest raise and the days between two (PolicyRules); at its start, the band alone.
                int ceiling = PolicyRules.RaiseDue(LastRaise(mp, f.Id), day, every) ? PolicyRules.RaiseCeiling(f.Paid, f.Going, band, most) : f.Paid, roll;
                RaiseAnswer fair = ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ceiling, false);
                if (fair != RaiseAnswer.Agree && ToApprover(mp, f, false, ceiling, day))
                {
                    continue;   // past what the manager may agree: left open for whoever the policy names to approve it
                }
                bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Raises, f.Id, out roll);
                RaiseAnswer answer = ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ceiling, slip);
                int askDay = f.AskDay, askTo = f.AskTo, rest = f.AskRest, standing = f.Standing, added = StaffRules.RaiseFor(f, answer);
                StaffRules.Answer(f, answer, day);
                Decision d = Record(mp, ManagerTask.Raises, f.Id, who, day, hour, answer != fair);
                d.A = added;
                d.B = f.Standing - standing;
                d.C = askDay;
                d.D = askTo;
                d.E = rest;
                d.F = (int)answer;
                d.Detail = ManagerStore.Word(f.Name);
                Logger?.Info("Company: " + who + " (manager, skill " + skill + ") answered " + f.Name + "'s request for " + askTo + " an hour (hundredths): " + answer
                             + "; the ceiling is " + ceiling + " (" + ManagerCatalogue.Step(ManagerCatalogue.WageBands, band) + " thousandths of the going rate "
                             + f.Going + ")" + (answer != fair ? ", a mistake: " + fair + " was the answer" : string.Empty) + ".");
            }
            Recount();
        }

        private void DecideOffers(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!On(CompanyPart.Staff) || mp.Shop == null)
            {
                return;
            }
            ManagerFile m = mp.Own;
            List<StaffFile> staff = _state.Staff;
            int band = Rule(mp, PolicyField.PayBand), most = Rule(mp, PolicyField.RaiseMost);
            for (int i = 0; i < staff.Count; i++)
            {
                StaffFile f = staff[i];
                if (f.PoachDay == 0 || f.Place != mp.Place.Place || f.Id == m.Id)
                {
                    continue;
                }
                CompanyGame.Offers(f);
                if (StaffRules.GivesWay(f))
                {
                    GaveWay(f, day);   // the game's own offer decides, not the company's, nor its manager
                    continue;
                }
                int ceiling = PolicyRules.RaiseCeiling(f.Paid, f.Going, band, most), roll;
                bool fair = ManagerRules.Match(f.PoachOffer, ceiling, false);
                if (!fair && ToApprover(mp, f, true, ceiling, day))
                {
                    continue;   // past the band: left open for whoever the policy names to approve it
                }
                bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Offers, f.Id, out roll);
                bool match = ManagerRules.Match(f.PoachOffer, ceiling, slip);
                int came = f.PoachDay, offer = f.PoachOffer, rest = f.PoachRest, standing = f.Standing, raise = f.Raise;
                string rival = f.Rival;
                StaffRules.OfferAnswer(f, match, day);
                Decision d = Record(mp, ManagerTask.Offers, f.Id, who, day, hour, match != fair);
                d.A = f.Raise - raise;
                d.B = f.Standing - standing;
                d.C = came;
                d.D = offer;
                d.E = rest;
                d.F = match ? 1 : 0;
                d.Detail = ManagerStore.Word(f.Name) + "*" + ManagerStore.Word(rival);
                Logger?.Info("Company: " + who + " (manager, skill " + skill + ") " + (match ? "matched " : "did not match ") + rival + "'s offer of " + offer
                             + " an hour (hundredths) to " + f.Name + "; the ceiling is " + ceiling + (match != fair ? ", a mistake" : string.Empty)
                             + (match ? "." : ": they leave on day " + f.LeaveDay + "."));
            }
        }

        private void DecideOvertime(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!On(CompanyPart.Staff) || mp.Shop == null || mp.Shop.File.OvertimeAsk == 0)
            {
                return;
            }
            ManagerFile m = mp.Own;
            ShopFile file = mp.Shop.File;
            string place = file.Place;
            int cost = OvertimeCost(place), left = ManagerRules.Left(BudgetOf(m), m.Spent), roll;
            bool slip = Slips(skill, review, day, place, ManagerTask.Overtime, string.Empty, out roll);
            bool fair = ManagerRules.Overtime(cost, CompanyCatalogue.OvertimeDays, left, false) && file.StrikeFrom == 0;
            bool agree = ManagerRules.Overtime(cost, CompanyCatalogue.OvertimeDays, left, slip) && file.StrikeFrom == 0;
            int asked = file.OvertimeAsk, rest = file.OvertimeRest, spends = agree ? cost * CompanyCatalogue.OvertimeDays : 0;
            AnswerOvertime(place, agree);
            m.Spent += spends;
            Decision d = Record(mp, ManagerTask.Overtime, string.Empty, who, day, hour, agree != fair);
            d.A = agree ? 1 : 0;
            d.B = cost;
            d.C = asked;
            d.D = rest;
            d.E = spends;
            Logger?.Info("Company: " + who + " (manager, skill " + skill + ") " + (agree ? "agreed" : "refused") + " overtime at " + mp.Place.Name + ", " + cost
                         + " a day for " + CompanyCatalogue.OvertimeDays + " days, against " + left + " left of the month's budget" + (agree != fair ? ", a mistake" : string.Empty) + ".");
        }

        private void DecideClaims(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!On(CompanyPart.Cover) || mp.Shop == null)
            {
                return;
            }
            int termDays = CompanyCatalogue.TermDays(CompanyGame.DaysPerYear);
            for (int i = 0; i < _state.Claims.Count; i++)
            {
                Claim c = _state.Claims[i];
                string subject = c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (c.Stage != ClaimStage.Open || c.Place != mp.Place.Place || LastOf(ManagerTask.Claims, c.Place, subject) != null)
                {
                    continue;   // each claim is looked at once: one left is the player's to put in
                }
                Policy p = mp.Shop.File.Cover[CompanyCatalogue.Clamp(c.Kind, 0, CompanyCatalogue.CoverKinds - 1)];
                int premium = p.Active ? p.Premium : 0, roll, counted = p.Claims;
                int adds = (int)Math.Min(int.MaxValue / 4, (long)premium * CompanyCatalogue.ClaimLoading * termDays / 1000);
                bool slip = Slips(skill, review, day, c.Place, ManagerTask.Claims, subject, out roll);
                bool fair = ManagerRules.Claim(c.Payout, premium, termDays, false), put = ManagerRules.Claim(c.Payout, premium, termDays, slip);
                if (put)
                {
                    CoverRules.File(c, p, day);
                }
                Decision d = Record(mp, ManagerTask.Claims, subject, who, day, hour, put != fair);
                d.A = c.Id;
                d.B = p.Claims > counted ? 1 : 0;
                d.C = c.Payout;
                d.D = put ? 1 : 0;
                d.E = adds;
                d.Detail = ManagerStore.Word(CompanyText.Cover(c.Kind));
                Logger?.Info("Company: " + who + " (manager, skill " + skill + ") " + (put ? "put in" : "left for you") + " claim " + c.Id + " at " + c.Name + ": it pays "
                             + c.Payout + " and adds " + adds + " to the next term" + (put != fair ? ", a mistake" : string.Empty)
                             + (put ? ", paid on day " + c.PayDay : ", to put in by day " + (c.Day + CompanyCatalogue.ClaimWindowDays)) + ".");
            }
        }

        // The products' stock now, for the next look to count what was used since. Kept with the place's manager.
        private void SnapNow(ManagedPlace mp, int day, int hour)
        {
            ManagerFile m = mp.Own;
            if (m == null || mp.Place == null || mp.Place.Reg == null)
            {
                return;
            }
            ManagerGame.Contracts(mp.Place.Reg, _contracts);
            for (int i = 0; i < _contracts.Count; i++)
            {
                List<DeliveryContractItem> lines = _contracts[i].items;
                for (int k = 0; k < lines.Count; k++)
                {
                    DeliveryContractItem line = lines[k];
                    if (line == null || string.IsNullOrEmpty(line.itemName))
                    {
                        continue;
                    }
                    StockSnap snap = m.Snap(line.itemName);
                    if (snap == null && m.Snaps.Count < ManagerCatalogue.SnapsMost)
                    {
                        snap = new StockSnap { Item = line.itemName };
                        m.Snaps.Add(snap);
                    }
                    if (snap != null)
                    {
                        snap.Stock = ManagerGame.OnSite(mp.Place.Reg, line.itemName);
                        snap.Week = Math.Max(0, line.amountOrderedThisWeek);
                    }
                }
            }
            m.SnapDay = day;
            m.SnapHour = hour;
        }

        private void DecideStock(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            ManagerFile m = mp.Own;
            BuildingRegistration reg = mp.Place.Reg;
            ManagerGame.Contracts(reg, _contracts);
            ManagerGame.Rounds(reg, _rounds);
            _lines.Clear();
            for (int i = 0; i < _contracts.Count; i++)
            {
                for (int k = 0; k < _contracts[i].items.Count; k++)
                {
                    string name = _contracts[i].items[k] != null ? _contracts[i].items[k].itemName : null;
                    if (!string.IsNullOrEmpty(name))
                    {
                        _lines[name] = _lines.TryGetValue(name, out int n) ? n + 1 : 1;
                    }
                }
            }
            int toDelivery = ManagerRules.HoursToDelivery(day, hour), floor = ManagerCatalogue.Step(ManagerCatalogue.StockDays, m.StockDays);
            int hours = m.SnapDay > 0 ? (day - m.SnapDay) * 24 + hour - m.SnapHour : 0;
            int turns = m.SnapDay > 0 ? ManagerRules.Turns(m.SnapDay, m.SnapHour, day, hour) : 9;
            for (int i = 0; i < _contracts.Count; i++)
            {
                DeliveryContract c = _contracts[i];
                string wholesaler = ManagerGame.Wholesaler(c);
                Decision last = LastSet(ManagerTask.Stock, mp.Place.Place, wholesaler);
                bool change = ManagerGame.CanChange(c), mistake = false, overridden = false;
                _changes.Clear();
                for (int k = 0; k < c.items.Count; k++)
                {
                    DeliveryContractItem line = c.items[k];
                    string name = line != null ? line.itemName : null;
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }
                    int now = ManagerGame.OnSite(reg, name), use = -1;
                    StockSnap snap = m.Snap(name);
                    if (snap != null && m.SnapDay > 0)
                    {
                        use = ManagerRules.Use(snap.Stock, ManagerRules.Delivered(turns, snap.Week, line.amountOrderedLastWeek, line.amountOrderedThisWeek), now, hours);
                    }
                    if (!change || _rounds.Contains(name) || _lines[name] > 1 || m.Holds(name) || use < 0)
                    {
                        continue;   // locked, the game's logistics manager's, on two contracts, the player's, or not yet known
                    }
                    if (ManagerRules.Changed(_lastSet.TryGetValue(name, out int set), set, line.amount))
                    {
                        Hold(m, name);   // the player changed what the manager set: theirs from now on
                        overridden = true;
                        continue;
                    }
                    int roll;
                    bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Stock, name, out roll);
                    int most = ManagerGame.Most(line);
                    int fair = ManagerRules.Order(use, now, toDelivery, floor, most, 0);
                    int amount = ManagerRules.Order(use, now, toDelivery, floor, most, ManagerRules.Slip(skill, roll, slip));
                    if (_changes.Count < ManagerCatalogue.ItemsInDecision && ManagerRules.Moves(line.amount, amount, ManagerCatalogue.StockStep))
                    {
                        _changes.Add(new ItemChange { Item = name, Before = line.amount, After = amount });
                        mistake |= amount != fair;
                        if (Verbose)
                        {
                            Logger?.Info("Company check (managers, stock at " + mp.Place.Name + "): " + name + " " + now + " on site, " + use
                                         + " thousandths used a day over " + hours + " hours, " + toDelivery + " hours to the delivery, a floor of " + floor
                                         + " days, the most " + most + ": " + line.amount + " to " + amount + (amount != fair ? " (a mistake, " + fair + " was right)" : string.Empty) + ": PASS");
                        }
                    }
                }
                if (overridden && last != null && last.State != DecisionState.Undone)
                {
                    last.State = DecisionState.Overridden;
                }
                if (_changes.Count == 0)
                {
                    continue;
                }
                for (int k = 0; k < _changes.Count; k++)
                {
                    ManagerGame.SetAmount(ManagerGame.Line(c, _changes[k].Item), _changes[k].After);
                }
                if (last != null && last.Open)
                {
                    last.State = DecisionState.Closed;   // its week's order is the new one now
                }
                Decision d = Record(mp, ManagerTask.Stock, wholesaler, who, day, hour, mistake);
                d.A = c.nextDeliveryDay;
                d.B = _changes.Count;
                d.Detail = ManagerStore.Items(_changes);
                Logger?.Info("Company: " + who + " (manager, skill " + skill + ") set " + _changes.Count + " product(s) of the delivery to " + mp.Place.Name + " on day "
                             + c.nextDeliveryDay + " for a floor of " + floor + " day(s)" + (mistake ? ", with a mistake" : string.Empty) + ": " + d.Detail + ".");
            }
            for (int i = 0; i < _contracts.Count; i++)
            {
                for (int k = 0; k < _contracts[i].items.Count; k++)
                {
                    DeliveryContractItem line = _contracts[i].items[k];
                    StockSnap snap = line != null ? m.Snap(line.itemName) : null;
                    if (snap == null && line != null && !string.IsNullOrEmpty(line.itemName) && m.Snaps.Count < ManagerCatalogue.SnapsMost)
                    {
                        snap = new StockSnap { Item = line.itemName };
                        m.Snaps.Add(snap);
                    }
                    if (snap != null)
                    {
                        snap.Stock = ManagerGame.OnSite(reg, line.itemName);
                        snap.Week = Math.Max(0, line.amountOrderedThisWeek);
                    }
                }
            }
            m.SnapDay = day;
            m.SnapHour = hour;
        }

        private void DecidePrices(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            BuildingRegistration reg = mp.Place.Reg;
            List<RetailPrice> prices = reg.retailPrices;
            if (mp.Shop == null || prices == null || CompanyBridges.WritePrice == null || ManagerGame.PricingManagerCovers(mp.District)
                || CompanyBridges.PricesKeptAt(mp.Place.Place))
            {
                return;   // the game's pricing manager's, or a brand's, or nothing to write them with
            }
            ManagerFile m = mp.Own;
            Decision last = LastSet(ManagerTask.Prices, mp.Place.Place, string.Empty);
            bool mistake = false, overridden = false;
            _changes.Clear();
            for (int i = 0; i < prices.Count && _changes.Count < ManagerCatalogue.ItemsInDecision; i++)
            {
                RetailPrice rp = prices[i];
                string name = rp != null ? rp.itemName : null;
                if (string.IsNullOrEmpty(name) || m.Holds(name))
                {
                    continue;
                }
                int now = ManagerGame.Cents(rp.price);
                if (ManagerRules.Changed(_lastSet.TryGetValue(name, out int set), set, now))
                {
                    Hold(m, name);
                    overridden = true;
                    continue;
                }
                int market = ManagerGame.MarketCents(name, mp.District), roll;
                if (market <= 0)
                {
                    continue;
                }
                bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Prices, name, out roll);
                int fair = ManagerRules.Price(ManagerGame.Suggested(name, mp.District, skill, false), market, m.PriceLow, m.PriceHigh);
                int price = slip ? ManagerRules.Price(ManagerGame.Suggested(name, mp.District, skill, true), market, m.PriceLow, m.PriceHigh) : fair;
                if (price > 0 && ManagerRules.Moves(now, price, ManagerCatalogue.PriceStep))
                {
                    _changes.Add(new ItemChange { Item = name, Before = now, After = price });
                    mistake |= price != fair;
                }
            }
            if (overridden && last != null && last.State != DecisionState.Undone)
            {
                last.State = DecisionState.Overridden;
            }
            int written = 0;
            for (int k = 0; k < _changes.Count; k++)
            {
                written += CompanyBridges.WritePrice(reg, _changes[k].Item, _changes[k].After / 100f, mp.District) ? 1 : 0;
            }
            if (written == 0)
            {
                return;
            }
            Decision d = Record(mp, ManagerTask.Prices, string.Empty, who, day, hour, mistake);
            d.A = _changes.Count;
            d.Detail = ManagerStore.Items(_changes);
            Logger?.Info("Company: " + who + " (manager, skill " + skill + ") repriced " + _changes.Count + " product(s) at " + mp.Place.Name + " inside "
                         + ManagerCatalogue.Step(ManagerCatalogue.PriceLows, m.PriceLow) + " to " + ManagerCatalogue.Step(ManagerCatalogue.PriceHighs, m.PriceHigh)
                         + " thousandths of the market price" + (mistake ? ", with a mistake" : string.Empty) + " (cents): " + d.Detail + ".");
        }

        private void DecideRota(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!RotaOn || mp.Kind == ManagedKind.Warehouse)
            {
                return;
            }
            int roll;
            if (Slips(skill, review, day, mp.Place.Place, ManagerTask.Rota, string.Empty, out roll))
            {
                Decision missed = Record(mp, ManagerTask.Rota, string.Empty, who, day, hour, true);
                missed.A = -1;   // a week missed: the rota as it is stays
                Logger?.Info("Company: " + who + " (manager, skill " + skill + ") did not write the rota of " + mp.Place.Name + " this week, a mistake; the week as it is stays.");
                return;
            }
            // Worked out now; written only if the week has a gap and the new one is whole (ManagerRotaJudged).
            _rotaBy[mp.Place.Place] = who;
            RotaAsk(mp.Place.Place, JobManager, false);
        }

        /// <summary>Whether a place's manager still writes its rota: the managers on, a manager there with the weekly rota given, the place the player's.</summary>
        private bool ManagerRotaGiven(ManagedPlace mp)
        {
            return ManagersOn && _mgr != null && mp != null && mp.Own != null && ManagerRules.Runs(mp.Own.Tasks, mp.Kind, ManagerTask.Rota)
                   && mp.Place.Reg != null && GoingPublic.Mine(mp.Place.Reg);
        }

        /// <summary>
        /// A manager's week, worked out by the planner, just before it would be written (RotaJobs): true to write it. It is
        /// written only while the manager still has the weekly rota, when the week as it is has a gap, and when the new week
        /// is whole (ManagerRules.Rota): everybody on the rota stays on it and no hour covered now is left with nobody.
        /// Otherwise nothing of the schedule is touched; a gap the manager could not mend whole is a decision in their log.
        /// Cost: one pass over the two weeks' cells, once a week a place.
        /// </summary>
        private bool ManagerRotaJudged(string place, RotaSheet sheet)
        {
            ManagedPlace mp = ManagedAt(place);
            string who = _rotaBy.TryGetValue(place, out string by) ? by : mp != null && mp.Own != null ? mp.Own.Name : "?";
            if (!ManagerRotaGiven(mp))
            {
                _rotaBy.Remove(place);
                Logger?.Info("Company: the week's rota of " + ManagedName(place) + " was not written: the weekly rota is no longer the manager's, so its schedule is left alone.");
                return false;
            }
            int workers = sheet.Problem.Workers;
            if (_rotaHoursNow.Length < workers)
            {
                _rotaHoursNow = new int[workers * 2];
                _rotaHoursAfter = new int[workers * 2];
            }
            RotaPlan now = sheet.Now, fill = sheet.Ladder.Fill;
            RotaCall call = ManagerRules.Rota(now, fill, sheet.Problem, sheet.Fixed, _rotaHoursNow, _rotaHoursAfter, out int dropped);
            Logger?.Info("Company: " + who + " (manager of " + mp.Place.Name + ") looked at the week's rota: " + now.CoveredHours + " of " + now.WantedHours
                         + " wanted hours covered now, " + fill.CoveredHours + " with a new rota, which would take " + dropped + " of the staff off it: "
                         + (call == RotaCall.Write ? "written" : call == RotaCall.Whole ? "left as it is, it has no gap"
                             : call == RotaCall.NoBetter ? "left as it is, a new one mends nothing"
                             : call == RotaCall.LeavesOut ? "left as it is, a new one would take somebody off the rota"
                             : "left as it is, a new one would leave hours with nobody that have somebody now") + ".");
            if (call == RotaCall.Write)
            {
                return true;   // who asked stays for ManagerRotaWritten
            }
            _rotaBy.Remove(place);
            if (call == RotaCall.LeavesOut || call == RotaCall.CoversLess)
            {
                Decision d = Record(mp, ManagerTask.Rota, string.Empty, who, Save.Day, Save.Hour, false);
                d.A = call == RotaCall.LeavesOut ? -3 : -4;
                d.B = call == RotaCall.LeavesOut ? dropped : fill.CoveredHours;
                d.C = now.WantedHours;
                d.D = now.CoveredHours;
                d.State = DecisionState.Closed;   // nothing changed, nothing to undo
                Changed();
            }
            return false;
        }

        /// <summary>The weekly rota taken back from a place's manager: a week of theirs waiting to be worked out is dropped.</summary>
        private void ManagerRotaDropped(string place)
        {
            bool underWay = false;
            for (int i = _rotaJobs.Count - 1; i >= 0; i--)
            {
                if (_rotaJobs[i].Place != place || _rotaJobs[i].Kind != JobManager)
                {
                    continue;
                }
                if (i > 0 || _rotaStage == StageRead)
                {
                    _rotaJobs.RemoveAt(i);
                }
                else
                {
                    underWay = true;   // stopped by ManagerRotaJudged before it can write
                }
            }
            if (!underWay)
            {
                _rotaBy.Remove(place);
            }
        }

        // The planner has written a manager's rota (CompanyDirector.RotaFill): the decision, with the week kept to put back.
        private void ManagerRotaWritten(string place, int shifts, RotaPlan plan)
        {
            ManagedPlace mp = ManagedAt(place);
            if (mp == null || mp.Own == null || _mgr == null)
            {
                return;
            }
            string who = _rotaBy.TryGetValue(place, out string by) ? by : mp.Own.Name;
            _rotaBy.Remove(place);
            for (int i = 0; i < _mgr.Decisions.Count; i++)
            {
                Decision old = _mgr.Decisions[i];
                if (old.Open && old.Task == (int)ManagerTask.Rota && old.Place == place)
                {
                    old.State = DecisionState.Closed;   // the week kept to put back is the one before this rota (RotaWrite), not an older one
                }
            }
            Decision d = Record(mp, ManagerTask.Rota, string.Empty, who, Save.Day, Save.Hour, false);
            d.A = shifts;
            d.B = plan.CoveredHours;
            d.C = plan.WantedHours;
            Changed();
        }

        // The planner could not write it: said in the log by the planner, and in the manager's log.
        private void ManagerRotaUnwritten(string place)
        {
            ManagedPlace mp = ManagedAt(place);
            string who = _rotaBy.TryGetValue(place, out string by) ? by : mp != null && mp.Own != null ? mp.Own.Name : "?";
            _rotaBy.Remove(place);
            if (!ManagerRotaGiven(mp))
            {
                return;   // taken back meanwhile: not the manager's to log
            }
            Decision d = Record(mp, ManagerTask.Rota, string.Empty, who, Save.Day, Save.Hour, false);
            d.A = -2;
            d.State = DecisionState.Closed;
        }

        private void DecideHiring(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            ManagerFile m = mp.Own;
            if (m.Keep < 0)
            {
                m.Keep = mp.Place.Staff;
            }
            if (m.Keep - mp.Place.Staff <= 0)
            {
                return;
            }
            BuildingRegistration reg = mp.Place.Reg;
            ManagerGame.Candidates(reg, _candidates);
            int count = _candidates.Count;
            if (count == 0)
            {
                HireFromRegion(mp, who, day, hour);   // the policy's next source: the region's other boards
                return;
            }
            if (_hireWage.Length < count)
            {
                _hireWage = new int[count * 2];
                _hireGoing = new int[count * 2];
                _hireSkill = new int[count * 2];
            }
            int income = mp.Shop != null ? mp.Shop.Income : UnityEngine.Mathf.RoundToInt(CityEconomy.DistrictIncome(mp.District));
            for (int i = 0; i < count; i++)
            {
                _hireWage[i] = ManagerGame.WageNow(_candidates[i], _index);
                _hireGoing[i] = ManagerGame.Going(_candidates[i], _index, _cycle, income);
                _hireSkill[i] = ManagerGame.SkillOf(_candidates[i]);
            }
            int left = ManagerRules.Left(BudgetOf(m), m.Spent), roll;
            int band = Rule(mp, PolicyField.PayBand), cap = Rule(mp, PolicyField.HirePayCap), floor = Rule(mp, PolicyField.SkillFloor);
            bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Hiring, string.Empty, out roll);
            // The policy's pick: the band, the hiring cap and the skill floor; at the policy's start the same pick as ManagerRules.Pick.
            int fair = PolicyRules.Pick(_hireWage, _hireGoing, _hireSkill, count, band, cap, floor, left, false);
            int pick = PolicyRules.Pick(_hireWage, _hireGoing, _hireSkill, count, band, cap, floor, left, slip);
            if (pick < 0)
            {
                if (Verbose)
                {
                    Logger?.Info("Company check (managers, hiring at " + mp.Place.Name + "): " + count + " candidate(s) from the job board, none inside the wage band "
                                 + ManagerCatalogue.Step(ManagerCatalogue.WageBands, band) + ", the hiring cap " + PolicyCatalogue.Value(PolicyField.HirePayCap, cap)
                                 + ", the skill floor " + PolicyCatalogue.Value(PolicyField.SkillFloor, floor) + " and the " + left + " left of the budget: PASS");
                }
                HireFromRegion(mp, who, day, hour);
                return;
            }
            EmployeeInstance e = _candidates[pick];
            string name = ManagerGame.NameOf(e), id = e.id;
            if (!ManagerGame.Hire(e, reg))
            {
                return;
            }
            int cost = ManagerRules.HireCost(_hireWage[pick]);
            m.Spent += cost;
            mp.Place.Staff++;
            Decision d = Record(mp, ManagerTask.Hiring, id, who, day, hour, pick != fair);
            d.A = _hireWage[pick];
            d.B = _hireSkill[pick];
            d.C = cost;
            d.Detail = ManagerStore.Word(name);
            _candidates.Clear();
            Logger?.Info("Company: " + who + " (manager, skill " + skill + ") hired " + name + " (skill " + d.B + ", " + d.A + " an hour in hundredths) from the job board of "
                         + mp.Place.Name + " to keep " + m.Keep + " staff; " + cost + " of the month's budget" + (pick != fair ? ", a mistake: a better candidate was there" : string.Empty) + ".");
        }

        private Decision LastSet(ManagerTask task, string place, string subject)
        {
            return _mgr.LastSet(task, place, subject, _lastSet, _items);
        }

        private Decision LastOf(ManagerTask task, string place, string subject)
        {
            return _mgr.LastOf(task, place, subject);
        }

        // --- Undoing ---

        /// <summary>Whether a decision can be undone now (UndoCan), not now but perhaps later (UndoNotNow), or never again (UndoNever), with the words why.</summary>
        internal int UndoState(Decision d, int day, out string why)
        {
            why = null;
            if (!d.Open)
            {
                return UndoNever;
            }
            switch ((ManagerTask)d.Task)
            {
                case ManagerTask.Raises:
                {
                    StaffFile f = _state.Employee(d.Subject);
                    if (f == null || !On(CompanyPart.Staff))
                    {
                        return Never(out why, "reallife_mgr_undo_gone");
                    }
                    if (!ManagerRules.InTime(ManagerTask.Raises, d.Day, day))
                    {
                        return Never(out why, "reallife_mgr_undo_late");
                    }
                    return f.AskDay > 0 || f.PoachDay > 0 || f.LeaveDay > 0 || f.Raise < d.A ? NotNow(out why, "reallife_mgr_undo_changed") : UndoCan;
                }
                case ManagerTask.Offers:
                {
                    StaffFile f = _state.Employee(d.Subject);
                    if (f == null || !On(CompanyPart.Staff))
                    {
                        return Never(out why, "reallife_mgr_undo_gone");
                    }
                    if (d.F == 1 ? !ManagerRules.InTime(ManagerTask.Offers, d.Day, day) : f.LeaveDay == 0 || day >= f.LeaveDay)
                    {
                        return Never(out why, "reallife_mgr_undo_late");
                    }
                    if (f.GameOffer || f.Held || f.AskDay > 0 || (d.F == 1 && (f.PoachDay > 0 || f.LeaveDay > 0 || f.Raise < d.A)))
                    {
                        return NotNow(out why, "reallife_mgr_undo_changed");
                    }
                    return UndoCan;
                }
                case ManagerTask.Overtime:
                {
                    CompanyShop shop = ShopAt(d.Place);
                    if (shop == null || !On(CompanyPart.Staff))
                    {
                        return Never(out why, "reallife_mgr_undo_gone");
                    }
                    ShopFile file = shop.File;
                    if (d.A == 1)
                    {
                        return file.OvertimeFrom != d.Day || day >= file.OvertimeEnd ? Never(out why, "reallife_mgr_undo_late") : UndoCan;
                    }
                    if (day - d.Day >= CompanyCatalogue.OvertimeAnswerDays)
                    {
                        return Never(out why, "reallife_mgr_undo_late");
                    }
                    return file.OvertimeAsk > 0 || file.Overtime(day) || file.StrikeFrom > 0 ? NotNow(out why, "reallife_mgr_undo_changed") : UndoCan;
                }
                case ManagerTask.Claims:
                {
                    Claim c = _state.ClaimOf(d.A);
                    if (d.D == 0)
                    {
                        return Never(out why, "reallife_mgr_undo_left");
                    }
                    return c == null || c.Stage != ClaimStage.Filed || day > c.Day + CompanyCatalogue.ClaimWindowDays ? Never(out why, "reallife_mgr_undo_late") : UndoCan;
                }
                case ManagerTask.Stock:
                {
                    RotaPlace p = PlaceOf(d.Place);
                    DeliveryContract c = p != null ? ManagerGame.Contract(p.Reg, d.Subject) : null;
                    if (c == null)
                    {
                        return Never(out why, "reallife_mgr_undo_gone");
                    }
                    if (!ManagerRules.Undelivered(d.A, day, Save.Hour))
                    {
                        return Never(out why, "reallife_mgr_undo_delivered");
                    }
                    return ManagerGame.CanChange(c) ? UndoCan : Never(out why, "reallife_mgr_undo_locked");   // locked until the delivery itself
                }
                case ManagerTask.Prices:
                {
                    RotaPlace p = PlaceOf(d.Place);
                    if (p == null || CompanyBridges.WritePrice == null)
                    {
                        return Never(out why, "reallife_mgr_undo_gone");
                    }
                    return !ManagerRules.InTime(ManagerTask.Prices, d.Day, day) ? Never(out why, "reallife_mgr_undo_late") : UndoCan;
                }
                case ManagerTask.Rota:
                {
                    if (d.A < 0)
                    {
                        return Never(out why, "reallife_mgr_undo_nothing");
                    }
                    if (!RotaOn)
                    {
                        return NotNow(out why, "reallife_mgr_undo_rota_off");
                    }
                    return RotaHasUndo(d.Place) ? UndoCan : Never(out why, "reallife_mgr_undo_rota_gone");
                }
                case ManagerTask.Hiring:
                {
                    RotaPlace p = PlaceOf(d.Place);
                    EmployeeInstance e = ManagerGame.Employee(d.Subject);
                    if (p == null || e == null || !ManagerGame.At(e, p.Reg))
                    {
                        return Never(out why, "reallife_mgr_undo_gone");
                    }
                    if (!ManagerRules.InTime(ManagerTask.Hiring, d.Day, day))
                    {
                        return Never(out why, "reallife_mgr_undo_late");
                    }
                    return CompanyBridges.Leave != null ? UndoCan : NotNow(out why, "reallife_mgr_undo_changed");
                }
                default:
                    return Never(out why, "reallife_mgr_undo_nothing");
            }
        }

        private static int Never(out string why, string key)
        {
            why = key;
            return UndoNever;
        }

        private static int NotNow(out string why, string key)
        {
            why = key;
            return UndoNotNow;
        }

        /// <summary>A decision undone by the player: what it changed is put back, and a product of it is the player's from now on.</summary>
        public void ManagerUndo(int id)
        {
            if (!ManagersOn || _mgr == null)
            {
                return;
            }
            Decision d = _mgr.Find(id);
            string why;
            int state = d != null ? UndoState(d, Save.Day, out why) : Never(out why, "reallife_mgr_undo_nothing");
            if (state != UndoCan)
            {
                if (d != null && state == UndoNever && d.Open)
                {
                    d.State = DecisionState.Closed;
                }
                Notify(NotificationType.Warning, why, null, 7f);
                Changed();
                return;
            }
            try
            {
                UndoOf(d, Save.Day);
            }
            catch (Exception e)
            {
                ManagerFault("undoing a decision", e);
                return;
            }
            d.State = DecisionState.Undone;
            CountUndone(d);
            Logger?.Info("Company: decision " + d.Id + " (" + (ManagerTask)d.Task + " at " + ManagedName(d.Place) + ", day " + d.Day + ", by " + d.Who
                         + ") undone by the player.");
            Notify(NotificationType.Success, "reallife_mgr_note_undone", new Dictionary<string, string> { { "place", ManagedName(d.Place) } }, 7f);
            Recount();
            Changed();
        }

        // An undone decision counted to whoever made it: the place's manager, or the regional manager who stood in.
        private void CountUndone(Decision d)
        {
            ManagerFile m = MadeBy(ManagedAt(d.Place), d.Who) ?? OwnOf(d.Place);
            if (m != null)
            {
                m.Undone++;
            }
        }

        private void UndoOf(Decision d, int day)
        {
            ManagerFile m = OwnOf(d.Place);
            switch ((ManagerTask)d.Task)
            {
                case ManagerTask.Raises:
                {
                    StaffFile f = _state.Employee(d.Subject);
                    f.Raise -= d.A;
                    StaffRules.MoveStanding(f, -d.B);
                    f.AskDay = day;   // the request waits for the player again, with its days to answer
                    f.AskTo = d.D;
                    f.AskRest = d.E;
                    break;
                }
                case ManagerTask.Offers:
                {
                    StaffFile f = _state.Employee(d.Subject);
                    if (d.F == 1)
                    {
                        f.Raise -= d.A;
                        StaffRules.MoveStanding(f, -d.B);
                    }
                    f.LeaveDay = 0;
                    f.PoachDay = day;
                    f.PoachOffer = d.D;
                    f.PoachRest = d.E;
                    break;
                }
                case ManagerTask.Overtime:
                {
                    CompanyShop shop = ShopAt(d.Place);
                    ShopFile file = shop.File;
                    if (d.A == 1)
                    {
                        int left = Math.Max(0, file.OvertimeEnd - Math.Max(day, file.OvertimeFrom));
                        if (day <= file.OvertimeFrom)
                        {
                            file.OvertimeFrom = file.OvertimeEnd = file.OvertimeCost = 0;   // not a day run, so nothing paid
                        }
                        else
                        {
                            file.OvertimeEnd = day;   // what ran up to yesterday is paid on the next day's pass as before
                        }
                        for (int i = 0; i < _state.Staff.Count; i++)
                        {
                            if (_state.Staff[i].Place == d.Place)
                            {
                                StaffRules.MoveStanding(_state.Staff[i], -CompanyCatalogue.OvertimeStanding);
                            }
                        }
                        if (m != null)
                        {
                            m.Spent = Math.Max(0, m.Spent - left * d.B);
                        }
                    }
                    else
                    {
                        file.OvertimeAsk = day;
                    }
                    file.OvertimeRest = d.D;
                    Refoot(shop);
                    break;
                }
                case ManagerTask.Claims:
                {
                    Claim c = _state.ClaimOf(d.A);
                    ShopFile file = _state.Shop(c.Place);
                    c.Stage = ClaimStage.Open;
                    c.PayDay = 0;
                    if (d.B == 1 && file != null)
                    {
                        Policy p = file.Cover[CompanyCatalogue.Clamp(c.Kind, 0, CompanyCatalogue.CoverKinds - 1)];
                        p.Claims = Math.Max(0, p.Claims - 1);
                    }
                    break;
                }
                case ManagerTask.Stock:
                {
                    RotaPlace p = PlaceOf(d.Place);
                    DeliveryContract c = ManagerGame.Contract(p.Reg, d.Subject);
                    ManagerStore.Items(d.Detail, _items);
                    for (int k = 0; k < _items.Count; k++)
                    {
                        DeliveryContractItem line = ManagerGame.Line(c, _items[k].Item);
                        if (line != null && line.amount == _items[k].After)
                        {
                            ManagerGame.SetAmount(line, _items[k].Before);
                        }
                        Hold(m, _items[k].Item);
                    }
                    break;
                }
                case ManagerTask.Prices:
                {
                    RotaPlace p = PlaceOf(d.Place);
                    string district = ManagerGame.District(p.Reg);
                    ManagerStore.Items(d.Detail, _items);
                    for (int k = 0; k < _items.Count; k++)
                    {
                        if (ManagerGame.PriceCents(p.Reg, _items[k].Item) == _items[k].After)
                        {
                            CompanyBridges.WritePrice(p.Reg, _items[k].Item, _items[k].Before / 100f, district);
                        }
                        Hold(m, _items[k].Item);
                    }
                    break;
                }
                case ManagerTask.Rota:
                    RotaPutBack(d.Place);
                    break;
                case ManagerTask.Hiring:
                {
                    EmployeeInstance e = ManagerGame.Employee(d.Subject);
                    if (CompanyBridges.Leave(e) && m != null)
                    {
                        m.Spent = Math.Max(0, m.Spent - d.C);
                    }
                    break;
                }
            }
        }
    }
}
