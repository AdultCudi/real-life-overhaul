using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Finance screen's facts from the save and carries each button to the director or the desk that already owns it, as the old
    /// app did: the same methods, the same questions in the game's own box, the same refusals as notices. Each tab fills only its own
    /// section, so one tab can never show another's parts. Read when the day series, the hour or a button moves the generation, or when the
    /// player's choice changes; nothing a frame.
    /// </summary>
    public sealed partial class FinanceLive : IFinanceSource
    {
        public static readonly FinanceLive Instance = new FinanceLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _local;
        private int _wanted = -1;

        private FinanceLive() { }

        /// <summary>The words the screen writes with: the game's texts, money as the game writes it.</summary>
        public static UtilitiesWords Words() => new UtilitiesWords(UiText.Words, FinanceText.Money, () => Inv);

        private static UtilitiesWords W => Words();

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        /// <summary>A notice asked for a tab: the screen shows it the next time it reads.</summary>
        public static void Want(int tab)
        {
            Instance._wanted = tab;
            Instance._local++;
        }

        public int TakeWanted()
        {
            int tab = _wanted;
            _wanted = -1;
            return tab;
        }

        private void Moved() => _local++;

        // ------------------------------------------------------------------------------------------------- reading

        public void Read(FinanceFacts f, FinanceView v)
        {
            EconomyDirector director = EconomyDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            f.Ready = director != null && director.Running && CreditLedger.Ready && save != null;
            f.RivalsOn = RealLifeOverhaul.Rivals.RivalPage.On();
            f.StandingOn = RealLifeOverhaul.Fame.FamePage.On();
            f.RivalsCount = RivalsCount();
            if (!f.Ready)
            {
                return;
            }
            f.Day = W.F("reallife_econ_day", "Day {day}", "day", save.Day.ToString(Inv));
            ReadScore(f.Score);
            switch (v.Tab)
            {
                case FinanceFace.Credit: ReadCredit(f, v); break;
                case FinanceFace.Borrow: ReadBorrow(f, v); break;
                case FinanceFace.CashFlow: ReadCash(f, v); break;
                case FinanceFace.Accounts: ReadAccounts(f, v); break;
                case FinanceFace.Rivals: ReadRivals(f, v); break;
                case FinanceFace.Standing: ReadStanding(f, v); break;
                default: ReadOverview(f, v); break;
            }
        }

        private static void ReadScore(FinanceScore s)
        {
            CreditScore score = CreditLedger.Score;
            s.Value = score.Value;
            s.Min = CreditCatalogue.ScoreMin;
            s.Max = CreditCatalogue.ScoreMax;
            s.Threshold = CreditCatalogue.LendingThreshold;
            s.Grade = FinanceText.Grade(score.Grade);
            s.Tone = score.Grade >= CreditGrade.Good ? UiRole.Ok : score.Grade == CreditGrade.Fair ? UiRole.Warn : UiRole.Danger;
            s.Lends = score.Lends;
            s.Halves = Halves(score.Value);
            s.HasChange = false;
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RealLifeOverhaul.Rivals.RivalGame.Day;
            if (book != null && day > 1)
            {
                int back = 0;
                for (int b = DaySeriesRules.ScoreWindow; b >= 1 && back == 0; b--)
                {
                    back = book.Get(DaySeriesCatalogue.Score, day - b, out int _) ? b : 0;
                }
                if (back > 0 && DaySeriesRules.Change(book, DaySeriesCatalogue.Score, day, back, out int change))
                {
                    s.HasChange = true;
                    s.Change = change;
                    s.ChangeDays = back;
                }
            }
            s.Sentence = W.F(score.Lends ? "reallife_fin_floor_ok" : "reallife_fin_floor_no",
                score.Lends ? "Banks lend from {threshold}. You are {above} points over it, so they will lend to you."
                    : "Banks lend from {threshold}. You are {below} points under it, so no bank will lend to you now.",
                "threshold", CreditCatalogue.LendingThreshold.ToString(Inv), "above", Math.Max(0, score.Value - CreditCatalogue.LendingThreshold).ToString(Inv),
                "below", Math.Max(0, CreditCatalogue.LendingThreshold - score.Value).ToString(Inv));
        }

        /// <summary>The rating as stars in halves, 0 to 10, from the lowest score to the highest.</summary>
        private static int Halves(int score)
        {
            int halves = (score - CreditCatalogue.ScoreMin) * 10 / (CreditCatalogue.ScoreMax - CreditCatalogue.ScoreMin);
            return halves < 0 ? 0 : halves > 10 ? 10 : halves;
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(FinanceAct act, FinanceView v, int arg)
        {
            try
            {
                bool done = DoAct(act, v, arg);
                if (done)
                {
                    Moved();
                }
                return done;
            }
            catch (Exception e)
            {
                EconomyDirector.Logger?.Warn("Finance (kit): " + act + " failed: " + e.Message);
                return false;
            }
        }

        private bool DoAct(FinanceAct act, FinanceView v, int arg)
        {
            switch (act)
            {
                case FinanceAct.Economy: return EconomyBridges.OpenEconomyApp != null && EconomyBridges.OpenEconomyApp(0);
                case FinanceAct.EconoView: return OpenEconoView();
                case FinanceAct.Take:
                case FinanceAct.Open: return Take(v);
                case FinanceAct.Pay:
                case FinanceAct.PayOff:
                case FinanceAct.Draw:
                case FinanceAct.Close:
                case FinanceAct.CancelApproval:
                case FinanceAct.Arrange:
                case FinanceAct.Bankrupt:
                case FinanceAct.PayOverdue:
                case FinanceAct.AgreePlan:
                case FinanceAct.CannotPay: return DoAccount(act, v);
                case FinanceAct.Export: return Export(v);
                case FinanceAct.Follow:
                case FinanceAct.Counter:
                case FinanceAct.CutPrices:
                case FinanceAct.HoldPrices:
                case FinanceAct.Campaign:
                case FinanceAct.OpenHolding:
                case FinanceAct.ShowList: return DoRival(act, v, arg);
                case FinanceAct.Accept:
                case FinanceAct.Refuse:
                case FinanceAct.Join:
                case FinanceAct.Lobby: return DoStanding(act, v, arg);
                default: return false;
            }
        }

        private static bool OpenEconoView()
        {
            EconomyDirector director = EconomyDirector.Instance;
            if (director == null)
            {
                return false;
            }
            director.KitEconoView();
            return true;
        }

        // ------------------------------------------------------------------------------------------------- shared

        private static string Money(float amount) => FinanceText.Money(amount);

        private static void Tell(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        /// <summary>A desk's answer said as the old app said it: a refusal as the notice with its reason.</summary>
        private static bool Said(ProductAnswer answer)
        {
            if (answer != ProductAnswer.Yes)
            {
                Tell(NotificationType.Error, "reallife_econ_note_not_done", new Dictionary<string, string> { { "why", FinanceApp.Why(answer) } });
                return false;
            }
            return true;
        }

        private static string Rate(int rateBp) => FinanceText.T("reallife_econ_row_rate", "rate", (rateBp / 100f).ToString("0.#", Inv));
    }
}
