using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Dialogs;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction company as a business type of the game's (ModdingAPI.RegisterModBusinessType), registered at the main
    /// menu whatever the options say, since a save holding one must find its type: an office the player rents or owns and
    /// staffs through the game's own hiring, its designers and its client-facing staff at the game's own office desks. It
    /// takes no shoppers of the game's; its money is its projects'. Furnished as the game's graphic design studio once that
    /// type is known; <see cref="Strip"/> makes each one a studio again for "prepare this save for removing the mod".
    /// Nothing per frame.
    /// </summary>
    public static class ConstructionType
    {
        public const string TypeName = "reallife:businesstype_construction";
        public const string Template = "ba:businesstype_graphicdesigner";   // what a company becomes when the mod is taken out, and is furnished as
        private const string Office = "ba:buildingtype_office";
        private const string Designer = "ba:skill_graphicdesigner";
        private const string CustomerService = "ba:skill_customerservice";

        public static IModLogger Logger;

        private static BusinessType _made;
        private static bool _registered, _furnished;

        /// <summary>True for a business of the construction company's type.</summary>
        public static bool Is(BuildingRegistration reg) => reg != null && reg.businessTypeName == TypeName;

        /// <summary>Registers the type, once; furnishes it as the game's studio when that type is known. Safe to call again.</summary>
        public static void Register()
        {
            if (_registered)
            {
                Furnish();
                return;
            }
            _registered = true;
            try
            {
                if (BusinessTypeHelper.GetData(TypeName) != null)
                {
                    return;
                }
                BusinessType t = Make();
                if (ModdingAPI.RegisterModBusinessType(t))
                {
                    _made = t;
                    Logger?.Info("Construction: the business type '" + TypeName + "' is registered for office buildings.");
                }
                else
                {
                    Logger?.Warn("Construction: the game did not take the business type '" + TypeName + "' (a type of that name is already there); no construction company can be founded.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Furnish();
        }

        // An office business the player may start, that takes money, with no customers or products of the game's.
        private static BusinessType Make()
        {
            BusinessType t = ScriptableObject.CreateInstance<BusinessType>();
            t.name = TypeName;
            t.businessTypeName = TypeName;
            t.suitableBuildingType = Office;
            t.simulator = null;                 // no shoppers: the company's work is its sites
            t.spawnCustomers = false;
            t.spawnCustomersOnlyOnEnter = false;
            t.acceptCustomersWithoutOrderEntries = false;
            t.hasEntranceFee = false;
            t.hasWeekendOnlyEntranceFee = false;
            t.courseRequired = DiplomaName.OfficeBusinesses;
            t.callDialogType = (CallDialogType)0;
            t.minutesToCheckBeforeTheSpawnTimeInAiBusinesses = 60f;
            t.cityMapFilterColor = new Color(0.86f, 0.55f, 0.18f, 1f);
            t.businessProducts = Array.Empty<BusinessProduct>();
            t.productSources = Array.Empty<string>();
            t.employeePrimarySkills = new[] { Designer, CustomerService };
            t.employeeDefaultAppearanceTags = Array.Empty<AppearanceTag>();
            t.customerType = (CustomerType)0;
            t.maxAmountPerProduct = 1f;
            t.businessRequirements = new List<BusinessRequirement>();
            t.uniforms = new List<EmployeePreset>();
            t.customerDemandSets = new List<CustomerDemandSet>();
            t.logoShapes = new List<string> { "notfound" };
            t.dayFactorMultipliers = new List<DayFactorMultiplier>();
            t.hourlyFactorMultipliers = new List<HourlyFactorMultiplier>();
            t.aliases = new[] { "Construction", "Builder", "Builders", "Contractor", "Developer" };
            t.hasPedestriansOutside = false;
            t.hasMusicOutside = false;
            t.radioVolume = 0.15f;
            t.AddTag("ba:businesstag_allowplayercreation", null);
            t.AddTag("ba:businesstag_generatesrevenue", null);
            return t;
        }

        /// <summary>The studio's requirements (bar the products one), uniforms, logo shapes and icon copied onto the type, once.</summary>
        public static void Furnish()
        {
            if (_furnished || _made == null)
            {
                return;
            }
            try
            {
                BusinessType like = BusinessTypeHelper.GetData(Template);
                if (like == null)
                {
                    return;   // the game's own types are not there yet: the next city load
                }
                _made.icon = like.icon;
                _made.employeeDefaultAppearanceTags = like.employeeDefaultAppearanceTags ?? Array.Empty<AppearanceTag>();
                _made.uniforms = like.uniforms != null ? new List<EmployeePreset>(like.uniforms) : new List<EmployeePreset>();
                _made.logoShapes = like.logoShapes != null && like.logoShapes.Count > 0 ? new List<string>(like.logoShapes) : _made.logoShapes;
                _made.businessRequirements.Clear();
                for (int r = 0; like.businessRequirements != null && r < like.businessRequirements.Count; r++)
                {
                    BusinessRequirement requirement = like.businessRequirements[r];
                    if (requirement != null && !(requirement is AnyPrimaryProduct))
                    {
                        _made.businessRequirements.Add(requirement);
                    }
                }
                _furnished = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>For "prepare this save for removing the mod": every construction company becomes a design studio. Returns how many.</summary>
        public static int Strip(GameInstance save)
        {
            int n = 0;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (Is(reg))
                {
                    reg.businessTypeName = Template;
                    n++;
                }
            }
            return n;
        }
    }
}
