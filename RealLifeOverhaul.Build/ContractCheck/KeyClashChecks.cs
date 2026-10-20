using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using RealLifeOverhaul;

// The walk key against every key that is already someone's (Core/KeyRules.cs), read from the files as they are:
//   the game's bindings   every input asset in the game's own assemblies and its plug-ins (the player's, the interior
//                         designer's, the vehicle's and the scene's maps: the JSON each generated wrapper carries)
//   the game's own reads  every key its code reads by itself, from the IL: a KeyCode passed to any method that takes one
//                         (Input.GetKey, GetKeyDown, GetKeyUp, NWH's fallbacks), stored in a KeyCode field (the debug
//                         console's toggle), a Keyboard property (leftShiftKey) or Keyboard[Key]
//   the mod's keys        the same read of the built mod: every action's default (Core/Keys' bindings) but the walk's,
//                         and every fixed key (photo mode's, the panels', the gallery's)
//   the rest              what Windows, Steam, the overlays and the mods players run beside this one take (KeyRules.Taken)
// The walk's default is read from the built mod. It fails if anything above has it; and, so that it can fail, the same
// question asked of keys known to be taken must find them taken. The free keys looked at are printed.
internal static partial class Program
{
    private sealed class KeyUse
    {
        public string Path;
        public string Who;
    }

    // Keys looked at for a walk key, printed with who has each.
    private static readonly string[] WalkCandidates =
    {
        "F3", "F4", "F11", "Insert", "Delete", "Home", "End", "Minus", "Equals", "Backslash", "Slash", "Quote", "Semicolon", "Comma", "Period",
        "Alpha0", "Keypad0", "Keypad1", "Keypad5", "KeypadPlus", "KeypadMinus", "KeypadDivide", "KeypadMultiply", "CapsLock", "Numlock",
        "RightControl", "RightAlt", "RightShift", "G", "J", "K", "L", "U", "X", "Y", "Z", "BackQuote", "Mouse3", "Mouse4",
    };

