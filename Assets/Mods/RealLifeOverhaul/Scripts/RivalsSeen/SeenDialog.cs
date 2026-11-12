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
    /// short. Two things can be asked, the business and the car, and each answer says no more than their real figures do. Opened by the director's panel button;
    /// what the greeting is worth was counted before it opened (SeenRules.Hello), so nothing said here earns anything.
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

        private DialogEntry Line(string key, bool last)
        {
            var data = new Dictionary<string, string>(2)
            {
                { "name", _talk != null ? _talk.Name ?? string.Empty : string.Empty }, { "car", _talk != null ? SeenGame.CarName(_talk.Car) : string.Empty }
            };
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
            entry.OnConfirm = () => Line(SeenCatalogue.BusinessTalk[Form], true);
            if (!string.IsNullOrEmpty(_talk.Car))
            {
                entry.SecondOptionTextOverride = "reallife_seen_btn_car";
                entry.OnSecondOption = () => Line(SeenCatalogue.CarTalk[Form], true);
            }
            entry.CancelTextOverride = "reallife_seen_btn_leave";
            entry.OnCancel = Leave;
            return entry;
        }
    }
}
