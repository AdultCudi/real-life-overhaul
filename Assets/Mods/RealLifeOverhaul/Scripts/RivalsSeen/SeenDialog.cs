using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using UI.Smartphone.Apps.Contacts;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// A greeting face to face with a rival, in the game's own dialogue panel as the social life's street talk is: a rival doing well is smug, one in trouble is
    /// short. Two things can be asked, the business and the car, and each answer says no more than their real figures do. A rival met at their head office in office hours
    /// goes on to what they have with the player (<see cref="SeenTalk.Dealings"/>): where the office is and when they are in it, a business of theirs on the market or one they
    /// would take (Services), a tycoon's fund or announced move (Schemes, Rivals), and how well they know the player. Opened by the director's panel button; what the greeting is worth
    /// was counted before it opened (SeenRules.Hello), so nothing said here earns anything, and what a button does is the rest of the mod's own page (a map marker, the fund's page).
    /// </summary>
    public sealed class SeenDialog : Dialog
    {
        private const string DialogName = "reallife_seen_calldialogtype";

        /// <summary>The greeting the next dialogue opens on.</summary>
        internal static SeenTalk Talk;

        private readonly SeenTalk _talk;

        /// <summary>Opens the greeting with somebody standing in the world.</summary>
        public static void Open(SeenTalk talk, ThirdPersonCharacter with)
        {
            Talk = talk;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new SeenDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, null, with);
        }

        public SeenDialog()
        {
            _talk = Talk;
            if (_talk == null)
            {
                DialogController.current.ShowEntry(Guard.Entry(Line("reallife_seen_bye", true)));
                return;
            }
            npcNameKey = _talk.Name;
            DialogController.current.ShowEntry(Guard.Entry(Hello()));
        }

        private int Form => SeenCatalogue.Clamp((int)_talk.Form, 0, (int)SeenForm.Count - 1);

        private static void Leave()
        {
            DialogController.current.CancelDialog();
        }

        private DialogEntry Line(string key, bool last, params string[] more)
        {
            var data = new Dictionary<string, string>(5 + more.Length / 2)
            {
                { "name", _talk != null ? _talk.Name ?? string.Empty : string.Empty }, { "car", _talk != null ? SeenGame.CarName(_talk.Car) : string.Empty },
                { "address", _talk != null ? _talk.Head ?? string.Empty : string.Empty }, { "from", SeenCatalogue.WorkFrom.ToString() }, { "to", SeenCatalogue.WorkTo.ToString() }
            };
            for (int i = 0; i + 1 < more.Length; i += 2)
            {
                data[more[i]] = more[i + 1];
            }
            var entry = new DialogEntry { messageData = key.Localize(data) };
            if (last)
            {
                entry.CancelTextOverride = "reallife_seen_btn_bye";
                entry.OnCancel = Leave;
            }
            return entry;
        }

        private DialogEntry Hello()
        {
            string key = _talk.Known ? SeenCatalogue.HelloKnown[Form] : SeenCatalogue.Hello[Form][SeenCatalogue.Clamp(_talk.Pick, 0, 1)];
            DialogEntry entry = Line(key, false);
            entry.ConfirmTextOverride = "reallife_seen_btn_business".Localize();
            entry.OnConfirm = () => Answer(SeenCatalogue.BusinessTalk[Form]);
            if (!string.IsNullOrEmpty(_talk.Car))
            {
                entry.SecondOptionTextOverride = "reallife_seen_btn_car";
                entry.OnSecondOption = () => Answer(SeenCatalogue.CarTalk[Form]);
            }
            else if (_talk.Dealings)
            {
                entry.SecondOptionTextOverride = "reallife_seen_btn_dealings";
                entry.OnSecondOption = Dealings;
            }
            entry.CancelTextOverride = "reallife_seen_btn_leave";
            entry.OnCancel = Leave;
            return entry;
        }

        // An answer ends the talk, but at their head office it can go on to their dealings.
        private DialogEntry Answer(string key)
        {
            DialogEntry entry = Line(key, true);
            if (_talk.Dealings)
            {
                entry.ConfirmTextOverride = "reallife_seen_btn_dealings".Localize();
                entry.OnConfirm = Dealings;
            }
            return entry;
        }

        // At their head office: where it is and when they are in it; then a business or an offer of theirs, and how well they know the player.
        private DialogEntry Dealings()
        {
            DialogEntry entry = Line("reallife_seen_dealings_open", false);
            entry.ConfirmTextOverride = (IsTycoon ? "reallife_seen_btn_deal_tycoon" : "reallife_seen_btn_deal_owner").Localize();
            entry.OnConfirm = IsTycoon ? (Func<DialogEntry>)Tycoon : Owner;
            entry.SecondOptionTextOverride = "reallife_seen_btn_standing";
            entry.OnSecondOption = () => Line(SeenCatalogue.StandingKeys[SeenCatalogue.Clamp(_talk.Standing, 0, SeenCatalogue.StandingKeys.Length - 1)], true);
            entry.CancelTextOverride = "reallife_seen_btn_leave";
            entry.OnCancel = Leave;
            return entry;
        }

        private bool IsTycoon => _talk.Key != null && _talk.Key.Length > 1 && _talk.Key[0] == 't';

        // One of the game's rival owners: a business of theirs on the market, or one they would take, from Services.
        private DialogEntry Owner()
        {
            SeenOffer o = _talk.Key != null && _talk.Key.Length > 1 && SeenBridges.Offer != null ? SeenBridges.Offer(_talk.Key.Substring(1)) : null;
            if (o == null)
            {
                return Line("reallife_seen_deal_none", true);
            }
            DialogEntry entry = Line(o.Selling ? "reallife_seen_deal_sell" : "reallife_seen_deal_buy", true, "business", o.Business, "other", o.Other, "amount", SeenGame.Money(o.Price),
                "day", o.Deadline.ToString(), "place", o.Address);
            int id = o.Id;
            entry.ConfirmTextOverride = "reallife_seen_btn_show_sale".Localize();
            entry.OnConfirm = () => Then(() => SeenBridges.ShowOffer?.Invoke(id));
            return entry;
        }

        // A tycoon: the fund they have open to the player, or what they have announced.
        private DialogEntry Tycoon()
        {
            int index;
            if (!int.TryParse(_talk.Key.Substring(1), out index))
            {
                return Line("reallife_seen_deal_none_tycoon", true);
            }
            if (SeenBridges.Fund != null && SeenBridges.Fund(index))
            {
                DialogEntry fund = Line("reallife_seen_deal_fund", true);
                fund.ConfirmTextOverride = "reallife_seen_btn_show_fund".Localize();
                fund.OnConfirm = () => Then(() => SeenBridges.ShowFund?.Invoke());
                return fund;
            }
            string move = SeenBridges.Move != null ? SeenBridges.Move(index) : string.Empty;
            return string.IsNullOrEmpty(move) ? Line("reallife_seen_deal_none_tycoon", true) : Line("reallife_seen_deal_move", true, "line", move);
        }

        // The talk ends, then the page the button leads to opens (a map marker, the fund's own page), behind the guard.
        private static DialogEntry Then(Action after)
        {
            Leave();
            try
            {
                after();
            }
            catch (Exception e)
            {
                SeenDirector.Logger?.Error(e);
            }
            return null;
        }
    }
}
