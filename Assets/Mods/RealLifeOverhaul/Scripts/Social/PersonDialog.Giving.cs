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
    /// The call's money: "Help with money" among the other things to say, which offers the ways to help one at a time (Giving's GivingTalk: Give money, Lend money, the rent, a debt, a
    /// business, a course, a car, a home), and the answer to a plea for help (Giving's asks): give what they ask, give less, lend it instead, or say no. The game's own dialogue panel
    /// throughout; the rules and the money are Giving's.
    /// </summary>
    public sealed partial class PersonDialog : IGivingTalkHost
    {
        private GivingTalk _talk;

        private GivingTalk Talk => _talk ?? (_talk = new GivingTalk(this));

        private static GivingDirector Giver => GivingDirector.Instance != null && GivingDirector.Instance.Running ? GivingDirector.Instance : null;

        SocialPerson IGivingTalkHost.Person => _p;

        DialogEntry IGivingTalkHost.Say(string key, Dictionary<string, string> data) => Line(key, data);

        DialogEntry IGivingTalkHost.Ask(string key, Dictionary<string, string> data) => Prompt(key, data);

        DialogEntry IGivingTalkHost.End(string key, Dictionary<string, string> data) => Close(key, data);

        // A plain closing: the result's words, with no voice of theirs.
        DialogEntry IGivingTalkHost.Close(string key, Dictionary<string, string> data)
        {
            _typing = null;
            return PhoneContacts.Closing(key, data);
        }

        // The call's neutral prompt: what is being asked of the player, not something they say.
        private DialogEntry Prompt(string key, Dictionary<string, string> data)
        {
            return new DialogEntry { messageData = key.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        // ---- a plea for help

        private DialogEntry Plea()
        {
            SocialOffer offer = SocialState.Offer;
            GivingDirector giver = Giver;
            GivingNeed need = giver != null ? giver.NeedOfPerson(_p) : GivingNeed.None;
            bool more = giver != null && giver.AsksForMore(_p);
            if (need == GivingNeed.None && !more)
            {
                offer.Clear();
                return Close("reallife_giving_plea_sorted");   // they have sorted it out, or giving is off
            }
            int amount = offer.Amount;
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            string say = more ? "reallife_giving_plea_more" : need == GivingNeed.Work ? "reallife_giving_plea_work" : need == GivingNeed.Debt ? "reallife_giving_plea_debt"
                : "reallife_giving_plea_rent";
            DialogEntry entry = Line(say, data);
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
            return giver == null ? Talk.Closing(GiveResult.Off) : Talk.Settled(giver.GiveToPerson(_p, amount, true), amount, null);
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
                return giver == null ? Talk.Closing(GiveResult.Off) : Talk.Settled(giver.Lend(_p, asked), asked, "reallife_giving_thanks_loan");
            };
            entry.SecondOptionTextOverride = "reallife_giving_btn_say_no";
            entry.OnSecondOption = PleaNo;
            return entry;
        }

        // No: the first time costs nothing, they understand; every later no costs a little affection (GivingRules.NoCost).
        private DialogEntry PleaNo()
        {
            SocialState.Offer.Clear();
            GivingDirector giver = Giver;
            bool free = giver == null || giver.Declined(_p);
            return Close(free ? "reallife_giving_plea_no_first" : "reallife_giving_plea_no");
        }
    }
}
