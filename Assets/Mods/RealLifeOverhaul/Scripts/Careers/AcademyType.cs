using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Dialogs;
using Entities;
using Helpers;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The player's own training academy, a business type registered with the game at its start whatever the options, so a
    /// save that holds one finds it; its students and fees are the mod's. <see cref="Strip"/> makes each a recruitment office.
    /// </summary>
    public static class AcademyType
    {
        public const string Type = "reallife:businesstype_academy";
        public const string Template = "ba:businesstype_recruitmentagency";
        private const string Office = "ba:buildingtype_office";

        public static IModLogger Logger;

        private static BusinessType _made;
        private static bool _registered, _furnished;

        public static bool Is(string type) => type == Type;

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
                if (BusinessTypeHelper.GetData(Type) != null)
                {
                    return;
                }
                BusinessType t = Make();
                if (ModdingAPI.RegisterModBusinessType(t))
                {
                    _made = t;
                }
                else
                {
                    Logger?.Warn("Careers: the game refused the business type " + Type + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Furnish();
        }

        private static BusinessType Make()
        {
            BusinessType t = ScriptableObject.CreateInstance<BusinessType>();
            t.name = Type;
            t.businessTypeName = Type;
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
            t.cityMapFilterColor = new Color(0.85f, 0.62f, 0.25f, 1f);
            t.businessProducts = Array.Empty<BusinessProduct>();
            t.productSources = Array.Empty<string>();
            t.employeePrimarySkills = new[] { TalentCatalogue.HrManager, TalentCatalogue.CustomerService };
            t.employeeDefaultAppearanceTags = Array.Empty<AppearanceTag>();
            t.customerType = (CustomerType)0;
            t.maxAmountPerProduct = 1f;
            t.businessRequirements = new List<BusinessRequirement>();
            t.uniforms = new List<EmployeePreset>();
            t.customerDemandSets = new List<CustomerDemandSet>();
            t.logoShapes = new List<string> { "notfound" };
            t.dayFactorMultipliers = new List<DayFactorMultiplier>();
            t.hourlyFactorMultipliers = new List<HourlyFactorMultiplier>();
            t.aliases = new[] { "Academy", "Training", "School", "Tutoring", "Courses" };
            t.hasPedestriansOutside = false;
            t.hasMusicOutside = false;
            t.radioVolume = 0.15f;
            t.AddTag("ba:businesstag_allowplayercreation", null);
            t.AddTag("ba:businesstag_generatesrevenue", null);
            return t;
        }

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
                    return;
                }
                _made.icon = like.icon;
                _made.cityMapFilterColor = like.cityMapFilterColor;
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

        /// <summary>For "prepare this save for removing the mod": every academy is made a recruitment office again. Returns how many.</summary>
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
