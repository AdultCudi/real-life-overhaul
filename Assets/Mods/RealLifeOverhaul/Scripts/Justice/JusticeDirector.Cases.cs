using System;
using BigAmbitions.DayNightCycle;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    // Cases: opening one, its court date, the warrant, bail and remand, dismissal and closing, and what the police report.
    public sealed partial class JusticeDirector
    {
        /// <summary>Opens a case, or adds to the one opened today. Called through Justice.Arrest and Justice.Summon, and by the tests.</summary>
        public JusticeCase OpenCase(JusticeReport report, bool arrested, bool test)
        {
            if (report == null || report.Charges == null || report.Charges.Length == 0 || !JusticeState.Loaded)
            {
                return null;
            }
            try
            {
                int today = TimeHelper.CurrentDay;
                if (JusticeState.HasWarrant)
                {
                    JusticeState.ServeWarrant = true;   // arrested with a warrant out: that is served too
                }
                // One incident, one case: what is reported on the day a case was opened joins it. Never more than three open.
                JusticeCase c = null;
                for (int i = JusticeState.Cases.Count - 1; i >= 0 && c == null; i--)
                {
                    JusticeCase open = JusticeState.Cases[i];
                    if (open.Open && open.Stage != CaseStage.Warrant && (_hearing == null || _hearing.Case != open)
                        && (open.OpenedDay == today || JusticeState.OpenCount >= MostOpenCases))
                    {
                        c = open;
                    }
                }
                bool joined = c != null;
                if (c == null)
                {
                    if (JusticeState.OpenCount >= MostOpenCases)
                    {
                        Logger?.Info("Justice: " + MostOpenCases + " cases are open already and none can take more; the new charges are not brought.");
                        return null;
                    }
                    c = JusticeState.Open(today);
                    c.Seed ^= Random.Range(0, 1 << 20);   // this save's own luck, kept in the save: a reload changes nothing
                }
                c.Witnesses = Mathf.Max(c.Witnesses, report.Witnesses);
                c.Cameras |= report.Cameras;
                c.InTheAct |= report.InTheAct;
                c.Fled |= report.Fled;
                c.Complied = joined ? c.Complied && report.Complied : report.Complied;
                c.PaidAtScene += Mathf.Max(0, report.PaidAtScene);
                c.Damages += Mathf.Max(0, report.Damages);
                c.HeldHours += Mathf.Max(0, report.HeldHours);
                int added = 0;
                for (int i = 0; i < report.Charges.Length; i++)
                {
                    string id = report.Charges[i];
                    if (JusticeLaw.Find(id) == null || c.Has(id))
                    {
                        continue;
                    }
                    float evidence = report.Evidence != null && i < report.Evidence.Length
                        ? JusticeLaw.Clamp(report.Evidence[i], 0.05f, 0.97f)
                        : JusticeLaw.Strength(report.Witnesses, report.Cameras, report.InTheAct, report.Fled, ((JusticeLaw.Hash(id) + c.Seed) % 11 - 5) / 100f);
                    c.Charges.Add(new JusticeCharge { Id = id, Evidence = evidence });
                    added++;
                }
                if (c.Charges.Count == 0)
                {
                    JusticeState.Cases.Remove(c);
                    return null;
                }
                if (added == 0)
                {
                    return c;
                }
                JusticeCharge main = c.Main;
                if (JusticeState.OnProbation(today) && !c.Has(JusticeLaw.Violation) && main != null)
                {
                    c.Charges.Add(new JusticeCharge { Id = JusticeLaw.Violation, Evidence = main.Evidence });   // it stands or falls with the main charge
                }
                Logger?.Info("Justice: case " + c.Number + (joined ? " added to" : " opened") + " (" + (arrested ? "arrest" : "summons") + (test ? ", test" : string.Empty)
                             + "): " + Describe(c) + ".");
                if (joined)
                {
                    return c;
                }
                if (arrested)
                {
                    c.Stage = CaseStage.Bail;   // put to the player as soon as they are free to hear it
                    JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_arrested", "charge", JusticeText.Charge(main.Id), 10f);
                }
                else
                {
                    SetDate(c, today);
                    JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_summons", "charge", JusticeText.Charge(main.Id), 10f);
                }
                return c;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        private static string Describe(JusticeCase c)
        {
            var text = new System.Text.StringBuilder(96);
            for (int i = 0; i < c.Charges.Count; i++)
            {
                text.Append(i > 0 ? ", " : string.Empty).Append(c.Charges[i].Id).Append(' ').Append(Mathf.RoundToInt(c.Charges[i].Evidence * 100f)).Append('%')
                    .Append(c.Charges[i].Dropped ? " (dropped)" : string.Empty);
            }
            text.Append("; witnesses ").Append(c.Witnesses).Append(c.Cameras ? ", filmed" : string.Empty).Append(c.InTheAct ? ", in the act" : string.Empty)
                .Append(c.Fled ? ", fled" : string.Empty).Append(c.Complied ? ", complied" : string.Empty);
            return text.ToString();
        }

        private void SetDate(JusticeCase c, int today)
        {
            c.Stage = CaseStage.Pending;
            c.CourtDay = today + JusticeLaw.DaysToCourt(c.Weight);
            c.Reminded = 0;
            LegalPhone.Send("reallife_text_justice_date", Data("number", JusticeText.N(c.Number), "day", JusticeText.N(c.CourtDay), "place", CourtPlaceName()));
            Logger?.Info("Justice: case " + c.Number + " is to be heard on day " + c.CourtDay + ".");
        }

        private void IssueWarrant(JusticeCase c, int today)
        {
            c.Stage = CaseStage.Warrant;
            c.WarrantDay = today;
            if (!c.Has(JusticeLaw.NoShow))
            {
                c.Charges.Add(new JusticeCharge { Id = JusticeLaw.NoShow, Evidence = 0.95f });
            }
            int forfeited = c.BailPaid;
            c.BailPaid = 0;
            LegalPhone.Send("reallife_text_justice_warrant", Data("number", JusticeText.N(c.Number), "amount", JusticeText.Money(forfeited)));
            JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_warrant", 12f);
            Logger?.Info("Justice: case " + c.Number + " was not attended; bench warrant issued" + (forfeited > 0 ? ", bail of " + forfeited + " forfeited." : "."));
        }

        /// <summary>What a wrongly arrested player is paid: a flat sum, so much an hour held, and the roadside fine back.</summary>
        public int CompensationFor(JusticeCase c)
        {
            return (JusticeSettings.Aftermath ? JusticeLaw.CompensationFlat + JusticeLaw.CompensationPerHour * Mathf.Min(c.HeldHours, JusticeLaw.CustodyCapHours) : 0)
                   + Mathf.Max(0, c.PaidAtScene);
        }

        /// <summary>The case is dropped for want of evidence: closed, compensated, off the record.</summary>
        public void Dismiss(JusticeCase c, bool announce)
        {
            if (c == null || !c.Open)
            {
                return;
            }
            int compensation = CompensationFor(c);
            if (compensation > 0)
            {
                Crime.Give(compensation, "reallife_transaction_compensation");
                JusticeState.Compensated += compensation;
            }
            int bail = CloseCase(c, CaseOutcome.Dismissed);
            ModState.AddModifier(ModState.MoodUp);
            if (announce)
            {
                LegalPhone.Send("reallife_text_justice_dismissed", Data("number", JusticeText.N(c.Number), "amount", JusticeText.Money(compensation + bail)));
            }
            JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_dismissed", "amount", JusticeText.Money(compensation + bail), 12f);
            Logger?.Info("Justice: case " + c.Number + " dismissed; " + compensation + " paid in compensation, " + bail + " of bail returned"
                         + (c.Damages > 0 ? "; the " + c.Damages + " of damages it held are never charged." : "."));
        }

        // Bail comes back: they appeared, or there was nothing to appear for. Returns the bail returned.
        private int CloseCase(JusticeCase c, CaseOutcome outcome)
        {
            c.Stage = CaseStage.Closed;
            c.Outcome = outcome;
            c.ClosedDay = TimeHelper.CurrentDay;
            int bail = c.BailPaid;
            c.BailPaid = 0;
            if (bail > 0)
            {
                Crime.Give(bail, "reallife_transaction_bail_refund");
            }
            if (JusticeState.HearNow == c.Number)
            {
                JusticeState.HearNow = 0;
            }
            if (!c.WasConviction)
            {
                CallBridge(() => Justice.Bridges.Law.Cleared(c.Number));
            }
            if (JusticeState.OpenCount == 0)
            {
                JusticeState.ClearLawyer();
                JusticeState.ServeWarrant = false;
            }
            return bail;
        }

        private void ShowBail(JusticeCase c)
        {
            int today = TimeHelper.CurrentDay;
            int fines = 0;
            for (int i = 0; i < c.Charges.Count; i++)
            {
                ChargeDef def = c.Charges[i].Dropped ? null : JusticeLaw.Find(c.Charges[i].Id);
                fines += def != null ? def.Fine : 0;
            }
            JusticeLaw.BailKind kind = JusticeLaw.BailFor(c.Weight, Justice.Priors, c.Fled, c.Has(JusticeLaw.NoShow));
            float cash = Crime.Cash;
            int amount = JusticeLaw.BailAmount(Prices.At(fines), cash);   // the charges' fines at today's prices
            int bond = Mathf.Max(Prices.At(50), JusticeLaw.Round10(amount * 0.1f));
            string body = JusticeText.T("reallife_justice_bail_open", "number", JusticeText.N(c.Number), "charges", LegalFile.ChargeList(c)) + "\n\n";
            var page = new JusticePage
            {
                Scene = JusticeScene.Court, Title = JusticeText.T("reallife_justice_bail_title"),
                Subtitle = JusticeText.T("reallife_justice_court_subtitle", "number", JusticeText.N(c.Number), "day", JusticeText.N(today)),
                Middle = JusticeFace.Judge, MiddleName = JusticeText.T("reallife_justice_bail_magistrate"), Speaker = 1,
                SpeakerName = JusticeText.T("reallife_justice_bail_magistrate")
            };
            _hold.Pause();
            _hold.Block();
            _bailOpen = true;
            if (kind == JusticeLaw.BailKind.OwnRecognisance)
            {
                page.Body = body + JusticeText.T("reallife_justice_bail_recognisance", "days", JusticeText.N(JusticeLaw.DaysToCourt(c.Weight)));
                page.Add(JusticeText.T("reallife_justice_continue"));
                JusticePanel.Instance.Show(page, _ =>
                {
                    CloseBail();
                    SetDate(c, today);
                });
                return;
            }
            if (kind == JusticeLaw.BailKind.Remand)
            {
                page.Body = body + JusticeText.T("reallife_justice_bail_refused");
                page.Add(JusticeText.T("reallife_justice_continue"));
                JusticePanel.Instance.Show(page, _ =>
                {
                    CloseBail();
                    Remand(c);
                });
                return;
            }
            bool canPay = cash - amount >= JusticeLaw.CashFloor;
            page.Body = body + JusticeText.T("reallife_justice_bail_set", "amount", JusticeText.Money(amount), "days", JusticeText.N(JusticeLaw.DaysToCourt(c.Weight)));
            page.Add(JusticeText.T("reallife_justice_bail_pay", "amount", JusticeText.Money(amount)),
                    JusticeText.T(canPay ? "reallife_justice_bail_pay_hint" : "reallife_justice_bail_pay_hint_short"))
                .Add(JusticeText.T("reallife_justice_bail_bond", "amount", JusticeText.Money(bond)), JusticeText.T("reallife_justice_bail_bond_hint"))
                .Add(JusticeText.T("reallife_justice_bail_stay"), JusticeText.T("reallife_justice_bail_stay_hint"));
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == 2)
                {
                    CloseBail();
                    Remand(c);
                    return;
                }
                if (choice == 0 && canPay && Crime.Pay(amount, "reallife_transaction_bail"))
                {
                    c.BailPaid = amount;
                }
                else
                {
                    // The bondsman: a tenth, not returned; owed over a few days if it cannot be paid on the spot.
                    c.Bond = true;
                    if (!(Crime.Cash - bond >= JusticeLaw.CashFloor && Crime.Pay(bond, "reallife_transaction_bail_bond")))
                    {
                        JusticeState.AddDebt(JusticeDebt.Fee, bond, 3, c.Number);
                    }
                }
                CloseBail();
                SetDate(c, today);
            });
        }

        private void CloseBail()
        {
            _bailOpen = false;
            JusticePanel.Instance.Close();
            _hold.ReleaseAll(this);
        }

        // Held until the court next sits (never longer than a day), then heard at once.
        private void Remand(JusticeCase c)
        {
            int hour = TimeHelper.CurrentHour;
            bool sitting = hour >= JusticeLaw.CourtOpens && hour < JusticeLaw.CourtCloses - 1;
            int hours = sitting ? 2 : hour < JusticeLaw.CourtOpens ? (int)JusticeLaw.CourtOpens - hour : 24 - hour + (int)JusticeLaw.CourtOpens;
            hours = Mathf.Clamp(hours, 2, 24);
            c.Remand = true;
            c.Stage = CaseStage.Pending;
            c.HeldHours += hours;
            Timestamp release = TimeHelper.Now();
            release.AddDays(hours / 24);   // the game's AddHours carries over one day only
            release.AddHours(hours % 24);
            c.CourtDay = release.Day;
            c.Reminded = 7;
            JusticeState.HearNow = c.Number;
            JusticeState.JailHoursPending = hours;
            Logger?.Info("Justice: case " + c.Number + ": held " + hours + " h until the court sits, then heard.");
        }

        private void ServeWarrant()
        {
            JusticeState.ServeWarrant = false;
            JusticeCase c = null;
            for (int i = 0; i < JusticeState.Cases.Count && c == null; i++)
            {
                if (JusticeState.Cases[i].Stage == CaseStage.Warrant)
                {
                    c = JusticeState.Cases[i];
                }
            }
            if (c == null)
            {
                return;
            }
            JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_warrant_served", "number", JusticeText.N(c.Number), 10f);
            Remand(c);
        }

        public void OnPoliceContact(bool driving)
        {
            try
            {
                if (JusticeState.HasWarrant && !JusticeState.ServeWarrant)
                {
                    JusticeState.ServeWarrant = true;
                    Logger?.Info("Justice: police contact with a bench warrant out; the player is taken in when the stop is over.");
                }
                int today = TimeHelper.CurrentDay;
                if (driving && Justice.DrivingSuspended && _lastUnlicensedDay != today)
                {
                    _lastUnlicensedDay = today;
                    OpenCase(new JusticeReport(JusticeLaw.Unlicensed) { Witnesses = 1, InTheAct = true, Complied = true }, arrested: false, test: false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Hours in a cell on an arrest another feature made: begun by Attend, and counted as time served in today's case.</summary>
        public bool HoldFor(int hours)
        {
            hours = Mathf.Min(hours, JusticeLaw.CustodyCapHours - JusticeState.JailHoursPending);
            if (hours <= 0 || JusticeState.InPrison)
            {
                return false;
            }
            int today = TimeHelper.CurrentDay;
            for (int i = JusticeState.Cases.Count - 1; i >= 0; i--)
            {
                if (JusticeState.Cases[i].Open && JusticeState.Cases[i].OpenedDay == today)
                {
                    JusticeState.Cases[i].HeldHours += hours;
                    break;
                }
            }
            JusticeState.JailHoursPending += hours;
            Logger?.Info("Justice: held " + hours + " h on an arrest made elsewhere.");
            return true;
        }

        /// <summary>Out of the police cell: what was paid to walk out is the case's bail. Only a case the court would remand stays at bail.</summary>
        public void OnReleasedByPolice(JusticeCase c, int bailPaid)
        {
            if (c == null || !c.Open || c.Stage != CaseStage.Bail)
            {
                return;
            }
            c.BailPaid += Mathf.Max(0, bailPaid);
            if (JusticeLaw.BailFor(c.Weight, Justice.Priors, c.Fled, c.Has(JusticeLaw.NoShow)) != JusticeLaw.BailKind.Remand)
            {
                SetDate(c, TimeHelper.CurrentDay);
            }
        }
    }
}
