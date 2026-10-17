using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The economy's three phone contacts, each put on the phone only once the save has met it: "Collections" (after
    /// the first collections call), "Private lender" (after a bank has refused the player or the rating has gone
    /// under the threshold) and "Debt advice" (after debts have stood overdue, or a claim was filed). They are
    /// registered the way the mod's other contacts are, and each conversation is the game's own dialogue panel with
    /// the game's reply buttons; every decision in it is the credit desk's (<see cref="CreditDesk"/>).
    ///
    /// With "Phone contacts" off nothing is added to the phone: a text becomes the game's notification, and a
    /// notification that is clicked opens the same conversation in the game's dialogue panel.
    /// </summary>
    public static class DebtPhone
    {
        public const int Collections = 0, Lender = 1, Advice = 2;
        public static readonly string[] ContactIds = { "reallife-collections", "reallife-lender", "reallife-debt-advice" };

        private static readonly string[] DialogNames = { "reallife_collections_calldialogtype", "reallife_lender_calldialogtype", "reallife_debtadvice_calldialogtype" };
        private static readonly string[] Descriptions = { "reallife_contact_collections_desc", "reallife_contact_lender_desc", "reallife_contact_debt_advice_desc" };
        private static readonly string[] Welcomes = { "reallife_text_collections_welcome", "reallife_text_lender_welcome", "reallife_text_debt_advice_welcome" };
        private static readonly Color[] Colours = { new Color(0.55f, 0.20f, 0.20f), new Color(0.25f, 0.25f, 0.28f), new Color(0.16f, 0.52f, 0.66f) };
        private static readonly Contact[] Contacts = new Contact[3];
        private static readonly bool[] Failed = new bool[3];

        /// <summary>The desk of the loaded save; null while the economy is not running.</summary>
        public static CreditDesk Desk { get; private set; }

        /// <summary>Puts on the phone the contacts this save has met. Asked at the start and whenever one becomes known; one that is there is left alone.</summary>
        public static void Setup(CreditDesk desk)
        {
            Desk = desk;
            CreditFile file = desk != null ? desk.File : null;
            if (file == null)
            {
                return;
            }
            Ensure(Collections, file.Relief.CollectorKnown);
            Ensure(Lender, file.Shark.Known);
            Ensure(Advice, file.Relief.AdviceKnown);
        }

        private static void Ensure(int which, bool known)
        {
            if (!known || Contacts[which] != null || Failed[which] || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                EconomyBridges.ContactIcon?.Invoke(ContactIds[which], Colours[which]);
                Contact contact = Contact.GetContact(ContactIds[which], ContactCategoryName.General, Descriptions[which], null, false, true);
                contact.callDialogTypeOverride = Register(which);   // not saved by the game, so set on every load
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage(Welcomes[which]), notify: false);
                }
                Contacts[which] = contact;
            }
            catch (Exception e)
            {
                Failed[which] = true;
                EconomyDirector.Logger?.Error(e);
                EconomyDirector.Logger?.Warn("Economy: the contact '" + ContactIds[which] + "' could not be added to the phone; its notifications still open the conversation.");
            }
        }

        public static void Forget()
        {
            Desk = null;
            for (int i = 0; i < Contacts.Length; i++)
            {
                Contacts[i] = null;
                Failed[i] = false;
            }
        }

        private static CallDialogType Register(int which)
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogNames[which]);
            switch (which)
            {
                case Collections: CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new CollectionsDialog())); break;
                case Lender: CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new LenderDialog())); break;
                default: CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new DebtAdviceDialog())); break;
            }
            return dialogType;
        }

        /// <summary>The conversation without the phone: the game's own dialogue panel, as its counters open one. Not while another conversation is on.</summary>
        public static void Open(int which)
        {
            try
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                if (Desk == null || uis == null || DialogController.current != null || HudConfirm.isOpen)
                {
                    return;
                }
                uis.playerHUD.dialogUI.ShowDialog(Register(which), NavigationBlocker.SpecialEmployeeDialog);
            }
            catch (Exception e)
            {
                EconomyDirector.Logger?.Error(e);
            }
        }

        /// <summary>A text in a contact's thread. Without the contact it is the game's notification, which opens the conversation when clicked.</summary>
        /// <summary>
        /// RLO Texts (Wiring/TextsPhone): a text about to go out (who it is from, the written line's key, its values, how to
        /// send a line): true when it is held a moment for the model's words and sent through the last argument then (the
        /// words, or the written line). Null or false: sent now, as written.
        /// </summary>
        public static System.Func<string, string, Dictionary<string, string>, System.Action<string, Dictionary<string, string>>, bool> HoldText;

        public static void Text(int which, string key, Dictionary<string, string> data, Action click)
        {
            try
            {
                if (Contacts[which] != null)
                {
                    Contact contact = Contacts[which];
                    if (HoldText == null || !HoldText(which == Lender ? "lender" : "debt", key, data, (k, d) => Send(contact, k, d)))
                    {
                        contact.SendMessage(new TextMessage(key, data), notify: true);
                    }
                    return;
                }
            }
            catch (Exception)
            {
                // the notification below still tells them
            }
            Notifications.Show(NotificationType.Warning, key, data, 14f, null, Guard.Wrap(click), true, false);
        }

        // A text held for its words (RLO Texts), sent now.
        private static void Send(Contact contact, string key, Dictionary<string, string> data)
        {
            try
            {
                contact.SendMessage(new TextMessage(key, data), notify: true);
            }
            catch (Exception)
            {
                // (the contact is gone with the scene: nothing more to tell)
            }
        }

        internal static string Money(float amount)
        {
            return FinanceText.Money(amount);
        }

        /// <summary>
        /// A step of a conversation, kept from throwing into the game's dialogue: a fault is logged and the call ends
        /// with a plain line.
        /// </summary>
        internal static DialogEntry Safe(Func<DialogEntry> step)
        {
            try
            {
                return step();
            }
            catch (Exception e)
            {
                EconomyDirector.Logger?.Error(e);
                return PhoneContacts.Closing("reallife_advice_closed");
            }
        }
    }

    /// <summary>Collections on the line about the first debt whose call is due: pay what is overdue, agree a plan, or neither.</summary>
    public class CollectionsDialog : Dialog
    {
        public CollectionsDialog()
        {
            npcNameKey = DebtPhone.ContactIds[DebtPhone.Collections];
            DialogController.current.ShowEntry(Guard.Entry(DebtPhone.Safe(First)));
        }

        private static DialogEntry First()
        {
            CreditDesk desk = DebtPhone.Desk;
            Debt debt;
            if (desk == null || !desk.Running || !desk.CallAbout(out debt))
            {
                return PhoneContacts.Closing("reallife_collect_nothing");
            }
            LadderState s = debt.Ladder;
            int overdue = Mathf.Min(s.Overdue, debt.Owed);
            var entry = new DialogEntry
            {
                messageData = "reallife_collect_call".Localize(new { lender = debt.Lender, account = debt.Name, amount = DebtPhone.Money(overdue), count = s.Misses.ToString() }),
                ConfirmTextOverride = "reallife_collect_pay".Localize(new { amount = DebtPhone.Money(overdue) }),
                OnConfirm = () => DebtPhone.Safe(() => Pay(debt, overdue)),
                CancelTextOverride = "reallife_collect_cannot",
                OnCancel = () =>
                {
                    DebtPhone.Desk?.CannotPay(debt);
                    PhoneContacts.HangUp();
                }
            };
            if (!s.PlanUsed)
            {
                entry.SecondOptionTextOverride = "reallife_collect_plan";
                entry.OnSecondOption = () => DebtPhone.Safe(() => Plan(debt));
            }
            return entry;
        }

        private static DialogEntry Pay(Debt debt, int overdue)
        {
            CreditDesk desk = DebtPhone.Desk;
            ProductAnswer answer = desk != null ? desk.PayOverdue(debt) : ProductAnswer.Off;
            return answer == ProductAnswer.Yes ? PhoneContacts.Closing("reallife_collect_paid", new { amount = DebtPhone.Money(overdue) })
                : answer == ProductAnswer.NoCash ? PhoneContacts.Closing("reallife_collect_no_cash", new { amount = DebtPhone.Money(overdue) })
                : PhoneContacts.Closing("reallife_collect_nothing");
        }

        private static DialogEntry Plan(Debt debt)
        {
            CreditDesk desk = DebtPhone.Desk;
            ProductAnswer answer = desk != null ? desk.AgreePlan(debt) : ProductAnswer.Off;
            LadderState s = debt.Ladder;
            return answer == ProductAnswer.Yes
                ? PhoneContacts.Closing("reallife_collect_plan_agreed", new { amount = DebtPhone.Money(s.PlanLeft), payment = DebtPhone.Money(s.PlanPerDay), days = ProductCatalogue.PlanDays.ToString() })
                : PhoneContacts.Closing("reallife_collect_plan_used");
        }
    }

    /// <summary>The private lender on the line: what it lends and on what terms, or what is owed, paying it and the one extension.</summary>
    public class LenderDialog : Dialog
    {
        public LenderDialog()
        {
            npcNameKey = DebtPhone.ContactIds[DebtPhone.Lender];
            DialogController.current.ShowEntry(Guard.Entry(DebtPhone.Safe(First)));
        }

        private static DialogEntry First()
        {
            CreditDesk desk = DebtPhone.Desk;
            SharkDebt debt = desk != null ? desk.Shark : null;
            if (debt == null || !desk.Running)
            {
                return PhoneContacts.Closing("reallife_lender_nobody");
            }
            if (debt.Active)
            {
                return Status(desk, debt);
            }
            if (!Settings.EconomyShark)
            {
                return PhoneContacts.Closing("reallife_lender_nobody");
            }
            return Offer(desk.SharkMost);
        }

        // The sums on offer, one at a time: the most first, then half of it, then the least.
        private static DialogEntry Offer(int amount)
        {
            CreditDesk desk = DebtPhone.Desk;
            if (desk == null)
            {
                return PhoneContacts.Closing("reallife_lender_nobody");
            }
            int most = desk.SharkMost;
            amount = Mathf.Clamp(amount, ProductCatalogue.SharkLeast, most);
            int next = amount > ProductCatalogue.SharkLeast ? Mathf.Max(ProductCatalogue.SharkLeast, amount / 2 / ProductCatalogue.SharkStep * ProductCatalogue.SharkStep) : most;
            var entry = new DialogEntry
            {
                messageData = "reallife_lender_offer".Localize(new
                {
                    amount = DebtPhone.Money(amount),
                    back = DebtPhone.Money(SharkRules.OwedOnDueDay(amount)),
                    days = ProductCatalogue.SharkDueDays.ToString(),
                    perDay = DebtPhone.Money(SharkRules.ADay(amount)),
                    cap = DebtPhone.Money(SharkRules.Cap(amount))
                }),
                ConfirmTextOverride = "reallife_lender_take".Localize(new { amount = DebtPhone.Money(amount) }),
                OnConfirm = () => DebtPhone.Safe(() => Lend(amount)),
                CancelTextOverride = "reallife_lender_no",
                OnCancel = PhoneContacts.HangUp
            };
            if (next != amount)
            {
                entry.SecondOptionTextOverride = "reallife_lender_other";
                entry.OnSecondOption = () => DebtPhone.Safe(() => Offer(next));
            }
            return entry;
        }

        private static DialogEntry Lend(int amount)
        {
            CreditDesk desk = DebtPhone.Desk;
            ProductAnswer answer = desk != null ? desk.SharkBorrow(amount) : ProductAnswer.Off;
            return answer == ProductAnswer.Yes
                ? PhoneContacts.Closing("reallife_lender_lent", new { amount = DebtPhone.Money(amount), back = DebtPhone.Money(SharkRules.OwedOnDueDay(amount)), day = desk.Shark.DueDay.ToString() })
                : PhoneContacts.Closing("reallife_lender_nobody");
        }

        private static DialogEntry Status(CreditDesk desk, SharkDebt debt)
        {
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            SharkRules.Grow(debt, today);
            int cash = desk.Wallet.Cash, pay = Mathf.Min(cash, debt.Owed);
            var entry = new DialogEntry
            {
                messageData = (today > debt.DueDay ? "reallife_lender_status_late" : "reallife_lender_status").Localize(new
                {
                    amount = DebtPhone.Money(debt.Owed),
                    day = debt.DueDay.ToString(),
                    perDay = DebtPhone.Money(SharkRules.ADay(debt.Borrowed)),
                    cap = DebtPhone.Money(SharkRules.Cap(debt.Borrowed))
                }),
                CancelTextOverride = "reallife_lender_later",
                OnCancel = PhoneContacts.HangUp
            };
            if (pay > 0)
            {
                entry.ConfirmTextOverride = (pay >= debt.Owed ? "reallife_lender_pay_all" : "reallife_lender_pay_some").Localize(new { amount = DebtPhone.Money(pay) });
                entry.OnConfirm = () => DebtPhone.Safe(() => Pay(pay));
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_lender_nothing_on_me".Localize();
                entry.OnConfirm = () => PhoneContacts.Closing("reallife_lender_bring_it");
            }
            if (!debt.Extended)
            {
                entry.SecondOptionTextOverride = "reallife_lender_extend";
                entry.OnSecondOption = () => DebtPhone.Safe(Extend);
            }
            return entry;
        }

        private static DialogEntry Pay(int amount)
        {
            CreditDesk desk = DebtPhone.Desk;
            int used = 0;
            ProductAnswer answer = desk != null ? desk.SharkPay(amount, out used) : ProductAnswer.Off;
            if (answer != ProductAnswer.Yes)
            {
                return PhoneContacts.Closing("reallife_lender_bring_it");
            }
            return desk.Shark.Active
                ? PhoneContacts.Closing("reallife_lender_paid_some", new { amount = DebtPhone.Money(used), left = DebtPhone.Money(desk.Shark.Owed) })
                : PhoneContacts.Closing("reallife_lender_paid_all", new { amount = DebtPhone.Money(used) });
        }

        private static DialogEntry Extend()
        {
            CreditDesk desk = DebtPhone.Desk;
            int fee = 0;
            ProductAnswer answer = desk != null ? desk.SharkExtend(out fee) : ProductAnswer.Off;
            return answer == ProductAnswer.Yes
                ? PhoneContacts.Closing("reallife_lender_extended", new { fee = DebtPhone.Money(fee), amount = DebtPhone.Money(desk.Shark.Owed), day = desk.Shark.DueDay.ToString() })
                : PhoneContacts.Closing("reallife_lender_no_extension");
        }
    }

    /// <summary>
    /// Debt advice on the line, free: the three ways out with the caller's own figures. It only advises; the two
    /// that change anything are asked for in Finance, under Accounts, where the game's own question box confirms them.
    /// </summary>
    public class DebtAdviceDialog : Dialog
    {
        public DebtAdviceDialog()
        {
            npcNameKey = DebtPhone.ContactIds[DebtPhone.Advice];
            DialogController.current.ShowEntry(Guard.Entry(DebtPhone.Safe(First)));
        }

        private static DialogEntry First()
        {
            CreditDesk desk = DebtPhone.Desk;
            if (desk == null || !desk.Running)
            {
                return PhoneContacts.Closing("reallife_advice_closed");
            }
            int loans, unsecured, secured, shark, other;
            desk.Owed(out loans, out unsecured, out secured, out shark, out other);
            int total = loans + unsecured + secured + shark + other;
            if (total <= 0)
            {
                return PhoneContacts.Closing("reallife_advice_nothing");
            }
            return new DialogEntry
            {
                messageData = "reallife_advice_greeting".Localize(new { amount = DebtPhone.Money(total) }),
                ConfirmTextOverride = "reallife_advice_sell".Localize(),
                OnConfirm = () => DebtPhone.Safe(Sell),
                SecondOptionTextOverride = "reallife_advice_arrange",
                OnSecondOption = () => DebtPhone.Safe(Arrange),
                CancelTextOverride = "reallife_advice_bye",
                OnCancel = PhoneContacts.HangUp
            };
        }

        // What the game itself counts the caller as worth: nothing is sold by the mod.
        private static DialogEntry Sell()
        {
            var wealth = Helpers.PlayerHelper.GetPersonalWealth();
            return new DialogEntry
            {
                messageData = "reallife_advice_sell_page".Localize(new
                {
                    cash = DebtPhone.Money(wealth.cash),
                    investments = DebtPhone.Money(wealth.totalInvestments),
                    assets = DebtPhone.Money(wealth.totalAssets)
                }),
                ConfirmTextOverride = "reallife_advice_arrange".Localize(),
                OnConfirm = () => DebtPhone.Safe(Arrange),
                SecondOptionTextOverride = "reallife_advice_bankrupt",
                OnSecondOption = () => DebtPhone.Safe(Bankrupt),
                CancelTextOverride = "reallife_advice_bye",
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Arrange()
        {
            CreditDesk desk = DebtPhone.Desk;
            ArrangementPlan plan = default(ArrangementPlan);
            ProductAnswer answer = desk != null ? desk.ArrangementOffer(out plan) : ProductAnswer.Off;
            LanguageChangeEventDataHolder words;
            switch (answer)
            {
                case ProductAnswer.Yes:
                    words = "reallife_advice_arrange_page".Localize(new
                    {
                        amount = DebtPhone.Money(plan.Total),
                        payment = DebtPhone.Money(plan.PerDay),
                        days = plan.Days.ToString(),
                        cap = ProductCatalogue.ArrangementScoreCap.ToString(),
                        after = ProductCatalogue.ArrangementCapDaysAfter.ToString(),
                        misses = ProductCatalogue.ArrangementMissesMost.ToString()
                    });
                    break;
                case ProductAnswer.AlreadyOpen: words = "reallife_advice_arranged_already".Localize(); break;
                default: words = "reallife_advice_arrange_none".Localize(); break;
            }
            return new DialogEntry
            {
                messageData = words,
                ConfirmTextOverride = "reallife_advice_bankrupt".Localize(),
                OnConfirm = () => DebtPhone.Safe(Bankrupt),
                CancelTextOverride = "reallife_advice_bye",
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Bankrupt()
        {
            CreditDesk desk = DebtPhone.Desk;
            BankruptcyPlan plan = default(BankruptcyPlan);
            ProductAnswer answer = desk != null ? desk.BankruptcyOffer(out plan) : ProductAnswer.Off;
            switch (answer)
            {
                case ProductAnswer.Yes:
                    return PhoneContacts.Closing("reallife_advice_bankrupt_page", new
                    {
                        amount = DebtPhone.Money(plan.WrittenOff),
                        kept = DebtPhone.Money(plan.Kept),
                        cash = DebtPhone.Money(plan.CashTaken),
                        left = DebtPhone.Money(plan.CashLeft),
                        days = ProductCatalogue.BankruptNoCreditDays.ToString(),
                        from = ProductCatalogue.BankruptRebuildFrom.ToString(),
                        every = ProductCatalogue.BankruptEveryDays.ToString()
                    });
                case ProductAnswer.Busy: return PhoneContacts.Closing("reallife_advice_game_quest");
                case ProductAnswer.Bankrupt: return PhoneContacts.Closing("reallife_advice_bankrupt_soon", new { days = ProductCatalogue.BankruptEveryDays.ToString() });
                default: return PhoneContacts.Closing("reallife_advice_bankrupt_none");
            }
        }
    }
}
