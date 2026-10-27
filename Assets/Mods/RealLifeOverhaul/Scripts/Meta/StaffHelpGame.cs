using System;
using System.Collections.Generic;
using System.Reflection;
using Localizor;
using RealLifeOverhaul.Staffing;
using UnityEngine.UI.Extensions.HelpSystem;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Staff and roles entries in the game's own help (F1): each entry's page is written into the game's language table under the key
    /// the help reads (as the new shops' pages are), and the two lists are added to the help's categories so they are searched like any other
    /// page. The help reads its list from a file in the game's folder, so the lists are put in its private list of categories after each load
    /// of it, and the help is made to build its list again (its own LoadCategories). The game's help is otherwise left alone. Costs a few
    /// milliseconds at a city load and at a change of language, and one look at the list on each page the help shows; nothing per frame.
    /// </summary>
    public static class StaffHelpGame
    {
        private const string FirstCategory = "reallife_staff_cat_places";

        private static Action _onLanguage;
        private static UnityEngine.Events.UnityAction<string> _onPage;
        private static HelpSystem _hooked;

        /// <summary>Writes the pages and hooks the help, once per city load; false when the game's help is not built as the contract says.</summary>
        public static bool Begin()
        {
            if (GameContract.StaffHelp.Off)
            {
                return false;
            }
            Pages();
            if (_onLanguage == null)
            {
                _onLanguage = Guard.Wrap(OnLanguage);
                _onPage = Guard.Listener<string>(OnPage);
            }
            LocalizorManager.OnLanguageChanged = (Action)Delegate.Remove(LocalizorManager.OnLanguageChanged, _onLanguage);
            LocalizorManager.OnLanguageChanged = (Action)Delegate.Combine(LocalizorManager.OnLanguageChanged, _onLanguage);
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            if (help != null && help != _hooked)
            {
                help.currentSlugChanged.RemoveListener(_onPage);
                help.currentSlugChanged.AddListener(_onPage);
                _hooked = help;
            }
            return true;
        }

        /// <summary>Takes the hooks off at the city's end.</summary>
        public static void End()
        {
            if (_onLanguage != null)
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Remove(LocalizorManager.OnLanguageChanged, _onLanguage);
            }
            if (_hooked != null && _onPage != null)
            {
                _hooked.currentSlugChanged.RemoveListener(_onPage);
            }
            _hooked = null;
        }

        private static void OnLanguage()
        {
            Pages();
            Inject();
        }

        // Each page the help shows: the lists are added if the help has been loaded anew since.
        private static void OnPage(string slug)
        {
            Inject();
        }

        /// <summary>The pages in the language shown now, written into the game's table under "help_{slug}_content". Returns how many; -1 when the table is not where the contract says.</summary>
        public static int Pages()
        {
            FieldInfo field = GameContract.LocaleTables.Field;
            if (!(field?.GetValue(null) is Dictionary<string, Dictionary<string, string>> tables) || !tables.TryGetValue(LocalizorManager.LoadedLocale, out Dictionary<string,
                string> table) || table == null)
            {
                return -1;
            }
            Dictionary<string, string> english = null;
            foreach (Dictionary<string, string> other in tables.Values)
            {
                if (other != null && other.TryGetValue("reallife_staff_cat_roles", out string roles) && roles == "Roles")
                {
                    english = other;
                    break;
                }
            }
            StaffStyle style = StaffPages.MarkdownStyle(key => table.TryGetValue(key, out string v) ? v : english != null && english.TryGetValue(key, out string e) ? e : key,
                cents => Guide.Money(Prices.At((int)Math.Min(int.MaxValue / 4, cents / 100))));
            StaffEntry[] all = StaffIndex.Entries;
            for (int i = 0; i < all.Length; i++)
            {
                table["help_" + all[i].NameKey + "_content"] = StaffPages.AsMarkdown(StaffPages.Page(all[i], style));
            }
            return all.Length;
        }

        // Puts the two lists in the help's categories if they are not there, then has the help build its list again. The page shown stays.
        private static void Inject()
        {
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            FieldInfo field = GameContract.HelpCategories.Field;
            MethodInfo load = GameContract.HelpLoad.Method;
            if (help == null || field == null || load == null || !(field.GetValue(help) is List<HelpStructureGroupEntry> categories))
            {
                return;
            }
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].CategoryLocalizorKey == FirstCategory)
                {
                    return;
                }
            }
            for (int c = 0; c < 2; c++)
            {
                var group = new HelpStructureGroupEntry { CategoryLocalizorKey = c == 0 ? FirstCategory : "reallife_staff_cat_roles", Pages = new List<HelpStructurePageEntry>(64) };
                foreach (StaffEntry entry in StaffIndex.Sorted(c == 0, key => key.GetLocalization()))
                {
                    group.Pages.Add(new HelpStructurePageEntry { Slug = entry.NameKey, PageLocalizorKeyPrefix = entry.NameKey });
                }
                categories.Add(group);
            }
            load.Invoke(help, null);
        }
    }
}
