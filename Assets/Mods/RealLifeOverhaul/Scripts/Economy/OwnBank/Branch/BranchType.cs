using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Dialogs;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank branch as a business type of the game's, registered through its mod interface for shop buildings at the game's
    /// start whatever the options, so a save that holds one finds it; its saved name is its text key. The game sends it no
    /// shoppers: its customers are the branch's own (BranchRules.Hour, Branch/BranchFloor). Dressed as the city's own banks
    /// (their icon, uniforms and logo shapes) once the game's types are known. <see cref="Strip"/> makes each a gift shop
    /// again for "prepare this save for removing the mod". Nothing per frame.
    /// </summary>
    public static class BranchType
    {
        public const string Template = "ba:businesstype_giftshop";   // what a branch becomes when the mod is taken out
        private const string Bank = "ba:businesstype_bank";             // the city's own banks: what a branch is dressed as
        private const string CustomerService = "ba:skill_customerservice";
        private const string Cleaning = "ba:skill_cleaning";

        public static IModLogger Logger;

        private static BusinessType _made;
        private static bool _registered, _dressed;

        /// <summary>True for a business of the bank branch type.</summary>
        public static bool Is(BuildingRegistration reg) => reg != null && reg.businessTypeName == BranchCatalogue.TypeName;

        /// <summary>The type, registered with the game, once; dressed when the game's own types are there.</summary>
        public static void Register()
        {
            if (_registered)
            {
                Dress();
                return;
            }
            _registered = true;
            try
            {
                if (BusinessTypeHelper.GetData(BranchCatalogue.TypeName) != null)
                {
                    return;
                }
                BusinessType t = Make();
                if (ModdingAPI.RegisterModBusinessType(t))
                {
                    _made = t;
                    Logger?.Info("Bank branch: the business type '" + BranchCatalogue.TypeName + "' is registered for shop buildings.");
                }
                else
                {
                    Logger?.Warn("Bank branch: the game did not take the business type '" + BranchCatalogue.TypeName + "' (a type of that name is already there); no branch can be opened.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Dress();
        }

        // A shop business the player may start, that takes money, with no customers or products of the game's.
        private static BusinessType Make()
        {
            BusinessType t = ScriptableObject.CreateInstance<BusinessType>();
            t.name = BranchCatalogue.TypeName;
            t.businessTypeName = BranchCatalogue.TypeName;
            t.suitableBuildingType = BranchCatalogue.BuildingType;
            t.simulator = null;                 // no simulation of shoppers: the branch's own hour does its business
            t.spawnCustomers = false;
            t.spawnCustomersOnlyOnEnter = false;
            t.acceptCustomersWithoutOrderEntries = false;
            t.hasEntranceFee = false;
            t.hasWeekendOnlyEntranceFee = false;
            t.callDialogType = (CallDialogType)0;
            t.minutesToCheckBeforeTheSpawnTimeInAiBusinesses = 60f;
            t.cityMapFilterColor = new Color(0.20f, 0.45f, 0.35f, 1f);
            t.businessProducts = Array.Empty<BusinessProduct>();
            t.productSources = Array.Empty<string>();
            t.employeePrimarySkills = new[] { CustomerService, Cleaning };
            t.employeeDefaultAppearanceTags = Array.Empty<AppearanceTag>();
            t.customerType = (CustomerType)0;
            t.maxAmountPerProduct = 1f;
            t.businessRequirements = new List<BusinessRequirement>();
            t.uniforms = new List<EmployeePreset>();
            t.customerDemandSets = new List<CustomerDemandSet>();
            t.logoShapes = new List<string> { "notfound" };
            t.dayFactorMultipliers = new List<DayFactorMultiplier>();
            t.hourlyFactorMultipliers = new List<HourlyFactorMultiplier>();
            t.aliases = new[] { "Bank branch", "Branch", "Bank", "Banking", "Teller" };
            t.hasPedestriansOutside = false;
            t.hasMusicOutside = false;
            t.radioVolume = 0.15f;
            t.AddTag("ba:businesstag_allowplayercreation", null);
            t.AddTag("ba:businesstag_generatesrevenue", null);
            return t;
        }

        /// <summary>The city's banks' icon, uniforms, look and logo shapes given to the type, once (a gift shop's where the bank has none).</summary>
        public static void Dress()
        {
            if (_dressed || _made == null)
            {
                return;
            }
            try
            {
                BusinessType bank = BusinessTypeHelper.GetData(Bank), shop = BusinessTypeHelper.GetData(Template);
                if (bank == null && shop == null)
                {
                    return;   // the game's own types are not there yet: the next city load
                }
                BusinessType icon = bank != null && bank.icon != null ? bank : shop;
                _made.icon = icon != null ? icon.icon : null;
                _made.uniforms = Pick(bank?.uniforms, shop?.uniforms);
                _made.employeeDefaultAppearanceTags = bank?.employeeDefaultAppearanceTags ?? shop?.employeeDefaultAppearanceTags ?? Array.Empty<AppearanceTag>();
                List<string> shapes = bank != null && bank.logoShapes != null && bank.logoShapes.Count > 0 ? bank.logoShapes : shop?.logoShapes;
                if (shapes != null && shapes.Count > 0)
                {
                    _made.logoShapes = new List<string>(shapes);
                }
                _dressed = _made.icon != null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static List<EmployeePreset> Pick(List<EmployeePreset> first, List<EmployeePreset> then)
        {
            List<EmployeePreset> from = first != null && first.Count > 0 ? first : then;
            return from != null ? new List<EmployeePreset>(from) : new List<EmployeePreset>();
        }

        /// <summary>For "prepare this save for removing the mod": every branch is made a gift shop again. Returns how many.</summary>
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
