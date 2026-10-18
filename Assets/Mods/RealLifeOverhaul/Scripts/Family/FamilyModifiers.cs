using System;
using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>The moods of family life, put into the game's registry of happiness modifiers as Core/ModState puts the mod's others.</summary>
    public static class FamilyModifiers
    {
        public const string Together = "reallife_happinessmodifier_together";
        public const string Engaged = "reallife_happinessmodifier_engaged";
        public const string Married = "reallife_happinessmodifier_married";
        public const string Joy = "reallife_happinessmodifier_family_joy";          // a milestone, a walk, a good evening
        public const string NewParent = "reallife_happinessmodifier_new_parent";
        public const string Tired = "reallife_happinessmodifier_broken_night";
        public const string Heartache = "reallife_happinessmodifier_heartache";
        public const string Honeymoon = "reallife_happinessmodifier_honeymoon";

        private static readonly (string type, int amount, int hours)[] Defs =
        {
            (Together, 5, 72), (Engaged, 8, 72), (Married, 10, 72), (Joy, 6, 12), (NewParent, 8, 72), (Tired, -4, 10), (Heartache, -8, 48), (Honeymoon, 12, 96)
        };

        private static bool _registered;

        public static void Register()
        {
            if (_registered && Known())
            {
                return;
            }
            try
            {
                Dictionary<string, HappinessModifier> registry = Registry();
                if (registry == null)
                {
                    return;   // not loaded yet: asked again when the save is ready
                }
                foreach ((string type, int amount, int hours) in Defs)
                {
                    if (registry.ContainsKey(type))
                    {
                        continue;
                    }
                    var modifier = ScriptableObject.CreateInstance<HappinessModifier>();
                    modifier.type = type;
                    modifier.amount = amount;
                    modifier.hoursDuration = hours;
                    modifier.maxHoursDuration = -1;
                    registry[type] = modifier;
                }
                _registered = true;
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        private static Dictionary<string, HappinessModifier> Registry()
        {
            return GameContract.HappinessRegistry.Field?.GetValue(null) as Dictionary<string, HappinessModifier>;
        }

        private static bool Known()
        {
            try
            {
                Dictionary<string, HappinessModifier> registry = Registry();
                return registry != null && registry.ContainsKey(Married);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Add(string type, int hours = -1)
        {
            if (_registered)
            {
                ModState.AddModifier(type, hours);
            }
        }

        public static void Remove(string type)
        {
            if (_registered)
            {
                ModState.RemoveModifier(type);
            }
        }

        /// <summary>Keeps a lasting mood topped up, renewed with less than a day left; called once an in-game day.</summary>
        public static void Keep(string type, bool wanted)
        {
            if (!_registered || SaveGameManager.Current == null || SaveGameManager.Current.happinessModifiers == null)
            {
                return;
            }
            List<HappinessModifierData> all = SaveGameManager.Current.happinessModifiers;
            HappinessModifierData found = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].type == type)
                {
                    found = all[i];
                    break;
                }
            }
            if (!wanted)
            {
                if (found != null)
                {
                    ModState.RemoveModifier(type);
                }
                return;
            }
            if (found == null || found.hoursLeft < 30)
            {
                ModState.AddModifier(type);
            }
        }

        public static void RemoveAll()
        {
            foreach ((string type, int _, int _) in Defs)
            {
                Remove(type);
            }
        }
    }
}
