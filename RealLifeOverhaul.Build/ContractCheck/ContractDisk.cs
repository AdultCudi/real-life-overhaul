using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One member of the game contract checked against the game's assemblies on disk, as ContractBook.Check checks it
    /// against the loaded game: null when it is there as expected, otherwise what is wrong, in the same words.
    /// </summary>
    internal static class ContractDisk
    {
        public static string Problem(GameFiles files, GameMember m)
        {
            if (!files.Has(m.Assembly))
            {
                return "the assembly " + m.Assembly + " is not in the game";
            }
            FoundType type = files.Find(m.Assembly, m.TypeName);
            if (!type.Exists)
            {
                return "the type " + m.TypeName + " is not in " + m.Assembly;
            }
            switch (m.Kind)
            {
                case GameMemberKind.Type:
                    return null;
                case GameMemberKind.Field:
                    foreach (FoundType t in files.Chain(type))
                    {
                        MetadataReader md = t.Assembly.Md;
                        foreach (FieldDefinitionHandle h in t.Def.GetFields())
                        {
                            FieldDefinition f = md.GetFieldDefinition(h);
                            if (md.GetString(f.Name) != m.Name)
                            {
                                continue;
                            }
                            bool isStatic = (f.Attributes & FieldAttributes.Static) != 0;
                            if (isStatic != m.Static)
                            {
                                return isStatic ? "the field is now static" : "the field is no longer static";
                            }
                            string actual = f.DecodeSignature(GameFiles.Short, new GameFiles.Ctx(md, t.Handle, t.Args));
                            return Same("the field's type", actual, m.ValueType);
                        }
                    }
                    return "the field is missing";
                case GameMemberKind.Property:
                    foreach (FoundType t in files.Chain(type))
                    {
                        MetadataReader md = t.Assembly.Md;
                        foreach (PropertyDefinitionHandle h in t.Def.GetProperties())
                        {
                            PropertyDefinition p = md.GetPropertyDefinition(h);
                            if (md.GetString(p.Name) != m.Name)
                            {
                                continue;
                            }
                            PropertyAccessors accessors = p.GetAccessors();
                            MethodDefinitionHandle either = !accessors.Getter.IsNil ? accessors.Getter : accessors.Setter;
                            bool isStatic = !either.IsNil && (md.GetMethodDefinition(either).Attributes & MethodAttributes.Static) != 0;
                            if (either.IsNil || isStatic != m.Static)
                            {
                                return isStatic ? "the property is now static" : "the property is no longer static";
                            }
                            if (m.NeedsSetter && accessors.Setter.IsNil)
                            {
                                return "the property can no longer be set";
                            }
                            MethodSignature<string> sig = p.DecodeSignature(GameFiles.Short, new GameFiles.Ctx(md, t.Handle, t.Args));
                            return Same("the property's type", sig.ReturnType, m.ValueType);
                        }
                    }
                    return "the property is missing";
                default:
                    string near = null;
                    foreach (FoundType t in files.Chain(type))
                    {
                        MetadataReader md = t.Assembly.Md;
                        foreach (MethodDefinitionHandle h in t.Def.GetMethods())
                        {
                            MethodDefinition d = md.GetMethodDefinition(h);
                            bool isStatic = (d.Attributes & MethodAttributes.Static) != 0;
                            if (md.GetString(d.Name) != m.Name || isStatic != m.Static)
                            {
                                continue;
                            }
                            MethodSignature<string> sig = d.DecodeSignature(GameFiles.Short, new GameFiles.Ctx(md, t.Handle, t.Args, h));
                            string shape = string.Join(",", sig.ParameterTypes);
                            if (m.Parameters == null || shape == ContractBook.Join(m.Parameters))
                            {
                                return Same("the method's return type", sig.ReturnType, m.ValueType);
                            }
                            near = near ?? shape;
                        }
                    }
                    return near != null ? "the method now takes (" + near + "), the mod expects (" + ContractBook.Join(m.Parameters) + ")" : "the method is missing";
            }
        }

        private static string Same(string what, string actual, string expected)
        {
            return expected == null || actual == expected ? null : what + " is now " + actual + ", the mod expects " + expected;
        }

        /// <summary>Every member's problem against the files, in the table's order (null entries for the members that are there).</summary>
        public static Dictionary<GameMember, string> All(GameFiles files)
        {
            var problems = new Dictionary<GameMember, string>();
            foreach (GameMember m in ContractBook.Members)
            {
                string p;
                try
                {
                    p = Problem(files, m);
                }
                catch (System.Exception e)
                {
                    p = "it could not be read (" + e.GetType().Name + ": " + e.Message + ")";
                }
                problems[m] = p;
            }
            return problems;
        }
    }
}
