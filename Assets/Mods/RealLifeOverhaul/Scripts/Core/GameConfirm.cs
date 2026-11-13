using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own question box, HudConfirm.Show, for every part of the mod, with the game's two calls and
    /// parameters. The mod is compiled against the eight-parameter Show, which older builds lack; on those, Mono could
    /// not compile any method naming it. So it is named only here, in small methods. On a game without it (the contract's
    /// ConfirmByKey, ConfirmByData) the running game's own Show is found by reflection once and called with the same
    /// answers, matched by name, then by place, the rest at their defaults; with none, nothing is shown, logged once.
    /// THE BOX'S ROOM: its panel grows with its text without limit, and a text too long puts its heading and buttons off the
    /// screen while it takes every click. So every body is measured first (Core/BoxFitRules, at the game's UI zoom), and one
    /// the box has no room for is shown cut, with the whole of it in Player.log.
    /// COST: one call more than the game's own and one pass over the body's text; the search runs once, only on a game that
    /// needs it.
    /// </summary>
    public static class GameConfirm
    {
        private const string TestKey = "reallife_test_build_box";
        private const string TestBodyKey = "reallife_test_build_box_body";
        private const string OkKey = "reallife_opt_ok";
        private const string TextKey = "reallife_opt_text";   // "{text}": a body cut here, shown as it is
        private const string CutKey = "reallife_box_cut";     // the words after a body that was cut

        private static bool _direct = true;   // false once the game is found to lack the Show the mod is built with
        private static bool _looked, _toldOwn, _toldNone;
        private static MethodInfo _byKey, _byData;
        private static ParameterInfo[] _byKeyParameters, _byDataParameters;

        /// <summary>From Core/Guard once the contract is resolved: a game lacking the built-in Show goes the way round from the first question.</summary>
        public static void Known()
        {
            if (GameContract.ConfirmByKey.Missing || GameContract.ConfirmByData.Missing)
            {
                _direct = false;
            }
            _noOpen = GameContract.ConfirmOpen.Missing;
            _noListening = GameContract.ConfirmListening.Missing;
            _noPress = GameContract.ConfirmPress.Missing;
            _noZoom = GameContract.UiZoom.Missing;
            _noMenu = GameContract.FullMenuOpen.Missing;
        }

        private static bool _noOpen, _noListening;   // the box's two flags this game lacks
        private static bool _noPress, _noZoom, _noMenu;   // its confirm, the UI zoom and the full menu's flag, where this game lacks them

        // The game's own parameter names, in order.
        private static readonly string[] Names = { "headerKey", "bodyKey", "onConfirmAction", "onCancelAction", "confirmKey", "cancelKey", "allowConfirmationSkip", "isNegative" };
        private static readonly string[] DataNames = { "headerData", "bodyData", "onConfirmAction", "onCancelAction", "confirmKey", "cancelKey", "allowConfirmationSkip", "isNegative" };

        /// <summary>The game's question box, by locale keys.</summary>
        public static void Show(string headerKey = null, string bodyKey = null, Action onConfirmAction = null, Action onCancelAction = null,
            string confirmKey = null, string cancelKey = null, bool allowConfirmationSkip = true, bool isNegative = false)
        {
            TryShow(headerKey, bodyKey, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        /// <summary>The game's question box, with texts that fill in names.</summary>
        public static void Show(LanguageChangeEventDataHolder headerData = default, LanguageChangeEventDataHolder bodyData = default, Action onConfirmAction = null,
            Action onCancelAction = null, string confirmKey = null, string cancelKey = null, bool allowConfirmationSkip = true, bool isNegative = false)
        {
            TryShow(headerData, bodyData, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        /// <summary>As Show; false only when the game has no question box the mod can call.</summary>
        public static bool TryShow(LanguageChangeEventDataHolder headerData, LanguageChangeEventDataHolder bodyData, Action onConfirmAction, Action onCancelAction,
            string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            string cut = bodyData.Key != null ? CutOf(Text(bodyData)) : null;
            if (cut != null)
            {
                bodyData = new LanguageChangeEventDataHolder { Key = TextKey, Arguments = new { text = cut } };
            }
            return Fitted(headerData, bodyData, Answer(onConfirmAction), Answer(onCancelAction), confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        // The body has been measured, and the answers are guarded.
        private static bool Fitted(LanguageChangeEventDataHolder headerData, LanguageChangeEventDataHolder bodyData, Action onConfirmAction, Action onCancelAction,
            string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            if (_direct)
            {
                try
                {
                    DirectData(headerData, bodyData, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
                    return true;
                }
                catch (Exception e) when (Lacks(e))
                {
                    NotDirect(e);
                }
            }
            if (Found() && _byData != null)
            {
                TellOwn(_byDataParameters);
                Call(_byData, _byDataParameters, DataNames, new object[] { headerData, bodyData, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative });
                return true;
            }
            // A game whose Show takes keys only: the texts are put together here and shown as they are.
            return ByKey(headerData.Key != null ? headerData.ToString() : null, bodyData.Key != null ? bodyData.ToString() : null,
                         onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        /// <summary>As Show; false only when the game has no question box the mod can call.</summary>
        public static bool TryShow(string headerKey, string bodyKey, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            string cut = string.IsNullOrEmpty(bodyKey) ? null : CutOf(Text(bodyKey));
            if (cut != null)
            {
                // A cut body is a text, not a key: it goes the way of the texts that fill in names.
                return Fitted(string.IsNullOrEmpty(headerKey) ? default : new LanguageChangeEventDataHolder { Key = headerKey },
                    new LanguageChangeEventDataHolder { Key = TextKey, Arguments = new { text = cut } },
                    Answer(onConfirmAction), Answer(onCancelAction), confirmKey, cancelKey, allowConfirmationSkip, isNegative);
            }
            return ByKey(headerKey, bodyKey, Answer(onConfirmAction), Answer(onCancelAction), confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        // The body as the box will write it; null where it cannot be told, and the box is then left to the game.
        private static string Text(LanguageChangeEventDataHolder bodyData)
        {
            try
            {
                return bodyData.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Text(string bodyKey)
        {
            try
            {
                return bodyKey.GetLocalization();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Null for a body the box has room for; else its beginning with the words that say where the rest is, the whole of it in Player.log.
        private static string CutOf(string body)
        {
            try
            {
                if (string.IsNullOrEmpty(body) || KitCard)
                {
                    return null;   // the RLO interface draws the question itself, wrapping a long body in its own card: the game's box is not the player's view
                }
                float zoom = Zoom;
                bool menuToo = MenuBoxToo;
                if (BoxFitRules.Fits(body, zoom, menuToo))
                {
                    return null;
                }
                string cut = BoxFitRules.Cut(body, zoom, menuToo, CutKey.GetLocalization());
                Say("Question box: a text of " + body.Length + " characters is more than the game's box has room for at a UI zoom of "
                    + zoom.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " (its panel grows with its text, and its buttons would be off the screen), so " + cut.Length + " characters of it are shown. The whole text: "
                    + body.Replace("\r", string.Empty).Replace('\n', ' '));
                return cut;
            }
            catch (Exception e)
            {
                Guard.Logger?.Error(e);
                return null;
            }
        }

        /// <summary>True while the RLO interface draws the game's question boxes as its own card, which has the room the game's box lacks.</summary>
        public static bool KitCard => Ui.UiDirector.On && Ui.UiScreenSwitches.On(QuestionScreenId);

        /// <summary>The kit's question box card (Ui/Shell/ShellBoxes).</summary>
        public const string QuestionScreenId = "question_box";

        /// <summary>The game's UI zoom (its Options' slider); the largest, which leaves a box the least room, where it cannot be read.</summary>
        public static float Zoom
        {
            get
            {
                if (_noZoom)
                {
                    return BoxFitRules.ZoomMost;
                }
                try
                {
                    return BoxFitRules.Zoom(ReadZoom());
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    _noZoom = true;
                    Guard.DoesWithout(e, "the game's UI zoom, for the room of its question box", "the least room is taken");
                    return BoxFitRules.ZoomMost;
                }
            }
        }

        /// <summary>
        /// True where a question may open in the full menu's box, which has less room than the HUD's: with the full menu open,
        /// and where that cannot be read. False in the main menu, which has none.
        /// </summary>
        public static bool MenuBoxToo
        {
            get
            {
                if (_noMenu)
                {
                    return true;
                }
                try
                {
                    return MenuOpen();
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    _noMenu = true;
                    Guard.DoesWithout(e, "whether the game's full menu is open, for the room of its question box", "the least room is taken");
                    return true;
                }
            }
        }

        /// <summary>True when the box is known to be closed: false while it is up, and where that cannot be read.</summary>
        public static bool KnownClosed
        {
            get
            {
                if (_noOpen)
                {
                    return false;
                }
                bool open = IsOpen;
                return !open && !_noOpen;   // the read itself may have found the flag missing
            }
        }

        /// <summary>
        /// Presses the open box's own confirm, as the game's Enter does where it listens for one (its city, its load screen).
        /// False where this game gives no way to.
        /// </summary>
        public static bool Confirm()
        {
            if (_noPress)
            {
                return false;
            }
            try
            {
                return Press();
            }
            catch (Exception e) when (Lacks(e))
            {
                _noPress = true;
                Guard.DoesWithout(e, "closing the game's question box with Esc or Enter in the main menu");
                return false;
            }
        }

        /// <summary>
        /// Makes the open box's confirm button the selected one, so the keys Unity's own interface answers (Enter, Space, a
        /// controller's button) press it. The game clears the selection when it opens the box. Only where the mod reads the
        /// keys itself too (GameBuild.Notice in the main menu): in a city the game's own Enter would press it a second time.
        /// </summary>
        public static void SelectConfirm()
        {
            if (_noPress || GameContract.AskConfirm.Field == null || GameContract.AskContainer.Field == null)
            {
                return;
            }
            try
            {
                Select();
            }
            catch (Exception e) when (Lacks(e))
            {
                _noPress = true;
                Guard.DoesWithout(e, "selecting the confirm button of the game's question box");
            }
            catch (Exception e)
            {
                Guard.Logger?.Error(e);
            }
        }

        // An answer runs inside the guard of the class it is written in: its fault never reaches the game's box.
        private static Action Answer(Action answer)
        {
            return answer != null ? Guard.Wrap(answer) : null;
        }

        private static bool ByKey(string headerKey, string bodyKey, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            if (_direct)
            {
                try
                {
                    Direct(headerKey, bodyKey, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
                    return true;
                }
                catch (Exception e) when (Lacks(e))
                {
                    NotDirect(e);
                }
            }
            if (!Found() || _byKey == null)
            {
                if (!_toldNone)
                {
                    _toldNone = true;
                    Say("Game build: the running game has no question box the mod can call, so the mod's questions are not shown. " + GameBuild.Builds(GameBuild.Running, Meta.Changelog.Version));
                }
                return false;
            }
            TellOwn(_byKeyParameters);
            Call(_byKey, _byKeyParameters, Names, new object[] { headerKey, bodyKey, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative });
            return true;
        }

        /// <summary>True while the game's question box is up; false where that cannot be read.</summary>
        public static bool IsOpen
        {
            get
            {
                if (_noOpen)
                {
                    return false;
                }
                try
                {
                    return Open();
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    _noOpen = true;
                    Guard.DoesWithout(e, "whether the game's question box is up");
                    return false;
                }
            }
        }

        /// <summary>True when the game's question box can be shown (its panel has started); true too where that cannot be read.</summary>
        public static bool Ready
        {
            get
            {
                if (_noListening)
                {
                    return true;
                }
                try
                {
                    return Listening();
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    _noListening = true;
                    Guard.DoesWithout(e, "whether the game's question box is there");
                    return true;
                }
            }
        }

        /// <summary>Options > Tests: a question the way round, through the Show found by reflection.</summary>
        public static void TestOwnBox()
        {
            try
            {
                if (!Found() || _byKey == null)
                {
                    Say("Game build check (test, question box): the running game's own Show taking keys was not found: FAIL.");
                    return;
                }
                Call(_byKey, _byKeyParameters, Names, new object[] { TestKey, TestBodyKey, null, null, OkKey, null, false, false });
                Guard.Logger?.Info("Game build check (test, question box): asked through the running game's own " + Describe(_byKeyParameters) + ", its parameters matched by name: PASS.");
            }
            catch (Exception e)
            {
                Guard.Logger?.Error(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Direct(string headerKey, string bodyKey, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            HudConfirm.Show(headerKey, bodyKey, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DirectData(LanguageChangeEventDataHolder headerData, LanguageChangeEventDataHolder bodyData, Action onConfirmAction, Action onCancelAction,
            string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            HudConfirm.Show(headerData, bodyData, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Open()
        {
            return HudConfirm.isOpen;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Listening()
        {
            return HudConfirm.onShow != null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ReadZoom()
        {
            return PlayerPrefSettings.uiZooming;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool MenuOpen()
        {
            return UI.Smartphone.FullMenu.IsOpen;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Press()
        {
            Action press = HudConfirm.onConfirm;
            if (press == null)
            {
                return false;
            }
            press();
            return true;
        }

        // Each box of the game's put itself on the confirm event (HudConfirmUi.Start): the one whose sheet is up is the open one.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Select()
        {
            Action press = HudConfirm.onConfirm;
            EventSystem events = EventSystem.current;
            if (press == null || events == null)
            {
                return;
            }
            foreach (Delegate each in press.GetInvocationList())
            {
                object box = each.Target;
                if (box == null || !GameContract.AskConfirm.Field.DeclaringType.IsInstanceOfType(box))
                {
                    continue;
                }
                var sheet = GameContract.AskContainer.Field.GetValue(box) as RectTransform;
                var button = GameContract.AskConfirm.Field.GetValue(box) as Button;
                if (sheet != null && sheet.gameObject.activeInHierarchy && button != null)
                {
                    events.SetSelectedGameObject(button.gameObject);
                    return;
                }
            }
        }

        // Only the Show itself missing counts: a fault of an answer run at once (the skip key held) is the caller's.
        private static bool Lacks(Exception e)
        {
            return Guard.Missing(e) && e.Message != null && e.Message.IndexOf("HudConfirm", StringComparison.Ordinal) >= 0;
        }

        private static void NotDirect(Exception e)
        {
            _direct = false;
            Guard.DoesWithout(e, "the mod's questions", "they are asked through the running game's own question box instead");
        }

        private static bool Found()
        {
            try
            {
                return Find();
            }
            catch (Exception e)
            {
                _looked = true;
                _byKey = null;
                _byData = null;
                Say("Game build: the running game's question box could not be looked up (" + e.GetType().Name + ": " + e.Message + ").");
                return false;
            }
        }

        private static bool Find()
        {
            if (_looked)
            {
                return _byKey != null || _byData != null;
            }
            _looked = true;
            Type box;
            try
            {
                box = TypeOfBox();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the game's question box", "the mod's questions are not shown");
                return false;
            }
            foreach (MethodInfo method in box.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Show")
                {
                    continue;
                }
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2)
                {
                    continue;
                }
                if (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(string) && _byKey == null)
                {
                    _byKey = method;
                    _byKeyParameters = parameters;
                }
                else if (parameters[0].ParameterType == typeof(LanguageChangeEventDataHolder) && parameters[1].ParameterType == typeof(LanguageChangeEventDataHolder) && _byData == null)
                {
                    _byData = method;
                    _byDataParameters = parameters;
                }
            }
            return _byKey != null || _byData != null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Type TypeOfBox()
        {
            return typeof(HudConfirm);
        }

        private static void TellOwn(ParameterInfo[] parameters)
        {
            if (!_toldOwn)
            {
                _toldOwn = true;
                Say("Game build: the mod's questions are asked through this game's own " + Describe(parameters) + ".");
            }
        }

        private static string Describe(ParameterInfo[] parameters)
        {
            var text = new System.Text.StringBuilder("Show(");
            for (int i = 0; i < parameters.Length; i++)
            {
                text.Append(i > 0 ? ", " : string.Empty).Append(parameters[i].ParameterType.Name).Append(' ').Append(parameters[i].Name);
            }
            return text.Append(')').ToString();
        }

        // Each parameter gets the answer of its name, else the one in its place if the type agrees, else its default.
        private static void Call(MethodInfo method, ParameterInfo[] parameters, string[] names, object[] answers)
        {
            var arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                int at = Array.IndexOf(names, p.Name);
                if (at < 0 && i < answers.Length && Fits(p.ParameterType, answers[i]))
                {
                    at = i;
                }
                if (at >= 0 && Fits(p.ParameterType, answers[at]))
                {
                    arguments[i] = answers[at];
                }
                else if (p.HasDefaultValue)
                {
                    arguments[i] = p.DefaultValue;
                }
                else
                {
                    arguments[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                }
            }
            try
            {
                method.Invoke(null, arguments);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();   // the caller's fault, as with a direct call
            }
        }

        private static bool Fits(Type type, object answer)
        {
            return answer == null ? !type.IsValueType : type.IsInstanceOfType(answer);
        }

        private static void Say(string line)
        {
            IModLogger logger = Guard.Logger;
            if (logger != null)
            {
                logger.Warn(line);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Mod:Real Life Overhaul] " + line);
            }
        }
    }
}
