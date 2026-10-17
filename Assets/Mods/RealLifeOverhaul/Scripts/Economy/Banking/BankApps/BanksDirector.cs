using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank apps: one app on the game's phone for each of the city's three largest banks (BankTerms.Banks, ranked by
    /// BankRules), each that bank's slice of the player's money (BankApp). A city with fewer banks has fewer apps, and one
    /// line in Player.log says so. See docs/features/economy-banks.md.
    ///
    /// This class only keeps the apps on the phone. It has no Update and no save data: once a second the credit side's
    /// timer calls <see cref="EachSecond"/>, which compares the banks wanted with the apps there (a few comparisons while
    /// nothing has changed) and puts on at most one app a second, so building them from the game's parts never lands in
    /// one frame together. After the credit side's hour and day the badges are counted again: one pass over the credit
    /// file's accounts and loans per bank.
    ///
    /// Everything the apps show and do is the credit desk's (CreditDesk): the same record and the same decisions as the
    /// Finance app. With "Bank apps" off, or the credit rating off, there is no bank app and nothing else changes: every
    /// account and loan stays where it is, and the Finance app still shows and offers everything.
    /// </summary>
    public sealed class BanksDirector : MonoBehaviour
    {
        public const string AppIdPrefix = "RealLifeBank";

        private static readonly string[] AppGlyphs = { "Icon-Coin", "Icon-Money", "Icon-Security" };
        // The icon's two colours for each rank, top and bottom: three plain banking colours, none a real bank's.
        private static readonly Color32[] Tops = { new Color32(96, 156, 232, 255), new Color32(120, 196, 120, 255), new Color32(214, 120, 120, 255) };
        private static readonly Color32[] Bottoms = { new Color32(34, 82, 170, 255), new Color32(40, 124, 64, 255), new Color32(150, 44, 56, 255) };

        public static IModLogger Logger;
        public static BanksDirector Instance { get; private set; }

        private sealed class Slot
        {
            public string Name;         // the bank's name; null for an empty slot
            public PhoneApp App;
            public BankApp View;
        }

        private readonly Slot[] _slots = { new Slot(), new Slot(), new Slot() };
        private readonly List<Debt> _work = new List<Debt>(16);
        private Action _second, _onHour, _onDay;
        private bool _gaveUp, _broken;
        private int _toldBanks = -1;    // how many banks the last line about the city's banks named

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            BankApp.Logger = Logger;
            _second = EachSecond;
            _onHour = Badges;
            _onDay = Badges;
            EconomyDirector.Follow(StartSeats.Banks, _second);   // (its first second after a load has a frame of its own: Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            RemoveAll(null);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private static readonly int HotSecond = HotSpots.Slot("Economy: BanksDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);   // the credit side's second, for this feature (Core/HotSpots)
            if (_broken)
            {
                return;
            }
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                RemoveAll(null);
                Logger?.Warn("Banks: the bank apps are switched off for this session after an error. Every account and loan is as it was, and the Finance app still shows and offers everything.");
            }
        }

        private void Slow()
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            EconomyDirector credit = EconomyDirector.Instance;
            if (!Settings.Economy || !Settings.EconomyBanks || credit == null || !credit.Running || credit.Desk == null || !credit.Desk.Running)
            {
                RemoveAll(!Settings.EconomyBanks ? "the option is off" : "the credit rating is not running");
                return;
            }
            if (_gaveUp)
            {
                return;
            }
            IReadOnlyList<Lender> banks = BankTerms.Banks;
            int wanted = Mathf.Min(BankRules.AppsMost, banks.Count);
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                Lender bank = i < wanted ? banks[i] : null;
                if (slot.App != null && (bank == null || !string.Equals(slot.Name, bank.Name, StringComparison.Ordinal)))
                {
                    Remove(slot, "the city's banks changed");
                }
                if (bank == null)
                {
                    continue;
                }
                if (slot.App != null)
                {
                    slot.View.Bank = bank;   // (the same bank read again: the app follows the new branch object)
                    continue;
                }
                Install(slot, bank, i, credit);
                return;   // at most one app a second
            }
            if (_toldBanks != banks.Count)
            {
                _toldBanks = banks.Count;
                Logger?.Info("Banks: " + banks.Count + " bank(s) in the city, " + wanted + " bank app(s) on the phone" + (banks.Count < BankRules.AppsMost ? " (the city has fewer than " + BankRules.AppsMost + " banks)" : string.Empty)
                             + Names(wanted) + ".");
            }
        }

        private static string Names(int count)
        {
            string names = string.Empty;
            for (int i = 0; i < count; i++)
            {
                Lender b = BankTerms.Banks[i];
                BankProfile p = BankRules.Of(b.Rank);
                names += (i == 0 ? ": " : "; ") + b.Name + " (rank " + b.Rank + ", own cap " + BankTerms.OwnCap(b.Settings).ToString("0") + " at " + BankTerms.OwnRate(b.Settings) + "%, "
                         + (p.RatePoints >= 0 ? "+" : string.Empty) + p.RatePoints + " point(s), limits " + p.LimitPercent + "%, fee " + p.FeePercent + "%)";
            }
            return names;
        }

        private void Install(Slot slot, Lender bank, int index, EconomyDirector credit)
        {
            string id = AppIdPrefix + (index + 1);
            var view = new BankApp(credit, id) { Bank = bank };
            GameSprites.Want(AppGlyphs);
            int rank = Mathf.Clamp(index, 0, Tops.Length - 1);
            bool tryAgain;
            PhoneApp app = PhoneApp.Install(new PhoneAppDef
            {
                Id = id,
                TitleKey = "reallife_bank_app",
                TitleArguments = new Dictionary<string, string>(1) { { "bank", bank.Name } },   // (read by the game as it is: no reflection, unlike an object with fields)
                Icon = like => AppIconPainter.Paint(like, Glyph(), Tops[rank], Bottoms[rank], 0.42f, "AppIcon-Bank" + (index + 1)),
                Build = view.Build,
                Opened = view.Opened,
                Closed = () =>
                {
                    view.Closed();
                    Badges();
                }
            }, out tryAgain);
            if (app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Banks: the game's phone is not built as this version expects, so the bank apps are left off. The Finance app still shows and offers everything.");
                }
                return;
            }
            slot.Name = bank.Name;
            slot.App = app;
            slot.View = view;
            Badge(slot);
            if (Verbose)
            {
                Logger?.Info("Bank check (app): '" + bank.Name + "' is on the phone as " + id + ", rank " + bank.Rank + ": PASS");
            }
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private void Remove(Slot slot, string why)
        {
            if (slot.App == null)
            {
                return;
            }
            if (Verbose && why != null)
            {
                Logger?.Info("Banks: the app of '" + slot.Name + "' is taken off the phone (" + why + ").");
            }
            slot.App.Remove();
            slot.App = null;
            slot.View = null;
            slot.Name = null;
        }

        private void RemoveAll(string why)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Remove(_slots[i], why);
            }
            _toldBanks = -1;
        }

        // ---------------------------------------------------------------- the badges

        private static readonly int HotBadges = HotSpots.Slot("Economy: BanksDirector.Badges");
        // The number on each bank's button: its debts that are behind or whose collections call waits for an answer.
        private void Badges()
        {
            using var hotSpot = HotSpots.Time(HotBadges);
            try
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    Badge(_slots[i]);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // (a badge that cannot be counted is left as it was)
            }
        }

        private void Badge(Slot slot)
        {
            EconomyDirector credit = EconomyDirector.Instance;
            if (slot.App == null || slot.View == null || slot.View.Bank == null || credit == null || credit.Desk == null)
            {
                return;
            }
            if (slot.App.Showing)
            {
                slot.App.SetBadge(0);
                return;
            }
            credit.Desk.Held(slot.View.Bank, _work);
            int behind = 0;
            for (int i = 0; i < _work.Count; i++)
            {
                LadderState s = _work[i].Ladder;
                behind += s.Misses > 0 || s.CallDue ? 1 : 0;
            }
            slot.App.SetBadge(behind);
        }

        // ---------------------------------------------------------------- for the test tools

        /// <summary>
        /// Test: each bank app's figures against the Finance app's. What every bank's Loans tab adds up, with what no bank
        /// holds, must be what the Finance app counts in all; and under the lending threshold every bank's Apply page
        /// must refuse. One line in Player.log and a notice.
        /// </summary>
        public void TestCheck()
        {
            try
            {
                EconomyDirector credit = EconomyDirector.Instance;
                CreditFile file = CreditLedger.File;
                GameInstance save = SaveGameManager.Current;
                if (credit == null || credit.Desk == null || !credit.Desk.Running || file == null || save == null)
                {
                    Notice(NotificationType.Error, "reallife_bank_test_not_running", null);
                    return;
                }
                credit.BringUpToDate("the bank test");
                IReadOnlyList<Lender> banks = BankTerms.Banks;
                float sliced = 0f, tomorrow = 0f;
                string detail = string.Empty;
                for (int i = 0; i < banks.Count; i++)
                {
                    BankSlice slice = credit.Desk.Slice(banks[i], _work);
                    sliced += slice.Owed;
                    tomorrow += slice.Tomorrow;
                    detail += (i > 0 ? "; " : string.Empty) + banks[i].Name + " " + slice.Debts + " debt(s), " + slice.Owed.ToString("0") + " owed, " + slice.Tomorrow.ToString("0") + " tomorrow";
                }
                // What the Finance app counts: the game's loans, and the mod's accounts (not a test tool's); and what no bank of the city holds.
                float whole = 0f, nobody = 0f;
                for (int i = 0; i < save.Loans.Count; i++)
                {
                    whole += Mathf.Max(0f, save.Loans[i].remainingAmount);
                    var at = save.Loans[i].bankAddress;
                    if (at == null || BankTerms.BankOf(CreditStore.Clean(at.streetName), at.streetNumber) == null)
                    {
                        nobody += Mathf.Max(0f, save.Loans[i].remainingAmount);
                    }
                }
                for (int i = 0; i < file.Accounts.Count; i++)
                {
                    CreditAccount a = file.Accounts[i];
                    if (a.Closed || a.Test || a.Owed <= 0)
                    {
                        continue;
                    }
                    whole += a.Owed;
                    nobody += BankTerms.BankOf(a.Street, a.Number) == null ? a.Owed : 0f;
                }
                bool sums = Mathf.Abs(sliced + nobody - whole) < 0.5f;
                bool refuses = true;
                for (int i = 0; i < banks.Count; i++)
                {
                    // Under the threshold, whichever bank: nothing to lend, on any product (the desk's own figures at that rating).
                    int below = CreditCatalogue.LendingThreshold - 1;
                    refuses &= CreditDesk.LimitAt(ProductKind.Overdraft, below, banks[i]) == 0 && CreditDesk.LimitAt(ProductKind.CreditCard, below, banks[i]) == 0
                               && CreditDesk.LimitAt(ProductKind.CarFinance, below, banks[i]) == 0 && CreditDesk.LimitAt(ProductKind.Mortgage, below, banks[i]) == 0
                               && !LendingTerms.For(below, BankTerms.OwnCap(banks[i].Settings), BankTerms.OwnRate(banks[i].Settings)).Lends;
                }
                bool ok = sums && refuses;
                Logger?.Info("Bank check (figures): " + banks.Count + " bank(s): " + (detail.Length > 0 ? detail : "none") + ". The banks together " + sliced.ToString("0") + " owed and " + tomorrow.ToString("0")
                             + " tomorrow, held by no bank of the city " + nobody.ToString("0") + ", the Finance app " + whole.ToString("0") + " owed in all"
                             + (sums ? string.Empty : " (they differ)") + "; under " + CreditCatalogue.LendingThreshold + " every bank refuses: " + (refuses ? "yes" : "NO") + ": " + (ok ? "PASS" : "FAIL"));
                Notice(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_bank_test_pass" : "reallife_bank_test_fail", new Dictionary<string, string>
                {
                    { "count", banks.Count.ToString() }, { "owed", FinanceText.Money(whole) }
                });
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: the first bank's app, on its Apply tab.</summary>
        public void TestOpen()
        {
            try
            {
                Slot slot = _slots[0];
                if (slot.App == null || slot.View == null)
                {
                    Notice(NotificationType.Error, "reallife_bank_test_not_running", null);
                    return;
                }
                slot.View.Want(BankApp.Apply);
                slot.App.Open();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void Notice(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 10f, null, null, true, false);
        }
    }
}
