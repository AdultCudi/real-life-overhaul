using System;
using System.Text;
using BigAmbitions.Characters.Skills;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the ladder of plans counts beside customers and wages, read from the game once for a sheet (never on a
    /// timer): the going wage of somebody new at each group of stations, how content the customers and each employee
    /// are, what the game's hourly theft takes, what a night shut risks from the mod's break-ins, and whether the
    /// cleaning service looks after the floor. Each figure says in RotaShop what it is; where the game gives none it
    /// stays at nothing and the ladder counts nothing for it.
    ///
    /// The game's arithmetic these follow (decompiled, file and line in docs/features/economy-rota.md):
    ///   theft       BusinessSecurityHelper.SimulateTheft: thieves a shelf = customers of the last hour x 0.04 x
    ///               (1 - security); under one, that is the chance one unit goes from a stocked shelf; a shelf under
    ///               a camera is passed over with the security level's chance, any other one time in twenty where a
    ///               security sign hangs. From one thief a shelf on, the game's code takes nothing
    ///   break-ins   Crime/OffscreenCrimes.BreakIn: once a night, at one of the player's shops that is shut then
    ///
    /// Cost: one pass over the business's items and employees each time a sheet is read.
    /// </summary>
    internal static partial class RotaGame
    {
        private const float ThievesPerCustomer = 0.04f, SignSkips = 0.05f;                      // BusinessSecurityHelper
        private const float BreakInChance = 0.04f, BreakInCap = 2500f, BreakInShare = 0.25f;     // Crime/OffscreenCrimes
        private const int BreakInRepair = 205;                                                   // the middle of its repair bill
        private static readonly StringBuilder Words = new StringBuilder(96);

        /// <summary>Fills the deeper figures of a sheet that has just been read. <paramref name="market"/>: the going rate against the game's wage formula, thousandths (StaffRules.Market).</summary>
        public static void ReadDeep(RotaSheet sheet, int market)
        {
            RotaShop shop = sheet.Shop;
            BuildingRegistration reg = sheet.Reg;
            if (sheet.TooLarge || reg == null)
            {
                return;
            }
            for (int w = 0; w < sheet.Workers.Count; w++)
            {
                sheet.Problem.Worker[w].Mood = Mathf.Clamp(Mathf.RoundToInt(sheet.Workers[w].satisfaction), 0, 100);
            }
            for (int s = 0; s < sheet.Stations.Count; s++)
            {
                int g = shop.Group[s];
                string[] skills = sheet.Stations[s].ItemCached.suitableSkills;
                if (shop.GroupGoing[g] == 0 && skills != null && skills.Length > 0)
                {
                    try
                    {
                        shop.GroupGoing[g] = StaffRules.Going(Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(skills[0], RotaLadder.HireSkill) * 100f), market);
                    }
                    catch (Exception)
                    {
                        shop.GroupGoing[g] = 0;   // (the station's own wage is taken)
                    }
                }
            }
            if (reg.satisfaction != null)
            {
                shop.Content = Mathf.Clamp(reg.satisfaction.overall, 0, 100);
                shop.CleanNow = Mathf.Clamp(reg.satisfaction.cleanliness, 0, 100);
            }
            try
            {
                shop.Cleaned = CompanyBridges.Cleaned != null && CompanyBridges.Cleaned(reg.Address);
            }
            catch (Exception e)
            {
                shop.Cleaned = false;
                Logger?.Warn("Rota: the cleaning service could not be asked about " + reg.BusinessName + " (" + e.Message + "); it is taken to clean for itself.");
            }
            ReadTheft(shop, reg);
        }

        private static void ReadTheft(RotaShop shop, BuildingRegistration reg)
        {
            BusinessType type = BusinessTypeHelper.GetData(reg);
            if (!shop.HasCustomers || type == null || !type.HasTag(TagRef.Businesstag.allowtheft) || reg.itemInstances == null)
            {
                return;
            }
            float security = Mathf.Clamp01(reg.securityLevelPercentage / 100f), exposed = 0f, onShelves = 0f;
            bool sign = false;
            foreach (ItemInstance item in reg.itemInstances.Values)
            {
                Item data = item != null ? item.ItemCached : null;
                if (data == null)
                {
                    continue;
                }
                sign |= data.HasTag(TagRef.Itemtag.issecuritysign);
            }
            foreach (ItemInstance item in reg.itemInstances.Values)
            {
                Item data = item != null ? item.ItemCached : null;
                if (data == null || (data.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0 || item.cargoInstances == null || item.cargoInstances.Count != 1 || item.cargoInstances[0].amount <= 0)
                {
                    continue;
                }
                float price = Mathf.Max(0f, item.cargoInstances[0].pricePerUnit);
                exposed += price * (item.isSecured ? 1f - security : sign ? 1f - SignSkips : 1f);
                onShelves += price * item.cargoInstances[0].amount;
            }
            float thieves = ThievesPerCustomer * (1f - security);
            shop.TheftRate = Mathf.RoundToInt(thieves * exposed * 100f);
            shop.TheftLimit = thieves > 0f ? Mathf.CeilToInt(1f / thieves) : 0;
            if (!Settings.CrimeAgainstPlayer)
            {
                return;
            }
            int shops = 0;
            foreach (BuildingRegistration other in Save.BuildingRegistrations)
            {
                BusinessType kind = other != null && GoingPublic.Mine(other) && other.businessTypeName != Empty ? BusinessTypeHelper.GetData(other) : null;
                shops += kind != null && kind.HasTag(TagRef.Businesstag.allowtheft) ? 1 : 0;
            }
            float exposure = 1f - security, frequency = Mathf.Lerp(0.5f, 2f, Mathf.InverseLerp(1f, 10f, Settings.CrimeFrequency));
            float loss = Mathf.Min(BreakInCap, onShelves * BreakInShare) * Mathf.Lerp(0.25f, 1f, exposure) + BreakInRepair;
            shop.NightRisk = Mathf.RoundToInt(BreakInChance * frequency * Mathf.Max(exposure, 0.08f) / Mathf.Max(1, shops) * loss * 100f);
        }

        /// <summary>Somebody's skills as the game names them, each with its value: "Customer Service 62, Cleaning 40".</summary>
        public static string Skills(EmployeeInstance e)
        {
            Words.Length = 0;
            for (int i = 0; e != null && e.characterData != null && e.characterData.skills != null && i < e.characterData.skills.Count; i++)
            {
                Skill skill = e.characterData.skills[i];
                if (skill == null)
                {
                    continue;
                }
                Words.Append(Words.Length > 0 ? ", " : string.Empty).Append(Text(skill.name)).Append(' ').Append(Mathf.RoundToInt(skill.value));
            }
            return Words.ToString();
        }
    }
}
