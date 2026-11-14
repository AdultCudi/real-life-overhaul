using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of the Mods page's rows for other mods' options: a field of one kind of the game's control is
    /// read safely from a control of another kind, and each kind of option reads only its own control's field, so a
    /// drop-down, a slider or a button is drawn as itself and never stops the page.
    /// </summary>
    internal static class ModRowCheck
    {
        private sealed class ToggleControl { public object toggle = "toggle"; }
        private sealed class DropdownControl { public object dropdown = "dropdown"; }
        private sealed class SliderControl { public object slider = "slider"; }
        private sealed class ButtonControl { public object button = "button"; }

        public static void Run(Action<string> fail, string src)
        {
            Console.WriteLine("\n== The Mods page: other mods' rows ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("mods page: " + message);
            }
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public;
            var fields = new Dictionary<string, FieldInfo>
            {
                ["toggle"] = typeof(ToggleControl).GetField("toggle", all),
                ["dropdown"] = typeof(DropdownControl).GetField("dropdown", all),
                ["slider"] = typeof(SliderControl).GetField("slider", all),
                ["button"] = typeof(ButtonControl).GetField("button", all),
            };
            object[] controls = { new ToggleControl(), new DropdownControl(), new SliderControl(), new ButtonControl() };
            foreach (KeyValuePair<string, FieldInfo> field in fields)
            {
                foreach (object control in controls)
                {
                    bool own = field.Value.DeclaringType == control.GetType();
                    object read = null;
                    string thrown = null;
                    try { read = OptionsRules.Read(field.Value, control); }
                    catch (Exception e) { thrown = e.GetType().Name; }
                    Check(thrown == null, "reading the " + field.Key + " field of a " + control.GetType().Name + " throws " + thrown);
                    Check(own ? Equals(read, field.Key) : read == null, "reading the " + field.Key + " field of a " + control.GetType().Name + " gives " + (read ?? "nothing"));
                }
            }
            Check(OptionsRules.Read(null, controls[0]) == null && OptionsRules.Read(fields["toggle"], null) == null, "a missing field or control does not read as nothing");

            string mods = File.ReadAllText(Path.Combine(src, "Scripts", "Ui", "Options", "OptionsScreen.Mods.cs"));
            var reads = new (string kind, string member)[] { ("ToggleOption", "ModsToggle"), ("SliderOption", "ModsSlider"), ("DropdownOption", "ModsDropdown"), ("ButtonOption", "ModsButton") };
            foreach ((string kind, string member) in reads)
            {
                Check(Regex.IsMatch(mods, @"=\s*option is " + kind + @"\s*\?\s*BuyKits\.Get<[\w.]+>\(GameContract\." + member + @","),
                    "the row reads GameContract." + member + " for options other than a " + kind);
            }
            Check(Regex.IsMatch(mods, @"else if \(dropdown != null\)\s*\{\s*AddChoice\("), "a drop-down option is not drawn as a choice");

            string kits = File.ReadAllText(Path.Combine(src, "Scripts", "Ui", "Buy", "BuyKits.cs"));
            Check(!kits.Contains(".Field.GetValue("), "BuyKits reads a field without asking whether the owner holds it");
            Console.WriteLine($"  {checks} checks of the Mods page's rows");
        }
    }
}
