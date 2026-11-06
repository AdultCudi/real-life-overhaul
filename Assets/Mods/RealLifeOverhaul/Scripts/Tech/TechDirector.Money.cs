using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the board pack shows of a firm with investors, read now. Cents at today's prices.</summary>
    public struct BoardPack
    {
        public long Balance, Investors, Floor, Wallet, Taken, Burn, Mrr, Target, SalaryDrawn, Band, RelatedPaid, RelatedOver, Loan, LoanOwed;
        public int Runway;            // days; -1 for none (the firm is not burning)
        public int RelatedDeals, Suspicion, Threshold, Mood, PackDay, GraceTo, Step;
        public int Fit;               // product market fit, thousandths, for a firm that was founded here or took its niche up; -1 for none
        public int Stage;             // its stage of going to market (FoundCatalogue)
        public bool Grace, Delayable, Question;
    }

    /// <summary>
    /// The firm account (VentureRules/StartupMoney): the wallet kept against the investors' money, the board's pack and its questions,
    /// the ladder of consequences and the wind down. COST: the money hook is a string compare and a float compare for each money
    /// change and nothing more while the wallet is over the floor; a look into the game's transactions happens only when it falls under
    /// it, at most <see cref="MoneyCatalogue.ScansPerDay"/> times a day. The rest runs once a game day or on a click.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const string MoneyChange = Economy.GameLoans.MoneyChangeEvent;
        private static readonly string[] Routine =
        {
            "ba:transactioncategory_rent", "ba:transactioncategory_salaryexpenses", "ba:transactioncategory_loanexpenses", "ba:transactioncategory_healthinsuranceexpenses",
            "ba:transactioncategory_parkingfees", "ba:transactioncategory_licensingfees", "ba:transactioncategory_marketing"
        };
        private static readonly string[] RoutineWords = { "tax", "fine", "interest", "rent", "salary", "wage", "utility" };

        private Action<string> _onMoney, _moneySeen;
        private double _wallet;
        private long _floorNow;      // what the wallet must keep for the firms' investors, at today's prices
        private int _scanDay = -1, _scans;
        private readonly List<Cap> _winding = new List<Cap>(2);

        private long Base(long cents) => cents * 1000 / Math.Max(1, Prices.Level);

        private void StartMoney()
        {
            _onMoney = _onMoney ?? Guard.Wrap<string>(OnMoneyEvent);
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onMoney, ref _moneySeen);
            _wallet = Save.Money;
            _scanDay = -1;
            int opened = 0;
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                opened += StartupMoney.OpenOld(_venture.Caps[i], Save.Day, Year) ? 1 : 0;
            }
            RefreshFloor(Save.Day);
            TechBridges.BoardSessionsNow(true);
            if (opened > 0)
            {
                Logger?.Info("Tech: " + opened + " firm(s) of the player's with investors opened their firm account with no floor and " + MoneyCatalogue.GraceDays + " days of grace.");
            }
        }

        private void StopMoney()
        {
            GameEvent.onGameEventTriggered -= _onMoney;
            _moneySeen = null;
            _floorNow = 0;
            TechBridges.BoardSessionsNow(false);
        }

        // The game clears its event hubs on every load, so the hook is kept attached (one reference compare while nothing changed).
        private void KeepMoney()
        {
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onMoney, ref _moneySeen);
        }

        /// <summary>What the wallet must keep for the investors of the player's firms, at today's prices.</summary>
        internal long FloorNow => _ventureOn ? _floorNow : 0;

        private void RefreshFloor(int day)
        {
            _floorNow = Level(StartupMoney.FloorOf(_venture.Caps, day));
            RefreshProvision();
        }

        private void OnMoneyEvent(string gameEvent)
        {
            if (gameEvent != MoneyChange)
            {
                return;
            }
            GameInstance save = Save;
            if (save == null || !_ventureOn || _venture == null)
            {
                return;
            }
            double was = _wallet, now = save.Money;
            _wallet = now;
            if (_provisionLeast > 0 && now < was && (was - now) * 100.0 >= _provisionLeast)
            {
                WatchProvision(save, (long)((was - now) * 100.0));
            }
            if (_floorNow <= 0 || now >= was)
            {
                return;
            }
            long wallet = (long)(Math.Max(0.0, now) * 100.0), drop = (long)((was - now) * 100.0);
            if (wallet >= _floorNow || drop < Level(MoneyCatalogue.NoiseCents))
            {
                return;
            }
            if (save.Day != _scanDay)
            {
                _scanDay = save.Day;
                _scans = 0;
            }
            if (++_scans > MoneyCatalogue.ScansPerDay)
            {
                return;
            }
            Transaction t = LastTransaction(save);
            if (t != null && (t.transactionType == VentureGame.WindKey || t.transactionType == VentureGame.DividendKey))
            {
                return;   // the mod's own payments to the investors
            }
            Cap own = t != null && t.address != null ? CapAt(t.address) : null;
            if (own != null)
            {
                if (!StatementLine(t))
                {
                    StartupMoney.Spent(own, Base(drop));   // spent on the firm itself: it lowers the account, and with it the floor
                    RefreshFloor(save.Day);
                }
                return;
            }
            // The measure is the gap itself: wallet under floor, however it came about. Rent, wages and loan payments elsewhere count too,
            // labelled by what they were, and weigh lightly on suspicion (StartupMoney.Suspicion); the firm's own are the firm's spending, above.
            bool routine = t != null && IsRoutine(t);
            string key = t != null && t.transactionType != null ? t.transactionType : string.Empty, name = string.Empty;
            if (routine)
            {
                name = BusinessNameAt(t.address);
                key = IsRent(t) ? RentKey : RunningKey;
            }
            long counted = StartupMoney.Shortfall(_venture.Caps, Base(wallet), Base(drop), save.Day, Burning(), key, routine, name);
            if (counted <= 0)
            {
                return;
            }
            RefreshFloor(save.Day);
            Warned(save.Day, counted, key, name);
            ChangedTech();
        }

        // The first day a firm of the player's is found without its business starts a day of waiting; the next finds it still gone.
        private static bool GoneFor(Cap c, int day)
        {
            if (c.Acc.Gone == 0)
            {
                c.Acc.Gone = day;
                return false;
            }
            return day > c.Acc.Gone;
        }

        private static Transaction LastTransaction(GameInstance save)
        {
            Transaction last = null;
            if (save.Transactions != null)
            {
                foreach (Transaction x in save.Transactions)
                {
                    last = x;
                }
            }
            return last;
        }

        // The funded firm a transaction was booked at, by its address.
        private Cap CapAt(Address address)
        {
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                Cap c = _venture.Caps[i];
                if (!StartupMoney.Has(c))
                {
                    continue;
                }
                FirmPlace firm = FirmOf(c.Place);
                if (firm != null && firm.Reg != null && firm.Reg.Address == address)
                {
                    return c;
                }
            }
            return null;
        }

        // A running cost the game's statement carries into the firm's profit by itself.
        private static bool StatementLine(Transaction t)
        {
            return Economy.BizLines.Of(t.transactionType) == Economy.BizLine.Cost || Economy.BizLines.Of(t.transactionType) == Economy.BizLine.Takings || ByCategory(t);
        }

        private static bool ByCategory(Transaction t)
        {
            List<string> categories = t.transactionCategories;
            for (int i = 0; categories != null && i < categories.Count; i++)
            {
                for (int r = 0; r < Routine.Length; r++)
                {
                    if (categories[i] == Routine[r])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Rent, wages, a loan's payment, a tax or a fine: the player's ordinary life, never counted as spending the investors' money.
        private static bool IsRoutine(Transaction t)
        {
            if (StatementLine(t))
            {
                return true;
            }
            string type = t.transactionType ?? string.Empty;
            for (int i = 0; i < RoutineWords.Length; i++)
            {
                if (type.IndexOf(RoutineWords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool Burning()
        {
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                FirmPlace firm = StartupMoney.Has(_venture.Caps[i]) ? FirmOf(_venture.Caps[i].Place) : null;
                if (firm != null && FirmProfit(firm) < 0)
                {
                    return true;
                }
            }
            return false;
        }

        internal const string RentKey = "reallife_vc_misuse_rent", RunningKey = "reallife_vc_misuse_running";

        private static string BusinessNameAt(Address address)
        {
            try
            {
                BuildingRegistration reg = address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
                return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? TalentGame.NameOf(reg) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static bool IsRent(Transaction t)
        {
            if ((t.transactionType ?? string.Empty).IndexOf("rent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            List<string> categories = t.transactionCategories;
            for (int i = 0; categories != null && i < categories.Count; i++)
            {
                if (categories[i] == Routine[0])
                {
                    return true;
                }
            }
            return false;
        }

        private void Warned(int day, long counted, string key, string name)
        {
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                Cap c = _venture.Caps[i];
                if (!StartupMoney.Has(c) || c.Acc.Event != day || c.Acc.WarnDay == day)
                {
                    continue;
                }
                c.Acc.WarnDay = day;
                FirmPlace firm = FirmOf(c.Place);
                TellTech(NotificationType.Warning, "reallife_vc_note_shortfall",
                    Data("firm", firm != null ? firm.File.Name : string.Empty, "amount", TechText.Cents(Level(counted)),
                        "what", Describe(key, name), "floor", TechText.Cents(Level(StartupMoney.Floor(c, day)))),
                    TabFunding, TechApp.CapRow + c.Place);
                return;
            }
        }

        /// <summary>A transaction's key as words, without its placeholders ("Vehicle purchase"); the mod's own lines of the account are words already.</summary>
        internal static string Describe(string key, string name = "")
        {
            if (string.IsNullOrEmpty(key))
            {
                return TechText.T("reallife_vc_misuse_unknown");
            }
            if (key == RunningKey)
            {
                return string.IsNullOrEmpty(name) ? TechText.T("reallife_vc_misuse_running_own") : TechText.T(RunningKey, "name", name);
            }
            if (key == RentKey)
            {
                return TechText.T(RentKey);
            }
            string text = key;
            try
            {
                text = LocalizorManager.IsLocalizedKey(key) ? key.GetLocalization() : key;
            }
            catch (Exception)
            {
                text = key;
            }
            var b = new System.Text.StringBuilder(text.Length);
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                depth += text[i] == '{' ? 1 : 0;
                if (depth == 0)
                {
                    b.Append(text[i]);
                }
                depth -= text[i] == '}' && depth > 0 ? 1 : 0;
            }
            string clean = b.ToString().Trim(' ', ':', '-');
            return clean.Length > 0 ? clean : TechText.T("reallife_vc_misuse_unknown");
        }

        // ---------------------------------------------------------------- the day

        // A firm of the player's with investors, once a game day: its profit read into the account, a loan that lapsed, the board pack,
        // and what the board does about what it has found. Returns false when the firm is gone and the cap was wound down.
        private bool MoneyDay(Cap c, FirmPlace firm, int day)
        {
            FirmAccount a = c.Acc;
            if (StartupMoney.OpenOld(c, day, Year))
            {
                Logger?.Info("Tech: the firm at " + Where(c.Place) + " has investors from before the firm account: it opens with no floor and a grace to day " + a.GraceTo + ".");
            }
            if (!StartupMoney.Has(c))
            {
                return true;
            }
            a.Gone = 0;
            a.Name = firm.File.Name;
            int days = day - a.Synced;
            if (days > 0)
            {
                int look = Math.Min(days, 30);
                long sum = FirmProfitSum(firm, look);
                StartupMoney.Profit(c, Base(sum), day);
            }
            StartupMoney.LoanLapsed(c, day);
            int accounting = TechBridges.SkillNow(TalentCatalogue.Accounting), leadership = TechBridges.SkillNow(TalentCatalogue.Leadership);
            bool packDay = StartupMoney.PackDue(c, day);
            if (packDay)
            {
                SendPack(c, firm, day);
            }
            int threshold = StartupMoney.ThresholdOf(c);
            int suspicion = StartupMoney.Suspicion(c, day, accounting, leadership);
            switch (StartupMoney.Next(c, day, suspicion, threshold, packDay || VentureTerms.OnDemand(c)))   // a right to look on demand: any day, not only a pack day
            {
                case StartupMoney.ActAsk:
                    Ask(c, firm, day);
                    break;
                case StartupMoney.ActDemand:
                    Demand(c, firm, day, true);   // a question left unanswered is stonewalling
                    break;
                case StartupMoney.ActRemove:
                    Remove(c, firm, day);
                    break;
                case StartupMoney.ActClaim:
                    Claim(c, firm, day);
                    break;
                case StartupMoney.ActCharge:
                    Charge(c, firm.File.Name, day);
                    break;
            }
            return true;
        }

        /// <summary>The firm's profit over the last days, cents at today's prices (negative: a loss), from the game's statements.</summary>
        internal long FirmProfitSum(FirmPlace firm, int days)
        {
            try
            {
                return (long)(Economy.Books.AverageProfit(firm.Reg.Address, days, out _) * days * 100f);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void SendPack(Cap c, FirmPlace firm, int day)
        {
            BoardPack p = PackOf(c, firm, day);
            PackSlip(c, firm, day);
            TellTech(NotificationType.Info, p.Fit >= 0 ? "reallife_vc_note_pack_fit" : "reallife_vc_note_pack",
                Data("firm", firm.File.Name, "runway", p.Runway < 0 ? "-" : p.Runway.ToString(), "revenue", TechText.Cents(p.Mrr), "target", TechText.Cents(p.Target),
                    "account", TechText.Cents(p.Balance), "wallet", TechText.Cents(p.Wallet), "fit", TechText.Mille(Math.Max(0, p.Fit))), TabFunding, TechApp.CapRow + c.Place);
            StartupMoney.Sent(c, day);
        }

        /// <summary>The board pack now: honest by construction, read from the books.</summary>
        internal BoardPack PackOf(Cap c, FirmPlace firm, int day)
        {
            FirmAccount a = c.Acc;
            long profit = firm != null ? FirmProfit(firm) : 0;
            int threshold = StartupMoney.ThresholdOf(c);
            int suspicion = StartupMoney.Suspicion(c, day, TechBridges.SkillNow(TalentCatalogue.Accounting), TechBridges.SkillNow(TalentCatalogue.Leadership));
            FoundedFirm founded = FoundingOf(c.Place);
            return new BoardPack
            {
                Fit = founded != null && founded.Counts ? founded.Fit : -1, Stage = founded != null ? founded.Stage : 0,
                Balance = Level(StartupMoney.Balance(c)), Investors = Level(StartupMoney.Investors(c)), Floor = Level(StartupMoney.Floor(c, day)), Wallet = VentureGame.Cash,
                Taken = Level(a.Taken), Burn = profit < 0 ? -profit : 0, Runway = profit < 0 ? (int)Math.Min(int.MaxValue, VentureGame.Cash / -profit) : -1,
                Mrr = firm != null ? FirmMrr(c.Place) : 0, Target = Level(c.TargetMrr), SalaryDrawn = Level(day - a.SalaryFrom >= MoneyCatalogue.PackDays ? 0L : a.SalaryDrawn),
                Band = Level(a.Band), RelatedPaid = Level(a.RelatedPaid), RelatedOver = Level(a.RelatedOver), RelatedDeals = a.RelatedDeals, Loan = Level(a.Loan),
                LoanOwed = Level(StartupMoney.LoanOwed(c, day)), Suspicion = suspicion, Threshold = threshold, Mood = StartupMoney.Mood(suspicion, threshold), PackDay = a.PackDay,
                GraceTo = a.GraceTo, Grace = day <= a.GraceTo, Delayable = !a.DelayUsed, Step = a.Step, Question = a.Step == StartupMoney.StepAsked
            };
        }

        internal int Today => Save != null ? Save.Day : 0;

        internal string LeadOf(Cap c) => LeadName(c);

        private static string LeadName(Cap c)
        {
            Holder lead = null;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc && (lead == null || c.Holders[i].Invested > lead.Invested))
                {
                    lead = c.Holders[i];
                }
            }
            return lead != null ? VentureCatalogue.Firms[TalentCatalogue.Clamp(lead.Who, 0, VentureCatalogue.FirmCount - 1)].Name : string.Empty;
        }

        // ---------------------------------------------------------------- the ladder

        private long Found(Cap c)
        {
            int suspicion = StartupMoney.Suspicion(c, Save.Day, TechBridges.SkillNow(TalentCatalogue.Accounting), TechBridges.SkillNow(TalentCatalogue.Leadership));
            bool audit = StartupMoney.Audit(c, suspicion, StartupMoney.ThresholdOf(c));
            return audit ? c.Acc.Taken : c.Acc.Taken * StartupMoney.LookFindsOf(c) / 1000;
        }

        private void Ask(Cap c, FirmPlace firm, int day)
        {
            c.Acc.Step = StartupMoney.StepAsked;
            c.Acc.AskDay = day;
            TellTech(NotificationType.Warning, "reallife_vc_note_ask",
                Data("firm", firm.File.Name, "vc", LeadName(c), "days", MoneyCatalogue.QuestionDays.ToString()), TabFunding, TechApp.CapRow + c.Place);
            ChangedTech();
        }

        private void Demand(Cap c, FirmPlace firm, int day, bool stonewalled)
        {
            c.Acc.Step = StartupMoney.StepDemand;
            c.Acc.StepDay = day;
            c.Acc.Stonewalled |= stonewalled;
            TellTech(NotificationType.Error, "reallife_vc_note_demand",
                Data("firm", firm.File.Name, "vc", LeadName(c), "amount", TechText.Cents(Level(Found(c))), "days", MoneyCatalogue.DemandDays.ToString()), TabFunding, TechApp.CapRow + c.Place);
            ChangedTech();
        }

        // The board removes the founder as chief executive: the unvested founder shares go back as a bad leaver, every venture firm shuts its door,
        // Standing falls and the City Ledger tells it.
        private void Remove(Cap c, FirmPlace firm, int day)
        {
            c.Acc.Step = StartupMoney.StepRemoved;
            c.Acc.StepDay = day;
            if (!FoundingCanRemove(c))
            {
                Unremoved(c, firm, day);   // the articles do not let this board remove the founder
                return;
            }
            long back = StartupMoney.BadLeaver(c, day, Year);
            bool replaced = string.IsNullOrEmpty(c.Ceo) && _talent.People.Count < TalentStore.PeopleMost;
            if (replaced)
            {
                ReplaceChief(c, firm, day);   // the board's own path: a professional on the payroll, and its story for the paper
            }
            c.Pressure = 2;
            ShutDoors(day);
            TechBridges.Fame?.Invoke(3, -MoneyCatalogue.StandingFall);
            if (!replaced)
            {
                Story("reallife_tech_news_removed", "firm", firm.File.Name, "vc", LeadName(c));
            }
            TellTech(NotificationType.Error, "reallife_vc_note_removed", Data("firm", firm.File.Name, "vc", LeadName(c), "shares", back.ToString()), TabFunding, TechApp.CapRow + c.Place);
            Logger?.Info("Tech: the board of " + firm.File.Name + " removed the founder with " + TechText.Cents(Level(c.Acc.Taken)) + " taken: " + back + " unvested share(s) went back.");
            RefreshFloor(day);
            ChangedTech();
        }

        private void ShutDoors(int day)
        {
            for (int i = 0; i < _venture.ClosedUntil.Length; i++)
            {
                _venture.ClosedUntil[i] = Math.Max(_venture.ClosedUntil[i], day + MoneyCatalogue.ClosedDays);
            }
        }

        private void Claim(Cap c, FirmPlace firm, int day)
        {
            c.Acc.Step = StartupMoney.StepClaim;
            c.Acc.StepDay = day;
            long owed = Level(c.Acc.Taken) * (1000 + MoneyCatalogue.CostsPermille) / 1000;
            c.Acc.Suit = TechBridges.InvestorClaim != null ? TechBridges.InvestorClaim(owed / 100, firm.File.Name, LeadName(c)) : 0;
            Story("reallife_tech_news_sued", "firm", firm.File.Name, "vc", LeadName(c));
            ChangedTech();
        }

        private void Charge(Cap c, string name, int day)
        {
            c.Acc.Step = StartupMoney.StepCharged;
            c.Acc.StepDay = day;
            c.Acc.Case = TechBridges.InvestorFraud != null ? TechBridges.InvestorFraud((int)Math.Min(int.MaxValue, Level(c.Acc.Taken) / 100), StartupMoney.Evidence(c)) : 0;
            Story("reallife_tech_news_charged", "firm", name, "vc", LeadName(c));
            ChangedTech();
        }

        // ---------------------------------------------------------------- the board's question

        /// <summary>Why the board's question cannot be held at a firm now (a key), or null when it is open.</summary>
        internal string BoardBlocked(string place)
        {
            Cap c = _ventureOn ? _venture.CapOf(place) : null;
            return c != null && StartupMoney.Has(c) && c.Acc.Step == StartupMoney.StepAsked ? null : "reallife_tech_desk_board_why_none";
        }

        /// <summary>The question answered at the desk: 0 repay what was taken, 1 explain, 2 stonewall. False, with the reason, when nothing could be done.</summary>
        internal bool BoardSession(string place, string name, int choice, out string key, out string[] pairs)
        {
            pairs = new[] { "firm", name ?? string.Empty };
            Cap c = _ventureOn ? _venture.CapOf(place) : null;
            FirmPlace firm = FirmOf(place);
            if (c == null || firm == null || !StartupMoney.Has(c) || c.Acc.Step != StartupMoney.StepAsked || Save == null)
            {
                key = "reallife_tech_desk_board_why_none";
                return false;
            }
            int day = Save.Day;
            if (choice == 0)
            {
                long paid = Level(RepayTaken(c, day));
                if (paid <= 0)
                {
                    key = "reallife_tech_desk_board_why_cash";
                    return false;
                }
                key = "reallife_tech_desk_board_repaid";
                pairs = new[] { "firm", firm.File.Name, "amount", TechText.Cents(paid) };
            }
            else if (choice == 1)
            {
                int chance = StartupMoney.ExplainChance(c, TechBridges.SkillNow(TalentCatalogue.Accounting), TechBridges.SkillNow(TalentCatalogue.Law));
                bool believed = TechRandom.Mille(_venture.Seed, day, StartupMoney.Hash(place), 91) < chance;
                if (believed)
                {
                    c.Acc.Step = StartupMoney.StepNone;
                    c.Acc.Calm = day + MoneyCatalogue.PackDays * 2;
                    c.Acc.Event = day;
                    key = "reallife_tech_desk_board_believed";
                }
                else
                {
                    Demand(c, firm, day, false);
                    key = "reallife_tech_desk_board_doubted";
                }
                pairs = new[] { "firm", firm.File.Name, "chance", TechText.Mille(chance) };
            }
            else
            {
                Demand(c, firm, day, true);
                key = "reallife_tech_desk_board_stonewalled";
            }
            RefreshFloor(day);
            Grow(TalentCatalogue.Leadership, TermsCatalogue.GrowBoard);
            ChangedTech();
            return true;
        }

        /// <summary>The board's question answered from the firm's page: the same answer as at the desk, and its notice.</summary>
        internal bool AnswerBoard(string place, int choice)
        {
            FirmPlace firm = FirmOf(place);
            bool done = BoardSession(place, firm != null ? firm.File.Name : string.Empty, choice, out string key, out string[] pairs);
            Notifications.Show(done ? NotificationType.Info : NotificationType.Warning, key, Data(pairs), 8f, null, null, true, false);
            return done;
        }

        // Cash over what every firm's investors are owed is put back against what was taken (it becomes the firm's again). Start-level cents put back.
        private long RepayTaken(Cap c, int day)
        {
            long free = Base(VentureGame.Cash) - StartupMoney.FloorOf(_venture.Caps, day);
            long paid = StartupMoney.Repay(c, free, c.Acc.Taken, day);
            if (paid > 0)
            {
                c.Acc.Stonewalled = false;
                if (c.Acc.Step == StartupMoney.StepAsked || c.Acc.Step == StartupMoney.StepDemand)
                {
                    c.Acc.Step = c.Acc.Taken > 0 ? c.Acc.Step : StartupMoney.StepNone;
                }
                RefreshFloor(day);
            }
            return paid;
        }

        // ---------------------------------------------------------------- the firm page's actions

        internal bool Repay(string place)
        {
            Cap c = CapFor(place, false);
            if (c == null || !StartupMoney.Has(c) || Save == null || c.Acc.Taken <= 0)
            {
                return false;
            }
            long paid = Level(RepayTaken(c, Save.Day));
            if (paid <= 0)
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            TellTech(NotificationType.Success, "reallife_vc_note_repaid",
                Data("firm", FirmOf(place)?.File.Name ?? string.Empty, "amount", TechText.Cents(paid), "left", TechText.Cents(Level(c.Acc.Taken))),
                TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        internal bool TakeSalary(string place)
        {
            Cap c = CapFor(place, false);
            if (c == null || !StartupMoney.Has(c) || Save == null)
            {
                return false;
            }
            long drawn = StartupMoney.Draw(c, StartupMoney.SalaryLeft(c, Save.Day), Save.Day);
            if (drawn <= 0)
            {
                return false;
            }
            RefreshFloor(Save.Day);
            TellTech(NotificationType.Success, "reallife_vc_note_salary",
                Data("firm", FirmOf(place)?.File.Name ?? string.Empty, "amount", TechText.Cents(Level(drawn))), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        internal bool TakeLoan(string place)
        {
            Cap c = CapFor(place, false);
            long lent = c != null && StartupMoney.Has(c) && Save != null ? StartupMoney.Lend(c, Save.Day) : 0;
            if (lent <= 0)
            {
                return false;
            }
            RefreshFloor(Save.Day);
            TellTech(NotificationType.Info, "reallife_vc_note_loan",
                Data("firm", FirmOf(place)?.File.Name ?? string.Empty, "amount", TechText.Cents(Level(lent)), "days", MoneyCatalogue.LoanDays.ToString()), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        internal bool RepayLoan(string place)
        {
            Cap c = CapFor(place, false);
            if (c == null || !StartupMoney.Has(c) || Save == null || c.Acc.Loan <= 0)
            {
                return false;
            }
            long free = Base(VentureGame.Cash) - StartupMoney.FloorOf(_venture.Caps, Save.Day);
            if (!StartupMoney.RepayLoan(c, free, Save.Day))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            RefreshFloor(Save.Day);
            ChangedTech();
            return true;
        }

        internal bool PayDividend(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || !StartupMoney.Has(c) || Save == null)
            {
                return false;
            }
            long room = StartupMoney.DividendRoom(c, FirmProfit(firm));
            if (room <= 0 || !StartupMoney.DividendVote(c))
            {
                TellTech(NotificationType.Info, "reallife_vc_note_dividend_no", Data("firm", firm.File.Name), TabFunding, TechApp.CapRow + place);
                return false;
            }
            long toInvestors = Level(StartupMoney.Dividend(c, room));
            VentureGame.Pay(toInvestors, VentureGame.DividendKey, firm.File.Name, null, true);
            RefreshFloor(Save.Day);
            TellTech(NotificationType.Success, "reallife_vc_note_dividend",
                Data("firm", firm.File.Name, "amount", TechText.Cents(Level(room)), "theirs", TechText.Cents(toInvestors)), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        internal bool DelayPack(string place)
        {
            Cap c = CapFor(place, false);
            if (c == null || Save == null || !StartupMoney.Delay(c, Save.Day))
            {
                return false;
            }
            TellTech(NotificationType.Warning, "reallife_vc_note_pack_delayed",
                Data("firm", FirmOf(place)?.File.Name ?? string.Empty, "day", c.Acc.PackDay.ToString()), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        /// <summary>The player's other businesses a firm can buy work from, by name, into a list of the caller's.</summary>
        internal void Siblings(string place, List<BuildingRegistration> into)
        {
            into.Clear();
            var all = new List<BuildingRegistration>(16);
            Economy.Books.Businesses(all);
            for (int i = 0; i < all.Count; i++)
            {
                if (TalentGame.Place(all[i]) != place)
                {
                    into.Add(all[i]);
                }
            }
        }

        /// <summary>The fair price of a unit of work between the player's businesses, at today's prices.</summary>
        internal long FairPrice(string place)
        {
            Cap c = CapFor(place, false);
            return c != null ? Level(StartupMoney.Fair(c)) : 0;
        }

        /// <summary>
        /// The firm pays another business of the player's for work, at the fair price (tier 0), half again (1) or double (2). Both sides are booked at their
        /// businesses and the wallet does not move; what is paid over the fair price is counted as taken.
        /// </summary>
        internal bool PayRelated(string place, int business, int tier)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            var others = new List<BuildingRegistration>(8);
            Siblings(place, others);
            if (firm == null || c == null || !StartupMoney.Has(c) || business < 0 || business >= others.Count || Save == null)
            {
                return false;
            }
            BuildingRegistration other = others[business];
            string name = TalentGame.NameOf(other);
            long fair = StartupMoney.Fair(c), paid = fair * (tier <= 0 ? 1000 : tier == 1 ? 1500 : 2000) / 1000;
            int allowed = StartupMoney.RelatedCheck(c, name, paid, Save.Day);
            if (allowed == StartupMoney.RelatedSoon || allowed == StartupMoney.RelatedNo)
            {
                TellTech(NotificationType.Warning, allowed == StartupMoney.RelatedSoon ? "reallife_vc_note_related_soon" : "reallife_vc_note_related_capped",
                    Data("firm", firm.File.Name, "other", name, "days", MoneyCatalogue.RelatedGap.ToString()), TabFunding, TechApp.CapRow + place);
                return false;
            }
            if (!RelatedAllowed(c, firm, paid, Save.Day))
            {
                return false;
            }
            VentureGame.Give(Level(paid), VentureGame.RelatedTakingsKey, firm.File.Name, other);
            FirmsGame.Cost(Level(paid), VentureGame.RelatedCostKey, firm.Reg, name, null);
            long over = StartupMoney.Related(c, paid, fair, name, Save.Day);
            RefreshFloor(Save.Day);
            TellTech(over > 0 ? NotificationType.Warning : NotificationType.Info, over > 0 ? "reallife_vc_note_related_over" : "reallife_vc_note_related",
                Data("firm", firm.File.Name, "other", name, "amount", TechText.Cents(Level(paid)), "over", TechText.Cents(Level(over))), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        // ---------------------------------------------------------------- the wind down

        /// <summary>
        /// A funded firm closes, for whatever reason: its debts (the bridge) first, then the waterfall shares what the firm holds, paid to
        /// the investors out of the wallet. What the wallet cannot cover is the investors' claim, and a clear pattern is reported. A firm that
        /// honestly ran dry owes nothing. The cap is then gone.
        /// </summary>
        internal void WindDown(Cap c, string name, int day)
        {
            long wallet = Base(VentureGame.Cash);
            float evidence = StartupMoney.Evidence(c);
            StartupMoney.Wind w = StartupMoney.Close(c, wallet, day);
            FoundingWound(c, w, name);
            string lead = LeadName(c);
            c.Acc.Wound = true;
            if (w.Paid > 0)
            {
                VentureGame.Pay(Level(w.Paid), VentureGame.WindKey, name, null, true);
            }
            int suit = 0;
            if (w.Gap > 0)
            {
                long owed = Level(w.Gap) * (1000 + MoneyCatalogue.CostsPermille) / 1000;
                suit = TechBridges.InvestorClaim != null ? TechBridges.InvestorClaim(owed / 100, name, lead) : 0;
                ShutDoors(day);
                TechBridges.Fame?.Invoke(3, -MoneyCatalogue.StandingFall);
            }
            int fraud = w.Fraud && TechBridges.InvestorFraud != null ? TechBridges.InvestorFraud((int)Math.Min(int.MaxValue, Level(w.Taken) / 100), evidence) : 0;
            string key = w.Gap > 0 ? (w.Fraud ? "reallife_vc_note_wind_fraud" : "reallife_vc_note_wind_claim") : w.Dry || w.Owed <= 0 ? "reallife_vc_note_wind_dry" : "reallife_vc_note_wind_paid";
            Story(w.Gap > 0 ? "reallife_tech_news_wound_owed" : "reallife_tech_news_wound", "firm", name, "vc", lead);
            Notifications.Show(w.Gap > 0 ? NotificationType.Error : w.Dry ? NotificationType.Warning : NotificationType.Info, key,
                Data("firm", name, "vc", lead, "paid", TechText.Cents(Level(w.Paid)), "gap", TechText.Cents(Level(w.Gap))), 12f, null, null, true, false);
            Logger?.Info("Tech: " + name + " wound down on day " + day + ": the firm held " + TechText.Cents(Level(StartupMoney.Balance(c)))
                         + ", owed to its investors " + TechText.Cents(Level(w.Owed))
                         + ", paid " + TechText.Cents(Level(w.Paid)) + ", claim " + TechText.Cents(Level(w.Gap)) + " (suit " + suit + "), fraud case " + fraud + ".");
            _venture.Caps.Remove(c);
            RefreshFloor(day);
            ChangedTech();
        }
    }
}
