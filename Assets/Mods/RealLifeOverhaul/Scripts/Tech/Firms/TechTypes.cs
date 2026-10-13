using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Dialogs;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech sector's kinds of business, as business types of the game's own (BAModAPI.ModdingAPI.RegisterModBusinessType,
    /// the call the game gives mods for this, as Empire Motors registers its dealership): a software studio, a game studio,
    /// an IT consultancy, a cybersecurity firm and an AI lab, each in an office building, picked in the game's own list
    /// when a business is started there, tagged as one the player may start and that takes money. Their staff are the
    /// game's programmers, graphic designers and customer service people, so the game's recruitment office, its staff app
    /// and its rota work for them as for a web development agency. The game sends them no customers of its own: what they
    /// sell is the tech sector's (Tech/Firms).
    ///
    /// WHEN. Registered once at the game's start (the main menu), before any save is loaded, and whatever the options say:
    /// a save that holds such a business must find its type, or the game's hourly run would stop on it. The first time
    /// the game's own web development agency is known (at the start, or at the first city load) its requirements,
    /// uniforms, logo shapes and icon are copied onto the registered types, so they are furnished and dressed as one.
    ///
    /// REMOVING THE MOD. "Prepare this save for removing the mod" turns each such business back into a web development
    /// agency, the game's own office business they were copied from (<see cref="Strip"/>), so the save loads without the mod.
    /// </summary>
    public static class TechTypes
    {
        public const string Prefix = "reallife:businesstype_";
        public const string SoftwareStudio = Prefix + "softwarestudio";
        public const string GameStudio = Prefix + "gamestudio";
        public const string Consultancy = Prefix + "itconsultancy";
        public const string Security = Prefix + "cybersecurity";
        public const string AiLab = Prefix + "ailab";
        public const string Template = "ba:businesstype_webdevelopmentagency";
        public const string Office = "ba:buildingtype_office";

        public static readonly string[] All = { SoftwareStudio, GameStudio, Consultancy, Security, AiLab };

        private static readonly string[][] Skills =
        {
            new[] { TalentCatalogue.Programmer, TalentCatalogue.GraphicDesigner, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.GraphicDesigner, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.CustomerService, TalentCatalogue.GraphicDesigner },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.CustomerService }
        };

        private static readonly string[][] Aliases =
        {
            new[] { "Software", "SaaS", "Apps", "Startup" },
            new[] { "Games", "Game studio", "Developer" },
            new[] { "IT", "Consultancy", "Managed services" },
            new[] { "Security", "Cyber" },
            new[] { "AI", "Research", "Lab" }
        };

        public static IModLogger Logger;

        private static readonly List<BusinessType> Made = new List<BusinessType>(5);
        private static bool _registered, _furnished;

        /// <summary>True for a business of one of the tech sector's own types.</summary>
        public static bool Is(string type) => type != null && type.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>The tech sector's kind of a type: <see cref="FirmKind"/>, or -1 for a type that is not one of its own.</summary>
        public static int KindOf(string type)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == type)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Registers the types, once. Safe to call again; a type the game already holds is left as it is.</summary>
        public static void Register()
        {
            if (_registered)
            {
                Furnish();
                return;
            }
            _registered = true;
            int done = 0;
            for (int i = 0; i < All.Length; i++)
            {
                try
                {
                    if (BusinessTypeHelper.GetData(All[i]) != null)
                    {
                        done++;
                        continue;
                    }
                    BusinessType t = Make(i);
                    if (ModdingAPI.RegisterModBusinessType(t))
                    {
                        Made.Add(t);
                        done++;
                    }
                    else
                    {
                        Logger?.Warn("Tech: the game refused the business type " + All[i] + ".");
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Furnish();
            Logger?.Info("Tech: " + done + " of " + All.Length + " business types of the tech sector registered" + (_furnished ? ", furnished as the game's web development agency." : "; furnished once the game's own office types are known."));
        }

        // A type of the tech sector's: an office business the player may start, that takes money, with no customers or products of the game's.
        private static BusinessType Make(int kind)
        {
            BusinessType t = ScriptableObject.CreateInstance<BusinessType>();
            t.name = All[kind];
            t.businessTypeName = All[kind];
            t.suitableBuildingType = Office;
            t.simulator = null;
            t.spawnCustomers = false;
            t.spawnCustomersOnlyOnEnter = false;
            t.acceptCustomersWithoutOrderEntries = false;
            t.hasEntranceFee = false;
            t.hasWeekendOnlyEntranceFee = false;
            t.courseRequired = DiplomaName.OfficeBusinesses;
            t.callDialogType = (CallDialogType)0;
            t.minutesToCheckBeforeTheSpawnTimeInAiBusinesses = 60f;
            t.cityMapFilterColor = new Color(0.25f, 0.55f, 0.85f, 1f);
            t.businessProducts = Array.Empty<BusinessProduct>();
            t.productSources = Array.Empty<string>();
            t.employeePrimarySkills = Skills[kind];
            t.employeeDefaultAppearanceTags = Array.Empty<AppearanceTag>();
            t.customerType = (CustomerType)0;
            t.maxAmountPerProduct = 1f;
            t.businessRequirements = new List<BusinessRequirement>();
            t.uniforms = new List<EmployeePreset>();
            t.customerDemandSets = new List<CustomerDemandSet>();
            t.logoShapes = new List<string> { "notfound" };
            t.dayFactorMultipliers = new List<DayFactorMultiplier>();
            t.hourlyFactorMultipliers = new List<HourlyFactorMultiplier>();
            t.aliases = Aliases[kind];
            t.hasPedestriansOutside = false;
            t.hasMusicOutside = false;
            t.radioVolume = 0.15f;
            t.AddTag("ba:businesstag_allowplayercreation", null);
            t.AddTag("ba:businesstag_generatesrevenue", null);
            return t;
        }

        /// <summary>
        /// The game's own web development agency's furnishing copied onto the types: what its building must hold (but a
        /// requirement for products, which these sell none of the game's), its uniforms, its logo shapes and its icon. Once.
        /// </summary>
        public static void Furnish()
        {
            if (_furnished || Made.Count == 0)
            {
                return;
            }
            try
            {
                BusinessType like = BusinessTypeHelper.GetData(Template);
                if (like == null)
                {
                    return;
                }
                for (int i = 0; i < Made.Count; i++)
                {
                    BusinessType t = Made[i];
                    t.icon = like.icon;
                    t.cityMapFilterColor = like.cityMapFilterColor;
                    t.employeeDefaultAppearanceTags = like.employeeDefaultAppearanceTags ?? Array.Empty<AppearanceTag>();
                    t.uniforms = like.uniforms != null ? new List<EmployeePreset>(like.uniforms) : new List<EmployeePreset>();
                    t.logoShapes = like.logoShapes != null && like.logoShapes.Count > 0 ? new List<string>(like.logoShapes) : t.logoShapes;
                    t.businessRequirements.Clear();
                    for (int r = 0; like.businessRequirements != null && r < like.businessRequirements.Count; r++)
                    {
                        BusinessRequirement requirement = like.businessRequirements[r];
                        if (requirement != null && !(requirement is AnyPrimaryProduct))
                        {
                            t.businessRequirements.Add(requirement);
                        }
                    }
                }
                _furnished = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// For "prepare this save for removing the mod": every business of the tech sector's types is made a web development
        /// agency again (the game's own office business), so the save loads without the mod. Returns how many.
        /// </summary>
        public static int Strip(GameInstance save)
        {
            int n = 0;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && Is(reg.businessTypeName))
                {
                    reg.businessTypeName = Template;
                    n++;
                }
            }
            return n;
        }
    }
}
