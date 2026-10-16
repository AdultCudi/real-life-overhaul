using System;
using System.Reflection;
using Player.HUD.ItemInfoOverlays;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The player's courses as buttons of the game's own computer, beside its Play a game and Pass the time.
    ///
    /// A click on a computer opens the game's detailed panel over it; its part for a computer is the object with the
    /// script CustomizableButtonsOverlay, a list of buttons it makes each time the panel is filled (UpdateOverlay): it
    /// throws away the buttons it made last time and copies its template (buttonTemplate) for each of its own. It shows
    /// that part only where the game offers Pass the time: a computer of the player's home with nothing it needs missing
    /// (ShouldShow). Any other child of its list it leaves alone.
    ///
    /// So the mod's two buttons are copies of that template, made once per city and put in the same list: the class
    /// button ("Attend {course}" while an online or distance class of the player's is on, greyed with when its next class
    /// is otherwise) and the homework button ("Do {course} homework" while homework is open). This component sits on the
    /// part's own object: the game switches the part on after it has filled it, which enables this component, and it then
    /// shows the buttons the player's courses call for (Study/ClassPlayRules.AtComputer), after the game's own, or hides
    /// them for a computer away from home and for the golf, tennis and locker panels the same part serves. Pressed, the
    /// panel closes as the game's own buttons close it, and the player walks to that computer (Careers/ComputerClass).
    ///
    /// The cost: nothing between two openings of the panel; on an opening, one look through the player's courses and the
    /// buttons' words written.
    /// </summary>
    public sealed class ComputerButtons : MonoBehaviour
    {
        public const string HomeworkKey = "reallife_study_prompt_homework";

        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "ComputerButtons", typeof(ComputerButtons));   // the game's calls of it (Core/Guard)

        private static ComputerButtons _instance;
        private static bool _failed;

        private CustomizableButtonsOverlay _panel;
        private GameObject _class, _homework;
        private Button _classButton;
        private int _classCourse = -1, _homeworkCourse = -1;

        public static bool Installed => _instance != null;

        /// <summary>
        /// Puts the buttons in the game's computer panel, once per city. False if the panel is not there yet (asked again on
        /// the next look) or not built as expected (said once in Player.log: online classes and homework are then begun
        /// from the Campus app at home only).
        /// </summary>
        public static bool Install()
        {
            if (_instance != null)
            {
                return true;
            }
            if (_failed)
            {
                return false;
            }
            try
            {
                OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
                if (overlays == null)
                {
                    return false;   // the interface is not there yet
                }
                CustomizableButtonsOverlay panel = overlays.GetComponentInChildren<CustomizableButtonsOverlay>(true);
                FieldInfo field = GameContract.ComputerButtonTemplate.Field;   // (resolved once: Core/GameContract)
                Button template = panel != null && field != null ? field.GetValue(panel) as Button : null;
                if (template == null || template.transform.parent == null)
                {
                    _failed = true;
                    UiClone.Fallback("the game's computer panel has no button to copy, so online classes and homework are begun from the Campus app at home");
                    return false;
                }
                using var piece = SessionWatch.Piece("game UI: the computer panel's class buttons being copied from the game's own");
                Transform list = template.transform.parent;
                Transform holder = UiClone.Dormant(list, "RealLifeOverhaul Class Holder");
                GameObject classCopy = UiClone.Asleep(template.gameObject, holder, "RealLifeOverhaul Attend Class");
                GameObject homeworkCopy = UiClone.Asleep(template.gameObject, holder, "RealLifeOverhaul Do Homework");
                Button classButton = UiClone.Rewire(classCopy.transform, ClassClicked);
                UiClone.Rewire(homeworkCopy.transform, HomeworkClicked);
                classCopy.SetActive(false);
                homeworkCopy.SetActive(false);
                classCopy.transform.SetParent(list, false);
                homeworkCopy.transform.SetParent(list, false);
                DestroyImmediate(holder.gameObject);

                _instance = panel.gameObject.AddComponent<ComputerButtons>();
                _instance._panel = panel;
                _instance._class = classCopy;
                _instance._homework = homeworkCopy;
                _instance._classButton = classButton;
                if (Meta.Dev.Verbose)
                {
                    CareersDirector.Logger?.Info("Careers check (computer): 'Attend' and 'Do homework' are buttons of the game's computer panel, copied from its own template: PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                CareersDirector.Logger?.Error(e);
                UiClone.Fallback("the class buttons could not be put in the game's computer panel, so online classes and homework are begun from the Campus app at home");
                return false;
            }
        }

        /// <summary>The buttons and this component out of the game's panel.</summary>
        public static void Remove()
        {
            try
            {
                if (_instance != null)
                {
                    if (_instance._class != null)
                    {
                        Destroy(_instance._class);
                    }
                    if (_instance._homework != null)
                    {
                        Destroy(_instance._homework);
                    }
                    Destroy(_instance);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            _instance = null;
            _failed = false;
        }

        // The game has just filled the panel and switched it on.
        private void OnEnable()
        {
            if (FrameGuard.Off)
            {
                Hide();
                return;
            }
            try
            {
                Decide();
            }
            catch (Exception e)
            {
                Hide();
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void Decide()
        {
            if (_class == null || _homework == null || _panel == null)
            {
                return;
            }
            CareersDirector director = CareersDirector.Instance;
            var computer = _panel.linkedController as ComputerController;
            ComputerOffer offer = director != null && computer != null && Settings.CareersStudy && CareersGame.AtHome() ? director.ComputerOfferNow() : ComputerOffer.None;
            _classCourse = offer.Class;
            _homeworkCourse = offer.Homework;
            if (offer.Class >= 0)
            {
                string course = CareersText.Course(offer.Class);
                UiClone.Words(_class.GetComponentInChildren<TMPro.TMP_Text>(true), offer.ClassNow
                    ? CareersText.T("reallife_study_prompt_attend", "course", course)
                    : CareersText.Fill("reallife_study_desk_next", "course", course, "day", CareersText.Day(offer.NextAt / (24 * 60)), "hour", CareersText.Hour(offer.NextAt % (24 * 60) / 60)));
                if (_classButton != null)
                {
                    _classButton.interactable = offer.ClassNow;
                }
            }
            if (offer.Homework >= 0)
            {
                UiClone.Words(_homework.GetComponentInChildren<TMPro.TMP_Text>(true), CareersText.T(HomeworkKey, "course", CareersText.Course(offer.Homework)));
            }
            Show(_class, offer.Class >= 0);
            Show(_homework, offer.Homework >= 0);
        }

        // After the game's own buttons (it copies its template to the end of the list each time it fills the panel).
        private static void Show(GameObject button, bool show)
        {
            if (show)
            {
                button.transform.SetAsLastSibling();
            }
            if (button.activeSelf != show)
            {
                button.SetActive(show);
            }
        }

        private void Hide()
        {
            if (_class != null && _class.activeSelf)
            {
                _class.SetActive(false);
            }
            if (_homework != null && _homework.activeSelf)
            {
                _homework.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private static void ClassClicked() => Pressed(true);

        private static void HomeworkClicked() => Pressed(false);

        private static void Pressed(bool isClass)
        {
            ComputerButtons buttons = _instance;
            ComputerController computer = buttons != null && buttons._panel != null ? buttons._panel.linkedController as ComputerController : null;
            int course = buttons == null ? -1 : isClass ? buttons._classCourse : buttons._homeworkCourse;
            OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
            if (overlays != null)
            {
                overlays.HideDetailedOverlay();   // as the game's own buttons of this panel do
            }
            CareersDirector director = CareersDirector.Instance;
            if (director == null || course < 0)
            {
                return;
            }
            string why = director.AtComputer(course, isClass ? SittingKind.Class : SittingKind.Homework, computer);
            if (why != null)
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, why, null, 8f);
            }
        }
    }
}
