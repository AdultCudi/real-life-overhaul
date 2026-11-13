using System;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A fault as one line of words for Player.log: its kind, its message cut short and the first methods of its stack
    /// ("NullReferenceException: Object reference not set to an instance of an object, at HudSides.OthersLook &lt; HudSides.BuildingPanel
    /// &lt; HudLive.Look"). The guard writes it on its own lines, so a player who copies only the lines that begin with the mod's name
    /// still sends what failed and where, and the line that switches a part off names its last fault. A method the compiler made
    /// (a coroutine's step, a lambda) is named by the method it was written in. Game-free; checked offline (EconomyCheck, UiGuardChecks).
    /// COST: on a fault only.
    /// </summary>
    public static class FaultWords
    {
        public const int Frames = 4;          // methods named: the one that threw and three callers, enough to tell the entry point
        public const int MessageMost = 160;   // characters of a message kept: a path or a long name never fills the line

        /// <summary>The fault's line: its kind, its message, its first methods. Never throws.</summary>
        public static string Of(Exception e)
        {
            if (e == null)
            {
                return "no fault given";
            }
            try
            {
                Exception deep = e;
                for (int i = 0; i < 4 && deep.InnerException != null && string.IsNullOrEmpty(deep.StackTrace); i++)
                {
                    deep = deep.InnerException;   // a wrapper thrown by the runtime has no stack of its own: the fault inside it has
                }
                return Line(deep.GetType().Name, deep.Message, deep.StackTrace, Frames);
            }
            catch (Exception)
            {
                return e.GetType().Name;
            }
        }

        /// <summary>A fault's line from its parts; a stack with no method that can be read adds nothing.</summary>
        public static string Line(string kind, string message, string stack, int frames)
        {
            var line = new StringBuilder(160);
            line.Append(string.IsNullOrEmpty(kind) ? "Exception" : kind);
            string said = Short(message);
            if (said.Length > 0)
            {
                line.Append(": ").Append(said);
            }
            string where = Where(stack, frames);
            if (where.Length > 0)
            {
                line.Append(", at ").Append(where);
            }
            return line.ToString();
        }

        /// <summary>A message on one line, cut to <see cref="MessageMost"/> characters.</summary>
        public static string Short(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }
            string one = message.Replace("\r", string.Empty).Replace('\n', ' ').Trim();
            return one.Length <= MessageMost ? one : one.Substring(0, MessageMost).TrimEnd() + "...";
        }

        /// <summary>The first methods of a stack trace, the one that threw first, each as Class.Method, joined by " &lt; "; empty for none.</summary>
        public static string Where(string stack, int most)
        {
            if (string.IsNullOrEmpty(stack) || most < 1)
            {
                return string.Empty;
            }
            var said = new StringBuilder(96);
            int taken = 0, at = 0;
            while (at < stack.Length && taken < most)
            {
                int end = stack.IndexOf('\n', at);
                if (end < 0)
                {
                    end = stack.Length;
                }
                string frame = Frame(stack.Substring(at, end - at));
                at = end + 1;
                if (frame == null)
                {
                    continue;
                }
                said.Append(taken > 0 ? " < " : string.Empty).Append(frame);
                taken++;
            }
            return said.ToString();
        }

        /// <summary>One line of a stack trace as Class.Method; null for a line that names no method (a wrapper's, a blank one).</summary>
        public static string Frame(string line)
        {
            if (line == null)
            {
                return null;
            }
            string text = line.Trim();
            if (!text.StartsWith("at ", StringComparison.Ordinal))
            {
                return null;
            }
            text = text.Substring(3);
            if (text.StartsWith("(wrapper", StringComparison.Ordinal))
            {
                return null;   // the runtime's own step between two methods
            }
            int args = text.IndexOf('(');
            string name = (args >= 0 ? text.Substring(0, args) : text).Trim();
            int space = name.IndexOf(' ');
            if (space >= 0)
            {
                name = name.Substring(0, space);
            }
            if (name.Length == 0)
            {
                return null;
            }
            bool made = name.EndsWith("..ctor", StringComparison.Ordinal) || name.EndsWith("..cctor", StringComparison.Ordinal);
            int dot = made ? name.LastIndexOf("..", StringComparison.Ordinal) : name.LastIndexOf('.');
            string method = dot >= 0 ? name.Substring(dot + (made ? 2 : 1)) : name;
            string type = dot >= 0 ? name.Substring(0, dot) : string.Empty;
            // A class the compiler made stands inside the class it was written in, and its name or its method's holds the method's own in angle brackets.
            string written = Written(type) ?? Written(method);
            int plus = type.IndexOf('+');
            string outer = Plain(plus >= 0 ? type.Substring(0, plus) : type);   // its type arguments first: their names hold dots of their own
            int ns = outer.LastIndexOf('.');
            string cls = ns >= 0 ? outer.Substring(ns + 1) : outer;
            string meth = written ?? (made ? "(made)" : Plain(method));
            return cls.Length > 0 ? cls + "." + meth : meth;
        }

        // The method a compiler-made name was written in: "<Tick>d__12" and "<Build>b__0_0" give Tick and Build; null for any other name.
        private static string Written(string name)
        {
            int open = name.LastIndexOf('<');
            int close = open >= 0 ? name.IndexOf('>', open) : -1;
            return close > open + 1 ? name.Substring(open + 1, close - open - 1) : null;
        }

        // A name without the marks of its type arguments ("List`1[T]" is List).
        private static string Plain(string name)
        {
            int tick = name.IndexOfAny(new[] { '`', '[', '<' });
            return tick > 0 ? name.Substring(0, tick) : name;
        }
    }
}
