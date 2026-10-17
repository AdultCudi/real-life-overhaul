using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    // The contacts' pictures (Scripts/Phone/Contacts/ContactCatalogue.cs): every contact the mod adds has an entry and every
    // entry is a contact the mod adds; every logo shape is one of the game's own; the look seed is the same on every run;
    // and the search's word matching, with case and accents.
    internal static partial class Program
    {
        private const string GameShapes = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\LogoShapes";

        private static void CheckContacts(string src)
        {
            Section("Contact pictures");
            var catalogue = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContactLook look in ContactCatalogue.All)
            {
                if (!catalogue.Add(look.Id)) Fail($"contact catalogue: {look.Id} is in it twice");
                if (look.Kind == PictureKind.Logo && Array.IndexOf(ContactCatalogue.Shapes, look.Shape) < 0) Fail($"contact catalogue: {look.Id}'s shape '{look.Shape}' is not in the list of shapes");
                if (look.Kind == PictureKind.Person && (look.Age < 18 || look.Age > 85)) Fail($"contact catalogue: {look.Id} is {look.Age}");
            }

            // Every "reallife-..." id of a file that adds contacts: the files that call Contact.GetContact.
            var added = new HashSet<string>(StringComparer.Ordinal);
            var pictured = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(Path.Combine(src, "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("ContactPictures.Person(")) pictured.Add(Path.GetFileName(file));
                if (!text.Contains("Contact.GetContact(")) continue;
                // A contact's id is declared as a constant, or in a list of ids (Economy/Banking/Debt/DebtPhone.ContactIds); a speaker's name in a call (npcNameKey) is not one.
                foreach (Match m in Regex.Matches(text, @"const string \w+ = ""(reallife-[a-z0-9-]+)"""))
                {
                    added.Add(m.Groups[1].Value);
                }
                foreach (Match list in Regex.Matches(text, @"ContactIds = \{([^}]*)\}"))
                {
                    foreach (Match m in Regex.Matches(list.Groups[1].Value, "\"(reallife-[a-z0-9-]+)\""))
                    {
                        added.Add(m.Groups[1].Value);
                    }
                }
            }
            foreach (string id in added)
            {
                if (!catalogue.Contains(id)) Fail($"contact catalogue: '{id}' is added as a contact but has no picture in the catalogue");
            }
            foreach (string id in catalogue)
            {
                if (!added.Contains(id)) Fail($"contact catalogue: '{id}' is in the catalogue but no file adds it as a contact");
            }
            foreach (string must in new[] { "SocialPhone.cs", "FamilyPhone.cs", "RoommatePhone.cs" })
            {
                if (!pictured.Contains(must)) Fail($"contact pictures: {must} no longer gives its people their own faces");
            }
            Console.WriteLine($"  {catalogue.Count} contacts in the catalogue, {added.Count} ids in the files that add contacts");

            if (Directory.Exists(GameShapes))
            {
                foreach (string shape in ContactCatalogue.Shapes)
                {
                    if (!File.Exists(Path.Combine(GameShapes, shape + ".png"))) Fail($"contact catalogue: the game has no logo shape '{shape}'");
                }
                if (!File.Exists(Path.Combine(GameShapes, "notfound.png"))) Fail("contact catalogue: the game has no 'notfound' logo shape");
                Console.WriteLine($"  {ContactCatalogue.Shapes.Length} logo shapes found among the game's own");
            }
            else
            {
                Console.WriteLine("  (the game is not installed here: its logo shapes are not checked)");
            }

            // The seed: the same on every run, never negative, a different face for another save and for another contact.
            int mom = ContactCatalogue.Seed("reallife-mom", "character-1");
            if (mom != ContactCatalogue.Seed("reallife-mom", "character-1") || mom < 0) Fail("contact seed: not the same twice, or negative");
            if (mom == ContactCatalogue.Seed("reallife-mom", "character-2")) Fail("contact seed: two saves give a contact one face");
            if (mom == ContactCatalogue.Seed("reallife-friend", "character-1")) Fail("contact seed: two contacts have one face");
            if (ContactCatalogue.Seed("reallife-mom", "character-1") != 858867700) Fail("contact seed: FNV-1a has changed (" + mom + "): every drawn face would be drawn again");
            if (ContactCatalogue.Seed(null, null) < 0) Fail("contact seed: nothing gives a negative seed");
            if (ContactCatalogue.CacheName(true, 58, 12) != "v1_f58_12.jpg" || ContactCatalogue.CacheName(false, -1, -5) != "v1_m0_0.jpg") Fail("contact cache name: not as expected");

            // The search: every word, in the name or the description, case and accents ignored.
            CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
            Func<string, string, bool> contains = (t, w) => compare.IndexOf(t, w, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            var cases = new (string query, string name, string about, bool hit)[]
            {
                ("", "Mom", "", true),
                ("   ", "Mom", "", true),
                ("mom", "Mom", "", true),
                ("MO", "Mom", "", true),
                ("dr pat", "Dr. Patel", "Family doctor", true),
                ("doctor", "Dr. Patel", "Family doctor, phone consultations", true),
                ("patel lawyer", "Dr. Patel", "Family doctor", false),
                ("mae", "M\u00e3e", "", true),
                ("ONIBUS", "Ponto de \u00f4nibus", "", true),
                ("taxi", "City Cabs", "", false),
                ("cabs", "City Cabs", null, true),
                ("x", null, null, false)
            };
            foreach (var c in cases)
            {
                if (ContactCatalogue.Matches(c.query, c.name, c.about, contains) != c.hit) Fail($"contact search: '{c.query}' in '{c.name}' / '{c.about}' should be {(c.hit ? "found" : "not found")}");
            }
            Console.WriteLine($"  search: {cases.Length} cases");
        }
    }
}
