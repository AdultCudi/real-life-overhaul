using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's levels and the owner's pay (Economy/OwnBank/Tiers): founded a community bank, moved up a level for the
    /// next licence when its record and capital allow, an older save's bank fitted to its level as it is read, the pay chosen
    /// in the app, and the notices: ready to move up (once a level), a limit reached (at a month's turn) and a branch that
    /// waits for a larger licence (once a day). Cost: nothing per frame; a few sums after the day and when a branch is read.
    /// </summary>
    public sealed partial class OwnBankDirector
    {
        private int _toldWaitingDay = -1;

        /// <summary>What founding a bank costs now: a community bank's licence, at today's prices and the scale bought at.</summary>
        public static long FoundLicence(int level, int share) => OwnBankRules.TierLicence(BankTiers.Community, level, share);

        /// <summary>The capital a new bank pays in: a community bank's least, AgainCapital more for each bank closed before.</summary>
        public static long FoundCapital(int level, int closures, int share) => OwnBankRules.TierCapital(BankTiers.Community, level, closures, share);

        /// <summary>A level's name for the player.</summary>
        public static string TierName(int tier) => OwnBankText.T(BankTiers.NameKeys[OwnBankRules.Clamp(tier, BankTiers.Community, BankTiers.National)]);

        // A level's name for the log.
        private static string TierWord(int tier)
        {
            switch (tier)
            {
                case BankTiers.Community: return "community";
                case BankTiers.Regional: return "regional";
                case BankTiers.National: return "national";
                default: return "not yet fitted";
            }
        }

        // A bank read from a line written before levels is put at the level its capital meets, keeping all it holds.
        private void Fit(OwnBank bank)
        {
            if (bank == null || bank.Tier != BankTiers.Unknown)
            {
                return;
            }
            int tier = OwnBankRules.FitTier(bank, Prices.Level);
            if (bank.Running)
            {
                Logger?.Info("Own bank: " + bank.Name + " was founded before banks had levels; with its capital of " + OwnBankRules.CapitalNow(bank) / 100 + " it is a " + TierWord(tier)
                             + " bank, and keeps every saver, loan and branch it has even past that level's limits.");
            }
        }

        /// <summary>The licence moving the bank up a level costs now; 0 for a bank at the top or none.</summary>
        public long MoveUpFee()
        {
            if (_bank == null || !_bank.Running || OwnBankRules.TierOf(_bank) >= BankTiers.National)
            {
                return 0;
            }
            return OwnBankRules.MoveUpFee(OwnBankRules.NextTier(_bank), Prices.Level, EndScaleRules.Buying(Settings.EndScale));
        }

        /// <summary>Why the bank may not move up now (a reason key), or null if it may once the licence is paid.</summary>
        public string MoveUpWhy()
        {
            if (!Running || Save == null)
            {
                return "reallife_ob_why_off";
            }
            return OwnBankRules.MoveUpWhy(_bank, Save.Day, GameCalendar.MonthDays, Prices.Level, EndScaleRules.Buying(Settings.EndScale));
        }

        /// <summary>
        /// The bank moved up a level, the licence paid from the player's cash (a test's bank is moved up by nobody, as it was
        /// founded). False with a reason key if it may not, or the licence is no longer the one the player confirmed.
        /// </summary>
        public bool MoveUp(long feeAsked, out string why)
        {
            why = Ready() ?? MoveUpWhy();
            long fee = MoveUpFee();
            if (why == null && fee != feeAsked)
            {
                why = ChangedKey;
            }
            bool test = _bank != null && _bank.Test;
            if (why == null && !test && fee > OwnBankGame.Cash)
            {
                why = "reallife_ob_why_no_cash";
            }
            if (why != null)
            {
                return false;
            }
            if (!test && !OwnBankGame.Pay(fee, OwnBankCatalogue.LicenceKey, OwnBankGame.Headquarters(), GameBooks.Licensing))
            {
                why = "reallife_ob_why_no_cash";
                return false;
            }
            OwnBankRules.MoveUp(_bank, Save.Day, EndScaleRules.Buying(Settings.EndScale));
            int tier = OwnBankRules.TierOf(_bank);
            BankCaps caps = OwnBankRules.Caps(_bank, Prices.Level, ScaleNow());
            if (tier >= BankTiers.National)
            {
                Tell(NotificationType.Success, "reallife_ob_toast_moved_top", TabBank, "bank", _bank.Name, "level", TierName(tier), "branches", Whole(caps.Branches));
            }
            else
            {
                Tell(NotificationType.Success, "reallife_ob_toast_moved_up", TabBank, "bank", _bank.Name, "level", TierName(tier), "branches", Whole(caps.Branches),
                    "loans", OwnBankText.Money(caps.Loans), "deposits", OwnBankText.Money(caps.Deposits));
            }
            Print("reallife_ob_news_moved_up", "bank", _bank.Name, "level", TierName(tier));
            Logger?.Info("Own bank: " + _bank.Name + " moved up to a " + TierWord(tier) + " bank on day " + Save.Day
                         + (test ? " (a test's bank, paid by nobody)" : ", the licence " + fee / 100 + " paid")
                         + "; its capital " + OwnBankRules.CapitalNow(_bank) / 100 + ", the least it keeps now " + OwnBankRules.Keep(_bank, Prices.Level) / 100 + ".");
            ReadBranchesNow();
            Changed();
            return true;
        }

        /// <summary>The owner's pay as a share of each day's profit (thousandths, to the slider's step) or as a fixed sum.</summary>
        public void SetPayMode(int mode)
        {
            if (Running && _bank.Running)
            {
                _bank.PayMode = mode == BankTiers.PayFixedMode ? BankTiers.PayFixedMode : BankTiers.PayShareMode;
            }
        }

        /// <summary>The owner's share of each day's profit, thousandths, put on the slider's step.</summary>
        public void SetPayShare(int share)
        {
            if (Running && _bank.Running)
            {
                _bank.PayShare = OwnBankRules.PayShareStep(share);
            }
        }

        /// <summary>The owner's fixed pay a day, whole money in cents. False with a reason key if it cannot be set.</summary>
        public bool SetPayFixed(long cents, out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : _bank.Test ? "reallife_ob_why_test" : null);
            if (why == null && (cents < 0 || cents > BankTiers.PayFixedMostCents))
            {
                why = "reallife_ob_why_pay_most";
            }
            if (why != null)
            {
                return false;
            }
            _bank.PayFixed = cents;
            _bank.PayMode = BankTiers.PayFixedMode;
            Logger?.Info("Own bank: the owner's pay set to " + cents / 100 + " a day, never more than the day's profit.");
            Changed();
            return true;
        }

        // After the day: a bank that now meets every condition of its next level but the licence is told once a level.
        private void TellReady()
        {
            if (!Running || Save == null || !_bank.Running || OwnBankRules.TierOf(_bank) >= BankTiers.National)
            {
                return;
            }
            int next = OwnBankRules.NextTier(_bank);
            if (_bank.TierTold >= next || MoveUpWhy() != null)
            {
                return;
            }
            _bank.TierTold = next;
            Tell(NotificationType.Success, "reallife_ob_toast_ready", TabBank, "bank", _bank.Name, "level", TierName(next), "fee", OwnBankText.Money(MoveUpFee()));
            Logger?.Info("Own bank: " + _bank.Name + " is ready to move up to a " + TierWord(next) + " bank on day " + Save.Day + ", for a licence of " + MoveUpFee() / 100 + ".");
        }

        // At a month's turn: a bank whose level's savings or loans are full is told so, with what moving up would do.
        private void TellFull(BankCity city)
        {
            int tier = OwnBankRules.TierOf(_bank);
            if (tier >= BankTiers.National || _bank.Test)
            {
                return;
            }
            BankCaps caps = OwnBankRules.Caps(_bank, city.Level, city.Scale);
            if (OwnBankRules.DepositRoom(_bank, caps) <= 0)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_full_deposits", TabBank, "bank", _bank.Name, "level", TierName(tier), "most", OwnBankText.Money(caps.Deposits));
            }
            else if (OwnBankRules.LoanRoom(_bank, caps) <= 0)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_full_loans", TabBank, "bank", _bank.Name, "level", TierName(tier), "most", OwnBankText.Money(caps.Loans));
            }
        }

        // Once a day at most: a branch of the player's that the bank's level has no room for.
        private void TellWaiting(string branch)
        {
            if (Save == null || _toldWaitingDay == Save.Day)
            {
                return;
            }
            _toldWaitingDay = Save.Day;
            int tier = OwnBankRules.TierOf(_bank);
            Tell(NotificationType.Warning, "reallife_branch_toast_waiting", TabBranches, "branch", branch ?? string.Empty, "level", TierName(tier),
                "count", Whole(BankTiers.BranchesMost[tier]));
            Logger?.Info("Bank branch: '" + branch + "' is not taken in: a " + TierWord(tier) + " bank runs " + BankTiers.BranchesMost[tier] + " branch(es) and " + _bank.Branches.Count
                         + " are open; it waits until the bank moves up.");
        }

        // After a move up a branch that waited is taken in at once, not at the next hour.
        private void ReadBranchesNow()
        {
            if (Settings.EconomyBranch)
            {
                ReadBranches();
            }
        }

        private static string Whole(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// For Options > Tests: the bank's record at its level made long enough to move up; a test's bank also has the next
        /// level's least capital put in by nobody, so that the whole move can be tried without the money.
        /// </summary>
        public void TestTierReady()
        {
            const string test = "My bank ready to move up";
            if (!TestReady(test, true))
            {
                return;
            }
            TestGuard("level", () =>
            {
                if (OwnBankRules.TierOf(_bank) >= BankTiers.National)
                {
                    TestRefusals.TellKey(Logger, test, OwnBankRules.UpTopKey);
                    return;
                }
                Keep();
                int next = OwnBankRules.NextTier(_bank);
                _bank.TierFrom = Math.Min(_bank.TierFrom, Save.Day - BankTiers.RecordMonths[next] * GameCalendar.MonthDays);
                _bank.TierTold = Math.Min(_bank.TierTold, next - 1);
                if (_bank.Test)
                {
                    long short_ = OwnBankRules.TierCapital(next, Prices.Level, _bank.Closures, EndScaleRules.Buying(Settings.EndScale)) - OwnBankRules.CapitalNow(_bank);
                    OwnBankRules.PutIn(_bank, OwnBankRules.ToStep(Math.Max(0, short_) + OwnBankCatalogue.CapitalStepCents - 1));
                }
                TestDone("the record for a " + TierWord(next) + " bank made long enough" + (_bank.Test ? ", and its least capital put in by nobody" : string.Empty)
                         + "; moving up now: " + (MoveUpWhy() ?? "ready"), TabBank);
            });
        }
    }
}