    private static void KeyClashes(GameFiles files, string mod)
    {
        Section("the walk key against every key of the game's, the mod's, the system's and the mods beside it");
        Dictionary<int, string> keyCodes = EnumNames(files, "UnityEngine.KeyCode", "UnityEngine.InputLegacyModule", "UnityEngine.CoreModule", "UnityEngine");
        Dictionary<int, string> inputKeys = EnumNames(files, "UnityEngine.InputSystem.Key", "Unity.InputSystem");
        Check(keyCodes.Count > 300 && keyCodes.TryGetValue(284, out string f3) && f3 == "F3", "keys: Unity's KeyCode is read from the game's files (" + keyCodes.Count + " names)");
        Check(inputKeys.Count > 100, "keys: the input system's Key is read from the game's files (" + inputKeys.Count + " names)");

        var game = new List<KeyUse>();
        int assets = 0;
        int scanned = 0;
        foreach (string path in Directory.GetFiles(files.Folder, "*.dll"))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!GameOwn(name))
            {
                continue;
            }
            GameAssembly a = files.Get(name);
            if (a == null)
            {
                continue;
            }
            scanned++;
            assets += InputAssets(a, "the game (" + name + ")", game);
            KeyReads(a, "the game (" + name + ")", keyCodes, inputKeys, game, null);
        }
        Check(scanned >= 10, "keys: the game's own assemblies are read (" + scanned + ")");
        Check(assets >= 3, "keys: the game's input assets are found: the player's, the vehicle's and the scene's maps (" + assets + ")");
        Check(game.Any(u => u.Who.Contains("Player/ToggleRunning") && KeyRules.Takes(u.Path, KeyRules.PathOf("LeftShift"))), "keys: the game's Toggle Running is read on Left Shift");
        Check(game.Any(u => u.Who.Contains("InteriorDesigner/") && KeyRules.Takes(u.Path, KeyRules.PathOf("Alpha1"))), "keys: the interior designer's tools are read on the number keys");
        Check(game.Any(u => u.Who.Contains("Vehicle Controls/Handbrake")), "keys: the vehicle's handbrake binding is read");
        foreach (string fixedKey in new[] { "F7", "F8", "Mouse3", "Mouse4", "BackQuote", "LeftShift", "LeftControl" })
        {
            string p = KeyRules.PathOf(fixedKey);
            Check(game.Any(u => u.Who.Contains(" reads ") && KeyRules.Takes(u.Path, p)), "keys: the game's own read of " + fixedKey + " is found in its code (the reading of its IL is not broken)");
        }

        // The built mod: its bindings' defaults and its fixed keys. The walk's own binding is set aside: it is what is checked.
        var ours = new List<KeyUse>();
        string walk = null;
        if (!File.Exists(mod))
        {
            Check(false, "keys: the built mod is not at " + mod);
            return;
        }
        KeyReads(new GameAssembly("RealLifeOverhaul", mod), "the mod", keyCodes, inputKeys, ours, id => id);
        foreach (KeyUse u in ours.Where(x => x.Who == "the mod: binding walk").ToList())
        {
            walk = u.Path;
            ours.Remove(u);
        }
        Check(walk != null, "keys: the walk key's default is read from the built mod (Core/Keys, the binding \"walk\")");
        int bindings = ours.Count(u => u.Who.StartsWith("the mod: binding ", StringComparison.Ordinal));
        Check(bindings >= 10, "keys: the mod's other bindings are read (" + bindings + ")");
        foreach (string modKey in new[] { "F10", "V", "G", "K", "Home" })
        {
            string p = KeyRules.PathOf(modKey);
            Check(ours.Any(u => KeyRules.Takes(u.Path, p)), "keys: the mod's own " + modKey + " is found in the built mod");
        }
        if (walk == null)
        {
            return;
        }

        // The question itself, and the same question of keys known to be taken, so that it is seen to fail.
        string Holder(string key) => game.Concat(ours).Where(u => KeyRules.Takes(u.Path, key)).Select(u => u.Who).FirstOrDefault() ?? KeyRules.TakenBy(key);
        string holder = Holder(walk);
        Check(holder == null, "keys: the walk key " + walk + " is nobody else's" + (holder != null ? ": " + holder + " has it" : string.Empty));
        Check(Holder(KeyRules.PathOf("F")) != null && Holder(KeyRules.PathOf("F10")) != null && Holder(KeyRules.PathOf("CapsLock")) != null
              && Holder(KeyRules.PathOf("RightShift")) != null && Holder(KeyRules.PathOf("V")) != null,
              "keys: the question finds the game's interact key, the mod's photo mode key, Caps Lock, Shift through \"shift\" and Going Public's V taken");
        Check(KeyRules.Takes("<Keyboard>/shift", "<Keyboard>/rightShift") && !KeyRules.Takes("<Keyboard>/shift", "<Keyboard>/f3") && KeyRules.Takes("<Keyboard>/F3", "<Keyboard>/f3"),
              "keys: a pair's binding takes both keys, and paths are compared as the input system compares them");
        var free = new List<string>();
        var taken = new List<string>();
        foreach (string candidate in WalkCandidates)
        {
            string p = KeyRules.PathOf(candidate);
            string who = Holder(p);
            if (who == null)
            {
                free.Add(candidate);
            }
            else
            {
                taken.Add(candidate + " (" + who + ")");
            }
        }
        Console.WriteLine("   walk key " + walk + "; " + game.Count + " uses of keys by the game, " + ours.Count + " by the mod, " + KeyRules.Taken.Length + " by the system and other mods.");
        Console.WriteLine("   free of all of them: " + string.Join(", ", free) + ".");
        Console.WriteLine("   taken: " + string.Join("; ", taken) + ".");
        Check(free.Contains("F3"), "keys: F3 is among the free keys looked at");
    }

    // The game's own assemblies and its plug-ins, not the engine's, .NET's or other vendors' libraries.
    private static bool GameOwn(string name)
    {
        string[] notOwn = { "System", "Mono.", "Unity.", "UnityEngine", "mscorlib", "netstandard", "Microsoft", "Google.", "Newtonsoft", "Facepunch", "Autodesk", "OdinSerializer" };
        return !notOwn.Any(n => name.StartsWith(n, StringComparison.Ordinal));
    }

    // An enum's values and names, the first name of each value (KeyCode has aliases), from wherever the type is defined.
    private static Dictionary<int, string> EnumNames(GameFiles files, string fullName, params string[] assemblies)
    {
        var names = new Dictionary<int, string>();
        foreach (string assembly in assemblies)
        {
            FoundType t = files.Find(assembly, fullName);
            if (!t.Exists)
            {
                continue;
            }
            MetadataReader md = t.Assembly.Md;
            foreach (FieldDefinitionHandle fh in t.Def.GetFields())
            {
                FieldDefinition fd = md.GetFieldDefinition(fh);
                if ((fd.Attributes & FieldAttributes.Literal) == 0)
                {
                    continue;
                }
                Constant c = md.GetConstant(fd.GetDefaultValue());
                BlobReader blob = md.GetBlobReader(c.Value);
                int value = c.TypeCode == ConstantTypeCode.Int32 ? blob.ReadInt32()
                            : c.TypeCode == ConstantTypeCode.UInt16 ? blob.ReadUInt16()
                            : c.TypeCode == ConstantTypeCode.Byte ? blob.ReadByte() : int.MinValue;
                if (value != int.MinValue && !names.ContainsKey(value))
                {
                    names[value] = md.GetString(fd.Name);
                }
            }
            break;
        }
        return names;
    }

    // Every input asset an assembly carries as the JSON of a generated wrapper: its bindings with their map and action.
    private static int InputAssets(GameAssembly a, string who, List<KeyUse> uses)
    {
        MetadataReader md = a.Md;
        int found = 0;
        int size = md.GetHeapSize(HeapIndex.UserString);
        UserStringHandle h = MetadataTokens.UserStringHandle(1);
        while (!h.IsNil && MetadataTokens.GetHeapOffset(h) < size)
        {
            string text = md.GetUserString(h);
            h = md.GetNextHandle(h);
            if (text.Length < 64 || text.IndexOf("\"maps\"", StringComparison.Ordinal) < 0 || text.IndexOf("\"bindings\"", StringComparison.Ordinal) < 0)
            {
                continue;
            }
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                continue;
            }
            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("maps", out JsonElement maps))
                {
                    continue;
                }
                found++;
                foreach (JsonElement map in maps.EnumerateArray())
                {
                    string mapName = map.GetProperty("name").GetString();
                    foreach (JsonElement b in map.GetProperty("bindings").EnumerateArray())
                    {
                        string path = b.TryGetProperty("path", out JsonElement p) ? p.GetString() : null;
                        bool composite = b.TryGetProperty("isComposite", out JsonElement c) && c.GetBoolean();
                        if (composite || string.IsNullOrEmpty(path) || path.IndexOf('{') >= 0 || path.StartsWith("*", StringComparison.Ordinal))
                        {
                            continue;   // (a composite's own row, or a binding by usage: its parts carry the keys)
                        }
                        uses.Add(new KeyUse { Path = path, Who = who + ": " + mapName + "/" + b.GetProperty("action").GetString() });
                    }
                }
            }
        }
        return found;
    }

    // A value on the IL stack as far as a straight run of instructions shows it: a constant, a text, or neither.
    private struct Pushed
    {
        public bool Known;
        public int Value;
        public string Text;
    }

    // Every key an assembly's code reads by itself. With bindingId, a newobj of a class named Binding whose first argument
    // is a text records the key under that text ("the mod: binding walk").
    private static void KeyReads(GameAssembly a, string who, Dictionary<int, string> keyCodes, Dictionary<int, string> inputKeys, List<KeyUse> uses, Func<string, string> bindingId)
    {
        MetadataReader md = a.Md;
        var ctx = new GameFiles.Ctx(md, default, null);
        var stack = new List<Pushed>();
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition type = md.GetTypeDefinition(th);
            foreach (MethodDefinitionHandle mh in type.GetMethods())
            {
                MethodDefinition m = md.GetMethodDefinition(mh);
                if (m.RelativeVirtualAddress == 0)
                {
                    continue;
                }
                byte[] il = a.Pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes();
                string where = who + " reads it in " + md.GetString(type.Name) + "." + md.GetString(m.Name);
                stack.Clear();
                int i = 0;
                while (i < il.Length)
                {
                    int op = il[i++];
                    if (op >= 0x15 && op <= 0x1E)
                    {
                        stack.Add(new Pushed { Known = true, Value = op - 0x16 });
                    }
                    else if (op == 0x1F)
                    {
                        stack.Add(new Pushed { Known = true, Value = (sbyte)il[i] });
                        i += 1;
                    }
                    else if (op == 0x20)
                    {
                        stack.Add(new Pushed { Known = true, Value = BitConverter.ToInt32(il, i) });
                        i += 4;
                    }
                    else if (op == 0x72)
                    {
                        stack.Add(new Pushed { Text = md.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il, i) & 0xFFFFFF)) });
                        i += 4;
                    }
                    else if (op == 0x14 || (op >= 0x02 && op <= 0x09))
                    {
                        stack.Add(default);   // ldnull, ldarg.0 to ldloc.3
                    }
                    else if (op == 0x0E || op == 0x11)
                    {
                        stack.Add(default);   // ldarg.s, ldloc.s
                        i += 1;
                    }
                    else if (op == 0x7E)
                    {
                        stack.Add(default);   // ldsfld
                        i += 4;
                    }
                    else if (op == 0x7B)
                    {
                        Replace(stack);   // ldfld: the object for its field
                        i += 4;
                    }
                    else if (op == 0x25)
                    {
                        stack.Add(stack.Count > 0 ? stack[stack.Count - 1] : default);   // dup
                    }
                    else if (op == 0x28 || op == 0x6F || op == 0x73)
                    {
                        Called(md, ctx, BitConverter.ToInt32(il, i), op == 0x73, stack, where, keyCodes, inputKeys, uses, bindingId, who);
                        i += 4;
                    }
                    else if (op == 0x7D || op == 0x80)
                    {
                        Stored(md, ctx, BitConverter.ToInt32(il, i), stack, where, keyCodes, uses);
                        if (op == 0x7D && stack.Count > 0)
                        {
                            stack.RemoveAt(stack.Count - 1);
                        }
                        if (stack.Count > 0)
                        {
                            stack.RemoveAt(stack.Count - 1);
                        }
                        i += 4;
                    }
                    else
                    {
                        stack.Clear();   // anything else ends the straight run: what was pushed before is not followed
                        i += OperandSize(op, il, i);
                    }
                }
            }
        }
    }

    private static void Replace(List<Pushed> stack)
    {
        if (stack.Count > 0)
        {
            stack[stack.Count - 1] = default;
        }
    }

    // A call, callvirt or newobj: a KeyCode argument known on the stack is a key read; then its arguments are taken off.
    private static void Called(MetadataReader md, GameFiles.Ctx ctx, int token, bool newobj, List<Pushed> stack, string where, Dictionary<int, string> keyCodes,
                               Dictionary<int, string> inputKeys, List<KeyUse> uses, Func<string, string> bindingId, string who)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        string name;
        string parent;
        MethodSignature<string> sig;
        if (handle.Kind == HandleKind.MemberReference)
        {
            MemberReference r = md.GetMemberReference((MemberReferenceHandle)handle);
            if (r.GetKind() != MemberReferenceKind.Method)
            {
                stack.Clear();
                return;
            }
            name = md.GetString(r.Name);
            parent = r.Parent.Kind == HandleKind.TypeReference ? md.GetString(md.GetTypeReference((TypeReferenceHandle)r.Parent).Name)
                   : r.Parent.Kind == HandleKind.TypeDefinition ? md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)r.Parent).Name) : null;
            sig = r.DecodeMethodSignature(GameFiles.Short, ctx);
        }
        else if (handle.Kind == HandleKind.MethodDefinition)
        {
            MethodDefinition d = md.GetMethodDefinition((MethodDefinitionHandle)handle);
            name = md.GetString(d.Name);
            parent = md.GetString(md.GetTypeDefinition(d.GetDeclaringType()).Name);
            sig = d.DecodeSignature(GameFiles.Short, ctx);
        }
        else
        {
            stack.Clear();   // a generic method's instance: none of the key reads is one
            return;
        }
        int n = sig.ParameterTypes.Length;
        int args = n + (sig.Header.IsInstance && !newobj ? 1 : 0);
        bool straight = stack.Count >= args;
        int first = stack.Count - n;   // the first parameter's place on the stack
        for (int p = 0; p < n && straight; p++)
        {
            Pushed arg = stack[first + p];
            if (!arg.Known)
            {
                continue;
            }
            string path = null;
            if (sig.ParameterTypes[p] == "KeyCode" && keyCodes.TryGetValue(arg.Value, out string code))
            {
                path = KeyRules.PathOf(code);
            }
            else if (sig.ParameterTypes[p] == "Key" && parent == "Keyboard" && inputKeys.TryGetValue(arg.Value, out string key))
            {
                path = KeyRules.PathOfKey(key);
            }
            if (path == null)
            {
                continue;
            }
            string id = bindingId != null && newobj && parent == "Binding" && n > 0 && stack[first].Text != null ? bindingId(stack[first].Text) : null;
            uses.Add(new KeyUse { Path = path, Who = id != null ? who + ": binding " + id : where });
        }
        if (parent == "Keyboard" && n == 0)
        {
            string path = KeyRules.PathOfProperty(name);
            if (path != null)
            {
                uses.Add(new KeyUse { Path = path, Who = where });
            }
        }
        if (straight)
        {
            stack.RemoveRange(stack.Count - args, args);
        }
        else
        {
            stack.Clear();
        }
        if (newobj || sig.ReturnType != "Void")
        {
            stack.Add(default);
        }
    }

    // A store into a field of type KeyCode with a known constant: a key the code keeps to read (the debug console's toggle).
    private static void Stored(MetadataReader md, GameFiles.Ctx ctx, int token, List<Pushed> stack, string where, Dictionary<int, string> keyCodes, List<KeyUse> uses)
    {
        if (stack.Count == 0 || !stack[stack.Count - 1].Known)
        {
            return;
        }
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        string type = null;
        if (handle.Kind == HandleKind.FieldDefinition)
        {
            type = md.GetFieldDefinition((FieldDefinitionHandle)handle).DecodeSignature(GameFiles.Short, ctx);
        }
        else if (handle.Kind == HandleKind.MemberReference)
        {
            MemberReference r = md.GetMemberReference((MemberReferenceHandle)handle);
            if (r.GetKind() == MemberReferenceKind.Field)
            {
                type = r.DecodeFieldSignature(GameFiles.Short, ctx);
            }
        }
        if (type == "KeyCode" && keyCodes.TryGetValue(stack[stack.Count - 1].Value, out string code) && KeyRules.PathOf(code) != null)
        {
            uses.Add(new KeyUse { Path = KeyRules.PathOf(code), Who = where });
        }
    }

    // The bytes of an instruction's operand after its opcode (ECMA-335 III), for the instructions not followed above.
    private static int OperandSize(int op, byte[] il, int at)
    {
        if (op == 0xFE)
        {
            int two = il[at];
            if (two == 0x06 || two == 0x07 || two == 0x15 || two == 0x16 || two == 0x1C)
            {
                return 1 + 4;
            }
            if (two >= 0x09 && two <= 0x0E)
            {
                return 1 + 2;
            }
            return two == 0x12 || two == 0x19 ? 1 + 1 : 1;
        }
        if ((op >= 0x0E && op <= 0x13) || op == 0x1F || (op >= 0x2B && op <= 0x37) || op == 0xDE)
        {
            return 1;
        }
        if (op == 0x20 || op == 0x22 || (op >= 0x38 && op <= 0x44) || op == 0xDD)
        {
            return 4;
        }
        if (op == 0x21 || op == 0x23)
        {
            return 8;
        }
        if (op == 0x45)
        {
            return 4 + 4 * BitConverter.ToInt32(il, at);
        }
        if (op == 0x72 || op == 0x29 || op == 0x27 || op == 0x28 || op == 0x6F || op == 0x73 || op == 0x70 || op == 0x71 || op == 0x74 || op == 0x75 || op == 0x79
            || (op >= 0x7B && op <= 0x81) || op == 0x8C || op == 0x8D || op == 0x8F || op == 0xA3 || op == 0xA4 || op == 0xA5 || op == 0xC2 || op == 0xC6 || op == 0xD0)
        {
            return 4;
        }
        return 0;
    }
}
