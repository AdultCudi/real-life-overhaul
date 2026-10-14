using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BAModAPI;
using Localizor.LanguageChangeEvent;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own question box, HudConfirm.Show, for every part of the mod: the same two calls with the same
    /// parameters, so that a call reads exactly as the game's. The mod is compiled against the game's Show with eight
    /// parameters; a build of the game before it (Build 3675) has no such method, and on it every method of the mod that
    /// asked a question could not be compiled at all, the opening of each of the mod's apps among them (Core/GameUi/PhoneApps).
    ///
    /// So the game's method is named only here, in a small method of its own. On a game without it the first call
    /// says so once in Player.log, and from then on the running game's own Show is found (once) and called with the
    /// same answers: its parameters matched by name, then by place, any it has beyond these given their defaults.
    /// Where none can be found the box is not shown, and that is said once; nothing is done in its place.
    ///
    /// Cost: one call more than the game's own; the search is made once, and only on a game that needs it.
    /// </summary>
    public static class GameConfirm
    {
        public static IModLogger Logger;

        private const string TestKey = "reallife_test_build_box";
        private const string TestBodyKey = "reallife_test_build_box_body";
        private const string OkKey = "reallife_opt_ok";

        private static bool _direct = true;   // false once the game has been found to lack the Show the mod is built with
        private static bool _looked, _toldOwn, _toldNone;
        private static MethodInfo _byKey, _byData;
        private static ParameterInfo[] _byKeyParameters, _byDataParameters;

        // The answers in the order of the game's own parameters.
        private static readonly string[] Names = { "headerKey", "bodyKey", "onConfirmAction", "onCancelAction", "confirmKey", "cancelKey", "allowConfirmationSkip", "isNegative" };
        private static readonly string[] DataNames = { "headerData", "bodyData", "onConfirmAction", "onCancelAction", "confirmKey", "cancelKey", "allowConfirmationSkip", "isNegative" };

        /// <summary>The game's question box (HudConfirm.Show with keys, or words shown as they are).</summary>
        public static void Show(string headerKey = null, string bodyKey = null, Action onConfirmAction = null, Action onCancelAction = null, string confirmKey = null, string cancelKey = null, bool allowConfirmationSkip = true, bool isNegative = false)
        {
            TryShow(headerKey, bodyKey, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        /// <summary>The game's question box with texts that fill names (HudConfirm.Show with LanguageChangeEventDataHolder).</summary>
        public static void Show(LanguageChangeEventDataHolder headerData = default, LanguageChangeEventDataHolder bodyData = default, Action onConfirmAction = null, Action onCancelAction = null, string confirmKey = null, string cancelKey = null, bool allowConfirmationSkip = true, bool isNegative = false)
        {
            TryShow(headerData, bodyData, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        /// <summary>
        /// As <see cref="Show(LanguageChangeEventDataHolder, LanguageChangeEventDataHolder, Action, Action, string, string, bool, bool)"/>;
        /// false only when the running game has no question box the mod can call, so that nothing was shown.
        /// </summary>
        public static bool TryShow(LanguageChangeEventDataHolder headerData, LanguageChangeEventDataHolder bodyData, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
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
            return TryShow(headerData.Key != null ? headerData.ToString() : null, bodyData.Key != null ? bodyData.ToString() : null,
                           onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        /// <summary>
        /// As <see cref="Show(string, string, Action, Action, string, string, bool, bool)"/>; false only when the running game
        /// has no question box the mod can call, so that nothing was shown.
        /// </summary>
        public static bool TryShow(string headerKey, string bodyKey, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
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

        /// <summary>True while the game's question box is up (HudConfirm.isOpen); false on a game where that cannot be read.</summary>
        public static bool IsOpen
        {
            get
            {
                try
                {
                    return Open();
                }
                catch (Exception e) when (GameMembers.Missing(e))
                {
                    GameMembers.DoesWithout(e, "whether the game's question box is up");
                    return false;
                }
            }
        }

        /// <summary>
        /// True when the game's question box is there to be shown (HudConfirm.onShow: its panel has started, in the main
        /// menu and in the city); true as well on a game where that cannot be read, which is then left to the game.
        /// </summary>
        public static bool Ready
        {
            get
            {
                try
                {
                    return Listening();
                }
                catch (Exception e) when (GameMembers.Missing(e))
                {
                    GameMembers.DoesWithout(e, "whether the game's question box is there");
                    return true;
                }
            }
        }

        /// <summary>
        /// Options > Tests: a question through the running game's own Show found as the mod runs, the way the mod asks on a
        /// game that lacks the Show it is built with, so that the way round can be seen on the game the mod is made for.
        /// </summary>
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
                Logger?.Info("Game build check (test, question box): asked through the running game's own " + Describe(_byKeyParameters) + ", its parameters matched by name: PASS.");
            }
            catch (Exception e)
            {
                (Logger ?? GameMembers.Logger)?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the game's own

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Direct(string headerKey, string bodyKey, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
        {
            HudConfirm.Show(headerKey, bodyKey, onConfirmAction, onCancelAction, confirmKey, cancelKey, allowConfirmationSkip, isNegative);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DirectData(LanguageChangeEventDataHolder headerData, LanguageChangeEventDataHolder bodyData, Action onConfirmAction, Action onCancelAction, string confirmKey, string cancelKey, bool allowConfirmationSkip, bool isNegative)
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

        // Only the game's own Show itself missing counts: a fault of what the box is told to do (an answer run at once,
        // with the skip key held) is the caller's, as it always was.
        private static bool Lacks(Exception e)
        {
            return GameMembers.Missing(e) && e.Message != null && e.Message.IndexOf("HudConfirm", StringComparison.Ordinal) >= 0;
        }

        private static void NotDirect(Exception e)
        {
            _direct = false;
            GameMembers.DoesWithout(e, "the mod's questions", "they are asked through the running game's own question box instead");
        }

        // ---------------------------------------------------------------- the running game's Show, by reflection

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
            foreach (MethodInfo method in TypeOfBox().GetMethods(BindingFlags.Public | BindingFlags.Static))
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

        // Said once, the first time a question really goes the way round.
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

        // Each of the game's parameters gets the answer of its name; failing that the answer in its place if the type
        // agrees; failing that its own default.
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
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();   // what the box ran is the caller's fault, as with a direct call
            }
        }

        private static bool Fits(Type type, object answer)
        {
            return answer == null ? !type.IsValueType : type.IsInstanceOfType(answer);
        }

        private static void Say(string line)
        {
            IModLogger logger = Logger ?? GameMembers.Logger;
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
