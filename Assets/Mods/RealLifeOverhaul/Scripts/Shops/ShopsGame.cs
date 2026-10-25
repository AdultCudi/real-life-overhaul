using System;
using System.Collections.Generic;
using System.Reflection;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.DayNightCycle;
using Buildings;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Entities;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Shops
{
    /// <summary>A business of one of the new kinds as the tests see it.</summary>
    public sealed class ShopLook
    {
        public string Name;
        public string Type;
        public bool Open;                    // its schedule has it open today
        public int Unmet;                    // requirements the game says are not met
        public string FirstUnmet;            // the locale key of the first of them
        public int Customers;                // the game's customers for it today
        public int Wanting;                  // of those, the ones who want something it sells
    }

    /// <summary>
    /// The only code of the new kinds of shop that touches the game: the types made and registered through the game's mod
    /// interface at its start (whatever the option says, so a save that holds one finds it), furnished from the game's own
    /// type each works as once the game's types are loaded, offered to the player or not by the option, and made the game's
    /// own again for "prepare this save for removing the mod". Nothing per frame: each runs once, at a start, a city load,
    /// a switch of the option or a test.
    /// </summary>
    public static class ShopsGame
    {
        private const string AllowCreation = "ba:businesstag_allowplayercreation";
        private const string Revenue = "ba:businesstag_generatesrevenue";

        // The donor's ways of trading that a kind takes on with its simulation.
        private static readonly string[] DonorTags =
        {
            "ba:businesstag_customersneedpaperbags", "ba:businesstag_customersneedshoppingcontainer", "ba:businesstag_allowtheft"
        };

        public static IModLogger Logger;

        private static readonly BusinessType[] Made = new BusinessType[ShopsCatalogue.Kinds.Length];
        private static readonly bool[] Furnished = new bool[ShopsCatalogue.Kinds.Length];
        private static readonly Sprite[] Icons = new Sprite[ShopsCatalogue.Kinds.Length];
        private static bool _offered;
        private static Action _onLanguage;

        /// <summary>The kinds registered with the game, offered to the player when <paramref name="offered"/>; furnished when the game's types are known.</summary>
        public static int Register(bool offered)
        {
            int done = 0;
            for (int k = 0; k < ShopsCatalogue.Kinds.Length; k++)
            {
                try
                {
                    if (Made[k] != null && BusinessTypeHelper.GetData(ShopsCatalogue.Kinds[k].Type) == Made[k] && _offered == offered)
                    {
                        done++;
                        continue;
                    }
                    if (Made[k] == null && BusinessTypeHelper.GetData(ShopsCatalogue.Kinds[k].Type) != null)
                    {
                        Logger?.Warn("New shops: a business type named " + ShopsCatalogue.Kinds[k].Type + " is already there (another mod's?); this one is left as it is.");
                        continue;
                    }
                    if (Made[k] != null)
                    {
                        ModdingAPI.UnregisterModBusinessType(Made[k].businessTypeName);   // made again with or without the player's founding
                    }
                    BusinessType t = Make(ShopsCatalogue.Kinds[k], k, offered);
                    Made[k] = null;
                    Furnished[k] = false;
                    if (ModdingAPI.RegisterModBusinessType(t))
                    {
                        Made[k] = t;
                        done++;
                    }
                    else
                    {
                        Logger?.Warn("New shops: the game refused the business type " + ShopsCatalogue.Kinds[k].Type + ".");
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _offered = offered;
            Furnish();
            return done;
        }

        /// <summary>How many kinds are registered, and how many of them are furnished as the game's types they work as.</summary>
        public static void Count(out int registered, out int furnished)
        {
            registered = furnished = 0;
            for (int k = 0; k < Made.Length; k++)
            {
                if (Made[k] != null && BusinessTypeHelper.GetData(Made[k].businessTypeName) == Made[k])
                {
                    registered++;
                    furnished += Furnished[k] && Made[k].simulator != null ? 1 : 0;
                }
            }
        }

        public static bool Offered => _offered;

        // A shop business of the retail buildings; what it trades like comes in Furnish.
        private static BusinessType Make(ShopKind kind, int index, bool offered)
        {
            BusinessType t = ScriptableObject.CreateInstance<BusinessType>();
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            t.name = kind.Type;
            t.businessTypeName = kind.Type;
            t.suitableBuildingType = ShopsCatalogue.BuildingType;
            t.simulator = null;                 // no trade before Furnish: the game skips a type without one
            t.spawnCustomers = false;           // no customers indoors before it knows how they are served
            t.spawnCustomersOnlyOnEnter = false;
            t.acceptCustomersWithoutOrderEntries = false;
            t.hasEntranceFee = false;
            t.hasWeekendOnlyEntranceFee = false;
            t.minutesToCheckBeforeTheSpawnTimeInAiBusinesses = 60f;
            t.icon = Icon(kind, index);
            t.cityMapFilterColor = new Color(kind.Red, kind.Green, kind.Blue, 1f);
            t.businessProducts = Products(kind);
            t.productSources = (string[])kind.Sources.Clone();
            t.employeePrimarySkills = new[] { "ba:skill_customerservice" };
            t.employeeDefaultAppearanceTags = Array.Empty<AppearanceTag>();
            t.customerType = CustomerType.None;
            t.maxAmountPerProduct = kind.MostEach / 10f;
            t.businessRequirements = new List<BusinessRequirement>();
            t.uniforms = new List<EmployeePreset>();
            t.customerDemandSets = new List<CustomerDemandSet>();
            t.logoShapes = new List<string> { "notfound" };
            t.dayFactorMultipliers = Days(kind);
            t.hourlyFactorMultipliers = Hours(kind);
            t.aliases = (string[])kind.Aliases.Clone();
            t.hasPedestriansOutside = false;
            t.hasMusicOutside = false;
            t.radioVolume = 0.15f;
            if (offered)
            {
                t.AddTag(AllowCreation, null);
            }
            t.AddTag(Revenue, null);
            return t;
        }

        private static BusinessProduct[] Products(ShopKind kind)
        {
            var products = new BusinessProduct[kind.Goods.Length];
            for (int i = 0; i < products.Length; i++)
            {
                products[i] = new BusinessProduct { itemName = kind.Goods[i].Item, impact = kind.Goods[i].Impact / 1000f };
            }
            return products;
        }

        private static List<DayFactorMultiplier> Days(ShopKind kind)
        {
            var days = new List<DayFactorMultiplier>(7);
            for (int d = 1; d <= 7; d++)
            {
                days.Add(new DayFactorMultiplier { dayOfWeekOrdered = (DayOfWeekOrdered)d, multiplier = ShopsRules.DayShare(kind, d) / 1000f });
            }
            return days;
        }

        private static List<HourlyFactorMultiplier> Hours(ShopKind kind)
        {
            var hours = new List<HourlyFactorMultiplier>(kind.Hours.Length);
            for (int i = 0; i < kind.Hours.Length; i++)
            {
                hours.Add(new HourlyFactorMultiplier { startingHour = kind.Hours[i].From, endingHour = kind.Hours[i].To, multiplier = kind.Hours[i].Share / 1000f });
            }
            return hours;
        }

        // Drawn once and kept: the map's filter rows and BizMan take a type's icon when they are built.
        private static Sprite Icon(ShopKind kind, int index)
        {
            if (Icons[index] != null)
            {
                return Icons[index];
            }
            byte[] alpha = ShopsRules.Glyph(kind.Glyph);
            var pixels = new Color32[alpha.Length];
            int size = ShopsRules.Size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[(size - 1 - y) * size + x] = new Color32(255, 255, 255, alpha[y * size + x]);   // the texture's rows run upwards
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RLO shop icon " + index, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;   // kept for the game's whole run: the map and BizMan hold it
            Icons[index] = sprite;
            return sprite;
        }

        /// <summary>Each kind given what it trades like from the game's type it works as, once that type is loaded.</summary>
        public static void Furnish()
        {
            for (int k = 0; k < Made.Length; k++)
            {
                if (Made[k] == null || Furnished[k])
                {
                    continue;
                }
                try
                {
                    BusinessType like = BusinessTypeHelper.GetData(ShopsCatalogue.Kinds[k].Donor);
                    if (like == null || like.simulator == null)
                    {
                        continue;   // the game's types are not loaded yet: the next city load
                    }
                    Take(Made[k], like);
                    Furnished[k] = true;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private static void Take(BusinessType t, BusinessType like)
        {
            t.courseRequired = like.courseRequired;
            t.callDialogType = like.callDialogType;
            t.customerType = like.customerType;
            t.spawnCustomers = like.spawnCustomers;
            t.spawnCustomersOnlyOnEnter = like.spawnCustomersOnlyOnEnter;
            t.acceptCustomersWithoutOrderEntries = like.acceptCustomersWithoutOrderEntries;
            t.minutesToCheckBeforeTheSpawnTimeInAiBusinesses = like.minutesToCheckBeforeTheSpawnTimeInAiBusinesses;
            t.employeePrimarySkills = like.employeePrimarySkills != null ? (string[])like.employeePrimarySkills.Clone() : t.employeePrimarySkills;
            t.employeeDefaultAppearanceTags = like.employeeDefaultAppearanceTags != null ? (AppearanceTag[])like.employeeDefaultAppearanceTags.Clone() : t.employeeDefaultAppearanceTags;
            t.uniforms = like.uniforms != null ? new List<EmployeePreset>(like.uniforms) : t.uniforms;
            t.customerDemandSets = like.customerDemandSets != null ? new List<CustomerDemandSet>(like.customerDemandSets) : t.customerDemandSets;
            t.businessRequirements = like.businessRequirements != null ? new List<BusinessRequirement>(like.businessRequirements) : t.businessRequirements;
            if (like.logoShapes != null && like.logoShapes.Count > 0)
            {
                t.logoShapes = new List<string>(like.logoShapes);
            }
            t.hasPedestriansOutside = like.hasPedestriansOutside;
            t.pedestrianPool = like.pedestrianPool;
            t.hasMusicOutside = like.hasMusicOutside;
            t.radioVolume = like.radioVolume;
            t.radioStation = like.radioStation;
            for (int i = 0; i < DonorTags.Length; i++)
            {
                if (like.HasTag(DonorTags[i]))
                {
                    t.AddTag(DonorTags[i], null);
                }
            }
            t.simulator = like.simulator;   // last: from here on the game trades the type
        }

        /// <summary>For "prepare this save for removing the mod": every shop of these kinds made the game's own type it is closest to. Returns how many.</summary>
        public static int Strip(GameInstance save)
        {
            int n = 0;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                ShopKind kind = reg != null ? ShopsCatalogue.Of(reg.businessTypeName) : null;
                if (kind != null)
                {
                    reg.businessTypeName = kind.Template;
                    n++;
                }
            }
            return n;
        }

        /// <summary>The player's own businesses of the new kinds, the one they stand in first.</summary>
        public static void Mine(List<BuildingRegistration> into)
        {
            into.Clear();
            BuildingRegistration here = Here();
            if (here != null && ShopsCatalogue.Is(here.businessTypeName) && GoingPublic.Mine(here))
            {
                into.Add(here);
            }
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg != here && reg.RentedByPlayer && ShopsCatalogue.Is(reg.businessTypeName) && GoingPublic.Mine(reg) && GameRegistrations.Found(reg))
                {
                    into.Add(reg);
                }
            }
        }

        private static BuildingRegistration Here()
        {
            return BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
        }

        /// <summary>What the game makes of a shop today: open or not, its requirements, its customers and what they want. Reads only.</summary>
        public static ShopLook Look(BuildingRegistration reg)
        {
            var look = new ShopLook { Name = reg.BusinessName, Type = reg.businessTypeName, Open = BusinessHelper.IsBusinessOpen(reg, 12) || Opens(reg) };
            BusinessType type = BusinessTypeHelper.GetData(reg);
            for (int r = 0; type != null && type.businessRequirements != null && r < type.businessRequirements.Count; r++)
            {
                BusinessRequirement requirement = type.businessRequirements[r];
                if (requirement != null && !requirement.IsRequirementMet(reg))
                {
                    look.Unmet++;
                    look.FirstUnmet = look.FirstUnmet ?? requirement.GetLocalizeKey();
                }
            }
            List<CustomerEntry> entries = CustomerEntriesHelper.GetEntriesByAddress(reg.Address);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CustomerEntry entry = entries[i];
                if (entry == null || entry.spawnTime == null || entry.spawnTime.Day != SaveGameManager.Current.Day)
                {
                    continue;
                }
                look.Customers++;
                look.Wanting += entry.order != null && entry.order.entries != null && entry.order.entries.Count > 0 ? 1 : 0;
            }
            return look;
        }

        private static bool Opens(BuildingRegistration reg)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                if (BusinessHelper.IsBusinessOpen(reg, hour))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Each kind's help page given to the game's language tables under the keys its help screen reads (the page's
        /// title from the kind's name, its text from the mod's own key), in every language loaded. The game makes the tables
        /// anew at each change of language and then says so (OnLanguageChanged), so this runs then. Returns the pages
        /// written, or -1 when the game's tables are not where the contract says.
        /// </summary>
        public static int Pages()
        {
            FieldInfo field = GameContract.LocaleTables.Field;
            if (!(field?.GetValue(null) is Dictionary<string, Dictionary<string, string>> tables))
            {
                return -1;
            }
            int written = 0;
            foreach (Dictionary<string, string> table in tables.Values)
            {
                for (int k = 0; table != null && k < ShopsCatalogue.Kinds.Length; k++)
                {
                    string type = ShopsCatalogue.Kinds[k].Type;
                    if (table.TryGetValue(ShopsRules.HelpTextKey(type), out string text))
                    {
                        table[ShopsRules.HelpContentKey(type)] = text;   // the game's tables hold their keys in lower case, as these are
                        written++;
                    }
                    if (table.TryGetValue(type, out string name))
                    {
                        table[ShopsRules.HelpSlug(type)] = name;
                    }
                }
            }
            return written;
        }

        /// <summary>Whether the game's tables now hold a kind's help page in the language shown.</summary>
        public static bool HasPage(string type)
        {
            string key = ShopsRules.HelpContentKey(type);
            return LocalizorManager.IsLocalizedKey(key);
        }

        /// <summary>The pages written again after each load of the game's language tables (on), or no more (off, the mod going).</summary>
        public static void Listen(bool on)
        {
            if (_onLanguage == null)
            {
                _onLanguage = Guard.Wrap(Rewrite);   // made once and kept: the event compares it
            }
            LocalizorManager.OnLanguageChanged = (Action)Delegate.Remove(LocalizorManager.OnLanguageChanged, _onLanguage);
            if (on)
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Combine(LocalizorManager.OnLanguageChanged, _onLanguage);
            }
        }

        // After each load of the game's language tables: the help pages written into them again.
        private static void Rewrite()
        {
            if (Pages() < 0)
            {
                Logger?.Info("New shops: the game's language tables are not where the contract says; the help pages of the new kinds of shop are not given.");
            }
        }

        /// <summary>The game's help screen opened at a kind's page, as BizMan's requirement link opens it.</summary>
        public static bool OpenHelp(string slug)
        {
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            if (help == null)
            {
                return false;
            }
            help.Toggle(true, slug);
            return true;
        }

        /// <summary>The name the game shows for a business type, in the player's language.</summary>
        public static string Named(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization();
        }

        public static bool Loaded()
        {
            return SaveGameManager.Current != null && ModState.Loaded;
        }

        public static void Notice(NotificationType kind, string key, Dictionary<string, string> data)
        {
            Notifications.Show(kind, key, data, 8f);
        }
    }
}
