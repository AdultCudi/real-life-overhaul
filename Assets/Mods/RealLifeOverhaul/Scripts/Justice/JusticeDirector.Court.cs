using System;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    // The hearing and what follows it: the sentence, fines and their instalments, appeals and civil suits.
    public sealed partial class JusticeDirector
    {
        /// <summary>Asks for a hearing to begin as soon as it is safe (the Legal file's video link, the test button).</summary>
        public void RequestHearing(JusticeCase c, bool remote)
        {
            _hearingWanted = c;
            _hearingRemote = remote;
        }

        private void BeginHearing(JusticeCase c, bool remote)
        {
            HidePrompt();
            _hold.Pause();
            _hold.Block();
            _hearing = new CourtHearing(this, c, remote);
            Logger?.Info("Justice: hearing of case " + c.Number + (remote ? " by video link" : " at the courthouse") + ": " + Describe(c) + "; counsel "
                         + JusticeState.Lawyer + ", priors " + Justice.Priors + ".");
            _hearing.Begin();
        }

        /// <summary>The last page of a hearing has been answered: the panel comes down and the result takes effect.</summary>
        public void HearingOver(CourtHearing hearing)
        {
            if (hearing != _hearing)
            {
                return;
            }
            _hearing = null;
            JusticePanel.Instance.Close();
            _hold.ReleaseAll(this);
            HearingResult result = hearing.Result;
            if (result == null || result.Case == null || !result.Case.Open)
            {
                return;
            }
            JusticeCase c = result.Case;
            int today = TimeHelper.CurrentDay;
            c.LawyerQuality = LegalCounsel.Quality(c);
            CallBridge(() => Justice.Bridges.Heard?.Invoke());
            if (result.Outcome == CaseOutcome.Dismissed)
            {
                Dismiss(c, announce: false);
                return;
            }
            if (result.Outcome == CaseOutcome.Acquitted)
            {
                int back = Mathf.Max(0, c.PaidAtScene);
                if (back > 0)
                {
                    Crime.Give(back, "reallife_transaction_compensation");
                }
                back += CloseCase(c, CaseOutcome.Acquitted);
                ModState.AddModifier(ModState.MoodUpBig);
                JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_acquitted", "amount", JusticeText.Money(back), 12f);
                Logger?.Info("Justice: case " + c.Number + ": acquitted; " + back + " returned.");
                Wiring.Paper.Print("reallife_news_story_acquitted");
                return;
            }

            JusticeSentence s = result.Sentence;
            c.FineImposed = result.FineDue;
            c.PrisonImposed = s.PrisonDays;
            c.JailImposed = s.JailHours;
            c.CommunityImposed = s.CommunityHours;
            c.ProbationImposed = s.ProbationDays;
            c.SuspensionImposed = s.SuspensionDays;
            ImposeFine(c, result.FineDue);
            for (int i = 0; i < c.Charges.Count; i++)
            {
                if (!c.Charges[i].Convicted || c.Charges[i].Dropped)
                {
                    continue;
                }
                string charge = c.Charges[i].Id;
                JusticeState.AddRecord(charge, today, c.Number);
                CallBridge(() => Justice.Bridges.Law.Convicted(charge, today, c.Number));
            }
            JusticeState.LastConvictionDay = today;
            if (s.CommunityHours > 0)
            {
                JusticeState.CommunityHours = Mathf.Min(JusticeLaw.CommunityCapHours, JusticeState.CommunityHours + s.CommunityHours);
                JusticeState.CommunityDue = today + JusticeLaw.CommunityDeadlineDays;
            }
            if (s.ProbationDays > 0 && s.PrisonDays == 0)
            {
                JusticeState.ProbationUntil = Mathf.Max(JusticeState.ProbationUntil, today + s.ProbationDays);   // after prison it runs from the release
            }
            if (s.SuspensionDays > 0)
            {
                int until = today + s.SuspensionDays + s.PrisonDays;
                JusticeState.SuspendedUntil = Mathf.Max(JusticeState.SuspendedUntil, until);
                CallBridge(() => Justice.Bridges.Licences.SuspendDriving(JusticeState.SuspendedUntil));
            }
            CloseCase(c, result.Outcome);
            ModState.AddModifier(ModState.MoodDownBig);
            if (s.PrisonDays > 0)
            {
                JusticeState.ClearPrison();
                JusticeState.PrisonTotal = JusticeState.PrisonOriginal = s.PrisonDays;
                JusticeState.PrisonCase = c.Number;   // served from the next second
                Wiring.Arrests.FamilyHears(3);
            }
            else if (s.JailHours > 0)
            {
                JusticeState.JailHoursPending = s.JailHours;
            }
            Wiring.Paper.Print(s.PrisonDays > 0 ? "reallife_news_story_prison" : "reallife_news_story_convicted");
            LegalPhone.Send("reallife_text_justice_judgment", Data("number", JusticeText.N(c.Number), "outcome", JusticeText.T(LegalFile.OutcomeKey(result.Outcome)),
                "amount", JusticeText.Money(result.FineDue)));
            Logger?.Info("Justice: case " + c.Number + ": " + result.Outcome + "; fine " + result.FineDue + " (nominal " + s.FineNominal + (s.Bounded ? ", bounded" : string.Empty)
                         + "), prison " + s.PrisonDays + " d, custody " + s.JailHours + " h, community " + s.CommunityHours + " h, probation " + s.ProbationDays
                         + " d, licence " + s.SuspensionDays + " d.");
        }

        /// <summary>A case due now that, with no courthouse to go to, can be heard by video link from the Legal file. Null if none.</summary>
        public JusticeCase HearingDueRemotely()
        {
            if (JusticeSettings.Courthouse && _court.Usable)
            {
                return null;
            }
            return DueNow();
        }

        private JusticeCase DueNow()
        {
            int hour = TimeHelper.CurrentHour;
            if (hour < JusticeLaw.CourtOpens || hour >= JusticeLaw.CourtCloses)
            {
                return null;
            }
            int today = TimeHelper.CurrentDay;
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (c.Stage == CaseStage.Warrant || (c.Stage == CaseStage.Pending && c.CourtDay == today))
                {
                    return c;
                }
            }
            return null;
        }

        private void ImposeFine(JusticeCase c, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            float cash = Crime.Cash;
            if (amount <= cash * JusticeLaw.PayNowShare && cash - amount >= JusticeLaw.CashFloor)
            {
                GameManager.ChangeMoneySafe(-amount, new TransactionInfo("reallife_transaction_court_fine"), null, null, force: true);
            }
            else
            {
                JusticeState.AddDebt(JusticeDebt.Fine, amount, JusticeLaw.FinePlanDays, c.Number);
            }
        }

        // A day's instalments, each taken only if it leaves the player something. One missed too many days running is
        // written off (a fine into hours of community service). Nothing grows: no interest, no penalty.
        private void PayInstalments(int today)
        {
            if (JusticeState.Debts.Count == 0)
            {
                return;
            }
            int paid = 0, missed = 0;
            for (int i = JusticeState.Debts.Count - 1; i >= 0; i--)
            {
                JusticeDebt debt = JusticeState.Debts[i];
                int due = Mathf.Min(debt.PerDay, debt.Left);
                if (Crime.Cash - due >= JusticeLaw.CashFloor)
                {
                    GameManager.ChangeMoneySafe(-due, new TransactionInfo(TransactionKey(debt.Kind)), null, null, force: true);
                    debt.Left -= due;
                    debt.Missed = 0;
                    paid += due;
                    if (debt.Left <= 0)
                    {
                        JusticeState.Debts.RemoveAt(i);
                    }
                    continue;
                }
                missed++;
                if (++debt.Missed < JusticeLaw.MissedInstalmentsMost)
                {
                    continue;
                }
                JusticeState.Debts.RemoveAt(i);
                JusticeCase fined = debt.Kind == JusticeDebt.Fine ? JusticeState.Find(debt.CaseNumber) : null;
                if (fined != null)
                {
                    fined.FineImposed = Mathf.Max(0, fined.FineImposed - debt.Left);   // never paid: an appeal won must not hand it back
                }
                if (debt.Kind == JusticeDebt.Fine && JusticeSettings.Community && debt.CaseNumber != JusticeDebt.Final)
                {
                    int hours = Mathf.Min(JusticeLaw.WriteOffHoursMost, (debt.Left + JusticeLaw.WriteOffPerHour - 1) / JusticeLaw.WriteOffPerHour);
                    JusticeState.CommunityHours = Mathf.Min(JusticeLaw.CommunityCapHours, JusticeState.CommunityHours + hours);
                    JusticeState.CommunityDue = today + JusticeLaw.CommunityDeadlineDays;
                    LegalPhone.Send("reallife_text_justice_written_off_hours", Data("amount", JusticeText.Money(debt.Left), "hours", JusticeText.N(hours)));
                }
                else
                {
                    ModState.AddModifier(ModState.MoodDown);
                    LegalPhone.Send("reallife_text_justice_written_off", Data("amount", JusticeText.Money(debt.Left)));
                }
            }
            if (paid > 0)
            {
                JusticeText.Toast(NotificationType.Info, "reallife_justice_toast_instalment", "amount", JusticeText.Money(paid), "left", JusticeText.Money(JusticeState.Owed), 9f);
            }
            else if (missed > 0)
            {
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_toast_instalment_missed", "floor", JusticeText.Money(JusticeLaw.CashFloor), 9f);
            }
        }

        private static string TransactionKey(int kind)
        {
            return kind == JusticeDebt.Fee ? "reallife_transaction_legal_fee" : kind == JusticeDebt.Civil ? "reallife_transaction_civil" : "reallife_transaction_court_fine";
        }

        /// <summary>The Legal file's "pay everything now". Returns what to say.</summary>
        public string PayOffAll()
        {
            int total = JusticeState.Owed;
            if (total <= 0 || Crime.Cash - total < JusticeLaw.CashFloor)
            {
                return JusticeText.T("reallife_justice_act_pay_all_short");
            }
            for (int i = JusticeState.Debts.Count - 1; i >= 0; i--)
            {
                GameManager.ChangeMoneySafe(-JusticeState.Debts[i].Left, new TransactionInfo(TransactionKey(JusticeState.Debts[i].Kind)), null, null, force: true);
            }
            JusticeState.Debts.Clear();
            Logger?.Info("Justice: everything owed (" + total + ") paid off at once.");
            return JusticeText.T("reallife_justice_act_pay_all_done", "amount", JusticeText.Money(total));
        }

        /// <summary>The chance an appeal of this conviction succeeds, from the evidence on its main charge and the counsel who argued it.</summary>
        public float AppealChance(JusticeCase c)
        {
            float evidence = 0.6f;
            for (int i = 0, most = -1; i < c.Charges.Count; i++)
            {
                ChargeDef def = c.Charges[i].Convicted ? JusticeLaw.Find(c.Charges[i].Id) : null;
                if (def != null && def.Fine > most)
                {
                    most = def.Fine;
                    evidence = c.Charges[i].Evidence;
                }
            }
            return JusticeLaw.AppealChance(evidence, c.LawyerQuality);
        }

        /// <summary>The one appeal a conviction allows. Returns what to say.</summary>
        public string Appeal(JusticeCase c)
        {
            if (c == null || c.Appealed || !c.WasConviction)
            {
                return JusticeText.T("reallife_justice_file_failed");
            }
            int fee = JusticeLaw.AppealFee(c.FineImposed, Crime.Cash);
            if (Crime.Cash - fee < JusticeLaw.CashFloor || !Crime.Pay(fee, "reallife_transaction_legal_fee"))
            {
                return JusticeText.T("reallife_justice_act_appeal_short", "fee", JusticeText.Money(fee));
            }
            c.Appealed = true;
            float chance = AppealChance(c);
            bool won = JusticeLaw.Roll(c.Seed, 500) < Mathf.RoundToInt(chance * 1000f);
            Logger?.Info("Justice: appeal in case " + c.Number + " (fee " + fee + ", chance " + Mathf.RoundToInt(chance * 100f) + "%): " + (won ? "allowed" : "dismissed") + ".");
            if (!won)
            {
                return JusticeText.T("reallife_justice_act_appeal_lost", "fee", JusticeText.Money(fee));
            }
            // Quashed: the fine comes back (what was paid) or is struck off (what was not), the conviction leaves the record,
            // and time served is paid for. With nothing else on the record, probation, suspension and hours go too.
            int unpaid = JusticeState.CancelDebts(c.Number, JusticeDebt.Fine);
            int back = Mathf.Max(0, c.FineImposed - unpaid);
            if (JusticeSettings.Aftermath)
            {
                back += c.PrisonImposed * JusticeLaw.CompensationPerPrisonDay + c.JailImposed * JusticeLaw.CompensationPerHour;
            }
            if (back > 0)
            {
                Crime.Give(back, "reallife_transaction_compensation");
                JusticeState.Compensated += back;
            }
            c.Outcome = CaseOutcome.Quashed;
            JusticeState.RemoveRecord(c.Number);
            CallBridge(() => Justice.Bridges.Law.Cleared(c.Number));
            if (JusticeState.Priors == 0)
            {
                JusticeState.ProbationUntil = 0;
                JusticeState.CommunityHours = 0;
                JusticeState.CommunityDue = 0;
                if (JusticeState.SuspendedUntil > 0)
                {
                    JusticeState.SuspendedUntil = 0;
                    CallBridge(() => Justice.Bridges.Licences.SuspendDriving(0));   // 0: lifted
                }
            }
            ModState.AddModifier(ModState.MoodUpBig);
            return JusticeText.T("reallife_justice_act_appeal_won", "amount", JusticeText.Money(back));
        }

        public void OnCrashVictim(int damages, int injured, int killed)
        {
            try
            {
                int today = TimeHelper.CurrentDay;
                if (!JusticeSettings.Civil || JusticeState.Suits.Count > 0 || (JusticeState.LastSuitDay > 0 && today - JusticeState.LastSuitDay < 7))
                {
                    return;
                }
                float chance = injured + killed > 0 ? 0.5f : damages >= 800 ? 0.2f : 0f;
                if (Random.value >= chance)
                {
                    return;
                }
                Sue(damages, injured, killed, today);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Sue(int damages, int injured, int killed, int today)
        {
            var suit = new JusticeSuit
            {
                Number = JusticeState.NextNumber++, Claim = JusticeLaw.CivilClaim(damages, injured, killed, Crime.Cash), DueDay = today + 5,
                Injury = injured + killed > 0, Seed = JusticeLaw.Hash("suit" + JusticeState.NextNumber) ^ (today * 7919)
            };
            JusticeState.Suits.Add(suit);
            JusticeState.LastSuitDay = today;
            LegalPhone.Send(suit.Injury ? "reallife_text_justice_suit_injury" : "reallife_text_justice_suit_property",
                Data("amount", JusticeText.Money(suit.Claim), "day", JusticeText.N(suit.DueDay)));
            Logger?.Info("Justice: civil suit " + suit.Number + " for " + suit.Claim + " (damages " + damages + ", injured " + injured + ", killed " + killed
                         + "), answer by day " + suit.DueDay + ".");
        }

        /// <summary>The chance of winning a contested suit, 0.10 to 0.70: property claims are easier to fight than injuries, and counsel helps.</summary>
        public float SuitWinChance(JusticeSuit suit)
        {
            return Mathf.Clamp((suit.Injury ? 0.20f : 0.35f) + LegalCounsel.Quality(null) * 0.3f, 0.10f, 0.70f);
        }

        public string SettleSuit(JusticeSuit suit)
        {
            if (!JusticeState.Suits.Remove(suit))
            {
                return JusticeText.T("reallife_justice_file_failed");
            }
            int amount = JusticeLaw.Round10(suit.Claim * 0.6f);
            bool now = Crime.Cash - amount >= JusticeLaw.CashFloor && Crime.Pay(amount, "reallife_transaction_civil");
            if (!now)
            {
                JusticeState.AddDebt(JusticeDebt.Civil, amount, JusticeLaw.CivilPlanDays, 0);
            }
            Logger?.Info("Justice: civil suit " + suit.Number + " settled for " + amount + (now ? " (paid)." : " (over " + JusticeLaw.CivilPlanDays + " days)."));
            return JusticeText.T(now ? "reallife_justice_act_settle_done" : "reallife_justice_act_settle_plan", "amount", JusticeText.Money(amount),
                "days", JusticeText.N(JusticeLaw.CivilPlanDays));
        }

        public string ContestSuit(JusticeSuit suit)
        {
            suit.Contested = true;
            return JusticeText.T("reallife_justice_act_contest_done", "day", JusticeText.N(suit.DueDay), "percent", JusticeText.Percent(SuitWinChance(suit)));
        }

        // The day after its date: a contested suit is decided by its own seed, an ignored one goes by default.
        private void JudgeSuit(JusticeSuit suit)
        {
            if (suit.Contested)
            {
                float chance = SuitWinChance(suit);
                if (JusticeLaw.Roll(suit.Seed, 7) < Mathf.RoundToInt(chance * 1000f))
                {
                    ModState.AddModifier(ModState.MoodUp);
                    LegalPhone.Send("reallife_text_justice_suit_won", Data("amount", JusticeText.Money(suit.Claim)));
                    Logger?.Info("Justice: civil suit " + suit.Number + " contested and won.");
                    return;
                }
                int owed = JusticeLaw.Round10(suit.Claim * 1.15f);
                JusticeState.AddDebt(JusticeDebt.Civil, owed, JusticeLaw.CivilPlanDays, 0);
                LegalPhone.Send("reallife_text_justice_suit_lost", Data("amount", JusticeText.Money(owed), "days", JusticeText.N(JusticeLaw.CivilPlanDays)));
                Logger?.Info("Justice: civil suit " + suit.Number + " contested and lost: " + owed + " over " + JusticeLaw.CivilPlanDays + " days.");
                return;
            }
            JusticeState.AddDebt(JusticeDebt.Civil, suit.Claim, JusticeLaw.CivilPlanDays, 0);
            LegalPhone.Send("reallife_text_justice_suit_default", Data("amount", JusticeText.Money(suit.Claim), "days", JusticeText.N(JusticeLaw.CivilPlanDays)));
            Logger?.Info("Justice: civil suit " + suit.Number + " was not answered: judgment by default for " + suit.Claim + ".");
        }
    }
}
