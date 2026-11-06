using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    // The Contacts app's groups (Scripts/Phone/Contacts: ContactGroups, ContactCatalogue's Group, ContactGroupsGame, ContactGroupsRow,
    // ContactGroupsTest, ContactSearch). Every contact the mod adds is in exactly one group, named in the one registry (the catalogue); a
    // contact with none fails here. The grouping rules, the plan that moves contacts (nothing lost, the game's own untouched, off puts
    // every contact back, the same twice), the row's fit, the names and texts of the groups, and the sources that wire it in.
    internal static partial class Program
    {
        private static void CheckContactGroups(string src, Dictionary<string, string> en)
        {
            Section("Contact groups");
            var catalogue = new HashSet<string>(StringComparer.Ordinal);
            var perGroup = new int[ContactGroups.Count + 1];

            // The registry: every entry names a group of the seven, and the rule finds it by the id.
            foreach (ContactLook look in ContactCatalogue.All)
            {
                catalogue.Add(look.Id);
                if (look.Group == ContactGroup.None || (int)look.Group < 1 || (int)look.Group > ContactGroups.Count)
                {
                    Fail($"contact groups: '{look.Id}' has no group in the catalogue (Phone/Contacts/ContactCatalogue.cs): a contact of the mod is in one group");
                    continue;
                }
                perGroup[(int)look.Group]++;
                ContactGroup found = ContactGroups.GroupOf(look.Id, null, out int plain);
                if (found != look.Group) Fail($"contact groups: the rule puts '{look.Id}' in {found}, the catalogue says {look.Group}");
                if (plain != look.Plain) Fail($"contact groups: '{look.Id}' goes back to category {plain} with groups off, the catalogue says {look.Plain}");
            }
            for (int g = 1; g <= ContactGroups.Count; g++)
            {
                if (perGroup[g] == 0 && (ContactGroup)g != ContactGroup.Friends) Fail($"contact groups: no contact of the catalogue is in {(ContactGroup)g}");
            }
            Console.WriteLine($"  {ContactCatalogue.All.Length} catalogue contacts in {perGroup.Count(n => n > 0)} groups: {string.Join(", ", Enumerable.Range(1, ContactGroups.Count).Select(g => (ContactGroup)g + " " + perGroup[g]))}");

            // The two kinds with no fixed id: the boss of a job and the people met; a "reallife-" id that is not registered is Other; the game's own is none.
            if (ContactGroups.GroupOf("Acme Ltd", ContactGroups.BossDescription, out int bossPlain) != ContactGroup.Work || bossPlain != ContactGroups.PlainEmployees) Fail("contact groups: a boss is not in Work, with Employees as its plain category");
            if (ContactGroups.GroupOf("Dana Reyes", ContactGroups.PersonDescriptionStart + "gym", out int personPlain) != ContactGroup.Friends || personPlain != ContactGroups.PlainGeneral) Fail("contact groups: a person met is not in Friends");
            if (ContactGroups.GroupOf("reallife-brand-new", "reallife_contact_x_desc", out int strayPlain) != ContactGroup.Other || strayPlain != ContactGroups.PlainGeneral) Fail("contact groups: an unregistered 'reallife-' contact is not in Other");
            if (ContactGroups.GroupOf("uncle_fred", "reallife_contact_x_desc", out _) != ContactGroup.None || ContactGroups.GroupOf("Vantander Bank", "Bank", out _) != ContactGroup.None
                || ContactGroups.GroupOf(null, null, out _) != ContactGroup.None) Fail("contact groups: one of the game's own contacts is put in a group");

            // The people's descriptions: every origin after the first two (Mom and the old friend are catalogue ids) starts the person mark.
            string social = File.ReadAllText(Path.Combine(src, "Scripts", "Social", "SocialCatalogue.cs"));
            Match origins = Regex.Match(social, @"OriginDesc\s*=\s*\{([^}]*)\}");
            string[] descriptions = Regex.Matches(origins.Groups[1].Value, "\"([a-z_]+)\"").Select(m => m.Groups[1].Value).ToArray();
            if (descriptions.Length < 3) Fail("contact groups: the people's descriptions could not be read from SocialCatalogue.OriginDesc");
            for (int i = 2; i < descriptions.Length; i++)
            {
                if (ContactGroups.GroupOf("Somebody", descriptions[i], out _) != ContactGroup.Friends) Fail($"contact groups: somebody met with the description '{descriptions[i]}' is not in Friends");
            }

            // The category numbers: beyond the game's seven, one for each group, and back.
            for (int c = 0; c <= 6; c++)
            {
                if (ContactGroups.GroupOfCategory(c) != ContactGroup.None) Fail($"contact groups: the game's own category {c} is read as a group");
            }
            for (int g = 1; g <= ContactGroups.Count; g++)
            {
                int category = ContactGroups.CategoryOf((ContactGroup)g);
                if (category <= 6 || ContactGroups.GroupOfCategory(category) != (ContactGroup)g) Fail($"contact groups: group {g} does not round-trip through category {category}");
            }
            if (ContactGroups.GroupOfCategory(ContactGroups.Base) != ContactGroup.None || ContactGroups.GroupOfCategory(ContactGroups.Base + ContactGroups.Count + 1) != ContactGroup.None)
                Fail("contact groups: a number beside the groups is read as a group");
            if (ContactGroups.Glyphs.Length != ContactGroups.Count || ContactGroups.Glyphs.Any(string.IsNullOrEmpty) || ContactGroups.Glyphs.Distinct().Count() != ContactGroups.Count)
                Fail("contact groups: not a picture of its own for each group");

            // The plan: a phone with the game's own contacts and the mod's, moved into the groups and back.
            var ids = new List<string>();
            var about = new List<string>();
            var categories = new List<int>();
            void Add(string id, string description, int now) { ids.Add(id); about.Add(description); categories.Add(now); }
            Add("Vantander Bank", "Bank", 2);
            Add("uncle_fred", "uncle_fred_desc", 0);
            Add("Rival Co", "Rival", 6);
            Add("Pat Employee", "employee_contact_description", 5);
            foreach (ContactLook look in ContactCatalogue.All) Add(look.Id, "reallife_x_desc", look.Plain);
            Add("Acme Ltd", ContactGroups.BossDescription, ContactGroups.PlainEmployees);
            Add("Dana Reyes", ContactGroups.PersonDescriptionStart + "gym", 0);
            Add("reallife-brand-new", "reallife_x", 0);
            string[] planIds = ids.ToArray(), planAbout = about.ToArray();
            int[] before = categories.ToArray(), working = categories.ToArray();
            int moved = ContactGroups.Regroup(planIds, planAbout, working, true);
            int ours = planIds.Length - 4;
            if (moved != ours) Fail($"contact groups: {moved} of the mod's {ours} contacts moved into groups");
            for (int i = 0; i < planIds.Length; i++)
            {
                bool theirs = i < 4;
                if (theirs && working[i] != before[i]) Fail($"contact groups: the game's own contact '{planIds[i]}' was moved");
                if (!theirs && ContactGroups.GroupOfCategory(working[i]) == ContactGroup.None) Fail($"contact groups: '{planIds[i]}' is in no group after grouping");
            }
            if (working.Length != before.Length) Fail("contact groups: contacts were lost in the plan");
            int total = 0;
            for (int c = 0; c <= 6; c++) total += ContactGroups.CountIn(working, c);
            for (int g = 1; g <= ContactGroups.Count; g++) total += ContactGroups.CountIn(working, ContactGroups.CategoryOf((ContactGroup)g));
            if (total != planIds.Length) Fail($"contact groups: the categories hold {total} of {planIds.Length} contacts after grouping: some are in no category");
            if (ContactGroups.Regroup(planIds, planAbout, working, true) != 0) Fail("contact groups: grouping twice moved contacts again");
            int back = ContactGroups.Regroup(planIds, planAbout, working, false);
            if (back != ours || !working.SequenceEqual(before)) Fail("contact groups: with groups off the contacts are not back where they were");
            Console.WriteLine($"  plan: {planIds.Length} contacts, {ours} of the mod moved in and back, the game's 4 untouched, none lost");

            // The row: more buttons than the width holds shrink the row to fit, never grow it, and stay readable at the most there can be.
            const float each = 215f, spacing = -75f, padding = 50f, room = 1173.8f;   // the game's row (the phone UI dump of 13 Oct 2026)
            for (int n = 1; n <= 2 * 7; n++)
            {
                float need = ContactGroups.RowNeeds(n, each, spacing, padding), scale = ContactGroups.ScaleFor(need, room);
                if (scale > 1f || scale <= 0f || need * scale > room + 0.01f) Fail($"contact groups: {n} buttons ask {need}, scale {scale} does not fit {room}");
                if (n <= 7 && scale != 1f) Fail($"contact groups: {n} buttons fit the game's row, but they are scaled");
                if (n == 14 && scale < 0.55f) Fail($"contact groups: fourteen buttons are scaled to {scale}, too small to read");
            }
            if (ContactGroups.ScaleFor(0f, room) != 1f || ContactGroups.ScaleFor(100f, 0f) != 1f || ContactGroups.RowNeeds(0, each, spacing, padding) != 0f) Fail("contact groups: an empty row or no room is scaled");

            // The texts: a name for each group in the game's own key, and the option, tip, count and test lines, in the three files.
            foreach (string lang in new[] { "en", "pt", "zh-cn" })
            {
                Dictionary<string, string> words = lang == "en" ? en : ReadLocale(src, lang);
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (int g = 1; g <= ContactGroups.Count; g++)
                {
                    string key = ContactGroups.NameKey((ContactGroup)g);
                    if (!words.TryGetValue(key, out string name) || string.IsNullOrWhiteSpace(name)) Fail($"contact groups: {key} has no text in {lang}.json");
                    else if (!names.Add(name)) Fail($"contact groups: two groups are called '{name}' in {lang}.json");
                    else if (name.Length > 24) Fail($"contact groups: {key} is {name.Length} characters in {lang}.json: a tooltip's title stays short");
                }
                foreach (string key in new[]
                         {
                             "reallife_phone_contact_groups", "reallife_opt_tip_phone_contact_groups", ContactGroups.CountKey, "reallife_contacts_test_groups", "reallife_contacts_test_groups_pass",
                             "reallife_contacts_test_groups_fail", "reallife_contacts_test_groups_off", "reallife_contacts_test_groups_none", "reallife_contract_part_contact_groups"
                         })
                {
                    if (!words.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text)) Fail($"contact groups: {key} has no text in {lang}.json");
                    else if (text.Contains("!") || text.Contains("  ") || text.Contains("—") || text.Contains("–") || text.Contains(" - ")) Fail($"contact groups: {key} in {lang}.json has an exclamation mark, a double space or a dash");
                    else if (lang == "en" && key.StartsWith("reallife_contacts_test_groups_", StringComparison.Ordinal) && text.Length > 240) Fail($"contact groups: {key} is over 240 characters");
                }
                if (words.TryGetValue(ContactGroups.CountKey, out string count) && !count.Contains("{count}")) Fail($"contact groups: the count line in {lang}.json lacks {{count}}");
                if (words.TryGetValue("reallife_contacts_test_groups_pass", out string pass) && !(pass.Contains("{contacts}") && pass.Contains("{groups}"))) Fail($"contact groups: the test's pass line in {lang}.json lacks its figures");
                if (words.TryGetValue("reallife_contacts_test_groups_fail", out string fail) && !fail.Contains("{faults}")) Fail($"contact groups: the test's fail line in {lang}.json lacks {{faults}}");
                if (lang != "en" && words.TryGetValue("reallife_guide_phone", out string guide) && guide.Contains("<b>") == false) Fail($"contact groups: the phone page in {lang}.json lost its markup");
            }

            // What's new, in any row: the line is in all nine languages and stays among the feature lines before the parks' line (fixes come after).
            var marks = new[] { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
            var starts = new Dictionary<string, string>
            {
                ["en"] = "The Contacts app has groups", ["pt"] = "O aplicativo Contatos tem grupos", ["zh-cn"] = "联系人应用现在有分组",
                ["de"] = "Die Kontakte-App hat Gruppen", ["fr"] = "L'application Contacts a des groupes", ["es"] = "La aplicación Contactos tiene grupos",
                ["ru"] = "В приложении «Контакты» появились группы",
                ["nl"] = "De Contacten-app heeft groepen", ["tr"] = "Kişiler uygulamasında artık gruplar var"
            };
            var anchors = new Dictionary<string, string>
            {
                ["en"] = "Uptown's two other squares are parks now", ["pt"] = "As outras duas praças do Uptown agora são parques", ["zh-cn"] = "上城另外两个广场现在是公园",
                ["de"] = "Die beiden anderen Plätze von Uptown sind jetzt Parks", ["fr"] = "Les deux autres places d'Uptown sont désormais des parcs",
                ["es"] = "Las otras dos plazas de Uptown son ahora parques", ["ru"] = "Две другие площади Uptown теперь парки",
                ["nl"] = "De twee andere pleinen van Uptown zijn nu parken", ["tr"] = "Uptown'ın diğer iki meydanı artık park"
            };
            string[] allLangs = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
            for (int l = 0; l < allLangs.Length; l++)
            {
                Dictionary<string, string> words = allLangs[l] == "en" ? en : ReadLocale(src, allLangs[l]);
                bool found = false;
                foreach (KeyValuePair<string, string> row in words.Where(r => r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && r.Key != "reallife_changes_title"))
                {
                    int at = row.Value.IndexOf(starts[allLangs[l]], StringComparison.Ordinal);
                    if (at < 0) continue;
                    found = true;
                    if (row.Value.IndexOf(starts[allLangs[l]], at + 1, StringComparison.Ordinal) >= 0) Fail($"contact groups: What's new holds the groups' line twice in {allLangs[l]}");
                    int parks = row.Key == "reallife_changes_next" ? row.Value.IndexOf(anchors[allLangs[l]], StringComparison.Ordinal) : -1;
                    if (parks >= 0)
                    {
                        // Among the feature lines: before the parks' line (the last feature line), with no fix line between them.
                        if (parks < at) Fail($"contact groups: the groups' line comes after the parks' line in {allLangs[l]}");
                        foreach (string mark in marks)
                        {
                            int fix = row.Value.IndexOf(mark, at, StringComparison.Ordinal);
                            if (fix >= 0 && fix < parks) Fail($"contact groups: a fix line stands between the groups' line and the parks' line in {allLangs[l]}");
                        }
                    }
                }
                if (!found) Fail($"contact groups: What's new lacks the groups' line in {allLangs[l]}");
            }

            // The sources: the registry is the only place that names a contact's category, the option is wired in, the search spans the groups.
            string scripts = Path.Combine(src, "Scripts");
            string settings = File.ReadAllText(Path.Combine(scripts, "Core", "Settings.cs"));
            string mod = File.ReadAllText(Path.Combine(scripts, "Core", "RealLifeOverhaulMod.cs"));
            string features = File.ReadAllText(Path.Combine(scripts, "Meta", "FeatureList.cs"));
            string tree = File.ReadAllText(Path.Combine(scripts, "Meta", "OptionsTree.cs"));
            string contract = File.ReadAllText(Path.Combine(scripts, "Core", "GameContract.cs"));
            string search = File.ReadAllText(Path.Combine(scripts, "Phone", "Contacts", "ContactSearch.cs"));
            string game = File.ReadAllText(Path.Combine(scripts, "Phone", "Contacts", "ContactGroupsGame.cs"));
            string row2 = File.ReadAllText(Path.Combine(scripts, "Phone", "Contacts", "ContactGroupsRow.cs"));
            string life = File.ReadAllText(Path.Combine(scripts, "Life", "LifeEvents.cs"));
            if (!settings.Contains("public const string PhoneContactGroupsId = \"phone_contact_groups\";") || !settings.Contains("public static bool PhoneContactGroups = true;")
                || !settings.Contains("PhoneContactGroups = Bool(modId, PhoneContactGroupsId, PhoneContactGroups);")) Fail("contact groups: the option is not in Settings (an id, on by default, read at load)");
            if (!mod.Contains("AddToggle(Settings.PhoneContactGroupsId, \"reallife_phone_contact_groups\"") || !mod.Contains("ContactGroupsGame.Apply();") || !mod.Contains("AddButton(\"reallife_contacts_test_groups\", ContactGroupsTest.Run)"))
                Fail("contact groups: the option or its test is not on the options page, or the option does not apply at once");
            if (!features.Contains("Settings.PhoneContactGroupsId") || !features.Contains("\"reallife_contacts_test_groups\"") || !features.Contains("ContactGroupsGame.Install()")) Fail("contact groups: the phone feature does not own the option and its test, or does not put the groups back with its repair");
            if (!tree.Contains("\"phone_contact_groups\"")) Fail("contact groups: the option is not in the options tree under Phone");
            if (!life.Contains("ContactGroupsGame.Install();")) Fail("contact groups: the groups are not installed at each load");

            // The first city of a session (Continue and Load are the same call of the game's, LoadGame.Load; what differs is that the Contacts app has not
            // woken, so its two events, made in its Awake and set to null when the game starts, are not there: 25 and 27 Oct 2026, both logs). The install makes
            // them itself, before it uses them, and never gives the groups up for their being null.
            int installAt = game.IndexOf("public static void Install()", StringComparison.Ordinal), eventsAt = game.IndexOf("private static int Events()", StringComparison.Ordinal);
            string install = installAt >= 0 && eventsAt > installAt ? game.Substring(installAt, eventsAt - installAt) : string.Empty;
            if (install.Length == 0) Fail("contact groups: ContactGroupsGame.Install could not be found to be read");
            if (!game.Contains("ContactsApp.onContactAdded = new UnityEvent();") || !game.Contains("ContactsApp.onContactRemoved = new UnityEvent();"))
                Fail("contact groups: the install does not make the Contacts app's two events when the game has not (the app has not woken at the first city of a session)");
            if (Regex.IsMatch(install, @"if\s*\([^)]*(onContactAdded|onContactRemoved)[^)]*==\s*null")) Fail("contact groups: the install gives the groups up when the app's events are null, as they are until it wakes");
            int madeAt = install.IndexOf("Events();", StringComparison.Ordinal), listenAt = install.IndexOf("onContactAdded.RemoveListener(", StringComparison.Ordinal);
            if (madeAt < 0 || listenAt < 0 || madeAt > listenAt) Fail("contact groups: the app's events are not made before the install puts its listeners on them");
            if (!game.Contains("Contacts check (groups, load ") || !game.Contains("Report(made, list, app);")) Fail("contact groups: no self-check line says at each load what the row and the contacts came to");
            foreach (string path in new[] { "TransitionToSave", "saveToLoadData", "LastSaveGameName" })
            {
                if (game.Contains(path) || life.Contains(path)) Fail("contact groups: the install reads '" + path + "', which tells one way of loading from another");
            }
            if (!contract.Contains("\"UI.Smartphone.Apps.Contacts.ContactCategorySelection\", \"categories\"")) Fail("contact groups: the row's list of categories is not a line of the game contract");
            if (!search.Contains("_app.LoadContactsList(null, false);") || !search.Contains("_resume")) Fail("contact groups: the search no longer spans every group, or no longer gives back the category shown");
            if (!game.Contains("contactsLastCategoryName")) Fail("contact groups: moving a contact leaves the app's last category stale (it would open on an empty list)");
            if (!game.Contains("ContactGroups.CategoryFor(") || game.Contains("ContactCategoryName.General")) Fail("contact groups: the game file names a category of its own instead of the registry's");
            if (row2.Contains("void Update(") || row2.Contains("void LateUpdate(") || !row2.Contains("Guard.Routine(")) Fail("contact groups: the row has a per-frame method, or starts its routine outside the guard");

            // The registry is the only place a category is chosen: every call that makes a contact of the mod asks for the plain one of its kind.
            var categoryUses = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var dynamicIds = new HashSet<string>(StringComparer.Ordinal) { "id", "ContactIds[which]", "IdOf(person)", "employer" };
            var constants = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, @"const string (\w+) = ""reallife-[a-z0-9-]+"""))
                {
                    constants.Add(m.Groups[1].Value);
                }
            }
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, @"Contact\.GetContact\(\s*([^,]+?)\s*,\s*ContactCategoryName\.(\w+)\s*,\s*([^,]+?)\s*,"))
                {
                    string idArgument = m.Groups[1].Value, name = Path.GetFileName(file), used = m.Groups[2].Value, description = m.Groups[3].Value;
                    if (!constants.Contains(idArgument) && !dynamicIds.Contains(idArgument))
                        Fail($"contact groups: {name} makes a contact with '{idArgument}', an id the registry cannot know: give it a catalogue entry or a rule in ContactGroups");
                    if (idArgument == "employer" && (name != "DeskGame.cs" || !description.Contains(ContactGroups.BossDescription))) Fail($"contact groups: {name} makes a contact by an employer's name that is not the boss's");
                    if (idArgument == "IdOf(person)" && !description.Contains("OriginDesc")) Fail($"contact groups: {name} makes a person's contact whose description is not an origin's");
                    if (!categoryUses.TryGetValue(used, out HashSet<string> files)) categoryUses[used] = files = new HashSet<string>(StringComparer.Ordinal);
                    files.Add(name);
                }
            }
            foreach (KeyValuePair<string, HashSet<string>> use in categoryUses)
            {
                bool allowed = use.Key == "General" || (use.Key == "Business" && use.Value.SetEquals(new[] { "BusinessDesk.cs" })) || (use.Key == "Employees" && use.Value.SetEquals(new[] { "DeskGame.cs" }));
                if (!allowed) Fail($"contact groups: {string.Join(", ", use.Value)} make a contact in the game's {use.Key} category, which the registry does not know as a plain category");
            }
            Console.WriteLine($"  sources: {categoryUses.Values.Sum(v => v.Count)} kinds of call checked, {constants.Count} ids in constants");
        }

        private static Dictionary<string, string> ReadLocale(string src, string lang)
        {
            var words = new Dictionary<string, string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(src, "Locales", lang + ".json"))))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String) words[p.Name] = p.Value.GetString();
                }
            }
            return words;
        }
    }
}
