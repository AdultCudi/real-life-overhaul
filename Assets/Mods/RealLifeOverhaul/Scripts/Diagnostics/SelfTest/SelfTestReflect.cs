using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// The self-test's way to the mod's own members by name, at run time: the kit's command constants (so a plan presses by name, not
    /// number), and any method or member of the mod's classes (so a plan can stage a situation by the mod's own call and read its own
    /// state). A developer diagnostic: read only by a running plan; a name that does not exist fails the step with what does.
    /// </summary>
    internal static class SelfTestReflect
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        private static readonly Assembly Mod = typeof(SelfTestReflect).Assembly;
        private static List<KeyValuePair<string, int>> _consts;   // "Class.Name", value: every integer constant of the mod's classes

        private static List<KeyValuePair<string, int>> Consts()
        {
            if (_consts != null)
            {
                return _consts;
            }
            var list = new List<KeyValuePair<string, int>>(2048);
            Type[] types;
            try
            {
                types = Mod.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = Array.FindAll(e.Types, t => t != null);
            }
            foreach (Type type in types)
            {
                foreach (FieldInfo f in type.GetFields(All))
                {
                    if (f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(int))
                    {
                        list.Add(new KeyValuePair<string, int>(type.Name + "." + f.Name, (int)f.GetRawConstantValue()));
                    }
                }
            }
            _consts = list;
            return list;
        }

        /// <summary>A command given as a number or as the name of a constant of the mod ("Row", "UiAppWindow.NavCommand"); the screen id narrows a name that several classes use.</summary>
        public static bool Command(string text, string screenId, out int value, out string why)
        {
            why = null;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
            string token = (screenId ?? string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            var hits = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<string, int> c in Consts())
            {
                if (c.Key == text || c.Key.EndsWith("." + text, StringComparison.Ordinal))
                {
                    hits.Add(c);
                }
            }
            if (hits.Count > 1 && token.Length > 0)
            {
                hits = hits.FindAll(c => c.Key.ToLowerInvariant().Contains(token));
            }
            bool same = hits.Count > 0 && hits.TrueForAll(c => c.Value == hits[0].Value);
            if (hits.Count == 0 || !same)
            {
                var near = new List<string>(40);
                foreach (KeyValuePair<string, int> c in Consts())
                {
                    if (token.Length > 0 && c.Key.ToLowerInvariant().Contains(token) && near.Count < 60)
                    {
                        near.Add(c.Key + "=" + c.Value);
                    }
                }
                why = (hits.Count == 0 ? "no command constant named '" + text + "'" : "'" + text + "' names several different constants")
                    + ". Constants of classes named for '" + screenId + "': " + string.Join(", ", near);
                return false;
            }
            value = hits[0].Value;
            return true;
        }

        /// <summary>The name of a command's constant, for a dump: a class named for the screen first, else any.</summary>
        public static string NameOf(int command, string screenId)
        {
            string token = (screenId ?? string.Empty).Replace("_", string.Empty).ToLowerInvariant(), any = null;
            foreach (KeyValuePair<string, int> c in Consts())
            {
                if (c.Value != command)
                {
                    continue;
                }
                if (token.Length > 0 && c.Key.ToLowerInvariant().Contains(token))
                {
                    return c.Key;
                }
                any = any ?? c.Key;
            }
            return any ?? command.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A class of the mod by full name, or by its plain name when only one has it.</summary>
        public static Type FindType(string name, out string why)
        {
            why = null;
            Type exact = Mod.GetType(name, false);
            if (exact != null)
            {
                return exact;
            }
            Type found = null;
            int count = 0;
            Type[] types;
            try
            {
                types = Mod.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = Array.FindAll(e.Types, t => t != null);
            }
            foreach (Type t in types)
            {
                if (t.Name == name)
                {
                    found = t;
                    count++;
                }
            }
            if (count != 1)
            {
                why = count == 0 ? "the mod has no class named '" + name + "'" : count + " classes of the mod are named '" + name + "': give the full name (with its namespace)";
                return null;
            }
            return found;
        }

        private static object Instance(Type type)
        {
            PropertyInfo p = type.GetProperty("Instance", All);
            if (p != null)
            {
                return p.GetValue(null);
            }
            FieldInfo f = type.GetField("Instance", All);
            return f != null ? f.GetValue(null) : null;
        }

        /// <summary>Calls a method of a class of the mod: static, else on the class's Instance; arguments are given as words and converted to the method's own types.</summary>
        public static bool Call(string typeName, string method, string argText, out string result, out string why)
        {
            result = null;
            Type type = FindType(typeName, out why);
            if (type == null)
            {
                return false;
            }
            string[] given = string.IsNullOrEmpty(argText) ? new string[0] : argText.Split('|');
            object instance = null;
            foreach (MethodInfo m in type.GetMethods(All))
            {
                if (m.Name != method || m.GetParameters().Length != given.Length)
                {
                    continue;
                }
                object[] values = Convert(m.GetParameters(), given);
                if (values == null)
                {
                    continue;
                }
                if (!m.IsStatic)
                {
                    instance = instance ?? Instance(type);
                    if (instance == null)
                    {
                        why = "'" + method + "' is an instance method and " + type.Name + " has no Instance now";
                        return false;
                    }
                }
                object returned = m.Invoke(m.IsStatic ? null : instance, values);
                result = returned == null ? string.Empty : System.Convert.ToString(returned, CultureInfo.InvariantCulture);
                return true;
            }
            var names = new List<string>();
            foreach (MethodInfo m in type.GetMethods(All))
            {
                if (m.DeclaringType == type && !m.IsSpecialName)
                {
                    names.Add(m.Name + "(" + m.GetParameters().Length + ")");
                }
            }
            why = type.Name + " has no method '" + method + "' taking " + given.Length + " argument(s) of these words. Its methods: " + string.Join(", ", names.GetRange(0, Math.Min(names.Count, 80)));
            return false;
        }

        private static object[] Convert(ParameterInfo[] ps, string[] given)
        {
            var values = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type t = ps[i].ParameterType;
                try
                {
                    values[i] = t == typeof(string) ? given[i] : t.IsEnum ? Enum.Parse(t, given[i], true) : System.Convert.ChangeType(given[i], t, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return values;
        }

        /// <summary>Reads a field or property of a class of the mod (static, else of its Instance), in words; a dotted path walks members from it ("Book.Count").</summary>
        public static bool Read(string typeName, string path, out string value, out string why)
        {
            value = null;
            Type type = FindType(typeName, out why);
            if (type == null)
            {
                return false;
            }
            object current = null;
            Type at = type;
            bool first = true;
            foreach (string part in path.Split('.'))
            {
                MemberInfo member = null;
                foreach (MemberInfo m in at.GetMember(part, All))
                {
                    member = m;
                    break;
                }
                if (member == null)
                {
                    why = at.Name + " has no member '" + part + "'";
                    return false;
                }
                bool isStatic = member is FieldInfo f0 ? f0.IsStatic : member is PropertyInfo p0 && p0.GetMethod != null && p0.GetMethod.IsStatic;
                object target = first && !isStatic ? Instance(type) : current;
                if (!isStatic && target == null)
                {
                    why = "'" + part + "' needs an instance and " + at.Name + " has no Instance now";
                    return false;
                }
                current = member is FieldInfo f ? f.GetValue(isStatic ? null : target) : member is PropertyInfo p ? p.GetValue(isStatic ? null : target) : null;
                first = false;
                if (current == null)
                {
                    value = "null";
                    return true;
                }
                at = current.GetType();
            }
            value = Describe(current);
            return true;
        }

        private static object Get(object o, string name)
        {
            Type t = o.GetType();
            PropertyInfo p = t.GetProperty(name, All);
            if (p != null)
            {
                return p.GetValue(o);
            }
            FieldInfo f = t.GetField(name, All);
            return f != null ? f.GetValue(o) : null;
        }

        /// <summary>
        /// Takes a post the way the careers' own calls do (the same steps as the careers test that stages an offer, aimed at a post of a
        /// profession named by its constant in ProCatalogue): the vacancies are looked at, the opening of that profession is found, an
        /// application is made, offered and accepted through CareersDirector.Apply-free staging (Offer, then the director's own Accept).
        /// The mod's careers classes are read by name; what they lack is named in the answer.
        /// </summary>
        public static bool TakePost(string profession, out string words)
        {
            words = null;
            Type dirType = FindType("CareersDirector", out string why);
            object dir = dirType != null ? Instance(dirType) : null;
            if (dir == null)
            {
                words = why ?? "CareersDirector has no Instance (the careers are off, or no city stands)";
                return false;
            }
            Type pro = FindType("ProCatalogue", out why);
            FieldInfo constant = pro != null ? pro.GetField(profession, All) : null;
            if (constant == null)
            {
                words = "ProCatalogue has no profession '" + profession + "'";
                return false;
            }
            int wanted = (int)constant.GetValue(null);
            int day = SaveGameManager.Current.Day;
            MethodInfo vacancies = dirType.GetMethod("Vacancies", All);
            vacancies?.Invoke(dir, new object[] { day });
            object work = Get(dir, "Work");
            System.Collections.IList openings = work != null ? Get(work, "Openings") as System.Collections.IList : null;
            if (openings == null)
            {
                words = "the careers hold no work file or openings (WorkReady is false)";
                return false;
            }
            object pick = null;
            foreach (object o in openings)
            {
                if ((int)Get(o, "Prof") == wanted)
                {
                    pick = o;
                    break;
                }
            }
            if (pick == null)
            {
                words = "no vacancy of profession " + profession + " is open today (" + openings.Count + " openings in all)";
                return false;
            }
            Type applicationType = dirType.Assembly.GetType("RealLifeOverhaul.Careers.Application");
            object application = Activator.CreateInstance(applicationType);
            applicationType.GetField("Day").SetValue(application, day);
            applicationType.GetField("Score").SetValue(application, 90);
            object post = applicationType.GetField("Post").GetValue(application);
            pick.GetType().GetMethod("CopyTo").Invoke(pick, new[] { post });
            dirType.GetMethod("Offer", All).Invoke(dir, new object[] { application, day });
            ((System.Collections.IList)Get(work, "Applications")).Add(application);
            int id = (int)Get(post, "Id");
            object refused = dirType.GetMethod("Accept", All).Invoke(dir, new object[] { id });
            if (refused != null)
            {
                words = "the careers refused the offer: " + refused;
                return false;
            }
            words = "offered and accepted: opening " + id + " of " + profession;
            return true;
        }

        private static string Describe(object o)
        {
            if (o is string s)
            {
                return s;
            }
            if (o is System.Collections.ICollection c)
            {
                return "count " + c.Count;
            }
            return System.Convert.ToString(o, CultureInfo.InvariantCulture);
        }
    }
}
