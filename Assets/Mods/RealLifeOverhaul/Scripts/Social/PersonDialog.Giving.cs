using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Giving;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The call's money: "Give money" and "Lend money" among the other things to say, each asking how much with the amounts the cash and the rules allow, and the answer to a plea for
    /// help (Giving's asks): give what they ask, give less, lend it instead, or say no. The game's own dialogue panel throughout; the rules and the money are Giving's.
    /// </summary>
    public sealed partial class PersonDialog
    {
        private readonly int[] _amounts = new int[GivingCatalogue.Steps.Length];

        private static GivingDirector Giver => GivingDirector.Instance != null && GivingDirector.Instance.Running ? GivingDirector.Instance : null;

        // The call's neutral prompt: what is being asked of the player, not something they say.
        private DialogEntry Prompt(string key, Dictionary<string, string> data)
        {
            return new DialogEntry { messageData = key.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        // How much? One amount at a time from the steps the cash and the rules allow; the other button shows the next.
        private DialogEntry GiveFlow(bool loan, int at)
        {
            GivingDirector giver = Giver;
            int n = giver != null ? GivingDirector.StepsFor(loan, _amounts) : 0;
            if (n == 0)
            {
                return Closing(GiveResult.Broke);
            }
            int amount = _amounts[at % n];
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = Prompt(loan ? "reallife_giving_lend_ask" : "reallife_giving_give_ask", data);
            entry.ConfirmTextOverride = (loan ? "reallife_giving_btn_lend_amount" : "reallife_giving_btn_give_amount").Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(loan ? giver.Lend(_p, amount) : giver.GiveToPerson(_p, amount, false), amount, loan);
            entry.SecondOptionTextOverride = n > 1 ? "reallife_giving_btn_other_amount" : "reallife_giving_btn_not_now";
            if (n > 1)
            {
                entry.OnSecondOption = () => GiveFlow(loan, at + 1);
            }
            else
            {
                entry.OnSecondOption = () => Close(SocialLines.Pick(SocialCatalogue.ChatBye));
            }
            return entry;
        }

        private DialogEntry Closing(GiveResult r)
        {
            _typing = null;
            return PhoneContacts.Closing(GivingResults.KeyOf(r), SocialLife.Data(_p));
        }

        // The money has moved, or it has not and the reason is said; their thanks otherwise, in their own words.
        private DialogEntry Settled(GiveResult r, int amount, bool loan)
        {
            if (!GivingResults.Moved(r))
            {
                return Closing(r);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            string key = r == GiveResult.DoneOnly ? "reallife_giving_thanks_only" : loan ? "reallife_giving_thanks_loan" : SocialLines.Pick(GiveThanks);
            return Close(key, data);
        }

        private static readonly string[] GiveThanks = { "reallife_giving_thanks_1", "reallife_giving_thanks_2" };

        // ---- a plea for help

        private DialogEntry Plea()
        {
            SocialOffer offer = SocialState.Offer;
            GivingDirector giver = Giver;
            GivingNeed need = giver != null ? giver.NeedOfPerson(_p) : GivingNeed.None;
            if (need == GivingNeed.None)
            {
                offer.Clear();
                return Close("reallife_giving_plea_sorted");   // they have sorted it out, or giving is off
            }
            int amount = offer.Amount;
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = Line(need == GivingNeed.Work ? "reallife_giving_plea_work" : need == GivingNeed.Debt ? "reallife_giving_plea_debt" : "reallife_giving_plea_rent", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_give_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => PleaGiven(amount);
            entry.SecondOptionTextOverride = "reallife_giving_btn_other_answer";
            entry.OnSecondOption = () => PleaLess(amount);
            return entry;
        }

        private DialogEntry PleaGiven(int amount)
        {
            SocialState.Offer.Clear();
            GivingDirector giver = Giver;
            return giver == null ? Closing(GiveResult.Off) : Settled(giver.GiveToPerson(_p, amount, true), amount, false);
        }

        // Less than they asked: half of it, to the nearest ten.
        private DialogEntry PleaLess(int asked)
        {
            int less = Math.Max(10, asked * GivingCatalogue.LessShare / 1000 / 10 * 10);
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(less));
            DialogEntry entry = Prompt("reallife_giving_plea_less", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_give_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(less));
            entry.OnConfirm = () => PleaGiven(less);
            entry.SecondOptionTextOverride = "reallife_giving_btn_other_answer";
            entry.OnSecondOption = () => PleaLend(asked);
            return entry;
        }

        private DialogEntry PleaLend(int asked)
        {
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(asked));
            DialogEntry entry = Prompt("reallife_giving_plea_lend", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_lend_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(asked));
            entry.OnConfirm = () =>
            {
                SocialState.Offer.Clear();
                GivingDirector giver = Giver;
                return giver == null ? Closing(GiveResult.Off) : Settled(giver.Lend(_p, asked), asked, true);
            };
            entry.SecondOptionTextOverride = "reallife_giving_btn_say_no";
            entry.OnSecondOption = PleaNo;
            return entry;
        }

        // No: a little less warmth, once.
        private DialogEntry PleaNo()
        {
            SocialState.Offer.Clear();
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 0f, -1f, 0f);
            return Close("reallife_giving_plea_no");
        }
    }
}
