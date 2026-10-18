using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The City Ledger's subscriptions desk: the phone contact's dialogue that takes out, cancels and resumes delivery.</summary>
    public static class LedgerDesk
    {
        /// <summary>The desk's first line, with its choices.</summary>
        public static DialogEntry First()
        {
            LedgerDirector d = LedgerDirector.Instance;
            LedgerState s = d != null ? d.State : null;
            if (s == null)
            {
                return Closing("reallife_ledger_desk_later");
            }
            string price = LedgerText.Money(d.WeekPrice);
            if (s.Subscribed && !s.Cancelled)
            {
                return new DialogEntry
                {
                    messageData = "reallife_ledger_desk_subscribed".Localize(Data("address", d.HomeName ?? LedgerText.T("reallife_ledger_desk_no_address"), "paid", s.PaidUntil.ToString(),
                        "next", (s.PaidUntil + 1).ToString(), "price", price)),
                    ConfirmTextOverride = "reallife_ledger_desk_keep".Localize(),
                    OnConfirm = () => Closing("reallife_ledger_desk_kept"),
                    SecondOptionTextOverride = "reallife_ledger_desk_stop",
                    OnSecondOption = Stop,
                    OnCancel = HangUp
                };
            }
            if (s.Subscribed)
            {
                return new DialogEntry
                {
                    messageData = "reallife_ledger_desk_ending".Localize(Data("day", s.PaidUntil.ToString(), "next", (s.PaidUntil + 1).ToString(), "price", price)),
                    ConfirmTextOverride = "reallife_ledger_desk_resume".Localize(),
                    OnConfirm = Resume,
                    CancelTextOverride = "reallife_ledger_desk_no_thanks",
                    OnCancel = HangUp
                };
            }
            string home = d.HomeName;
            if (home == null)
            {
                return Closing("reallife_ledger_desk_no_home");
            }
            return new DialogEntry
            {
                messageData = "reallife_ledger_desk_offer".Localize(Data("address", home, "price", price, "copy", LedgerText.Money(d.CopyPrice),
                    "open", LedgerText.Hour(LedgerCatalogue.KioskOpenHour), "close", LedgerText.Hour(LedgerCatalogue.KioskCloseHour))),
                ConfirmTextOverride = "reallife_ledger_desk_subscribe".Localize(),
                OnConfirm = Subscribe,
                CancelTextOverride = "reallife_ledger_desk_no_thanks",
                OnCancel = HangUp
            };
        }

        private static DialogEntry Subscribe()
        {
            LedgerDirector d = LedgerDirector.Instance;
            if (d == null)
            {
                return Closing("reallife_ledger_desk_later");
            }
            if (d.Subscribe(out int first, out bool noMoney))
            {
                return Closing("reallife_ledger_desk_welcome", Data("day", first.ToString()));
            }
            return noMoney ? Closing("reallife_ledger_desk_no_money", Data("price", LedgerText.Money(d.WeekPrice))) : Closing("reallife_ledger_desk_later");
        }

        private static DialogEntry Stop()
        {
            LedgerDirector d = LedgerDirector.Instance;
            LedgerState s = d != null ? d.State : null;
            if (s == null || !d.Cancel())
            {
                return Closing("reallife_ledger_desk_later");
            }
            return Closing("reallife_ledger_desk_cancelled", Data("day", s.PaidUntil.ToString()));
        }

        private static DialogEntry Resume()
        {
            LedgerDirector d = LedgerDirector.Instance;
            LedgerState s = d != null ? d.State : null;
            if (s == null || !d.Subscribe(out _, out _))
            {
                return Closing("reallife_ledger_desk_later");
            }
            return Closing("reallife_ledger_desk_resumed", Data("next", (s.PaidUntil + 1).ToString(), "price", LedgerText.Money(d.WeekPrice)));
        }

        private static Dictionary<string, string> Data(params string[] pairs)
        {
            return LedgerText.Data(pairs);
        }

        // A closing line: on the phone the call ends by itself, in person there is a leave button.
        private static DialogEntry Closing(string key, object arguments = null)
        {
            bool phone = DialogController.current.dialogType == DialogType.PhoneCall;
            return new DialogEntry
            {
                messageData = key.Localize(arguments),
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : DialogController.current.FinishDialog
            };
        }

        private static void HangUp()
        {
            DialogController.current.CancelDialog();
        }
    }
}
