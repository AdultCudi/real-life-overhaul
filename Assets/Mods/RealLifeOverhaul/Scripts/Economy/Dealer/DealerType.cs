using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.Items;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The car dealership as a business type of the game's: "reallife_businesstype_cardealership", registered through
    /// the game's own mod interface (BAModAPI.ModdingAPI.RegisterModBusinessType) when the mod starts, for a retail
    /// building. From then on it is a business like any other to the game: the player founds it in BizMan, hires its
    /// staff in the game's own hiring app (customer service and cleaning are its primary skills, so the job board and
    /// the recruitment agencies offer those), sets its opening hours and shifts, pays its rent, wages and utilities; and
    /// to this mod, as it is tagged as a business that takes money, its rota, licences, insurance, campaigns, brands and
    /// business pressure reach it. The game sends it none of its own shoppers (it lists no products and no hours in
    /// which customers come: CustomerEntriesCalculator counts none for it); its customers are the dealership's own,
    /// counted from the game's own customer count for the building (DealerRules.Hour).
    ///
    /// The type's name is also its text key, which the game looks up in this mod's locale files (the game passes a mod's
    /// Locales folder to its translation). What the game can only give once a city has loaded (the vehicle icon, the
    /// logo shapes and the uniforms of a retail business of its own, the map's filter colour) is taken from the game
    /// when the city loads (<see cref="Dress"/>); before that the type has none, and nothing shows it.
    ///
    /// It stays registered while the option is off, so that a save with a dealership still loads and keeps it; with the
    /// option off it simply does not trade (Economy/Dealer/DealerDirector). The cost: nothing per frame.
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

        /// <summary>True for a business of the type's name (a place the game names by it).</summary>
        public static bool Is(string businessTypeName)
        {
            return businessTypeName == DealerCatalogue.TypeName;
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
                    _type.simulator = null;                 // no simulation of shoppers: the dealership's own hour does its business
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
                    _type.aliases = new[] { "Car dealership", "Dealership", "Cars", "Showroom" };
                    _type.cityMapFilterColor = new Color(0.16f, 0.36f, 0.62f, 1f);
                    _type.AddTag("ba:businesstag_allowplayercreation", null);
                    _type.AddTag("ba:businesstag_generatesrevenue", null);
                }
                _registered = ModdingAPI.RegisterModBusinessType(_type);
                if (_registered)
                {
                    Logger?.Info("Dealership: the business type '" + DealerCatalogue.TypeName + "' is registered for retail buildings.");
                }
                else
                {
                    Logger?.Warn("Dealership: the game did not take the business type '" + DealerCatalogue.TypeName + "' (a type of that name is already there); no dealership of this mod's can be founded.");
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

        /// <summary>
        /// A city has loaded: the type is given what the game only has now, its vehicle icon and, from a retail business of
        /// its own, the logo shapes and the uniforms that BizMan's branding and the staff's clothes choose from. Each only
        /// where the type has none yet. And the registration is made again if the game has lost it.
        /// </summary>
        public static void Dress()
        {
            Register();
            if (_type == null)
            {
                return;
            }
            try
            {
                GlobalReferences refs = InstanceBehavior<GlobalReferences>.Instance;
                if (_type.icon == null && refs != null)
                {
                    _type.icon = refs.vehiclePOIIcon;
                }
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

        /// <summary>For the self-check: whether the game has the type now.</summary>
        public static bool Registered => _registered && BusinessTypeHelper.GetData(DealerCatalogue.TypeName) != null;
    }
}
