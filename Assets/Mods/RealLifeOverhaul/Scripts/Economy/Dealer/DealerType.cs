using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Car Showroom as a business type of the game's, registered through its mod interface for retail buildings; its saved
    /// name is its text key and it stays registered with the option off. The game sends it no shoppers. Its icon is given at the
    /// game's start, before the map's filter rows take theirs; the rest when a city loads (<see cref="Dress"/>). Nothing per frame.
    /// </summary>
    public static class DealerType
    {
        private const string CustomerService = "ba:skill_customerservice";
        private const string Cleaning = "ba:skill_cleaning";
        private static readonly string[] Donors = { "ba:businesstype_electronicsstore", "ba:businesstype_appliancestore", "ba:businesstype_giftshop", "ba:businesstype_furniturestore" };

        public static IModLogger Logger;

        private static BusinessType _type;
        private static bool _registered;

        /// <summary>True for a business of the mod's own dealership type.</summary>
        public static bool Is(BuildingRegistration reg)
        {
            return reg != null && reg.businessTypeName == DealerCatalogue.TypeName;
        }

        /// <summary>The type, registered with the game, once. Safe to call again: a type already there is left as it is.</summary>
        public static void Register()
        {
            try
            {
                if (_registered && BusinessTypeHelper.GetData(DealerCatalogue.TypeName) != null)
                {
                    return;
                }
                if (_type == null)
                {
                    _type = ScriptableObject.CreateInstance<BusinessType>();
                    _type.name = DealerCatalogue.TypeName;
                    _type.businessTypeName = DealerCatalogue.TypeName;
                    _type.suitableBuildingType = DealerCatalogue.BuildingType;
                    _type.simulator = null;                 // no simulation of shoppers: the showroom's own hour does its business
                    _type.spawnCustomers = false;
                    _type.businessProducts = new BusinessProduct[0];
                    _type.productSources = new string[0];
                    _type.employeePrimarySkills = new[] { CustomerService, Cleaning };
                    _type.employeeDefaultAppearanceTags = new AppearanceTag[0];
                    _type.businessRequirements = new List<BusinessRequirement>();
                    _type.uniforms = new List<EmployeePreset>();
                    _type.customerDemandSets = new List<CustomerDemandSet>();
                    _type.logoShapes = new List<string>();
                    _type.dayFactorMultipliers = new List<DayFactorMultiplier>();
                    _type.hourlyFactorMultipliers = new List<HourlyFactorMultiplier>();
                    _type.aliases = new[] { "Car showroom", "Showroom", "Car dealership", "Dealership", "Cars" };
                    _type.cityMapFilterColor = new Color(0.16f, 0.36f, 0.62f, 1f);
                    _type.AddTag("ba:businesstag_allowplayercreation", null);
                    _type.AddTag("ba:businesstag_generatesrevenue", null);
                }
                bool icon = GiveIcon();
                _registered = ModdingAPI.RegisterModBusinessType(_type);
                if (_registered)
                {
                    Logger?.Info("Dealership: the business type '" + DealerCatalogue.TypeName + "' is registered for retail buildings, "
                                 + (icon ? "with its icon (the map's filter row has it)."
                                     : "without its icon yet: the game's references are not there; it is given when the city loads, so the map's filter row has it from the next city load."));
                }
                else
                {
                    Logger?.Warn("Dealership: the game did not take the business type '" + DealerCatalogue.TypeName
                                 + "' (a type of that name is already there); no dealership of this mod's can be founded.");
                }
            }
            catch (Exception e)
            {
                _registered = false;
                Logger?.Error(e);
            }
        }

        /// <summary>The mod is being unloaded: the type is taken off the game's list.</summary>
        public static void Unregister()
        {
            try
            {
                if (_registered)
                {
                    ModdingAPI.UnregisterModBusinessType(DealerCatalogue.TypeName);
                    _registered = false;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A city has loaded: registered again if lost, and given what it lacks from one of the game's retail types.</summary>
        public static void Dress()
        {
            Register();
            if (_type == null)
            {
                return;
            }
            try
            {
                GiveIcon();
                for (int i = 0; i < Donors.Length; i++)
                {
                    BusinessType donor = BusinessTypeHelper.GetData(Donors[i]);
                    if (donor == null)
                    {
                        continue;
                    }
                    if ((_type.logoShapes == null || _type.logoShapes.Count == 0) && donor.logoShapes != null && donor.logoShapes.Count > 0)
                    {
                        _type.logoShapes = new List<string>(donor.logoShapes);
                    }
                    if ((_type.uniforms == null || _type.uniforms.Count == 0) && donor.uniforms != null && donor.uniforms.Count > 0)
                    {
                        _type.uniforms = new List<EmployeePreset>(donor.uniforms);
                    }
                    if ((_type.employeeDefaultAppearanceTags == null || _type.employeeDefaultAppearanceTags.Length == 0) && donor.employeeDefaultAppearanceTags != null)
                    {
                        _type.employeeDefaultAppearanceTags = (AppearanceTag[])donor.employeeDefaultAppearanceTags.Clone();
                    }
                    _type.customerType = donor.customerType;
                    break;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The game's vehicle icon given to the type, where it has none yet; true when it has one.</summary>
        private static bool GiveIcon()
        {
            if (_type.icon == null)
            {
                GlobalReferences refs = InstanceBehavior<GlobalReferences>.Instance;
                if (refs != null)
                {
                    _type.icon = refs.vehiclePOIIcon;
                }
            }
            return _type.icon != null;
        }
    }
}
