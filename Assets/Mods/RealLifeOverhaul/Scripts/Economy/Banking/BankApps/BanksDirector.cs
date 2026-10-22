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
    /// The Banks app: one app on the phone for the city's three largest banks (<see cref="BankApp"/>), the bank picked at
    /// the head of its box with the game's own selector, its five tabs the same for each bank. (Until the phone had
    /// folders each bank had an app of its own: three buttons that were one app in all but the bank. They are one now, as a
    /// phone's banking app holds every account.) No save data.
    /// Cost: nothing per frame; once a second a few comparisons and the app built at most once, and the badge after the economy's hour and day.
    /// </summary>
    public sealed class BanksDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeBanks";

        private static readonly string[] AppGlyphs = { "Icon-Coin", "Icon-Money", "Icon-Security" };
        // the icon's two colours, top and bottom: a plain banking blue, no real bank's
        private static readonly Color32 Top = new Color32(96, 156, 232, 255);
        private static readonly Color32 Bottom = new Color32(34, 82, 170, 255);
        private static readonly int HotSecond = HotSpots.Slot("Economy: BanksDirector.Second");
        private static readonly int HotBadges = HotSpots.Slot("Economy: BanksDirector.Badges");

        public static IModLogger Logger;
        public static BanksDirector Instance { get; private set; }

        private PhoneApp _app;
        private BankApp _view;
        private readonly List<Lender> _banks = new List<Lender>(BankRules.AppsMost);   // the banks the app shows, largest first
        private readonly List<Debt> _work = new List<Debt>(16);
        private Action _second, _onHour, _onDay;
        private bool _gaveUp, _broken;
        private int _toldBanks = -1;    // how many banks the last line about the city's banks named

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            BankApp.Logger = Logger;
            _second = EachSecond;
            _onHour = Badges;
            _onDay = Badges;
            EconomyDirector.Follow(StartSeats.Banks, _second);   // its first second after a load gets a frame of its own (Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            Remove(null);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);
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
                Remove(null);
                Logger?.Warn("Banks: the Banks app is switched off for this session after an error. Every account and loan is as it was, and the Finance app still shows and offers everything.");
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
                Remove(!Settings.EconomyBanks ? "the option is off" : "the credit rating is not running");
                return;
            }
            if (_gaveUp)
            {
                return;
            }
            IReadOnlyList<Lender> banks = BankTerms.Banks;
            int wanted = Mathf.Min(BankRules.AppsMost, banks.Count);
            if (wanted == 0)
            {
                Remove("the city has no bank");
                return;
            }
            if (Changed(banks, wanted))
            {
                _banks.Clear();
                for (int i = 0; i < wanted; i++)
                {
                    _banks.Add(banks[i]);   // (a bank read again is the new branch object: the app follows it)
                }
                _view?.Offer(_banks);
            }
            if (_app == null)
            {
                Install(credit);
                return;
            }
            if (_toldBanks != banks.Count)
            {
                _toldBanks = banks.Count;
                Logger?.Info("Banks: " + banks.Count + " bank(s) in the city, " + wanted + " in the Banks app"
                             + (banks.Count < BankRules.AppsMost ? " (the city has fewer than " + BankRules.AppsMost + " banks)" : string.Empty) + Names(wanted) + ".");
            }
        }

        // Whether the banks the app shows are not the city's (another bank, or a bank's branch object read again).
        private bool Changed(IReadOnlyList<Lender> banks, int wanted)
        {
            if (_banks.Count != wanted)
            {
                return true;
            }
            for (int i = 0; i < wanted; i++)
            {
                if (!ReferenceEquals(_banks[i], banks[i]))
                {
                    return true;
                }
            }
            return false;
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

        private void Install(EconomyDirector credit)
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new BankApp(credit, AppId);
            view.Offer(_banks);
            bool tryAgain;
            PhoneApp app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_banks_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), Top, Bottom, 0.42f, "AppIcon-Banks"),
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
                    Logger?.Warn("Banks: the game's phone is not built as this version expects, so the Banks app is left off. The Finance app still shows and offers everything.");
                }
                return;
            }
            _app = app;
            _view = view;
            Badges();
            if (Verbose)
            {
                Logger?.Info("Bank check (app): the Banks app is on the phone as " + AppId + " with " + _banks.Count + " bank(s)" + Names(_banks.Count) + ": PASS");
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

        private void Remove(string why)
        {
            _toldBanks = -1;
            _banks.Clear();
            if (_app == null)
            {
                return;
            }
            if (Verbose && why != null)
            {
                Logger?.Info("Banks: the Banks app is taken off the phone (" + why + ").");
            }
            _app.Remove();
            _app = null;
            _view = null;
        }

        // The number on the app's button: the debts behind, or with a collections call waiting, at every bank it shows.
        private void Badges()
        {
            using var hotSpot = HotSpots.Time(HotBadges);
            try
            {
                Badge();
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // a badge that cannot be counted is left as it was
            }
        }

        private void Badge()
        {
            EconomyDirector credit = EconomyDirector.Instance;
            if (_app == null || credit == null || credit.Desk == null)
            {
                return;
            }
            if (_app.Showing)
            {
                _app.SetBadge(0);
                return;
            }
            int behind = 0;
            for (int b = 0; b < _banks.Count; b++)
            {
                credit.Desk.Held(_banks[b], _work);
                for (int i = 0; i < _work.Count; i++)
                {
                    LadderState s = _work[i].Ladder;
                    behind += s.Misses > 0 || s.CallDue ? 1 : 0;
                }
            }
            _app.SetBadge(behind);
        }

        /// <summary>Test: every bank's figures with what no bank holds must be what the Finance app counts, and under the threshold every bank refuses.</summary>
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
                // what the Finance app counts (the game's loans and the mod's accounts, not a test tool's) and what no bank of the city holds
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
                int below = CreditCatalogue.LendingThreshold - 1;
                for (int i = 0; i < banks.Count; i++)
                {
                    refuses &= CreditDesk.LimitAt(ProductKind.Overdraft, below, banks[i]) == 0 && CreditDesk.LimitAt(ProductKind.CreditCard, below, banks[i]) == 0
                               && CreditDesk.LimitAt(ProductKind.CarFinance, below, banks[i]) == 0 && CreditDesk.LimitAt(ProductKind.Mortgage, below, banks[i]) == 0
                               && !LendingTerms.For(below, BankTerms.OwnCap(banks[i].Settings), BankTerms.OwnRate(banks[i].Settings)).Lends;
                }
                bool ok = sums && refuses;
                Logger?.Info("Bank check (figures): " + banks.Count + " bank(s): " + (detail.Length > 0 ? detail : "none") + ". The banks together " + sliced.ToString("0") + " owed and "
                             + tomorrow.ToString("0")
                             + " tomorrow, held by no bank of the city " + nobody.ToString("0") + ", the Finance app " + whole.ToString("0") + " owed in all"
                             + (sums ? string.Empty : " (they differ)") + "; under " + CreditCatalogue.LendingThreshold + " every bank refuses: " + (refuses ? "yes" : "NO") + ": "
                                + (ok ? "PASS" : "FAIL"));
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

        /// <summary>Test: the Banks app on the largest bank's Apply tab.</summary>
        public void TestOpen()
        {
            try
            {
                if (_app == null || _view == null)
                {
                    Notice(NotificationType.Error, "reallife_bank_test_not_running", null);
                    return;
                }
                _view.Want(BankApp.Apply, 0);
                _app.Open();
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
