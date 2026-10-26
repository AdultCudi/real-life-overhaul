using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BigAmbitions.Neighborhoods;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The district save's check of what is about to be written (the self-check at every save): every value the game's
    /// serializer writes, gone through by reflection along a plan made once a session per type, by the rule the game's
    /// save policy follows (public fields and those marked to be kept; none marked not to be, none obsolete), the same
    /// rule ContractCheck's field census reads the game's assemblies by. It looks at every address, every street and
    /// district by its field's name, and every reference to someone or a plan that left; a field a game update adds is
    /// gone through like the others, so a place of the mod's in it is found before the mod knows the field. A type that
    /// cannot hold a place anywhere below it (a shop's items, its designs) is never entered.
    /// COST: nothing per frame; at a save of a game with places of the mod's, a few milliseconds.
    /// </summary>
    internal static class DistrictSaveScan
    {
        private enum Role
        {
            Descend,     // a record, struct or collection to go into
            Address,     // an Address
            Street,      // a street's id, with its number in a field beside it
            Number,      // a street's number, read with its street
            District,    // a district's id
            Id,          // an id of someone or a plan
            Ids,         // a list of such ids
            Districts    // a list of texts that may each be a district's id (the city map's ticked rows: a district's row is its id)
        }

        private sealed class Plan
        {
            public FieldInfo[] Fields = new FieldInfo[0];
            public Role[] Roles = new Role[0];
            public int[] Numbers = new int[0];   // a street's number field, or -1
            public string[] Pieces = new string[0];   // each field's piece of a path: "." and its name
            public bool Reaches;                 // a place can be found in it or below it
        }

        private static readonly Dictionary<Type, Plan> Plans = new Dictionary<Type, Plan>();
        private static readonly HashSet<Type> Planning = new HashSet<Type>();

        /// <summary>
        /// What of the mod's the save about to be written still names, each as its path and value, at most
        /// <paramref name="most"/>; empty when it names nothing.
        /// </summary>
        public static List<string> Find(GameInstance save, PlaceSet places, DistrictSaveGame.Strip left, int most)
        {
            var walk = new Walk { Places = places, Left = left, Most = most };
            walk.Into(save, "GameInstance");
            return walk.Hits;
        }

        // The walk keeps its path as a stack of pieces, joined only for what it finds.
        private sealed class Walk
        {
            public PlaceSet Places;
            public DistrictSaveGame.Strip Left;
            public int Most;
            public readonly List<string> Hits = new List<string>(0);
            private readonly HashSet<object> _seen = new HashSet<object>(Same.Instance);
            private readonly List<string> _path = new List<string>(16);

            public void Into(object o, string piece)
            {
                _path.Add(piece);
                Visit(o);
                _path.RemoveAt(_path.Count - 1);
            }

            private void Visit(object o)
            {
                if (o == null || Hits.Count >= Most)
                {
                    return;
                }
                Type t = o.GetType();
                if (t.IsPrimitive || t.IsEnum || o is string || o is UnityEngine.Object || o is Delegate || o is Type)
                {
                    return;
                }
                if (o is Address a)
                {
                    if (Places.Names(a.streetName, a.streetNumber))
                    {
                        Hit(null, a.streetName + " " + a.streetNumber);
                    }
                    return;
                }
                bool collection = o is IEnumerable;
                Plan plan = collection ? null : PlanOf(t);
                if (plan != null && !plan.Reaches)
                {
                    return;   // nothing below it can hold a place: never remembered, never entered
                }
                if (!t.IsValueType && !_seen.Add(o))
                {
                    return;   // met before: the serializer writes it once and points to it after
                }
                if (o is IDictionary map)
                {
                    foreach (DictionaryEntry e in map)
                    {
                        Into(e.Key, "{key}");
                        Into(e.Value, "{}");
                    }
                    return;
                }
                if (o is IEnumerable list)
                {
                    foreach (object e in list)
                    {
                        Into(e, "[]");
                    }
                    return;
                }
                for (int i = 0; i < plan.Fields.Length && Hits.Count < Most; i++)
                {
                    FieldInfo f = plan.Fields[i];
                    switch (plan.Roles[i])
                    {
                        case Role.Street:
                        {
                            string street = f.GetValue(o) as string;
                            int number = plan.Numbers[i] >= 0 ? (int)plan.Fields[plan.Numbers[i]].GetValue(o) : 0;
                            if (Places.Names(street, number))
                            {
                                Hit(plan.Pieces[i], street + " " + number);
                            }
                            break;
                        }
                        case Role.District:
                        {
                            string district = f.GetValue(o) as string;
                            if (Places.NamesDistrict(district))
                            {
                                Hit(plan.Pieces[i], district);
                            }
                            break;
                        }
                        case Role.Id:
                            Id(f.GetValue(o) as string, plan.Pieces[i]);
                            break;
                        case Role.Number:
                            break;
                        case Role.Districts:
                            if (f.GetValue(o) is IEnumerable rows)
                            {
                                int k = 0;
                                foreach (object row in rows)
                                {
                                    if (Places.NamesDistrict(row as string))
                                    {
                                        Hit(plan.Pieces[i] + "[" + k.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", row as string);
                                    }
                                    k++;
                                }
                            }
                            break;
                        case Role.Ids:
                            if (f.GetValue(o) is IEnumerable ids)
                            {
                                foreach (object id in ids)
                                {
                                    Id(id as string, plan.Pieces[i]);
                                }
                            }
                            break;
                        default:
                            Into(f.GetValue(o), plan.Pieces[i]);
                            break;
                    }
                }
                Positions(o);
            }

            // A reference to someone or a plan that left this save.
            private void Id(string id, string piece)
            {
                DistrictSaveGame.Strip s = Left;
                if (s != null && !string.IsNullOrEmpty(id)
                    && (s.Employees.Contains(id) || s.HrPlans.Contains(id) || s.Offers.Contains(id) || s.Negotiations.Contains(id)))
                {
                    Hit(piece, "the id " + id + " of a record that left");
                }
            }

            // The player and a car out of doors on the mod's ground (their stand-ins put them at a way out).
            private void Positions(object o)
            {
                if (o is GameInstance g && string.IsNullOrEmpty(g.CurrentStreetName) && Places.OnGround(g.LastPlayerPosition.x, g.LastPlayerPosition.z))
                {
                    Hit(".LastPlayerPosition", "a spot on the mod's ground");
                }
                else if (o is VehicleInstance v && string.IsNullOrEmpty(v.streetName) && Places.OnGround(v.position.x, v.position.z))
                {
                    Hit(".position", "a spot on the mod's ground (car " + v.id + ")");
                }
            }

            private void Hit(string last, string what)
            {
                if (Hits.Count >= Most)
                {
                    return;
                }
                var path = new System.Text.StringBuilder(96);
                for (int i = 0; i < _path.Count; i++)
                {
                    path.Append(_path[i]);
                }
                Hits.Add(path.Append(last).Append(" = ").Append(what).ToString());
            }
        }

        // The fields of a type that the game's serializer writes, each with what it may hold; made once a type.
        private static Plan PlanOf(Type t)
        {
            if (Plans.TryGetValue(t, out Plan plan))
            {
                return plan;
            }
            if (!Planning.Add(t))
            {
                return new Plan { Reaches = true };   // a type within itself: gone into, and judged when it is met
            }
            plan = new Plan();
            var fields = new List<FieldInfo>(8);
            var roles = new List<Role>(8);
            for (Type at = t; at != null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType)
            {
                foreach (FieldInfo f in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!Written(f) || Skip(t, f))
                    {
                        continue;
                    }
                    Role role;
                    if (!RoleOf(t, f, out role))
                    {
                        continue;
                    }
                    fields.Add(f);
                    roles.Add(role);
                }
            }
            plan.Fields = fields.ToArray();
            plan.Roles = roles.ToArray();
            plan.Numbers = new int[plan.Fields.Length];
            plan.Pieces = new string[plan.Fields.Length];
            for (int i = 0; i < plan.Fields.Length; i++)
            {
                plan.Pieces[i] = "." + plan.Fields[i].Name;
                plan.Numbers[i] = plan.Roles[i] == Role.Street ? NumberBeside(plan.Fields, plan.Fields[i].Name) : -1;
                if ((plan.Roles[i] != Role.Descend && plan.Roles[i] != Role.Number) || Reaches(plan.Fields[i].FieldType))
                {
                    plan.Reaches = true;
                }
            }
            if (t == typeof(GameInstance) || t == typeof(VehicleInstance))
            {
                plan.Reaches = true;   // their positions
            }
            Planning.Remove(t);
            Plans[t] = plan;
            return plan;
        }

        // Whether a declared type can hold a place: a type that may be another below (not sealed) is judged when met.
        private static bool Reaches(Type t)
        {
            if (t == typeof(Address))
            {
                return true;
            }
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(t) || typeof(Delegate).IsAssignableFrom(t))
            {
                return false;
            }
            if (t.IsArray)
            {
                return Reaches(t.GetElementType());
            }
            if (t.IsGenericType)
            {
                foreach (Type arg in t.GetGenericArguments())
                {
                    if (Reaches(arg))
                    {
                        return true;
                    }
                }
                return !typeof(IEnumerable).IsAssignableFrom(t) && PlanOf(t).Reaches;
            }
            if (t == typeof(object) || t.IsInterface || t.IsAbstract || (!t.IsSealed && !t.IsValueType))
            {
                return true;
            }
            return PlanOf(t).Reaches;
        }

        // The game's save policy: public fields not marked unserialized, private ones marked to be kept; never obsolete.
        private static bool Written(FieldInfo f)
        {
            if (f.IsLiteral || f.IsStatic || f.IsDefined(typeof(ObsoleteAttribute), true))
            {
                return false;
            }
            bool kept = f.IsDefined(typeof(UnityEngine.SerializeField), false) || Marked(f, "OdinSerializer.OdinSerializeAttribute");
            if (f.IsNotSerialized && !Marked(f, "OdinSerializer.OdinSerializeAttribute"))
            {
                return false;
            }
            return f.IsPublic || kept;
        }

        private static bool Marked(FieldInfo f, string attribute)
        {
            foreach (object a in f.GetCustomAttributes(false))
            {
                if (a.GetType().FullName == attribute)
                {
                    return true;
                }
            }
            return false;
        }

        // The mod's own section is not the game's.
        private static bool Skip(Type owner, FieldInfo f)
        {
            return owner == typeof(GameInstance) && f.Name == "modData";
        }

        // What a field may hold, by its type and, for a text, its name (as ContractCheck's census reads them).
        private static bool RoleOf(Type owner, FieldInfo f, out Role role)
        {
            role = Role.Descend;
            Type t = f.FieldType;
            string n = f.Name.ToLowerInvariant();
            if (owner == typeof(GameInstance) && f.Name == "SelectedCitymapFilters")
            {
                role = Role.Districts;   // by what it holds, not its name (ContractCheck's ByMeaning)
                return true;
            }
            if (t == typeof(Address))
            {
                role = Role.Address;
                return true;
            }
            if (t == typeof(string))
            {
                if (n.EndsWith("streetname", StringComparison.Ordinal) || n == "street" || n.EndsWith("street", StringComparison.Ordinal))
                {
                    role = Role.Street;
                    return true;
                }
                if (n.Contains("neighbourhood") || n.Contains("neighborhood") || n.Contains("district") || (owner == typeof(NeighbourhoodStats) && n == "name"))
                {
                    role = Role.District;
                    return true;
                }
                if (IdName(n))
                {
                    role = Role.Id;
                    return true;
                }
                return false;
            }
            if (t == typeof(int) && n.EndsWith("number", StringComparison.Ordinal))
            {
                role = Role.Number;   // read with its street
                return true;
            }
            if (t.IsPrimitive || t.IsEnum)
            {
                return false;
            }
            if (IdName(n) && (t == typeof(string[]) || typeof(IEnumerable<string>).IsAssignableFrom(t)))
            {
                role = Role.Ids;
                return true;
            }
            return Reaches(t);
        }

        // An id of someone or of a plan, an offer or a negotiation: what the district save takes out with a business.
        private static bool IdName(string n)
        {
            bool idName = n.EndsWith("id", StringComparison.Ordinal) || n.EndsWith("ids", StringComparison.Ordinal) || n.EndsWith("employees", StringComparison.Ordinal)
                          || n.EndsWith("plans", StringComparison.Ordinal);
            if (!idName || n == "sourceheadhunterid")
            {
                return false;   // (where a candidate was found: read through null-safe lookups for its text alone, CandidateInfo.GetSource)
            }
            return n.Contains("employee") || n.Contains("candidate") || n.Contains("plan") || n.Contains("negotiation") || n.Contains("offer")
                   || n.Contains("headhunter") || n.Contains("manager") || n.Contains("driver");
        }

        // A street's number: the field of the same name with Number for Name ("StreetName", "StreetNumber").
        private static int NumberBeside(FieldInfo[] fields, string street)
        {
            string number = street.EndsWith("Name", StringComparison.Ordinal) ? street.Substring(0, street.Length - 4) + "Number"
                            : street.EndsWith("name", StringComparison.Ordinal) ? street.Substring(0, street.Length - 4) + "number" : street + "Number";
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name == number && fields[i].FieldType == typeof(int))
                {
                    return i;
                }
            }
            return -1;
        }

        // Objects by reference: a record met twice is gone into once.
        private sealed class Same : IEqualityComparer<object>
        {
            public static readonly Same Instance = new Same();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object o)
            {
                return RuntimeHelpers.GetHashCode(o);
            }
        }
    }
}
