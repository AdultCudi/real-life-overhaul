using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BAModAPI;
using Localizor.LanguageChangeEvent;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own question box, HudConfirm.Show, for every part of the mod, with the game's two calls and
    /// parameters. The mod is compiled against the eight-parameter Show, which older builds lack; on those, Mono could
    /// not compile any method naming it. So it is named only here, in small methods. On a game without it (the contract's
    /// ConfirmByKey, ConfirmByData) the running game's own Show is found by reflection once and called with the same
    /// answers, matched by name, then by place, the rest at their defaults; with none, nothing is shown, logged once.
    /// COST: one call more than the game's own; the search runs once, only on a game that needs it.
    /// </summary>
    public static class GameConfirm
    {
        private const string TestKey = "reallife_test_build_box";
        private const string TestBodyKey = "reallife_test_build_box_body";
        private const string OkKey = "reallife_opt_ok";

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
        }

        private static bool _noOpen, _noListening;   // the box's two flags this game lacks

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
            onConfirmAction = Answer(onConfirmAction);
            onCancelAction = Answer(onCancelAction);
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
            return ByKey(headerKey, bodyKey, Answer(onConfirmAction), Answer(onCancelAction), confirmKey, cancelKey, allowConfirmationSkip, isNegative);
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
