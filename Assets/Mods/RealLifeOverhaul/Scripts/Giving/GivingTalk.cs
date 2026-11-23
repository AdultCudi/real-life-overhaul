using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// Where a help talk is held: a call with the person (PersonDialog) or the person's own page (PeopleDialog). The talk asks the host to say a line in the person's words, to put a
    /// neutral question to the player, to end the conversation with a line, and to go back to where it began.
    /// </summary>
    public interface IGivingTalkHost
    {
        SocialPerson Person { get; }

        /// <summary>A line in the person's own words, kept in what they said.</summary>
        DialogEntry Say(string key, Dictionary<string, string> data);

        /// <summary>A question put to the player: not something they say.</summary>
        DialogEntry Ask(string key, Dictionary<string, string> data);

        /// <summary>The person's last line, and the conversation ends.</summary>
        DialogEntry End(string key, Dictionary<string, string> data);

        /// <summary>A plain closing text, and the conversation ends.</summary>
        DialogEntry Close(string key, Dictionary<string, string> data);
    }

    /// <summary>
    /// How the player offers money to somebody they know, in the game's own dialogue panel whichever interface is on: the menu of ways to help, one at a time (Give money, Lend money, the
    /// rent, a debt, a business, a course, a car, a home), each with its price shown under the button and the person's answer in their own words. The rules and the money are the
    /// director's (<see cref="GivingDirector"/>); nothing here is kept between presses. COST: nothing per frame; a few lookups when a line is made.
    /// </summary>
    public sealed class GivingTalk
    {
        private const int OptionLend = 100;         // the menu's own codes beside <see cref="HelpKind"/>'s numbers
        private const int OptionPartnerCar = 101;

        /// <summary>Set by Wiring/FamilyLinks: whether the partner can be given a car through the family conversation, and that conversation. Null: not offered.</summary>
        public static Func<SocialPerson, bool> PartnerCarCan;
        public static Func<DialogEntry> PartnerCarOffer;

        private static readonly string[] GiveThanks = { "reallife_giving_thanks_1", "reallife_giving_thanks_2" };
        private readonly IGivingTalkHost _host;
        private readonly SocialPerson _p;
        private readonly int[] _amounts = new int[GivingCatalogue.Steps.Length];
        private readonly HelpKind[] _kinds = new HelpKind[(int)HelpKind.Count];
        private readonly List<int> _options = new List<int>(10);

        public GivingTalk(IGivingTalkHost host)
        {
            _host = host;
            _p = host.Person;
        }

        private static GivingDirector Giver => GivingDirector.Instance != null && GivingDirector.Instance.Running ? GivingDirector.Instance : null;

        /// <summary>Whether there is any money to offer this person now: giving runs and the city's people are there to receive it.</summary>
        public static bool Offered => Giver != null && GivingDirector.CauseWhy == 0;

        // ---- the menu: one way to help at a time

        /// <summary>The ways to help, one at a time: the first is offered, the second button shows the next.</summary>
        public DialogEntry Menu(int at)
        {
            GivingDirector giver = Giver;
            if (giver == null)
            {
                return Closing(GiveResult.Off);
            }
            _options.Clear();
            _options.Add((int)HelpKind.Money);
            _options.Add(OptionLend);
            int n = giver.HelpKinds(_p, _kinds);
            for (int i = 0; i < n; i++)
            {
                _options.Add((int)_kinds[i]);
            }
            if (PartnerCarCan != null && PartnerCarCan(_p))
            {
                _options.Add(OptionPartnerCar);
            }
            int option = _options[at % _options.Count], next = (at + 1) % _options.Count;
            DialogEntry entry = _host.Ask("reallife_giving_help_menu", SocialLife.Data(_p));
            switch (option)
            {
                case (int)HelpKind.Money:
                    entry.ConfirmTextOverride = "reallife_giving_btn_give".Localize();
                    entry.OnConfirm = () => GiveFlow(false, 0);
                    break;
                case OptionLend:
                    entry.ConfirmTextOverride = "reallife_giving_btn_lend".Localize();
                    entry.OnConfirm = () => GiveFlow(true, 0);
                    break;
                case (int)HelpKind.Rent:
                    entry.ConfirmTextOverride = "reallife_giving_btn_pay_rent".Localize();
                    entry.OnConfirm = () => RentFlow(0);
                    break;
                case (int)HelpKind.Debt:
                    entry.ConfirmTextOverride = "reallife_giving_btn_clear_debt".Localize();
                    entry.OnConfirm = () => DebtFlow(0);
                    break;
                case (int)HelpKind.Business:
                    entry.ConfirmTextOverride = "reallife_giving_btn_back_business".Localize();
                    entry.OnConfirm = () => BusinessFlow(0);
                    break;
                case (int)HelpKind.Course:
                    entry.ConfirmTextOverride = "reallife_giving_btn_pay_course".Localize();
                    entry.OnConfirm = CourseFlow;
                    break;
                case (int)HelpKind.Car:
                    entry.ConfirmTextOverride = "reallife_giving_btn_buy_car".Localize();
                    entry.OnConfirm = () => CarFlow(0);
                    break;
                case (int)HelpKind.Home:
                    entry.ConfirmTextOverride = "reallife_giving_btn_buy_home".Localize();
                    entry.OnConfirm = HomeFlow;
                    break;
                default:
                    entry.ConfirmTextOverride = "reallife_giving_btn_buy_car".Localize();
                    entry.OnConfirm = () => PartnerCarOffer != null ? PartnerCarOffer() : Closing(GiveResult.NotNeeded);
                    break;
            }
            if (_options.Count > 1)
            {
                entry.SecondOptionTextOverride = "reallife_social_btn_else";
                entry.OnSecondOption = () => Menu(next);
            }
            return entry;
        }

        // ---- money, and a plain loan: one amount at a time from the steps the cash and the rules allow

        /// <summary>How much? One amount at a time; the other button shows the next.</summary>
        public DialogEntry GiveFlow(bool loan, int at)
        {
            GivingDirector giver = Giver;
            int n = giver != null ? GivingDirector.StepsFor(loan, _amounts) : 0;
            if (n == 0)
            {
                return Closing(GiveResult.Broke);
            }
            int amount = _amounts[at % n];
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = _host.Ask(loan ? "reallife_giving_lend_ask" : "reallife_giving_give_ask", data);
            entry.ConfirmTextOverride = (loan ? "reallife_giving_btn_lend_amount" : "reallife_giving_btn_give_amount").Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(loan ? giver.Lend(_p, amount) : giver.GiveToPerson(_p, amount, false), amount, loan ? "reallife_giving_thanks_loan" : null);
            entry.SecondOptionTextOverride = n > 1 ? "reallife_giving_btn_other_amount" : "reallife_giving_btn_not_now";
            entry.OnSecondOption = n > 1 ? (Func<DialogEntry>)(() => GiveFlow(loan, at + 1)) : () => _host.Close(SocialLines.Pick(SocialCatalogue.ChatBye), SocialLife.Data(_p));
            return entry;
        }

        // ---- the rent: one, three or six months

        private DialogEntry RentFlow(int at)
        {
            GivingDirector giver = Giver;
            if (giver == null || !giver.PriceOf(_p, HelpKind.Rent, at, out int amount, out int options))
            {
                return Closing(GiveResult.NotNeeded);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = _host.Ask("reallife_giving_rent_ask", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_pay_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(giver.GiveHelp(_p, HelpKind.Rent, at, false), amount, "reallife_giving_thanks_rent");
            entry.SecondOptionTextOverride = "reallife_giving_btn_other_amount";
            entry.OnSecondOption = () => RentFlow((at + 1) % options);
            return entry;
        }

        // ---- a debt: all of what they owe (to the most), or half

        private DialogEntry DebtFlow(int at)
        {
            GivingDirector giver = Giver;
            if (giver == null || !giver.PriceOf(_p, HelpKind.Debt, at, out int amount, out int options))
            {
                return Closing(GiveResult.NotNeeded);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = _host.Ask("reallife_giving_debt_ask", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_clear_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(giver.GiveHelp(_p, HelpKind.Debt, at, false), amount, "reallife_giving_thanks_debt");
            entry.SecondOptionTextOverride = "reallife_giving_btn_other_amount";
            entry.OnSecondOption = () => DebtFlow((at + 1) % options);
            return entry;
        }

        // ---- a small business: how much, then a gift or a loan with terms

        private DialogEntry BusinessFlow(int at)
        {
            GivingDirector giver = Giver;
            if (giver == null || !giver.PriceOf(_p, HelpKind.Business, at, out int amount, out int options))
            {
                return Closing(GiveResult.NotNeeded);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = _host.Ask("reallife_giving_business_ask", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_business_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => BusinessTerms(at, amount);
            entry.SecondOptionTextOverride = options > 1 ? "reallife_giving_btn_other_amount" : "reallife_giving_btn_not_now";
            entry.OnSecondOption = options > 1 ? (Func<DialogEntry>)(() => BusinessFlow((at + 1) % options)) : () => _host.Close(SocialLines.Pick(SocialCatalogue.ChatBye), SocialLife.Data(_p));
            return entry;
        }

        private DialogEntry BusinessTerms(int at, int amount)
        {
            GivingDirector giver = Giver;
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            data["parts"] = GivingCatalogue.BusinessParts.ToString();
            data["share"] = (GivingCatalogue.BusinessInterest / 10f).ToString("0.#") + "%";
            data["back"] = SocialLife.Money(amount + GivingHelp.BusinessExtra(amount));
            DialogEntry entry = _host.Ask("reallife_giving_business_terms", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_as_gift".Localize();
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(giver != null ? giver.GiveHelp(_p, HelpKind.Business, at, false) : GiveResult.Off, amount, "reallife_giving_thanks_business");
            entry.SecondOptionTextOverride = "reallife_giving_btn_as_loan";
            entry.OnSecondOption = () => Settled(giver != null ? giver.GiveHelp(_p, HelpKind.Business, at, true) : GiveResult.Off, amount, "reallife_giving_thanks_business_loan");
            entry.Says(DialogButton.Second, "reallife_giving_says_business_loan", "parts", data["parts"], "share", data["share"]);
            return entry;
        }

        // ---- a course

        private DialogEntry CourseFlow()
        {
            GivingDirector giver = Giver;
            if (giver == null || !giver.PriceOf(_p, HelpKind.Course, 0, out int amount, out _))
            {
                return Closing(GiveResult.NotNeeded);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = _host.Ask("reallife_giving_course_ask", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_pay_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(giver.GiveHelp(_p, HelpKind.Course, 0, false), amount, "reallife_giving_thanks_course");
            entry.SecondOptionTextOverride = "reallife_giving_btn_not_now";
            entry.OnSecondOption = () => _host.Close(SocialLines.Pick(SocialCatalogue.ChatBye), SocialLife.Data(_p));
            return entry;
        }

        // ---- a car: three, by the game's own prices

        private DialogEntry CarFlow(int at)
        {
            GivingDirector giver = Giver;
            if (giver == null || !giver.PriceOf(_p, HelpKind.Car, at, out int amount, out int options))
            {
                return Closing(GiveResult.NotNeeded);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            data["car"] = giver.CarName(at);
            DialogEntry entry = _host.Ask("reallife_giving_car_ask", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_buy_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(giver.GiveHelp(_p, HelpKind.Car, at, false), amount, "reallife_giving_thanks_car");
            entry.SecondOptionTextOverride = "reallife_giving_btn_other_car";
            entry.OnSecondOption = () => CarFlow((at + 1) % options);
            return entry;
        }

        // ---- a home: the one they live in

        private DialogEntry HomeFlow()
        {
            GivingDirector giver = Giver;
            if (giver == null || !giver.PriceOf(_p, HelpKind.Home, 0, out int amount, out _))
            {
                return Closing(GiveResult.NotNeeded);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            DialogEntry entry = _host.Ask("reallife_giving_home_ask", data);
            entry.ConfirmTextOverride = "reallife_giving_btn_buy_amount".Localize(data);
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(amount));
            entry.OnConfirm = () => Settled(giver.GiveHelp(_p, HelpKind.Home, 0, false), amount, "reallife_giving_thanks_home");
            entry.SecondOptionTextOverride = "reallife_giving_btn_not_now";
            entry.OnSecondOption = () => _host.Close(SocialLines.Pick(SocialCatalogue.ChatBye), SocialLife.Data(_p));
            return entry;
        }

        // ---- the end of an offer

        /// <summary>A result as the end of the conversation: their thanks in their own words, or why not. <paramref name="thanks"/> is the key of their thanks (null: a plain gift's).</summary>
        public DialogEntry Settled(GiveResult r, int amount, string thanks)
        {
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            if (GivingResults.Refused(r))
            {
                return _host.End(r == GiveResult.Proud ? "reallife_giving_refuse_proud" : r == GiveResult.Needless ? "reallife_giving_refuse_needless" : "reallife_giving_refuse_distant", data);
            }
            if (!GivingResults.Moved(r))
            {
                return Closing(r);
            }
            string key = r == GiveResult.DoneOnly ? "reallife_giving_thanks_only" : thanks ?? SocialLines.Pick(GiveThanks);
            return _host.End(key, data);
        }

        /// <summary>A result as a plain closing: why it did not move.</summary>
        public DialogEntry Closing(GiveResult r) => _host.Close(GivingResults.KeyOf(r), SocialLife.Data(_p));
    }
}
