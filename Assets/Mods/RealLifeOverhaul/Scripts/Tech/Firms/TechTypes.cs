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
    /// The sector's five kinds of business as game business types (ModdingAPI.RegisterModBusinessType), registered at the main menu
    /// whatever the options say, since a save holding one must find its type; furnished as the web development agency.
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
            Logger?.Info("Tech: " + done + " of " + All.Length + " business types of the tech sector registered"
                         + (_furnished ? ", furnished as the game's web development agency." : "; furnished once the game's own office types are known."));
        }

        // An office business the player may start, that takes money, with no customers or products of the game's.
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

        /// <summary>Copies the web development agency's requirements (bar the products one), uniforms, logo shapes and icon onto the types, once.</summary>
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

        /// <summary>For "prepare this save for removing the mod": every business of these types becomes a web development agency. Returns how many.</summary>
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
